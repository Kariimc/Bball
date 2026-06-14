namespace Shift9.Sim.Core
{
    /// <summary>
    /// The single source of randomness for the whole simulation — the "dice roller".
    /// Same seed in => exact same sequence out, forever, on every device. This is the
    /// foundation for repeatable replays and (later) online play where both machines
    /// MUST roll identical dice. Uses SplitMix64: integer-only state with a deterministic
    /// float conversion, so results do not drift across CPUs the way ad-hoc float RNGs do.
    ///
    /// Pass this struct BY REF through the sim so every consumer advances the one shared
    /// stream; never call UnityEngine.Random in simulation code.
    /// </summary>
    public struct DeterministicRng
    {
        private ulong _state;

        public DeterministicRng(ulong seed)
        {
            // Avoid the all-zero fixed point; any nonzero seed gives a full-period stream.
            _state = seed != 0UL ? seed : 0x9E3779B97F4A7C15UL;
        }

        /// <summary>Raw 64-bit draw (SplitMix64). Advances the stream.</summary>
        public ulong NextULong()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Float in [0,1). Uses the top 24 bits, so the division is exact and portable.</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) * (1.0f / 16777216.0f); // 2^24
        }

        /// <summary>Integer in [minInclusive, maxExclusive). Returns min if the range is empty.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            // Lemire-style unbiased reduction: multiply-high a 32-bit draw by the span.
            uint draw = (uint)(NextULong() >> 32);
            ulong wide = (ulong)draw * span;
            return minInclusive + (int)(wide >> 32);
        }

        /// <summary>True with the given probability [0,1]. The core coin-flip for shot make/miss.</summary>
        public bool Chance(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return NextFloat() < probability;
        }

        /// <summary>
        /// Derives an independent sub-stream from this one without disturbing it, so a feature
        /// can have its own repeatable dice (e.g., crowd flavor) that never desyncs core gameplay.
        /// </summary>
        public DeterministicRng Fork(ulong salt)
        {
            ulong mixed = _state ^ (salt * 0xD1B54A32D192ED03UL);
            return new DeterministicRng(mixed);
        }

        /// <summary>Current internal state — snapshot this to save/restore or to checksum a replay.</summary>
        public ulong State => _state;
    }
}
