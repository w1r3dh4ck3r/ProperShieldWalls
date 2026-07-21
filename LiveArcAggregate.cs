using System.Collections.Generic;
using System.Globalization;

namespace ProperShieldWalls
{
    /// <summary>
    /// Pure aggregator over the same fields LiveArcCensus.BuildKey buckets, rendering the spec's
    /// §5 pre-registered answers directly instead of leaving them for a human to hand-sum out of
    /// 100+ raw census lines. Pure by design, same reason as LiveArcCensus: no TaleWorlds/MCM
    /// types, source-linked by ProperShieldWalls.Tests.
    ///
    /// Every §5 percentage is of the WEAPON-STRIKE population (alt=0) -- alternative attacks
    /// (kicks, shield bashes) are recorded on their own line but never enter a percentage,
    /// because they are tagged with the attacker's wielded weapon and would otherwise inflate
    /// every row that weapon happens to match.
    /// </summary>
    internal sealed class LiveArcAggregate
    {
        private int _weaponStrikes;
        private int _altAttacks;
        private int _detached;
        private int _rank1Plus;
        private int _rank1PlusPolearmThrust;
        private int _rank1PlusReach200;
        private int _rank1PlusThrust;
        private int _rank1PlusSwing;
        private int _rank1PlusPolearmThrustFront;

        internal int Total { get; private set; }

        internal void Add(int rankIndex, string weaponClassName, int weaponLength,
                           int strikeType, bool isAlternativeAttack, string relativePosition)
        {
            Total++;

            if (isAlternativeAttack)
            {
                // Recorded (Total already incremented above) but deliberately excluded from
                // every population below -- Fix 1's whole point.
                _altAttacks++;
                return;
            }

            _weaponStrikes++;

            if (rankIndex < 0)
            {
                _detached++;
                return;
            }

            if (rankIndex < 1) return; // rank 0: outside the rank>=1 population §5 asks about

            _rank1Plus++;

            bool isPolearm = LiveArcCensus.IsPolearmClass(weaponClassName);
            bool isThrust = strikeType == 1;
            bool isSwing = strikeType == 0;

            if (isPolearm && isThrust) _rank1PlusPolearmThrust++;
            if (weaponLength >= 200) _rank1PlusReach200++;
            if (isThrust) _rank1PlusThrust++;
            if (isSwing) _rank1PlusSwing++;
            if (isPolearm && isThrust && relativePosition == "front") _rank1PlusPolearmThrustFront++;
        }

        /// <summary>
        /// Renders the pre-registered §5 answers plus supporting counts, one string per line.
        /// Percentages are of the weapon-strike population (alt=0). Guarded against divide-by-zero:
        /// an empty weapon-strike population renders "0.0%" rather than throwing or NaN-ing.
        /// </summary>
        internal List<string> Render(int windupRejectLiveArcCount)
        {
            var lines = new List<string>();

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "total live-arc rejects: {0}", Total));

            string verdict = (windupRejectLiveArcCount == Total) ? "MATCH" : "MISMATCH -- samples dropped";
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "cross-check vs windup rejects[live-arc]={0}: {1}", windupRejectLiveArcCount, verdict));

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "weapon strikes (alt=0): {0}", _weaponStrikes));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "alternative attacks (alt=1): {0}", _altAttacks));
            lines.Add("rows below are computed over weapon strikes only (alt=1 excluded)");

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1: {0} ({1}% of weapon strikes)", _rank1Plus, Pct(_rank1Plus)));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 polearm Thrust: {0} ({1}%)", _rank1PlusPolearmThrust, Pct(_rank1PlusPolearmThrust)));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 with reach>=200: {0} ({1}%)", _rank1PlusReach200, Pct(_rank1PlusReach200)));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 Thrust: {0} vs Swing: {1}", _rank1PlusThrust, _rank1PlusSwing));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 polearm Thrust IN FRONT (rel=front): {0} ({1}%)",
                _rank1PlusPolearmThrustFront, Pct(_rank1PlusPolearmThrustFront)));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "detached: {0} ({1}%)", _detached, Pct(_detached)));

            return lines;
        }

        /// <summary>Percentage of the weapon-strike population, formatted "X.X". 0.0 when empty, never NaN.</summary>
        private string Pct(int n)
        {
            if (_weaponStrikes == 0) return "0.0";
            double pct = 100.0 * n / _weaponStrikes;
            return pct.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
