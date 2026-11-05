using System;

namespace MahjongNight.Core
{
    /// <summary>
    /// Deterministic random number generator (SplitMix64). Same seed → same sequence on
    /// every platform and runtime, unlike <see cref="System.Random"/>. Used for shuffling
    /// so a hand can be replayed exactly from its seed (debugging, replays, desync checks).
    /// </summary>
    public sealed class SeededRandom
    {
        private ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform integer in [0, maxExclusive), without modulo bias.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Must be positive.");

            ulong bound = (ulong)maxExclusive;
            ulong threshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                ulong r = NextUInt64();
                if (r >= threshold)
                    return (int)(r % bound);
            }
        }
    }
}
