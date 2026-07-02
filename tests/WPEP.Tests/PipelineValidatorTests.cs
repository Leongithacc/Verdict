using WPEP.Core.Benchmark;
using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class PipelineValidatorTests
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
    public void AA_identicalGroups_passes_no_false_positive()
    {
        var a = new[] { Run(10.0, 14.0, 18.0), Run(10.1, 14.1, 18.1),
                        Run(9.9, 13.9, 17.9), Run(10.05, 14.05, 18.05), Run(9.95, 13.95, 17.95) };
        var b = new[] { Run(10.0, 14.0, 18.0), Run(10.1, 14.1, 18.1),
                        Run(9.9, 13.9, 17.9), Run(10.05, 14.05, 18.05), Run(9.95, 13.95, 17.95) };

        var result = PipelineValidator.Run(a, b, PipelineValidator.Expectation.None);

        Assert.True(result.Passed, result.Summary);
    }

    [Fact]
    public void AA_primaryFlat_secondaryMetricFires_still_passes_F7()
    {
        // Median (primary) is identical across groups, but the 0.2% low tail is
        // consistently worse in post. Under the OLD "any of 4 metrics significant"
        // rule this A/A test would FALSELY fail; basing the decision on the primary
        // metric keeps the false-positive rate at ≈ α (audit F7).
        var a = new[] { Run(10.0, 14.0, 18.0), Run(10.05, 14.05, 18.05), Run(9.95, 13.95, 17.95),
                        Run(10.02, 14.02, 18.02), Run(9.98, 13.98, 17.98) };
        var b = new[] { Run(10.0, 14.0, 23.0), Run(10.05, 14.05, 23.05), Run(9.95, 13.95, 22.95),
                        Run(10.02, 14.02, 23.02), Run(9.98, 13.98, 22.98) };

        var report = ComparisonEngine.Compare(a, b);
        // Sanity: the primary (median) is a non-effect, a secondary (0.2% low) fired.
        Assert.Equal(Verdict.NoMeasurableEffect, report.PrimaryVerdict);
        Assert.Equal(Verdict.Regression, report.Metrics[2].Verdict);

        var result = PipelineValidator.Run(a, b, PipelineValidator.Expectation.None);
        Assert.True(result.Passed, "primary metric is flat → A/A must not report a false effect");
    }

    [Fact]
    public void KnownEffect_realImprovement_passes()
    {
        var a = new[] { Run(12.0, 18.0, 25.0), Run(12.2, 18.4, 25.5), Run(11.9, 17.8, 24.8),
                        Run(12.1, 18.2, 25.2), Run(12.0, 18.1, 25.1) };
        var b = new[] { Run(9.5, 14.0, 19.0), Run(9.7, 14.3, 19.4), Run(9.4, 13.9, 18.8),
                        Run(9.6, 14.1, 19.2), Run(9.5, 14.0, 19.1) };

        var result = PipelineValidator.Run(a, b, PipelineValidator.Expectation.Effect);

        Assert.True(result.Passed, result.Summary);
    }

    [Fact]
    public void NoisyScenario_isGated_and_fails()
    {
        // Primary metric swings wildly run-to-run: the scenario can certify nothing.
        var a = new[] { Run(2, 6, 10), Run(18, 24, 30), Run(4, 8, 12), Run(16, 22, 28), Run(10, 15, 20) };
        var b = new[] { Run(3, 7, 11), Run(17, 23, 29), Run(5, 9, 13), Run(15, 21, 27), Run(10, 15, 20) };

        var result = PipelineValidator.Run(a, b, PipelineValidator.Expectation.None);

        Assert.False(result.Passed);
        Assert.Contains("rumoroso", result.Summary);
    }
}
