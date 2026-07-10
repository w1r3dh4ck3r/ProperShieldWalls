using ProperShieldWalls;
using Xunit;

public class CrowdStateTests
{
    public CrowdStateTests() => CrowdState.Reset();

    [Fact]
    public void NotCrowded_WhenNeverStamped()
    {
        Assert.False(CrowdState.IsCrowded(7, now: 0f));
    }

    [Fact]
    public void Crowded_WithinDuration()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(7, now: 11.9f));
    }

    [Fact]
    public void NotCrowded_AtExactExpiry()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.False(CrowdState.IsCrowded(7, now: 12f));
    }

    [Fact]
    public void NotCrowded_AfterExpiry()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.False(CrowdState.IsCrowded(7, now: 12.1f));
    }

    [Fact]
    public void StampIsPerAgent()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(7, now: 11f));
        Assert.False(CrowdState.IsCrowded(8, now: 11f));
    }

    [Fact]
    public void GrowsBeyondInitialCapacity()
    {
        CrowdState.Stamp(5000, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(5000, now: 11f));
    }

    [Fact]
    public void IsCrowded_BeyondCapacity_IsFalseNotOutOfRange()
    {
        Assert.False(CrowdState.IsCrowded(99999, now: 11f));
    }

    [Fact]
    public void NegativeIndex_IsIgnored()
    {
        CrowdState.Stamp(-1, now: 10f, duration: 2f);
        Assert.False(CrowdState.IsCrowded(-1, now: 11f));
    }

    [Fact]
    public void Reset_ClearsStamps()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        CrowdState.Reset();
        Assert.False(CrowdState.IsCrowded(7, now: 11f));
    }

    [Fact]
    public void Restamp_ExtendsExpiry()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        CrowdState.Stamp(7, now: 11f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(7, now: 12.5f));
    }

    [Fact]
    public void StampLargeIndex_DoesNotHang()
    {
        // Test with index 50000 to ensure the doubling loop terminates (256 -> 512 -> ... -> 65536).
        // This probes the path where newSize must be clamped to prevent overflow.
        CrowdState.Stamp(50000, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(50000, now: 11f));
    }

    [Fact]
    public void AfterLargeIndex_NormalIndexStillWorks()
    {
        // After stamping a very large index, verify that normal-sized indices still query correctly.
        CrowdState.Stamp(50000, now: 10f, duration: 2f);
        CrowdState.Stamp(15, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(15, now: 11f));
        Assert.False(CrowdState.IsCrowded(15, now: 12.1f));
    }
}
