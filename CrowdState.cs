using System;

namespace ProperShieldWalls
{
    /// <summary>
    /// Tracks which agents recently had an attack wind-up clip a friendly, keyed by Agent.Index.
    /// Main-thread only. Deliberately free of TaleWorlds types so the test project can compile it.
    /// </summary>
    internal static class CrowdState
    {
        private const int InitialCapacity = 256;

        private static float[] _crowdedUntil = new float[InitialCapacity];

        internal static void Reset()
        {
            _crowdedUntil = new float[InitialCapacity];
        }

        internal static void Stamp(int agentIndex, float now, float duration)
        {
            if (agentIndex < 0) return;
            EnsureCapacity(agentIndex);
            _crowdedUntil[agentIndex] = now + duration;
        }

        internal static bool IsCrowded(int agentIndex, float now)
        {
            if (agentIndex < 0 || agentIndex >= _crowdedUntil.Length) return false;
            return now < _crowdedUntil[agentIndex];
        }

        /// <summary>
        /// Computes the array length needed to hold the given index, using a doubling strategy.
        /// Pure function: no allocation, no side effects. Handles integer overflow by clamping.
        /// </summary>
        /// <remarks>
        /// At index == int.MaxValue, returns int.MaxValue (which is unallocatable in .NET,
        /// but serves as a sentinel for pathological indices; actual allocation will fail gracefully).
        /// </remarks>
        internal static int ComputeNewSize(int currentLength, int index)
        {
            int newSize = currentLength;
            while (newSize <= index)
            {
                // Prevent unchecked integer overflow: if doubling would exceed int.MaxValue/2,
                // clamp to int.MaxValue. (Bannerlord agent indices are bounded to ~thousands;
                // OutOfMemory is only possible for pathological indices approaching 2^31.)
                if (newSize > int.MaxValue / 2)
                {
                    newSize = int.MaxValue;
                    break;
                }
                newSize *= 2;
            }
            return newSize;
        }

        private static void EnsureCapacity(int index)
        {
            if (index < _crowdedUntil.Length) return;

            int newSize = ComputeNewSize(_crowdedUntil.Length, index);
            var grown = new float[newSize];
            Array.Copy(_crowdedUntil, grown, _crowdedUntil.Length);
            _crowdedUntil = grown;
        }
    }
}
