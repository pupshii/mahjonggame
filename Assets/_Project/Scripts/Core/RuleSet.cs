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

        /// <summary>Allow Seven Pairs (七對) as a winning shape.</summary>
        public bool AllowSevenPairs { get; set; }

        /// <summary>Faan value of each pattern.</summary>
        public FaanTable Faan { get; set; } = FaanTable.Default();

        /// <summary>Party mode: any valid hand wins, fun hands allowed.</summary>
        public static RuleSet Party() => new RuleSet
        {
            DeadWallSize = 0,
            AllowMultipleWinners = false,
            MinFaan = 0,
            MaxFaan = 10,
            AllowSevenPairs = true,
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
