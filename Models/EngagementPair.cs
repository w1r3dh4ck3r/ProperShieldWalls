using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Models
{
    internal enum EngagementState { Idle, PreLock, Locked, Breaking }

    internal sealed class EngagementPair
    {
        internal Formation FormationA { get; }
        internal Formation FormationB { get; }
        internal EngagementState State { get; set; }
        internal float StateTimer     { get; set; }
        internal float StaminaA       { get; set; }
        internal float StaminaB       { get; set; }

        internal EngagementPair(Formation a, Formation b)
        {
            FormationA = a;
            FormationB = b;
            State = EngagementState.Idle;
            StaminaA = 100f;
            StaminaB = 100f;
        }

        internal bool Involves(Formation f) => FormationA == f || FormationB == f;
        internal Formation Opponent(Formation f) => FormationA == f ? FormationB : FormationA;
        internal bool StaminaExhausted => StaminaA <= 0f || StaminaB <= 0f;
    }
}
