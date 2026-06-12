using WPEP.Core.Benchmark;
using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class OutlierDetectorTests
{
    private static BenchmarkRun Run(double medianMs) => new(
        "t", "g.exe", DateTimeOffset.UnixEpoch, 60,
        new RunMetrics(10_000, 0, medianMs, medianMs, medianMs * 1.5, medianMs * 2, null), []);

    [Fact]
    public void Find_TightGroup_NoOutliers()
    {
        var runs = new[] { Run(10.0), Run(10.1), Run(9.9), Run(10.05), Run(9.95) };
        Assert.Empty(OutlierDetector.Find(runs));
    }

    [Fact]
    public void Find_OneRunFarFromGroup_IsFlaggedWithItsNumber()
    {
        // Léon's real Fortnite case: runs 1-3 ~steady, run 5 a different scene.
        var runs = new[] { Run(1.94), Run(1.97), Run(1.96), Run(1.95), Run(1.10) };
        var outlier = Assert.Single(OutlierDetector.Find(runs));
        Assert.Equal(5, outlier.RunNumber);
    }

    [Fact]
    public void Find_SmallRelativeDeviation_NotFlaggedEvenIfIqrTrips()
    {
        // 2% wiggle on an ultra-tight group: IQR alone would flag it, the
        // practical 10% gate must not.
        var runs = new[] { Run(10.00), Run(10.01), Run(10.00), Run(10.01), Run(10.20) };
        Assert.Empty(OutlierDetector.Find(runs));
    }

    [Fact]
    public void Find_FewerThanFourRuns_ReturnsEmpty()
    {
        Assert.Empty(OutlierDetector.Find([Run(10), Run(50), Run(10)]));
    }
}
