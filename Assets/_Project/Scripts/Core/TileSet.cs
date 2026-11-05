using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>Constants and helpers for the full 144-tile Hong Kong set.</summary>
    public static class TileSet
    {
        /// <summary>Distinct playable kinds: 27 suited + 4 winds + 3 dragons.</summary>
        public const int PlayableKindCount = 34;
        /// <summary>Distinct bonus kinds: 4 flowers + 4 seasons.</summary>
        public const int BonusKindCount = 8;
        /// <summary>All distinct kinds; valid kind indices are [0, KindCount).</summary>
        public const int KindCount = PlayableKindCount + BonusKindCount;
        /// <summary>Copies of each playable kind. Bonus kinds have one copy.</summary>
        public const int CopiesPerPlayableKind = 4;
        /// <summary>Tiles in a full set: 34 * 4 + 8 = 144.</summary>
        public const int TotalTiles = PlayableKindCount * CopiesPerPlayableKind + BonusKindCount;

        /// <summary>Every tile in a full set, in kind-index order (unshuffled).</summary>
        public static List<Tile> CreateFull()
        {
            var tiles = new List<Tile>(TotalTiles);
            for (int kindIndex = 0; kindIndex < KindCount; kindIndex++)
            {
                Tile tile = Tile.FromKindIndex(kindIndex);
                int copies = tile.IsBonus ? 1 : CopiesPerPlayableKind;
                for (int copy = 0; copy < copies; copy++)
                    tiles.Add(tile);
            }
            return tiles;
        }
    }
}
