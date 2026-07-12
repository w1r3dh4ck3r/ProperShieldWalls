using System.Collections.Generic;

namespace ProperShieldWalls
{
    /// <summary>
    /// Plans the slot swaps that put shielded men at the low ranks of a file.
    ///
    /// Vanilla already does this (LineFormation.SwitchFrontUnitTypesToFrontRows) but opens with
    /// `if (Interval &lt;= 0f) return;` — and ArrangementOrder.GetUnitSpacingOf returns 0 for BOTH
    /// ShieldWall and Square, so Interval is exactly 0 and the rotation returns on its first line,
    /// forever. It has never run in either formation.
    ///
    /// One rule covers both, because rank means different things per arrangement:
    ///   ShieldWall (LineFormation)              rank 0 = the front rank  -> shields to the front
    ///   Square (RectilinearSchiltronFormation)  rank 0 = the outer ring  -> shields on the perimeter
    ///                                           (fileIndex picks the side; rank walks inward from it)
    ///
    /// Vanilla also only ever swaps ADJACENT ranks, one pair per 0.5 s tick, so a shieldless man
    /// bubbles rearward over several seconds. This partitions the whole file in a single pass, so a
    /// shieldless front-ranker is replaced on the next sweep rather than four sweeps later.
    ///
    /// Deliberately free of TaleWorlds types so the net8.0 test project can source-link it.
    /// </summary>
    internal static class ShieldRotation
    {
        internal struct Swap
        {
            internal readonly int A;
            internal readonly int B;

            internal Swap(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        /// <summary>
        /// <paramref name="hasShield"/> is one file, ordered by rank ascending (index 0 = rank 0).
        /// Returns the swaps to apply IN ORDER. Empty when the file is already partitioned, so a
        /// settled formation costs nothing and cannot churn.
        /// </summary>
        internal static List<Swap> PlanFileSwaps(bool[] hasShield)
        {
            var swaps = new List<Swap>();
            if (hasShield == null) return swaps;

            // Planning mirrors each swap as it goes, so that `next` keeps meaning "lowest slot not
            // yet holding a shield" across the whole walk — without that, a file with several gaps
            // plans nonsense. Mirror on a COPY: the caller's array is not ours to mutate.
            var state = (bool[])hasShield.Clone();

            // Partition by selection: walk front-to-back; every shielded man found below `next` is
            // swapped up into it. One swap per misplaced shielded man — the minimum possible.
            int next = 0;
            for (int i = 0; i < state.Length; i++)
            {
                if (!state[i]) continue;

                if (i != next)
                {
                    swaps.Add(new Swap(next, i));

                    bool tmp = state[next];
                    state[next] = state[i];
                    state[i] = tmp;
                }

                next++;
            }

            return swaps;
        }
    }
}
