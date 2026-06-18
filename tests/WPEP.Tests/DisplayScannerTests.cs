using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class DisplayScannerTests
{
    private static DisplayInfo D(int hz, bool primary, int w = 2560, int h = 1440) =>
        new($"{hz}Hz panel", w, h, hz, primary);

    [Fact]
    public void NoDisplays_NoFindings()
    {
        Assert.Empty(DisplayScanner.Analyze([]));
    }

    [Fact]
    public void SingleDisplay_SaysNothingToOptimize()
    {
        var f = DisplayScanner.Analyze([D(240, primary: true)]);
        Assert.Single(f);
        Assert.Equal("Ok", f[0].Level);
    }

    [Fact]
    public void FastestPanelNotPrimary_IsWarned()
    {
        // 240Hz secondary, 60Hz primary → should warn to game on / set primary the 240Hz one.
        var f = DisplayScanner.Analyze([D(60, primary: true), D(240, primary: false)]);
        Assert.Contains(f, x => x.Level == "Warn" && x.Text.Contains("240 Hz"));
    }

    [Fact]
    public void FastestPanelIsPrimary_NoWarn()
    {
        var f = DisplayScanner.Analyze([D(240, primary: true), D(60, primary: false)]);
        Assert.DoesNotContain(f, x => x.Level == "Warn");
    }

    [Fact]
    public void MixedRefresh_IsFlagged()
    {
        var f = DisplayScanner.Analyze([D(240, true), D(144, false), D(60, false)]);
        Assert.Contains(f, x => x.Text.Contains("Refresh misti"));
    }

    [Fact]
    public void MultipleDisplays_SuggestSingleScreenForCompetitive()
    {
        var f = DisplayScanner.Analyze([D(240, true), D(240, false)]);
        Assert.Contains(f, x => x.Text.Contains("Win+P"));
    }
}
