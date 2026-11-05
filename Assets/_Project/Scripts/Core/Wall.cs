using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// The wall: normal draws come from the front, replacement draws (after a kong or a
    /// bonus tile) from the back. The last <see cref="DeadWallSize"/> tiles are never drawn.
    /// </summary>
    public sealed class Wall
    {
        private readonly Tile[] tiles;
        private int front;      // index of the next front tile
        private int back;       // one past the index of the next back tile

        public Wall(IReadOnlyList<Tile> orderedTiles, int deadWallSize = 0)
        {
            if (orderedTiles == null)
                throw new ArgumentNullException(nameof(orderedTiles));
            if (deadWallSize < 0 || deadWallSize > orderedTiles.Count)
                throw new ArgumentOutOfRangeException(nameof(deadWallSize));

            tiles = new Tile[orderedTiles.Count];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = orderedTiles[i];
            front = 0;
            back = tiles.Length;
            DeadWallSize = deadWallSize;
        }

        /// <summary>A full 144-tile set shuffled with <paramref name="seed"/> (Fisher–Yates).</summary>
        public static Wall CreateShuffled(ulong seed, int deadWallSize = 0)
        {
            List<Tile> set = TileSet.CreateFull();
            var rng = new SeededRandom(seed);
            for (int i = set.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                Tile tmp = set[i];
                set[i] = set[j];
                set[j] = tmp;
            }
            return new Wall(set, deadWallSize);
        }

        public int DeadWallSize { get; }

        /// <summary>Tiles still in the wall, including the dead wall.</summary>
        public int Remaining => back - front;

        /// <summary>Tiles that may still be drawn.</summary>
        public int DrawableCount => Math.Max(0, Remaining - DeadWallSize);

        public bool CanDraw => DrawableCount > 0;

        public Tile DrawFront()
        {
            if (!CanDraw)
                throw new InvalidOperationException("The wall is exhausted.");
            return tiles[front++];
        }

        public Tile DrawBack()
        {
            if (!CanDraw)
                throw new InvalidOperationException("The wall is exhausted.");
            return tiles[--back];
        }

        /// <summary>Tiles still in the wall, front to back. Host-only information.</summary>
        public IEnumerable<Tile> RemainingTiles()
        {
            for (int i = front; i < back; i++)
                yield return tiles[i];
        }
    }
}
