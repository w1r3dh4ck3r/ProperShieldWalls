using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// A friendly hit landing during an attack's wind-up costs nothing: no friendly-fire stun,
    /// no Bounced weapon reaction, no shield clang, no blow. The sweep continues past the ally.
    ///
    /// Mechanism (verified, Mission.cs:5297-5397, v1.4.6): MeleeHitCallback wraps its entire
    /// penalty block in `if (colReaction != MeleeCollisionReaction.ContinueChecking)`. Vanilla
    /// itself uses this to let kicks and bashes (IsAlternativeAttack) pass through friendlies.
    /// Setting ContinueChecking and returning true makes the original skip that block for us.
    ///
    /// DO NOT return false. That would suppress other mods' prefixes on this same method —
    /// RealisticCombatSounds and XorberaxLegacy both reference it.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class WindupTransparencyPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            ref MeleeCollisionReaction colReaction)
        {
            try
            {
                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled || !settings.WindupTransparency) return true;

                if (attacker == null || victim == null) return true;   // world hit
                if (!collisionData.IsColliderAgent) return true;
                if (ReferenceEquals(attacker, victim)) return true;    // self-hit
                if (!victim.IsHuman) return true;                      // mounts keep vanilla behaviour

                // Team is a free managed field; IsFriendOf is a native call. Short-circuit on the
                // common case before paying for the interop.
                if (attacker.Team != victim.Team && !attacker.IsFriendOf(victim)) return true;

                bool windup =
                    (collisionData.CollisionHitResultFlags & CombatHitResultFlags.HitWithStartOfTheAnimation) != 0
                    || collisionData.AttackProgress < settings.WindupThreshold;

                if (settings.DiagnosticLogging)
                {
                    SubModule.Log(string.Format(
                        "[PSW] friendly hit strike={0} flags={1} progress={2:0.000} windup={3}",
                        collisionData.StrikeType, collisionData.CollisionHitResultFlags,
                        collisionData.AttackProgress, windup));
                }

                if (!windup) return true;   // live strike arc: an ally in front still stops the blade

                colReaction = MeleeCollisionReaction.ContinueChecking;
                CrowdState.Stamp(attacker.Index, Mission.Current.CurrentTime, settings.CrowdedDuration);
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log("[PSW] WindupTransparencyPatch error: " + ex.Message);
                return true;
            }
        }
    }
}
