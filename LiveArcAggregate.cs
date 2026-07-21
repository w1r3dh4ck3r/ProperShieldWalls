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
        private int _rank1PlusPolearmSwing;
        private int _rank1PlusPolearm;
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

            if (isPolearm) _rank1PlusPolearm++;
            if (isPolearm && isThrust) _rank1PlusPolearmThrust++;
            if (isPolearm && isSwing) _rank1PlusPolearmSwing++;
            if (weaponLength >= 200) _rank1PlusReach200++;
            if (isThrust) _rank1PlusThrust++;
            if (isSwing) _rank1PlusSwing++;
            if (isPolearm && isThrust && relativePosition == "front") _rank1PlusPolearmThrustFront++;
        }

        /// <summary>
        /// Renders the pre-registered §5 answers plus supporting counts, one string per line.
        /// Every rank>=1 sub-metric prints BOTH denominators -- percent of weapon strikes AND
        /// percent of rank>=1 itself (F1). The two answer different questions: "how big is this
        /// slice of the whole mission" vs. "what does rank>=1 look like on the inside", and §5 row 3
        /// asks the second one. Reporting only the first is the exact bug this file exists to fix --
        /// see the class doc comment and the brief's worked example. Both denominators are guarded
        /// against divide-by-zero independently (PctOfWeaponStrikes / PctOfRankOnePlus).
        /// </summary>
        internal List<string> Render(int windupRejectLiveArcCount)
        {
            var lines = new List<string>();

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "total live-arc rejects: {0}", Total));

            bool matched = windupRejectLiveArcCount == Total;
            // F4: MATCH is guaranteed by construction (census and windupRejects increment on the
            // same event with no branch between them) -- it proves the two counters are wired to
            // the same source, not that the underlying sampling is sound. Say so in the line itself
            // so a reader does not mistake wiring proof for a sampling guarantee.
            string verdict = matched
                ? "MATCH (coupled by construction; proves wiring, not sampling)"
                : "MISMATCH -- samples dropped";
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "cross-check vs windup rejects[live-arc]={0}: {1}", windupRejectLiveArcCount, verdict));

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "weapon strikes (alt=0): {0}", _weaponStrikes));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "alternative attacks (alt=1): {0}", _altAttacks));
            lines.Add("rows below are computed over weapon strikes only (alt=1 excluded)");

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1: {0} ({1}% of weapon strikes)", _rank1Plus, PctOfWeaponStrikes(_rank1Plus)));

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 polearm Thrust: {0} ({1}% of weapon strikes, {2}% of rank>=1)",
                _rank1PlusPolearmThrust, PctOfWeaponStrikes(_rank1PlusPolearmThrust), PctOfRankOnePlus(_rank1PlusPolearmThrust)));

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 with reach>=200: {0} ({1}% of weapon strikes, {2}% of rank>=1)",
                _rank1PlusReach200, PctOfWeaponStrikes(_rank1PlusReach200), PctOfRankOnePlus(_rank1PlusReach200)));

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 Thrust: {0} vs Swing: {1}", _rank1PlusThrust, _rank1PlusSwing));
            // F5: strikeType values other than Thrust(1)/Swing(0) -- Invalid -- count into
            // rank>=1 but into neither bucket above, so Thrust+Swing can legitimately fall short
            // of rank>=1. Correct behaviour; say so or a reader will chase a phantom bug.
            lines.Add("note: Thrust + Swing can be less than rank>=1 -- a strikeType outside " +
                      "Thrust/Swing (Invalid) counts into rank>=1 but into neither bucket; that " +
                      "shortfall is expected, not a bug");

            // F2: §5 row 4 asks whether POLEARM rejects are majority Swing, not whether rank>=1
            // as a whole is. Printed alongside (not replacing) the all-weapon Thrust-vs-Swing line
            // above, so a sword-heavy rear rank can no longer masquerade as the polearm answer.
            // FIX: Report the total rank>=1 polearm count and percentages, so the reader can see
            // the true Swing percentage even when Invalid strike types exist.
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 polearm Thrust: {0} vs polearm Swing: {1}   (of {2} rank>=1 polearms: {3}% Thrust, {4}% Swing)",
                _rank1PlusPolearmThrust, _rank1PlusPolearmSwing, _rank1PlusPolearm,
                PctOfRankOnePlusPolearm(_rank1PlusPolearmThrust), PctOfRankOnePlusPolearm(_rank1PlusPolearmSwing)));

            // F1 applies here too: _rank1PlusPolearmThrustFront <= _rank1Plus by construction,
            // same shape as reach>=200 above, so this line needs the same second denominator.
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "rank>=1 polearm Thrust IN FRONT (rel=front): {0} ({1}% of weapon strikes, {2}% of rank>=1)",
                _rank1PlusPolearmThrustFront, PctOfWeaponStrikes(_rank1PlusPolearmThrustFront), PctOfRankOnePlus(_rank1PlusPolearmThrustFront)));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "detached: {0} ({1}%)", _detached, PctOfWeaponStrikes(_detached)));

            return lines;
        }

        /// <summary>Percentage of the weapon-strike population, formatted "X.X". 0.0 when empty, never NaN.</summary>
        private string PctOfWeaponStrikes(int n)
        {
            if (_weaponStrikes == 0) return "0.0";
            double pct = 100.0 * n / _weaponStrikes;
            return pct.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Percentage of the rank>=1 population specifically -- a SEPARATE denominator from
        /// PctOfWeaponStrikes, guarded independently against divide-by-zero (F1). §5 row 3 asks a
        /// question about composition WITHIN rank>=1 ("do they carry reach>=200"), not about
        /// rank>=1's share of all weapon strikes; answering it against the wrong denominator is
        /// the headline bug this file exists to fix.
        /// </summary>
        private string PctOfRankOnePlus(int n)
        {
            if (_rank1Plus == 0) return "0.0";
            double pct = 100.0 * n / _rank1Plus;
            return pct.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Percentage of the rank>=1 polearm population specifically, guarded independently
        /// against divide-by-zero. §5 row 4 asks whether rank>=1 polearms are majority Swing,
        /// not whether they are majority among all rank>=1 strikes. This denominator isolates
        /// the polearm population to answer that question. When Invalid strike types exist
        /// (neither Thrust nor Swing), this denominator reveals the true Thrust and Swing
        /// percentages within the polearm population.
        /// </summary>
        private string PctOfRankOnePlusPolearm(int n)
        {
            if (_rank1PlusPolearm == 0) return "0.0";
            double pct = 100.0 * n / _rank1PlusPolearm;
            return pct.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
