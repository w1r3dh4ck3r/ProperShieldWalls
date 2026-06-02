using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    // Primary slot enforcement: returns each locked agent's current slot position as their
    // formation slot, preventing the engine from issuing a chase order.
    // Confirmed approach from RBM Frontline.cs:240 and validated by POC.
    [HarmonyPatch(typeof(Formation), "GetOrderPositionOfUnit")]
    internal static class SlotLockPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref Agent unit, ref WorldPosition __result)
        {
            if (unit == null || !unit.IsActive() || !unit.IsAIControlled) return true;
            if (!OthismosState.IsAgentLocked(unit)) return true;

            Vec3 slotPos;
            if (!OthismosState.TryGetSlot(unit, out slotPos))
                slotPos = unit.Position;

            WorldPosition wp = unit.GetWorldPosition();
            wp.SetVec2(new TaleWorlds.Library.Vec2(slotPos.x, slotPos.y));
            unit.SetTargetPosition(new TaleWorlds.Library.Vec2(slotPos.x, slotPos.y));
            __result = wp;
            return false;
        }
    }
}
