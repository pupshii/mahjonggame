using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class FaanTests
    {
        /// <summary>"pung:RD chow:1D kong:5B ckong:E" → melds (claimed ones from seat 1).</summary>
        private static List<Meld> Melds(string spec)
        {
            var melds = new List<Meld>();
            foreach (string part in (spec ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                Tile t = Tile.Parse(kv[1]);
                switch (kv[0])
                {
                    case "chow": melds.Add(Meld.Chow(t, 1, t)); break;
                    case "pung": melds.Add(Meld.Pung(t, 1)); break;
                    case "kong": melds.Add(Meld.ExposedKong(t, 1)); break;
                    case "ckong": melds.Add(Meld.ConcealedKong(t)); break;
                    default: throw new ArgumentException(part);
                }
            }
            return melds;
        }

        /// <summary>Builds a context; the winning tile defaults to the last concealed tile.</summary>
        private static WinContext Ctx(string concealed, string melds = "", string bonus = "", bool selfDrawn = false,
            Wind seat = Wind.East, Wind prevailing = Wind.East, string win = null)
        {
            List<Tile> tiles = Tile.ParseList(concealed);
            return new WinContext
            {
                SeatWind = seat,
                PrevailingWind = prevailing,
                Concealed = tiles,
                Melds = Melds(melds),
                Bonus = Tile.ParseList(bonus),
                SelfDrawn = selfDrawn,
                WinningTile = win != null ? Tile.Parse(win) : tiles[tiles.Count - 1],
            };
        }

        private static FaanResult Score(WinContext ctx, RuleSet rules = null) =>
            FaanCalculator.Calculate(ctx, rules ?? RuleSet.Classic());

        private static void AssertPatterns(FaanResult r, params FaanPattern[] expected)
        {
            Assert.That(r, Is.Not.Null, "Expected a winning hand");
            Assert.That(r.Items.Select(i => i.Pattern), Is.EquivalentTo(expected), r.ToString());
        }

        private const string ChickenHand = "1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D 5D";

        // ------------------------------------------------------------------ shape

        [Test]
        public void IncompleteHand_IsNotAWin()
        {
            Assert.That(Score(Ctx("1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D 6D")), Is.Null);
            Assert.That(Score(Ctx("1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D")), Is.Null, "13 tiles");
        }

        [Test]
        public void AllChows_OnDiscard()
        {
            FaanResult r = Score(Ctx(ChickenHand));
            AssertPatterns(r, FaanPattern.AllChows, FaanPattern.ConcealedHand, FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(3));
            Assert.That(r.Shape, Is.EqualTo(HandShape.Standard));
        }

        [Test]
        public void SelfDrawn_ReplacesConcealedHand()
        {
            FaanResult r = Score(Ctx(ChickenHand, selfDrawn: true));
            AssertPatterns(r, FaanPattern.AllChows, FaanPattern.SelfDrawn, FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(3));
        }

        [Test]
        public void ConcealedKong_KeepsHandConcealed()
        {
            FaanResult r = Score(Ctx("1D 2D 3D 4B 5B 6B 7C 8C 9C 5D 5D", melds: "ckong:N"));
            Assert.That(r.Has(FaanPattern.ConcealedHand), Is.True);
            Assert.That(Score(Ctx("1D 2D 3D 4B 5B 6B 7C 8C 9C 5D 5D", melds: "kong:N")).Has(FaanPattern.ConcealedHand), Is.False);
        }

        // ------------------------------------------------------------------ bonus tiles

        [Test]
        public void OwnFlower_MatchesSeatWind()
        {
            FaanResult r = Score(Ctx(ChickenHand, bonus: "F2 S3", seat: Wind.South));
            AssertPatterns(r, FaanPattern.AllChows, FaanPattern.ConcealedHand, FaanPattern.OwnFlower);
        }

        [Test]
        public void OwnSeason_MatchesSeatWind()
        {
            FaanResult r = Score(Ctx(ChickenHand, bonus: "S4", seat: Wind.North));
            Assert.That(r.Has(FaanPattern.OwnSeason), Is.True);
            Assert.That(r.Has(FaanPattern.NoFlowers), Is.False);
        }

        [Test]
        public void FlowerSet_ReplacesOwnFlower()
        {
            FaanResult r = Score(Ctx(ChickenHand, bonus: "F1 F2 F3 F4 S1"));
            AssertPatterns(r, FaanPattern.AllChows, FaanPattern.ConcealedHand, FaanPattern.FlowerSet, FaanPattern.OwnSeason);
            Assert.That(r.Total, Is.EqualTo(5));
        }

        [Test]
        public void EightFlowers_IsLimit()
        {
            FaanResult r = Score(Ctx(ChickenHand, bonus: "F1 F2 F3 F4 S1 S2 S3 S4"));
            Assert.That(r.IsLimit, Is.True);
            Assert.That(r.Has(FaanPattern.EightFlowers), Is.True);
            Assert.That(r.Total, Is.EqualTo(13));
        }

        // ------------------------------------------------------------------ honour sets

        [Test]
        public void DragonAndWindPungs()
        {
            FaanResult r = Score(Ctx("E E E 1D 2D 3D 9C 9C", melds: "pung:RD pung:S", seat: Wind.South));
            AssertPatterns(r, FaanPattern.DragonPung, FaanPattern.SeatWindPung, FaanPattern.PrevailingWindPung,
                FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(4));
        }

        [Test]
        public void SameSeatAndPrevailingWind_ScoresTwice()
        {
            FaanResult r = Score(Ctx("E E E 1D 2D 3D 9C 9C", melds: "chow:4B chow:1C"));
            Assert.That(r.Has(FaanPattern.SeatWindPung), Is.True);
            Assert.That(r.Has(FaanPattern.PrevailingWindPung), Is.True);
        }

        [Test]
        public void SmallThreeDragons_ReplacesDragonPungs()
        {
            FaanResult r = Score(Ctx("RD RD RD GD GD GD WD WD 1B 2B 3B 5C 6C 7C"));
            AssertPatterns(r, FaanPattern.SmallThreeDragons, FaanPattern.ConcealedHand, FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(7));
        }

        [Test]
        public void TwoDragonPungs_WithoutDragonPair_AreNotSmallThreeDragons()
        {
            FaanResult r = Score(Ctx("RD RD RD GD GD GD 9B 9B 1B 2B 3B 5C 6C 7C"));
            Assert.That(r.Items.Single(i => i.Pattern == FaanPattern.DragonPung).Count, Is.EqualTo(2));
            Assert.That(r.Has(FaanPattern.SmallThreeDragons), Is.False);
        }

        // ------------------------------------------------------------------ structure & suits

        [Test]
        public void AllPungs()
        {
            FaanResult r = Score(Ctx("7C 7C 7C 9D 9D 9D N N", melds: "pung:2D pung:5B"));
            AssertPatterns(r, FaanPattern.AllPungs, FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(4));
        }

        [Test]
        public void MixedOneSuit()
        {
            FaanResult r = Score(Ctx("1D 2D 3D 4D 5D 6D 7D 7D 7D RD RD RD E E"));
            AssertPatterns(r, FaanPattern.MixedOneSuit, FaanPattern.DragonPung, FaanPattern.ConcealedHand, FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(6));
        }

        [Test]
        public void PureOneSuit()
        {
            FaanResult r = Score(Ctx("1D 2D 3D 4D 5D 6D 7D 8D 9D 2D 3D 4D 5D 5D"));
            AssertPatterns(r, FaanPattern.PureOneSuit, FaanPattern.AllChows, FaanPattern.ConcealedHand, FaanPattern.NoFlowers);
            Assert.That(r.Total, Is.EqualTo(10));
        }

        [Test]
        public void Total_IsCappedAtMaxFaan()
        {
            WinContext ctx = Ctx("3D 3D 3D 5D 5D 5D 7D 7D 7D 9D 9D", melds: "pung:1D", selfDrawn: true);
            FaanResult classic = Score(ctx, RuleSet.Classic());
            Assert.That(classic.RawTotal, Is.EqualTo(12)); // pure 7 + all pungs 3 + self-drawn 1 + no flowers 1
            Assert.That(classic.Total, Is.EqualTo(12));

            FaanResult party = Score(ctx, RuleSet.Party());
            Assert.That(party.Total, Is.EqualTo(10), "Party caps at 10");
            Assert.That(party.IsLimit, Is.False);
        }

        // ------------------------------------------------------------------ best reading

        [Test]
        public void AmbiguousHand_UsesHighestScoringReading()
        {
            // 111 222 333 could be three pungs or three chows. On a 7D discard the pung reading
            // scores more (all pungs) even though it isn't four concealed pungs.
            FaanResult r = Score(Ctx("1D 1D 1D 2D 2D 2D 3D 3D 3D 7D 7D 7D 9D 9D", win: "7D"));
            Assert.That(r.Has(FaanPattern.AllPungs), Is.True, r.ToString());
            Assert.That(r.Has(FaanPattern.FourConcealedPungs), Is.False);
            Assert.That(r.Total, Is.EqualTo(12));
        }

        [Test]
        public void AmbiguousHand_SelfDrawn_IsFourConcealedPungs()
        {
            FaanResult r = Score(Ctx("1D 1D 1D 2D 2D 2D 3D 3D 3D 7D 7D 7D 9D 9D", selfDrawn: true));
            Assert.That(r.IsLimit, Is.True);
            Assert.That(r.Has(FaanPattern.FourConcealedPungs), Is.True);
        }

        // ------------------------------------------------------------------ limit hands

        [TestCase("RD RD RD GD GD GD WD WD WD 1B 2B 3B 5C 5C", FaanPattern.BigThreeDragons)]
        [TestCase("E E E S S S W W W N N 1B 2B 3B", FaanPattern.SmallFourWinds)]
        [TestCase("E E E S S S W W W N N N 5C 5C", FaanPattern.BigFourWinds)]
        [TestCase("E E E S S S RD RD RD GD GD GD WD WD", FaanPattern.AllHonors)]
        [TestCase("1D 1D 1D 9D 9D 9D 1B 1B 1B 9C 9C 9C 1C 1C", FaanPattern.AllTerminals)]
        [TestCase("1D 9D 1B 9B 1C 9C E S W N RD GD WD WD", FaanPattern.ThirteenOrphans)]
        [TestCase("1D 1D 1D 2D 3D 4D 5D 6D 7D 8D 9D 9D 9D 5D", FaanPattern.NineGates)]
        public void LimitHands(string concealed, FaanPattern pattern)
        {
            FaanResult r = Score(Ctx(concealed));
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Has(pattern), Is.True, r.ToString());
            Assert.That(r.IsLimit, Is.True);
            Assert.That(r.Total, Is.EqualTo(13));
        }

        [Test]
        public void ThirteenOrphans_HasItsOwnShape()
        {
            FaanResult r = Score(Ctx("1D 9D 1B 9B 1C 9C E S W N RD GD WD 1D"));
            Assert.That(r.Shape, Is.EqualTo(HandShape.ThirteenOrphans));
            Assert.That(r.Decomposition, Is.Null);
        }

        [Test]
        public void FourConcealedPungs_OnDiscard_NeedsPairWait()
        {
            const string hand = "2D 2D 2D 5B 5B 5B 8C 8C 8C E E E N N";
            Assert.That(Score(Ctx(hand, win: "N")).Has(FaanPattern.FourConcealedPungs), Is.True, "Won on the pair");

            FaanResult onPung = Score(Ctx(hand, win: "8C"));
            Assert.That(onPung.Has(FaanPattern.FourConcealedPungs), Is.False, "Discard completed a pung");
            Assert.That(onPung.Has(FaanPattern.AllPungs), Is.True);
            Assert.That(Score(Ctx(hand, win: "8C", selfDrawn: true)).Has(FaanPattern.FourConcealedPungs), Is.True);
        }

        [Test]
        public void FourKongs()
        {
            FaanResult r = Score(Ctx("N N", melds: "kong:1D kong:5B ckong:9C kong:E"));
            Assert.That(r.Has(FaanPattern.FourKongs), Is.True);
            Assert.That(r.IsLimit, Is.True);
        }

        [Test]
        public void HeavenlyAndEarthlyHands_AreLimits()
        {
            WinContext heavenly = Ctx(ChickenHand, selfDrawn: true);
            heavenly.HeavenlyHand = true;
            Assert.That(Score(heavenly).Has(FaanPattern.HeavenlyHand), Is.True);
            Assert.That(Score(heavenly).IsLimit, Is.True);

            WinContext earthly = Ctx(ChickenHand);
            earthly.EarthlyHand = true;
            Assert.That(Score(earthly).Has(FaanPattern.EarthlyHand), Is.True);
            Assert.That(Score(earthly).IsLimit, Is.True);
        }

        // ------------------------------------------------------------------ way of winning

        [Test]
        public void LastTile_OnlyCountsWhenSelfDrawn()
        {
            WinContext selfDrawn = Ctx(ChickenHand, selfDrawn: true);
            selfDrawn.LastTile = true;
            Assert.That(Score(selfDrawn).Has(FaanPattern.LastTile), Is.True);

            WinContext discard = Ctx(ChickenHand);
            discard.LastTile = true;
            Assert.That(Score(discard).Has(FaanPattern.LastTile), Is.False);
        }

        [Test]
        public void KongReplacementAndRobbingKong()
        {
            WinContext replacement = Ctx("1D 2D 3D 4B 5B 6B 7C 8C 9C 5D 5D", melds: "ckong:N", selfDrawn: true);
            replacement.KongReplacement = true;
            Assert.That(Score(replacement).Has(FaanPattern.KongReplacement), Is.True);

            WinContext robbed = Ctx(ChickenHand);
            robbed.RobbedKong = true;
            Assert.That(Score(robbed).Has(FaanPattern.RobbingKong), Is.True);
        }

        // ------------------------------------------------------------------ seven pairs

        [Test]
        public void SevenPairs_OnlyWhenAllowed()
        {
            const string pairs = "1D 1D 3D 3D 5B 5B 7B 7B 9C 9C E E RD RD";
            FaanResult party = Score(Ctx(pairs), RuleSet.Party());
            AssertPatterns(party, FaanPattern.SevenPairs, FaanPattern.ConcealedHand, FaanPattern.NoFlowers);
            Assert.That(party.Total, Is.EqualTo(6));
            Assert.That(Score(Ctx(pairs), RuleSet.Classic()), Is.Null);
        }

        [Test]
        public void SevenPairs_CombineWithSuitPatterns()
        {
            FaanResult r = Score(Ctx("1D 1D 2D 2D 4D 4D 5D 5D 7D 7D 8D 8D 9D 9D"), RuleSet.Party());
            Assert.That(r.Has(FaanPattern.PureOneSuit), Is.True);
            Assert.That(r.RawTotal, Is.EqualTo(13));
            Assert.That(r.Total, Is.EqualTo(10));
        }

        [Test]
        public void SevenPairs_RejectsFourOfAKind()
        {
            Assert.That(FaanCalculator.IsSevenPairs(Tile.ParseList("1D 1D 1D 1D 5B 5B 7B 7B 9C 9C E E RD RD")), Is.False);
        }

        // ------------------------------------------------------------------ waits & validator

        [Test]
        public void Waits_FindCompletingTiles()
        {
            List<Tile> waits = FaanCalculator.GetWaits(Tile.ParseList("1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 5D 5D"),
                new Meld[0], RuleSet.Classic());
            Assert.That(waits, Is.EqualTo(Tile.ParseList("1C 4C")));
        }

        [Test]
        public void Waits_ThirteenOrphansThirteenWayWait()
        {
            List<Tile> waits = FaanCalculator.GetWaits(Tile.ParseList("1D 9D 1B 9B 1C 9C E S W N RD GD WD"),
                new Meld[0], RuleSet.Classic());
            Assert.That(waits, Has.Count.EqualTo(13));
        }

        [Test]
        public void Waits_EmptyWhenNotReady()
        {
            Assert.That(FaanCalculator.GetWaits(Tile.ParseList("1D 4D 7D 1B 4B 7B 1C 4C 7C E S W N"),
                new Meld[0], RuleSet.Classic()), Is.Empty);
        }

        [Test]
        public void Waits_SevenPairsOnlyWhenAllowed()
        {
            List<Tile> hand = Tile.ParseList("1D 1D 3D 3D 5B 5B 7B 7B 9C 9C E E RD");
            Assert.That(FaanCalculator.GetWaits(hand, new Meld[0], RuleSet.Party()), Is.EqualTo(Tile.ParseList("RD")));
            Assert.That(FaanCalculator.GetWaits(hand, new Meld[0], RuleSet.Classic()), Is.Empty);
        }

        [Test]
        public void Validator_EnforcesMinimumFaan()
        {
            // All chows + concealed, but a non-own flower removes "no flowers": 2 faan.
            WinContext twoFaan = Ctx(ChickenHand, bonus: "F3");
            Assert.That(Score(twoFaan).Total, Is.EqualTo(2));
            Assert.That(new FaanWinValidator(RuleSet.Classic()).IsWinningHand(twoFaan), Is.False, "Classic needs 3");
            Assert.That(new FaanWinValidator(RuleSet.Party()).IsWinningHand(twoFaan), Is.False, "Party needs 3");
            RuleSet anything = RuleSet.Party();
            anything.MinFaan = 0;
            Assert.That(new FaanWinValidator(anything).IsWinningHand(twoFaan), Is.True, "0 minimum");
            Assert.That(new FaanWinValidator(RuleSet.Classic()).IsWinningHand(Ctx(ChickenHand)), Is.True);
        }

        [Test]
        public void FaanTable_IsConfigurable()
        {
            RuleSet rules = RuleSet.Classic();
            rules.Faan[FaanPattern.AllChows] = 3;
            Assert.That(Score(Ctx(ChickenHand), rules).Total, Is.EqualTo(5));
        }
    }

    /// <summary>Real win rules running inside the turn loop.</summary>
    public class HandStateScoringTests
    {
        private const string Seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
        private const string Seat3 = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD";

        [Test]
        public void EarthlyHand_OnDealersFirstDiscard()
        {
            HandState hand = HandState.DealFromWall(RuleSet.Party(), 0, Wind.East, TestWall.Build(0, new[]
            {
                "5D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD",
                "1B 2B 3B 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D",
                Seat2, Seat3,
            }));
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(5)));
            Assert.That(hand.GetLegalActions(1), Has.Member(PlayerAction.Win(1)));
            hand.Apply(PlayerAction.Win(1));

            FaanResult score = FaanCalculator.Calculate(hand.Result.WinContexts[0], hand.Rules);
            Assert.That(score.Has(FaanPattern.EarthlyHand), Is.True, score.ToString());
            Assert.That(score.Total, Is.EqualTo(10));
            Assert.That(hand.Result.WinContexts[0].Concealed, Has.Count.EqualTo(14));
        }

        [Test]
        public void HeavenlyHand_ForDealer()
        {
            HandState hand = HandState.DealFromWall(RuleSet.Classic(), 0, Wind.East, TestWall.Build(0, new[]
            {
                "1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D 5D",
                "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD",
                Seat2, Seat3,
            }));
            Assert.That(hand.GetLegalActions(0), Has.Member(PlayerAction.SelfDrawnWin(0)));
            hand.Apply(PlayerAction.SelfDrawnWin(0));

            FaanResult score = FaanCalculator.Calculate(hand.Result.WinContexts[0], hand.Rules);
            Assert.That(score.Has(FaanPattern.HeavenlyHand), Is.True);
            Assert.That(score.Total, Is.EqualTo(13));
        }

        [Test]
        public void SelfDrawnWin_IsLegalExactlyWhenWorthMinimumFaan()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                HandState hand = HandState.Deal(RuleSet.Party(), 0, Wind.East, seed);
                FaanResult score = FaanCalculator.Calculate(hand.PreviewWin(0), hand.Rules);
                bool worth = score != null && score.Total >= hand.Rules.MinFaan;
                Assert.That(hand.GetLegalActions(0).Any(a => a.Type == ActionType.SelfDrawnWin), Is.EqualTo(worth), $"seed {seed}");
            }
        }
    }
}
