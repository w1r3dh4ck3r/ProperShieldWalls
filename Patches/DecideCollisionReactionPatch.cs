using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Postfix on MissionCombatMechanicsHelper.DecideWeaponCollisionReaction.
    ///
    /// The method is void and outputs via ref colReaction parameter (not a return value).
    /// When AttackBlockedWithShield is true, it sets colReaction = Bounced.
    /// We override that to ContinueChecking for friendly hits.
    /// </summary>
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "DecideWeaponCollisionReaction")]
    internal static class DecideCollisionReactionPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref MeleeCollisionReaction colReaction)
        {
            try
            {
                if (!MeleeHitFriendlyBypassPatch.ShouldBypassFriendlyHit)
                    return;

                if (colReaction == MeleeCollisionReaction.Bounced)
                    colReaction = MeleeCollisionReaction.ContinueChecking;
            }
            catch (Exception ex)
            {
                SubModule.Log($"DecideCollisionReaction postfix error: {ex.Message}");
            }
        }
    }
}
