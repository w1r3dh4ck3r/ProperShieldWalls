using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    /// <summary>
    /// Owns the lifetime of CrowdState's static buffer. Agent.Index is recycled between
    /// missions, so a stale stamp would alias a fresh agent in the next battle.
    ///
    /// Mission.AfterStart() calls OnBehaviorInitialize() on behaviors in the list BEFORE
    /// OnMissionBehaviorInitialize() adds this behavior via AddMissionBehavior(). Since
    /// AddMissionBehavior() calls OnCreated() directly on the instance, OnCreated() is the
    /// hook that executes for late-added behaviors.
    /// </summary>
    internal sealed class CrowdStateBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        public override void OnCreated()
        {
            base.OnCreated();
            CrowdState.Reset();
            // Reset error-throttle counters so each battle gets a fresh 3-log budget for
            // each fault key. Without this, an error that storms and self-suppresses in
            // battle 1 stays permanently silent for the rest of the session, hiding a
            // real fault in battle 2. Resetting per mission restores visibility without
            // allowing in-battle storms.
            SubModule.ResetErrorThrottle();
            Diagnostics.Reset();

            if (Diagnostics.Enabled)
                Diagnostics.Write("[PSW] ---- mission start ----");
        }

        /// <summary>
        /// Attaches the AI attack-gate to every human agent as it spawns. Mission.SpawnAgent calls
        /// OnAgentBuild on each behavior, so reinforcement waves are covered too — not just the
        /// initial deployment.
        /// </summary>
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (agent != null && agent.IsHuman)
                agent.AddComponent(new AttackGateComponent(agent));
        }

        protected override void OnEndMission()
        {
            if (Diagnostics.Enabled)
                Diagnostics.WriteMissionReport();

            CrowdState.Reset();
            SubModule.ResetErrorThrottle();
            Diagnostics.Reset();
            base.OnEndMission();
        }
    }
}
