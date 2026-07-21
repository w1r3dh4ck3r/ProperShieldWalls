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
        string key = LiveArcCensus.BuildKey(2, "OneHandedPolearm", 250, 1, "Up", false, null);

        Assert.Contains("rank=2", key);
        Assert.Contains("wpn=OneHandedPolearm", key);
        Assert.Contains("len=200-279", key);
        Assert.Contains("strike=Thrust", key);
        Assert.Contains("dir=Up", key);
    }

    [Fact]
    public void BuildKey_SwingIsLabelledSwing()
    {
        string key = LiveArcCensus.BuildKey(0, "OneHandedSword", 100, 0, "Left", false, null);
        Assert.Contains("strike=Swing", key);
    }

    [Fact]
    public void BuildKey_IsStableForIdenticalInput()
    {
        // The key IS the dictionary identity. Two identical events must collapse to one entry.
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up", false, "front");
        string b = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up", false, "front");
        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildKey_DiffersWhenRankDiffers()
    {
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up", false, null);
        string b = LiveArcCensus.BuildKey(2, "TwoHandedPolearm", 300, 1, "Up", false, null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildKey_DiffersWhenStrikeTypeDiffers()
    {
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up", false, null);
        string b = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 0, "Up", false, null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildKey_NullWeaponClassBecomesUnarmed()
    {
        string key = LiveArcCensus.BuildKey(1, null, 0, 0, "Left", false, null);
        Assert.Contains("wpn=unarmed", key);
    }

    [Fact]
    public void BuildKey_EmptyWeaponClassBecomesUnarmed()
    {
        string key = LiveArcCensus.BuildKey(1, "", 0, 0, "Left", false, null);
        Assert.Contains("wpn=unarmed", key);
    }

    [Fact]
    public void BuildKey_NullDirectionBecomesUnknown()
    {
        string key = LiveArcCensus.BuildKey(1, "OneHandedSword", 100, 0, null, false, null);
        Assert.Contains("dir=?", key);
    }

    // --- Fix 1: alternative-attack tagging ---

    [Fact]
    public void BuildKey_WeaponStrikeRendersAltZero()
    {
        string key = LiveArcCensus.BuildKey(1, "OneHandedSword", 100, 0, "Left", false, null);
        Assert.Contains("alt=0", key);
    }

    [Fact]
    public void BuildKey_AlternativeAttackRendersAltOne()
    {
        string key = LiveArcCensus.BuildKey(1, "OneHandedSword", 100, 0, "Left", true, null);
        Assert.Contains("alt=1", key);
    }

    // --- Fix 2: relative-position tagging ---

    [Fact]
    public void BuildKey_RelativePositionRendersInKey()
    {
        string key = LiveArcCensus.BuildKey(2, "OneHandedPolearm", 250, 1, "Up", false, "front");
        Assert.Contains("rel=front", key);
    }

    [Fact]
    public void BuildKey_NullRelativePositionRendersUnknown()
    {
        string key = LiveArcCensus.BuildKey(2, "OneHandedPolearm", 250, 1, "Up", false, null);
        Assert.Contains("rel=unknown", key);
    }

    [Fact]
    public void BuildKey_EmptyRelativePositionRendersUnknown()
    {
        string key = LiveArcCensus.BuildKey(2, "OneHandedPolearm", 250, 1, "Up", false, "");
        Assert.Contains("rel=unknown", key);
    }

    [Fact]
    public void RelativePosition_UnknownWhenAttackerFileNegative()
    {
        Assert.Equal("unknown", LiveArcCensus.RelativePosition(-1, 2, 3, 2));
    }

    [Fact]
    public void RelativePosition_UnknownWhenAttackerRankNegative()
    {
        Assert.Equal("unknown", LiveArcCensus.RelativePosition(3, -1, 3, 2));
    }

    [Fact]
    public void RelativePosition_UnknownWhenVictimFileNegative()
    {
        Assert.Equal("unknown", LiveArcCensus.RelativePosition(3, 2, -1, 2));
    }

    [Fact]
    public void RelativePosition_UnknownWhenVictimRankNegative()
    {
        Assert.Equal("unknown", LiveArcCensus.RelativePosition(3, 2, 3, -1));
    }

    [Fact]
    public void RelativePosition_OtherFileWhenFileIndicesDiffer()
    {
        Assert.Equal("other-file", LiveArcCensus.RelativePosition(3, 2, 4, 2));
    }

    [Fact]
    public void RelativePosition_FrontWhenSameFileVictimRankLower()
    {
        Assert.Equal("front", LiveArcCensus.RelativePosition(3, 2, 3, 0));
    }

    [Fact]
    public void RelativePosition_SameRankWhenSameFileVictimRankEqual()
    {
        Assert.Equal("same-rank", LiveArcCensus.RelativePosition(3, 2, 3, 2));
    }

    [Fact]
    public void RelativePosition_BehindWhenSameFileVictimRankHigher()
    {
        Assert.Equal("behind", LiveArcCensus.RelativePosition(3, 0, 3, 2));
    }

    // --- IsPolearmClass ---

    [Fact]
    public void IsPolearmClass_TrueForOneHandedPolearm()
    {
        Assert.True(LiveArcCensus.IsPolearmClass("OneHandedPolearm"));
    }

    [Fact]
    public void IsPolearmClass_TrueForTwoHandedPolearm()
    {
        Assert.True(LiveArcCensus.IsPolearmClass("TwoHandedPolearm"));
    }

    [Fact]
    public void IsPolearmClass_FalseForOneHandedSword()
    {
        Assert.False(LiveArcCensus.IsPolearmClass("OneHandedSword"));
    }

    [Fact]
    public void IsPolearmClass_FalseForJavelin()
    {
        // Deliberate: a javelin lacks the reach the feature is about, so it is excluded
        // even though it is thrown with a polearm-adjacent animation set.
        Assert.False(LiveArcCensus.IsPolearmClass("Javelin"));
    }

    [Fact]
    public void IsPolearmClass_FalseForNull()
    {
        Assert.False(LiveArcCensus.IsPolearmClass(null));
    }

    [Fact]
    public void IsPolearmClass_FalseForEmpty()
    {
        Assert.False(LiveArcCensus.IsPolearmClass(""));
    }

    [Fact]
    public void IsPolearmClass_TrueForLowGripPolearm()
    {
        // The substring match also catches LowGripPolearm; the doc comment must say so (F5).
        Assert.True(LiveArcCensus.IsPolearmClass("LowGripPolearm"));
    }

    // --- F3: cross-formation bucket ---

    [Fact]
    public void OtherFormationBucket_IsTheExpectedString()
    {
        // Diagnostics.RecordLiveArc buckets a cross-formation collision to this constant instead
        // of calling RelativePosition on incomparable per-formation indices. Referenced by name
        // so a rename here is a deliberate, visible change, not a silent drift.
        Assert.Equal("other-formation", LiveArcCensus.OtherFormationBucket);
    }
}
