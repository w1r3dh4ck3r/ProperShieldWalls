using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Patches Mission.MeleeHitCallback to skip melee collision against friendly
    /// agents that are BEHIND the attacker. This prevents weapons from getting
    /// caught on allies' shields and bodies during wind-up in tight formations.
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
            Agent victim)
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

                // Weapon type check (switch on int, no allocation)
                if (!IsWeaponTypeEnabled(attacker, settings))
                    return true;

                // Directional check — is the victim behind the attacker?
                if (!IsAgentBehind(attacker, victim, settings.BehindAngle))
                    return true;

                if (settings.EnableDebug)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            "[PSW] Attack passed through friendly behind",
                            Colors.Cyan
                        )
                    );
                }

                return false;
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

        private static bool IsWeaponTypeEnabled(Agent agent, Settings settings)
        {
            var wieldedWeapon = agent.WieldedWeapon;
            if (wieldedWeapon.IsEmpty)
                return false;

            var weaponClass = wieldedWeapon.Item?.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;

            switch (weaponClass)
            {
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm:
                    return settings.PolearmEnabled;

                case WeaponClass.OneHandedSword:
                    return settings.OneHandedSwordEnabled;

                case WeaponClass.TwoHandedSword:
                    return settings.TwoHandedSwordEnabled;

                case WeaponClass.OneHandedAxe:
                    return settings.OneHandedAxeEnabled;

                case WeaponClass.TwoHandedAxe:
                    return settings.TwoHandedAxeEnabled;

                case WeaponClass.Mace:
                case WeaponClass.TwoHandedMace:
                    return settings.MaceEnabled;

                case WeaponClass.Dagger:
                    return settings.DaggerEnabled;

                default:
                    return false;
            }
        }
    }
}
