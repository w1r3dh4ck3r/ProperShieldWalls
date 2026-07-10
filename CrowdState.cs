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

        private static void EnsureCapacity(int index)
        {
            if (index < _crowdedUntil.Length) return;

            int newSize = _crowdedUntil.Length;
            while (newSize <= index) newSize *= 2;

            var grown = new float[newSize];
            Array.Copy(_crowdedUntil, grown, _crowdedUntil.Length);
            _crowdedUntil = grown;
        }
    }
}
