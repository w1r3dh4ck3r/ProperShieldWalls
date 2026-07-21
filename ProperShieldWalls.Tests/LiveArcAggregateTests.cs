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
}
