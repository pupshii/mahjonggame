using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    public enum HandShape : byte
    {
        Standard,        // four sets + a pair
        SevenPairs,
        ThirteenOrphans
    }

    /// <summary>One line of a faan breakdown, e.g. "Dragon pung ×2 = 2".</summary>
    public readonly struct FaanItem
    {
        public FaanItem(FaanPattern pattern, int count, int faan)
        {
            Pattern = pattern;
            Count = count;
            Faan = faan;
        }

        public FaanPattern Pattern { get; }
        /// <summary>How many times it applies (e.g. two dragon pungs).</summary>
        public int Count { get; }
        /// <summary>Faan from this line (0 for limit patterns — they set the total to the limit).</summary>
        public int Faan { get; }
        public bool IsLimit => FaanPatterns.IsLimit(Pattern);

        public override string ToString()
        {
            string name = FaanPatterns.EnglishName(Pattern) + (Count > 1 ? $" ×{Count}" : "");
            return IsLimit ? $"{name} (limit)" : $"{name} = {Faan}";
        }
    }

    /// <summary>The score of a winning hand, read in its highest-scoring way.</summary>
    public sealed class FaanResult
    {
        public FaanResult(HandShape shape, HandDecomposition decomposition, IReadOnlyList<FaanItem> items, int maxFaan)
        {
            Shape = shape;
            Decomposition = decomposition;
            Items = items;
            RawTotal = items.Sum(i => i.Faan);
            IsLimit = items.Any(i => i.IsLimit);
            Total = IsLimit ? maxFaan : System.Math.Min(RawTotal, maxFaan);
        }

        public HandShape Shape { get; }
        /// <summary>The sets used, for <see cref="HandShape.Standard"/> hands; null otherwise.</summary>
        public HandDecomposition Decomposition { get; }
        public IReadOnlyList<FaanItem> Items { get; }
        /// <summary>Sum of the items before the cap.</summary>
        public int RawTotal { get; }
        /// <summary>A limit hand (爆棚) — total is the cap.</summary>
        public bool IsLimit { get; }
        /// <summary>Final faan, capped at <see cref="RuleSet.MaxFaan"/>.</summary>
        public int Total { get; }

        public bool Has(FaanPattern pattern) => Items.Any(i => i.Pattern == pattern);

        public override string ToString() => $"{Total} faan: " + string.Join(", ", Items);
    }
}
