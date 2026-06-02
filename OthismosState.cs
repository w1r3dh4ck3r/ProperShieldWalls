using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls
{
    internal static class OthismosState
    {
        private static readonly HashSet<Formation> _locked = new HashSet<Formation>();
        private static readonly Dictionary<Agent, Vec3> _slots = new Dictionary<Agent, Vec3>();

        internal static bool HasActiveEngagement => _locked.Count > 0;

        internal static void Lock(Formation f)   { if (f != null) _locked.Add(f); }
        internal static void Unlock(Formation f) { _locked.Remove(f); }

        internal static bool IsLocked(Formation f)    => f != null && _locked.Contains(f);
        internal static bool IsAgentLocked(Agent a)   => a?.Formation != null && IsLocked(a.Formation);

        internal static void RegisterSlot(Agent a)
        {
            if (a != null) _slots[a] = a.Position;
        }

        internal static void UnregisterSlot(Agent a)
        {
            if (a != null) _slots.Remove(a);
        }

        internal static bool TryGetSlot(Agent a, out Vec3 pos)
            => _slots.TryGetValue(a, out pos);

        // Moves a locked agent's slot by delta (used by PressureResolver for push/shove).
        internal static void NudgeSlot(Agent a, Vec2 delta)
        {
            if (_slots.TryGetValue(a, out Vec3 pos))
                _slots[a] = new Vec3(pos.x + delta.x, pos.y + delta.y, pos.z);
        }

        internal static void Clear()
        {
            _locked.Clear();
            _slots.Clear();
        }
    }
}
