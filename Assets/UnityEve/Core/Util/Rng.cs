namespace Unity.Eve.Core.Util
{
    /// <summary>
    /// Seeded xoshiro128** generator. Every random roll in Core goes through this, never
    /// <c>System.Random</c> or <c>UnityEngine.Random</c>, so a battle replays identically from its seed.
    /// </summary>
    public sealed class Rng
    {
        private uint _s0, _s1, _s2, _s3;

        public Rng(uint seed)
        {
            // SplitMix32 expands a single seed into four non-zero state words.
            _s0 = SplitMix(ref seed);
            _s1 = SplitMix(ref seed);
            _s2 = SplitMix(ref seed);
            _s3 = SplitMix(ref seed);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }

        public uint NextUInt()
        {
            var result = Rotl(_s1 * 5, 7) * 9;
            var t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 11);
            return result;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public bool Chance(float probability) => NextFloat() < probability;

        private static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        private static uint SplitMix(ref uint state)
        {
            var z = state += 0x9E3779B9u;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            return z ^ (z >> 16);
        }
    }
}
