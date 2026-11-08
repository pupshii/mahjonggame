using System.Collections.Generic;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class CookingScoreTests
    {
        [Test]
        public void Timing_FullMarksNearTarget_FallingToZero()
        {
            Assert.That(CookingScore.Timing(0.80, 0.8, 0.02, 0.2), Is.EqualTo(100));
            Assert.That(CookingScore.Timing(0.81, 0.8, 0.02, 0.2), Is.EqualTo(100));
            Assert.That(CookingScore.Timing(0.92, 0.8, 0.02, 0.2), Is.EqualTo(50).Within(1e-9));
            Assert.That(CookingScore.Timing(0.30, 0.8, 0.02, 0.2), Is.EqualTo(0));
        }

        [Test]
        public void Slice_ScoresEachGuide_AndPunishesMissesAndExtraCuts()
        {
            var guides = new List<double> { 0.2, 0.4, 0.6, 0.8 };
            Assert.That(CookingScore.Slice(new List<double> { 0.2, 0.4, 0.6, 0.8 }, guides), Is.EqualTo(100));
            Assert.That(CookingScore.Slice(new List<double> { 0.8, 0.6, 0.4, 0.2 }, guides), Is.EqualTo(100), "Any order");
            Assert.That(CookingScore.Slice(new List<double> { 0.2, 0.4 }, guides), Is.EqualTo(50), "Two guides never cut");
            Assert.That(CookingScore.Slice(new List<double> { 0.2, 0.4, 0.6, 0.8, 0.5 }, guides), Is.EqualTo(90), "One extra cut");
            Assert.That(CookingScore.Slice(new List<double>(), guides), Is.EqualTo(0));
        }

        [Test]
        public void SpeedAndStir()
        {
            Assert.That(CookingScore.Speed(3, 5, 12), Is.EqualTo(100));
            Assert.That(CookingScore.Speed(8.5, 5, 12), Is.EqualTo(70).Within(1e-9));
            Assert.That(CookingScore.Speed(30, 5, 12), Is.EqualTo(40));
            Assert.That(CookingScore.Stir(3, 6), Is.EqualTo(50));
            Assert.That(CookingScore.Stir(9, 6), Is.EqualTo(100));
        }

        [Test]
        public void BetterSoup_BiggerTip()
        {
            Assert.That(CookingScore.Grade(95), Is.EqualTo(DishGrade.Perfect));
            Assert.That(CookingScore.Grade(75), Is.EqualTo(DishGrade.Great));
            Assert.That(CookingScore.Grade(50), Is.EqualTo(DishGrade.Ok));
            Assert.That(CookingScore.Grade(10), Is.EqualTo(DishGrade.Bad));
            Assert.That(CookingScore.TipBonus(DishGrade.Perfect), Is.GreaterThan(CookingScore.TipBonus(DishGrade.Great)));
            Assert.That(CookingScore.TipBonus(DishGrade.Bad), Is.LessThan(0));
        }

        [Test]
        public void Kitchen_DishQuality_AveragesSteps_AndTheLadleKeepsIt()
        {
            var k = new Kitchen();
            k.ToggleStove();
            k.RecordScore(CookStep.Ignite, 100);
            k.TryFillPot(out _);
            k.RecordScore(CookStep.Fill, 80);
            foreach (Ingredient i in Kitchen.Recipe)
            {
                k.TrySlice(i, out _);
                k.RecordScore(CookStep.Slice, i == Ingredient.Chicken ? 40 : 100);   // slices average 85
            }
            Assert.That(k.Quality, Is.EqualTo((100 + 80 + 85) / 3.0).Within(1e-9));

            k.Tick(k.Rules.BoilSeconds);
            k.TryAddIngredients(out _);
            k.RecordScore(CookStep.AddToPot, 100);
            // Nobody stirred yet: counts as the unstirred score.
            Assert.That(k.Quality, Is.EqualTo((100 + 80 + 85 + 100 + Kitchen.UnstirredScore) / 5.0).Within(1e-9));
            k.RecordScore(CookStep.Stir, 100);
            k.Tick(k.Rules.SimmerSeconds);
            k.RecordScore(CookStep.Ladle, 100);
            double expected = (100 + 80 + 85 + 100 + 100 + 100) / 6.0;
            Assert.That(k.TryLadle(out _), Is.True);
            Assert.That(k.LastBowlQuality, Is.EqualTo(expected).Within(1e-9));

            // The next pot starts fresh, but the stove is still lit (its score carries over).
            Assert.That(k.HasScore(CookStep.Fill), Is.False);
            Assert.That(k.HasScore(CookStep.Ignite), Is.True);
            k.RecordScore(CookStep.Fill, 200);
            Assert.That(k.Quality, Is.EqualTo(100), "Scores are clamped to 100");
        }
    }
}
