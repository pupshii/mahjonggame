using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    /// <summary>The HK-style Taiwanese sheet (16-tile hands).</summary>
    public class TaiwaneseScoringTests
    {
        private static RuleSet Rules()
        {
            var r = RuleSet.Classic();
            r.HandSets = 5;
            r.MinFaan = 0;
            r.Scoring = ScoringStyle.Taiwanese;
            return r;
        }

        /// <summary>A win: <paramref name="concealed"/> includes the winning tile (the last code).</summary>
        private static WinContext Win(string concealed, bool selfDrawn, string bonus = "", Meld[] melds = null,
            Wind seat = Wind.South, Wind round = Wind.East, int discards = 12)
        {
            List<Tile> tiles = Tile.ParseList(concealed);
            return new WinContext
            {
                Seat = 1,
                SeatWind = seat,
                PrevailingWind = round,
                Concealed = tiles,
                WinningTile = tiles.Last(),
                Melds = melds ?? new Meld[0],
                Bonus = bonus.Length == 0 ? new List<Tile>() : Tile.ParseList(bonus),
                SelfDrawn = selfDrawn,
                DiscardsBeforeWin = discards,
            };
        }

        private static FaanResult Score(WinContext ctx) => FaanCalculator.Calculate(ctx, Rules());

        private static int Fan(FaanResult r, TwHand h) =>
            r.Items.Where(i => i.Name == TaiwaneseTable.English(h)).Sum(i => i.Faan);

        private static bool Has(FaanResult r, TwHand h) => r.Items.Any(i => i.Name == TaiwaneseTable.English(h));

        [Test]
        public void ConcealedSelfDraw_ReplacesFullyConcealedAndSelfDrawn()
        {
            // 123B 456B 789C 234D 678D + 55C (elephant eyes), self-drawn, one flower.
            FaanResult r = Score(Win("1B 2B 3B 4B 5B 6B 7C 8C 9C 2D 3D 4D 6D 7D 8D 5C 5C", selfDrawn: true, bonus: "F3"));
            Assert.That(Has(r, TwHand.ConcealedSelfDraw), Is.True);
            Assert.That(Has(r, TwHand.FullyConcealed) || Has(r, TwHand.SelfDrawn), Is.False);
            Assert.That(Has(r, TwHand.PingWu), Is.True);
            Assert.That(Has(r, TwHand.ElephantEyes), Is.True);
            Assert.That(Has(r, TwHand.NoHonours), Is.True);
            Assert.That(Fan(r, TwHand.Flower), Is.EqualTo(1), "F3 isn't South's flower");
            Assert.That(r.Total, Is.EqualTo(5 + 5 + 1 + 1 + 1 + 2), "+ single wait on 5C");
        }

        [Test]
        public void OwnFlower_IsWorthTwo_AndAFullSetIsTen()
        {
            FaanResult r = Score(Win("1B 2B 3B 4B 5B 6B 7C 8C 9C 2D 3D 4D 6D 7D 8D E E", selfDrawn: true, bonus: "F2 S2"));
            Assert.That(Fan(r, TwHand.OwnFlower), Is.EqualTo(4), "South = 2, for both a flower and a season");
            FaanResult set = Score(Win("1B 2B 3B 4B 5B 6B 7C 8C 9C 2D 3D 4D 6D 7D 8D E E", selfDrawn: true, bonus: "F1 F2 F3 F4"));
            Assert.That(Fan(set, TwHand.OneFlowerSet), Is.EqualTo(10));
            Assert.That(Has(set, TwHand.OwnFlower) || Has(set, TwHand.Flower), Is.False);
        }

        [Test]
        public void ChickenHand_WinOffADiscardWorthOnlyOne_CountsAsTen()
        {
            // Open hand with honours: just "No flowers" = 1 → Chicken Hand 10.
            var melds = new[] { Meld.Chow(Tile.Parse("1B"), 0, Tile.Parse("2B")), Meld.Pung(Tile.Parse("6C"), 0) };
            FaanResult r = Score(Win("S S 7D 8D 3B 4B 5B 2C 3C 4C 9D", selfDrawn: false, melds: melds));   // two-sided wait (6D/9D)
            Assert.That(r.Items.Count, Is.EqualTo(1), r.ToString());
            Assert.That(r.Items.Single().Name, Is.EqualTo(TaiwaneseTable.English(TwHand.ChickenHand)));
            Assert.That(r.Total, Is.EqualTo(10));
        }

        [Test]
        public void HonourPungs_AndTheBigVersionsReplaceThem()
        {
            var melds = new[] { Meld.Pung(Tile.Parse("RD"), 0), Meld.Pung(Tile.Parse("GD"), 0) };
            FaanResult small = Score(Win("1B 2B 3B 4C 5C 6C 7D 8D 9D WD WD", selfDrawn: true, melds: melds));
            Assert.That(Has(small, TwHand.SmallThreeDragons), Is.True);
            Assert.That(Has(small, TwHand.DragonPung), Is.False);

            var windMelds = new[] { Meld.Pung(Tile.Parse("E"), 0), Meld.Pung(Tile.Parse("W"), 0) };
            FaanResult winds = Score(Win("1B 2B 3B 4C 5C 6C 7D 8D 9D 2B 2B", selfDrawn: true, melds: windMelds));
            Assert.That(Fan(winds, TwHand.RoundWindPung), Is.EqualTo(2), "East in the East round");
            Assert.That(Fan(winds, TwHand.WindPung), Is.EqualTo(1));
        }

        [Test]
        public void AllPungs_AndConcealedAllPungs()
        {
            FaanResult open = Score(Win("2B 2B 2B 5C 5C 5C 9D 9D 9D N N N RD RD RD 7B 7B", selfDrawn: false));
            Assert.That(Has(open, TwHand.AllPungs), Is.True);
            FaanResult hidden = Score(Win("2B 2B 2B 5C 5C 5C 9D 9D 9D N N N RD RD 7B 7B RD", selfDrawn: true));
            Assert.That(Has(hidden, TwHand.ConcealedAllPungs), Is.True);
            Assert.That(Has(hidden, TwHand.AllPungs) || Has(hidden, TwHand.ConcealedSelfDraw) || Has(hidden, TwHand.ConcealedPungs5), Is.False);
        }

        [Test]
        public void Suits_FullFlush_MixedOneSuit_MissingOneSuit()
        {
            Assert.That(Has(Score(Win("1B 2B 3B 2B 3B 4B 4B 5B 6B 6B 7B 8B 7B 8B 9B 9B 9B", selfDrawn: true)), TwHand.FullFlush), Is.True);
            Assert.That(Has(Score(Win("1B 2B 3B 2B 3B 4B 4B 5B 6B 6B 7B 8B E E E N N", selfDrawn: true)), TwHand.MixedOneSuit), Is.True);
            FaanResult missing = Score(Win("1B 2B 3B 2B 3B 4B 4B 5B 6B 6C 7C 8C 7C 8C 9C 5C 5C", selfDrawn: true));
            Assert.That(Has(missing, TwHand.MissingOneSuit), Is.True);
            Assert.That(Has(missing, TwHand.NoHonours), Is.False, "Don't also score No Honours");
        }

        [Test]
        public void Sisters_Brothers_SameNumberChows()
        {
            // Pungs of 3D 4D 5D + 6D pair = Small Four Sisters.
            FaanResult sisters = Score(Win("3D 3D 3D 4D 4D 4D 5D 5D 5D 1B 2B 3B 7C 8C 9C 6D 6D", selfDrawn: true));
            Assert.That(Has(sisters, TwHand.SmallFourSisters), Is.True);
            Assert.That(Has(sisters, TwHand.ThreeSisters) || Has(sisters, TwHand.TwoSisters), Is.False);

            FaanResult brothers = Score(Win("5C 5C 5C 5D 5D 5D 5B 5B 5B 1B 2B 3B 7C 8C 9C E E", selfDrawn: true));
            Assert.That(Has(brothers, TwHand.BigThreeBrothers), Is.True);

            FaanResult chows = Score(Win("1B 2B 3B 1B 2B 3B 1C 2C 3C 5D 6D 7D 7D 8D 9D E E", selfDrawn: true));
            Assert.That(Has(chows, TwHand.IdenticalChows2), Is.True);
            Assert.That(Has(chows, TwHand.MixedChows2), Is.True);
        }

        [Test]
        public void Dragon_Straights()
        {
            FaanResult pure = Score(Win("1C 2C 3C 4C 5C 6C 7C 8C 9C 2D 3D 4D 6B 7B 8B E E", selfDrawn: true));
            Assert.That(Has(pure, TwHand.ConcealedDragon), Is.True);
            FaanResult mixed = Score(Win("1C 2C 3C 4D 5D 6D 7B 8B 9B 2D 3D 4D 6B 7B 8B E E", selfDrawn: true));
            Assert.That(Has(mixed, TwHand.ConcealedMixedDragon), Is.True);
        }

        [Test]
        public void LikGuLikGu_IsAWinningShape_WorthFortyOrFifty()
        {
            RuleSet rules = Rules();
            List<Tile> eightPairWait = Tile.ParseList("1B 1B 4C 4C 2D 2D 7D 7D 9B 9B 3C 3C S S 5B 5B 5B");
            Assert.That(FaanCalculator.IsCompleteShape(eightPairWait, new Meld[0], rules), Is.True);
            Assert.That(Has(Score(Win(string.Join(" ", eightPairWait), selfDrawn: true)), TwHand.LikGuEightPairs), Is.True);
            FaanResult single = Score(Win("1B 1B 4C 4C 2D 2D 7D 7D 9B 9B 5B 5B 5B 3C 3C S S", selfDrawn: true));
            Assert.That(Has(single, TwHand.LikGuSingleWait) || Has(single, TwHand.LikGuEightPairs), Is.True);

            var classic = Rules();
            classic.Scoring = ScoringStyle.HongKong;
            Assert.That(FaanCalculator.IsCompleteShape(eightPairWait, new Meld[0], classic), Is.False, "Only with Taiwanese scoring");
        }

        [Test]
        public void SixteenUnrelated_PureAndMixed()
        {
            // 1-4-7 dots, 2-5-8 bamboo, 3-6-9 characters, every honour, + a second East.
            FaanResult pure = Score(Win("1D 4D 7D 2B 5B 8B 3C 6C 9C E S W N RD GD WD E", selfDrawn: true));
            Assert.That(Has(pure, TwHand.SixteenUnrelatedPure), Is.True);
            FaanResult mixed = Score(Win("1D 4D 8D 2B 5B 8B 3C 6C 9C E S W N RD GD WD E", selfDrawn: true));
            Assert.That(Has(mixed, TwHand.SixteenUnrelated), Is.True);
        }

        [Test]
        public void Timing_WithinDiscards_AndHeavenly()
        {
            FaanResult fast = Score(Win("1B 2B 3B 4B 5B 6B 7C 8C 9C 2D 3D 4D 6D 7D 8D E E", selfDrawn: true, discards: 3));
            Assert.That(Has(fast, TwHand.Within4Discards), Is.True);
            WinContext heaven = Win("1B 2B 3B 4B 5B 6B 7C 8C 9C 2D 3D 4D 6D 7D 8D E E", selfDrawn: true, discards: 0);
            heaven.HeavenlyHand = true;
            FaanResult h = Score(heaven);
            Assert.That(Fan(h, TwHand.HeavenlyWin), Is.EqualTo(120));
            Assert.That(Has(h, TwHand.Within4Discards), Is.False, "Heavenly replaces it");
        }

        [Test]
        public void HalfConcealed_AndAllByOthers()
        {
            var melds = new[]
            {
                Meld.Chow(Tile.Parse("1B"), 0, Tile.Parse("1B")), Meld.Chow(Tile.Parse("4B"), 0, Tile.Parse("4B")),
                Meld.Pung(Tile.Parse("9C"), 2), Meld.Chow(Tile.Parse("2D"), 0, Tile.Parse("2D")), Meld.Pung(Tile.Parse("N"), 3),
            };
            Assert.That(Has(Score(Win("5C 5C", selfDrawn: true, melds: melds)), TwHand.HalfConcealed), Is.True);
            FaanResult others = Score(Win("5C 5C", selfDrawn: false, melds: melds));
            Assert.That(Has(others, TwHand.AllByOthers), Is.True);
            Assert.That(Has(others, TwHand.SingleWait), Is.False);
        }

        [Test]
        public void DealerBonus_GrowsWithTheStreak()
        {
            var match = new ScoreMatch(Rules(), MatchRules.Default(), 1);
            Assert.That(match.DealerWinBonus, Is.EqualTo(1));
            var hk = new ScoreMatch(RuleSet.Classic(), MatchRules.Default(), 1);
            Assert.That(hk.DealerWinBonus, Is.EqualTo(0));
        }

        [Test]
        public void RandomHands_ScoreWithoutErrors_AndBotsPlayMatches()
        {
            int wins = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var match = new ScoreMatch(Rules(), MatchRules.Default(), seed);
                for (int h = 0; h < 4 && match.Phase != MatchPhase.MatchOver; h++)
                {
                    match.StartHand();
                    HandState hand = match.Hand;
                    var bots = Enumerable.Range(0, 4).Select(s => new SimpleBot(seed * 17 + (ulong)(h * 4 + s), ambition: 0.5)).ToArray();
                    for (int step = 0; hand.Phase != HandPhase.Ended; step++)
                    {
                        Assert.That(step, Is.LessThan(20000));
                        int seat = hand.SeatToAct();
                        hand.Apply(bots[seat].Choose(hand, seat, hand.GetLegalActions(seat)));
                        Assert.That(HandInvariants.Check(hand), Is.Empty);
                    }
                    if (hand.Result.Outcome == HandOutcome.Win)
                    {
                        wins++;
                        FaanResult r = FaanCalculator.Calculate(hand.Result.WinContexts[0], hand.Rules);
                        Assert.That(r, Is.Not.Null);
                        Assert.That(r.Total, Is.GreaterThan(0), "Every Taiwanese win scores something");
                    }
                    match.ResolveHand();
                    Assert.That(match.Players.Sum(p => p.Score), Is.EqualTo(4 * MatchRules.Default().StartingScore), "Zero-sum");
                }
            }
            Assert.That(wins, Is.GreaterThan(10));
        }
    }
}
