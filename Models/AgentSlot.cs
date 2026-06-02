using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Models
{
    // Lightweight per-agent record used by PressureResolver for rank-weighting.
    // Actual slot position is stored in OthismosState._slots and enforced by SlotLockPatch.
    internal sealed class AgentSlot
    {
        internal Agent Agent     { get; }
        internal int   RankIndex { get; }

        internal AgentSlot(Agent agent)
        {
            Agent     = agent;
            RankIndex = ((IFormationUnit)agent).FormationRankIndex;
        }
    }
}
