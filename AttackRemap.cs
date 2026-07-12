namespace ProperShieldWalls
{
    /// <summary>
    /// Decides whether a crowded agent's horizontal swing becomes an overhead.
    /// Values mirror Agent.MovementControlFlag (v1.4.6). Kept free of TaleWorlds
    /// types so the test project can compile it; callers cast at the boundary.
    /// </summary>
    internal static class AttackRemap
    {
        internal const uint AttackLeft  = 0x40u;
        internal const uint AttackRight = 0x80u;
        internal const uint AttackUp    = 0x100u;
        internal const uint AttackDown  = 0x200u;
        internal const uint AttackMask  = 0x3C0u;

        internal static uint Decide(uint flags, bool canSwing, bool isCrowded)
        {
            if (!isCrowded) return flags;
            if (!canSwing) return flags;

            // Only a horizontal swing is remapped. Overhead and thrust are already legal
            // in a press, and a weapon that cannot swing must keep whatever it was doing.
            if ((flags & (AttackLeft | AttackRight)) == 0) return flags;

            return (flags & ~AttackMask) | AttackUp;
        }
    }
}
