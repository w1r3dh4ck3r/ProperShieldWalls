using ProperShieldWalls;
using Xunit;

public class LiveArcCensusTests
{
    [Fact]
    public void RankBucket_Detached_ForNegativeIndex()
    {
        Assert.Equal("detached", LiveArcCensus.RankBucket(-1));
    }

    [Fact]
    public void RankBucket_Detached_ForAnyNegative()
    {
        Assert.Equal("detached", LiveArcCensus.RankBucket(-99));
    }

    [Fact]
    public void RankBucket_ExactForFirstThree()
    {
        Assert.Equal("0", LiveArcCensus.RankBucket(0));
        Assert.Equal("1", LiveArcCensus.RankBucket(1));
        Assert.Equal("2", LiveArcCensus.RankBucket(2));
    }

    [Fact]
    public void RankBucket_CollapsesDeepRanks()
    {
        Assert.Equal("3+", LiveArcCensus.RankBucket(3));
        Assert.Equal("3+", LiveArcCensus.RankBucket(17));
    }

    [Fact]
    public void LengthBucket_Boundaries()
    {
        Assert.Equal("<120", LiveArcCensus.LengthBucket(0));
        Assert.Equal("<120", LiveArcCensus.LengthBucket(119));
        Assert.Equal("120-199", LiveArcCensus.LengthBucket(120));
        Assert.Equal("120-199", LiveArcCensus.LengthBucket(199));
        Assert.Equal("200-279", LiveArcCensus.LengthBucket(200));
        Assert.Equal("200-279", LiveArcCensus.LengthBucket(279));
        Assert.Equal("280+", LiveArcCensus.LengthBucket(280));
        Assert.Equal("280+", LiveArcCensus.LengthBucket(9999));
    }

    [Fact]
    public void LengthBucket_NegativeIsTreatedAsShortest()
    {
        // An absent weapon reports length 0 via the adapter, but guard the negative case
        // so a surprising native value cannot produce an unbucketed key.
        Assert.Equal("<120", LiveArcCensus.LengthBucket(-5));
    }

    [Fact]
    public void StrikeLabel_MapsAllThreeCases()
    {
        // Must be three-way. Folding Invalid into Swing would corrupt the "majority Swing"
        // decision-rule row in the spec.
        Assert.Equal("Swing", LiveArcCensus.StrikeLabel(0));
        Assert.Equal("Thrust", LiveArcCensus.StrikeLabel(1));
        Assert.Equal("Invalid", LiveArcCensus.StrikeLabel(-1));
        Assert.Equal("Invalid", LiveArcCensus.StrikeLabel(7));
    }

    [Fact]
    public void BuildKey_ContainsEveryField()
    {
        string key = LiveArcCensus.BuildKey(2, "OneHandedPolearm", 250, 1, "Up");

        Assert.Contains("rank=2", key);
        Assert.Contains("wpn=OneHandedPolearm", key);
        Assert.Contains("len=200-279", key);
        Assert.Contains("strike=Thrust", key);
        Assert.Contains("dir=Up", key);
    }

    [Fact]
    public void BuildKey_SwingIsLabelledSwing()
    {
        string key = LiveArcCensus.BuildKey(0, "OneHandedSword", 100, 0, "Left");
        Assert.Contains("strike=Swing", key);
    }

    [Fact]
    public void BuildKey_IsStableForIdenticalInput()
    {
        // The key IS the dictionary identity. Two identical events must collapse to one entry.
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        string b = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildKey_DiffersWhenRankDiffers()
    {
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        string b = LiveArcCensus.BuildKey(2, "TwoHandedPolearm", 300, 1, "Up");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildKey_DiffersWhenStrikeTypeDiffers()
    {
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        string b = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 0, "Up");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildKey_NullWeaponClassBecomesUnarmed()
    {
        string key = LiveArcCensus.BuildKey(1, null, 0, 0, "Left");
        Assert.Contains("wpn=unarmed", key);
    }

    [Fact]
    public void BuildKey_EmptyWeaponClassBecomesUnarmed()
    {
        string key = LiveArcCensus.BuildKey(1, "", 0, 0, "Left");
        Assert.Contains("wpn=unarmed", key);
    }

    [Fact]
    public void BuildKey_NullDirectionBecomesUnknown()
    {
        string key = LiveArcCensus.BuildKey(1, "OneHandedSword", 100, 0, null);
        Assert.Contains("dir=?", key);
    }
}
