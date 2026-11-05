using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    /// <summary>The party "choose your draw" rule (RuleSet.ChooseDraws).</summary>
    public class ChosenDrawTests
    {
        private const string Seat0 = "1D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD"; // dealer
        private const string Seat1 = "2D 5D 8D 2B 5B 8B 2C 5C 8C E S W GD";
        private const string Seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
        private const string Seat3AllEast = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD E"; // 4th East: none left in the wall

        private static RuleSet Rules()
        {
            RuleSet r = RuleSet.Party();
            r.MinFaan = 0;
            r.ChooseDraws = true;
            return r;
        }

        private static HandState Deal(string s3 = Seat3AllEast, RuleSet rules = null, IWinValidator validator = null) =>
            TestWall.Deal(new[] { Seat0, Seat1, Seat2, s3 }, rules: rules ?? Rules(), validator: validator);

        private static void AssertConsistent(HandState hand)
        {
            List<string> problems = HandInvariants.Check(hand);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void AfterDiscard_NextSeatChoosesADraw()
        {
            HandState hand = Deal();
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingDraw));
            Assert.That(hand.CurrentSeat, Is.EqualTo(1));
            List<PlayerAction> legal = hand.GetLegalActions(1);
            Assert.That(legal, Has.Count.EqualTo(34));
            Assert.That(legal.All(a => a.Type == ActionType.Draw), Is.True);
            Assert.That(legal.Any(a => a.Tile.IsBonus), Is.False, "Flowers/seasons can't be picked");
            Assert.That(hand.GetLegalActions(2), Is.Empty);
            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(13));
            AssertConsistent(hand);
        }

        [Test]
        public void SuccessfulDraw_TakesThatTile()
        {
            HandState hand = Deal();
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));
            int before = hand.Wall.Remaining;
            hand.Apply(PlayerAction.Draw(1, Tile.Bamboo(9)));

            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingAction));
            Assert.That(hand.CurrentSeat, Is.EqualTo(1));
            Assert.That(hand.LastDrawnTile, Is.EqualTo(Tile.Bamboo(9)));
            Assert.That(hand.Seats[1].Concealed, Has.Member(Tile.Bamboo(9)));
            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(14));
            Assert.That(hand.Wall.Remaining, Is.EqualTo(before - 1));
            Assert.That(hand.Events.Last(), Is.TypeOf<TileDrawnEvent>());
            AssertConsistent(hand);
        }

        [Test]
        public void FailedDraw_LosesTheTurn()
        {
            HandState hand = Deal();
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));
            int before = hand.Wall.Remaining;
            hand.Apply(PlayerAction.Draw(1, Tile.WindTile(Wind.East))); // all four Easts are in hands

            var failed = hand.Events.OfType<DrawFailedEvent>().Single();
            Assert.That(failed.Seat, Is.EqualTo(1));
            Assert.That(failed.Tile, Is.EqualTo(Tile.WindTile(Wind.East)));
            Assert.That(hand.Seats[1].Concealed, Has.Count.EqualTo(13), "No tile gained");
            Assert.That(hand.Seats[1].Discards, Is.Empty, "No discard either");
            Assert.That(hand.Wall.Remaining, Is.EqualTo(before));
            Assert.That(hand.CurrentSeat, Is.EqualTo(2), "Turn passes on");
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingDraw));
            AssertConsistent(hand);
        }

        [Test]
        public void FailedDraw_WhenAllCopiesAreDiscarded()
        {
            // Seat 0 discards its 1D; with 1D in seat 3's hand too, two more are in the wall.
            // Draw both remaining 1Ds, then the next pick of 1D must fail.
            HandState hand = Deal();
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));
            hand.Apply(PlayerAction.Draw(1, Tile.Dots(1)));
            hand.Apply(PlayerAction.Discard(1, Tile.Dots(1)));
            hand.Apply(PlayerAction.Draw(2, Tile.Dots(1)));
            hand.Apply(PlayerAction.Discard(2, Tile.Dots(1)));
            Assert.That(hand.CurrentSeat, Is.EqualTo(3));
            hand.Apply(PlayerAction.Draw(3, Tile.Dots(1)));

            Assert.That(hand.Events.OfType<DrawFailedEvent>().Single().Seat, Is.EqualTo(3));
            Assert.That(hand.CurrentSeat, Is.EqualTo(0));
            AssertConsistent(hand);
        }

        [Test]
        public void DeadWallTiles_CannotBeChosen()
        {
            RuleSet rules = Rules();
            rules.DeadWallSize = 144 - 53 - 1; // exactly one drawable tile after the deal
            HandState hand = Deal(rules: rules);
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));
            Assert.That(hand.Wall.DrawablePlayableCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ConcealedKong_ReplacementIsAlsoChosen()
        {
            HandState hand = TestWall.Deal(new[]
            {
                "RD RD RD RD 4D 7D 1B 4B 7B 1C 4C 7C E S", Seat1, Seat2,
                "1D 4D 7D 1B 4B 7B 1C 4C 7C N N GD WD",
            }, rules: Rules());
            hand.Apply(PlayerAction.ConcealedKong(0, Tile.DragonTile(Dragon.Red)));
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingDraw));
            Assert.That(hand.CurrentSeat, Is.EqualTo(0));

            hand.Apply(PlayerAction.Draw(0, Tile.Characters(9)));
            Assert.That(((TileDrawnEvent)hand.Events.Last()).FromBack, Is.True, "Marked as a replacement draw");
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingAction));
            AssertConsistent(hand);
        }

        [Test]
        public void PreviewDraw_ShowsWinningPicks()
        {
            // Seat 1 waits on 1C/4C (all chows + 5D pair).
            HandState hand = TestWall.Deal(new[]
            {
                "1D 4D 7D 1B 4B 7B 9D 4C 7C E S W N RD",
                "1D 2D 3D 4B 5B 6B 7C 8C 9C 2C 3C 5D 5D",
                Seat2, "1D 4D 7D 1B 4B 7B 1C 9B 7C N N RD GD",
            }, rules: Rules(), validator: new FaanWinValidator(Rules()));
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(9)));
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingDraw));

            Assert.That(FaanCalculator.Calculate(hand.PreviewDraw(1, Tile.Characters(1)), hand.Rules), Is.Not.Null);
            Assert.That(FaanCalculator.Calculate(hand.PreviewDraw(1, Tile.Characters(4)), hand.Rules).Has(FaanPattern.SelfDrawn), Is.True);
            Assert.That(FaanCalculator.Calculate(hand.PreviewDraw(1, Tile.Characters(6)), hand.Rules), Is.Null);
            Assert.That(hand.PreviewDraw(2, Tile.Characters(1)), Is.Null, "Not seat 2's draw");

            hand.Apply(PlayerAction.Draw(1, Tile.Characters(4)));
            Assert.That(hand.GetLegalActions(1), Has.Member(PlayerAction.SelfDrawnWin(1)));
        }

        [Test]
        public void VisibleKindCounts_UseOnlyPublicInformation()
        {
            HandState hand = Deal();
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));
            int[] seenBy1 = hand.VisibleKindCounts(1);
            Assert.That(seenBy1[Tile.Dots(1).KindIndex], Is.EqualTo(1), "Only the discarded 1D, not seat 3's");
            Assert.That(seenBy1[Tile.WindTile(Wind.East).KindIndex], Is.EqualTo(1), "Only seat 1's own East");
            Assert.That(seenBy1[Tile.Dots(2).KindIndex], Is.EqualTo(1));
        }

        [Test]
        public void Bot_NeverRepeatsAFailedPick()
        {
            HandState hand = Deal();
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(1)));
            hand.Apply(PlayerAction.Draw(1, Tile.WindTile(Wind.East))); // seat 1 fails on East
            // Play round to seat 1's next draw.
            hand.Apply(PlayerAction.Draw(2, Tile.Bamboo(9)));
            hand.Apply(PlayerAction.Discard(2, Tile.Bamboo(9)));
            hand.Apply(PlayerAction.Draw(3, Tile.Characters(9)));
            hand.Apply(PlayerAction.Discard(3, Tile.Characters(9)));
            hand.Apply(PlayerAction.Draw(0, Tile.Dots(9)));
            hand.Apply(PlayerAction.Discard(0, Tile.Dots(9)));
            Assert.That(hand.CurrentSeat, Is.EqualTo(1));
            Assert.That(hand.Phase, Is.EqualTo(HandPhase.AwaitingDraw));

            for (ulong seed = 0; seed < 20; seed++)
            {
                PlayerAction pick = new SimpleBot(seed).Choose(hand, 1, hand.GetLegalActions(1));
                Assert.That(pick.Tile, Is.Not.EqualTo(Tile.WindTile(Wind.East)));
            }
        }

        [Test]
        public void ChanceInWall_Basics()
        {
            Assert.That(SimpleBot.ChanceInWall(0, 39, 80), Is.EqualTo(0));
            Assert.That(SimpleBot.ChanceInWall(2, 39, 0), Is.EqualTo(0));
            Assert.That(SimpleBot.ChanceInWall(1, 0, 80), Is.EqualTo(1));
            Assert.That(SimpleBot.ChanceInWall(1, 39, 80), Is.EqualTo(80.0 / 119).Within(1e-9));
            Assert.That(SimpleBot.ChanceInWall(3, 39, 80), Is.GreaterThan(SimpleBot.ChanceInWall(1, 39, 80)));
        }

        [Test]
        public void RandomChosenDrawHands_StayConsistent()
        {
            int failures = 0;
            for (ulong seed = 1; seed <= 150; seed++)
            {
                var rng = new SeededRandom(seed);
                RuleSet rules = RuleSet.Party();
                rules.ChooseDraws = true;
                HandState hand = HandState.Deal(rules, (int)(seed % 4), Wind.East, seed);
                for (int step = 0; hand.Phase != HandPhase.Ended; step++)
                {
                    Assert.That(step, Is.LessThan(20000), $"seed {seed}");
                    int seat = hand.SeatToAct();
                    List<PlayerAction> legal = hand.GetLegalActions(seat);
                    hand.Apply(legal[rng.NextInt(legal.Count)]);
                    List<string> problems = HandInvariants.Check(hand);
                    Assert.That(problems, Is.Empty, $"seed {seed}: " + string.Join("\n", problems));
                }
                failures += hand.Events.OfType<DrawFailedEvent>().Count();
            }
            TestContext.WriteLine($"Failed random draws: {failures}");
            Assert.That(failures, Is.GreaterThan(0));
        }
    }
}
