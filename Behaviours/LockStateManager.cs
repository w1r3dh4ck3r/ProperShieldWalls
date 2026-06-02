using System.Collections.Generic;
using ProperShieldWalls.Models;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    internal class LockStateManager
    {
        private readonly List<EngagementPair> _pairs = new List<EngagementPair>();
        private readonly Settings _settings;
        private readonly SlotEnforcer _slotEnforcer;

        internal IReadOnlyList<EngagementPair> Pairs => _pairs;

        internal LockStateManager(Settings settings, SlotEnforcer slotEnforcer)
        {
            _settings = settings;
            _slotEnforcer = slotEnforcer;
        }

        internal void Tick(float dt, List<(Formation, Formation)> candidates)
        {
            // Advance existing pairs (backwards to allow safe RemoveAt)
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                var pair = _pairs[i];
                switch (pair.State)
                {
                    case EngagementState.Idle:
                        _pairs.RemoveAt(i);
                        break;
                    case EngagementState.PreLock:
                        TickPreLock(pair, candidates, dt);
                        break;
                    case EngagementState.Locked:
                        TickLocked(pair, dt, i);
                        break;
                    case EngagementState.Breaking:
                        TickBreaking(pair, dt, i);
                        break;
                }
            }

            // Promote new candidate pairs to PreLock if not already tracked
            foreach (var (a, b) in candidates)
            {
                if (AlreadyTracked(a, b)) continue;
                var pair = new EngagementPair(a, b) { State = EngagementState.PreLock };
                _pairs.Add(pair);
                SubModule.Log($"[PSW] PreLock: formation {a.Index} vs {b.Index}");
            }
        }

        private bool AlreadyTracked(Formation a, Formation b)
        {
            foreach (var p in _pairs)
                if (p.Involves(a) || p.Involves(b)) return true;
            return false;
        }

        private void TickPreLock(EngagementPair pair, List<(Formation, Formation)> candidates, float dt)
        {
            if (!StillCandidate(pair, candidates))
            {
                pair.State = EngagementState.Idle;
                return;
            }

            pair.StateTimer += dt;
            if (pair.StateTimer >= 1.0f)  // 1 s debounce
            {
                pair.State = EngagementState.Locked;
                pair.StateTimer = 0f;
                pair.StaminaA = 100f;
                pair.StaminaB = 100f;
                OthismosState.Lock(pair.FormationA);
                OthismosState.Lock(pair.FormationB);
                _slotEnforcer.OnLocked(pair);
                SubModule.Log($"[PSW] Locked: formation {pair.FormationA.Index} vs {pair.FormationB.Index}");
            }
        }

        private void TickLocked(EngagementPair pair, float dt, int index)
        {
            pair.StaminaA -= _settings.StaminaDrainRate * dt;
            pair.StaminaB -= _settings.StaminaDrainRate * dt;

            bool tooFew = pair.FormationA.CountOfUnitsWithoutDetachedOnes < _settings.MinAgentsPerSide
                       || pair.FormationB.CountOfUnitsWithoutDetachedOnes < _settings.MinAgentsPerSide;

            if (pair.StaminaExhausted || tooFew)
                BeginBreaking(pair, pair.StaminaExhausted ? "stamina exhausted" : "too few agents");
        }

        private void TickBreaking(EngagementPair pair, float dt, int index)
        {
            pair.StateTimer += dt;
            if (pair.StateTimer >= 0.5f)
            {
                OthismosState.Unlock(pair.FormationA);
                OthismosState.Unlock(pair.FormationB);
                _slotEnforcer.OnBreaking(pair);
                SubModule.Log($"[PSW] Disengaged: formation {pair.FormationA.Index} vs {pair.FormationB.Index}");
                _pairs.RemoveAt(index);
            }
        }

        private static bool StillCandidate(EngagementPair pair, List<(Formation, Formation)> candidates)
        {
            foreach (var (a, b) in candidates)
            {
                if ((a == pair.FormationA && b == pair.FormationB) ||
                    (a == pair.FormationB && b == pair.FormationA))
                    return true;
            }
            return false;
        }

        private static void BeginBreaking(EngagementPair pair, string reason)
        {
            pair.State = EngagementState.Breaking;
            pair.StateTimer = 0f;
            SubModule.Log($"[PSW] Breaking ({reason}): formation {pair.FormationA.Index} vs {pair.FormationB.Index}");
        }

        internal void OnAgentRemoved(Agent agent)
        {
            if (agent?.Formation == null) return;
            foreach (var pair in _pairs)
            {
                if (pair.State != EngagementState.Locked) continue;
                if (!pair.Involves(agent.Formation)) continue;
                if (pair.FormationA.CountOfUnitsWithoutDetachedOnes < 1 ||
                    pair.FormationB.CountOfUnitsWithoutDetachedOnes < 1)
                    BeginBreaking(pair, "formation wiped");
            }
        }

        internal void Clear()
        {
            foreach (var pair in _pairs)
            {
                OthismosState.Unlock(pair.FormationA);
                OthismosState.Unlock(pair.FormationB);
            }
            _pairs.Clear();
        }
    }
}
