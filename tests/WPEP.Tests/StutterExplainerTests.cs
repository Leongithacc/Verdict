using WPEP.Core.Diagnostics;
using WPEP.Diagnostics;
using Xunit;

namespace WPEP.Tests;

public class StutterExplainerTests
{
    private static DpcIsrReport Report(params DriverStats[] drivers) => new()
    {
        CaptureDurationSeconds = 15,
        TotalEvents = drivers.Sum(d => d.TotalCount),
        UnresolvedEvents = 0,
        Drivers = drivers,
    };

    private static DriverStats Drv(string name, double maxUs, long spikes500 = 0, long spikes1000 = 0) =>
        new() { Driver = name, DpcCount = 100, MaxUs = maxUs, TotalUs = 1000,
                SpikesOver500Us = spikes500, SpikesOver1000Us = spikes1000 };

    [Fact]
    public void HealthySystem_IsClean_NoFindings()
    {
        // Mirrors Léon's real result: nvlddmkm max ~277µs, zero spikes → no offender.
        var r = StutterExplainer.Explain(Report(Drv("nvlddmkm.sys", 277)));
        Assert.Equal(StutterSeverity.Clean, r.Overall);
        Assert.Empty(r.Findings);
        Assert.Contains("Nessun colpevole", r.Headline);
    }

    [Fact]
    public void DriverWithBigSpike_IsNamedAsSevere()
    {
        var r = StutterExplainer.Explain(Report(Drv("ndis.sys", 1400, spikes500: 5, spikes1000: 2)));
        Assert.Equal(StutterSeverity.Severe, r.Overall);
        var f = Assert.Single(r.Findings);
        Assert.Equal("scheda di rete", f.Component);
    }

    [Fact]
    public void Unresolved_IsIgnored()
    {
        var r = StutterExplainer.Explain(Report(
            Drv(DpcIsrAggregator.UnresolvedKey, 5000, spikes1000: 10)));
        Assert.Equal(StutterSeverity.Clean, r.Overall);
        Assert.Empty(r.Findings);
    }

    [Theory]
    [InlineData("nvlddmkm.sys", "GPU NVIDIA")]
    [InlineData("atikmdag.sys", "GPU AMD")]
    [InlineData("ndis.sys", "scheda di rete")]
    [InlineData("rtkvhd64.sys", "scheda audio")]
    [InlineData("stornvme.sys", "disco / SSD")]
    [InlineData("usbxhci.sys", "controller USB")]
    [InlineData("totallyunknown.sys", "un driver di sistema")]
    public void DescribeDriver_MapsKnownComponents(string driver, string expected) =>
        Assert.Equal(expected, StutterExplainer.DescribeDriver(driver));

    [Fact]
    public void WorstDriver_DrivesOverallSeverity()
    {
        var r = StutterExplainer.Explain(Report(
            Drv("nvlddmkm.sys", 200),                       // clean
            Drv("portcls.sys", 620, spikes500: 1)));        // likely
        Assert.Equal(StutterSeverity.Likely, r.Overall);
        Assert.Single(r.Findings); // only the offender is listed
    }
}
