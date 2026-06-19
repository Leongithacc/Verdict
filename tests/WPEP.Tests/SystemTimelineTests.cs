using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class SystemTimelineTests
{
    private static SystemState State(bool? expo = true, double? ram = 32,
        string gpu = "RTX 5080", string bios = "1804", int startup = 5) =>
        new("2026-06-18T20:00:00", expo, ram, gpu, bios, startup);

    [Fact]
    public void Diff_IdenticalStates_NoChanges()
    {
        Assert.Empty(SystemTimeline.Diff(State(), State()));
    }

    [Fact]
    public void Diff_ExpoTurnedOff_IsReported()
    {
        var d = SystemTimeline.Diff(State(expo: true), State(expo: false));
        var c = Assert.Single(d);
        Assert.Equal("RAM EXPO/XMP", c.Field);
        Assert.Equal("attivo", c.Before);
        Assert.Equal("spento", c.After);
    }

    [Fact]
    public void Diff_BiosUpdate_IsReported()
    {
        var d = SystemTimeline.Diff(State(bios: "1804"), State(bios: "1901"));
        Assert.Contains(d, c => c.Field == "BIOS" && c.After == "1901");
    }

    [Fact]
    public void Diff_StartupGrew_IsReported()
    {
        var d = SystemTimeline.Diff(State(startup: 5), State(startup: 9));
        Assert.Contains(d, c => c.Field.Contains("avvio") && c.Before == "5" && c.After == "9");
    }

    [Fact]
    public void Diff_MultipleChanges_AllReported()
    {
        var d = SystemTimeline.Diff(
            State(expo: true, ram: 32, startup: 5),
            State(expo: false, ram: 64, startup: 8));
        Assert.Equal(3, d.Count);
    }

    [Fact]
    public void Diff_UnknownToUnknown_NotAChange()
    {
        var d = SystemTimeline.Diff(State(expo: null), State(expo: null));
        Assert.DoesNotContain(d, c => c.Field == "RAM EXPO/XMP");
    }
}
