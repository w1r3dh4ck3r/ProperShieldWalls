using System;
using HarmonyLib;
using SandBox.GameComponents;

namespace ProperShieldWalls.Patches
{
    // Safety net: zero out shield damage for friendly hits during othismos pass-throughs
    // so allied shields don't lose HP from formation-mates stabbing through them.
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CalculateShieldDamage")]
    internal static class ShieldDamagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float __result)
        {
            try
            {
                if (MeleeHitCallbackPatch.Active)
                {
                    __result = 0f;
                    return false;
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] ShieldDamage error: {ex.Message}");
            }
            return true;
        }
    }
}
