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
        // This probes normal multi-doubling growth, not the overflow-clamp path
        // (which requires indices near 2^31 and cannot be tested via Stamp without OOM).
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

    [Fact]
    public void ComputeNewSize_NormalGrow()
    {
        // Test normal doubling growth: 256 -> 512 -> 1024 -> 2048 -> 4096 -> 8192.
        // Index 5000 should fit in 8192.
        int newSize = CrowdState.ComputeNewSize(currentLength: 256, index: 5000);
        Assert.Equal(8192, newSize);
    }

    [Fact]
    public void ComputeNewSize_AlreadyLargeEnough()
    {
        // If currentLength already exceeds index, return it unchanged.
        int newSize = CrowdState.ComputeNewSize(currentLength: 1000, index: 500);
        Assert.Equal(1000, newSize);
    }

    [Fact]
    public void ComputeNewSize_ExactBoundary()
    {
        // If currentLength == index, it's still too small (must fit index, so need > index).
        // 256 <= 256 is true, so we double to 512.
        int newSize = CrowdState.ComputeNewSize(currentLength: 256, index: 256);
        Assert.Equal(512, newSize);
    }

    [Fact]
    public void ComputeNewSize_OverflowClamp()
    {
        // At overflow boundary: currentLength just above int.MaxValue/2 cannot double.
        // Should clamp to int.MaxValue and not loop forever.
        int boundary = int.MaxValue / 2 + 1;  // 1073741824
        int newSize = CrowdState.ComputeNewSize(currentLength: boundary, index: int.MaxValue);
        Assert.Equal(int.MaxValue, newSize);
        Assert.True(newSize > 0); // Verify it's not negative (no wraparound).
    }

    [Fact]
    public void ComputeNewSize_MaxValueIndex()
    {
        // Requesting int.MaxValue should clamp the result to int.MaxValue,
        // not loop forever or wrap around.
        int newSize = CrowdState.ComputeNewSize(currentLength: 256, index: int.MaxValue);
        Assert.Equal(int.MaxValue, newSize);
    }
}
