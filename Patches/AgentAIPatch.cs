using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Secondary slot enforcement: sets ShouldCatchUpWithFormation=true so the agent's AI
    // treats its slot as unreached, preventing free-roam movement.
    // All API names verified against installed DLL via strings extraction.
    [HarmonyPatch(typeof(HumanAIComponent), "ParallelUpdateFormationMovement")]
    internal static class AgentAIPatch
    {
        private static readonly MethodInfo _setShouldCatchUp;
        private static readonly PropertyInfo _agentProp;

        static AgentAIPatch()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _setShouldCatchUp = typeof(HumanAIComponent).GetMethod("SetShouldCatchUpWithFormation", flags);
            _agentProp = typeof(HumanAIComponent).GetProperty("Agent", flags)
                      ?? typeof(HumanAIComponent).BaseType?.GetProperty("Agent", flags);
            SubModule.Log(
                $"[PSW] AgentAIPatch init: " +
                $"catchUp={_setShouldCatchUp?.Name ?? "MISSING"} " +
                $"agentProp={_agentProp?.Name ?? "MISSING"}");
        }

        [HarmonyPostfix]
        public static void Postfix(HumanAIComponent __instance)
        {
            try
            {
                if (_setShouldCatchUp == null || _agentProp == null) return;
                var agent = _agentProp.GetValue(__instance) as Agent;
                if (agent == null || !agent.IsActive() || agent.Formation == null) return;
                if (!OthismosState.IsLocked(agent.Formation)) return;
                _setShouldCatchUp.Invoke(__instance, new object[] { true });
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] AgentAIPatch error: {ex.Message}");
            }
        }
    }
}
