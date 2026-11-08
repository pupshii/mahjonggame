using System;

namespace MahjongNight.Core
{
    /// <summary>How your dog feels — and so how often they text (or call) you.</summary>
    public enum DogMood : byte
    {
        Content,   // fed and napping: leaves you alone
        Peckish,   // the occasional "when r u coming home??"
        Hungry,    // texts much more often
        Starving,  // phones you, howling, every few seconds' worth of hand
    }

    /// <summary>Tuning for <see cref="Dog"/>. Times are in seconds.</summary>
    public sealed class DogRules
    {
        /// <summary>Seconds from a full bowl to starving, at the normal rate.</summary>
        public double SecondsFullToEmpty { get; set; } = 360;
        public double StartFullness { get; set; } = 75;

        public double PeckishBelow { get; set; } = 60;
        public double HungryBelow { get; set; } = 30;
        public double StarvingBelow { get; set; } = 10;

        /// <summary>Feeding them something special (a bowl of your chicken soup) slows their hunger for a while.</summary>
        public double TreatSeconds { get; set; } = 240;
        public double TreatDrainMultiplier { get; set; } = 0.5;

        /// <summary>Petting is worth a little fullness (it's love), once per cooldown.</summary>
        public double PetRestores { get; set; } = 8;
        public double PetCooldown { get; set; } = 30;

        /// <summary>Seconds they spend at the bowl after you fill it (just for show).</summary>
        public double EatSeconds { get; set; } = 6;

        /// <summary>Seconds between messages for each mood (min, max).</summary>
        public (double Min, double Max) PeckishGap { get; set; } = (70, 120);
        public (double Min, double Max) HungryGap { get; set; } = (35, 60);
        public (double Min, double Max) StarvingGap { get; set; } = (15, 30);

        public static DogRules Default() => new DogRules();
    }

    /// <summary>
    /// Your dog at home. Their fullness (0–100) drops over time; the hungrier they are, the more
    /// often they message you. Go home and fill their bowl to make them stop.
    /// </summary>
    public sealed class Dog
    {
        public const double Max = 100;

        public Dog(DogRules rules = null)
        {
            Rules = rules ?? DogRules.Default();
            Fullness = Math.Max(0, Math.Min(Max, Rules.StartFullness));
        }

        public DogRules Rules { get; }
        public double Fullness { get; private set; }
        /// <summary>Seconds left of a treat's slower digestion.</summary>
        public double TreatLeft { get; private set; }
        public double PetCooldownLeft { get; private set; }
        /// <summary>Seconds left of eating from the bowl (for the animation).</summary>
        public double EatingLeft { get; private set; }
        public bool IsEating => EatingLeft > 0;

        public DogMood Mood =>
            Fullness < Rules.StarvingBelow ? DogMood.Starving
            : Fullness < Rules.HungryBelow ? DogMood.Hungry
            : Fullness < Rules.PeckishBelow ? DogMood.Peckish
            : DogMood.Content;

        /// <summary>A starving dog phones you instead of texting.</summary>
        public bool Calls => Mood == DogMood.Starving;

        /// <summary>How long until the next message for this mood (min, max), or null if they're content.</summary>
        public (double Min, double Max)? MessageGap
        {
            get
            {
                switch (Mood)
                {
                    case DogMood.Peckish: return Rules.PeckishGap;
                    case DogMood.Hungry: return Rules.HungryGap;
                    case DogMood.Starving: return Rules.StarvingGap;
                    default: return null;
                }
            }
        }

        public void Tick(double dt)
        {
            if (dt <= 0)
                return;
            double rate = Max / Rules.SecondsFullToEmpty;
            if (TreatLeft > 0)
                rate *= Rules.TreatDrainMultiplier;
            Fullness = Math.Max(0, Fullness - rate * dt);
            TreatLeft = Math.Max(0, TreatLeft - dt);
            PetCooldownLeft = Math.Max(0, PetCooldownLeft - dt);
            EatingLeft = Math.Max(0, EatingLeft - dt);
        }

        /// <summary>Fill the bowl with dog food: full again.</summary>
        public void Feed()
        {
            Fullness = Max;
            EatingLeft = Rules.EatSeconds;
        }

        /// <summary>Give them a bowl of chicken soup: full, and they stay full for longer.</summary>
        public void FeedTreat()
        {
            Feed();
            TreatLeft = Rules.TreatSeconds;
        }

        /// <summary>Pet them. Returns false (and does nothing) while petting is on cooldown.</summary>
        public bool TryPet()
        {
            if (PetCooldownLeft > 0)
                return false;
            Fullness = Math.Min(Max, Fullness + Rules.PetRestores);
            PetCooldownLeft = Rules.PetCooldown;
            return true;
        }

        public static string Describe(DogMood mood)
        {
            switch (mood)
            {
                case DogMood.Content: return "content";
                case DogMood.Peckish: return "peckish";
                case DogMood.Hungry: return "hungry";
                default: return "STARVING";
            }
        }
    }
}
