using System;
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
        /// Decides the remap for a crowded agent swinging horizontally with a weapon that can swing.
        /// Returns true and overwrites <paramref name="flags"/> only when a rewrite is warranted.
        /// Main thread only. No allocation, no interop beyond IsActive().
        /// </summary>
        private static bool TryRemap(Agent agent, uint eventFlags, ref uint flags)
        {
            var settings = GlobalSettings<Settings>.Instance;
            if (settings == null || !settings.Enabled || !settings.CrampedAttackGating) return false;

            if (agent == null || !agent.IsActive()) return false;

            // A kick in flight always wins — do not fight it.
            if ((eventFlags & (uint)Agent.EventControlFlag.Kick) != 0) return false;

            var mission = Mission.Current;
            if (mission == null) return false;

            if (!CrowdState.IsCrowded(agent.Index, mission.CurrentTime)) return false;

            uint next = AttackRemap.Decide(flags, CanSwing(agent), isCrowded: true);
            if (next == flags) return false;

            flags = next;
            return true;
        }

        /// <summary>
        /// AI path. Mutates the pending movement flag the engine reads back on this tick, rather than
        /// round-tripping through Agent.MovementFlags. Called from AttackGateComponent.OnAIInputSet.
        /// </summary>
        internal static void ApplyToInput(Agent agent, uint eventFlags, ref uint movementFlags)
        {
            try
            {
                TryRemap(agent, eventFlags, ref movementFlags);
            }
            catch (Exception ex)
            {
                // Key = site + exception type, not ex.Message: this runs at AI-decision-tick rate for every
                // active AI agent, so a repeating fault must collapse into one throttled bucket instead of
                // logging (or allocating a dictionary entry) once per agent per tick forever.
                SubModule.LogErrorThrottled(
                    "AttackGate.ApplyToInput:" + ex.GetType().Name,
                    "[PSW] AttackGate error: " + ex.Message);
            }
        }

        /// <summary>Player path: no AI input hook exists, so write the property directly.</summary>
        internal static void Apply(Agent agent)
        {
            try
            {
                if (agent == null) return;
                uint flags = (uint)agent.MovementFlags;
                if (TryRemap(agent, (uint)agent.EventControlFlags, ref flags))
                    agent.MovementFlags = (Agent.MovementControlFlag)flags;
            }
            catch (Exception ex)
            {
                SubModule.LogErrorThrottled(
                    "AttackGate.Apply:" + ex.GetType().Name,
                    "[PSW] AttackGate error: " + ex.Message);
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
    /// The player. Runs LAST (low priority) so we get the final write to MovementFlags,
    /// after FluidCombatNextNext's postfix has OR'd in its own direction.
    ///
    /// NOTE: there is deliberately no patch on Agent.OnAIInputSet. That method is an [MBCallback]
    /// native engine callback; Harmony-patching it folds every character into a spike (the
    /// "meat bullet" bug), even when the patch body does nothing. AI agents are handled by
    /// AttackGateComponent instead, via the engine's own component fan-out.
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
