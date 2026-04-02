using System;
using HarmonyLib;
using SandBox.GameComponents;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Safety net: prevent friendly shields from losing HP when our bypass is active.
    /// Returns 0 shield damage so the ally's shield stays intact.
    /// </summary>
    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CalculateShieldDamage")]
    internal static class ShieldDamagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float __result)
        {
            try
            {
                if (MeleeHitFriendlyBypassPatch.ShouldBypassFriendlyHit)
                {
                    __result = 0f;
                    return false; // skip original
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"ShieldDamage prefix error: {ex.Message}");
            }

            return true;
        }
    }
}
