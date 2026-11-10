using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    public enum MatchPhase : byte
    {
        /// <summary>Between hands: players may buy upgrades; call <see cref="ScoreMatch.StartHand"/>.</summary>
        BetweenHands,
        /// <summary><see cref="ScoreMatch.Hand"/> is live; call <see cref="ScoreMatch.ResolveHand"/> when it ends.</summary>
        PlayingHand,
        MatchOver
    }

    /// <summary>A player's match-level state: score, upgrades, and secret pocket.</summary>
    public sealed class MatchPlayer
    {
        private readonly int[] levels = new int[Upgrades.All.Length];
        internal readonly List<Tile> pocket = new List<Tile>();

        internal MatchPlayer(int seat, int score)
        {
            Seat = seat;
            Score = score;
        }

        public int Seat { get; }
        /// <summary>Faan balance: earned by winning, spent on upgrades, lost to penalties.</summary>
        public int Score { get; internal set; }
        /// <summary>Tiles from outside the game this player can swap in. Private to them (host-only).</summary>
        public IReadOnlyList<Tile> Pocket => pocket;
        public int CheatsCaught { get; internal set; }
        /// <summary>Knocked out of the match (e.g. died); a house bot plays their seat and they can't win.</summary>
        public bool IsOut { get; internal set; }

        public int Level(Upgrade u) => levels[(int)u];
        internal void SetLevel(Upgrade u, int level) => levels[(int)u] = level;
    }

    /// <summary>
    /// A scored match: a fixed number of hands, faan paid between players after each one, a
    /// secret pocket of outside tiles per player for cheating, call-outs, and upgrades bought
    /// with faan between hands. Pure rules; the host owns it. Who saw a cheat is decided by
    /// the presentation layer, which then calls <see cref="CatchCheater"/> or
    /// <see cref="FalseAccusation"/>.
    /// </summary>
    public sealed class ScoreMatch
    {
        private readonly SeededRandom rng;
        private readonly MatchPlayer[] players = new MatchPlayer[HandState.SeatCount];
        private readonly List<MatchEvent> events = new List<MatchEvent>();
        private int dealerPasses;
        private readonly int[] trickshot = { 1, 1, 1, 1 };

        /// <summary>The biggest trickshot multiplier allowed.</summary>
        public const int MaxTrickshot = 3;

        /// <param name="firstDealer">Who deals the first hand (East). Several tables can start with
        /// different dealers so their turns don't all line up.</param>
        public ScoreMatch(RuleSet rules, MatchRules matchRules, ulong seed, int firstDealer = 0)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            MatchRules = matchRules ?? throw new ArgumentNullException(nameof(matchRules));
            if (firstDealer < 0 || firstDealer >= HandState.SeatCount)
                throw new ArgumentOutOfRangeException(nameof(firstDealer));
            Dealer = firstDealer;
            rng = new SeededRandom(seed);
            for (int s = 0; s < players.Length; s++)
                players[s] = new MatchPlayer(s, matchRules.StartingScore);
            Phase = MatchPhase.BetweenHands;
        }

        public RuleSet Rules { get; }
        public MatchRules MatchRules { get; }
        public MatchPhase Phase { get; private set; }
        public HandState Hand { get; private set; }
        public int HandNumber { get; private set; }
        public int Dealer { get; private set; }
        public Wind PrevailingWind => (Wind)(dealerPasses / HandState.SeatCount % 4 + 1);
        public IReadOnlyList<MatchPlayer> Players => players;
        public IReadOnlyList<MatchEvent> Events => events;
        /// <summary>Highest score(s) once the match is over.</summary>
        public IReadOnlyList<int> Winners { get; private set; } = new int[0];

        // ------------------------------------------------------------------ hands

        /// <summary>Deals the next hand and fills everyone's pocket with fresh outside tiles.</summary>
        public HandState StartHand()
        {
            if (Phase != MatchPhase.BetweenHands)
                throw new InvalidOperationException($"Can't start a hand during {Phase}.");
            HandNumber++;
            for (int s = 0; s < trickshot.Length; s++)
                trickshot[s] = 1;
            Hand = HandState.Deal(Rules, Dealer, PrevailingWind, rng.NextUInt64());
            foreach (MatchPlayer p in players)
            {
                p.pocket.Clear();
                int size = Upgrades.PocketSize(MatchRules.PocketSize, p.Level(Upgrade.DeepPockets));
                for (int i = 0; i < size; i++)
                    p.pocket.Add(Tile.FromKindIndex(rng.NextInt(TileSet.PlayableKindCount)));
            }
            Phase = MatchPhase.PlayingHand;
            events.Add(new MatchHandStartedEvent(HandNumber, Dealer, PrevailingWind));
            return Hand;
        }

        /// <summary>Pays out a hand that ended normally (win or draw game).</summary>
        public void ResolveHand()
        {
            if (Phase != MatchPhase.PlayingHand || Hand.Phase != HandPhase.Ended)
                throw new InvalidOperationException("No finished hand to resolve.");
            if (Hand.Result.Outcome == HandOutcome.CheatCaught)
                throw new InvalidOperationException("Caught cheats are settled by CatchCheater.");

            HandResult result = Hand.Result;
            if (result.Outcome == HandOutcome.Win)
            {
                for (int i = 0; i < result.Winners.Count; i++)
                {
                    int winner = result.Winners[i];
                    FaanResult score = FaanCalculator.Calculate(result.WinContexts[i], Rules);
                    // Base (底, if the rules have one) plus faan; a standing trickshot stack multiplies the win.
                    int faan = (Rules.WinBase + (score?.Total ?? 0)) * trickshot[winner];
                    if (result.SelfDrawn)
                    {
                        for (int s = 0; s < players.Length; s++)
                            if (s != winner)
                                Pay(s, winner, faan, PaymentReason.SelfDrawnWin);
                    }
                    else
                    {
                        Pay(result.Discarder, winner, faan, PaymentReason.WinOnDiscard);
                    }
                }
            }

            bool dealerKeeps = result.Outcome == HandOutcome.ExhaustiveDraw || result.Winners.Contains(Dealer);
            FinishHand(dealerKeeps);
        }

        // ------------------------------------------------------------------ trickshots

        /// <summary>
        /// A player with a ready hand stacked it into a diagonal tower: if they win this hand
        /// (while it's still standing), every payment to them is multiplied. 1 = no trickshot.
        /// </summary>
        public int TrickshotMultiplier(int seat) => trickshot[seat];

        /// <summary>Sets (or, with 1, knocks down) a seat's trickshot for the current hand.</summary>
        public void SetTrickshot(int seat, int multiplier)
        {
            if (Phase != MatchPhase.PlayingHand)
                throw new InvalidOperationException("No hand in progress.");
            trickshot[seat] = Math.Max(1, Math.Min(MaxTrickshot, multiplier));
        }

        // ------------------------------------------------------------------ cheating

        /// <summary>
        /// Completes a cheat: <paramref name="fromHand"/> goes into the pocket slot
        /// <paramref name="pocketIndex"/>, and the tile that was there goes into the hand.
        /// </summary>
        public bool TryPocketSwap(int seat, Tile fromHand, int pocketIndex, out string error)
        {
            if (Phase != MatchPhase.PlayingHand)
            {
                error = "No hand in progress.";
                return false;
            }
            List<Tile> pocket = players[seat].pocket;
            if (pocketIndex < 0 || pocketIndex >= pocket.Count)
            {
                error = "No such pocket tile.";
                return false;
            }
            if (!Hand.TryPocketSwap(seat, fromHand, pocket[pocketIndex], out error))
                return false;
            pocket[pocketIndex] = fromHand;
            return true;
        }

        /// <summary>
        /// <paramref name="caller"/> caught <paramref name="cheater"/> mid-cheat: the hand ends at
        /// once and the cheater pays <see cref="MatchRules.CheatPenalty"/> to each other player.
        /// </summary>
        public void CatchCheater(int cheater, int caller)
        {
            if (Phase != MatchPhase.PlayingHand || Hand.Phase == HandPhase.Ended)
                throw new InvalidOperationException("No live hand to stop.");
            Hand.CatchCheater(cheater);
            players[cheater].CheatsCaught++;
            events.Add(new CheaterCaughtEvent(cheater, caller));
            for (int s = 0; s < players.Length; s++)
                if (s != cheater)
                    Pay(cheater, s, MatchRules.CheatPenalty, PaymentReason.CheatPenalty);
            FinishHand(dealerKeeps: cheater != Dealer);
        }

        /// <summary>
        /// Takes <paramref name="seat"/> out of the match (they died). Their seat keeps playing as a
        /// house bot; their score stays on the board but they can't be a winner.
        /// </summary>
        public void Eliminate(int seat, string reason)
        {
            if (players[seat].IsOut)
                return;
            players[seat].IsOut = true;
            events.Add(new PlayerOutEvent(seat, reason));
        }

        /// <summary><paramref name="caller"/> called out <paramref name="accused"/>, who wasn't cheating.</summary>
        public void FalseAccusation(int caller, int accused)
        {
            if (Phase != MatchPhase.PlayingHand)
                throw new InvalidOperationException("No hand in progress.");
            events.Add(new FalseAccusationEvent(caller, accused));
            Pay(caller, accused, MatchRules.FalseAccusationFine, PaymentReason.FalseAccusation);
        }

        /// <summary>
        /// Moves faan between players outside of mahjong (e.g. a customer tipping the manager for
        /// food, or a refund for a late order). Works in any phase.
        /// </summary>
        public void Transfer(int from, int to, int amount, PaymentReason reason) => Pay(from, to, amount, reason);

        // ------------------------------------------------------------------ upgrades

        public bool CanBuy(int seat, Upgrade upgrade, out int cost)
        {
            int next = players[seat].Level(upgrade) + 1;
            cost = Upgrades.Cost(upgrade, next);
            return Phase == MatchPhase.BetweenHands && next <= Upgrades.MaxLevel && players[seat].Score >= cost;
        }

        public bool TryBuy(int seat, Upgrade upgrade, out string error)
        {
            MatchPlayer p = players[seat];
            if (Phase != MatchPhase.BetweenHands)
            {
                error = "Upgrades can only be bought between hands.";
                return false;
            }
            int next = p.Level(upgrade) + 1;
            if (next > Upgrades.MaxLevel)
            {
                error = $"{Upgrades.Name(upgrade)} is already maxed out.";
                return false;
            }
            int cost = Upgrades.Cost(upgrade, next);
            if (p.Score < cost)
            {
                error = $"{Upgrades.Name(upgrade)} {next} costs {cost} faan; you have {p.Score}.";
                return false;
            }
            error = null;
            p.Score -= cost;
            p.SetLevel(upgrade, next);
            events.Add(new UpgradeBoughtEvent(seat, upgrade, next, cost));
            return true;
        }

        // ------------------------------------------------------------------ helpers

        private void Pay(int from, int to, int amount, PaymentReason reason)
        {
            if (amount <= 0 || from == to)
                return;
            players[from].Score -= amount;
            players[to].Score += amount;
            events.Add(new PaymentEvent(from, to, amount, reason));
        }

        private void FinishHand(bool dealerKeeps)
        {
            if (!dealerKeeps)
            {
                Dealer = HandState.NextSeat(Dealer);
                dealerPasses++;
            }

            if (HandNumber >= MatchRules.HandsPerMatch)
            {
                var standing = players.Where(p => !p.IsOut).ToList();
                if (standing.Count == 0)
                    standing = players.ToList();
                int best = standing.Max(p => p.Score);
                Winners = standing.Where(p => p.Score == best).Select(p => p.Seat).ToList();
                Phase = MatchPhase.MatchOver;
                events.Add(new MatchOverEvent(Winners));
            }
            else
            {
                Phase = MatchPhase.BetweenHands;
            }
        }
    }
}
