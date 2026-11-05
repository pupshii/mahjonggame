using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// One hand of Hong Kong mahjong, from the deal to a win or draw game.
    /// The host owns this object; players submit <see cref="PlayerAction"/>s through
    /// <see cref="TryApply"/>, which rejects anything not in <see cref="GetLegalActions"/>.
    /// </summary>
    /// <remarks>
    /// Seats are 0-3 in turn order (play passes 0 → 1 → 2 → 3 → 0).
    /// Claim resolution: once every eligible player has responded to a discard,
    /// Win beats Pung/Kong beats Chow. Players whose only option is Pass are passed
    /// automatically; the host should still wait a moment before revealing the
    /// result so timing doesn't leak that information.
    /// </remarks>
    public sealed class HandState
    {
        public const int SeatCount = 4;
        public const int StartingHandSize = 13;

        private readonly IWinValidator winValidator;
        private readonly SeatState[] seats = new SeatState[SeatCount];
        private readonly PlayerAction?[] responses = new PlayerAction?[SeatCount];
        private readonly List<HandEvent> events = new List<HandEvent>();

        private bool mustDiscardOnly;          // after claiming a chow/pung
        private bool lastDrawWasKongReplacement;
        private Tile pendingKongTile;          // during AwaitingRobKong

        private HandState(RuleSet rules, int dealer, Wind prevailingWind, Wall wall, IWinValidator winValidator)
        {
            if (dealer < 0 || dealer >= SeatCount)
                throw new ArgumentOutOfRangeException(nameof(dealer));

            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Dealer = dealer;
            PrevailingWind = prevailingWind;
            Wall = wall;
            this.winValidator = winValidator ?? NeverWinValidator.Instance;

            for (int i = 0; i < SeatCount; i++)
                seats[i] = new SeatState(i, (Wind)(((i - dealer + SeatCount) % SeatCount) + 1));

            DealStartingHands();
            ReplaceStartingBonusTiles();

            CurrentSeat = dealer;
            Phase = HandPhase.AwaitingAction;
        }

        /// <summary>Starts a hand with a wall shuffled from <paramref name="seed"/>.</summary>
        public static HandState Deal(RuleSet rules, int dealer, Wind prevailingWind, ulong seed,
            IWinValidator winValidator = null)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            return new HandState(rules, dealer, prevailingWind, Wall.CreateShuffled(seed, rules.DeadWallSize), winValidator);
        }

        /// <summary>
        /// Starts a hand from an exact wall order (front first). Must be a complete 144-tile set.
        /// For tests, replays and scripted tutorials.
        /// </summary>
        public static HandState DealFromWall(RuleSet rules, int dealer, Wind prevailingWind, IReadOnlyList<Tile> wallOrder,
            IWinValidator winValidator = null)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (wallOrder == null)
                throw new ArgumentNullException(nameof(wallOrder));
            ValidateFullSet(wallOrder);
            return new HandState(rules, dealer, prevailingWind, new Wall(wallOrder, rules.DeadWallSize), winValidator);
        }

        public RuleSet Rules { get; }
        public int Dealer { get; }
        public Wind PrevailingWind { get; }
        /// <summary>Host-only: clients must never receive the wall order.</summary>
        public Wall Wall { get; }
        public HandPhase Phase { get; private set; }
        /// <summary>Seat whose turn it is (or who just discarded / is promoting a kong).</summary>
        public int CurrentSeat { get; private set; }
        public IReadOnlyList<SeatState> Seats => seats;
        /// <summary>The discard being claimed, during <see cref="HandPhase.AwaitingClaims"/>.</summary>
        public Tile? LastDiscard { get; private set; }
        public int LastDiscardSeat { get; private set; } = -1;
        /// <summary>Tile the current seat just drew, or null after a chow/pung claim.</summary>
        public Tile? LastDrawnTile { get; private set; }
        /// <summary>Set once <see cref="Phase"/> is <see cref="HandPhase.Ended"/>.</summary>
        public HandResult Result { get; private set; }
        /// <summary>Everything that has happened this hand, in order.</summary>
        public IReadOnlyList<HandEvent> Events => events;

        public static int NextSeat(int seat) => (seat + 1) % SeatCount;

        // ------------------------------------------------------------------ queries

        /// <summary>Everything <paramref name="seat"/> may do right now (empty if nothing).</summary>
        public List<PlayerAction> GetLegalActions(int seat)
        {
            var actions = new List<PlayerAction>();
            if (seat < 0 || seat >= SeatCount)
                return actions;

            switch (Phase)
            {
                case HandPhase.AwaitingAction:
                    if (seat == CurrentSeat)
                        AddTurnActions(seats[seat], actions);
                    break;

                case HandPhase.AwaitingClaims:
                    if (seat != LastDiscardSeat && responses[seat] == null)
                        AddClaimActions(seats[seat], actions);
                    break;

                case HandPhase.AwaitingRobKong:
                    if (seat != CurrentSeat && responses[seat] == null)
                    {
                        actions.Add(PlayerAction.Pass(seat));
                        if (WouldWinWith(seats[seat], pendingKongTile))
                            actions.Add(PlayerAction.Win(seat));
                    }
                    break;
            }
            return actions;
        }

        /// <summary>Seats that still owe a response in the current claim window.</summary>
        public List<int> SeatsAwaitingResponse()
        {
            var result = new List<int>();
            if (Phase == HandPhase.AwaitingClaims || Phase == HandPhase.AwaitingRobKong)
            {
                for (int s = 0; s < SeatCount; s++)
                    if (s != CurrentSeat && responses[s] == null)
                        result.Add(s);
            }
            return result;
        }

        private void AddTurnActions(SeatState seat, List<PlayerAction> actions)
        {
            var seen = new HashSet<Tile>();
            foreach (Tile t in seat.concealed)
                if (seen.Add(t))
                    actions.Add(PlayerAction.Discard(seat.Seat, t));

            if (mustDiscardOnly)
                return;

            if (Wall.CanDraw)
            {
                foreach (Tile t in seen)
                    if (seat.CountConcealed(t) == 4)
                        actions.Add(PlayerAction.ConcealedKong(seat.Seat, t));

                foreach (Meld m in seat.melds)
                    if (m.Type == MeldType.Pung && seat.CountConcealed(m.FirstTile) > 0)
                        actions.Add(PlayerAction.PromoteKong(seat.Seat, m.FirstTile));
            }

            if (winValidator.IsWinningHand(seat.concealed, seat.melds))
                actions.Add(PlayerAction.SelfDrawnWin(seat.Seat));
        }

        private void AddClaimActions(SeatState seat, List<PlayerAction> actions)
        {
            Tile discard = LastDiscard.Value;
            int s = seat.Seat;
            actions.Add(PlayerAction.Pass(s));

            if (WouldWinWith(seat, discard))
                actions.Add(PlayerAction.Win(s));

            int have = seat.CountConcealed(discard);
            if (have >= 2)
                actions.Add(PlayerAction.Pung(s));
            if (have == 3 && Wall.CanDraw)
                actions.Add(PlayerAction.Kong(s));

            if (s == NextSeat(LastDiscardSeat) && discard.IsSuited)
            {
                for (int low = discard.Rank - 2; low <= discard.Rank; low++)
                {
                    if (low < 1 || low + 2 > 9)
                        continue;
                    bool ok = true;
                    for (int r = low; r <= low + 2 && ok; r++)
                        if (r != discard.Rank && seat.CountConcealed(new Tile(discard.Suit, r)) == 0)
                            ok = false;
                    if (ok)
                        actions.Add(PlayerAction.Chow(s, new Tile(discard.Suit, low)));
                }
            }
        }

        private bool WouldWinWith(SeatState seat, Tile tile)
        {
            var hand = new List<Tile>(seat.concealed) { tile };
            return winValidator.IsWinningHand(hand, seat.melds);
        }

        // ------------------------------------------------------------------ commands

        /// <summary>Applies <paramref name="action"/> if legal. Returns false with a reason otherwise.</summary>
        public bool TryApply(PlayerAction action, out string error)
        {
            if (Phase == HandPhase.Ended)
            {
                error = "The hand is over.";
                return false;
            }
            if (!GetLegalActions(action.Seat).Contains(action))
            {
                error = $"{action} is not legal now (phase {Phase}, current seat {CurrentSeat}).";
                return false;
            }

            error = null;
            switch (action.Type)
            {
                case ActionType.Discard:
                    DoDiscard(action.Seat, action.Tile);
                    break;
                case ActionType.ConcealedKong:
                    DoConcealedKong(action.Seat, action.Tile);
                    break;
                case ActionType.PromoteKong:
                    DoPromoteKong(action.Seat, action.Tile);
                    break;
                case ActionType.SelfDrawnWin:
                    EndWithWin(new[] { action.Seat }, -1, LastDrawnTile, selfDrawn: true, robbedKong: false,
                        kongReplacement: lastDrawWasKongReplacement);
                    break;
                default:
                    responses[action.Seat] = action;
                    ResolveIfComplete();
                    break;
            }
            return true;
        }

        /// <summary>Applies <paramref name="action"/>; throws if it is not legal.</summary>
        public void Apply(PlayerAction action)
        {
            if (!TryApply(action, out string error))
                throw new InvalidOperationException(error);
        }

        private void DoDiscard(int seat, Tile tile)
        {
            SeatState s = seats[seat];
            s.RemoveConcealed(tile, 1);
            s.discards.Add(tile);
            LastDiscard = tile;
            LastDiscardSeat = seat;
            LastDrawnTile = null;
            mustDiscardOnly = false;
            events.Add(new TileDiscardedEvent(seat, tile));
            OpenResponseWindow(HandPhase.AwaitingClaims);
        }

        private void DoConcealedKong(int seat, Tile tile)
        {
            SeatState s = seats[seat];
            s.RemoveConcealed(tile, 4);
            Meld meld = Meld.ConcealedKong(tile);
            s.melds.Add(meld);
            events.Add(new MeldDeclaredEvent(seat, meld));
            StartTurn(seat, kongReplacement: true);
        }

        private void DoPromoteKong(int seat, Tile tile)
        {
            pendingKongTile = tile;
            OpenResponseWindow(HandPhase.AwaitingRobKong);
        }

        private void OpenResponseWindow(HandPhase phase)
        {
            Phase = phase;
            for (int s = 0; s < SeatCount; s++)
                responses[s] = null;

            // Auto-pass anyone whose only option is Pass.
            for (int s = 0; s < SeatCount; s++)
            {
                if (s == CurrentSeat)
                    continue;
                List<PlayerAction> legal = GetLegalActions(s);
                if (legal.Count == 1 && legal[0].Type == ActionType.Pass)
                    responses[s] = legal[0];
            }
            ResolveIfComplete();
        }

        private void ResolveIfComplete()
        {
            if (SeatsAwaitingResponse().Count > 0)
                return;

            if (Phase == HandPhase.AwaitingClaims)
                ResolveClaims();
            else if (Phase == HandPhase.AwaitingRobKong)
                ResolveRobKong();
        }

        /// <summary>Seats in turn order after <paramref name="from"/> that responded with <paramref name="type"/>.</summary>
        private List<int> RespondersInTurnOrder(int from, ActionType type)
        {
            var result = new List<int>();
            for (int i = 1; i < SeatCount; i++)
            {
                int s = (from + i) % SeatCount;
                if (responses[s] is PlayerAction a && a.Type == type)
                    result.Add(s);
            }
            return result;
        }

        private void ResolveClaims()
        {
            int discarder = LastDiscardSeat;
            Tile tile = LastDiscard.Value;

            List<int> winners = RespondersInTurnOrder(discarder, ActionType.Win);
            if (winners.Count > 0)
            {
                if (!Rules.AllowMultipleWinners)
                    winners.RemoveRange(1, winners.Count - 1);
                TakeDiscard(discarder);
                seats[winners[0]].concealed.Add(tile); // physically, the nearest winner takes it
                EndWithWin(winners, discarder, tile, selfDrawn: false, robbedKong: false, kongReplacement: false);
                return;
            }

            List<int> kongs = RespondersInTurnOrder(discarder, ActionType.Kong);
            List<int> pungs = RespondersInTurnOrder(discarder, ActionType.Pung);
            List<int> chows = RespondersInTurnOrder(discarder, ActionType.Chow);

            if (kongs.Count > 0)
            {
                int s = kongs[0];
                TakeDiscard(discarder);
                seats[s].RemoveConcealed(tile, 3);
                DeclareMeld(s, Meld.ExposedKong(tile, discarder));
                StartTurn(s, kongReplacement: true);
            }
            else if (pungs.Count > 0)
            {
                int s = pungs[0];
                TakeDiscard(discarder);
                seats[s].RemoveConcealed(tile, 2);
                DeclareMeld(s, Meld.Pung(tile, discarder));
                BeginDiscardOnlyTurn(s);
            }
            else if (chows.Count > 0)
            {
                int s = chows[0];
                Tile low = responses[s].Value.Tile;
                TakeDiscard(discarder);
                for (int r = low.Rank; r <= low.Rank + 2; r++)
                    if (r != tile.Rank)
                        seats[s].RemoveConcealed(new Tile(tile.Suit, r), 1);
                DeclareMeld(s, Meld.Chow(low, discarder, tile));
                BeginDiscardOnlyTurn(s);
            }
            else
            {
                LastDiscard = null;
                StartTurn(NextSeat(discarder), kongReplacement: false);
            }
        }

        private void ResolveRobKong()
        {
            int promoter = CurrentSeat;
            Tile tile = pendingKongTile;

            List<int> winners = RespondersInTurnOrder(promoter, ActionType.Win);
            if (winners.Count > 0)
            {
                if (!Rules.AllowMultipleWinners)
                    winners.RemoveRange(1, winners.Count - 1);
                seats[promoter].RemoveConcealed(tile, 1);
                seats[winners[0]].concealed.Add(tile);
                EndWithWin(winners, promoter, tile, selfDrawn: false, robbedKong: true, kongReplacement: false);
                return;
            }

            SeatState s = seats[promoter];
            int index = s.melds.FindIndex(m => m.Type == MeldType.Pung && m.FirstTile == tile);
            s.RemoveConcealed(tile, 1);
            s.melds[index] = s.melds[index].PromoteToKong();
            events.Add(new MeldDeclaredEvent(promoter, s.melds[index]));
            StartTurn(promoter, kongReplacement: true);
        }

        private void TakeDiscard(int discarder)
        {
            List<Tile> pile = seats[discarder].discards;
            pile.RemoveAt(pile.Count - 1);
            LastDiscard = null;
        }

        private void DeclareMeld(int seat, Meld meld)
        {
            seats[seat].melds.Add(meld);
            events.Add(new MeldDeclaredEvent(seat, meld));
        }

        private void BeginDiscardOnlyTurn(int seat)
        {
            CurrentSeat = seat;
            LastDrawnTile = null;
            lastDrawWasKongReplacement = false;
            mustDiscardOnly = true;
            Phase = HandPhase.AwaitingAction;
        }

        /// <summary>Gives <paramref name="seat"/> the turn and draws a tile (or ends the hand if the wall is empty).</summary>
        private void StartTurn(int seat, bool kongReplacement)
        {
            CurrentSeat = seat;
            mustDiscardOnly = false;
            if (!DrawInto(seat, fromBack: kongReplacement))
                return;
            lastDrawWasKongReplacement = kongReplacement;
            Phase = HandPhase.AwaitingAction;
        }

        /// <summary>Draws one playable tile, setting aside bonus tiles. False if the wall ran out (hand ended).</summary>
        private bool DrawInto(int seat, bool fromBack)
        {
            while (true)
            {
                if (!Wall.CanDraw)
                {
                    EndAsDraw();
                    return false;
                }

                Tile tile = fromBack ? Wall.DrawBack() : Wall.DrawFront();
                events.Add(new TileDrawnEvent(seat, tile, fromBack));

                if (tile.IsBonus)
                {
                    seats[seat].bonus.Add(tile);
                    events.Add(new BonusTileRevealedEvent(seat, tile));
                    fromBack = true;
                    continue;
                }

                seats[seat].concealed.Add(tile);
                LastDrawnTile = tile;
                return true;
            }
        }

        private void EndWithWin(IReadOnlyList<int> winners, int discarder, Tile? tile, bool selfDrawn, bool robbedKong,
            bool kongReplacement)
        {
            Phase = HandPhase.Ended;
            Result = new HandResult(HandOutcome.Win, winners, discarder, tile, selfDrawn, robbedKong,
                kongReplacementWin: selfDrawn && kongReplacement, lastTileWin: !Wall.CanDraw);
            events.Add(new HandEndedEvent(Result));
        }

        private void EndAsDraw()
        {
            Phase = HandPhase.Ended;
            Result = HandResult.Draw();
            events.Add(new HandEndedEvent(Result));
        }

        // ------------------------------------------------------------------ setup

        private void DealStartingHands()
        {
            // Real dealing pattern: three passes of 4 tiles, one pass of 1 tile, then the
            // dealer takes a 14th. Starts with the dealer, in turn order.
            for (int pass = 0; pass < 4; pass++)
            {
                int count = pass < 3 ? 4 : 1;
                for (int i = 0; i < SeatCount; i++)
                {
                    int seat = (Dealer + i) % SeatCount;
                    for (int k = 0; k < count; k++)
                        seats[seat].concealed.Add(Wall.DrawFront());
                }
            }
            Tile extra = Wall.DrawFront();
            seats[Dealer].concealed.Add(extra);
            LastDrawnTile = extra;
        }

        private void ReplaceStartingBonusTiles()
        {
            for (int i = 0; i < SeatCount; i++)
            {
                SeatState s = seats[(Dealer + i) % SeatCount];
                while (true)
                {
                    int index = s.concealed.FindIndex(t => t.IsBonus);
                    if (index < 0)
                        break;
                    Tile bonus = s.concealed[index];
                    s.concealed.RemoveAt(index);
                    s.bonus.Add(bonus);
                    events.Add(new BonusTileRevealedEvent(s.Seat, bonus));

                    Tile replacement = Wall.DrawBack();
                    events.Add(new TileDrawnEvent(s.Seat, replacement, fromBack: true));
                    s.concealed.Add(replacement);
                }
            }
        }

        private static void ValidateFullSet(IReadOnlyList<Tile> tiles)
        {
            if (tiles.Count != TileSet.TotalTiles)
                throw new ArgumentException($"Wall must have {TileSet.TotalTiles} tiles, got {tiles.Count}.");
            var counts = new int[TileSet.KindCount];
            foreach (Tile t in tiles)
            {
                if (!t.IsValid)
                    throw new ArgumentException($"Wall contains an invalid tile ({t.Suit} {t.Rank}).");
                counts[t.KindIndex]++;
            }
            for (int k = 0; k < TileSet.KindCount; k++)
            {
                int expected = k < TileSet.PlayableKindCount ? TileSet.CopiesPerPlayableKind : 1;
                if (counts[k] != expected)
                    throw new ArgumentException($"Wall has {counts[k]} × {Tile.FromKindIndex(k)}, expected {expected}.");
            }
        }
    }
}
