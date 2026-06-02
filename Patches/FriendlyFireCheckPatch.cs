using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Allows the engine to route a weapon hit into MeleeHitCallback when an othismos
    // engagement is active. Without this, native code blocks friendly collisions before
    // MeleeHitCallbackPatch ever fires.
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CanWeaponIgnoreFriendlyFireChecks")]
    internal static class FriendlyFireCheckPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(WeaponComponentData weapon, ref bool __result)
        {
            try
            {
                if (weapon != null && OthismosState.HasActiveEngagement)
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
