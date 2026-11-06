using System;

namespace MahjongNight.Core
{
    public enum Need : byte
    {
        Hunger,   // food
        Thirst,   // water
        Bladder,  // the "pee bar": drains over time (faster after drinking); the toilet refills it
    }

    /// <summary>Tuning for <see cref="SurvivalNeeds"/>. Durations are seconds from full to empty at rest.</summary>
    public sealed class NeedsRules
    {
        public double HungerSeconds { get; set; } = 420;
        public double ThirstSeconds { get; set; } = 300;
        public double BladderSeconds { get; set; } = 360;

        /// <summary>Drain multiplier while walking / sprinting.</summary>
        public double WalkMultiplier { get; set; } = 1.3;
        public double SprintMultiplier { get; set; } = 2.0;

        public double FoodRestores { get; set; } = 45;
        public double FoodThirstCost { get; set; } = 5;
        public double WaterRestores { get; set; } = 45;
        /// <summary>Drinking fills your bladder: this much comes off the pee bar.</summary>
        public double WaterBladderCost { get; set; } = 30;

        /// <summary>Seconds each action takes (you're away from the table meanwhile).</summary>
        public double EatSeconds { get; set; } = 3;
        public double DrinkSeconds { get; set; } = 2;
        public double ToiletSeconds { get; set; } = 4;

        public static NeedsRules Default() => new NeedsRules();
    }

    public enum Exertion : byte { Resting, Walking, Sprinting }

    /// <summary>
    /// Hunger, thirst and bladder bars (0–100, start full). They drain over time; eating,
    /// drinking and the toilet refill them. Any bar at 0 and the player dies.
    /// </summary>
    public sealed class SurvivalNeeds
    {
        public const double Max = 100;

        public SurvivalNeeds(NeedsRules rules = null)
        {
            Rules = rules ?? NeedsRules.Default();
        }

        public NeedsRules Rules { get; }
        public double Hunger { get; private set; } = Max;
        public double Thirst { get; private set; } = Max;
        public double Bladder { get; private set; } = Max;

        public bool IsDead => Hunger <= 0 || Thirst <= 0 || Bladder <= 0;

        /// <summary>The bar that ran out, if dead.</summary>
        public Need? CauseOfDeath =>
            Hunger <= 0 ? Need.Hunger : Thirst <= 0 ? Need.Thirst : Bladder <= 0 ? Need.Bladder : (Need?)null;

        public double Get(Need need) => need == Need.Hunger ? Hunger : need == Need.Thirst ? Thirst : Bladder;

        public void Tick(double seconds, Exertion exertion = Exertion.Resting)
        {
            if (IsDead || seconds <= 0)
                return;
            double m = exertion == Exertion.Sprinting ? Rules.SprintMultiplier
                : exertion == Exertion.Walking ? Rules.WalkMultiplier : 1;
            Hunger = Clamp(Hunger - Max / Rules.HungerSeconds * seconds * m);
            Thirst = Clamp(Thirst - Max / Rules.ThirstSeconds * seconds * m);
            Bladder = Clamp(Bladder - Max / Rules.BladderSeconds * seconds);
        }

        public void Eat()
        {
            if (IsDead) return;
            Hunger = Clamp(Hunger + Rules.FoodRestores);
            Thirst = Clamp(Thirst - Rules.FoodThirstCost);
        }

        public void Drink()
        {
            if (IsDead) return;
            Thirst = Clamp(Thirst + Rules.WaterRestores);
            Bladder = Clamp(Bladder - Rules.WaterBladderCost);
        }

        public void UseToilet()
        {
            if (IsDead) return;
            Bladder = Max;
        }

        private static double Clamp(double v) => Math.Max(0, Math.Min(Max, v));

        public static string DeathMessage(Need need)
        {
            switch (need)
            {
                case Need.Hunger: return "You starved at the mahjong table.";
                case Need.Thirst: return "You died of thirst.";
                default: return "Your bladder burst. You really should have gone.";
            }
        }
    }
}
