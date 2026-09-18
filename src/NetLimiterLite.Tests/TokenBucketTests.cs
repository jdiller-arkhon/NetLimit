using NetLimiterLite.Core.Throttling;
using Xunit;

namespace NetLimiterLite.Tests;

public class TokenBucketTests
{
    [Fact]
    public void AllowsImmediateConsumeWithinCapacity()
    {
        var bucket = new TokenBucket(ratePerSecond: 1000, capacityBytes: 1000);

        var allowed = bucket.TryConsume(500, out var retryAfter);

        Assert.True(allowed);
        Assert.Equal(TimeSpan.Zero, retryAfter);
    }

    [Fact]
    public void RejectsConsumeBeyondCapacityAndReportsWait()
    {
        var bucket = new TokenBucket(ratePerSecond: 1000, capacityBytes: 1000);
        bucket.TryConsume(1000, out _); // drain

        var allowed = bucket.TryConsume(500, out var retryAfter);

        Assert.False(allowed);
        Assert.True(retryAfter > TimeSpan.Zero);
        // 500 bytes deficit at 1000 B/s = 0.5s
        Assert.InRange(retryAfter.TotalSeconds, 0.4, 0.6);
    }

    [Fact]
    public void RefillsOverTime()
    {
        var bucket = new TokenBucket(ratePerSecond: 1_000_000, capacityBytes: 1_000_000);
        bucket.TryConsume(1_000_000, out _); // drain completely

        Thread.Sleep(50);

        var allowed = bucket.TryConsume(10_000, out _);

        Assert.True(allowed);
    }

    [Fact]
    public void ZeroRateNeverAllows()
    {
        var bucket = new TokenBucket(ratePerSecond: 0);

        var allowed = bucket.TryConsume(1, out var retryAfter);

        Assert.False(allowed);
        Assert.True(retryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void SetRateAdjustsFutureThroughputWithoutExceedingNewCapacity()
    {
        var bucket = new TokenBucket(ratePerSecond: 1000, capacityBytes: 1000);
        bucket.SetRate(100, capacityBytes: 100);

        // Should be clamped down to new capacity, not still holding the old 1000 tokens.
        var allowed = bucket.TryConsume(500, out var retryAfter);

        Assert.False(allowed);
        Assert.True(retryAfter > TimeSpan.Zero);
    }
}
