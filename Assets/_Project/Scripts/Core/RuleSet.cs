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

        /// <summary>Default rules for party mode.</summary>
        public static RuleSet Party() => new RuleSet { DeadWallSize = 0, AllowMultipleWinners = false };

        /// <summary>Default rules for classic mode.</summary>
        public static RuleSet Classic() => new RuleSet { DeadWallSize = 0, AllowMultipleWinners = false };
    }
}
