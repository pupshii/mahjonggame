using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    public enum MatchPhase : byte
    {
        /// <summary>Waiting for <see cref="PartyMatch.StartHand"/>.</summary>
        BetweenHands,
        /// <summary><see cref="PartyMatch.Hand"/> is being played; call <see cref="PartyMatch.ResolveHand"/> when it ends.</summary>
        PlayingHand,
        /// <summary>Losers are eating dumplings, one at a time, via <see cref="PartyMatch.TryEat"/>.</summary>
        Roulette,
        MatchOver
    }

    /// <summary>A contestant in the match. Knocked-out seats are played by a neutral house bot.</summary>
    public sealed class PartyPlayer
    {
        internal PartyPlayer(int seat, int lives)
        {
            Seat = seat;
            Lives = lives;
            IsAlive = true;
        }

        public int Seat { get; }
        public bool IsAlive { get; internal set; }
        public int Lives { get; internal set; }
        public int DumplingsEaten { get; internal set; }
    }

    /// <summary>
    /// A whole party match: hands of mahjong, Dumpling Roulette for the losers after each
    /// hand, knockouts, and a last-player-standing winner. Owned by the host; pure rules.
    /// </summary>
    /// <remarks>
    /// Typical loop: <see cref="StartHand"/> → play <see cref="Hand"/> until it ends →
    /// <see cref="ResolveHand"/> → while <see cref="Phase"/> is Roulette, <see cref="CurrentEater"/>
    /// calls <see cref="TryEat"/> → back to BetweenHands (or MatchOver).
    /// </remarks>
    public sealed class PartyMatch
    {
        private readonly SeededRandom rng;
        private readonly PartyPlayer[] players = new PartyPlayer[HandState.SeatCount];
        private readonly Steamer[] steamers;
        private readonly List<int> eaters = new List<int>();
        private readonly List<PartyEvent> events = new List<PartyEvent>();
        private int dealerPasses;

        public PartyMatch(RuleSet rules, PartyRules party, ulong seed)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            if (party.Lives < 1)
                throw new ArgumentException("Lives must be at least 1.", nameof(party));
            rng = new SeededRandom(seed);

            for (int s = 0; s < players.Length; s++)
                players[s] = new PartyPlayer(s, party.Lives);

            steamers = party.SharedSteamer
                ? new[] { new Steamer(party.DumplingsPerSteamer, rng) }
                : Enumerable.Range(0, HandState.SeatCount).Select(_ => new Steamer(party.DumplingsPerSteamer, rng)).ToArray();

            Phase = MatchPhase.BetweenHands;
        }

        public RuleSet Rules { get; }
        public PartyRules Party { get; }
        public MatchPhase Phase { get; private set; }
        public HandState Hand { get; private set; }
        public int HandNumber { get; private set; }
        public int Dealer { get; private set; }
        public Wind PrevailingWind => (Wind)(dealerPasses / HandState.SeatCount % 4 + 1);
        public IReadOnlyList<PartyPlayer> Players => players;
        public IReadOnlyList<PartyEvent> Events => events;
        /// <summary>Set when <see cref="Phase"/> is MatchOver.</summary>
        public int Winner { get; private set; } = -1;
        public int AliveCount => players.Count(p => p.IsAlive);

        /// <summary>Seats still owed dumplings, in eating order (a seat appears once per dumpling).</summary>
        public IReadOnlyList<int> PendingEaters => eaters;
        /// <summary>Who must eat next, or -1.</summary>
        public int CurrentEater => Phase == MatchPhase.Roulette && eaters.Count > 0 ? eaters[0] : -1;

        /// <summary>The steamer <paramref name="seat"/> eats from.</summary>
        public Steamer SteamerFor(int seat) => Party.SharedSteamer ? steamers[0] : steamers[seat];

        // ------------------------------------------------------------------ hands

        /// <summary>Deals the next hand. Seats of knocked-out players are still dealt in (house bot).</summary>
        public HandState StartHand()
        {
            if (Phase != MatchPhase.BetweenHands)
                throw new InvalidOperationException($"Can't start a hand during {Phase}.");
            HandNumber++;
            Hand = HandState.Deal(Rules, Dealer, PrevailingWind, rng.NextUInt64());
            Phase = MatchPhase.PlayingHand;
            events.Add(new HandStartedEvent(HandNumber, Dealer, PrevailingWind));
            return Hand;
        }

        /// <summary>Scores the finished hand: assigns dumplings and rotates the dealer.</summary>
        public void ResolveHand()
        {
            if (Phase != MatchPhase.PlayingHand || Hand.Phase != HandPhase.Ended)
                throw new InvalidOperationException("No finished hand to resolve.");

            HandResult result = Hand.Result;
            bool[] alive = players.Select(p => p.IsAlive).ToArray();
            bool bigHand = false;
            int[] dumplings = Party.RouletteEnabled
                ? ComputeDumplings(Hand, Rules, Party, alive, out bigHand)
                : new int[HandState.SeatCount];
            events.Add(new PenaltiesAssignedEvent(dumplings, bigHand));

            // Dealer keeps the deal on a win or a draw game; otherwise it passes on.
            if (result.Outcome == HandOutcome.Win && !result.Winners.Contains(Dealer))
            {
                Dealer = HandState.NextSeat(Dealer);
                dealerPasses++;
            }

            // Eating order: turn order starting after the (first) winner, or after the dealer on a draw.
            int start = result.Outcome == HandOutcome.Win ? result.Winners[0] : Hand.Dealer;
            eaters.Clear();
            for (int i = 1; i <= HandState.SeatCount; i++)
            {
                int s = (start + i) % HandState.SeatCount;
                for (int d = 0; d < dumplings[s]; d++)
                    eaters.Add(s);
            }

            Phase = eaters.Count > 0 ? MatchPhase.Roulette : MatchPhase.BetweenHands;
        }

        /// <summary>
        /// How many dumplings each seat eats after <paramref name="hand"/>. Only living
        /// contestants eat; a knocked-out (house) seat dealing in or losing costs nobody.
        /// </summary>
        public static int[] ComputeDumplings(HandState hand, RuleSet rules, PartyRules party, IReadOnlyList<bool> alive,
            out bool bigHand)
        {
            var dumplings = new int[HandState.SeatCount];
            HandResult result = hand.Result;
            bigHand = IsBigHand(hand, rules, party);

            if (result.Outcome == HandOutcome.Win)
            {
                int count = bigHand ? party.BigHandDumplings : 1;
                if (result.SelfDrawn)
                {
                    for (int s = 0; s < dumplings.Length; s++)
                        if (alive[s] && !result.Winners.Contains(s))
                            dumplings[s] = count;
                }
                else if (alive[result.Discarder])
                {
                    dumplings[result.Discarder] = count;
                }
            }
            else if (party.DrawGameNotReadyEat)
            {
                for (int s = 0; s < dumplings.Length; s++)
                {
                    SeatState seat = hand.Seats[s];
                    if (alive[s] && FaanCalculator.GetWaits(seat.Concealed, seat.Melds, rules).Count == 0)
                        dumplings[s] = 1;
                }
            }
            return dumplings;
        }

        private static bool IsBigHand(HandState hand, RuleSet rules, PartyRules party)
        {
            if (hand.Result.Outcome != HandOutcome.Win)
                return false;
            foreach (WinContext ctx in hand.Result.WinContexts)
            {
                FaanResult score = FaanCalculator.Calculate(ctx, rules);
                if (score != null && score.Total >= party.BigHandFaan)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ roulette

        /// <summary><paramref name="seat"/> eats the dumpling in <paramref name="slot"/> of their steamer.</summary>
        public bool TryEat(int seat, int slot, out string error)
        {
            if (Phase != MatchPhase.Roulette)
            {
                error = "Nobody is eating right now.";
                return false;
            }
            if (seat != CurrentEater)
            {
                error = $"It's seat {CurrentEater}'s turn to eat.";
                return false;
            }
            Steamer steamer = SteamerFor(seat);
            if (slot < 0 || slot >= steamer.Size || steamer.IsEaten(slot))
            {
                error = $"Dumpling {slot} isn't available.";
                return false;
            }

            error = null;
            PartyPlayer player = players[seat];
            bool pepper = steamer.Eat(slot);
            player.DumplingsEaten++;
            eaters.RemoveAt(0);

            if (pepper)
                player.Lives--;
            events.Add(new DumplingEatenEvent(seat, slot, pepper, player.Lives, steamer.Remaining));

            if (pepper)
            {
                steamer.Refill(rng);
                events.Add(new SteamerRefilledEvent(Party.SharedSteamer ? -1 : seat));

                if (player.Lives <= 0)
                {
                    player.IsAlive = false;
                    eaters.RemoveAll(s => s == seat);
                    events.Add(new KnockedOutEvent(seat));
                }
            }

            if (AliveCount <= 1)
            {
                eaters.Clear();
                Winner = players.First(p => p.IsAlive).Seat;
                Phase = MatchPhase.MatchOver;
                events.Add(new MatchWonEvent(Winner));
            }
            else if (eaters.Count == 0)
            {
                Phase = MatchPhase.BetweenHands;
            }
            return true;
        }

        public void Eat(int seat, int slot)
        {
            if (!TryEat(seat, slot, out string error))
                throw new InvalidOperationException(error);
        }
    }
}
