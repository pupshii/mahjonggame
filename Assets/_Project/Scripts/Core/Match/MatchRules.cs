namespace MahjongNight.Core
{
    /// <summary>Settings for a scored match with the pocket cheat. See OUTLINE.md §1.</summary>
    public sealed class MatchRules
    {
        /// <summary>Hands in a match; the highest faan score after the last one wins.</summary>
        public int HandsPerMatch { get; set; } = 8;

        /// <summary>A caught cheater pays this many faan to <b>each</b> other player.</summary>
        public int CheatPenalty { get; set; } = 10;

        /// <summary>A wrong call-out costs the accuser this many faan, paid to the accused.</summary>
        public int FalseAccusationFine { get; set; } = 3;

        /// <summary>Tiles each player can choose from in their pocket (before Deep Pockets).</summary>
        public int PocketSize { get; set; } = 5;

        /// <summary>Faan everyone starts the match with.</summary>
        public int StartingScore { get; set; }

        public static MatchRules Default() => new MatchRules();
    }

    /// <summary>Cheating upgrades bought with faan between hands.</summary>
    public enum Upgrade
    {
        /// <summary>Each step of the pocket swap is faster.</summary>
        SleightOfHand,
        /// <summary>More tiles to choose from in your pocket.</summary>
        DeepPockets,
        /// <summary>Others must watch you longer to spot a cheat.</summary>
        PokerFace,
        /// <summary>You spot other cheaters faster.</summary>
        EagleEye,
    }

    /// <summary>Upgrade catalogue and the numbers behind each effect.</summary>
    public static class Upgrades
    {
        public const int MaxLevel = 2;

        /// <summary>Seconds for each of the three cheat steps at Sleight of Hand level 0.</summary>
        public const double BaseCheatStepSeconds = 1.2;

        /// <summary>Seconds someone must watch a cheat (Poker Face 0 vs Eagle Eye 0) to spot it.</summary>
        public const double BaseSpotSeconds = 0.5;

        public static readonly Upgrade[] All =
            { Upgrade.SleightOfHand, Upgrade.DeepPockets, Upgrade.PokerFace, Upgrade.EagleEye };

        /// <summary>Price of buying <paramref name="nextLevel"/> (1 or 2).</summary>
        public static int Cost(Upgrade u, int nextLevel) => BaseCost(u) * nextLevel;

        private static int BaseCost(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.SleightOfHand: return 8;
                case Upgrade.DeepPockets: return 6;
                case Upgrade.PokerFace: return 8;
                case Upgrade.EagleEye: return 6;
                default: return 10;
            }
        }

        public static string Name(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.SleightOfHand: return "Sleight of Hand";
                case Upgrade.DeepPockets: return "Deep Pockets";
                case Upgrade.PokerFace: return "Poker Face";
                case Upgrade.EagleEye: return "Eagle Eye";
                default: return u.ToString();
            }
        }

        public static string Description(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.SleightOfHand: return "Each step of your pocket swap is 25% faster.";
                case Upgrade.DeepPockets: return "+2 tiles to choose from in your pocket.";
                case Upgrade.PokerFace: return "Others must watch you 50% longer to spot a cheat.";
                case Upgrade.EagleEye: return "You spot cheaters 30% faster.";
                default: return "";
            }
        }

        public static double CheatStepSeconds(int sleightLevel) => BaseCheatStepSeconds * (1 - 0.25 * sleightLevel);

        public static int PocketSize(int baseSize, int deepPocketsLevel) => baseSize + 2 * deepPocketsLevel;

        /// <summary>How long <paramref name="spotterEagleEye"/> must watch a cheater with <paramref name="cheaterPokerFace"/>.</summary>
        public static double SpotSeconds(int cheaterPokerFace, int spotterEagleEye) =>
            BaseSpotSeconds * (1 + 0.5 * cheaterPokerFace) * (1 - 0.3 * spotterEagleEye);
    }
}
