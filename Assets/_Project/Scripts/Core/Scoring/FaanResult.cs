using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    public enum HandShape : byte
    {
        Standard,        // four sets + a pair
        SevenPairs,
        ThirteenOrphans,
        LikGuLikGu,       // 嚦咕嚦咕: seven pairs + a pung (16-tile, Taiwanese scoring)
        SixteenUnrelated  // 十六不搭 (16-tile, Taiwanese scoring)
    }

    /// <summary>One line of a faan breakdown, e.g. "Dragon pung ×2 = 2".</summary>
    public readonly struct FaanItem
    {
        public FaanItem(FaanPattern pattern, int count, int faan)
        {
            Pattern = pattern;
            Count = count;
            Faan = faan;
            Name = null;
            ChineseLabel = null;
        }

        /// <summary>A named hand from another scoring sheet (e.g. Taiwanese), not a <see cref="FaanPattern"/>.</summary>
        public FaanItem(string name, string chinese, int count, int faan)
        {
            Pattern = NoPattern;
            Count = count;
            Faan = faan;
            Name = name;
            ChineseLabel = chinese;
        }

        /// <summary>The <see cref="Pattern"/> of a named (non-Hong-Kong) item.</summary>
        public const FaanPattern NoPattern = (FaanPattern)(-1);

        public FaanPattern Pattern { get; }
        /// <summary>Name of a named item (null for Hong Kong patterns).</summary>
        public string Name { get; }
        private string ChineseLabel { get; }
        public string Chinese => Name != null ? ChineseLabel ?? "" : FaanPatterns.ChineseName(Pattern);
        /// <summary>How many times it applies (e.g. two dragon pungs).</summary>
        public int Count { get; }
        /// <summary>Faan from this line (0 for limit patterns — they set the total to the limit).</summary>
        public int Faan { get; }
        public bool IsLimit => Name == null && FaanPatterns.IsLimit(Pattern);

        public override string ToString()
        {
            string name = (Name ?? FaanPatterns.EnglishName(Pattern)) + (Count > 1 ? $" ×{Count}" : "");
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
