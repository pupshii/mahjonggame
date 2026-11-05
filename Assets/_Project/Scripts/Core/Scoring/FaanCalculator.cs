using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    /// <summary>
    /// Scores a winning hand. Tries every way of reading the hand (standard decompositions,
    /// Seven Pairs, Thirteen Orphans) and returns the highest-scoring one.
    /// </summary>
    /// <remarks>
    /// House-rule choices made here (change if your table differs):
    /// <list type="bullet">
    /// <item>Small three dragons replaces the faan of its two dragon pungs.</item>
    /// <item>A full flower/season set replaces the own-flower/own-season faan of that set.</item>
    /// <item>Concealed hand (門前清) only applies when winning on a discard.</item>
    /// <item>Win on the last tile (海底撈月) only applies to self-drawn wins.</item>
    /// <item>Four concealed pungs needs all four pungs concealed: on a discard win the
    /// winning tile must complete the pair.</item>
    /// </list>
    /// </remarks>
    public static class FaanCalculator
    {
        /// <summary>The best score for the hand, or null if it is not a complete winning shape.</summary>
        public static FaanResult Calculate(WinContext ctx, RuleSet rules)
        {
            FaanResult best = null;

            void Consider(FaanResult r)
            {
                if (best == null || r.Total > best.Total || (r.Total == best.Total && r.RawTotal > best.RawTotal))
                    best = r;
            }

            if (ctx.Melds.Count == 0 && IsThirteenOrphans(ctx.Concealed))
                Consider(Score(ctx, rules, HandShape.ThirteenOrphans, null));

            if (rules.AllowSevenPairs && ctx.Melds.Count == 0 && IsSevenPairs(ctx.Concealed))
                Consider(Score(ctx, rules, HandShape.SevenPairs, null));

            foreach (HandDecomposition d in HandDecomposer.Decompose(ctx.Concealed, ctx.Melds))
                Consider(Score(ctx, rules, HandShape.Standard, d));

            return best;
        }

        /// <summary>True if the tiles form any winning shape allowed by <paramref name="rules"/>, ignoring faan.</summary>
        public static bool IsCompleteShape(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds, RuleSet rules)
        {
            if (melds.Count == 0 && IsThirteenOrphans(concealed))
                return true;
            if (rules.AllowSevenPairs && melds.Count == 0 && IsSevenPairs(concealed))
                return true;
            return HandDecomposer.Decompose(concealed, melds).Count > 0;
        }

        /// <summary>
        /// Tiles that would complete a 13-tile hand (聽牌). Empty = not ready. Shape only —
        /// ignores minimum faan and how many copies are still unseen.
        /// </summary>
        public static List<Tile> GetWaits(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds, RuleSet rules)
        {
            var waits = new List<Tile>();
            var hand = new List<Tile>(concealed) { default };
            for (int k = 0; k < TileSet.PlayableKindCount; k++)
            {
                Tile t = Tile.FromKindIndex(k);
                if (concealed.Count(c => c == t) >= 4)
                    continue;
                hand[hand.Count - 1] = t;
                if (IsCompleteShape(hand, melds, rules))
                    waits.Add(t);
            }
            return waits;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>十三么 shape: one of each terminal and honour plus a duplicate (14 tiles, no melds).</summary>
        public static bool IsThirteenOrphans(IReadOnlyList<Tile> tiles)
        {
            if (tiles.Count != 14 || tiles.Any(t => !t.IsValid || !t.IsTerminalOrHonor))
                return false;
            return tiles.Distinct().Count() == 13;
        }

        /// <summary>七對 shape: seven different pairs (14 tiles, no melds; four of a kind doesn't count).</summary>
        public static bool IsSevenPairs(IReadOnlyList<Tile> tiles)
        {
            if (tiles.Count != 14 || tiles.Any(t => !t.IsValid || t.IsBonus))
                return false;
            return tiles.GroupBy(t => t).All(g => g.Count() == 2);
        }

        // ------------------------------------------------------------------ scoring

        private static FaanResult Score(WinContext ctx, RuleSet rules, HandShape shape, HandDecomposition d)
        {
            var items = new List<FaanItem>();
            FaanTable table = rules.Faan;

            void Add(FaanPattern p, int count = 1)
            {
                if (count > 0)
                    items.Add(new FaanItem(p, count, FaanPatterns.IsLimit(p) ? 0 : table[p] * count));
            }

            List<Tile> allTiles = ctx.Concealed.Concat(ctx.Melds.SelectMany(m => m.Tiles)).ToList();

            // ---- way of winning
            if (ctx.HeavenlyHand) Add(FaanPattern.HeavenlyHand);
            if (ctx.EarthlyHand) Add(FaanPattern.EarthlyHand);
            if (ctx.SelfDrawn) Add(FaanPattern.SelfDrawn);
            if (!ctx.SelfDrawn && ctx.Melds.All(m => m.IsConcealed)) Add(FaanPattern.ConcealedHand);
            if (ctx.SelfDrawn && ctx.LastTile) Add(FaanPattern.LastTile);
            if (ctx.SelfDrawn && ctx.KongReplacement) Add(FaanPattern.KongReplacement);
            if (ctx.RobbedKong) Add(FaanPattern.RobbingKong);

            // ---- bonus tiles
            AddBonusFaan(ctx, Add);

            // ---- shape
            switch (shape)
            {
                case HandShape.ThirteenOrphans:
                    Add(FaanPattern.ThirteenOrphans);
                    break;
                case HandShape.SevenPairs:
                    Add(FaanPattern.SevenPairs);
                    AddSuitFaan(allTiles, Add);
                    break;
                default:
                    AddStandardFaan(ctx, d, allTiles, Add);
                    break;
            }

            return new FaanResult(shape, d, items, rules.MaxFaan);
        }

        private static void AddBonusFaan(WinContext ctx, System.Action<FaanPattern, int> add)
        {
            if (ctx.Bonus.Count == 0)
            {
                add(FaanPattern.NoFlowers, 1);
                return;
            }
            if (ctx.Bonus.Count == 8)
            {
                add(FaanPattern.EightFlowers, 1);
                return;
            }

            int own = (int)ctx.SeatWind; // East = flower/season 1, South = 2, ...
            int flowers = ctx.Bonus.Count(t => t.Suit == TileSuit.Flower);
            int seasons = ctx.Bonus.Count(t => t.Suit == TileSuit.Season);
            int sets = (flowers == 4 ? 1 : 0) + (seasons == 4 ? 1 : 0);
            add(FaanPattern.FlowerSet, sets);
            if (flowers < 4 && ctx.Bonus.Contains(Tile.Flower(own)))
                add(FaanPattern.OwnFlower, 1);
            if (seasons < 4 && ctx.Bonus.Contains(Tile.Season(own)))
                add(FaanPattern.OwnSeason, 1);
        }

        /// <summary>Pure/mixed one suit and all honours.</summary>
        private static void AddSuitFaan(List<Tile> allTiles, System.Action<FaanPattern, int> add)
        {
            bool anyHonor = allTiles.Any(t => t.IsHonor);
            int suits = allTiles.Where(t => t.IsSuited).Select(t => t.Suit).Distinct().Count();

            if (suits == 0)
                add(FaanPattern.AllHonors, 1);
            else if (suits == 1)
                add(anyHonor ? FaanPattern.MixedOneSuit : FaanPattern.PureOneSuit, 1);
        }

        private static void AddStandardFaan(WinContext ctx, HandDecomposition d, List<Tile> allTiles,
            System.Action<FaanPattern, int> add)
        {
            IReadOnlyList<TileGroup> groups = d.Groups;
            Tile pair = d.Pair;

            // Dragons
            int dragonPungs = groups.Count(g => g.IsPungOrKong && g.First.Suit == TileSuit.Dragon);
            if (dragonPungs == 3)
                add(FaanPattern.BigThreeDragons, 1);
            else if (dragonPungs == 2 && pair.Suit == TileSuit.Dragon)
                add(FaanPattern.SmallThreeDragons, 1);
            else
                add(FaanPattern.DragonPung, dragonPungs);

            // Winds
            int windPungs = groups.Count(g => g.IsPungOrKong && g.First.Suit == TileSuit.Wind);
            if (windPungs == 4)
                add(FaanPattern.BigFourWinds, 1);
            else if (windPungs == 3 && pair.Suit == TileSuit.Wind)
                add(FaanPattern.SmallFourWinds, 1);
            else
            {
                if (groups.Any(g => g.IsPungOrKong && g.First == Tile.WindTile(ctx.SeatWind)))
                    add(FaanPattern.SeatWindPung, 1);
                if (groups.Any(g => g.IsPungOrKong && g.First == Tile.WindTile(ctx.PrevailingWind)))
                    add(FaanPattern.PrevailingWindPung, 1);
            }

            // Set structure
            if (groups.All(g => g.Type == MeldType.Chow))
                add(FaanPattern.AllChows, 1);
            if (groups.All(g => g.IsPungOrKong))
                add(FaanPattern.AllPungs, 1);
            if (groups.Count(g => g.Type == MeldType.Kong) == 4)
                add(FaanPattern.FourKongs, 1);
            if (CountConcealedPungs(ctx, d) == 4)
                add(FaanPattern.FourConcealedPungs, 1);

            // Tiles
            if (allTiles.All(t => t.IsTerminal))
                add(FaanPattern.AllTerminals, 1);
            if (ctx.Melds.Count == 0 && IsNineGates(ctx.Concealed))
                add(FaanPattern.NineGates, 1);
            AddSuitFaan(allTiles, add);
        }

        /// <summary>
        /// Concealed pungs/kongs. A pung completed by a discard counts as exposed, unless the
        /// winning tile can be read as completing the pair or a concealed chow instead.
        /// </summary>
        private static int CountConcealedPungs(WinContext ctx, HandDecomposition d)
        {
            int n = d.Groups.Count(g => g.IsPungOrKong && g.Concealed);
            if (ctx.SelfDrawn || d.Pair == ctx.WinningTile)
                return n;
            if (d.Groups.Any(g => g.Type == MeldType.Chow && g.Concealed && g.Contains(ctx.WinningTile)))
                return n;
            if (d.Groups.Any(g => g.IsPungOrKong && g.Concealed && g.First == ctx.WinningTile))
                n--;
            return n;
        }

        /// <summary>九子連環: 1112345678999 of one suit plus any tile of that suit, fully concealed.</summary>
        private static bool IsNineGates(IReadOnlyList<Tile> concealed)
        {
            if (concealed.Count != 14 || !concealed.All(t => t.IsSuited))
                return false;
            TileSuit suit = concealed[0].Suit;
            if (concealed.Any(t => t.Suit != suit))
                return false;
            var c = new int[10];
            foreach (Tile t in concealed)
                c[t.Rank]++;
            if (c[1] < 3 || c[9] < 3)
                return false;
            for (int r = 2; r <= 8; r++)
                if (c[r] < 1)
                    return false;
            return true;
        }
    }

    /// <summary>
    /// The real win rule: the hand must be a complete shape worth at least
    /// <see cref="RuleSet.MinFaan"/>.
    /// </summary>
    public sealed class FaanWinValidator : IWinValidator
    {
        private readonly RuleSet rules;

        public FaanWinValidator(RuleSet rules)
        {
            this.rules = rules ?? throw new System.ArgumentNullException(nameof(rules));
        }

        public bool IsWinningHand(WinContext context)
        {
            FaanResult result = FaanCalculator.Calculate(context, rules);
            return result != null && result.Total >= rules.MinFaan;
        }
    }
}
