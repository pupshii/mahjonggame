using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// Everything about a (potential) win that affects whether it is legal and what it
    /// scores. Built by <see cref="HandState"/>; also easy to build by hand in tests.
    /// </summary>
    public sealed class WinContext
    {
        public int Seat { get; set; }
        public Wind SeatWind { get; set; } = Wind.East;
        public Wind PrevailingWind { get; set; } = Wind.East;

        /// <summary>Concealed tiles <b>including</b> the winning tile (14 or 17, − 3 × melds).</summary>
        public IReadOnlyList<Tile> Concealed { get; set; } = new Tile[0];
        public IReadOnlyList<Meld> Melds { get; set; } = new Meld[0];
        /// <summary>Flowers and seasons.</summary>
        public IReadOnlyList<Tile> Bonus { get; set; } = new Tile[0];

        public Tile WinningTile { get; set; }
        /// <summary>自摸 — drew the winning tile (false = won on a discard or robbed kong).</summary>
        public bool SelfDrawn { get; set; }
        /// <summary>搶槓</summary>
        public bool RobbedKong { get; set; }
        /// <summary>槓上開花 — self-drawn on a kong replacement tile.</summary>
        public bool KongReplacement { get; set; }
        /// <summary>Self-drawn on the tile that replaced a flower or season.</summary>
        public bool FlowerReplacement { get; set; }
        /// <summary>How many tiles the winner had discarded before this win (for the "within N discards" hands).</summary>
        public int DiscardsBeforeWin { get; set; }
        /// <summary>No drawable tiles were left when the hand was won.</summary>
        public bool LastTile { get; set; }
        /// <summary>天糊 — dealer wins with the starting hand.</summary>
        public bool HeavenlyHand { get; set; }
        /// <summary>地糊 — non-dealer wins on the dealer's first discard.</summary>
        public bool EarthlyHand { get; set; }
    }
}
