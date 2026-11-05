using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    /// <summary>Bots with ambition chase bigger hands, so hands last longer and pay more.</summary>
    public class AmbitionTests
    {
        private struct Stats
        {
            public int Hands, Wins, Draws, TotalFaan, TotalDiscards, Limits;
            public double AvgFaan => Wins == 0 ? 0 : (double)TotalFaan / Wins;
            public double AvgDiscards => (double)TotalDiscards / Hands;
            public double DrawRate => (double)Draws / Hands;
            public override string ToString() =>
                $"wins {Wins}/{Hands}, draws {DrawRate:P0}, avg faan {AvgFaan:0.0}, limits {Limits}, avg discards {AvgDiscards:0.0}";
        }

        private static Stats Play(double ambition, int hands, RuleSet rules)
        {
            var st = new Stats();
            for (ulong seed = 1; seed <= (ulong)hands; seed++)
            {
                HandState hand = HandState.Deal(rules, (int)(seed % 4), Wind.East, seed);
                var bots = Enumerable.Range(0, 4)
                    .Select(s => new SimpleBot(seed * 17 + (ulong)s, ambition: ambition)).ToArray();
                for (int step = 0; hand.Phase != HandPhase.Ended; step++)
                {
                    Assert.That(step, Is.LessThan(5000));
                    int seat = hand.SeatToAct();
                    hand.Apply(bots[seat].Choose(hand, seat, hand.GetLegalActions(seat)));
                }
                st.Hands++;
                st.TotalDiscards += hand.Events.OfType<TileDiscardedEvent>().Count();
                if (hand.Result.Outcome == HandOutcome.Win)
                {
                    st.Wins++;
                    FaanResult score = FaanCalculator.Calculate(hand.Result.WinContexts[0], rules);
                    st.TotalFaan += score.Total;
                    if (score.IsLimit) st.Limits++;
                }
                else
                {
                    st.Draws++;
                }
            }
            return st;
        }

        [Test]
        public void Ambition_MakesWinsBigger_WithoutStallingHands()
        {
            Stats calm = Play(0, 120, RuleSet.Party());
            Stats keen = Play(0.8, 120, RuleSet.Party());
            TestContext.WriteLine($"ambition 0.0: {calm}");
            TestContext.WriteLine($"ambition 0.8: {keen}");

            Assert.That(keen.AvgFaan, Is.GreaterThan(calm.AvgFaan + 0.5), "Wins should be bigger");
            Assert.That(keen.DrawRate, Is.LessThanOrEqualTo(calm.DrawRate), "Declining cheap wins mustn't leave more hands unwon");
        }

        [Test, Explicit("Prints a table for tuning; run manually.")]
        public void Ambition_Table()
        {
            foreach (double a in new[] { 0.0, 0.4, 0.6, 0.8, 1.0 })
                TestContext.WriteLine($"ambition {a:0.0}: {Play(a, 200, RuleSet.Party())}");
        }

        [Test]
        public void TargetFaan_FallsAsTheWallRunsDown()
        {
            HandState hand = HandState.Deal(RuleSet.Party(), 0, Wind.East, 3);
            var keen = new SimpleBot(1, ambition: 1);
            var calm = new SimpleBot(1, ambition: 0);
            Assert.That(calm.TargetFaan(hand), Is.EqualTo(3));
            Assert.That(keen.TargetFaan(hand), Is.EqualTo(9), "Full wall: 3 + 6");

            var rules = RuleSet.Party();
            rules.DeadWallSize = 144 - 53 - 8; // a handful of tiles left (room for flower replacements)
            HandState empty = HandState.Deal(rules, 0, Wind.East, 3);
            Assert.That(keen.TargetFaan(empty), Is.EqualTo(3), "Nearly empty wall: back to the minimum");
        }

        [Test]
        public void AmbitiousBot_DeclinesACheapEarlyWin_ButTakesItLate()
        {
            // Seat 1 is ready for a 3-faan hand (all chows, concealed, no flowers) waiting on 5D.
            const string dealer = "5D 4D 7D 1B 4B 7B 1C 4C 7C E S W N RD";
            const string seat1 = "1B 2B 3B 4B 5B 6B 7C 8C 9C 2C 3C 4C 5D";
            const string seat2 = "3D 6D 9D 3B 6B 9B 3C 6C 9C E S W WD";
            const string seat3 = "1D 4D 7D 1B 4B 7B 1C 4C 7C N N RD GD";

            HandState Setup(RuleSet rules)
            {
                HandState hand = TestWall.Deal(new[] { dealer, seat1, seat2, seat3 },
                    frontDraws: "9D WD WD WD", rules: rules, validator: new FaanWinValidator(rules));
                // Go once around the table so the win isn't an earthly hand (a limit).
                hand.Apply(PlayerAction.Discard(0, Tile.WindTile(Wind.North)));
                if (hand.Phase == HandPhase.AwaitingClaims)
                    hand.Apply(PlayerAction.Pass(3));
                hand.Apply(PlayerAction.Discard(1, Tile.Dots(9)));
                hand.Apply(PlayerAction.Discard(2, Tile.DragonTile(Dragon.White)));
                hand.Apply(PlayerAction.Discard(3, Tile.DragonTile(Dragon.White)));
                hand.Apply(PlayerAction.Discard(0, Tile.Dots(5)));
                return hand;
            }

            RuleSet fullWall = TestRules.RandomDraws();
            fullWall.MinFaan = 3;
            HandState early = Setup(fullWall);
            List<PlayerAction> legal = early.GetLegalActions(1);
            Assert.That(legal, Has.Member(PlayerAction.Win(1)));
            Assert.That(FaanCalculator.Calculate(early.PreviewWin(1), early.Rules).Total, Is.EqualTo(3));

            Assert.That(new SimpleBot(5, ambition: 0).Choose(early, 1, legal), Is.EqualTo(PlayerAction.Win(1)));
            Assert.That(new SimpleBot(5, ambition: 1).Choose(early, 1, legal), Is.Not.EqualTo(PlayerAction.Win(1)),
                "3 faan is below the early target of 9");

            RuleSet nearlyEmpty = TestRules.RandomDraws();
            nearlyEmpty.MinFaan = 3;
            nearlyEmpty.DeadWallSize = 144 - 53 - 6; // only a few draws left
            HandState late = Setup(nearlyEmpty);
            Assert.That(new SimpleBot(5, ambition: 1).Choose(late, 1, late.GetLegalActions(1)), Is.EqualTo(PlayerAction.Win(1)));
        }
    }
}
