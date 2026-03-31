using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// Patches Mission.MeleeHitCallback to skip melee collision entirely when:
    ///   1. Attacker and victim are on the same team (friendly)
    ///   2. The weapon is a polearm/spear
    ///   3. The attack is a thrust (not a swing)
    ///
    /// Returning false from the Prefix prevents both damage AND the physical
    /// collision reaction (bounce/stagger), allowing the spear to pass through
    /// friendly agents in shield wall formations.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class MeleeHitFriendlyBypassPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim)
        {
            try
            {
                if (attacker == null || victim == null)
                    return true;

                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled)
                    return true;

                // Only skip hits on friendly agents
                if (!attacker.IsFriendOf(victim))
                    return true;

                // Only skip thrust attacks (not swings)
                if (collisionData.StrikeType != (int)StrikeType.Thrust)
                    return true;

                // Check if the weapon is a polearm/spear type
                if (!IsPolearmWeapon(attacker))
                    return true;

                // If player-only mode, check if attacker is player-controlled
                if (settings.AffectPlayerOnly && !attacker.IsPlayerControlled)
                    return true;

                if (settings.EnableDebug)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            "[PSW] Polearm thrust passed through friendly agent",
                            Colors.Cyan
                        )
                    );
                }

                // Skip the hit entirely — weapon passes through the friendly agent
                return false;
            }
            catch (Exception ex)
            {
                SubModule.Log($"MeleeHitCallback prefix error: {ex.Message}");
                return true;
            }
        }

        private static bool IsPolearmWeapon(Agent agent)
        {
            var weaponEquipmentIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            if (weaponEquipmentIndex == EquipmentIndex.None)
                return false;

            var equipment = agent.Equipment[weaponEquipmentIndex];
            if (equipment.IsEmpty)
                return false;

            var weaponClass = equipment.Item?.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;

            return weaponClass == WeaponClass.OneHandedPolearm
                || weaponClass == WeaponClass.TwoHandedPolearm
                || weaponClass == WeaponClass.LowGripPolearm;
        }
    }
}
