using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    /// <summary>A set in a decomposed hand: either a declared meld or one found in the concealed tiles.</summary>
    public readonly struct TileGroup
    {
        public TileGroup(MeldType type, Tile first, bool concealed)
        {
            Type = type;
            First = first;
            Concealed = concealed;
        }

        public MeldType Type { get; }
        /// <summary>Kind of a pung/kong; lowest tile of a chow.</summary>
        public Tile First { get; }
        /// <summary>Formed entirely from concealed tiles (or a concealed kong).</summary>
        public bool Concealed { get; }

        public bool IsPungOrKong => Type != MeldType.Chow;

        public bool Contains(Tile t) =>
            Type == MeldType.Chow
                ? t.Suit == First.Suit && t.Rank >= First.Rank && t.Rank <= First.Rank + 2
                : t == First;

        public override string ToString() =>
            Type == MeldType.Chow
                ? $"{First}{new Tile(First.Suit, First.Rank + 1)}{new Tile(First.Suit, First.Rank + 2)}"
                : $"{Type}({First}){(Concealed ? "*" : "")}";
    }

    /// <summary>One way to read a standard hand as four sets plus a pair.</summary>
    public sealed class HandDecomposition
    {
        public HandDecomposition(Tile pair, IReadOnlyList<TileGroup> groups)
        {
            Pair = pair;
            Groups = groups;
        }

        public Tile Pair { get; }
        /// <summary>All the sets (four, or five in 16-tile mahjong): declared melds first, then concealed sets.</summary>
        public IReadOnlyList<TileGroup> Groups { get; }

        public override string ToString() => string.Join(" ", Groups) + $" + pair {Pair}";
    }

    /// <summary>Finds every way to split a hand into sets plus a pair (four sets, or five for 16-tile mahjong).</summary>
    public static class HandDecomposer
    {
        /// <param name="concealed">Concealed tiles including the winning tile.</param>
        /// <param name="melds">Declared melds.</param>
        /// <param name="sets">Sets in a winning hand besides the pair (4, or 5 for 16-tile hands).</param>
        /// <returns>All decompositions; empty if the hand is not a standard winning shape.</returns>
        public static List<HandDecomposition> Decompose(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds, int sets = 4)
        {
            var results = new List<HandDecomposition>();
            int setsNeeded = sets - melds.Count;
            if (setsNeeded < 0 || concealed.Count != setsNeeded * 3 + 2)
                return results;

            int[] counts = CountKinds(concealed);
            if (counts == null)
                return results;

            var declared = melds.Select(m => new TileGroup(m.Type, m.FirstTile, m.IsConcealed)).ToList();
            var found = new List<TileGroup>();

            for (int pair = 0; pair < TileSet.PlayableKindCount; pair++)
            {
                if (counts[pair] < 2)
                    continue;
                counts[pair] -= 2;
                FindSets(counts, 0, found, setsNeeded, results, Tile.FromKindIndex(pair), declared);
                counts[pair] += 2;
            }
            return results;
        }

        /// <summary>Counts per playable kind, or null if any tile is a bonus/invalid tile.</summary>
        internal static int[] CountKinds(IReadOnlyList<Tile> tiles)
        {
            var counts = new int[TileSet.PlayableKindCount];
            foreach (Tile t in tiles)
            {
                if (!t.IsValid || t.IsBonus)
                    return null;
                counts[t.KindIndex]++;
            }
            return counts;
        }

        private static void FindSets(int[] counts, int from, List<TileGroup> found, int setsNeeded,
            List<HandDecomposition> results, Tile pair, List<TileGroup> declared)
        {
            int k = from;
            while (k < TileSet.PlayableKindCount && counts[k] == 0)
                k++;

            if (k == TileSet.PlayableKindCount)
            {
                if (found.Count == setsNeeded)
                    results.Add(new HandDecomposition(pair, declared.Concat(found).ToList()));
                return;
            }

            Tile tile = Tile.FromKindIndex(k);

            if (counts[k] >= 3)
            {
                counts[k] -= 3;
                found.Add(new TileGroup(MeldType.Pung, tile, concealed: true));
                FindSets(counts, k, found, setsNeeded, results, pair, declared);
                found.RemoveAt(found.Count - 1);
                counts[k] += 3;
            }

            // Chows: suited, rank 1-7 (kind index within suit 0-6).
            if (k < 27 && k % 9 <= 6 && counts[k + 1] > 0 && counts[k + 2] > 0)
            {
                counts[k]--; counts[k + 1]--; counts[k + 2]--;
                found.Add(new TileGroup(MeldType.Chow, tile, concealed: true));
                FindSets(counts, k, found, setsNeeded, results, pair, declared);
                found.RemoveAt(found.Count - 1);
                counts[k]++; counts[k + 1]++; counts[k + 2]++;
            }
        }
    }
}
