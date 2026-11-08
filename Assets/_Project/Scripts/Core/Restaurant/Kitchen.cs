using System.Collections.Generic;

namespace MahjongNight.Core
{
    public enum Ingredient : byte { Chicken, Onion, Garlic, Herbs }

    public enum PotStage : byte
    {
        Empty,
        /// <summary>Water in the pot, heating (only while the stove is on).</summary>
        Heating,
        Boiling,
        /// <summary>Ingredients in, cooking (only while the stove is on).</summary>
        Simmering,
        /// <summary>Soup's ready — ladle it before it burns.</summary>
        Done,
        Burnt
    }

    /// <summary>Timings for the chicken soup recipe (about 1.5 minutes if you slice while the water boils).</summary>
    public sealed class KitchenRules
    {
        public double BoilSeconds { get; set; } = 30;
        public double SliceSeconds { get; set; } = 5;
        public double SimmerSeconds { get; set; } = 60;
        /// <summary>A finished soup left on a lit stove this long burns.</summary>
        public double BurnAfterSeconds { get; set; } = 60;

        public static KitchenRules Default() => new KitchenRules();
    }

    /// <summary>
    /// The club kitchen, one pot, one recipe — chicken soup:
    /// stove on → fill the pot and boil the water → slice chicken, onion, garlic and herbs →
    /// add them once the water boils → simmer → ladle into a bowl. Pure state machine; the game
    /// handles walking over and the slicing animation.
    /// </summary>
    public sealed class Kitchen
    {
        public static readonly Ingredient[] Recipe = { Ingredient.Chicken, Ingredient.Onion, Ingredient.Garlic, Ingredient.Herbs };

        private readonly HashSet<Ingredient> sliced = new HashSet<Ingredient>();

        public Kitchen(KitchenRules rules = null)
        {
            Rules = rules ?? KitchenRules.Default();
        }

        public KitchenRules Rules { get; }
        public bool StoveOn { get; private set; }
        public PotStage Stage { get; private set; } = PotStage.Empty;
        /// <summary>Seconds of progress in the current stage.</summary>
        public double StageTime { get; private set; }

        public bool IsSliced(Ingredient i) => sliced.Contains(i);
        public bool AllSliced => sliced.Count == Recipe.Length;

        /// <summary>0..1 progress of the current timed stage (heating, simmering), else 0.</summary>
        public double Progress =>
            Stage == PotStage.Heating ? StageTime / Rules.BoilSeconds
            : Stage == PotStage.Simmering ? StageTime / Rules.SimmerSeconds
            : 0;

        /// <summary>Seconds before a finished soup burns (only counts down while the stove is on).</summary>
        public double SecondsUntilBurnt => Stage == PotStage.Done ? Rules.BurnAfterSeconds - StageTime : 0;

        public void ToggleStove() => StoveOn = !StoveOn;

        public bool TryFillPot(out string error)
        {
            if (Stage != PotStage.Empty)
            {
                error = "The pot isn't empty.";
                return false;
            }
            error = null;
            SetStage(PotStage.Heating);
            return true;
        }

        public bool TrySlice(Ingredient ingredient, out string error)
        {
            if (sliced.Contains(ingredient))
            {
                error = $"The {ingredient.ToString().ToLowerInvariant()} is already sliced.";
                return false;
            }
            if (Stage == PotStage.Simmering || Stage == PotStage.Done)
            {
                error = "This pot already has its ingredients.";
                return false;
            }
            error = null;
            sliced.Add(ingredient);
            return true;
        }

        public bool TryAddIngredients(out string error)
        {
            if (Stage != PotStage.Boiling)
            {
                error = Stage == PotStage.Empty ? "Fill the pot with water first."
                    : Stage == PotStage.Heating ? "Wait for the water to boil."
                    : "The pot already has its ingredients.";
                return false;
            }
            if (!AllSliced)
            {
                error = "Slice the chicken, onion, garlic and herbs first.";
                return false;
            }
            error = null;
            SetStage(PotStage.Simmering);
            return true;
        }

        /// <summary>Ladles the finished soup into a bowl and empties the pot.</summary>
        public bool TryLadle(out string error)
        {
            if (Stage != PotStage.Done)
            {
                error = Stage == PotStage.Burnt ? "It's burnt. Throw it out." : "The soup isn't ready yet.";
                return false;
            }
            error = null;
            Reset();
            return true;
        }

        /// <summary>Throws out a burnt pot.</summary>
        public bool TryDump(out string error)
        {
            if (Stage != PotStage.Burnt)
            {
                error = "Nothing to throw out.";
                return false;
            }
            error = null;
            Reset();
            return true;
        }

        public void Tick(double seconds)
        {
            if (!StoveOn || seconds <= 0)
                return;
            StageTime += seconds;
            if (Stage == PotStage.Heating && StageTime >= Rules.BoilSeconds)
                SetStage(PotStage.Boiling);
            else if (Stage == PotStage.Simmering && StageTime >= Rules.SimmerSeconds)
                SetStage(PotStage.Done);
            else if (Stage == PotStage.Done && StageTime >= Rules.BurnAfterSeconds)
                SetStage(PotStage.Burnt);
        }

        /// <summary>What to do next, for the recipe card.</summary>
        public string NextStep()
        {
            if (!StoveOn && Stage != PotStage.Done && Stage != PotStage.Burnt) return "Turn on the stove";
            switch (Stage)
            {
                case PotStage.Empty: return "Fill the pot with water";
                case PotStage.Heating: return AllSliced ? "Wait for the water to boil" : "Slice the ingredients while the water heats";
                case PotStage.Boiling: return AllSliced ? "Add the ingredients to the pot" : "Slice the ingredients";
                case PotStage.Simmering: return "Let it simmer";
                case PotStage.Done: return "Ladle it into a bowl — before it burns!";
                default: return "It's burnt! Throw it out and start again";
            }
        }

        private void SetStage(PotStage stage)
        {
            Stage = stage;
            StageTime = 0;
        }

        private void Reset()
        {
            sliced.Clear();
            SetStage(PotStage.Empty);
        }
    }
}
