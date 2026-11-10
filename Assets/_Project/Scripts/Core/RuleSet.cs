namespace MahjongNight.Core
{
    /// <summary>
    /// Table rules that vary between groups. All house-rule variation belongs here
    /// rather than in scattered if-statements. Grows as milestones add rules.
    /// </summary>
    public sealed class RuleSet
    {
        /// <summary>
        /// Tiles at the back of the wall that are never drawn; the hand is a draw game when
        /// only these remain. 0 = play until the wall is empty.
        /// </summary>
        public int DeadWallSize { get; set; }

        /// <summary>
        /// If several players claim a win on the same discard: false = only the first in turn
        /// order after the discarder wins (截糊), true = all of them win (一炮多響).
        /// </summary>
        public bool AllowMultipleWinners { get; set; }

        /// <summary>A hand must be worth at least this many faan to be declared a win (起糊番數).</summary>
        public int MinFaan { get; set; }

        /// <summary>Faan cap (爆棚); limit hands score exactly this.</summary>
        public int MaxFaan { get; set; } = 13;

        /// <summary>
        /// Sets in a winning hand besides the pair: 4 = Hong Kong style (13 tiles in hand, 14 to
        /// win); 5 = 16-tile style, as in Taiwanese mahjong (16 in hand, 17 to win). Seven Pairs
        /// and Thirteen Orphans only exist with 4.
        /// </summary>
        public int HandSets { get; set; } = 4;

        /// <summary>Tiles in hand between turns: 13 (4 sets) or 16 (5 sets).</summary>
        public int HandSize => HandSets * 3 + 1;

        /// <summary>Allow Seven Pairs (七對) as a winning shape.</summary>
        public bool AllowSevenPairs { get; set; }

        /// <summary>
        /// Players choose which tile kind to draw each turn (party twist). If no copy of that
        /// kind is left in the wall, the draw fails and they lose their turn. False = normal
        /// random draws from the wall.
        /// </summary>
        public bool ChooseDraws { get; set; }

        /// <summary>Faan value of each pattern.</summary>
        public FaanTable Faan { get; set; } = FaanTable.Default();

        /// <summary>Party mode: 3 faan to win, fun hands allowed. (Chosen draws are available but off.)</summary>
        public static RuleSet Party() => new RuleSet
        {
            DeadWallSize = 0,
            AllowMultipleWinners = false,
            MinFaan = 3,
            MaxFaan = 10,
            AllowSevenPairs = true,
            ChooseDraws = false,
        };

        /// <summary>Classic Hong Kong table rules: 3 faan minimum, 13 faan limit.</summary>
        public static RuleSet Classic() => new RuleSet
        {
            DeadWallSize = 0,
            AllowMultipleWinners = false,
            MinFaan = 3,
            MaxFaan = 13,
            AllowSevenPairs = false,
        };
    }
}
