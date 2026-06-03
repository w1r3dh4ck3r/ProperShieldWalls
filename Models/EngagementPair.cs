using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Models
{
    internal enum EngagementState { Idle, PreLock, Locked, Breaking }

    internal sealed class EngagementPair
    {
        internal Formation FormationA { get; }
        internal Formation FormationB { get; }
        internal EngagementState State { get; set; }
        internal float StateTimer      { get; set; }
        internal int InitialFrontRankA { get; set; }
        internal int InitialFrontRankB { get; set; }

        internal EngagementPair(Formation a, Formation b)
        {
            FormationA = a;
            FormationB = b;
            State = EngagementState.Idle;
        }

        internal bool Involves(Formation f) => FormationA == f || FormationB == f;
        internal Formation Opponent(Formation f) => FormationA == f ? FormationB : FormationA;
    }
}
