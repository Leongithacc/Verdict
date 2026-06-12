using WPEP.Core.Benchmark;

namespace WPEP.Statistics;

/// <summary>
/// F5: flags runs whose median frametime deviates wildly from their group
/// (shader compilation, alt-tab, scene change). Flagged, NEVER silently
/// excluded — the user decides, and reports must state what was flagged.
/// </summary>
public static class OutlierDetector
{
    public sealed record Outlier(int RunNumber, double MedianMs, double GroupMedianMs);

    public static IReadOnlyList<Outlier> Find(IReadOnlyList<BenchmarkRun> runs)
    {
        if (runs.Count < 4)
            return [];

        var medians = runs.Select(r => r.Metrics.MedianFrameTimeMs).ToArray();
        var sorted = medians.OrderBy(v => v).ToArray();
        double q1 = sorted[sorted.Length / 4];
        double q3 = sorted[3 * sorted.Length / 4];
        double iqr = Math.Max(q3 - q1, 1e-9);
        double groupMedian = Bootstrap.Median(medians);

        var outliers = new List<Outlier>();
        for (int i = 0; i < runs.Count; i++)
        {
            double deviation = Math.Abs(medians[i] - groupMedian);
            // Both gates: statistical (2×IQR) and practical (>10% relative) —
            // on ultra-tight groups the IQR alone would flag harmless wiggle.
            if (deviation > 2 * iqr && deviation / groupMedian > 0.10)
                outliers.Add(new Outlier(i + 1, medians[i], groupMedian));
        }
        return outliers;
    }
}
