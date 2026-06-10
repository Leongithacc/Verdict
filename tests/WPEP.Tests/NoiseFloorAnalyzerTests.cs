using WPEP.Core.Benchmark;
using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class NoiseFloorAnalyzerTests
{
    private static BenchmarkRun Run(double medianMs) => new(
        "noise", "game.exe", DateTimeOffset.UnixEpoch, 60,
        new RunMetrics(10_000, 0, medianMs, medianMs, medianMs * 1.5, medianMs * 2, null),
        FrameTimesMs: []);

    [Fact]
    public void Analyze_IdenticalRuns_ZeroRange()
    {
        var report = NoiseFloorAnalyzer.Analyze([Run(10), Run(10), Run(10), Run(10)]);

        Assert.True(report.Reliable);
        Assert.All(report.Metrics, m => Assert.Equal(0, m.Range));
    }

    [Fact]
    public void Analyze_SpreadRuns_RangeAndPercentComputed()
    {
        var report = NoiseFloorAnalyzer.Analyze([Run(10), Run(12), Run(11), Run(10.5)]);

        var median = report.Metrics.Single(m => m.Metric.StartsWith("Median"));
        Assert.Equal(2, median.Range, precision: 10);   // 12 - 10
        Assert.Equal(10.75, median.Median);             // median of 10, 10.5, 11, 12
        Assert.True(median.RangePercent > 18 && median.RangePercent < 19);
    }

    [Fact]
    public void Analyze_FewerThanFourRuns_MarkedUnreliable()
    {
        var report = NoiseFloorAnalyzer.Analyze([Run(10), Run(11)]);
        Assert.False(report.Reliable);
    }

    [Fact]
    public void Analyze_SingleRun_Throws()
    {
        Assert.Throws<ArgumentException>(() => NoiseFloorAnalyzer.Analyze([Run(10)]));
    }
}
