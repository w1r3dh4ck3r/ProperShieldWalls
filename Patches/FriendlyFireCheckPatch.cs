using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Belt-and-suspenders patch: tells the damage model that polearm weapons
    /// can ignore friendly fire checks. This catches any alternate code paths
    /// that check CanWeaponIgnoreFriendlyFireChecks before calling MeleeHitCallback.
    /// </summary>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CanWeaponIgnoreFriendlyFireChecks")]
    internal static class FriendlyFireCheckPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponComponentData weapon, ref bool __result)
        {
            try
            {
                if (weapon == null)
                    return true;

                if (weapon.WeaponClass == WeaponClass.OneHandedPolearm
                    || weapon.WeaponClass == WeaponClass.TwoHandedPolearm
                    || weapon.WeaponClass == WeaponClass.LowGripPolearm)
                {
                    __result = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"FriendlyFireCheck prefix error: {ex.Message}");
            }

            return true;
        }
    }
}
