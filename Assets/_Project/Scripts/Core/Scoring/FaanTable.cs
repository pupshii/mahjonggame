using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// Faan value of each non-limit pattern. Limit patterns always score
    /// <see cref="RuleSet.MaxFaan"/> and have no entry. Edit to match your table's house rules.
    /// </summary>
    public sealed class FaanTable
    {
        private readonly Dictionary<FaanPattern, int> values = new Dictionary<FaanPattern, int>();

        public int this[FaanPattern pattern]
        {
            get => values.TryGetValue(pattern, out int v) ? v : 0;
            set => values[pattern] = value;
        }

        /// <summary>Common Hong Kong values (see OUTLINE.md §3.1).</summary>
        public static FaanTable Default()
        {
            var t = new FaanTable();
            t[FaanPattern.SelfDrawn] = 1;
            t[FaanPattern.ConcealedHand] = 1;
            t[FaanPattern.LastTile] = 1;
            t[FaanPattern.KongReplacement] = 1;
            t[FaanPattern.RobbingKong] = 1;
            t[FaanPattern.NoFlowers] = 1;
            t[FaanPattern.OwnFlower] = 1;
            t[FaanPattern.OwnSeason] = 1;
            t[FaanPattern.FlowerSet] = 2;
            t[FaanPattern.DragonPung] = 1;
            t[FaanPattern.SeatWindPung] = 1;
            t[FaanPattern.PrevailingWindPung] = 1;
            t[FaanPattern.AllChows] = 1;
            t[FaanPattern.AllPungs] = 3;
            t[FaanPattern.SmallThreeDragons] = 5;
            t[FaanPattern.MixedOneSuit] = 3;
            t[FaanPattern.PureOneSuit] = 7;
            t[FaanPattern.SevenPairs] = 4;
            return t;
        }
    }
}
