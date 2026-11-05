using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    /// <summary>
    /// A quick heuristic bot: always wins when it can, discards to reach a ready hand (聽牌)
    /// with the most waits, otherwise keeps connected tiles (pairs, neighbours) and throws
    /// isolated ones. Claims scoring honour pungs, and other claims by chance.
    /// Deterministic for a given seed. Good enough to fill seats; not defensive.
    /// </summary>
    public sealed class SimpleBot : IBot
    {
        private readonly SeededRandom rng;
        private readonly double chowChance;
        private readonly double pungChance;

        /// <param name="chowChance">Probability of taking an available chow.</param>
        /// <param name="pungChance">Probability of taking a pung that isn't obviously good.</param>
        public SimpleBot(ulong seed, double chowChance = 0.5, double pungChance = 0.35)
        {
            rng = new SeededRandom(seed);
            this.chowChance = chowChance;
            this.pungChance = pungChance;
        }

        public PlayerAction Choose(HandState hand, int seat, IReadOnlyList<PlayerAction> legal)
        {
            if (legal == null || legal.Count == 0)
                throw new ArgumentException("No legal actions to choose from.", nameof(legal));

            foreach (PlayerAction a in legal)
                if (a.Type == ActionType.Win || a.Type == ActionType.SelfDrawnWin)
                    return a;

            SeatState me = hand.Seats[seat];

            if (hand.Phase == HandPhase.AwaitingDraw)
                return ChooseDraw(hand, me, legal);

            if (hand.Phase == HandPhase.AwaitingAction)
            {
                foreach (PlayerAction a in legal)
                    if (a.Type == ActionType.ConcealedKong || a.Type == ActionType.PromoteKong)
                        return a;
                return ChooseDiscard(hand, me, legal);
            }

            PlayerAction pass = legal.First(a => a.Type == ActionType.Pass);

            foreach (PlayerAction a in legal)
                if (a.Type == ActionType.Kong)
                    return a;

            if (hand.LastDiscard.HasValue)
            {
                Tile discard = hand.LastDiscard.Value;
                foreach (PlayerAction a in legal)
                    if (a.Type == ActionType.Pung && ShouldPung(hand, me, discard))
                        return a;

                List<PlayerAction> chows = legal.Where(a => a.Type == ActionType.Chow).ToList();
                if (chows.Count > 0 && rng.NextDouble() < chowChance)
                    return chows.OrderByDescending(c => HandValue(WithoutChowTiles(me.Concealed, c.Tile, discard), hand, me))
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
        private PlayerAction ChooseDraw(HandState hand, SeatState me, IReadOnlyList<PlayerAction> legal)
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
            double currentValue = HandValue(current, hand, me);

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
                if (score != null && score.Total >= hand.Rules.MinFaan)
                {
                    gain = 1000 + score.Total;
                }
                else
                {
                    current.Add(a.Tile);
                    gain = BestValueAfterDiscard(current, hand, me) - currentValue;
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

        private static double BestValueAfterDiscard(List<Tile> fourteen, HandState hand, SeatState me)
        {
            double best = double.MinValue;
            var seen = new HashSet<Tile>();
            foreach (Tile t in fourteen)
            {
                if (!seen.Add(t))
                    continue;
                List<Tile> rest = Without(fourteen, t, 1);
                best = Math.Max(best, HandValue(rest, hand, me));
            }
            return best;
        }

        // ------------------------------------------------------------------ discards

        private PlayerAction ChooseDiscard(HandState hand, SeatState me, IReadOnlyList<PlayerAction> legal)
        {
            PlayerAction best = legal[0];
            double bestScore = double.MinValue;

            foreach (PlayerAction a in legal)
            {
                if (a.Type != ActionType.Discard)
                    continue;

                List<Tile> remaining = Without(me.Concealed, a.Tile, 1);
                List<Tile> waits = FaanCalculator.GetWaits(remaining, me.Melds, hand.Rules);
                double score = waits.Count > 0
                    ? 1000 + 10 * waits.Count
                    : HandValue(remaining, hand, me);

                // Tie-break: prefer throwing tiles that are less useful on their own.
                score -= 0.01 * KeepValue(a.Tile, hand, me);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>How connected a set of tiles is: pairs, triplets, and suited neighbours.</summary>
        private static double HandValue(List<Tile> tiles, HandState hand, SeatState me)
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
                    if (rankIndex <= 7 && counts[k + 1] > 0) v += 2;
                    if (rankIndex <= 6 && counts[k + 2] > 0) v += 1;
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

        private bool ShouldPung(HandState hand, SeatState me, Tile discard)
        {
            if (IsScoringHonor(discard, hand, me))
                return true;
            int pairs = me.Concealed.GroupBy(t => t).Count(g => g.Count() >= 2);
            if (pairs >= 4)
                return true; // heading for all pungs
            return rng.NextDouble() < pungChance;
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
