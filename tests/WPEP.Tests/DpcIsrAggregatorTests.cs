using WPEP.Core.Diagnostics;
using WPEP.Diagnostics;
using Xunit;

namespace WPEP.Tests;

public class DpcIsrAggregatorTests
{
    private static DpcIsrAggregator NewAggregator() => new(new DriverMap([
        new DriverModule("nvlddmkm.sys", 0x1000, 0x1000),
        new DriverModule("ndis.sys", 0x5000, 0x1000),
    ]));

    private static DpcIsrEvent Dpc(ulong routine, double durationUs) =>
        new(KernelEventKind.Dpc, routine, Cpu: 0, TimestampMs: 0, durationUs);

    [Fact]
    public void Add_AccumulatesPerDriverStats()
    {
        var agg = NewAggregator();
        agg.Add(Dpc(0x1100, 50));
        agg.Add(Dpc(0x1200, 150));
        agg.Add(Dpc(0x5100, 700));
        agg.Add(new DpcIsrEvent(KernelEventKind.Isr, 0x5100, 0, 0, 1200));

        var report = agg.Build(captureDurationSeconds: 10);

        Assert.Equal(4, report.TotalEvents);
        Assert.Equal(0, report.UnresolvedEvents);

        var nv = Assert.Single(report.Drivers, d => d.Driver == "nvlddmkm.sys");
        Assert.Equal(2, nv.DpcCount);
        Assert.Equal(0, nv.IsrCount);
        Assert.Equal(150, nv.MaxUs);
        Assert.Equal(100, nv.AvgUs);
        Assert.Equal(1, nv.SpikesOver100Us);
        Assert.Equal(0, nv.SpikesOver500Us);

        var ndis = Assert.Single(report.Drivers, d => d.Driver == "ndis.sys");
        Assert.Equal(1, ndis.DpcCount);
        Assert.Equal(1, ndis.IsrCount);
        Assert.Equal(1200, ndis.MaxUs);
        Assert.Equal(2, ndis.SpikesOver500Us);
        Assert.Equal(1, ndis.SpikesOver1000Us);
    }

    [Fact]
    public void Add_UnknownRoutine_CountedAsUnresolvedNotDropped()
    {
        var agg = NewAggregator();
        agg.Add(Dpc(0xDEAD0000, 90));

        var report = agg.Build(1);

        Assert.Equal(1, report.TotalEvents);
        Assert.Equal(1, report.UnresolvedEvents);
        var unresolved = Assert.Single(report.Drivers);
        Assert.Equal(DpcIsrAggregator.UnresolvedKey, unresolved.Driver);
        Assert.Equal(90, unresolved.MaxUs);
    }

    [Fact]
    public void Build_OrdersDriversByMaxDurationDescending()
    {
        var agg = NewAggregator();
        agg.Add(Dpc(0x1100, 10));
        agg.Add(Dpc(0x5100, 999));

        var report = agg.Build(1);

        Assert.Equal("ndis.sys", report.Drivers[0].Driver);
        Assert.Equal("nvlddmkm.sys", report.Drivers[1].Driver);
    }

    [Fact]
    public void Build_EmptyCapture_ReportsZeroEventsHonestly()
    {
        var report = NewAggregator().Build(30);
        Assert.Equal(0, report.TotalEvents);
        Assert.Empty(report.Drivers);
    }
}
