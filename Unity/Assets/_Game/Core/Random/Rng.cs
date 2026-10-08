using System;

namespace Game.Core.Random
{
    /// <summary>
    /// Deterministic, seedable pseudo-random generator (xoshiro256**). Same seed => same sequence
    /// on the same device/build. Never use System.Random or the Unity random API in the pure zone.
    /// The full state can be read and restored for persistence (<see cref="GetState"/>/<see cref="FromState"/>).
    /// </summary>
    public sealed class Rng
    {
        private ulong _s0, _s1, _s2, _s3;

        public Rng(ulong seed)
        {
            // Expand the seed with SplitMix64, as recommended by the xoshiro authors.
            ulong x = seed;
            _s0 = SplitMix64.Next(ref x);
            _s1 = SplitMix64.Next(ref x);
            _s2 = SplitMix64.Next(ref x);
            _s3 = SplitMix64.Next(ref x);
        }

        private Rng(RngState state)
        {
            if ((state.S0 | state.S1 | state.S2 | state.S3) == 0)
                throw new ArgumentException("All-zero state is invalid for xoshiro256**.", nameof(state));
            _s0 = state.S0; _s1 = state.S1; _s2 = state.S2; _s3 = state.S3;
        }

        public static Rng FromState(RngState state) => new Rng(state);

        public RngState GetState() => new RngState(_s0, _s1, _s2, _s3);

        /// <summary>Continues from a saved state in place (for owners that keep the same instance, e.g. a restored match).</summary>
        public void SetState(RngState state)
        {
            if ((state.S0 | state.S1 | state.S2 | state.S3) == 0)
                throw new ArgumentException("All-zero state is invalid for xoshiro256**.", nameof(state));
            _s0 = state.S0; _s1 = state.S1; _s2 = state.S2; _s3 = state.S3;
        }

        public ulong NextULong()
        {
            ulong result = RotateLeft(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>Uniform integer in [minInclusive, maxExclusive), without modulo bias (Lemire).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            uint range = (uint)((long)maxExclusive - minInclusive);
            ulong m = (ulong)NextUInt() * range;
            uint low = (uint)m;
            if (low < range)
            {
                uint threshold = (uint)(-(int)range) % range;
                while (low < threshold)
                {
                    m = (ulong)NextUInt() * range;
                    low = (uint)m;
                }
            }
            return (int)((long)minInclusive + (long)(m >> 32));
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1.0f / (1U << 24));

        private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
    }

    /// <summary>Serializable snapshot of an <see cref="Rng"/>.</summary>
    public readonly struct RngState : IEquatable<RngState>
    {
        public readonly ulong S0, S1, S2, S3;

        public RngState(ulong s0, ulong s1, ulong s2, ulong s3)
        {
            S0 = s0; S1 = s1; S2 = s2; S3 = s3;
        }

        public bool Equals(RngState o) => S0 == o.S0 && S1 == o.S1 && S2 == o.S2 && S3 == o.S3;
        public override bool Equals(object obj) => obj is RngState o && Equals(o);
        public override int GetHashCode() => (S0 ^ S1 ^ S2 ^ S3).GetHashCode();
    }

    internal static class SplitMix64
    {
        public static ulong Next(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public static ulong Mix(ulong x)
        {
            ulong s = x;
            return Next(ref s);
        }
    }
}
