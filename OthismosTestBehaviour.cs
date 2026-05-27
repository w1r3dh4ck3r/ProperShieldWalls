// POC test behavior — proves single-agent stab-in-slot before full MVP.
// DELETE THIS FILE after the day-one POC gate passes.
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls
{
    internal class OthismosTestBehaviour : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        // Read by SlotLockPatch to decide which agents to hold in place.
        // Static because Harmony prefix runs outside any behavior instance context.
        internal static readonly HashSet<Agent> LockedAgents = new HashSet<Agent>();

        private Vec3 _slotPosition;
        private float _logTimer;
        private const float LOG_INTERVAL = 3f;

        public override void AfterStart()
        {
            LockedAgents.Clear();
            TryLockOneAgent();
        }

        public override void OnDeploymentFinished()
        {
            // Fallback: some mission types spawn agents after AfterStart
            if (LockedAgents.Count == 0)
                TryLockOneAgent();
        }

        private void TryLockOneAgent()
        {
            foreach (var agent in Mission.Current.Agents)
            {
                if (!agent.IsAIControlled) continue;
                if (agent.Team != Mission.Current.PlayerTeam) continue;
                if (!HasShieldAndOneHanded(agent)) continue;

                _slotPosition = agent.Position;
                LockedAgents.Add(agent);
                SubModule.Log($"[PSW TEST] Locked agent '{agent.Name}' at ({_slotPosition.x:F1}, {_slotPosition.y:F1}, {_slotPosition.z:F1})");
                return;
            }

            SubModule.Log("[PSW TEST] WARNING: No shield+1H infantry found to lock");
        }

        public override void OnMissionTick(float dt)
        {
            _logTimer += dt;
            if (_logTimer < LOG_INTERVAL) return;
            _logTimer = 0f;

            foreach (var agent in LockedAgents)
            {
                if (!agent.IsActive()) continue;

                float dist = (agent.Position - _slotPosition).Length;
                var actionType = agent.GetCurrentActionType(0);
                SubModule.Log(
                    $"[PSW TEST] '{agent.Name}': dist={dist:F2}m  action={actionType}" +
                    $"  HP={agent.Health:F0}/{agent.HealthLimit:F0}  formation={agent.Formation?.Index}");
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState, KillingBlow blow)
        {
            LockedAgents.Remove(affectedAgent);
        }

        private static bool HasShieldAndOneHanded(Agent agent)
        {
            bool hasShield = false, hasOneHanded = false;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                var elem = agent.Equipment[i];
                if (elem.IsEmpty) continue;
                var wc = elem.Item?.PrimaryWeapon?.WeaponClass;
                if (wc == WeaponClass.SmallShield || wc == WeaponClass.LargeShield)
                    hasShield = true;
                if (wc == WeaponClass.OneHandedSword || wc == WeaponClass.OneHandedAxe ||
                    wc == WeaponClass.OneHandedPolearm)
                    hasOneHanded = true;
            }
            return hasShield && hasOneHanded;
        }
    }
}
