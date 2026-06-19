using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class RegressionSentinelTests
{
    private static ComparisonEngine.ComparisonReport Report(Verdict primary, double delta) =>
        new(BaselineRuns: 5, PostRuns: 5, Conclusive: true, GateThresholdPercent: 10,
            Metrics: [new ComparisonEngine.MetricComparison(
                "Median frametime (ms)", 10, 10 + delta / 10, delta / 10, delta, 0.01,
                new Bootstrap.Interval(delta / 10, 0, 1), 5, primary)]);

    [Fact]
    public void NullReport_NoBaseline()
    {
        var r = RegressionSentinel.Evaluate(null);
        Assert.Equal(SentinelStatus.NoBaseline, r.Status);
    }

    [Fact]
    public void Regression_IsFlaggedDanger()
    {
        var r = RegressionSentinel.Evaluate(Report(Verdict.Regression, 12.0));
        Assert.Equal(SentinelStatus.Regressed, r.Status);
        Assert.Equal("Danger", r.Color);
        Assert.Contains("PEGGIORAT", r.Headline.ToUpperInvariant());
    }

    [Fact]
    public void Improvement_IsPositive()
    {
        var r = RegressionSentinel.Evaluate(Report(Verdict.Improvement, -9.0));
        Assert.Equal(SentinelStatus.Improved, r.Status);
        Assert.Equal("Ok", r.Color);
    }

    [Fact]
    public void NoEffect_IsStable()
    {
        var r = RegressionSentinel.Evaluate(Report(Verdict.NoMeasurableEffect, 0.5));
        Assert.Equal(SentinelStatus.Stable, r.Status);
    }

    [Fact]
    public void GateTriggered_IsInconclusive()
    {
        var r = RegressionSentinel.Evaluate(Report(Verdict.ScenarioTooNoisy, 0));
        Assert.Equal(SentinelStatus.Inconclusive, r.Status);
        Assert.Equal("Warn", r.Color);
    }
}
