using WPEP.Core.Benchmark;

namespace WPEP.Statistics;

public enum Verdict
{
    NoMeasurableEffect,
    Improvement,
    Regression,
    /// <summary>The baseline noise exceeds the gate: no verdict can honestly be
    /// emitted for this metric — the measurement, not the tweak, is the problem.</summary>
    ScenarioTooNoisy,
}

/// <summary>
/// Compares baseline vs post benchmark runs, one metric at a time, treating the
/// RUN as the unit of observation — never the pooled frames. Pooling hundreds of
/// thousands of frames makes any microscopic delta "significant"; that is exactly
/// the placebo factory this module exists to prevent (spec §6).
/// </summary>
public static class ComparisonEngine
{
    public const int MinRunsForConclusion = 5;
    public const double Alpha = 0.05;

    public sealed record MetricComparison(
        string Metric,
        double BaselineMedian,
        double PostMedian,
        double DeltaMs,
        double DeltaPercent,
        double PValue,
        Bootstrap.Interval Ci,
        double MdePercent,
        Verdict Verdict);

    public sealed record ComparisonReport(
        int BaselineRuns,
        int PostRuns,
        bool Conclusive,
        double GateThresholdPercent,
        IReadOnlyList<MetricComparison> Metrics)
    {
        /// <summary>True when the primary metric (median frametime) is gated:
        /// the whole comparison should be presented as "no verdict".</summary>
        public bool GateTriggered =>
            Metrics.Count > 0 && Metrics[0].Verdict == Verdict.ScenarioTooNoisy;
    }

    public static ComparisonReport Compare(
        IReadOnlyList<BenchmarkRun> baseline, IReadOnlyList<BenchmarkRun> post,
        double gateThresholdPercent = Mde.DefaultGateThresholdPercent)
    {
        if (baseline.Count == 0 || post.Count == 0)
            throw new ArgumentException("Servono run sia baseline sia post.");

        var metrics = new List<MetricComparison>
        {
            CompareMetric("Median frametime (ms)", baseline, post, r => r.MedianFrameTimeMs, gateThresholdPercent),
            CompareMetric("1% low frametime (ms)", baseline, post, r => r.P99FrameTimeMs, gateThresholdPercent),
            CompareMetric("0.2% low frametime (ms)", baseline, post, r => r.P998FrameTimeMs, gateThresholdPercent),
            CompareMetric("Avg frametime (ms)", baseline, post, r => r.AvgFrameTimeMs, gateThresholdPercent),
        };

        return new ComparisonReport(
            BaselineRuns: baseline.Count,
            PostRuns: post.Count,
            Conclusive: baseline.Count >= MinRunsForConclusion && post.Count >= MinRunsForConclusion,
            GateThresholdPercent: gateThresholdPercent,
            Metrics: metrics);
    }

    private static MetricComparison CompareMetric(
        string name,
        IReadOnlyList<BenchmarkRun> baseline, IReadOnlyList<BenchmarkRun> post,
        Func<RunMetrics, double> selector, double gateThresholdPercent)
    {
        var a = baseline.Select(r => selector(r.Metrics)).ToArray();
        var b = post.Select(r => selector(r.Metrics)).ToArray();

        var mw = MannWhitney.Test(a, b);
        var ci = Bootstrap.DifferenceCi(a, b, Bootstrap.Median);
        double mdePercent = Mde.Percent(a);

        double baseMedian = Bootstrap.Median(a);
        double postMedian = Bootstrap.Median(b);
        double delta = postMedian - baseMedian;
        double deltaPct = baseMedian != 0 ? delta / baseMedian * 100 : 0;

        // Noise gate FIRST (HANDOFF_R7 §1): if the baseline cannot detect effects
        // below the threshold, the honest output is "no verdict", not "no effect".
        Verdict verdict;
        if (mdePercent > gateThresholdPercent)
            verdict = Verdict.ScenarioTooNoisy;
        // Golden rule (spec §6): CI containing zero or a non-significant test
        // means the only honest verdict is "no measurable effect".
        else if (ci.IncludesZero || mw.PValueTwoSided > Alpha)
            verdict = Verdict.NoMeasurableEffect;
        else
            verdict = delta < 0 ? Verdict.Improvement : Verdict.Regression;

        return new MetricComparison(
            name, baseMedian, postMedian, delta, deltaPct,
            mw.PValueTwoSided, ci, mdePercent, verdict);
    }
}
