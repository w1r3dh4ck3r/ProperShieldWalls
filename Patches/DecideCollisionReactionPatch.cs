using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Safety net: if DecideWeaponCollisionReaction overrides our ContinueChecking back to
    // Bounced, restore it. Fires only after MeleeHitCallbackPatch has already set Active.
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "DecideWeaponCollisionReaction")]
    internal static class DecideCollisionReactionPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref MeleeCollisionReaction colReaction)
        {
            try
            {
                if (!MeleeHitCallbackPatch.Active) return;
                if (colReaction == MeleeCollisionReaction.Bounced)
                    colReaction = MeleeCollisionReaction.ContinueChecking;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] DecideCollisionReaction error: {ex.Message}");
            }
        }
    }
}
