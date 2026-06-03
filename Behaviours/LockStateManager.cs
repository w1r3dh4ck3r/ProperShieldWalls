using System.Collections.Generic;
using ProperShieldWalls.Models;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    internal class LockStateManager
    {
        private readonly List<EngagementPair> _pairs = new List<EngagementPair>();
        private readonly Settings _settings;
        private readonly SlotEnforcer _slotEnforcer;

        // Cached once per mission; null if StaminaSystem not loaded.
        private MissionBehavior _staminaBehavior;
        private bool _staminaBehaviorSearched;

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
                        TickLocked(pair, i);
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
            if (pair.StateTimer >= 1.0f)  // 1 s debounce before locking
            {
                pair.State = EngagementState.Locked;
                pair.StateTimer = 0f;
                pair.InitialFrontRankA = CountFrontRank(pair.FormationA);
                pair.InitialFrontRankB = CountFrontRank(pair.FormationB);
                OthismosState.Lock(pair.FormationA);
                OthismosState.Lock(pair.FormationB);
                _slotEnforcer.OnLocked(pair);
                SubModule.Log($"[PSW] Locked: formation {pair.FormationA.Index} vs {pair.FormationB.Index} " +
                              $"(frontRank {pair.InitialFrontRankA} vs {pair.InitialFrontRankB}, " +
                              $"staminaMod={(StaminaReader.IsAvailable ? "on" : "off")})");
            }
        }

        private void TickLocked(EngagementPair pair, int index)
        {
            // --- Stamina exhaustion (StaminaSystem mod) ---
            if (StaminaReader.IsAvailable)
            {
                var inst = GetStaminaBehavior();
                float staminaA = AverageFrontRankStamina(pair.FormationA, inst);
                float staminaB = AverageFrontRankStamina(pair.FormationB, inst);
                if (staminaA < _settings.StaminaBreakThreshold || staminaB < _settings.StaminaBreakThreshold)
                {
                    BeginBreaking(pair, $"stamina exhausted (A={staminaA:F2} B={staminaB:F2})");
                    return;
                }
            }

            // --- Absolute count floor ---
            bool tooFew = pair.FormationA.CountOfUnitsWithoutDetachedOnes < _settings.MinAgentsPerSide
                       || pair.FormationB.CountOfUnitsWithoutDetachedOnes < _settings.MinAgentsPerSide;
            if (tooFew) { BeginBreaking(pair, "too few agents"); return; }

            // --- Front-rank coverage: gaps too wide to maintain shields ---
            bool coverageLost = (pair.InitialFrontRankA > 0 && CountFrontRank(pair.FormationA) < pair.InitialFrontRankA * 0.5f)
                             || (pair.InitialFrontRankB > 0 && CountFrontRank(pair.FormationB) < pair.InitialFrontRankB * 0.5f);
            if (coverageLost) { BeginBreaking(pair, "coverage lost"); return; }

            // --- Macro-disengage: formations have drifted apart (pulse ended, not a break) ---
            float dist = FormationDistance(pair.FormationA, pair.FormationB);
            if (dist > _settings.EngagementDistance * 1.5f)
            {
                BeginBreaking(pair, $"macro-disengage (dist={dist:F1}m)");
            }
        }

        private MissionBehavior GetStaminaBehavior()
        {
            if (!_staminaBehaviorSearched)
            {
                _staminaBehavior = StaminaReader.FindInstance();
                _staminaBehaviorSearched = true;
            }
            return _staminaBehavior;
        }

        // Average 0–1 stamina ratio across active front-rank agents. Returns 1.0 if no front-rank agents found.
        private static float AverageFrontRankStamina(Formation f, MissionBehavior inst)
        {
            float total = 0f;
            int count = 0;
            foreach (var unit in f.Arrangement.GetAllUnits())
            {
                if (!(unit is Agent a) || !a.IsActive()) continue;
                if (((IFormationUnit)a).FormationRankIndex != 0) continue;
                total += StaminaReader.GetStaminaRatio(a, inst);
                count++;
            }
            return count > 0 ? total / count : 1f;
        }

        private static int CountFrontRank(Formation f)
        {
            int count = 0;
            foreach (var unit in f.Arrangement.GetAllUnits())
                if (unit is Agent a && a.IsActive() && ((IFormationUnit)a).FormationRankIndex == 0)
                    count++;
            return count;
        }

        private static float FormationDistance(Formation a, Formation b)
        {
            Vec2 ca = GetCenter(a);
            Vec2 cb = GetCenter(b);
            float dx = ca.x - cb.x, dy = ca.y - cb.y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private static Vec2 GetCenter(Formation f)
        {
            float x = 0f, y = 0f; int n = 0;
            foreach (var unit in f.Arrangement.GetAllUnits())
            {
                if (unit is Agent a && a.IsActive())
                { x += a.Position.x; y += a.Position.y; n++; }
            }
            return n > 0 ? new Vec2(x / n, y / n) : Vec2.Zero;
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
                if ((a == pair.FormationA && b == pair.FormationB) ||
                    (a == pair.FormationB && b == pair.FormationA))
                    return true;
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
