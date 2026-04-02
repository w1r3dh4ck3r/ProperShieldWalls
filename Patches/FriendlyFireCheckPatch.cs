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
    ///
    /// Returns true for any weapon type that has bypass enabled in MCM settings.
    /// The actual bypass logic (angle, timing) is handled in MeleeHitFriendlyBypassPatch.
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

                bool shouldBypass = false;

                switch (weapon.WeaponClass)
                {
                    case WeaponClass.OneHandedPolearm:
                    case WeaponClass.TwoHandedPolearm:
                    case WeaponClass.LowGripPolearm:
                        shouldBypass = settings.PolearmEnabled;
                        break;

                    case WeaponClass.OneHandedSword:
                        shouldBypass = settings.OneHandedSwordEnabled;
                        break;

                    case WeaponClass.TwoHandedSword:
                        shouldBypass = settings.TwoHandedSwordEnabled;
                        break;

                    case WeaponClass.OneHandedAxe:
                        shouldBypass = settings.OneHandedAxeEnabled;
                        break;

                    case WeaponClass.TwoHandedAxe:
                        shouldBypass = settings.TwoHandedAxeEnabled;
                        break;

                    case WeaponClass.Mace:
                    case WeaponClass.TwoHandedMace:
                        shouldBypass = settings.MaceEnabled;
                        break;

                    case WeaponClass.Dagger:
                        shouldBypass = settings.DaggerEnabled;
                        break;
                }

                if (shouldBypass)
                {
                    __result = true;
                    return false; // skip original
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
