using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>The cooking mini-games, one per step of the recipe.</summary>
    public enum CookStep : byte
    {
        Ignite,    // light the stove: stop the needle in the flame zone
        Fill,      // pour water up to the line
        Slice,     // swipe through each cutting guide (once per ingredient)
        AddToPot,  // drag every ingredient into the pot
        Stir,      // circle the spoon while it simmers
        Ladle,     // pour a bowl up to the line
    }

    public enum DishGrade : byte { Bad, Ok, Great, Perfect }

    /// <summary>
    /// Scoring for the cooking mini-games (each 0–100) and what a dish's overall quality is
    /// worth: better soup, bigger tip.
    /// </summary>
    public static class CookingScore
    {
        /// <summary>
        /// A timing / aim score: 100 within <paramref name="perfect"/> of the target, falling
        /// linearly to 0 at <paramref name="perfect"/> + <paramref name="tolerance"/> away.
        /// </summary>
        public static double Timing(double value, double target, double perfect, double tolerance)
        {
            double error = Math.Abs(value - target) - perfect;
            if (error <= 0)
                return 100;
            return Math.Max(0, 100 * (1 - error / tolerance));
        }

        /// <summary>
        /// Slicing: each guide is matched with the nearest unused cut and scored with
        /// <see cref="Timing"/>; a guide with no cut scores 0, and every extra cut costs 10.
        /// </summary>
        public static double Slice(IReadOnlyList<double> cuts, IReadOnlyList<double> guides, double perfect = 0.012, double tolerance = 0.06)
        {
            if (guides.Count == 0)
                return 100;
            var unused = new List<double>(cuts);
            double total = 0;
            foreach (double g in guides)
            {
                if (unused.Count == 0)
                    continue;
                int best = 0;
                for (int i = 1; i < unused.Count; i++)
                    if (Math.Abs(unused[i] - g) < Math.Abs(unused[best] - g))
                        best = i;
                total += Timing(unused[best], g, perfect, tolerance);
                unused.RemoveAt(best);
            }
            return Math.Max(0, total / guides.Count - 10 * unused.Count);
        }

        /// <summary>Doing it fast: 100 up to <paramref name="fast"/> seconds, falling to 40 at <paramref name="slow"/>, 40 after.</summary>
        public static double Speed(double seconds, double fast, double slow)
        {
            if (seconds <= fast)
                return 100;
            if (seconds >= slow)
                return 40;
            return 100 - 60 * (seconds - fast) / (slow - fast);
        }

        /// <summary>Stirring: laps of the pot against a target number of laps.</summary>
        public static double Stir(double laps, double targetLaps) =>
            targetLaps <= 0 ? 100 : Math.Max(0, Math.Min(100, 100 * laps / targetLaps));

        public static DishGrade Grade(double score) =>
            score >= 90 ? DishGrade.Perfect : score >= 70 ? DishGrade.Great : score >= 45 ? DishGrade.Ok : DishGrade.Bad;

        /// <summary>Extra tip (or less) for the soup's grade, on top of the base tip.</summary>
        public static int TipBonus(DishGrade grade)
        {
            switch (grade)
            {
                case DishGrade.Perfect: return 5;
                case DishGrade.Great: return 2;
                case DishGrade.Ok: return 0;
                default: return -4;
            }
        }

        public static string Shout(DishGrade grade)
        {
            switch (grade)
            {
                case DishGrade.Perfect: return "PERFECT!";
                case DishGrade.Great: return "GREAT!";
                case DishGrade.Ok: return "OK!";
                default: return "OOPS…";
            }
        }
    }
}
