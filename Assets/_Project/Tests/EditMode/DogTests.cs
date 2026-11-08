using MahjongNight.Core;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class DogTests
    {
        [Test]
        public void StartsContent_ThenGetsHungrierOverTime()
        {
            var dog = new Dog();
            Assert.That(dog.Mood, Is.EqualTo(DogMood.Content));
            Assert.That(dog.MessageGap, Is.Null, "A content dog doesn't message you");

            double rate = Dog.Max / dog.Rules.SecondsFullToEmpty;
            dog.Tick((dog.Rules.StartFullness - 59) / rate);
            Assert.That(dog.Mood, Is.EqualTo(DogMood.Peckish));
            Assert.That(dog.MessageGap, Is.EqualTo(dog.Rules.PeckishGap));

            dog.Tick(30 / rate);
            Assert.That(dog.Mood, Is.EqualTo(DogMood.Hungry));
            Assert.That(dog.Calls, Is.False);

            dog.Tick(20 / rate);
            Assert.That(dog.Mood, Is.EqualTo(DogMood.Starving));
            Assert.That(dog.Calls, Is.True, "A starving dog phones you");

            dog.Tick(10000);
            Assert.That(dog.Fullness, Is.EqualTo(0));
        }

        [Test]
        public void HungrierDogs_MessageMoreOften()
        {
            var r = DogRules.Default();
            Assert.That(r.HungryGap.Max, Is.LessThan(r.PeckishGap.Min));
            Assert.That(r.StarvingGap.Max, Is.LessThan(r.HungryGap.Min));
        }

        [Test]
        public void Feeding_FillsThemUp()
        {
            var dog = new Dog();
            dog.Tick(1000);
            dog.Feed();
            Assert.That(dog.Fullness, Is.EqualTo(Dog.Max));
            Assert.That(dog.Mood, Is.EqualTo(DogMood.Content));
            Assert.That(dog.IsEating, Is.True);
            dog.Tick(dog.Rules.EatSeconds);
            Assert.That(dog.IsEating, Is.False);
        }

        [Test]
        public void Treat_SlowsHungerWhileItLasts()
        {
            var plain = new Dog();
            var treated = new Dog();
            plain.Feed();
            treated.FeedTreat();
            plain.Tick(100);
            treated.Tick(100);
            double plainDrop = Dog.Max - plain.Fullness;
            double treatDrop = Dog.Max - treated.Fullness;
            Assert.That(treatDrop, Is.EqualTo(plainDrop * treated.Rules.TreatDrainMultiplier).Within(1e-6));

            treated.Tick(treated.Rules.TreatSeconds);   // treat wears off
            Assert.That(treated.TreatLeft, Is.EqualTo(0));
        }

        [Test]
        public void Petting_HelpsALittle_OncePerCooldown()
        {
            var dog = new Dog();
            dog.Tick(100);
            double before = dog.Fullness;
            Assert.That(dog.TryPet(), Is.True);
            Assert.That(dog.Fullness, Is.EqualTo(before + dog.Rules.PetRestores).Within(1e-9));
            Assert.That(dog.TryPet(), Is.False, "Cooldown");
            dog.Tick(dog.Rules.PetCooldown);
            Assert.That(dog.TryPet(), Is.True);

            dog.Feed();
            dog.Tick(dog.Rules.PetCooldown);
            dog.TryPet();
            Assert.That(dog.Fullness, Is.LessThanOrEqualTo(Dog.Max));
        }
    }
}
