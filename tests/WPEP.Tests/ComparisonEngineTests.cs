using WPEP.Core.Benchmark;
using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class ComparisonEngineTests
{
    private static BenchmarkRun Run(double medianMs, double p99Ms, double p998Ms) => new(
        "test", "game.exe", DateTimeOffset.UnixEpoch, 60,
        new RunMetrics(
            FrameCount: 10_000,
            ExcludedNonApplicationFrames: 0,
            AvgFrameTimeMs: medianMs,
            MedianFrameTimeMs: medianMs,
            P99FrameTimeMs: p99Ms,
            P998FrameTimeMs: p998Ms,
            AvgGpuBusyMs: null),
        FrameTimesMs: []);

    [Fact]
    public void Compare_RunToRunNoiseOnly_VerdictIsNoMeasurableEffect()
    {
        // Same system measured twice: only natural run-to-run jitter.
        // This is the case the tool MUST get right to not be placebo.
        var baseline = new[] { Run(10.0, 14.1, 18.0), Run(10.2, 14.3, 18.5),
                               Run(9.9, 13.9, 17.8), Run(10.1, 14.2, 18.2), Run(10.0, 14.0, 18.1) };
        var post = new[] { Run(10.1, 14.2, 18.3), Run(9.9, 14.0, 17.9),
                           Run(10.2, 14.1, 18.4), Run(10.0, 13.9, 18.0), Run(10.1, 14.3, 18.2) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.True(report.Conclusive);
        Assert.All(report.Metrics,
            m => Assert.Equal(Verdict.NoMeasurableEffect, m.Verdict));
    }

    [Fact]
    public void Compare_RealImprovement_VerdictIsImprovement()
    {
        var baseline = new[] { Run(12.0, 18.0, 25.0), Run(12.2, 18.4, 25.5),
                               Run(11.9, 17.8, 24.8), Run(12.1, 18.2, 25.2), Run(12.0, 18.1, 25.1) };
        // Post is consistently ~20% faster, far beyond the jitter.
        var post = new[] { Run(9.5, 14.0, 19.0), Run(9.7, 14.3, 19.4),
                           Run(9.4, 13.9, 18.8), Run(9.6, 14.1, 19.2), Run(9.5, 14.0, 19.1) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.All(report.Metrics, m => Assert.Equal(Verdict.Improvement, m.Verdict));
        Assert.All(report.Metrics, m => Assert.True(m.DeltaMs < 0));
    }

    [Fact]
    public void Compare_RealRegression_VerdictIsRegression()
    {
        var baseline = new[] { Run(9.5, 14.0, 19.0), Run(9.7, 14.3, 19.4),
                               Run(9.4, 13.9, 18.8), Run(9.6, 14.1, 19.2), Run(9.5, 14.0, 19.1) };
        var post = new[] { Run(12.0, 18.0, 25.0), Run(12.2, 18.4, 25.5),
                           Run(11.9, 17.8, 24.8), Run(12.1, 18.2, 25.2), Run(12.0, 18.1, 25.1) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.All(report.Metrics, m => Assert.Equal(Verdict.Regression, m.Verdict));
    }

    [Fact]
    public void Compare_FewerThanFiveRuns_MarkedNotConclusive()
    {
        var baseline = new[] { Run(10, 14, 18), Run(10.1, 14.1, 18.1) };
        var post = new[] { Run(9, 13, 17), Run(9.1, 13.1, 17.1) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.False(report.Conclusive);
    }

    [Fact]
    public void Compare_TinyShiftWithinVariance_NotDeclaredImprovement()
    {
        // 0.05ms "improvement" buried in 0.4ms run-to-run spread:
        // declaring this an improvement would be the placebo pattern.
        var baseline = new[] { Run(10.0, 14.0, 18.0), Run(10.4, 14.4, 18.4),
                               Run(9.8, 13.8, 17.8), Run(10.2, 14.2, 18.2), Run(9.9, 13.9, 17.9) };
        var post = new[] { Run(9.95, 13.95, 17.95), Run(10.35, 14.35, 18.35),
                           Run(9.75, 13.75, 17.75), Run(10.15, 14.15, 18.15), Run(9.85, 13.85, 17.85) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.All(report.Metrics,
            m => Assert.Equal(Verdict.NoMeasurableEffect, m.Verdict));
    }
}
