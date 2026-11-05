using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>
    /// A bamboo steamer of dumplings, exactly one of which is the ghost pepper. Eaten dumplings
    /// aren't replaced, so each pick is riskier than the last (1 in remaining).
    /// </summary>
    public sealed class Steamer
    {
        private readonly bool[] eaten;
        private int pepperSlot;

        internal Steamer(int size, SeededRandom rng)
        {
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));
            eaten = new bool[size];
            Refill(rng);
        }

        public int Size => eaten.Length;

        public int Remaining
        {
            get
            {
                int n = 0;
                foreach (bool e in eaten)
                    if (!e)
                        n++;
                return n;
            }
        }

        /// <summary>Chance that the next dumpling is the pepper.</summary>
        public double PepperChance => Remaining == 0 ? 0 : 1.0 / Remaining;

        public bool IsEaten(int slot) => eaten[slot];

        public List<int> UneatenSlots()
        {
            var slots = new List<int>();
            for (int i = 0; i < eaten.Length; i++)
                if (!eaten[i])
                    slots.Add(i);
            return slots;
        }

        /// <summary>Host-only secret: which slot holds the pepper. Never send this to clients.</summary>
        public int RevealPepperSlot() => pepperSlot;

        internal void Refill(SeededRandom rng)
        {
            for (int i = 0; i < eaten.Length; i++)
                eaten[i] = false;
            pepperSlot = rng.NextInt(eaten.Length);
        }

        /// <summary>Eats the dumpling in <paramref name="slot"/>; true if it was the pepper.</summary>
        internal bool Eat(int slot)
        {
            eaten[slot] = true;
            return slot == pepperSlot;
        }
    }
}
