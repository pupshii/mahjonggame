using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>One player's tiles during a hand. Mutated only by <see cref="HandState"/>.</summary>
    public sealed class SeatState
    {
        internal readonly List<Tile> concealed = new List<Tile>();
        internal readonly List<Meld> melds = new List<Meld>();
        internal readonly List<Tile> bonus = new List<Tile>();
        internal readonly List<Tile> discards = new List<Tile>();

        internal SeatState(int seat, Wind seatWind)
        {
            Seat = seat;
            SeatWind = seatWind;
        }

        public int Seat { get; }
        /// <summary>門風 — East for the dealer, then South, West, North in turn order.</summary>
        public Wind SeatWind { get; }

        /// <summary>Tiles in hand, in the order received (UI sorts for display). Private to this player.</summary>
        public IReadOnlyList<Tile> Concealed => concealed;
        /// <summary>Declared melds, in declaration order.</summary>
        public IReadOnlyList<Meld> Melds => melds;
        /// <summary>Flowers and seasons set aside.</summary>
        public IReadOnlyList<Tile> Bonus => bonus;
        /// <summary>This player's discards still on the table (claimed discards are removed).</summary>
        public IReadOnlyList<Tile> Discards => discards;

        /// <summary>Copies of <paramref name="tile"/> in the concealed hand.</summary>
        public int CountConcealed(Tile tile)
        {
            int n = 0;
            foreach (Tile t in concealed)
                if (t == tile)
                    n++;
            return n;
        }

        internal void RemoveConcealed(Tile tile, int copies)
        {
            for (int i = 0; i < copies; i++)
                concealed.Remove(tile);
        }
    }
}
