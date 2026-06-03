using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    internal class OthismosBehaviour : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private Settings           _settings;
        private EngagementDetector _detector;
        private SlotEnforcer       _slotEnforcer;
        private LockStateManager   _lockManager;
        private StabForcer         _stabForcer;
        private PressureResolver   _pressureResolver;

        public override void AfterStart()
        {
            OthismosState.Clear();
            _settings         = GlobalSettings<Settings>.Instance ?? new Settings();
            _slotEnforcer     = new SlotEnforcer();
            _detector         = new EngagementDetector(_settings);
            _lockManager      = new LockStateManager(_settings, _slotEnforcer);
            _stabForcer       = new StabForcer();
            _pressureResolver = new PressureResolver();
        }

        public override void OnMissionTick(float dt)
        {
            if (_settings == null || !_settings.Enabled) return;

            var candidates = _detector.FindCandidatePairs(Mission.Current);
            _lockManager.Tick(dt, candidates);
            _stabForcer.Tick(_lockManager.Pairs);
            _pressureResolver.Tick(_lockManager.Pairs, dt);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState, KillingBlow blow)
        {
            _slotEnforcer.OnAgentRemoved(affectedAgent);
            _lockManager.OnAgentRemoved(affectedAgent);
        }
    }
}
