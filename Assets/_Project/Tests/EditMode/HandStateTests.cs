using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class HandStateTests
    {
        // Neutral hands: no pairs worth claiming, no chow chances for the next seat.
        private const string Seat0 = "1D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD"; // dealer, 14 tiles
        private const string Seat1 = "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD";
        private const string Seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
        private const string Seat3 = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD";
        /// <summary>Like <see cref="Seat1"/> but without 5B, and no chow on a 5B discard.</summary>
        private const string Seat1NoFiveB = "2D 5D 8D 2B 9B 8B 2C 5C 8C E S W GD";

        private static string[] Hands(string s0 = Seat0, string s1 = Seat1, string s2 = Seat2, string s3 = Seat3) =>
            new[] { s0, s1, s2, s3 };

        private static void AssertConsistent(HandState hand)
        {
            List<string> problems = HandInvariants.Check(hand);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static List<Tile> Sorted(IEnumerable<Tile> tiles) => tiles.OrderBy(t => t).ToList();

        // ------------------------------------------------------------------ dealing

        [Test]
        public void Deal_GivesThirteenEachAndDealerFourteen()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                HandState hand = HandState.Deal(RuleSet.Party(), dealer: 2, Wind.East, seed);
                for (int s = 0; s < 4; s++)
                    Assert.That(hand.Seats[s].Concealed, Has.Count.EqualTo(s == 2 ? 14 : 13), $"seed {seed} seat {s}");
                Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingAction));
                Assert.That(hand.CurrentSeat, Is.EqualTo(2));
                AssertConsistent(hand);
            }
        }

        [Test]
        public void Deal_AssignsSeatWindsFromDealer()
        {
            HandState hand = HandState.Deal(RuleSet.Party(), dealer: 1, Wind.South, 5);
            Assert.That(hand.Seats.Select(s => s.SeatWind), Is.EqualTo(new[] { Wind.North, Wind.East, Wind.South, Wind.West }));
            Assert.That(hand.PrevailingWind, Is.EqualTo(Wind.South));
        }

        [Test]
        public void Deal_IsDeterministicPerSeed()
        {
            HandState a = HandState.Deal(RuleSet.Party(), 0, Wind.East, 99);
            HandState b = HandState.Deal(RuleSet.Party(), 0, Wind.East, 99);
            HandState c = HandState.Deal(RuleSet.Party(), 0, Wind.East, 100);
            Assert.That(a.Seats[0].Concealed, Is.EqualTo(b.Seats[0].Concealed));
            Assert.That(a.Seats[3].Concealed, Is.EqualTo(b.Seats[3].Concealed));
            Assert.That(a.Seats[0].Concealed, Is.Not.EqualTo(c.Seats[0].Concealed));
        }

        [Test]
        public void DealFromWall_DealsExactHands()
        {
            HandState hand = TestWall.Deal(Hands());
            Assert.That(Sorted(hand.Seats[0].Concealed), Is.EqualTo(Sorted(Tile.ParseList(Seat0))));
            Assert.That(Sorted(hand.Seats[1].Concealed), Is.EqualTo(Sorted(Tile.ParseList(Seat1))));
            Assert.That(Sorted(hand.Seats[2].Concealed), Is.EqualTo(Sorted(Tile.ParseList(Seat2))));
            Assert.That(Sorted(hand.Seats[3].Concealed), Is.EqualTo(Sorted(Tile.ParseList(Seat3))));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.DragonTile(Dragon.Red)), "Dealer's 14th tile");
        }

        [Test]
        public void DealFromWall_RejectsIncompleteSet()
        {
            List<Tile> wall = TileSet.CreateFull();
            wall[0] = Tile.Dots(2); // now five 2D and three 1D
            Assert.That(() => HandState.DealFromWall(RuleSet.Party(), 0, Wind.East, wall),
                Throws.TypeOf<System.ArgumentException>());
        }

        // ------------------------------------------------------------------ bonus tiles

        [Test]
        public void StartingFlower_IsSetAsideAndReplacedFromBack()
        {
            HandState hand = TestWall.Deal(Hands(s0: "1D 4D 7D 1B 4B 7B 1C 4C 7C E S W N F1"), backDraws: "WD");
            SeatState dealer = hand.Seats[0];
            Assert.That(dealer.Bonus, Is.EqualTo(new[] { Tile.Flower(1) }));
            Assert.That(dealer.Concealed, Has.Count.EqualTo(14));
            Assert.That(dealer.Concealed, Has.Member(Tile.DragonTile(Dragon.White)));
            Assert.That(dealer.Concealed.Any(t => t.IsBonus), Is.False);

            Assert.That(hand.Events[0], Is.TypeOf<BonusTileRevealedEvent>());
            var drawn = (TileDrawnEvent)hand.Events[1];
            Assert.That(drawn.FromBack, Is.True);
            Assert.That(drawn.Tile, Is.EqualTo(Tile.DragonTile(Dragon.White)));
            AssertConsistent(hand);
        }

        [Test]
        public void DrawnFlower_IsSetAsideAndReplacedFromBack()
        {
            HandState hand = TestWall.Deal(Hands(), frontDraws: "F2", backDraws: "9C");
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));

            SeatState seat1 = hand.Seats[1];
            Assert.That(hand.CurrentSeat, Is.EqualTo(1));
            Assert.That(seat1.Bonus, Is.EqualTo(new[] { Tile.Flower(2) }));
            Assert.That(seat1.Concealed, Has.Count.EqualTo(14));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Characters(9)));
            AssertConsistent(hand);
        }

        // ------------------------------------------------------------------ turn flow

        [Test]
        public void Discard_WithNoClaims_PassesTurnToNextSeat()
        {
            HandState hand = TestWall.Deal(Hands(), frontDraws: "9C");
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingAction));
            Assert.That(hand.CurrentSeat, Is.EqualTo(1));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Characters(9)));
            Assert.That(hand.Seats[0].Discards, Is.EqualTo(new[] { Tile.Dots(1) }));
            Assert.That(hand.Seats[0].Concealed, Has.Count.EqualTo(13));
            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(14));
            AssertConsistent(hand);
        }

        [Test]
        public void TurnActions_OfferEachDistinctTileOnce()
        {
            HandState hand = TestWall.Deal(Hands(s0: "1D 1D 7D 1B 4B 7B 1C 4C 7C E S W N RD", s3: "4D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD"));
            List<PlayerAction> actions = hand.GetLegalActions(0);
            Assert.That(actions.All(a => a.Type == ActionType.Discard), Is.True);
            Assert.That(actions, Has.Count.EqualTo(13));
            Assert.That(hand.GetLegalActions(1), Is.Empty, "Not seat 1's turn");
        }

        // ------------------------------------------------------------------ claims

        private const string ChowDealer = "5B 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD";
        private const string ChowSeat1 = "3B 4B 6B 7B 5D 8D 2C 5C 8C E S W GD";

        [Test]
        public void Chow_IsOfferedToNextSeatOnly_WithEverySequence()
        {
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: ChowSeat1,
                s2: "3B 4B 6B 7B 6D 9D 3C 6C 9C E S W WD"));
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingClaims));
            Assert.That(hand.SeatsAwaitingResponse(), Is.EqualTo(new[] { 1 }), "Seats 2 and 3 auto-pass");
            Assert.That(hand.GetLegalActions(1), Is.EquivalentTo(new[]
            {
                PlayerAction.Pass(1),
                PlayerAction.Chow(1, Tile.Bamboo(3)),
                PlayerAction.Chow(1, Tile.Bamboo(4)),
                PlayerAction.Chow(1, Tile.Bamboo(5)),
            }));
            Assert.That(hand.GetLegalActions(2), Is.Empty, "Seat 2 holds 4B 6B but isn't next");
        }

        [Test]
        public void Chow_MeldsTilesAndMustDiscard()
        {
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: ChowSeat1));
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            hand.Apply(PlayerAction.Chow(1, Tile.Bamboo(4)));

            SeatState seat1 = hand.Seats[1];
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingAction));
            Assert.That(hand.CurrentSeat, Is.EqualTo(1));
            Assert.That(hand.LastDrawnTile, Is.Null, "No draw after a claim");
            Assert.That(seat1.Melds, Has.Count.EqualTo(1));
            Assert.That(seat1.Melds[0].Type, Is.EqualTo(MeldType.Chow));
            Assert.That(seat1.Melds[0].Tiles, Is.EqualTo(Tile.ParseList("4B 5B 6B")));
            Assert.That(seat1.Melds[0].ClaimedFromSeat, Is.EqualTo(0));
            Assert.That(seat1.Concealed, Has.Count.EqualTo(11));
            Assert.That(hand.Seats[0].Discards, Is.Empty, "Claimed discard leaves the pile");
            Assert.That(hand.GetLegalActions(1).All(a => a.Type == ActionType.Discard), Is.True);
            Assert.That(hand.Events.Last(), Is.TypeOf<MeldDeclaredEvent>());
            AssertConsistent(hand);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Pung_BeatsChow_RegardlessOfResponseOrder(bool chowFirst)
        {
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: ChowSeat1,
                s2: "5B 5B 6B 7B 6D 9D 3C 6C 9C E S W WD"));
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            Assert.That(hand.SeatsAwaitingResponse(), Is.EqualTo(new[] { 1, 2 }));

            if (chowFirst)
            {
                hand.Apply(PlayerAction.Chow(1, Tile.Bamboo(3)));
                Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingClaims), "Waits for seat 2");
                hand.Apply(PlayerAction.Pung(2));
            }
            else
            {
                hand.Apply(PlayerAction.Pung(2));
                hand.Apply(PlayerAction.Chow(1, Tile.Bamboo(3)));
            }

            Assert.That(hand.CurrentSeat, Is.EqualTo(2));
            Assert.That(hand.Seats[2].Melds.Single().Type, Is.EqualTo(MeldType.Pung));
            Assert.That(hand.Seats[1].Melds, Is.Empty);
            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(13));
            Assert.That(hand.GetLegalActions(2).All(a => a.Type == ActionType.Discard), Is.True);
            AssertConsistent(hand);
        }

        [Test]
        public void Win_BeatsPungAndChow()
        {
            var validator = PredicateWinValidator.Holding("5B 9C 9C");
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: ChowSeat1,
                    s2: "5B 5B 6B 7B 6D 9D 3C 6C 9C E S W WD",
                    s3: "1D 4D 7D 1B 4B 7B 1C 4C 7C 9C 9C RD GD"),
                validator: validator);
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));

            Assert.That(hand.GetLegalActions(3), Has.Member(PlayerAction.Win(3)));
            Assert.That(hand.GetLegalActions(2), Has.No.Member(PlayerAction.Win(2)));
            hand.Apply(PlayerAction.Chow(1, Tile.Bamboo(3)));
            hand.Apply(PlayerAction.Pung(2));
            hand.Apply(PlayerAction.Win(3));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.Ended));
            HandResult r = hand.Result;
            Assert.That(r.Outcome, Is.EqualTo(HandOutcome.Win));
            Assert.That(r.Winners, Is.EqualTo(new[] { 3 }));
            Assert.That(r.Discarder, Is.EqualTo(0));
            Assert.That(r.WinningTile, Is.EqualTo(Tile.Bamboo(5)));
            Assert.That(r.SelfDrawn, Is.False);
            Assert.That(hand.Seats[3].Concealed, Has.Count.EqualTo(14));
            Assert.That(hand.Events.Last(), Is.TypeOf<HandEndedEvent>());
            AssertConsistent(hand);
        }

        [TestCase(false, new[] { 1 })]
        [TestCase(true, new[] { 1, 3 })]
        public void SimultaneousWins_FollowMultipleWinnerRule(bool allowMultiple, int[] expectedWinners)
        {
            var rules = RuleSet.Party();
            rules.AllowMultipleWinners = allowMultiple;
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer,
                    s1: "3B 4B 6B 7B 5D 8D 2C 9C 9C E S W GD",
                    s2: "5B 5B 6B 7B 6D 9D 3C 6C 8C E S W WD",
                    s3: "1D 4D 7D 1B 4B 7B 1C 4C 7C 9C 9C RD GD"),
                validator: PredicateWinValidator.Holding("5B 9C 9C"), rules: rules);
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            hand.Apply(PlayerAction.Win(3));
            hand.Apply(PlayerAction.Pass(2));
            hand.Apply(PlayerAction.Win(1));

            Assert.That(hand.Result.Winners, Is.EqualTo(expectedWinners));
            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(14), "Nearest winner takes the tile");
            Assert.That(hand.Seats[3].Concealed, Has.Count.EqualTo(13));
            AssertConsistent(hand);
        }

        // ------------------------------------------------------------------ kongs

        [Test]
        public void ExposedKong_DrawsReplacementFromBack()
        {
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: Seat1NoFiveB, s2: "5B 5B 5B 7B 6D 9D 3C 6C 9C E S W WD"),
                backDraws: "9C");
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            Assert.That(hand.GetLegalActions(2), Is.EquivalentTo(new[]
                { PlayerAction.Pass(2), PlayerAction.Pung(2), PlayerAction.Kong(2) }));
            hand.Apply(PlayerAction.Kong(2));

            Meld kong = hand.Seats[2].Melds.Single();
            Assert.That(kong.Type, Is.EqualTo(MeldType.Kong));
            Assert.That(kong.IsConcealed, Is.False);
            Assert.That(kong.ClaimedFromSeat, Is.EqualTo(0));
            Assert.That(hand.CurrentSeat, Is.EqualTo(2));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Characters(9)));
            Assert.That(((TileDrawnEvent)hand.Events.Last()).FromBack, Is.True);
            AssertConsistent(hand);
        }

        [Test]
        public void ConcealedKong_DrawsReplacementAndKeepsTurn()
        {
            HandState hand = TestWall.Deal(Hands(s0: "RD RD RD RD 4D 7D 1B 4B 7B 1C 4C 7C E S",
                    s3: "1D 4D 7D 1B 4B 7B 1C 4C 7C N N GD WD"),
                backDraws: "9C");
            Tile red = Tile.DragonTile(Dragon.Red);
            Assert.That(hand.GetLegalActions(0), Has.Member(PlayerAction.ConcealedKong(0, red)));
            hand.Apply(PlayerAction.ConcealedKong(0, red));

            Meld kong = hand.Seats[0].Melds.Single();
            Assert.That(kong.IsConcealed, Is.True);
            Assert.That(kong.Tiles, Is.EqualTo(new[] { red, red, red, red }));
            Assert.That(hand.CurrentSeat, Is.EqualTo(0));
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingAction));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Characters(9)));
            AssertConsistent(hand);
        }

        [Test]
        public void SelfDrawnWin_OnKongReplacement_IsFlagged()
        {
            HandState hand = TestWall.Deal(Hands(s0: "RD RD RD RD 9C 9C 7D 1B 4B 7B 1C 4C E S",
                    s2: "3D 6D 9D 3B 6B 9B 3C 6C 8C E S W WD",
                    s3: "1D 4D 7D 1B 4B 7B 1C 4C 7C N N GD WD"),
                backDraws: "9C", validator: PredicateWinValidator.Holding("9C 9C 9C"));
            Assert.That(hand.GetLegalActions(0), Has.No.Member(PlayerAction.SelfDrawnWin(0)));
            hand.Apply(PlayerAction.ConcealedKong(0, Tile.DragonTile(Dragon.Red)));
            hand.Apply(PlayerAction.SelfDrawnWin(0));

            HandResult r = hand.Result;
            Assert.That(r.SelfDrawn, Is.True);
            Assert.That(r.Discarder, Is.EqualTo(-1));
            Assert.That(r.KongReplacementWin, Is.True);
            Assert.That(r.WinningTile, Is.EqualTo(Tile.Characters(9)));
            AssertConsistent(hand);
        }

        /// <summary>
        /// Dealer discards 5B, seat 2 pungs it and discards WD; everyone then draws and
        /// discards WD until seat 2 draws the fourth 5B.
        /// </summary>
        private static HandState PromoteKongSetup(IWinValidator validator)
        {
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: Seat1NoFiveB,
                    s2: "5B 5B 6B 7B 6D 9D 3C 6C 8C E S W WD",
                    s3: "1D 4D 7D 1B 4B 7B 1C 4C 7C 9C 9C RD GD"),
                frontDraws: "WD WD WD 5B", backDraws: "9B", validator: validator);
            Tile wd = Tile.DragonTile(Dragon.White);
            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            hand.Apply(PlayerAction.Pung(2));
            hand.Apply(PlayerAction.Discard(2, wd));
            hand.Apply(PlayerAction.Discard(3, wd));
            hand.Apply(PlayerAction.Discard(0, wd));
            hand.Apply(PlayerAction.Discard(1, wd));
            Assert.That(hand.CurrentSeat, Is.EqualTo(2));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Bamboo(5)));
            return hand;
        }

        [Test]
        public void PromoteKong_WithoutRobber_BecomesKong()
        {
            HandState hand = PromoteKongSetup(null);
            Assert.That(hand.GetLegalActions(2), Has.Member(PlayerAction.PromoteKong(2, Tile.Bamboo(5))));
            hand.Apply(PlayerAction.PromoteKong(2, Tile.Bamboo(5)));

            Meld kong = hand.Seats[2].Melds.Single();
            Assert.That(kong.Type, Is.EqualTo(MeldType.Kong));
            Assert.That(kong.IsPromotedKong, Is.True);
            Assert.That(hand.CurrentSeat, Is.EqualTo(2));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Bamboo(9)));
            AssertConsistent(hand);
        }

        [Test]
        public void PromoteKong_CanBeRobbed()
        {
            bool winsEnabled = false;
            var validator = new PredicateWinValidator(h =>
                winsEnabled && h.Count(t => t == Tile.Characters(9)) >= 2 && h.Contains(Tile.Bamboo(5)));
            HandState hand = PromoteKongSetup(validator);
            winsEnabled = true;
            hand.Apply(PlayerAction.PromoteKong(2, Tile.Bamboo(5)));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingRobKong));
            Assert.That(hand.GetLegalActions(2), Is.Empty);
            Assert.That(hand.GetLegalActions(3), Is.EquivalentTo(new[] { PlayerAction.Pass(3), PlayerAction.Win(3) }));
            AssertConsistent(hand);

            hand.Apply(PlayerAction.Win(3));
            HandResult r = hand.Result;
            Assert.That(r.RobbedKong, Is.True);
            Assert.That(r.Winners, Is.EqualTo(new[] { 3 }));
            Assert.That(r.Discarder, Is.EqualTo(2));
            Assert.That(r.WinningTile, Is.EqualTo(Tile.Bamboo(5)));
            Assert.That(hand.Seats[2].Melds.Single().Type, Is.EqualTo(MeldType.Pung), "Kong never completed");
            AssertConsistent(hand);
        }

        // ------------------------------------------------------------------ ending

        [Test]
        public void ExhaustedWall_EndsInDrawGame()
        {
            var rules = RuleSet.Party();
            rules.DeadWallSize = 144 - 53; // nothing drawable after the deal
            HandState hand = TestWall.Deal(Hands(), rules: rules);
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.Ended));
            Assert.That(hand.Result.Outcome, Is.EqualTo(HandOutcome.ExhaustiveDraw));
            Assert.That(hand.Result.Winners, Is.Empty);
            AssertConsistent(hand);
        }

        [Test]
        public void HeavenlyHand_DealerCanWinImmediately()
        {
            HandState hand = TestWall.Deal(Hands(), validator: PredicateWinValidator.Holding("RD 1D"));
            hand.Apply(PlayerAction.SelfDrawnWin(0));
            Assert.That(hand.Result.Winners, Is.EqualTo(new[] { 0 }));
            Assert.That(hand.Result.SelfDrawn, Is.True);
            Assert.That(hand.Result.WinningTile, Is.EqualTo(Tile.DragonTile(Dragon.Red)));
        }

        // ------------------------------------------------------------------ illegal actions

        [Test]
        public void IllegalActions_AreRejectedWithoutChangingState()
        {
            HandState hand = TestWall.Deal(Hands(s0: ChowDealer, s1: ChowSeat1));
            int eventCount = hand.Events.Count;

            Assert.That(hand.TryApply(PlayerAction.Discard(1, Tile.Dots(2)), out string error), Is.False, "Out of turn");
            Assert.That(error, Is.Not.Empty);
            Assert.That(hand.TryApply(PlayerAction.Discard(0, Tile.Bamboo(9)), out _), Is.False, "Tile not held");
            Assert.That(hand.TryApply(PlayerAction.Pass(1), out _), Is.False, "No claim window open");
            Assert.That(hand.Events, Has.Count.EqualTo(eventCount));

            hand.Apply(PlayerAction.Discard(0, Tile.Bamboo(5)));
            Assert.That(hand.TryApply(PlayerAction.Chow(1, Tile.Bamboo(1)), out _), Is.False, "Not a valid sequence");
            Assert.That(hand.TryApply(PlayerAction.Pung(1), out _), Is.False, "Seat 1 has no 5B pair");
            Assert.That(() => hand.Apply(PlayerAction.Discard(0, Tile.Dots(4))), Throws.InvalidOperationException);

            hand.Apply(PlayerAction.Pass(1));
            Assert.That(hand.TryApply(PlayerAction.Pass(1), out _), Is.False, "Already responded");
        }

        [Test]
        public void NoActionsAfterHandEnds()
        {
            HandState hand = TestWall.Deal(Hands(), validator: PredicateWinValidator.Holding("RD"));
            hand.Apply(PlayerAction.SelfDrawnWin(0));
            for (int s = 0; s < 4; s++)
                Assert.That(hand.GetLegalActions(s), Is.Empty);
            Assert.That(hand.TryApply(PlayerAction.Discard(0, Tile.Dots(1)), out string error), Is.False);
            Assert.That(error, Does.Contain("over"));
        }
    }
}
