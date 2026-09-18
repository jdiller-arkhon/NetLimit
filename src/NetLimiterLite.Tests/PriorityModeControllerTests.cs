using NetLimiterLite.Core.Model;
using NetLimiterLite.Core.Throttling;
using Xunit;

namespace NetLimiterLite.Tests;

public class PriorityModeControllerTests
{
    [Fact]
    public void NoRulesWhenDisabled()
    {
        var profile = GamingPriorityProfile.Destiny2();
        profile.Enabled = false;
        var controller = new PriorityModeController(profile);

        var rules = controller.BuildRules(new[] { "destiny2.exe", "chrome.exe" });

        Assert.Empty(rules);
    }

    [Fact]
    public void NoRulesWhenPriorityProcessNotRunning()
    {
        var profile = GamingPriorityProfile.Destiny2();
        profile.Enabled = true;
        var controller = new PriorityModeController(profile);

        var rules = controller.BuildRules(new[] { "chrome.exe", "steam.exe" });

        Assert.Empty(rules);
    }

    [Fact]
    public void CapsEverythingExceptDestinyWhileItRuns()
    {
        var profile = GamingPriorityProfile.Destiny2(backgroundCapDownKBps: 100, backgroundCapUpKBps: 20);
        profile.Enabled = true;
        var controller = new PriorityModeController(profile);

        var rules = controller.BuildRules(new[] { "destiny2.exe", "bungielauncher.exe", "chrome.exe", "steam.exe" });

        Assert.Equal(2, rules.Count);
        Assert.All(rules, r => Assert.Equal(100 * 1024, r.MaxBytesPerSecondDown));
        Assert.All(rules, r => Assert.Equal(20 * 1024, r.MaxBytesPerSecondUp));
        Assert.Contains(rules, r => r.ProcessMatch.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(rules, r => r.ProcessMatch.Equals("steam.exe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rules, r => r.ProcessMatch.Equals("destiny2.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BungieLauncherAloneCountsAsPriorityProcessRunning()
    {
        var profile = GamingPriorityProfile.Destiny2();
        profile.Enabled = true;
        var controller = new PriorityModeController(profile);

        var isActive = controller.IsProfileActive(new[] { "bungielauncher.exe", "chrome.exe" });

        Assert.True(isActive);
    }
}
