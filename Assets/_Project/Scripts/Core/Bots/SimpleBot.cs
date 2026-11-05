using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    /// <summary>
    /// A quick heuristic bot: discards to reach a ready hand (聽牌), otherwise keeps connected
    /// tiles (pairs, neighbours) and throws isolated ones. Claims scoring honour pungs, and
    /// other claims by chance. Deterministic for a given seed. Not defensive.
    /// </summary>
    /// <remarks>
    /// <b>Ambition</b> (0 = take the first legal win, 1 = greedy for big hands):
    /// <list type="bullet">
    /// <item>Picks a plan from its tiles — one suit (混一色/清一色) or all pungs (對對糊) — and
    /// discards and claims toward it.</item>
    /// <item>Declines wins below a target faan that starts high (up to MinFaan + 6) and falls as
    /// the wall runs down, so hands get bigger but still finish. Limit hands are always taken.</item>
    /// <item>Chows less (a chow breaks the concealed hand and all-pungs).</item>
    /// </list>
    /// </remarks>
    public sealed class SimpleBot : IBot
    {
        private readonly SeededRandom rng;
        private readonly double chowChance;
        private readonly double pungChance;
        private readonly double ambition;

        /// <param name="chowChance">Probability of taking an available chow.</param>
        /// <param name="pungChance">Probability of taking a pung that isn't obviously good.</param>
        /// <param name="ambition">0..1: how hard the bot chases big hands (see remarks).</param>
        public SimpleBot(ulong seed, double chowChance = 0.5, double pungChance = 0.35, double ambition = 0)
        {
            rng = new SeededRandom(seed);
            this.chowChance = chowChance;
            this.pungChance = pungChance;
            this.ambition = Math.Max(0, Math.Min(1, ambition));
        }

        /// <summary>Wall tiles drawable right after a typical deal; used to scale the faan target.</summary>
        private const double FullWall = 84;

        /// <summary>The smallest win this bot will accept right now.</summary>
        public int TargetFaan(HandState hand)
        {
            int min = hand.Rules.MinFaan;
            if (ambition <= 0)
                return min;
            double wallLeft = Math.Max(0, Math.Min(1, hand.Wall.DrawableCount / FullWall));
            int extra = (int)Math.Round(ambition * 6 * wallLeft); // 3 → 9 early, back to 3 at the end
            return Math.Min(hand.Rules.MaxFaan, min + extra);
        }

        private bool WorthWinning(FaanResult score, HandState hand) =>
            ambition <= 0 || (score != null && (score.IsLimit || score.Total >= TargetFaan(hand)));

        // ------------------------------------------------------------------ plans

        private enum PlanKind { Default, OneSuit, AllPungs }

        private readonly struct Plan
        {
            public Plan(PlanKind kind, TileSuit suit)
            {
                Kind = kind;
                Suit = suit;
            }

            public PlanKind Kind { get; }
            public TileSuit Suit { get; }
            public bool Fits(Tile t) => Kind != PlanKind.OneSuit || t.IsHonor || t.Suit == Suit;
        }

        /// <summary>Which big hand the tiles lean toward (only when ambitious).</summary>
        private Plan ChoosePlan(SeatState me)
        {
            if (ambition <= 0)
                return default;

            var counts = new Dictionary<TileSuit, int> { { TileSuit.Dots, 0 }, { TileSuit.Bamboo, 0 }, { TileSuit.Characters, 0 } };
            int honors = 0;
            foreach (Tile t in me.Concealed.Concat(me.Melds.SelectMany(m => m.Tiles)))
            {
                if (t.IsSuited) counts[t.Suit]++;
                else if (t.IsHonor) honors++;
            }
            TileSuit best = counts.OrderByDescending(kv => kv.Value).First().Key;

            bool hasChow = me.Melds.Any(m => m.Type == MeldType.Chow);
            int pungish = me.Concealed.GroupBy(t => t).Count(g => g.Count() >= 2)
                + me.Melds.Count(m => m.Type != MeldType.Chow);
            bool meldsFitSuit = me.Melds.All(m => m.FirstTile.IsHonor || m.FirstTile.Suit == best);

            if (!hasChow && pungish >= 5)
                return new Plan(PlanKind.AllPungs, best);
            if (meldsFitSuit && counts[best] + honors >= 9)
                return new Plan(PlanKind.OneSuit, best);
            if (!hasChow && pungish >= 4)
                return new Plan(PlanKind.AllPungs, best);
            return default;
        }

        /// <summary>Best self-drawn faan over the tiles that would complete a 13-tile hand.</summary>
        private static int BestWaitFaan(HandState hand, SeatState me, List<Tile> thirteen, List<Tile> waits)
        {
            int best = 0;
            var ctx = new WinContext
            {
                Seat = me.Seat,
                SeatWind = me.SeatWind,
                PrevailingWind = hand.PrevailingWind,
                Melds = me.Melds,
                Bonus = me.Bonus,
                SelfDrawn = true,
            };
            var fourteen = new List<Tile>(thirteen) { default };
            foreach (Tile w in waits)
            {
                fourteen[fourteen.Count - 1] = w;
                ctx.Concealed = fourteen;
                ctx.WinningTile = w;
                FaanResult r = FaanCalculator.Calculate(ctx, hand.Rules);
                if (r != null)
                    best = Math.Max(best, r.IsLimit ? hand.Rules.MaxFaan : r.Total);
            }
            return best;
        }

        public PlayerAction Choose(HandState hand, int seat, IReadOnlyList<PlayerAction> legal)
        {
            if (legal == null || legal.Count == 0)
                throw new ArgumentException("No legal actions to choose from.", nameof(legal));

            foreach (PlayerAction a in legal)
            {
                if (a.Type != ActionType.Win && a.Type != ActionType.SelfDrawnWin)
                    continue;
                WinContext ctx = hand.PreviewWin(seat);
                if (WorthWinning(ctx != null ? FaanCalculator.Calculate(ctx, hand.Rules) : null, hand))
                    return a;
            }

            SeatState me = hand.Seats[seat];
            Plan plan = ChoosePlan(me);

            if (hand.Phase == HandPhase.AwaitingDraw)
                return ChooseDraw(hand, me, legal, plan);

            if (hand.Phase == HandPhase.AwaitingAction)
            {
                foreach (PlayerAction a in legal)
                    if (a.Type == ActionType.ConcealedKong || a.Type == ActionType.PromoteKong)
                        return a;
                return ChooseDiscard(hand, me, legal, plan);
            }

            PlayerAction pass = legal.First(a => a.Type == ActionType.Pass);

            foreach (PlayerAction a in legal)
                if (a.Type == ActionType.Kong)
                    return a;

            if (hand.LastDiscard.HasValue)
            {
                Tile discard = hand.LastDiscard.Value;
                foreach (PlayerAction a in legal)
                    if (a.Type == ActionType.Pung && ShouldPung(hand, me, discard, plan))
                        return a;

                List<PlayerAction> chows = legal.Where(a => a.Type == ActionType.Chow).ToList();
                double chance = plan.Kind == PlanKind.AllPungs ? 0
                    : plan.Kind == PlanKind.OneSuit && plan.Fits(discard) ? chowChance
                    : chowChance * (1 - ambition);
                if (chows.Count > 0 && rng.NextDouble() < chance)
                    return chows.OrderByDescending(c => HandValue(WithoutChowTiles(me.Concealed, c.Tile, discard), hand, me, plan))
                        .First();
            }
            return pass;
        }

        // ------------------------------------------------------------------ chosen draws

        /// <summary>
        /// Picks the tile kind with the best expected gain: (chance a copy is still in the wall)
        /// × (how much it improves the hand). Uses only public information — the bot's own
        /// tiles, everything on the table, and the wall's size — never the wall itself.
        /// </summary>
        private PlayerAction ChooseDraw(HandState hand, SeatState me, IReadOnlyList<PlayerAction> legal, Plan plan)
        {
            int[] visible = hand.VisibleKindCounts(me.Seat);
            int hiddenInHands = 0;
            int revealedBonus = 0;
            foreach (SeatState s in hand.Seats)
            {
                revealedBonus += s.Bonus.Count;
                if (s.Seat != me.Seat)
                    hiddenInHands += s.Concealed.Count;
            }
            int wallPlayable = Math.Max(0, hand.Wall.Remaining - (TileSet.BonusKindCount - revealedBonus) - hand.Rules.DeadWallSize);

            // Our own failed picks this hand prove those kinds are gone from the wall for good.
            var knownGone = new HashSet<Tile>();
            foreach (HandEvent e in hand.Events)
                if (e is DrawFailedEvent f && f.Seat == me.Seat)
                    knownGone.Add(f.Tile);

            var current = new List<Tile>(me.Concealed);
            double currentValue = HandValue(current, hand, me, plan);
            int target = TargetFaan(hand);

            PlayerAction best = legal[0];
            double bestScore = double.MinValue;
            double bestChance = -1;

            foreach (PlayerAction a in legal)
            {
                if (a.Type != ActionType.Draw)
                    continue;
                int unseen = TileSet.CopiesPerPlayableKind - visible[a.Tile.KindIndex];
                double chance = knownGone.Contains(a.Tile) ? 0 : ChanceInWall(unseen, hiddenInHands, wallPlayable);
                if (chance <= 0)
                    continue;

                double gain;
                WinContext win = hand.PreviewDraw(me.Seat, a.Tile);
                FaanResult score = win != null ? FaanCalculator.Calculate(win, hand.Rules) : null;
                if (score != null && (score.IsLimit || score.Total >= target))
                {
                    gain = 1000 + score.Total;
                }
                else
                {
                    current.Add(a.Tile);
                    gain = BestValueAfterDiscard(current, hand, me, plan) - currentValue;
                    current.RemoveAt(current.Count - 1);
                }

                double expected = chance * gain + 0.001 * chance; // tie-break: safer picks
                if (expected > bestScore || (expected == bestScore && chance > bestChance))
                {
                    bestScore = expected;
                    bestChance = chance;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// Probability that at least one of <paramref name="unseen"/> copies is in the wall rather
        /// than in other players' hands (hypergeometric), given the hidden tile counts.
        /// </summary>
        public static double ChanceInWall(int unseen, int hiddenInHands, int wallPlayable)
        {
            if (unseen <= 0 || wallPlayable <= 0)
                return 0;
            double allInHands = 1;
            for (int i = 0; i < unseen; i++)
            {
                double inHands = hiddenInHands - i;
                double total = hiddenInHands + wallPlayable - i;
                if (inHands <= 0 || total <= 0)
                    return 1;
                allInHands *= inHands / total;
            }
            return 1 - allInHands;
        }

        private double BestValueAfterDiscard(List<Tile> fourteen, HandState hand, SeatState me, Plan plan)
        {
            double best = double.MinValue;
            var seen = new HashSet<Tile>();
            foreach (Tile t in fourteen)
            {
                if (!seen.Add(t))
                    continue;
                List<Tile> rest = Without(fourteen, t, 1);
                best = Math.Max(best, HandValue(rest, hand, me, plan));
            }
            return best;
        }

        // ------------------------------------------------------------------ discards

        private PlayerAction ChooseDiscard(HandState hand, SeatState me, IReadOnlyList<PlayerAction> legal, Plan plan)
        {
            PlayerAction best = legal[0];
            double bestScore = double.MinValue;
            int target = TargetFaan(hand);

            foreach (PlayerAction a in legal)
            {
                if (a.Type != ActionType.Discard)
                    continue;

                List<Tile> remaining = Without(me.Concealed, a.Tile, 1);
                List<Tile> waits = FaanCalculator.GetWaits(remaining, me.Melds, hand.Rules);
                double score;
                if (waits.Count == 0)
                {
                    score = HandValue(remaining, hand, me, plan);
                }
                else if (ambition <= 0)
                {
                    score = 1000 + 10 * waits.Count;
                }
                else
                {
                    // Ready: value it by how big the win would be, not just by being ready.
                    int faan = BestWaitFaan(hand, me, remaining, waits);
                    score = faan >= target
                        ? 1000 + 10 * waits.Count + 20 * faan
                        : 60 + 5 * waits.Count + 10 * faan + HandValue(remaining, hand, me, plan);
                }

                // Tie-break: prefer throwing tiles that are less useful on their own (or off-plan).
                score -= 0.01 * KeepValue(a.Tile, hand, me) + (plan.Fits(a.Tile) ? 0 : 0.5 * ambition);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// How promising a set of tiles is: pairs, triplets and suited neighbours, weighted toward
        /// the bot's plan (one suit: on-suit tiles up, off-suit down; all pungs: pairs up,
        /// sequences down).
        /// </summary>
        private double HandValue(List<Tile> tiles, HandState hand, SeatState me, Plan plan)
        {
            double v = BaseHandValue(tiles, hand, me, plan.Kind == PlanKind.AllPungs ? 0.4 : 1.0);
            if (plan.Kind == PlanKind.OneSuit)
            {
                foreach (Tile t in tiles)
                    v += plan.Fits(t) ? 1.5 * ambition : -1.5 * ambition;
            }
            else if (plan.Kind == PlanKind.AllPungs)
            {
                foreach (var g in tiles.GroupBy(t => t))
                    v += g.Count() >= 3 ? 3 * ambition : g.Count() == 2 ? 2 * ambition : 0;
            }
            return v;
        }

        private static double BaseHandValue(List<Tile> tiles, HandState hand, SeatState me, double sequenceWeight)
        {
            var counts = new int[TileSet.PlayableKindCount];
            foreach (Tile t in tiles)
                if (!t.IsBonus)
                    counts[t.KindIndex]++;

            double v = 0;
            for (int k = 0; k < counts.Length; k++)
            {
                int c = counts[k];
                if (c == 0)
                    continue;
                if (c >= 3) v += 6;
                else if (c == 2) v += 3;

                Tile t = Tile.FromKindIndex(k);
                if (t.IsSuited)
                {
                    int rankIndex = k % 9;
                    if (rankIndex <= 7 && counts[k + 1] > 0) v += 2 * sequenceWeight;
                    if (rankIndex <= 6 && counts[k + 2] > 0) v += 1 * sequenceWeight;
                }
                else if (c >= 2 && IsScoringHonor(t, hand, me))
                {
                    v += 2;
                }
            }
            return v;
        }

        /// <summary>Value of a single tile kind: middle ranks and scoring honours are worth keeping.</summary>
        private static double KeepValue(Tile t, HandState hand, SeatState me)
        {
            if (t.IsHonor)
                return IsScoringHonor(t, hand, me) ? 3 : 0;
            int distanceFromEdge = Math.Min(t.Rank - 1, 9 - t.Rank);
            return 1 + distanceFromEdge;
        }

        // ------------------------------------------------------------------ claims

        private bool ShouldPung(HandState hand, SeatState me, Tile discard, Plan plan)
        {
            if (IsScoringHonor(discard, hand, me))
                return true;
            if (plan.Kind == PlanKind.AllPungs || (plan.Kind == PlanKind.OneSuit && plan.Fits(discard)))
                return true;
            if (plan.Kind == PlanKind.OneSuit)
                return false; // off-suit pung would break the plan
            int pairs = me.Concealed.GroupBy(t => t).Count(g => g.Count() >= 2);
            if (pairs >= 4)
                return true; // heading for all pungs
            return rng.NextDouble() < pungChance * (1 - 0.6 * ambition);
        }

        private static bool IsScoringHonor(Tile t, HandState hand, SeatState me) =>
            t.Suit == TileSuit.Dragon
            || t == Tile.WindTile(me.SeatWind)
            || t == Tile.WindTile(hand.PrevailingWind);

        // ------------------------------------------------------------------ helpers

        private static List<Tile> Without(IReadOnlyList<Tile> tiles, Tile remove, int copies)
        {
            var result = new List<Tile>(tiles);
            for (int i = 0; i < copies; i++)
                result.Remove(remove);
            return result;
        }

        private static List<Tile> WithoutChowTiles(IReadOnlyList<Tile> tiles, Tile low, Tile discard)
        {
            var result = new List<Tile>(tiles);
            for (int r = low.Rank; r <= low.Rank + 2; r++)
                if (r != discard.Rank)
                    result.Remove(new Tile(low.Suit, r));
            return result;
        }
    }
}
