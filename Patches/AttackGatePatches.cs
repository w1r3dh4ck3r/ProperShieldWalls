using System;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace ProperShieldWalls.Patches
{
    internal static class AttackGate
    {
        /// <summary>
        /// If the agent is currently flagged as crowded and is swinging horizontally with a
        /// weapon that can swing, rewrite the swing into an overhead. Main thread only.
        /// </summary>
        internal static void Apply(Agent agent)
        {
            try
            {
                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled || !settings.CrampedAttackGating) return;

                if (agent == null || !agent.IsActive()) return;

                // A kick in flight (AIKickNBashFork) always wins — do not fight it.
                if ((agent.EventControlFlags & Agent.EventControlFlag.Kick) != 0) return;

                var mission = Mission.Current;
                if (mission == null) return;

                if (!CrowdState.IsCrowded(agent.Index, mission.CurrentTime)) return;

                uint flags = (uint)agent.MovementFlags;
                uint next = AttackRemap.Decide(flags, CanSwing(agent), isCrowded: true);
                if (next != flags) agent.MovementFlags = (Agent.MovementControlFlag)next;
            }
            catch (Exception ex)
            {
                SubModule.Log("[PSW] AttackGate error: " + ex.Message);
            }
        }

        /// <summary>
        /// A thrust-only weapon (pike) must never be remapped to an overhead — that would be
        /// the dead input the design explicitly rejects. All reads here are managed, no interop.
        /// </summary>
        internal static bool CanSwing(Agent agent)
        {
            WeaponComponentData weapon = agent.WieldedWeapon.CurrentUsageItem;   // null when unarmed
            if (weapon == null) return false;
            if (!weapon.IsMeleeWeapon) return false;
            return weapon.SwingDamageType != DamageTypes.Invalid && weapon.SwingSpeed > 0;
        }
    }

    /// <summary>
    /// AI agents. Runs FIRST (high priority) so AIKickNBashFork's postfix, which runs after,
    /// can overwrite our remap with its kick.
    /// </summary>
    [HarmonyPatch]
    internal static class AiAttackGatePatch
    {
        // Agent.OnAIInputSet is internal; the typeof/string attribute form will not bind it.
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Agent), "OnAIInputSet");
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        public static void Postfix(Agent __instance)
        {
            AttackGate.Apply(__instance);
        }
    }

    /// <summary>
    /// The player. Runs LAST (low priority) so we get the final write to MovementFlags,
    /// after FluidCombatNextNext's postfix has OR'd in its own direction.
    /// </summary>
    [HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
    internal static class PlayerAttackGatePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix()
        {
            var mission = Mission.Current;
            if (mission == null) return;
            AttackGate.Apply(mission.MainAgent);
        }
    }
}
