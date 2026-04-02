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
                if (attacker == null || victim == null)
                    return true;

                var settings = GlobalSettings<Settings>.Instance;
                bool debug = settings != null && settings.EnableDebug;

                // Log ALL melee hits when debug is on — this tells us if the patch fires at all
                if (debug)
                {
                    bool sameTeam = attacker.Team == victim.Team;
                    DebugMsg($"HIT: {(sameTeam ? "FRIENDLY" : "enemy")} | " +
                             $"colReaction={colReaction} | progress={collisionData.AttackProgress:F3}");
                }

                if (attacker.Team != victim.Team)
                    return true;

                // === From here: friendly hit ===

                if (settings == null || !settings.Enabled)
                {
                    if (debug) DebugMsg("SKIP: mod disabled");
                    return true;
                }

                if (settings.AffectPlayerOnly && !attacker.IsPlayerControlled)
                {
                    if (debug) DebugMsg("SKIP: player-only mode, attacker is AI");
                    return true;
                }

                // Weapon check (without timing — log weapon class first)
                var wieldedWeapon = attacker.WieldedWeapon;
                if (wieldedWeapon.IsEmpty)
                {
                    if (debug) DebugMsg("SKIP: no wielded weapon");
                    return true;
                }

                var weaponClass = wieldedWeapon.Item?.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;
                if (debug) DebugMsg($"Weapon: {weaponClass} | progress: {collisionData.AttackProgress:F3}");

                // Weapon type + timing window check
                if (!IsWeaponBypassActive(weaponClass, settings, collisionData.AttackProgress, debug))
                    return true;

                // Directional check — is the victim behind the attacker?
                if (!IsAgentBehind(attacker, victim, settings.BehindAngle, debug))
                    return true;

                // === BYPASS: weapon passes through friendly ===
                colReaction = MeleeCollisionReaction.ContinueChecking;

                if (debug)
                {
                    int progressPct = (int)(collisionData.AttackProgress * 100f);
                    DebugMsg($"BYPASS! {weaponClass} passed through friendly (progress: {progressPct}%)",
                             Colors.Green);
                }

                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"MeleeHitCallback prefix error: {ex.Message}");
                return true;
            }
        }

        private static bool IsAgentBehind(Agent attacker, Agent victim, float behindAngleDegrees, bool debug)
        {
            if (Math.Abs(behindAngleDegrees - _cachedAngle) > 0.01f)
            {
                _cachedCosThreshold = (float)Math.Cos(behindAngleDegrees * Math.PI / 180.0);
                _cachedAngle = behindAngleDegrees;
            }

            float dx = victim.Position.x - attacker.Position.x;
            float dy = victim.Position.y - attacker.Position.y;
            float distSq = dx * dx + dy * dy;

            if (distSq < 0.001f)
            {
                if (debug) DebugMsg("SKIP: agents overlapping");
                return false;
            }

            float invDist = 1f / (float)Math.Sqrt(distSq);
            float toVictimX = dx * invDist;
            float toVictimY = dy * invDist;

            Vec3 lookDir = attacker.LookDirection;
            float lookDistSq = lookDir.x * lookDir.x + lookDir.y * lookDir.y;
            if (lookDistSq < 0.001f)
            {
                if (debug) DebugMsg("SKIP: zero look direction");
                return false;
            }

            float lookInvDist = 1f / (float)Math.Sqrt(lookDistSq);
            float lookX = lookDir.x * lookInvDist;
            float lookY = lookDir.y * lookInvDist;

            float dot = lookX * toVictimX + lookY * toVictimY;
            bool isBehind = dot < _cachedCosThreshold;

            if (debug)
            {
                int angleDeg = (int)(Math.Acos(Math.Max(-1f, Math.Min(1f, dot))) * 180.0 / Math.PI);
                DebugMsg($"Angle: dot={dot:F2} -> {angleDeg}deg " +
                         $"(threshold={behindAngleDegrees}deg, cos={_cachedCosThreshold:F2}) " +
                         $"-> {(isBehind ? "BEHIND" : "IN FRONT")}");
            }

            return isBehind;
        }

        private static bool IsWeaponBypassActive(WeaponClass weaponClass, Settings settings,
            float attackProgress, bool debug)
        {
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
                    if (debug) DebugMsg($"SKIP: weapon class {weaponClass} not configured");
                    return false;
            }

            if (!enabled)
            {
                if (debug) DebugMsg($"SKIP: {weaponClass} bypass disabled in settings");
                return false;
            }

            float startNorm = startPct * 0.01f;
            float endNorm = endPct * 0.01f;
            bool inWindow = attackProgress >= startNorm && attackProgress <= endNorm;

            if (debug && !inWindow)
            {
                DebugMsg($"SKIP: progress {attackProgress:F3} outside window [{startNorm:F2}-{endNorm:F2}]");
            }

            return inWindow;
        }

        private static void DebugMsg(string msg, Color? color = null)
        {
            InformationManager.DisplayMessage(
                new InformationMessage($"[PSW] {msg}", color ?? Colors.Cyan));
        }
    }
}
