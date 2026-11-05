using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>Marks a tile GameObject so clicks can be mapped back to the tile it shows.</summary>
    public sealed class TileView : MonoBehaviour
    {
        public Tile Tile { get; set; }
        public int Seat { get; set; }
        /// <summary>True for tiles in a player's concealed hand (the only clickable ones).</summary>
        public bool InHand { get; set; }
        /// <summary>World position before any hover lift.</summary>
        public Vector3 RestPosition { get; set; }
    }
}
