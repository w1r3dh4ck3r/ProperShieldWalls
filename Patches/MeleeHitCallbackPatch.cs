using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Intercepts friendly-hit events for locked agents: clears shield-blocked flags and
    // sets ContinueChecking so weapons pass through allied bodies in the wall.
    // Sets MeleeHitCallbackPatch.Active so downstream patches (Collision, RegisterBlow,
    // ShieldDamage) know this is an othismos-sanctioned pass-through.
    // Field names _attackBlockedWithShield / _collidedWithShieldOnBack verified in DLL.
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class MeleeHitCallbackPatch
    {
        [ThreadStatic]
        internal static bool Active;

        private static readonly FieldInfo _shieldBlockedField;
        private static readonly FieldInfo _shieldOnBackField;

        static MeleeHitCallbackPatch()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _shieldBlockedField = typeof(AttackCollisionData).GetField("_attackBlockedWithShield", flags);
            _shieldOnBackField  = typeof(AttackCollisionData).GetField("_collidedWithShieldOnBack", flags);
            SubModule.Log(
                $"[PSW] MeleeHitCallbackPatch init: " +
                $"shieldBlocked={_shieldBlockedField?.Name ?? "MISSING"} " +
                $"shieldBack={_shieldOnBackField?.Name ?? "MISSING"}");
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            ref MeleeCollisionReaction colReaction)
        {
            Active = false;
            try
            {
                if (attacker == null || victim == null)       return true;
                if (attacker.Team != victim.Team)             return true;
                if (!OthismosState.IsAgentLocked(attacker))  return true;

                Active = true;
                ClearShieldFlags(ref collisionData);
                colReaction = MeleeCollisionReaction.ContinueChecking;
                return false;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] MeleeHitCallback error: {ex.Message}");
                return true;
            }
        }

        [HarmonyPostfix]
        public static void Postfix() { Active = false; }

        private static void ClearShieldFlags(ref AttackCollisionData cd)
        {
            if (_shieldBlockedField == null) return;
            TypedReference tr = __makeref(cd);
            _shieldBlockedField.SetValueDirect(tr, false);
            _shieldOnBackField?.SetValueDirect(tr, false);
        }
    }
}
