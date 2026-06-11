using WPEP.Core.Benchmark;
using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class NoiseGateTests
{
    private static BenchmarkRun Run(double medianMs, RunEnvironment? env = null) => new(
        "t", "game.exe", DateTimeOffset.UnixEpoch, 60,
        new RunMetrics(10_000, 0, medianMs, medianMs, medianMs * 1.5, medianMs * 2, null),
        FrameTimesMs: [], Environment: env);

    [Fact]
    public void Mde_TightBaseline_IsSmall()
    {
        // ±1% spread → MDE well under the 10% gate.
        double mde = Mde.Percent([10.0, 10.1, 9.9, 10.05, 9.95]);
        Assert.True(mde < 5, $"MDE atteso piccolo, era {mde:F1}%");
    }

    [Fact]
    public void Mde_FortniteLikeNoise_ExceedsGate()
    {
        // The real Fortnite live medians measured on 2026-06-10 (ms).
        double mde = Mde.Percent([11.69, 8.65, 9.02, 10.78, 11.33]);
        Assert.True(mde > Mde.DefaultGateThresholdPercent,
            $"MDE atteso sopra il gate, era {mde:F1}%");
    }

    [Fact]
    public void Compare_NoisyScenario_EmitsNoVerdictNotNoEffect()
    {
        // Huge apparent delta on a hopeless scenario: the honest output is
        // "no verdict", never "no effect" and never "improvement".
        var baseline = new[] { Run(11.69), Run(8.65), Run(9.02), Run(10.78), Run(11.33) };
        var post = new[] { Run(8.0), Run(11.5), Run(9.5), Run(10.0), Run(8.8) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.True(report.GateTriggered);
        Assert.Equal(Verdict.ScenarioTooNoisy, report.Metrics[0].Verdict);
    }

    [Fact]
    public void Compare_CleanScenario_GateStaysOpen()
    {
        var baseline = new[] { Run(10.0), Run(10.1), Run(9.9), Run(10.05), Run(9.95) };
        var post = new[] { Run(10.02), Run(10.08), Run(9.93), Run(10.0), Run(9.97) };

        var report = ComparisonEngine.Compare(baseline, post);

        Assert.False(report.GateTriggered);
        Assert.Equal(Verdict.NoMeasurableEffect, report.Metrics[0].Verdict);
        Assert.True(report.Metrics[0].MdePercent < 10);
    }

    [Fact]
    public void Compare_GateThresholdIsConfigurable()
    {
        var baseline = new[] { Run(10.0), Run(10.4), Run(9.6), Run(10.2), Run(9.8) }; // ~4-6% MDE
        var post = baseline;

        var strict = ComparisonEngine.Compare(baseline, post, gateThresholdPercent: 1);
        var loose = ComparisonEngine.Compare(baseline, post, gateThresholdPercent: 50);

        Assert.True(strict.GateTriggered);
        Assert.False(loose.GateTriggered);
    }
}

public class EnvironmentValidatorTests
{
    private static readonly RunEnvironment EnvA =
        new("RTX 5080", "32.0.16.1047", 2560, 1440, 240, "guid-a");
    private static readonly RunEnvironment EnvB =
        new("RTX 5080", "33.0.0.1", 2560, 1440, 240, "guid-a"); // driver changed

    private static BenchmarkRun Run(RunEnvironment? env) => new(
        "t", "g.exe", DateTimeOffset.UnixEpoch, 60,
        new RunMetrics(1000, 0, 10, 10, 15, 20, null), [], env);

    [Fact]
    public void Validate_SameEnvironment_IsValid()
    {
        var result = EnvironmentValidator.Validate([Run(EnvA), Run(EnvA)], [Run(EnvA)]);
        Assert.True(result.Valid);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Validate_DriverChangedBetweenGroups_BlocksVerdict()
    {
        var result = EnvironmentValidator.Validate([Run(EnvA)], [Run(EnvB)]);
        Assert.False(result.Valid);
        Assert.Contains("33.0.0.1", result.BlockReason);
    }

    [Fact]
    public void Validate_LegacyRunsWithoutEnvironment_WarnsButDoesNotBlock()
    {
        var result = EnvironmentValidator.Validate([Run(null)], [Run(null)]);
        Assert.True(result.Valid);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public void Validate_DisplayModeChanged_BlocksVerdict()
    {
        var fourK = EnvA with { DisplayWidth = 3840, DisplayHeight = 2160, RefreshHz = 120 };
        var result = EnvironmentValidator.Validate([Run(EnvA)], [Run(fourK)]);
        Assert.False(result.Valid);
    }
}

public class PipelineValidatorTests
{
    private static BenchmarkRun Run(double medianMs) => new(
        "t", "g.exe", DateTimeOffset.UnixEpoch, 60,
        new RunMetrics(10_000, 0, medianMs, medianMs, medianMs * 1.5, medianMs * 2, null), []);

    private static readonly BenchmarkRun[] StableA =
        [Run(10.0), Run(10.1), Run(9.9), Run(10.05), Run(9.95)];
    private static readonly BenchmarkRun[] StableASecond =
        [Run(10.02), Run(10.07), Run(9.94), Run(10.03), Run(9.92)];
    private static readonly BenchmarkRun[] ClearlyFaster =
        [Run(8.0), Run(8.1), Run(7.9), Run(8.05), Run(7.95)];

    [Fact]
    public void AaTest_OnStableIdenticalGroups_Passes()
    {
        var result = PipelineValidator.Run(StableA, StableASecond, PipelineValidator.Expectation.None);
        Assert.True(result.Passed, result.Summary);
        Assert.Contains("PASS", result.Summary);
    }

    [Fact]
    public void KnownEffectTest_WithRealEffect_Passes()
    {
        var result = PipelineValidator.Run(StableA, ClearlyFaster, PipelineValidator.Expectation.Effect);
        Assert.True(result.Passed, result.Summary);
    }

    [Fact]
    public void KnownEffectTest_WithNoEffect_Fails()
    {
        var result = PipelineValidator.Run(StableA, StableASecond, PipelineValidator.Expectation.Effect);
        Assert.False(result.Passed);
        Assert.Contains("FAIL", result.Summary);
    }

    [Fact]
    public void AaTest_OnNoisyScenario_FailsWithNoiseExplanation()
    {
        BenchmarkRun[] noisy = [Run(11.7), Run(8.6), Run(9.0), Run(10.8), Run(11.3)];
        BenchmarkRun[] noisy2 = [Run(9.5), Run(11.0), Run(8.9), Run(10.2), Run(11.6)];

        var result = PipelineValidator.Run(noisy, noisy2, PipelineValidator.Expectation.None);

        Assert.False(result.Passed);
        Assert.Contains("rumoroso", result.Summary);
    }
}
