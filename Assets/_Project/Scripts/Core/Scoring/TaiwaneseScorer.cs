using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongNight.Core
{
    /// <summary>
    /// Scores a winning hand on the HK-style Taiwanese sheet (16-tile mahjong): add up every hand
    /// that applies, no cap. Bigger versions of a hand replace the smaller ones (e.g. Concealed
    /// Self-Draw replaces Fully Concealed + Self-Drawn; Small Three Dragons replaces the dragon
    /// pungs; only the best "sisters" run counts). If a win off a discard scores only 1, it's a
    /// Chicken Hand worth 10. The dealer bonus is added by <see cref="ScoreMatch"/> (it depends
    /// on the dealer's streak, not the tiles).
    /// </summary>
    /// <remarks>
    /// Not scored (no matching game event yet): Declared Ready, Kong on Kong, Robbing Kong on
    /// Kong, Seven Robs One, Flower Win, Thirteen Orphans. "House" hands (Old &amp; Young,
    /// Big Ping Wu) are 0 until set in <see cref="TaiwaneseTable"/>.
    /// </remarks>
    public static class TaiwaneseScorer
    {
        // ------------------------------------------------------------------ special shapes

        /// <summary>嚦咕嚦咕: seven pairs + one pung (17 tiles), nothing claimed.</summary>
        public static bool IsLikGu(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds)
        {
            if (melds.Count != 0 || concealed.Count != 17 || concealed.Any(t => !t.IsValid || t.IsBonus))
                return false;
            int triples = 0, pairs = 0;
            foreach (int c in Counts(concealed).Where(c => c > 0))
            {
                if (c == 2) pairs++;
                else if (c == 4) pairs += 2;
                else if (c == 3) triples++;
                else return false;
            }
            return triples == 1 && pairs == 7;
        }

        /// <summary>
        /// 十六不搭: one of each honour plus three unconnected tiles (ranks 3+ apart) in each suit —
        /// 16 different tiles — and the 17th tile pairs one of them. Nothing claimed.
        /// </summary>
        public static bool IsSixteenUnrelated(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds, out bool pure)
        {
            pure = false;
            if (melds.Count != 0 || concealed.Count != 17 || concealed.Any(t => !t.IsValid || t.IsBonus))
                return false;
            int[] counts = Counts(concealed);
            if (counts.Count(c => c > 0) != 16 || counts.Any(c => c > 2))
                return false;
            for (int k = 27; k < 34; k++)          // every wind and dragon
                if (counts[k] == 0)
                    return false;
            pure = true;
            foreach (TileSuit suit in new[] { TileSuit.Dots, TileSuit.Bamboo, TileSuit.Characters })
            {
                List<int> ranks = Enumerable.Range(1, 9).Where(r => counts[new Tile(suit, r).KindIndex] > 0).ToList();
                if (ranks.Count != 3 || ranks[1] - ranks[0] < 3 || ranks[2] - ranks[1] < 3)
                    return false;
                if (!(ranks[1] - ranks[0] == 3 && ranks[2] - ranks[1] == 3))
                    pure = false;                   // not 1-4-7 / 2-5-8 / 3-6-9
            }
            return true;
        }

        public static bool IsSpecialShape(IReadOnlyList<Tile> concealed, IReadOnlyList<Meld> melds) =>
            IsLikGu(concealed, melds) || IsSixteenUnrelated(concealed, melds, out _);

        private static int[] Counts(IEnumerable<Tile> tiles)
        {
            var counts = new int[TileSet.PlayableKindCount];
            foreach (Tile t in tiles)
                if (t.IsValid && !t.IsBonus)
                    counts[t.KindIndex]++;
            return counts;
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>The best reading of a winning hand, or null if it isn't one.</summary>
        public static FaanResult Calculate(WinContext ctx, RuleSet rules)
        {
            TaiwaneseTable table = rules.Taiwanese;
            FaanResult best = null;
            void Consider(FaanResult r)
            {
                if (r != null && (best == null || r.Total > best.Total))
                    best = r;
            }

            List<HandDecomposition> decompositions = HandDecomposer.Decompose(ctx.Concealed, ctx.Melds, rules.HandSets);
            bool lik = IsLikGu(ctx.Concealed, ctx.Melds);
            bool sixteen = IsSixteenUnrelated(ctx.Concealed, ctx.Melds, out bool pure);
            if (decompositions.Count == 0 && !lik && !sixteen)
                return null;

            // How the hand was waiting before the winning tile.
            var before = new List<Tile>(ctx.Concealed);
            before.Remove(ctx.WinningTile);
            List<Tile> waits = FaanCalculator.GetWaits(before, ctx.Melds, rules);

            foreach (HandDecomposition d in decompositions)
                Consider(Finish(ctx, table, Standard(ctx, rules, table, d, before, waits), HandShape.Standard, d));
            if (lik)
            {
                var items = new List<TwHand>();
                int winningKindCount = ctx.Concealed.Count(t => t == ctx.WinningTile);
                items.Add(winningKindCount == 3 ? TwHand.LikGuEightPairs : TwHand.LikGuSingleWait);
                Common(ctx, table, items, special: true);
                Consider(Finish(ctx, table, items, HandShape.LikGuLikGu, null));
            }
            if (sixteen)
            {
                var items = new List<TwHand> { pure ? TwHand.SixteenUnrelatedPure : TwHand.SixteenUnrelated };
                Common(ctx, table, items, special: true);
                items.Remove(TwHand.FiveGates);    // always true for this shape
                Consider(Finish(ctx, table, items, HandShape.SixteenUnrelated, null));
            }
            return best;
        }

        /// <summary>Applies the Chicken Hand rule and turns the hand list into a result.</summary>
        private static FaanResult Finish(WinContext ctx, TaiwaneseTable table, List<TwHand> hands, HandShape shape, HandDecomposition d)
        {
            var items = hands.GroupBy(h => h)
                .Select(g => new FaanItem(TaiwaneseTable.English(g.Key), TaiwaneseTable.Chinese(g.Key), g.Count(), table[g.Key] * g.Count()))
                .Where(i => i.Faan > 0)
                .ToList();
            if (!ctx.SelfDrawn && items.Sum(i => i.Faan) == 1)
            {
                items = new List<FaanItem>
                {
                    new FaanItem(TaiwaneseTable.English(TwHand.ChickenHand), TaiwaneseTable.Chinese(TwHand.ChickenHand), 1, table[TwHand.ChickenHand]),
                };
            }
            return new FaanResult(shape, d, items, int.MaxValue);
        }

        /// <summary>Hands about flowers, honours/suits, concealment and timing — they don't depend on the sets.</summary>
        private static void Common(WinContext ctx, TaiwaneseTable table, List<TwHand> items, bool special)
        {
            // Flowers & seasons.
            int seatNumber = (int)ctx.SeatWind;
            if (ctx.Bonus.Count == 0)
                items.Add(TwHand.NoFlowers);
            foreach (TileSuit set in new[] { TileSuit.Flower, TileSuit.Season })
            {
                List<Tile> mine = ctx.Bonus.Where(b => b.Suit == set).ToList();
                if (mine.Count == 4)
                {
                    items.Add(TwHand.OneFlowerSet);
                    continue;
                }
                foreach (Tile b in mine)
                    items.Add(b.Rank == seatNumber ? TwHand.OwnFlower : TwHand.Flower);
            }

            // Concealment.
            bool fullyConcealed = ctx.Melds.All(m => m.IsConcealed);
            if (fullyConcealed && ctx.SelfDrawn)
                items.Add(TwHand.ConcealedSelfDraw);
            else
            {
                if (fullyConcealed) items.Add(TwHand.FullyConcealed);
                if (ctx.SelfDrawn) items.Add(TwHand.SelfDrawn);
            }

            // Suits and honours.
            List<Tile> all = AllTiles(ctx);
            int suits = all.Where(t => t.IsSuited).Select(t => t.Suit).Distinct().Count();
            bool honours = all.Any(t => t.IsHonor);
            bool winds = all.Any(t => t.Suit == TileSuit.Wind), dragons = all.Any(t => t.Suit == TileSuit.Dragon);
            if (suits == 0) items.Add(TwHand.AllHonours);
            else if (suits == 1) items.Add(honours ? TwHand.MixedOneSuit : TwHand.FullFlush);
            else if (!honours && suits == 2) items.Add(TwHand.MissingOneSuit);
            else if (!honours)
            {
                if (items.Remove(TwHand.NoFlowers)) items.Add(TwHand.NoHonoursNoFlowers);
                else items.Add(TwHand.NoHonours);
            }
            if (winds && dragons && suits == 3)
                items.Add(TwHand.FiveGates);
            if (all.All(t => t.IsSuited && t.Rank >= 2 && t.Rank <= 8))
                items.Add(TwHand.AllSimples);

            // Luck & timing.
            if (ctx.SelfDrawn && ctx.KongReplacement) items.Add(TwHand.KongReplacement);
            if (ctx.SelfDrawn && ctx.FlowerReplacement) items.Add(TwHand.FlowerReplacement);
            if (ctx.RobbedKong) items.Add(TwHand.RobbingKong);
            if (ctx.SelfDrawn && ctx.LastTile) items.Add(TwHand.LastTileSelfDraw);
            if (ctx.HeavenlyHand) items.Add(TwHand.HeavenlyWin);
            else if (ctx.EarthlyHand) items.Add(TwHand.EarthlyWin);
            else if (fullyConcealed)           // claims don't count as discards, but a claim means you've already played
            {
                if (ctx.DiscardsBeforeWin <= 4) items.Add(TwHand.Within4Discards);
                else if (ctx.DiscardsBeforeWin <= 7) items.Add(TwHand.Within7Discards);
                else if (ctx.DiscardsBeforeWin <= 10) items.Add(TwHand.Within10Discards);
            }
        }

        private static List<Tile> AllTiles(WinContext ctx)
        {
            var all = new List<Tile>(ctx.Concealed);
            foreach (Meld m in ctx.Melds)
                all.AddRange(m.Tiles);
            return all;
        }

        /// <summary>Everything for a hand read as five (or four) sets plus a pair.</summary>
        private static List<TwHand> Standard(WinContext ctx, RuleSet rules, TaiwaneseTable table, HandDecomposition d,
            List<Tile> before, List<Tile> waits)
        {
            var items = new List<TwHand>();
            Common(ctx, table, items, special: false);
            IReadOnlyList<TileGroup> groups = d.Groups;
            Tile pair = d.Pair;
            bool fullyConcealed = ctx.Melds.All(m => m.IsConcealed);

            // Kongs.
            foreach (Meld m in ctx.Melds.Where(m => m.Type == MeldType.Kong))
                items.Add(m.IsConcealed ? TwHand.ConcealedKong : TwHand.ExposedKong);

            // Honour pungs.
            List<TileGroup> pungs = groups.Where(g => g.IsPungOrKong).ToList();
            int dragonPungs = pungs.Count(g => g.First.Suit == TileSuit.Dragon);
            List<TileGroup> windPungs = pungs.Where(g => g.First.Suit == TileSuit.Wind).ToList();
            if (dragonPungs == 3) items.Add(TwHand.BigThreeDragons);
            else if (dragonPungs == 2 && pair.Suit == TileSuit.Dragon) items.Add(TwHand.SmallThreeDragons);
            else for (int i = 0; i < dragonPungs; i++) items.Add(TwHand.DragonPung);
            if (windPungs.Count == 4) items.Add(TwHand.BigFourWinds);
            else if (windPungs.Count == 3 && pair.Suit == TileSuit.Wind) items.Add(TwHand.SmallFourWinds);
            else if (windPungs.Count == 3) items.Add(TwHand.BigThreeWinds);
            else if (windPungs.Count == 2 && pair.Suit == TileSuit.Wind) items.Add(TwHand.SmallThreeWinds);
            else
                foreach (TileGroup w in windPungs)
                    items.Add(w.First.Rank == (int)ctx.PrevailingWind ? TwHand.RoundWindPung : TwHand.WindPung);

            // Pungs.
            bool allPungs = groups.All(g => g.IsPungOrKong);
            int concealedPungs = CountConcealedPungs(ctx, d);
            if (allPungs && fullyConcealed && ctx.SelfDrawn)
            {
                items.Add(TwHand.ConcealedAllPungs);     // replaces All Pungs, the concealed-pung count and Concealed Self-Draw
                items.Remove(TwHand.ConcealedSelfDraw);
            }
            else
            {
                if (allPungs) items.Add(TwHand.AllPungs);
                if (concealedPungs >= 5) items.Add(TwHand.ConcealedPungs5);
                else if (concealedPungs == 4) items.Add(TwHand.ConcealedPungs4);
                else if (concealedPungs == 3) items.Add(TwHand.ConcealedPungs3);
                else if (concealedPungs == 2) items.Add(TwHand.ConcealedPungs2);
            }

            // Chows.
            List<TileGroup> chows = groups.Where(g => g.Type == MeldType.Chow).ToList();
            if (chows.Count == groups.Count)
            {
                items.Add(TwHand.PingWu);
                if (items.Contains(TwHand.NoHonoursNoFlowers)) items.Add(TwHand.BigPingWu);
            }
            foreach (IGrouping<int, TileGroup> same in chows.GroupBy(c => (int)c.First.Rank))
            {
                int n = same.Count();
                int maxSameSuit = same.GroupBy(c => c.First.Suit).Max(g => g.Count());
                int distinctSuits = same.Select(c => c.First.Suit).Distinct().Count();
                if (n >= 5) items.Add(TwHand.FiveSameNumberChows);
                else if (n == 4) items.Add(maxSameSuit == 4 ? TwHand.IdenticalChows4 : TwHand.FourSameNumberChows);
                else if (n == 3)
                {
                    if (maxSameSuit == 3) items.Add(TwHand.IdenticalChows3);
                    else if (distinctSuits == 3) items.Add(TwHand.MixedChows3);
                    else { items.Add(TwHand.IdenticalChows2); items.Add(TwHand.MixedChows2); }
                }
                else if (n == 2) items.Add(maxSameSuit == 2 ? TwHand.IdenticalChows2 : TwHand.MixedChows2);
            }

            // Dragons (1-9 straights).
            bool Hidden(TileGroup g) => g.Concealed && (ctx.SelfDrawn || !g.Contains(ctx.WinningTile));
            TwHand? dragon = null;
            foreach (TileSuit suit in new[] { TileSuit.Dots, TileSuit.Bamboo, TileSuit.Characters })
            {
                var run = new[] { 1, 4, 7 }.Select(r => chows.FirstOrDefault(c => c.First.Suit == suit && c.First.Rank == r)).ToList();
                if (run.All(c => c.First.IsValid))
                    dragon = run.All(Hidden) ? TwHand.ConcealedDragon : TwHand.ExposedDragon;
            }
            if (dragon == null)
            {
                foreach (var (a, b, c) in new[] { (0, 1, 2), (0, 2, 1), (1, 0, 2), (1, 2, 0), (2, 0, 1), (2, 1, 0) })
                {
                    var suitsOrder = new[] { TileSuit.Dots, TileSuit.Bamboo, TileSuit.Characters };
                    TileGroup low = chows.FirstOrDefault(x => x.First.Suit == suitsOrder[a] && x.First.Rank == 1);
                    TileGroup mid = chows.FirstOrDefault(x => x.First.Suit == suitsOrder[b] && x.First.Rank == 4);
                    TileGroup high = chows.FirstOrDefault(x => x.First.Suit == suitsOrder[c] && x.First.Rank == 7);
                    if (low.First.IsValid && mid.First.IsValid && high.First.IsValid)
                    {
                        dragon = Hidden(low) && Hidden(mid) && Hidden(high) ? TwHand.ConcealedMixedDragon : TwHand.ExposedMixedDragon;
                        break;
                    }
                }
            }
            if (dragon.HasValue) items.Add(dragon.Value);

            // Sisters: consecutive pungs in one suit (only the best run counts).
            TwHand? sisters = null;
            foreach (TileSuit suit in new[] { TileSuit.Dots, TileSuit.Bamboo, TileSuit.Characters })
            {
                var ranks = new HashSet<int>(pungs.Where(p => p.First.Suit == suit).Select(p => (int)p.First.Rank));
                foreach (int start in ranks.Where(r => !ranks.Contains(r - 1)))
                {
                    int len = 1;
                    while (ranks.Contains(start + len)) len++;
                    if (len < 2) continue;
                    bool small = pair.Suit == suit && (pair.Rank == start - 1 || pair.Rank == start + len);
                    TwHand h = len >= 5 ? TwHand.FiveSisters
                        : len == 4 ? (small ? TwHand.SmallFiveSisters : TwHand.FourSisters)
                        : len == 3 ? (small ? TwHand.SmallFourSisters : TwHand.ThreeSisters)
                        : (small ? TwHand.SmallThreeSisters : TwHand.TwoSisters);
                    if (sisters == null || table[h] > table[sisters.Value])
                        sisters = h;
                }
            }
            if (sisters.HasValue) items.Add(sisters.Value);

            // Brothers: pungs of the same number in different suits.
            foreach (IGrouping<int, TileGroup> same in pungs.Where(p => p.First.IsSuited).GroupBy(p => (int)p.First.Rank))
            {
                int k = same.Select(p => p.First.Suit).Distinct().Count();
                if (k >= 3) items.Add(TwHand.BigThreeBrothers);
                else if (k == 2)
                {
                    bool small = pair.IsSuited && pair.Rank == same.Key && same.All(p => p.First.Suit != pair.Suit);
                    items.Add(small ? TwHand.SmallThreeBrothers : TwHand.TwoBrothers);
                }
            }

            // Four-in-one / two / four: all four copies of a tile used, not as a kong.
            List<Tile> all = AllTiles(ctx);
            foreach (Tile kind in all.Distinct())
            {
                if (all.Count(t => t == kind) != 4 || pungs.Any(p => p.Type == MeldType.Kong && p.First == kind))
                    continue;
                if (pair == kind) items.Add(TwHand.FourInTwo);
                else if (!pungs.Any(p => p.First == kind)) items.Add(TwHand.FourInFour);
                else items.Add(TwHand.FourInOne);
            }

            // Outside hands: every set and the pair has a 1, 9 or honour.
            bool Outside(Tile t) => t.IsTerminalOrHonor;
            bool everySet = groups.All(g => g.Type == MeldType.Chow ? (g.First.Rank == 1 || g.First.Rank == 7) : Outside(g.First))
                            && Outside(pair);
            bool hasHonours = all.Any(t => t.IsHonor);
            if (everySet && all.Any(t => t.IsSuited))
            {
                if (allPungs) items.Add(hasHonours ? TwHand.MixedTerminals : TwHand.PureTerminals);
                else items.Add(hasHonours ? TwHand.MixedOutside : TwHand.PureOutside);
            }

            // Old & young (house): 1-2-3 and 7-8-9 in the same suit.
            if (chows.Any(c => c.First.Rank == 1 && chows.Any(o => o.First.Suit == c.First.Suit && o.First.Rank == 7)))
                items.Add(TwHand.OldAndYoung);

            // The pair.
            if (pair.IsSuited && (pair.Rank == 2 || pair.Rank == 5 || pair.Rank == 8))
                items.Add(TwHand.ElephantEyes);

            // Waits.
            int exposedSets = ctx.Melds.Count(m => !m.IsConcealed);
            if (exposedSets >= rules.HandSets)
                items.Add(ctx.SelfDrawn ? TwHand.HalfConcealed : TwHand.AllByOthers);   // single wait by nature
            else if (waits.Count == 1)
                items.Add(TwHand.SingleWait);
            else if (waits.Count == 2)
            {
                Tile other = waits[0] == ctx.WinningTile ? waits[1] : waits[0];
                if (pair == ctx.WinningTile)
                    items.Add(TwHand.FalseSingleWait);
                else if (pungs.Any(p => p.Concealed && p.First == ctx.WinningTile) && before.Count(t => t == other) == 2
                         && before.Count(t => t == ctx.WinningTile) == 2)
                    items.Add(TwHand.TwoPairWait);
            }
            return items;
        }

        /// <summary>Pungs/kongs made in the hand; one completed by the winning discard doesn't count.</summary>
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
    }
}
