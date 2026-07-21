using System.Linq;
using ProperShieldWalls;
using Xunit;

public class LiveArcAggregateTests
{
    [Fact]
    public void Total_CountsEveryAdd_IncludingAlternativeAttacks()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 0, false, "front");
        agg.Add(2, "OneHandedPolearm", 250, 1, true, "front"); // alt attack still counted in Total

        Assert.Equal(2, agg.Total);
    }

    [Fact]
    public void AlternativeAttacks_ExcludedFromPercentageRows_ButReportedOnOwnLine()
    {
        var agg = new LiveArcAggregate();
        // Alt attack at rank 2 with a polearm+thrust shape that WOULD land in every §5 row
        // if it were mis-tagged as a weapon strike.
        agg.Add(2, "OneHandedPolearm", 250, 1, true, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("weapon strikes (alt=0): 0"));
        Assert.Contains(lines, l => l.Contains("alternative attacks (alt=1): 1"));
        Assert.Contains(lines, l => l.Contains("rank>=1: 0"));
        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust: 0"));
        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust IN FRONT (rel=front): 0"));
    }

    [Fact]
    public void RankZeroOnlyPopulation_YieldsZeroRankOnePlus_NoDivideByZero()
    {
        var agg = new LiveArcAggregate();
        agg.Add(0, "OneHandedSword", 100, 0, false, "front");
        agg.Add(0, "OneHandedPolearm", 250, 1, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1: 0 (0.0%"));
    }

    [Fact]
    public void TotalZero_RendersWithoutThrowing()
    {
        var agg = new LiveArcAggregate();

        var lines = agg.Render(0);

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("total live-arc rejects: 0"));
    }

    [Fact]
    public void CrossCheck_ReportsMatch_WhenPassedCountEqualsTotal()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 0, false, "front");
        agg.Add(2, "OneHandedPolearm", 250, 1, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("MATCH") && !l.Contains("MISMATCH"));
    }

    [Fact]
    public void CrossCheck_ReportsMismatch_WhenPassedCountDiffersFromTotal()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 0, false, "front");

        var lines = agg.Render(99);

        Assert.Contains(lines, l => l.Contains("MISMATCH"));
    }

    [Fact]
    public void PolearmThrustAtRank2WithFront_LandsInFrontLine()
    {
        var agg = new LiveArcAggregate();
        agg.Add(2, "OneHandedPolearm", 250, 1, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust IN FRONT (rel=front): 1"));
        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust: 1"));
        Assert.Contains(lines, l => l.Contains("rank>=1: 1"));
    }

    [Fact]
    public void PolearmThrustAtRank2NotInFront_ExcludedFromFrontLine_ButCountedInPolearmThrust()
    {
        var agg = new LiveArcAggregate();
        agg.Add(2, "OneHandedPolearm", 250, 1, false, "behind");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust IN FRONT (rel=front): 0"));
        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust: 1"));
    }

    [Fact]
    public void ThrustVsSwing_CountedSeparately()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 1, false, "front"); // Thrust
        agg.Add(1, "OneHandedSword", 100, 1, false, "front"); // Thrust
        agg.Add(1, "OneHandedSword", 100, 0, false, "front"); // Swing

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 Thrust: 2 vs Swing: 1"));
    }

    [Fact]
    public void DetachedAgents_CountedSeparately_NotInRankOnePlus()
    {
        var agg = new LiveArcAggregate();
        agg.Add(-1, "OneHandedSword", 100, 0, false, "unknown");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("detached: 1"));
        Assert.Contains(lines, l => l.Contains("rank>=1: 0"));
    }

    [Fact]
    public void ReachAtLeast200_CountedForRankOnePlusOnly()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 250, 0, false, "front");  // rank>=1, reach>=200
        agg.Add(0, "OneHandedSword", 250, 0, false, "front");  // rank 0, must NOT count

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 with reach>=200: 1"));
    }

    // --- F1: every rank>=1 sub-metric must print BOTH denominators ---

    [Fact]
    public void ReachAtLeast200_ReportsPercentOfRankOnePlus_NotJustWeaponStrikes()
    {
        var agg = new LiveArcAggregate();
        // 10 rank>=1 strikes, EVERY one carrying reach>=200 -- 100% of rank>=1.
        for (int i = 0; i < 10; i++)
            agg.Add(1, "TwoHandedPolearm", 300, 1, false, "front");
        // 90 rank-0 strikes with reach<200, padding the weapon-strike population so
        // rank>=1 sits at 10% of weapon strikes -- squarely inside §5 row 3's 5-20% band,
        // the exact regime the brief says is "essentially the only regime row 3 is reached".
        for (int i = 0; i < 90; i++)
            agg.Add(0, "OneHandedSword", 90, 0, false, "front");

        var lines = agg.Render(agg.Total);

        // Sharp regression: with the old single-denominator arithmetic this line reports
        // "10.0%" (of weapon strikes) even though every single rank>=1 man carries reach>=200.
        // A correct reader must see "100.0% of rank>=1" to answer §5 row 3 correctly.
        Assert.Contains(lines, l => l.Contains("rank>=1 with reach>=200: 10") && l.Contains("100.0% of rank>=1"));
    }

    [Fact]
    public void ReachAtLeast200_RankOnePlusZero_RendersZeroPercentOfRankOnePlus_NoDivideByZero()
    {
        var agg = new LiveArcAggregate();
        agg.Add(0, "OneHandedSword", 250, 0, false, "front"); // rank 0 only -- rank>=1 population is empty

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 with reach>=200: 0") && l.Contains("0.0% of rank>=1"));
    }

    [Fact]
    public void PolearmThrust_ReportsPercentOfRankOnePlus_AlongsidePercentOfWeaponStrikes()
    {
        var agg = new LiveArcAggregate();
        for (int i = 0; i < 5; i++)
            agg.Add(1, "TwoHandedPolearm", 300, 1, false, "front"); // rank>=1 polearm Thrust
        for (int i = 0; i < 45; i++)
            agg.Add(0, "OneHandedSword", 90, 0, false, "front");    // padding, rank 0

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust: 5") && l.Contains("100.0% of rank>=1"));
    }

    // --- F2: §5 row 4 is about POLEARMS specifically ---

    [Fact]
    public void PolearmThrustVsSwing_ReportsPolearmCountsOnly_NotAllWeaponCounts()
    {
        var agg = new LiveArcAggregate();
        // 3 rank>=1 polearm Swings.
        for (int i = 0; i < 3; i++)
            agg.Add(1, "TwoHandedPolearm", 300, 0, false, "front");
        // 5 rank>=1 sword Thrusts -- non-polearm. These would drag the ALL-WEAPON Thrust/Swing
        // line to "majority Thrust" while the polearm-only line must still show majority Swing.
        for (int i = 0; i < 5; i++)
            agg.Add(1, "OneHandedSword", 100, 1, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 Thrust: 5 vs Swing: 3"));           // all-weapon, unchanged
        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust: 0 vs polearm Swing: 3")); // polearm-only
    }

    [Fact]
    public void PolearmThrustInFront_ReportsPercentOfRankOnePlus_SameShapeAsReach200()
    {
        var agg = new LiveArcAggregate();
        // 4 rank>=1 polearm Thrusts, all landed "front" -- 100% of rank>=1.
        for (int i = 0; i < 4; i++)
            agg.Add(2, "TwoHandedPolearm", 300, 1, false, "front");
        // 36 rank-0 strikes padding the weapon-strike population so rank>=1 sits at 10%.
        for (int i = 0; i < 36; i++)
            agg.Add(0, "OneHandedSword", 90, 0, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l =>
            l.Contains("rank>=1 polearm Thrust IN FRONT (rel=front): 4") && l.Contains("100.0% of rank>=1"));
    }

    // --- F3: relative position must never fall back to "front" when formations differ ---

    [Fact]
    public void OtherFormationRelativePosition_NeverCountsAsFront()
    {
        var agg = new LiveArcAggregate();
        agg.Add(2, "OneHandedPolearm", 250, 1, false, LiveArcCensus.OtherFormationBucket);

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("rank>=1 polearm Thrust IN FRONT (rel=front): 0"));
    }

    // --- F4: the MATCH cross-check must state its own limitation ---

    [Fact]
    public void CrossCheck_MatchLine_ContainsHonestyClause()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 0, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l =>
            l.Contains("MATCH") &&
            l.Contains("coupled by construction") &&
            l.Contains("proves wiring, not sampling"));
    }

    // --- F5: Thrust/Swing shortfall against rank>=1 is expected, not a bug ---

    [Fact]
    public void Render_ExplainsThrustSwingShortfallAgainstRankOnePlus()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 0, false, "front");

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l => l.Contains("Invalid") && l.Contains("not a bug"));
    }

    // --- FIX: §5 row 4 total denominator ---

    [Fact]
    public void PolearmThrustVsSwing_WithInvalidStrikeTypes_ReportsTotalAndPercentages()
    {
        var agg = new LiveArcAggregate();
        // 3 rank>=1 polearm Thrusts (strikeType=1)
        for (int i = 0; i < 3; i++)
            agg.Add(1, "TwoHandedPolearm", 300, 1, false, "front");
        // 4 rank>=1 polearm Swings (strikeType=0)
        for (int i = 0; i < 4; i++)
            agg.Add(1, "TwoHandedPolearm", 300, 0, false, "front");
        // 5 rank>=1 polearm Invalid strikes (strikeType=2, neither Thrust nor Swing)
        for (int i = 0; i < 5; i++)
            agg.Add(1, "TwoHandedPolearm", 300, 2, false, "front");

        var lines = agg.Render(agg.Total);

        // The line should show: rank>=1 polearm Thrust: 3 vs polearm Swing: 4   (of 12 rank>=1 polearms: 25.0% Thrust, 33.3% Swing)
        Assert.Contains(lines, l =>
            l.Contains("rank>=1 polearm Thrust: 3 vs polearm Swing: 4") &&
            l.Contains("of 12 rank>=1 polearms") &&
            l.Contains("25.0%") &&
            l.Contains("33.3%"));
    }

    [Fact]
    public void PolearmThrustVsSwing_ZeroRankOnePlusPolearms_RendersZeroPercentOfRankOnePlusPolearms_NoDivideByZero()
    {
        var agg = new LiveArcAggregate();
        agg.Add(1, "OneHandedSword", 100, 0, false, "front"); // rank>=1 but not polearm

        var lines = agg.Render(agg.Total);

        Assert.Contains(lines, l =>
            l.Contains("rank>=1 polearm Thrust: 0 vs polearm Swing: 0") &&
            l.Contains("of 0 rank>=1 polearms") &&
            l.Contains("0.0%"));
    }
}
