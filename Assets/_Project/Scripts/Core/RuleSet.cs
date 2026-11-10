namespace MahjongNight.Core
{
    /// <summary>
    /// Table rules that vary between groups. All house-rule variation belongs here
    /// rather than in scattered if-statements. Grows as milestones add rules.
    /// </summary>
    /// <summary>Which scoring sheet the table uses.</summary>
    public enum ScoringStyle : byte
    {
        /// <summary>Hong Kong faan (<see cref="FaanTable"/>), with a cap (<see cref="RuleSet.MaxFaan"/>).</summary>
        HongKong,
        /// <summary>HK-style Taiwanese fan for 16-tile hands (<see cref="TaiwaneseScorer"/>): add up every hand that applies, no cap.</summary>
        Taiwanese,
    }

    public sealed class RuleSet
    {
        /// <summary>The scoring sheet. Taiwanese is meant for 16-tile hands (<see cref="HandSets"/> = 5).</summary>
        public ScoringStyle Scoring { get; set; } = ScoringStyle.HongKong;

        /// <summary>Fan for each Taiwanese hand (house values can be changed here).</summary>
        public TaiwaneseTable Taiwanese { get; set; } = TaiwaneseTable.Default();

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

        /// <summary>
        /// Paid on every win on top of the faan (底, as in Taiwanese base + tai scoring), so even a
        /// 0-faan hand is worth something when <see cref="MinFaan"/> is 0. 0 = faan only.
        /// </summary>
        public int WinBase { get; set; }

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
