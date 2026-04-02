using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Tells the damage model that melee weapons can ignore friendly fire checks.
    /// Without this, the native engine blocks friendly melee collisions BEFORE
    /// MeleeHitCallback is ever called — our main patch would never fire.
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

                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled)
                    return true;

                if (WeaponBypassConfig.IsWeaponEnabled(weapon.WeaponClass, settings))
                {
                    __result = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] FriendlyFireCheck error: {ex.Message}");
            }

            return true;
        }
    }
}
