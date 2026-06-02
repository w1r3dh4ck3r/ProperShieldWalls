using System.Collections.Generic;
using ProperShieldWalls.Models;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    // Translates the slot positions of losing-side agents based on per-rank pressure delta.
    // Pressure formula (MVP): front * 1.0 + second * 0.5 + third * 0.25.
    internal class PressureResolver
    {
        internal void Tick(IReadOnlyList<EngagementPair> pairs, float dt)
        {
            foreach (var pair in pairs)
            {
                if (pair.State != EngagementState.Locked) continue;
                ApplyPressure(pair, dt);
            }
        }

        private static void ApplyPressure(EngagementPair pair, float dt)
        {
            float pA = ComputePressure(pair.FormationA);
            float pB = ComputePressure(pair.FormationB);
            float delta = pA - pB;

            // Small per-tick cap: max 5 cm/s drift
            float drift = MathF.Clamp(delta * 0.005f * dt, -0.02f, 0.02f);
            if (MathF.Abs(drift) < 0.001f) return;

            // FormationA.CurrentDirection points toward B (confirmed facing each other by detector).
            Vec2 aForward = pair.FormationA.CurrentDirection;

            if (drift > 0f)
            {
                // A is stronger: B retreats (nudge B agents backward, i.e. in A's forward direction)
                NudgeFormation(pair.FormationB, aForward * drift);
            }
            else
            {
                // B is stronger: A retreats (drift is negative; negate so A moves back)
                NudgeFormation(pair.FormationA, aForward * drift);
            }
        }

        private static float ComputePressure(Formation f)
        {
            float p = 0f;
            foreach (var unit in f.Arrangement.GetAllUnits())
            {
                if (!(unit is Agent a) || !a.IsActive()) continue;
                int rank = ((IFormationUnit)a).FormationRankIndex;
                if (rank == 0)      p += 1.0f;
                else if (rank == 1) p += 0.5f;
                else if (rank == 2) p += 0.25f;
            }
            return p;
        }

        private static void NudgeFormation(Formation f, Vec2 delta)
        {
            foreach (var unit in f.Arrangement.GetAllUnits())
                if (unit is Agent a && a.IsActive()) OthismosState.NudgeSlot(a, delta);
        }
    }
}
