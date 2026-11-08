using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class KitchenTests
    {
        [Test]
        public void ChickenSoup_FullRecipe_TakesAboutNinetySeconds()
        {
            var k = new Kitchen();
            Assert.That(k.NextStep(), Is.EqualTo("Turn on the stove"));
            k.ToggleStove();
            Assert.That(k.TryFillPot(out _), Is.True);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Heating));

            double elapsed = 0;
            // Slice while the water heats (4 x 5 s fits inside the 30 s boil).
            foreach (Ingredient i in Kitchen.Recipe)
            {
                k.Tick(5); elapsed += 5;
                Assert.That(k.TrySlice(i, out _), Is.True);
            }
            Assert.That(k.TryAddIngredients(out string early), Is.False);
            Assert.That(early, Does.Contain("boil"));

            k.Tick(10); elapsed += 10;
            Assert.That(k.Stage, Is.EqualTo(PotStage.Boiling));
            Assert.That(k.TryAddIngredients(out _), Is.True);
            k.Tick(60); elapsed += 60;
            Assert.That(k.Stage, Is.EqualTo(PotStage.Done));
            Assert.That(elapsed, Is.EqualTo(90));

            Assert.That(k.TryLadle(out _), Is.True);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Empty));
            Assert.That(k.IsSliced(Ingredient.Chicken), Is.False, "Ready for the next pot");
        }

        [Test]
        public void NothingCooks_WithTheStoveOff()
        {
            var k = new Kitchen();
            k.TryFillPot(out _);
            k.Tick(100);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Heating));
            Assert.That(k.Progress, Is.EqualTo(0));
        }

        [Test]
        public void MissingIngredients_CantGoIn()
        {
            var k = new Kitchen();
            k.ToggleStove();
            k.TryFillPot(out _);
            k.Tick(30);
            k.TrySlice(Ingredient.Chicken, out _);
            Assert.That(k.TryAddIngredients(out string error), Is.False);
            Assert.That(error, Does.Contain("Slice"));
            Assert.That(k.TrySlice(Ingredient.Chicken, out _), Is.False, "Already sliced");
        }

        [Test]
        public void FinishedSoup_BurnsIfLeftOnTheStove()
        {
            var k = new Kitchen();
            k.ToggleStove();
            k.TryFillPot(out _);
            foreach (Ingredient i in Kitchen.Recipe) k.TrySlice(i, out _);
            k.Tick(30);
            k.TryAddIngredients(out _);
            k.Tick(60);
            k.Tick(59);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Done));
            k.Tick(2);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Burnt));
            Assert.That(k.TryLadle(out string error), Is.False);
            Assert.That(error, Does.Contain("burnt"));
            Assert.That(k.TryDump(out _), Is.True);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Empty));
        }

        [Test]
        public void TurningTheStoveOff_KeepsFinishedSoupFromBurning()
        {
            var k = new Kitchen();
            k.ToggleStove();
            k.TryFillPot(out _);
            foreach (Ingredient i in Kitchen.Recipe) k.TrySlice(i, out _);
            k.Tick(30);
            k.TryAddIngredients(out _);
            k.Tick(60);
            k.ToggleStove();
            k.Tick(500);
            Assert.That(k.Stage, Is.EqualTo(PotStage.Done));
        }
    }

    public class OrderBookTests
    {
        [Test]
        public void Orders_ArriveAfterTheFirstDelay_AndRespectTheLimit()
        {
            var book = new OrderBook(OrderRules.Default(), 1, now: 0);
            Assert.That(book.TryCreate(10, 5), Is.Null, "Too early");
            FoodOrder first = book.TryCreate(45, 5);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Seat, Is.InRange(1, 3), "Customers are bots, never you");
            Assert.That(first.TableIndex, Is.InRange(0, 4));
            Assert.That(first.Deadline, Is.EqualTo(45 + 240));

            Assert.That(book.TryCreate(46, 5), Is.Null, "Next one isn't due yet");
            FoodOrder second = book.TryCreate(45 + 150, 5);
            Assert.That(second, Is.Not.Null);
            Assert.That(book.TryCreate(10000, 5), Is.Null, "At most 2 open orders");
            Assert.That(book.Open, Has.Count.EqualTo(2));
        }

        [Test]
        public void Orders_Expire_AndCanBeDelivered()
        {
            var book = new OrderBook(OrderRules.Default(), 2, now: 0);
            FoodOrder order = book.TryCreate(45, 1);
            Assert.That(book.Find(order.TableIndex, order.Seat), Is.SameAs(order));
            Assert.That(book.Expire(100), Is.Empty);
            Assert.That(book.Deliver(order), Is.True);
            Assert.That(book.Open, Is.Empty);

            FoodOrder late = book.TryCreate(1000, 1);
            Assert.That(book.Expire(late.Deadline).Single(), Is.SameAs(late));
        }

        [Test]
        public void TipsMoveFaanBetweenPlayers()
        {
            var match = new ScoreMatch(RuleSet.Party(), MatchRules.Default(), 1);
            match.Transfer(2, 0, 8, PaymentReason.FoodTip);
            Assert.That(match.Players[0].Score, Is.EqualTo(8));
            Assert.That(match.Players[2].Score, Is.EqualTo(-8));
            Assert.That(match.Events.OfType<PaymentEvent>().Single().Reason, Is.EqualTo(PaymentReason.FoodTip));
        }
    }
}
