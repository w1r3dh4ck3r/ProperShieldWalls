using System;
using System.Collections.Generic;
using MCM.Abstractions.Base.Global;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    /// <summary>
    /// Revives vanilla's shield rotation in the two formations where it is structurally dead.
    ///
    /// LineFormation.SwitchFrontUnitTypesToFrontRows() already pulls shielded men toward rank 0 —
    /// but it opens with `if (Interval &lt;= 0f) return;`, and ArrangementOrder.GetUnitSpacingOf
    /// returns 0 for BOTH ShieldWall and Square. Interval = 0.38f * 0 = 0, so it returns on its
    /// first line every tick, forever, in exactly the two formations built around shields.
    ///
    /// No Harmony patch: every member used here is public. We call the SAME method vanilla's own
    /// loop calls (IFormationArrangement.SwitchUnitLocations), so RBMFork's and FrontlineModFork's
    /// prefixes on it still run on our swaps — both return true for a valid active pair.
    /// </summary>
    internal sealed class ShieldRotationBehavior : MissionBehavior
    {
        private float _sinceLastSweep;

        /// <summary>Reused across files and sweeps: the sweep runs 2x/second, and this would otherwise churn the GC.</summary>
        private readonly List<Agent> _column = new List<Agent>();
        private readonly Dictionary<int, List<Agent>> _files = new Dictionary<int, List<Agent>>();

        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            var settings = GlobalSettings<Settings>.Instance;
            if (settings == null || !settings.Enabled || !settings.ShieldRotation) return;

            _sinceLastSweep += dt;
            if (_sinceLastSweep < settings.RotationInterval) return;
            _sinceLastSweep = 0f;

            try
            {
                Sweep();
            }
            catch (Exception ex)
            {
                // Runs 2x/second for the whole battle: an unthrottled log here is a storm.
                SubModule.LogErrorThrottled(
                    "ShieldRotationBehavior:" + ex.GetType().Name,
                    "[PSW] ShieldRotationBehavior error: " + ex.Message);
            }
        }

        private void Sweep()
        {
            var mission = Mission.Current;
            if (mission == null) return;

            foreach (Team team in mission.Teams)
            {
                if (team == null) continue;

                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation == null) continue;

                    var arrangement = formation.Arrangement;
                    if (arrangement == null || arrangement.UnitCount < 2) continue;

                    // Vanilla's OWN guard, inverted. It bails when Interval <= 0, which today means
                    // exactly ShieldWall and Square. Testing Interval rather than hard-coding the
                    // ArrangementOrderEnum list means we track any future spacing change for free,
                    // and we never touch Line/Circle, where vanilla's rotation already works.
                    if (formation.Interval > 0f) continue;

                    RotateFormation(arrangement);
                }
            }
        }

        private void RotateFormation(IFormationArrangement arrangement)
        {
            _files.Clear();

            foreach (IFormationUnit unit in arrangement.GetAllUnits())
            {
                var agent = unit as Agent;
                if (agent == null || !agent.IsActive()) continue;

                int fileIndex, rankIndex;
                agent.GetFormationFileAndRankInfo(out fileIndex, out rankIndex);

                // LineFormation.SwitchUnitLocations — the overload vanilla's loop uses, and the one
                // we call — has NO detachment guard (only Formation's Agent-typed overload does).
                // An unpositioned unit reports -1 and must not be swapped, so we guard it ourselves.
                if (fileIndex < 0 || rankIndex < 0)
                {
                    Diagnostics.RecordRotationSkippedDetached();
                    continue;
                }

                List<Agent> column;
                if (!_files.TryGetValue(fileIndex, out column))
                {
                    column = new List<Agent>();
                    _files[fileIndex] = column;
                }
                column.Add(agent);
            }

            Diagnostics.RecordRotationFormation();

            foreach (var entry in _files)
                RotateFile(arrangement, entry.Value);
        }

        private void RotateFile(IFormationArrangement arrangement, List<Agent> unordered)
        {
            if (unordered.Count < 2) return;

            _column.Clear();
            _column.AddRange(unordered);
            _column.Sort(CompareByRank);

            if (!_column[0].HasShieldCached)
                Diagnostics.RecordShieldlessFront();

            var hasShield = new bool[_column.Count];
            for (int i = 0; i < _column.Count; i++)
                hasShield[i] = _column[i].HasShieldCached;

            List<ShieldRotation.Swap> plan = ShieldRotation.PlanFileSwaps(hasShield);

            foreach (ShieldRotation.Swap swap in plan)
            {
                Agent a = _column[swap.A];
                Agent b = _column[swap.B];

                // Defence in depth. Within a single synchronous sweep an agent cannot actually die
                // between the snapshot above and this call, so this should never fire — but
                // SwitchUnitLocations has no guard of its own and would index _units2D[-1, -1] and
                // throw, inside OnMissionTick, on every frame. Both RBMFork and FrontlineModFork
                // guard this same call against null/inactive units, which is reason enough.
                //
                // Skipping WITHOUT mirroring is correct: the game state did not change either, so the
                // local column and the arrangement stay in agreement. The file is simply left partly
                // unsorted and the next sweep finishes the job.
                if (a == null || b == null || !a.IsActive() || !b.IsActive())
                    continue;

                arrangement.SwitchUnitLocations(a, b);
                Diagnostics.RecordShieldSwap();

                // Mirror the swap in our local view so later swaps in the same plan address the
                // right agents — the plan's indices are slot positions, not agent identities.
                _column[swap.A] = b;
                _column[swap.B] = a;
            }
        }

        private static int CompareByRank(Agent left, Agent right)
        {
            int leftFile, leftRank, rightFile, rightRank;
            left.GetFormationFileAndRankInfo(out leftFile, out leftRank);
            right.GetFormationFileAndRankInfo(out rightFile, out rightRank);
            return leftRank.CompareTo(rightRank);
        }
    }
}
