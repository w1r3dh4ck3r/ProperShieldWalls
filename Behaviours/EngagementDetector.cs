using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    internal class EngagementDetector
    {
        private readonly Settings _settings;

        internal EngagementDetector(Settings settings) { _settings = settings; }

        internal List<(Formation, Formation)> FindCandidatePairs(Mission mission)
        {
            var result = new List<(Formation, Formation)>();
            if (mission?.Teams == null || mission.Teams.Count < 2) return result;

            var teamA = mission.Teams[0];
            var teamB = mission.Teams[1];
            if (teamA == null || teamB == null) return result;

            foreach (Formation fa in teamA.FormationsIncludingEmpty)
            {
                if (!IsEligible(fa)) continue;
                foreach (Formation fb in teamB.FormationsIncludingEmpty)
                {
                    if (!IsEligible(fb)) continue;
                    if (DistanceBetween(fa, fb) < _settings.EngagementDistance && FacingEachOther(fa, fb))
                        result.Add((fa, fb));
                }
            }
            return result;
        }

        private bool IsEligible(Formation f)
            => f != null
            && f.CountOfUnitsWithoutDetachedOnes >= _settings.MinAgentsPerSide
            && f.ArrangementOrder.OrderEnum == ArrangementOrderEnum.ShieldWall;

        private static float DistanceBetween(Formation a, Formation b)
        {
            Vec2 ca = GetCenter(a);
            Vec2 cb = GetCenter(b);
            float dx = ca.x - cb.x, dy = ca.y - cb.y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        // Iterating agents is O(n) but only runs per-tick on a small list of formation pairs.
        private static Vec2 GetCenter(Formation f)
        {
            float x = 0, y = 0; int n = 0;
            foreach (var unit in f.Arrangement.GetAllUnits())
            {
                if (unit is Agent a && a.IsActive())
                {
                    x += a.Position.x;
                    y += a.Position.y;
                    n++;
                }
            }
            return n > 0 ? new Vec2(x / n, y / n) : Vec2.Zero;
        }

        private static bool FacingEachOther(Formation a, Formation b)
        {
            Vec2 da = a.CurrentDirection;
            Vec2 db = b.CurrentDirection;
            return da.x * db.x + da.y * db.y < -0.5f;
        }
    }
}
