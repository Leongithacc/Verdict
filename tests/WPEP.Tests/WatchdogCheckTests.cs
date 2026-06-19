using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class WatchdogCheckTests
{
    private static WatchInputs In(bool? expoBase = true, bool? expoNow = true,
        int startupBase = 5, int startupNow = 5, DriftItem[]? reverted = null) =>
        new(expoBase, expoNow, startupBase, startupNow, reverted ?? []);

    [Fact]
    public void NothingWrong_ReportsOk()
    {
        var a = WatchdogCheck.Evaluate(In());
        Assert.Single(a);
        Assert.Equal(WatchLevel.Ok, a[0].Level);
        Assert.Equal(WatchLevel.Ok, WatchdogCheck.Worst(a));
    }

    [Fact]
    public void ExpoTurnedOff_WarnsAndDominates()
    {
        var a = WatchdogCheck.Evaluate(In(expoBase: true, expoNow: false));
        Assert.Contains(a, x => x.Level == WatchLevel.Warn && x.Title.Contains("EXPO"));
        Assert.Equal(WatchLevel.Warn, WatchdogCheck.Worst(a));
    }

    [Fact]
    public void ExpoAlreadyOff_NotFlagged()
    {
        // It was off at baseline too → not a regression, no alert.
        var a = WatchdogCheck.Evaluate(In(expoBase: false, expoNow: false));
        Assert.DoesNotContain(a, x => x.Title.Contains("EXPO"));
    }

    [Fact]
    public void RevertedTweak_IsWarned()
    {
        var a = WatchdogCheck.Evaluate(In(reverted:
            [new DriftItem("disable-gamedvr", @"HKCU\...\AppCaptureEnabled", "0", "1")]));
        Assert.Contains(a, x => x.Level == WatchLevel.Warn && x.Title.Contains("disable-gamedvr"));
    }

    [Fact]
    public void StartupGrowth_BelowThreshold_NotFlagged()
    {
        var a = WatchdogCheck.Evaluate(In(startupBase: 5, startupNow: 7)); // +2 < 3
        Assert.DoesNotContain(a, x => x.Title.Contains("avvio"));
    }

    [Fact]
    public void StartupGrowth_AtThreshold_IsInfo()
    {
        var a = WatchdogCheck.Evaluate(In(startupBase: 5, startupNow: 8)); // +3
        Assert.Contains(a, x => x.Level == WatchLevel.Info && x.Title.Contains("avvio"));
    }

    [Fact]
    public void MultipleIssues_AllReported()
    {
        var a = WatchdogCheck.Evaluate(In(expoBase: true, expoNow: false,
            startupBase: 5, startupNow: 10,
            reverted: [new DriftItem("t", "p", "0", "1")]));
        Assert.Equal(3, a.Count);
        Assert.Equal(WatchLevel.Warn, WatchdogCheck.Worst(a));
    }
}
