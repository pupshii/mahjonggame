using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>The seven tile groups in a Hong Kong mahjong set.</summary>
    public enum TileSuit : byte
    {
        Dots,        // 筒  ranks 1-9
        Bamboo,      // 索  ranks 1-9
        Characters,  // 萬  ranks 1-9
        Wind,        // 風  ranks 1-4 (see Wind)
        Dragon,      // 三元 ranks 1-3 (see Dragon)
        Flower,      // 花  ranks 1-4, bonus tile, one copy each
        Season       // 季  ranks 1-4, bonus tile, one copy each
    }

    /// <summary>Wind ranks. Also used for seat winds and the prevailing wind.</summary>
    public enum Wind : byte
    {
        East = 1,
        South = 2,
        West = 3,
        North = 4
    }

    /// <summary>Dragon ranks.</summary>
    public enum Dragon : byte
    {
        Red = 1,    // 中
        Green = 2,  // 發
        White = 3   // 白
    }

    /// <summary>
    /// A tile <i>kind</i> (e.g. "5 Bamboo", "Red Dragon"), not a physical tile: the four
    /// physical copies of 5 Bamboo are all equal. <c>default(Tile)</c> is invalid.
    /// </summary>
    /// <remarks>
    /// Kind index layout (stable — used for count arrays and sorting):
    /// <code>
    ///   0-8   Dots 1-9        27-30  Winds E S W N
    ///   9-17  Bamboo 1-9      31-33  Dragons Red Green White
    ///   18-26 Characters 1-9  34-37  Flowers 1-4
    ///                         38-41  Seasons 1-4
    /// </code>
    /// </remarks>
    public readonly struct Tile : IEquatable<Tile>, IComparable<Tile>
    {
        // First kind index of each suit, indexed by TileSuit.
        private static readonly int[] SuitBaseIndex = { 0, 9, 18, 27, 31, 34, 38 };
        // Number of ranks in each suit, indexed by TileSuit.
        private static readonly int[] SuitRankCount = { 9, 9, 9, 4, 3, 4, 4 };

        public TileSuit Suit { get; }
        public byte Rank { get; }

        public Tile(TileSuit suit, int rank)
        {
            Suit = suit;
            Rank = (byte)rank;
        }

        public static Tile Dots(int rank) => new Tile(TileSuit.Dots, rank);
        public static Tile Bamboo(int rank) => new Tile(TileSuit.Bamboo, rank);
        public static Tile Characters(int rank) => new Tile(TileSuit.Characters, rank);
        public static Tile WindTile(Wind wind) => new Tile(TileSuit.Wind, (int)wind);
        public static Tile DragonTile(Dragon dragon) => new Tile(TileSuit.Dragon, (int)dragon);
        public static Tile Flower(int rank) => new Tile(TileSuit.Flower, rank);
        public static Tile Season(int rank) => new Tile(TileSuit.Season, rank);

        /// <summary>Rebuilds a tile from its kind index in [0, <see cref="TileSet.KindCount"/>).</summary>
        public static Tile FromKindIndex(int kindIndex)
        {
            if (kindIndex < 0 || kindIndex >= TileSet.KindCount)
                throw new ArgumentOutOfRangeException(nameof(kindIndex), kindIndex, "Kind index out of range.");

            for (int suit = SuitBaseIndex.Length - 1; suit >= 0; suit--)
            {
                if (kindIndex >= SuitBaseIndex[suit])
                    return new Tile((TileSuit)suit, kindIndex - SuitBaseIndex[suit] + 1);
            }
            throw new InvalidOperationException("Unreachable.");
        }

        /// <summary>True if <see cref="Rank"/> is in range for <see cref="Suit"/>.</summary>
        public bool IsValid
        {
            get
            {
                int suit = (int)Suit;
                return suit < SuitRankCount.Length && Rank >= 1 && Rank <= SuitRankCount[suit];
            }
        }

        /// <summary>Dots, Bamboo or Characters.</summary>
        public bool IsSuited => Suit == TileSuit.Dots || Suit == TileSuit.Bamboo || Suit == TileSuit.Characters;
        /// <summary>Winds or Dragons.</summary>
        public bool IsHonor => Suit == TileSuit.Wind || Suit == TileSuit.Dragon;
        /// <summary>Flowers or Seasons: set aside when drawn, never part of melds.</summary>
        public bool IsBonus => Suit == TileSuit.Flower || Suit == TileSuit.Season;
        /// <summary>Suited 1 or 9.</summary>
        public bool IsTerminal => IsSuited && (Rank == 1 || Rank == 9);
        /// <summary>Terminal or honor (么九) — used by Thirteen Orphans and terminal hands.</summary>
        public bool IsTerminalOrHonor => IsTerminal || IsHonor;

        /// <summary>Stable index in [0, <see cref="TileSet.KindCount"/>); see layout in remarks.</summary>
        public int KindIndex
        {
            get
            {
                if (!IsValid)
                    throw new InvalidOperationException($"Invalid tile ({Suit} {Rank}) has no kind index.");
                return SuitBaseIndex[(int)Suit] + Rank - 1;
            }
        }

        /// <summary>
        /// Short code for logs and tests: "1D" "9B" "5C" (Dots/Bamboo/Characters),
        /// "E" "S" "W" "N" (winds), "RD" "GD" "WD" (dragons), "F1"-"F4", "S1"-"S4".
        /// </summary>
        public override string ToString()
        {
            if (!IsValid)
                return "??";

            switch (Suit)
            {
                case TileSuit.Dots: return $"{Rank}D";
                case TileSuit.Bamboo: return $"{Rank}B";
                case TileSuit.Characters: return $"{Rank}C";
                case TileSuit.Wind: return "ESWN"[Rank - 1].ToString();
                case TileSuit.Dragon: return Rank == 1 ? "RD" : Rank == 2 ? "GD" : "WD";
                case TileSuit.Flower: return $"F{Rank}";
                case TileSuit.Season: return $"S{Rank}";
                default: return "??";
            }
        }

        /// <summary>Parses a short code produced by <see cref="ToString"/> (case-insensitive).</summary>
        public static bool TryParse(string code, out Tile tile)
        {
            tile = default;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            string c = code.Trim().ToUpperInvariant();
            switch (c)
            {
                case "E": tile = WindTile(Wind.East); return true;
                case "S": tile = WindTile(Wind.South); return true;
                case "W": tile = WindTile(Wind.West); return true;
                case "N": tile = WindTile(Wind.North); return true;
                case "RD": tile = DragonTile(Dragon.Red); return true;
                case "GD": tile = DragonTile(Dragon.Green); return true;
                case "WD": tile = DragonTile(Dragon.White); return true;
            }

            if (c.Length != 2)
                return false;

            if ((c[0] == 'F' || c[0] == 'S') && c[1] >= '1' && c[1] <= '4')
            {
                tile = new Tile(c[0] == 'F' ? TileSuit.Flower : TileSuit.Season, c[1] - '0');
                return true;
            }

            if (c[0] >= '1' && c[0] <= '9')
            {
                int rank = c[0] - '0';
                switch (c[1])
                {
                    case 'D': tile = Dots(rank); return true;
                    case 'B': tile = Bamboo(rank); return true;
                    case 'C': tile = Characters(rank); return true;
                }
            }
            return false;
        }

        /// <summary>Parses a short code; throws <see cref="FormatException"/> if invalid.</summary>
        public static Tile Parse(string code)
        {
            if (!TryParse(code, out Tile tile))
                throw new FormatException($"'{code}' is not a tile code.");
            return tile;
        }

        /// <summary>Parses space-separated codes, e.g. "1D 2D 3D RD RD".</summary>
        public static List<Tile> ParseList(string codes)
        {
            var result = new List<Tile>();
            if (codes == null)
                return result;
            foreach (string part in codes.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                result.Add(Parse(part));
            return result;
        }

        public bool Equals(Tile other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Tile other && Equals(other);
        public override int GetHashCode() => ((int)Suit << 8) | Rank;

        /// <summary>Orders by kind index — the natural hand-sorting order.</summary>
        public int CompareTo(Tile other)
        {
            int bySuit = Suit.CompareTo(other.Suit);
            return bySuit != 0 ? bySuit : Rank.CompareTo(other.Rank);
        }

        public static bool operator ==(Tile a, Tile b) => a.Equals(b);
        public static bool operator !=(Tile a, Tile b) => !a.Equals(b);
        public static bool operator <(Tile a, Tile b) => a.CompareTo(b) < 0;
        public static bool operator >(Tile a, Tile b) => a.CompareTo(b) > 0;
    }
}
