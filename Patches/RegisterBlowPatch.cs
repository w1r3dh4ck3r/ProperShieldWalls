using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Safety net: skip blow registration entirely for friendly shield hits
    /// that our main patch identified as a bypass. Prevents any residual
    /// damage from being applied to the friendly agent or their shield.
    ///
    /// In the normal flow, DecideCollisionReactionPatch changes Bounced to
    /// ContinueChecking, which should prevent RegisterBlow from being called.
    /// This prefix is a defensive fallback for edge cases.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    internal static class RegisterBlowPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Agent attacker, Agent victim)
        {
            try
            {
                if (!MeleeHitFriendlyBypassPatch.ShouldBypassFriendlyHit)
                    return true;

                // Double-check it's actually a friendly hit
                if (attacker != null && victim != null && attacker.Team == victim.Team)
                    return false; // skip blow registration

                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"RegisterBlow prefix error: {ex.Message}");
                return true;
            }
        }
    }
}
