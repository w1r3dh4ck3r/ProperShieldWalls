using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    /// <summary>
    /// Owns the lifetime of CrowdState's static buffer. Agent.Index is recycled between
    /// missions, so a stale stamp would alias a fresh agent in the next battle.
    /// </summary>
    internal sealed class CrowdStateBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            CrowdState.Reset();
            // Reset error-throttle counters so each battle gets a fresh 3-log budget for
            // each fault key. Without this, an error that storms and self-suppresses in
            // battle 1 stays permanently silent for the rest of the session, hiding a
            // real fault in battle 2. Resetting per mission restores visibility without
            // allowing in-battle storms.
            SubModule.ResetErrorThrottle();
        }

        protected override void OnEndMission()
        {
            CrowdState.Reset();
            SubModule.ResetErrorThrottle();
            base.OnEndMission();
        }
    }
}
