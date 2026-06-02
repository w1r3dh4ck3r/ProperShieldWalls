using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Safety net: skip blow registration for friendly hits during othismos pass-throughs
    // so no residual damage reaches the allied agent.
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    internal static class RegisterBlowPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Agent attacker, Agent victim)
        {
            try
            {
                if (!MeleeHitCallbackPatch.Active) return true;
                if (attacker != null && victim != null && attacker.Team == victim.Team)
                    return false;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] RegisterBlow error: {ex.Message}");
            }
            return true;
        }
    }
}
