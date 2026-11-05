using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    public enum MeldType : byte
    {
        Chow,   // 上 three in sequence
        Pung,   // 碰 three of a kind
        Kong    // 槓 four of a kind
    }

    /// <summary>A declared set of tiles. Immutable.</summary>
    public sealed class Meld
    {
        private readonly Tile[] tiles;

        private Meld(MeldType type, Tile[] tiles, bool isConcealed, int claimedFromSeat, Tile? claimedTile, bool isPromotedKong)
        {
            Type = type;
            this.tiles = tiles;
            IsConcealed = isConcealed;
            ClaimedFromSeat = claimedFromSeat;
            ClaimedTile = claimedTile;
            IsPromotedKong = isPromotedKong;
        }

        public MeldType Type { get; }
        public IReadOnlyList<Tile> Tiles => tiles;
        /// <summary>Only concealed kongs are concealed; every claimed meld is exposed.</summary>
        public bool IsConcealed { get; }
        /// <summary>Seat whose discard completed this meld, or -1.</summary>
        public int ClaimedFromSeat { get; }
        /// <summary>The discard that completed this meld, if any.</summary>
        public Tile? ClaimedTile { get; }
        /// <summary>True for a kong made by adding a tile to an exposed pung (加槓).</summary>
        public bool IsPromotedKong { get; }

        /// <summary>The lowest tile — the kind of a pung/kong, the start of a chow.</summary>
        public Tile FirstTile => tiles[0];

        public static Meld Chow(Tile lowest, int claimedFromSeat, Tile claimedTile)
        {
            if (!lowest.IsSuited || lowest.Rank > 7)
                throw new ArgumentException($"{lowest} cannot start a chow.", nameof(lowest));
            var t = new[] { lowest, new Tile(lowest.Suit, lowest.Rank + 1), new Tile(lowest.Suit, lowest.Rank + 2) };
            return new Meld(MeldType.Chow, t, false, claimedFromSeat, claimedTile, false);
        }

        public static Meld Pung(Tile tile, int claimedFromSeat) =>
            new Meld(MeldType.Pung, new[] { tile, tile, tile }, false, claimedFromSeat, tile, false);

        public static Meld ExposedKong(Tile tile, int claimedFromSeat) =>
            new Meld(MeldType.Kong, new[] { tile, tile, tile, tile }, false, claimedFromSeat, tile, false);

        public static Meld ConcealedKong(Tile tile) =>
            new Meld(MeldType.Kong, new[] { tile, tile, tile, tile }, true, -1, null, false);

        /// <summary>This pung with a fourth tile added (加槓).</summary>
        public Meld PromoteToKong()
        {
            if (Type != MeldType.Pung)
                throw new InvalidOperationException("Only a pung can be promoted to a kong.");
            Tile t = FirstTile;
            return new Meld(MeldType.Kong, new[] { t, t, t, t }, false, ClaimedFromSeat, ClaimedTile, true);
        }

        public override string ToString()
        {
            string kind = Type == MeldType.Kong && IsConcealed ? "ConcealedKong" : Type.ToString();
            return $"{kind}[{string.Join(" ", tiles)}]";
        }
    }
}
