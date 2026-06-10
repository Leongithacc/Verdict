using WPEP.Core.Benchmark;

namespace WPEP.Statistics;

public enum Verdict
{
    NoMeasurableEffect,
    Improvement,
    Regression,
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
        Verdict Verdict);

    public sealed record ComparisonReport(
        int BaselineRuns,
        int PostRuns,
        bool Conclusive,
        IReadOnlyList<MetricComparison> Metrics);

    public static ComparisonReport Compare(
        IReadOnlyList<BenchmarkRun> baseline, IReadOnlyList<BenchmarkRun> post)
    {
        if (baseline.Count == 0 || post.Count == 0)
            throw new ArgumentException("Servono run sia baseline sia post.");

        var metrics = new List<MetricComparison>
        {
            CompareMetric("Median frametime (ms)", baseline, post, r => r.MedianFrameTimeMs),
            CompareMetric("1% low frametime (ms)", baseline, post, r => r.P99FrameTimeMs),
            CompareMetric("0.2% low frametime (ms)", baseline, post, r => r.P998FrameTimeMs),
            CompareMetric("Avg frametime (ms)", baseline, post, r => r.AvgFrameTimeMs),
        };

        return new ComparisonReport(
            BaselineRuns: baseline.Count,
            PostRuns: post.Count,
            Conclusive: baseline.Count >= MinRunsForConclusion && post.Count >= MinRunsForConclusion,
            Metrics: metrics);
    }

    private static MetricComparison CompareMetric(
        string name,
        IReadOnlyList<BenchmarkRun> baseline, IReadOnlyList<BenchmarkRun> post,
        Func<RunMetrics, double> selector)
    {
        var a = baseline.Select(r => selector(r.Metrics)).ToArray();
        var b = post.Select(r => selector(r.Metrics)).ToArray();

        var mw = MannWhitney.Test(a, b);
        var ci = Bootstrap.DifferenceCi(a, b, Bootstrap.Median);

        double baseMedian = Bootstrap.Median(a);
        double postMedian = Bootstrap.Median(b);
        double delta = postMedian - baseMedian;
        double deltaPct = baseMedian != 0 ? delta / baseMedian * 100 : 0;

        // Golden rule (spec §6): CI containing zero or a non-significant test
        // means the only honest verdict is "no measurable effect".
        Verdict verdict;
        if (ci.IncludesZero || mw.PValueTwoSided > Alpha)
            verdict = Verdict.NoMeasurableEffect;
        else
            verdict = delta < 0 ? Verdict.Improvement : Verdict.Regression;

        return new MetricComparison(
            name, baseMedian, postMedian, delta, deltaPct,
            mw.PValueTwoSided, ci, verdict);
    }
}
