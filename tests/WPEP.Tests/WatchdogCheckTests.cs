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

    [Fact]
    public void Evaluate_PopulatesStableKeys()
    {
        var a = WatchdogCheck.Evaluate(In(expoBase: true, expoNow: false,
            startupBase: 5, startupNow: 10,
            reverted: [new DriftItem("disable-gamedvr", "p", "0", "1")]));
        Assert.Contains(a, x => x.Key == "expo");
        Assert.Contains(a, x => x.Key == "startup");
        Assert.Contains(a, x => x.Key == "revert:disable-gamedvr");
    }

    // ── WatchdogMonitor: the anti-spam de-dupe that the tray loop sits on ──────────
    private static IReadOnlyList<WatchAlert> Pass(WatchInputs i) => WatchdogCheck.Evaluate(i);

    [Fact]
    public void Monitor_FirstWarn_IsFresh_ThenSilentWhileUnchanged()
    {
        var m = new WatchdogMonitor();
        var expoOff = In(expoBase: true, expoNow: false);

        var first = m.Update(Pass(expoOff));
        Assert.Contains(first, x => x.Key == "expo");

        // Same condition next pass → nothing new to notify (no spam).
        Assert.Empty(m.Update(Pass(expoOff)));
    }

    [Fact]
    public void Monitor_OkPass_NeverNotifies()
    {
        var m = new WatchdogMonitor();
        Assert.Empty(m.Update(Pass(In()))); // all fine → Ok only → no actionable alerts
    }

    [Fact]
    public void Monitor_ResolvedThenReturns_NotifiesAgain()
    {
        var m = new WatchdogMonitor();
        var expoOff = In(expoBase: true, expoNow: false);

        Assert.Single(m.Update(Pass(expoOff)));   // appears → notify
        Assert.Empty(m.Update(Pass(In())));        // resolved → clears, nothing to notify
        Assert.Single(m.Update(Pass(expoOff)));   // returns → notify again
    }

    [Fact]
    public void Monitor_NewIssueAlongsideExisting_OnlyTheNewOneIsFresh()
    {
        var m = new WatchdogMonitor();
        // Pass 1: only EXPO off.
        m.Update(Pass(In(expoBase: true, expoNow: false)));
        // Pass 2: EXPO still off + a reverted tweak appears → only the revert is fresh.
        var fresh = m.Update(Pass(In(expoBase: true, expoNow: false,
            reverted: [new DriftItem("hags", "p", "0", "1")])));
        Assert.Single(fresh);
        Assert.Equal("revert:hags", fresh[0].Key);
    }
}
