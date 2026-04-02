using System;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Core patch: makes melee weapons pass through friendly agents behind the attacker.
    /// Clears native shield flags via FieldRefAccess, skips the original method,
    /// and queues an attack restore via ShieldWallBehaviour in case the native
    /// engine cancels the attack animation despite our ContinueChecking.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class MeleeHitFriendlyBypassPatch
    {
        [ThreadStatic]
        internal static bool ShouldBypassFriendlyHit;

        // FieldInfo handles for read-only AttackCollisionData shield flags.
        // Field names known from runtime discovery. Uses __makeref + SetValueDirect
        // for zero-boxing writes on the ref struct parameter.
        private static readonly FieldInfo _shieldBlockedField;
        private static readonly FieldInfo _shieldOnBackField;

        private static float _cachedAngle = -1f;
        private static float _cachedCosThreshold;

        static MeleeHitFriendlyBypassPatch()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _shieldBlockedField = typeof(AttackCollisionData).GetField("_attackBlockedWithShield", flags);
            _shieldOnBackField = typeof(AttackCollisionData).GetField("_collidedWithShieldOnBack", flags);

            SubModule.Log($"[PSW] Shield fields: blocked={_shieldBlockedField?.Name ?? "MISSING"}" +
                $" back={_shieldOnBackField?.Name ?? "MISSING"}");
        }

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
                ShouldBypassFriendlyHit = false;

                if (attacker == null || victim == null)
                    return true;

                if (attacker.Team != victim.Team)
                    return true;

                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled)
                    return true;

                bool debug = settings.EnableDebug;

                if (settings.AffectPlayerOnly && !attacker.IsPlayerControlled)
                {
                    if (debug) DebugMsg("SKIP: player-only, attacker is AI");
                    return true;
                }

                var wieldedWeapon = attacker.WieldedWeapon;
                if (wieldedWeapon.IsEmpty)
                    return true;

                var weaponClass = wieldedWeapon.Item?.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;

                if (!WeaponBypassConfig.TryGetSettings(weaponClass, settings, out var ws))
                {
                    if (debug) DebugMsg($"SKIP: {weaponClass} not configured");
                    return true;
                }

                if (!ws.Enabled)
                {
                    if (debug) DebugMsg($"SKIP: {weaponClass} disabled");
                    return true;
                }

                float startNorm = ws.BypassStart * 0.01f;
                float endNorm = ws.BypassEnd * 0.01f;
                float progress = collisionData.AttackProgress;

                if (progress < startNorm || progress > endNorm)
                {
                    if (debug) DebugMsg($"SKIP: progress {progress:F3} outside [{startNorm:F2}-{endNorm:F2}]");
                    return true;
                }

                if (!IsAgentBehind(attacker, victim, settings.BehindAngle, debug))
                    return true;

                // === BYPASS ===
                ShouldBypassFriendlyHit = true;

                // Clear shield flags via __makeref (zero-boxing, writes through ref param)
                ClearShieldFlags(ref collisionData);

                colReaction = MeleeCollisionReaction.ContinueChecking;

                // Queue attack restore in case native cancels despite ContinueChecking
                var behaviour = Mission.Current?.GetMissionBehavior<ShieldWallBehaviour>();
                if (behaviour != null)
                    behaviour.RequestAttackRestore(attacker,
                        attacker.GetCurrentAction(0),
                        attacker.GetCurrentActionProgress(0));

                if (debug)
                    DebugMsg($"BYPASS {weaponClass} progress:{(int)(progress * 100f)}%", Colors.Green);

                return false;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] MeleeHitCallback prefix error: {ex.Message}");
                return true;
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            ShouldBypassFriendlyHit = false;
        }

        private static void ClearShieldFlags(ref AttackCollisionData collisionData)
        {
            if (_shieldBlockedField == null) return;
            TypedReference tr = __makeref(collisionData);
            _shieldBlockedField.SetValueDirect(tr, false);
            _shieldOnBackField?.SetValueDirect(tr, false);
        }

        private static bool IsAgentBehind(Agent attacker, Agent victim, float angleDeg, bool debug)
        {
            if (Math.Abs(angleDeg - _cachedAngle) > 0.01f)
            {
                _cachedCosThreshold = (float)Math.Cos(angleDeg * Math.PI / 180.0);
                _cachedAngle = angleDeg;
            }

            float dx = victim.Position.x - attacker.Position.x;
            float dy = victim.Position.y - attacker.Position.y;
            float distSq = dx * dx + dy * dy;
            if (distSq < 0.001f) return false;

            float invDist = 1f / (float)Math.Sqrt(distSq);

            Vec3 look = attacker.LookDirection;
            float lookSq = look.x * look.x + look.y * look.y;
            if (lookSq < 0.001f) return false;

            float lookInv = 1f / (float)Math.Sqrt(lookSq);
            float dot = (look.x * lookInv) * (dx * invDist) + (look.y * lookInv) * (dy * invDist);
            bool behind = dot < _cachedCosThreshold;

            if (debug)
            {
                int deg = (int)(Math.Acos(Math.Max(-1f, Math.Min(1f, dot))) * 180.0 / Math.PI);
                DebugMsg($"Angle: {deg}deg (threshold:{angleDeg}deg) -> {(behind ? "BEHIND" : "IN FRONT")}");
            }

            return behind;
        }

        private static void DebugMsg(string msg, Color? color = null)
        {
            InformationManager.DisplayMessage(
                new InformationMessage($"[PSW] {msg}", color ?? Colors.Cyan));
        }
    }
}
