// Holds locked agents in their current world position so the engine attacks in place.
// Confirmed pattern from RBM Frontline.cs:240.
// DELETE THIS FILE after POC; promote to OthismosBehaviour for MVP.
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    [HarmonyPatch(typeof(Formation), "GetOrderPositionOfUnit")]
    internal static class SlotLockPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Formation __instance, ref Agent unit, ref WorldPosition __result)
        {
            if (unit == null || !unit.IsActive() || !unit.IsAIControlled) return true;
            if (!OthismosTestBehaviour.LockedAgents.Contains(unit)) return true;

            // Return agent's current position as its "slot". Engine then attacks in place
            // rather than chasing — spatial constraint produces thrusts naturally.
            __result = unit.GetWorldPosition();
            unit.SetTargetPosition(unit.GetWorldPosition().AsVec2);
            return false;
        }
    }
}
