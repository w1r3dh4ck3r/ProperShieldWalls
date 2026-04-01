using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Patches Mission.MeleeHitCallback to make weapons pass through friendly
    /// agents that are BEHIND the attacker. Sets colReaction to ContinueChecking
    /// so the native engine continues the weapon swing instead of stopping it.
    ///
    /// Performance: MeleeHitCallback only fires on actual physics hits (not every
    /// agent on the field), so this prefix runs infrequently. The cos threshold
    /// is cached and only recalculated when the setting changes. All math is
    /// branchless float ops — no allocations, no LINQ, no string work in the
    /// hot path.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class MeleeHitFriendlyBypassPatch
    {
        // Cached cos(angle) threshold — avoids trig per hit
        private static float _cachedAngle = -1f;
        private static float _cachedCosThreshold;

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
                // Cheapest checks first — bail early on the common case (enemy hits)
                if (attacker == null || victim == null)
                    return true;

                if (attacker.Team != victim.Team)
                    return true;

                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled)
                    return true;

                if (settings.AffectPlayerOnly && !attacker.IsPlayerControlled)
                    return true;

                // Weapon type + timing window check
                if (!IsWeaponBypassActive(attacker, settings, collisionData.AttackProgress))
                    return true;

                // Directional check — is the victim behind the attacker?
                if (!IsAgentBehind(attacker, victim, settings.BehindAngle))
                    return true;

                // Tell the native engine to continue the swing — weapon passes through.
                // We return true so the original MeleeHitCallback runs; it checks
                // "if (colReaction != ContinueChecking)" and skips all processing,
                // then returns normally — ensuring the ref parameter is properly
                // written back to the native engine.
                colReaction = MeleeCollisionReaction.ContinueChecking;

                if (settings.EnableDebug)
                {
                    int progressPct = (int)(collisionData.AttackProgress * 100f);
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"[PSW] Attack passed through friendly (progress: {progressPct}%)",
                            Colors.Cyan
                        )
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"MeleeHitCallback prefix error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Returns true if the victim is behind the attacker.
        /// Uses dot product — no trig in the hot path (cos threshold is cached).
        /// </summary>
        private static bool IsAgentBehind(Agent attacker, Agent victim, float behindAngleDegrees)
        {
            // Cache the cos threshold — only recalculate when angle setting changes
            if (Math.Abs(behindAngleDegrees - _cachedAngle) > 0.01f)
            {
                _cachedCosThreshold = (float)Math.Cos(behindAngleDegrees * Math.PI / 180.0);
                _cachedAngle = behindAngleDegrees;
            }

            // Direction from attacker to victim (horizontal plane only)
            float dx = victim.Position.x - attacker.Position.x;
            float dy = victim.Position.y - attacker.Position.y;
            float distSq = dx * dx + dy * dy;

            // Skip normalization if agents overlap (shouldn't happen but be safe)
            if (distSq < 0.001f)
                return false;

            float invDist = 1f / (float)Math.Sqrt(distSq);
            float toVictimX = dx * invDist;
            float toVictimY = dy * invDist;

            // Attacker look direction (horizontal)
            Vec3 lookDir = attacker.LookDirection;
            float lookDistSq = lookDir.x * lookDir.x + lookDir.y * lookDir.y;
            if (lookDistSq < 0.001f)
                return false;

            float lookInvDist = 1f / (float)Math.Sqrt(lookDistSq);
            float lookX = lookDir.x * lookInvDist;
            float lookY = lookDir.y * lookInvDist;

            // Dot product — no Vec3 allocation
            float dot = lookX * toVictimX + lookY * toVictimY;

            return dot < _cachedCosThreshold;
        }

        /// <summary>
        /// Checks if the weapon type is enabled AND the current attack progress
        /// falls within the per-weapon timing window.
        /// </summary>
        private static bool IsWeaponBypassActive(Agent agent, Settings settings, float attackProgress)
        {
            var wieldedWeapon = agent.WieldedWeapon;
            if (wieldedWeapon.IsEmpty)
                return false;

            var weaponClass = wieldedWeapon.Item?.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;

            bool enabled;
            float startPct, endPct;

            switch (weaponClass)
            {
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm:
                    enabled = settings.PolearmEnabled;
                    startPct = settings.PolearmBypassStart;
                    endPct = settings.PolearmBypassEnd;
                    break;

                case WeaponClass.OneHandedSword:
                    enabled = settings.OneHandedSwordEnabled;
                    startPct = settings.OneHandedSwordBypassStart;
                    endPct = settings.OneHandedSwordBypassEnd;
                    break;

                case WeaponClass.TwoHandedSword:
                    enabled = settings.TwoHandedSwordEnabled;
                    startPct = settings.TwoHandedSwordBypassStart;
                    endPct = settings.TwoHandedSwordBypassEnd;
                    break;

                case WeaponClass.OneHandedAxe:
                    enabled = settings.OneHandedAxeEnabled;
                    startPct = settings.OneHandedAxeBypassStart;
                    endPct = settings.OneHandedAxeBypassEnd;
                    break;

                case WeaponClass.TwoHandedAxe:
                    enabled = settings.TwoHandedAxeEnabled;
                    startPct = settings.TwoHandedAxeBypassStart;
                    endPct = settings.TwoHandedAxeBypassEnd;
                    break;

                case WeaponClass.Mace:
                case WeaponClass.TwoHandedMace:
                    enabled = settings.MaceEnabled;
                    startPct = settings.MaceBypassStart;
                    endPct = settings.MaceBypassEnd;
                    break;

                case WeaponClass.Dagger:
                    enabled = settings.DaggerEnabled;
                    startPct = settings.DaggerBypassStart;
                    endPct = settings.DaggerBypassEnd;
                    break;

                default:
                    return false;
            }

            if (!enabled)
                return false;

            // Convert 0-100 settings to 0.0-1.0 range for comparison
            float progressNormalized = attackProgress;
            return progressNormalized >= startPct * 0.01f
                && progressNormalized <= endPct * 0.01f;
        }
    }
}
