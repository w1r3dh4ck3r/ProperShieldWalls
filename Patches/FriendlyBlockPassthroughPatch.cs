using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// An ally's shield must never halt your swing.
    ///
    /// Why this exists (measured, 2026-07-10). WindupTransparencyPatch was firing correctly — the
    /// battle log shows `friend=1 prog=0.000 collider=1 -> BYPASS` — and yet the attack still
    /// stopped dead on a friendly's shield. The reason is that Mission.MeleeHitCallback is not the
    /// only place a friendly contact halts an attack. When native classifies the collision as a
    /// block or parry (`result=Blocked` / `result=Parried`, `blockedShield=1` in the log), it takes
    /// an entirely separate callback, Mission.GetDefendCollisionResults, which delegates straight to
    /// the static helper patched below. That helper computes `attackerStunPeriod` (line 240) and
    /// `crushedThrough` — and THAT is what freezes the attacker's arm mid-swing. Suppressing
    /// MeleeHitCallback's penalty block never touched it.
    ///
    /// Target choice: the plain static MissionCombatMechanicsHelper.GetDefendCollisionResults, NOT
    /// the Mission wrapper. The wrapper carries [MBCallback] — a native engine callback, the same
    /// class of method whose patching folded every character into a spike (Agent.OnAIInputSet). The
    /// helper is ordinary managed code. UnblockableThrust (Nexus, shipped) patches this exact static
    /// for the same reason, which is where the technique is borrowed from. Note the helper's
    /// signature carries an extra `ref bool chamber` that the Mission wrapper does not.
    ///
    /// Priority.Last so we get the final write: postfixes sort DESCENDING by priority, and three
    /// other enabled mods (UnblockableThrust, RealisticCombatAdjustments, StaminaSystemFork) also
    /// postfix this method.
    ///
    /// Only `crushedThrough` is set, mirroring the precedent. `attackerStunPeriod` is deliberately
    /// left alone for now: if the next battle log still shows a halt, residual stun is the next
    /// suspect and the one-line change is obvious. Don't pre-emptively zero it.
    /// </summary>
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "GetDefendCollisionResults")]
    internal static class FriendlyBlockPassthroughPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(
            Agent attackerAgent,
            Agent defenderAgent,
            CombatCollisionResult collisionResult,
            ref bool crushedThrough)
        {
            try
            {
                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled || !settings.FriendlyBlockPassthrough) return;

                if (crushedThrough) return;                       // already passing through
                if (attackerAgent == null || defenderAgent == null) return;
                if (ReferenceEquals(attackerAgent, defenderAgent)) return;
                if (!attackerAgent.IsFriendOf(defenderAgent)) return;

                // Only a block or a parry stops the attacker here. Anything else is not our business.
                if (collisionResult != CombatCollisionResult.Blocked &&
                    collisionResult != CombatCollisionResult.Parried &&
                    collisionResult != CombatCollisionResult.ChamberBlocked) return;

                crushedThrough = true;
                Diagnostics.RecordFriendlyBlockNeutralised();
            }
            catch (Exception ex)
            {
                // Runs on every blocked melee collision: a repeating fault must collapse into one
                // throttled bucket rather than log once per collision forever.
                SubModule.LogErrorThrottled(
                    "FriendlyBlockPassthroughPatch:" + ex.GetType().Name,
                    "[PSW] FriendlyBlockPassthroughPatch error: " + ex.Message);
            }
        }
    }
}
