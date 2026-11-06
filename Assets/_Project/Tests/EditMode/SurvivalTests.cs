using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class SurvivalTests
    {
        [Test]
        public void BarsStartFull_AndDrainOverTime()
        {
            var n = new SurvivalNeeds();
            Assert.That(n.Hunger, Is.EqualTo(100));
            n.Tick(60);
            Assert.That(n.Hunger, Is.EqualTo(100 - 100.0 / 420 * 60).Within(1e-9));
            Assert.That(n.Thirst, Is.EqualTo(80).Within(1e-9));
            Assert.That(n.Bladder, Is.EqualTo(100 - 100.0 / 360 * 60).Within(1e-9));
            Assert.That(n.IsDead, Is.False);
        }

        [Test]
        public void Exertion_DrainsFoodAndWaterFaster()
        {
            var rest = new SurvivalNeeds();
            var sprint = new SurvivalNeeds();
            rest.Tick(30);
            sprint.Tick(30, Exertion.Sprinting);
            Assert.That(sprint.Thirst, Is.LessThan(rest.Thirst));
            Assert.That(sprint.Hunger, Is.LessThan(rest.Hunger));
            Assert.That(sprint.Bladder, Is.EqualTo(rest.Bladder).Within(1e-9), "Bladder doesn't care about running");
        }

        [Test]
        public void Eating_DrinkingAndToilet_Refill()
        {
            var n = new SurvivalNeeds();
            n.Tick(200);
            double hunger = n.Hunger, thirst = n.Thirst, bladder = n.Bladder;

            n.Eat();
            Assert.That(n.Hunger, Is.EqualTo(System.Math.Min(100, hunger + 45)).Within(1e-9));
            Assert.That(n.Thirst, Is.EqualTo(thirst - 5).Within(1e-9));

            thirst = n.Thirst;
            n.Drink();
            Assert.That(n.Thirst, Is.EqualTo(System.Math.Min(100, thirst + 45)).Within(1e-9));
            Assert.That(n.Bladder, Is.EqualTo(bladder - 30).Within(1e-9), "Drinking fills your bladder");

            n.UseToilet();
            Assert.That(n.Bladder, Is.EqualTo(100));
        }

        [Test]
        public void AnyEmptyBar_Kills()
        {
            var n = new SurvivalNeeds();
            n.Tick(301); // thirst runs out first
            Assert.That(n.IsDead, Is.True);
            Assert.That(n.CauseOfDeath, Is.EqualTo(Need.Thirst));
            n.Drink();
            Assert.That(n.IsDead, Is.True, "No coming back");

            var bladder = new SurvivalNeeds();
            for (int i = 0; i < 4; i++) bladder.Drink();
            Assert.That(bladder.IsDead, Is.True);
            Assert.That(bladder.CauseOfDeath, Is.EqualTo(Need.Bladder));
        }

        [Test]
        public void EliminatedPlayer_CantWinTheMatch()
        {
            var rules = MatchRules.Default();
            rules.HandsPerMatch = 1;
            var match = new ScoreMatch(RuleSet.Party(), rules, 1);
            match.StartHand();
            match.Eliminate(0, "starved");
            Assert.That(match.Players[0].IsOut, Is.True);
            Assert.That(match.Events.OfType<PlayerOutEvent>().Single().Seat, Is.EqualTo(0));

            match.CatchCheater(cheater: 3, caller: 1); // seat 0 gains 10 but is out
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(match.Winners, Has.No.Member(0));
            Assert.That(match.Winners, Is.EquivalentTo(new[] { 1, 2 }));
        }
    }
}
