using NetLimiterLite.Core.Model;
using NetLimiterLite.Core.Throttling;
using Xunit;

namespace NetLimiterLite.Tests;

public class RuleEngineTests
{
    [Fact]
    public void ReturnsNullBucketWhenNoRuleMatches()
    {
        var engine = new RuleEngine();

        var bucket = engine.GetBucket(pid: 1234, processName: "steam.exe", executablePath: null, isDownload: true);

        Assert.Null(bucket);
    }

    [Fact]
    public void MatchesByProcessNameCaseInsensitively()
    {
        var engine = new RuleEngine();
        engine.AddOrUpdateRule(new BandwidthRule
        {
            ProcessMatch = "chrome.exe",
            MaxBytesPerSecondDown = 50_000,
        });

        var bucket = engine.GetBucket(pid: 1, processName: "Chrome.EXE", executablePath: null, isDownload: true);

        Assert.NotNull(bucket);
        Assert.Equal(50_000, bucket!.RatePerSecond);
    }

    [Fact]
    public void DisabledRuleDoesNotApply()
    {
        var engine = new RuleEngine();
        engine.AddOrUpdateRule(new BandwidthRule
        {
            ProcessMatch = "chrome.exe",
            MaxBytesPerSecondDown = 50_000,
            Enabled = false,
        });

        var bucket = engine.GetBucket(pid: 1, processName: "chrome.exe", executablePath: null, isDownload: true);

        Assert.Null(bucket);
    }

    [Fact]
    public void UpAndDownDirectionsAreIndependentBuckets()
    {
        var engine = new RuleEngine();
        engine.AddOrUpdateRule(new BandwidthRule
        {
            ProcessMatch = "game.exe",
            MaxBytesPerSecondDown = 100_000,
            MaxBytesPerSecondUp = 10_000,
        });

        var down = engine.GetBucket(1, "game.exe", null, isDownload: true);
        var up = engine.GetBucket(1, "game.exe", null, isDownload: false);

        Assert.NotSame(down, up);
        Assert.Equal(100_000, down!.RatePerSecond);
        Assert.Equal(10_000, up!.RatePerSecond);
    }

    [Fact]
    public void UpdatingRuleChangesExistingBucketRateInPlace()
    {
        var engine = new RuleEngine();
        engine.AddOrUpdateRule(new BandwidthRule { ProcessMatch = "game.exe", MaxBytesPerSecondDown = 100_000 });
        var bucket = engine.GetBucket(1, "game.exe", null, isDownload: true);

        engine.AddOrUpdateRule(new BandwidthRule { ProcessMatch = "game.exe", MaxBytesPerSecondDown = 20_000 });
        var sameBucket = engine.GetBucket(1, "game.exe", null, isDownload: true);

        Assert.Same(bucket, sameBucket);
        Assert.Equal(20_000, sameBucket!.RatePerSecond);
    }

    [Fact]
    public void MatchFullPathRequiresExecutablePath()
    {
        var engine = new RuleEngine();
        engine.AddOrUpdateRule(new BandwidthRule
        {
            ProcessMatch = @"C:\Games\game.exe",
            MatchFullPath = true,
            MaxBytesPerSecondDown = 1000,
        });

        var noPath = engine.GetBucket(1, "game.exe", executablePath: null, isDownload: true);
        var withPath = engine.GetBucket(1, "game.exe", executablePath: @"C:\Games\game.exe", isDownload: true);

        Assert.Null(noPath);
        Assert.NotNull(withPath);
    }
}
