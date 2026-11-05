namespace MahjongNight.Core
{
    /// <summary>Settings for the party layer (Dumpling Roulette and eliminations). See OUTLINE.md §1.3, §3.2.</summary>
    public sealed class PartyRules
    {
        /// <summary>Off = plain mahjong, hands forever, nobody eats.</summary>
        public bool RouletteEnabled { get; set; } = true;

        /// <summary>Dumplings in a full steamer; exactly one is the ghost pepper.</summary>
        public int DumplingsPerSteamer { get; set; } = 6;

        /// <summary>Peppers a player can survive before being knocked out.</summary>
        public int Lives { get; set; } = 1;

        /// <summary>One steamer for the whole table (true) or one per player (false).</summary>
        public bool SharedSteamer { get; set; } = true;

        /// <summary>Wins worth at least this many faan make each loser eat <see cref="BigHandDumplings"/>.</summary>
        public int BigHandFaan { get; set; } = 5;
        public int BigHandDumplings { get; set; } = 2;

        /// <summary>On a draw game, players without a ready hand (聽牌) eat one dumpling.</summary>
        public bool DrawGameNotReadyEat { get; set; } = true;

        public static PartyRules Default() => new PartyRules();
    }
}
