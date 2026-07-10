using System;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Cramped attack gating, AI-only.
    ///
    /// The player is deliberately exempt: he keeps full manual control of thrust/overhead even when
    /// packed among friendlies. There is no patch here at all — the AI path rides
    /// AttackGateComponent, and the former MissionMainAgentController.ControlTick postfix that
    /// remapped the player's swing has been removed.
    ///
    /// The player still gets wind-up transparency: that lives on Mission.MeleeHitCallback, which is
    /// agent-agnostic and keyed on friend-of-attacker. The two features are independent.
    /// </summary>
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

            // The player is never remapped. He keeps manual control of attack direction even when
            // packed among friendlies. OnAIInputSet is not expected to fire for the main agent, but
            // the exemption is asserted here rather than assumed.
            if (agent.IsMainAgent) return false;

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
                // A successful remap is otherwise completely silent, so a no-op gate looks
                // identical to a working one. Count them; CrowdStateBehavior reports the total.
                if (TryRemap(agent, eventFlags, ref movementFlags))
                    Diagnostics.RemapCount++;
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
}
