using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Secondary slot enforcement: sets ShouldCatchUpWithFormation=true so the agent's AI
    // treats its slot as unreached, preventing free-roam movement.
    // Agent is a protected field on AgentComponent; ShouldCatchUpWithFormation is a non-public property.
    [HarmonyPatch(typeof(HumanAIComponent), "ParallelUpdateFormationMovement")]
    internal static class AgentAIPatch
    {
        private static readonly FieldInfo    _agentField;
        private static readonly PropertyInfo _catchUpProp;

        static AgentAIPatch()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _agentField  = typeof(HumanAIComponent).BaseType?.GetField("Agent", flags);
            _catchUpProp = typeof(HumanAIComponent).GetProperty("ShouldCatchUpWithFormation", flags);
            SubModule.Log(
                $"[PSW] AgentAIPatch init: " +
                $"agentField={_agentField?.Name ?? "MISSING"} " +
                $"catchUp={_catchUpProp?.Name ?? "MISSING"}");
        }

        [HarmonyPostfix]
        public static void Postfix(HumanAIComponent __instance)
        {
            try
            {
                if (_agentField == null || _catchUpProp == null) return;
                var agent = _agentField.GetValue(__instance) as Agent;
                if (agent == null || !agent.IsActive() || agent.Formation == null) return;
                if (!OthismosState.IsLocked(agent.Formation)) return;
                _catchUpProp.SetValue(__instance, true);
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] AgentAIPatch error: {ex.Message}");
            }
        }
    }
}
