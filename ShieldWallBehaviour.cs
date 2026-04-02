using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls
{
    /// <summary>
    /// Mission behaviour that restores attacks cancelled by friendly shield collisions.
    ///
    /// The native engine cancels the attack animation when it detects a shield block,
    /// AFTER MeleeHitCallback returns. We can't prevent this from managed code.
    /// Instead, we save the attack action in the prefix and restore it on the next tick.
    /// The 1-frame bounce animation (~16ms) is imperceptible.
    /// </summary>
    internal class ShieldWallBehaviour : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private Agent _restoreAgent;
        private ActionIndexCache _restoreAction;
        private float _restoreProgress;
        private bool _pendingRestore;

        public void RequestAttackRestore(Agent agent, ActionIndexCache action, float progress)
        {
            _restoreAgent = agent;
            _restoreAction = action;
            _restoreProgress = progress;
            _pendingRestore = true;
        }

        public override void OnMissionTick(float dt)
        {
            if (!_pendingRestore) return;
            _pendingRestore = false;

            try
            {
                if (_restoreAgent == null || !_restoreAgent.IsActive())
                    return;

                var currentAction = _restoreAgent.GetCurrentAction(0);
                if (currentAction == _restoreAction)
                    return; // attack wasn't cancelled, nothing to do

                // Restore the attack action, advancing progress slightly to avoid rewind
                float newProgress = Math.Min(_restoreProgress + dt * 2f, 0.99f);
                _restoreAgent.SetActionChannel(0, _restoreAction,
                    startProgress: newProgress);
            }
            catch (Exception ex)
            {
                SubModule.Log($"[PSW] Attack restore error: {ex.Message}");
            }
            finally
            {
                _restoreAgent = null;
            }
        }
    }
}
