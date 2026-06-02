using System.Collections.Generic;
using ProperShieldWalls.Models;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    // Enforces shield-down posture for front-rank agents in locked formations.
    // Attack direction is handled naturally by the spatial constraint (SlotLockPatch) —
    // no animation forcing needed per doc 03 section 6d.
    internal class StabForcer
    {
        internal void Tick(IReadOnlyList<EngagementPair> pairs)
        {
            foreach (var pair in pairs)
            {
                if (pair.State != EngagementState.Locked) continue;
                EnforceFormation(pair.FormationA);
                EnforceFormation(pair.FormationB);
            }
        }

        private static void EnforceFormation(Formation f)
        {
            foreach (var unit in f.Arrangement.GetAllUnits())
            {
                if (!(unit is Agent agent)) continue;
                if (!agent.IsActive() || !agent.IsAIControlled) continue;
                if (((IFormationUnit)agent).FormationRankIndex != 0) continue;

                if (agent.HasShieldCached)
                    agent.EnforceShieldUsage(Agent.UsageDirection.DefendDown);
            }
        }
    }
}
