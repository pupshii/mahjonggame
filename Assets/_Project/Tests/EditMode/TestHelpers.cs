using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core.Tests
{
    /// <summary>Builds rigged walls so tests can choose exact starting hands and draws.</summary>
    internal static class TestWall
    {
        /// <param name="dealer">Dealer seat.</param>
        /// <param name="hands">Four hands as tile codes, indexed by seat; the dealer's has 14 tiles, others 13.</param>
        /// <param name="frontDraws">Tiles drawn from the front after the deal, in order.</param>
        /// <param name="backDraws">Replacement tiles drawn from the back, in order.</param>
        public static List<Tile> Build(int dealer, string[] hands, string frontDraws = "", string backDraws = "")
        {
            if (hands.Length != 4)
                throw new ArgumentException("Need four hands.");

            List<Tile>[] parsed = hands.Select(Tile.ParseList).ToArray();
            for (int s = 0; s < 4; s++)
            {
                int expected = s == dealer ? 14 : 13;
                if (parsed[s].Count != expected)
                    throw new ArgumentException($"Seat {s} hand has {parsed[s].Count} tiles, expected {expected}.");
            }

            var order = new List<Tile>();
            for (int pass = 0; pass < 4; pass++)
            {
                int count = pass < 3 ? 4 : 1;
                for (int i = 0; i < 4; i++)
                {
                    int seat = (dealer + i) % 4;
                    order.AddRange(parsed[seat].Skip(pass * 4).Take(count));
                }
            }
            order.Add(parsed[dealer][13]);
            order.AddRange(Tile.ParseList(frontDraws));

            List<Tile> back = Tile.ParseList(backDraws);

            // Filler: everything not yet used. Bonus tiles go in the middle, away from
            // the ends, so they are never drawn unless a test asks for them.
            var counts = new int[TileSet.KindCount];
            foreach (Tile t in TileSet.CreateFull())
                counts[t.KindIndex]++;
            foreach (Tile t in order.Concat(back))
            {
                if (--counts[t.KindIndex] < 0)
                    throw new ArgumentException($"Too many copies of {t} in the rigged wall.");
            }

            var playable = new List<Tile>();
            var bonus = new List<Tile>();
            for (int k = 0; k < TileSet.KindCount; k++)
                for (int c = 0; c < counts[k]; c++)
                    (k < TileSet.PlayableKindCount ? playable : bonus).Add(Tile.FromKindIndex(k));

            int mid = playable.Count / 2;
            order.AddRange(playable.Take(mid));
            order.AddRange(bonus);
            order.AddRange(playable.Skip(mid));

            back.Reverse(); // first replacement draw comes from the very end
            order.AddRange(back);
            return order;
        }

        public static HandState Deal(string[] hands, string frontDraws = "", string backDraws = "",
            IWinValidator validator = null, RuleSet rules = null, int dealer = 0)
        {
            return HandState.DealFromWall(rules ?? RuleSet.Party(), dealer, Wind.East,
                Build(dealer, hands, frontDraws, backDraws), validator);
        }
    }

    /// <summary>Win validator driven by a lambda, for scripting win scenarios.</summary>
    internal sealed class PredicateWinValidator : IWinValidator
    {
        private readonly Func<IReadOnlyList<Tile>, bool> predicate;

        public PredicateWinValidator(Func<IReadOnlyList<Tile>, bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool IsWinningHand(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds) => predicate(concealed);

        /// <summary>Wins when the hand holds every tile in <paramref name="codes"/>.</summary>
        public static PredicateWinValidator Holding(string codes)
        {
            List<Tile> needed = Tile.ParseList(codes);
            return new PredicateWinValidator(hand =>
                needed.GroupBy(t => t).All(g => hand.Count(t => t == g.Key) >= g.Count()));
        }
    }

    /// <summary>
    /// Pseudo-random but deterministic win decisions (about 1 in <c>oneIn</c> hands), so
    /// soak tests exercise every win path without real scoring.
    /// </summary>
    internal sealed class HashWinValidator : IWinValidator
    {
        private readonly int oneIn;

        public HashWinValidator(int oneIn)
        {
            this.oneIn = oneIn;
        }

        public bool IsWinningHand(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds)
        {
            unchecked
            {
                int h = 17;
                foreach (Tile t in concealed.OrderBy(t => t))
                    h = h * 31 + t.KindIndex;
                h = h * 31 + melds.Count;
                return ((h & 0x7fffffff) % oneIn) == 0;
            }
        }
    }
}
