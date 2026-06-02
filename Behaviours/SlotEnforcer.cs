using ProperShieldWalls.Models;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    // Registers and unregisters per-agent slot positions in OthismosState when
    // engagements lock and break. SlotLockPatch reads these positions each tick.
    internal class SlotEnforcer
    {
        internal void OnLocked(EngagementPair pair)
        {
            RegisterFormation(pair.FormationA);
            RegisterFormation(pair.FormationB);
        }

        internal void OnBreaking(EngagementPair pair)
        {
            UnregisterFormation(pair.FormationA);
            UnregisterFormation(pair.FormationB);
        }

        internal void OnAgentRemoved(Agent agent)
        {
            OthismosState.UnregisterSlot(agent);
        }

        private static void RegisterFormation(Formation f)
        {
            foreach (var unit in f.Arrangement.GetAllUnits())
                if (unit is Agent a && a.IsActive()) OthismosState.RegisterSlot(a);
        }

        private static void UnregisterFormation(Formation f)
        {
            foreach (var unit in f.Arrangement.GetAllUnits())
                if (unit is Agent a) OthismosState.UnregisterSlot(a);
        }
    }
}
