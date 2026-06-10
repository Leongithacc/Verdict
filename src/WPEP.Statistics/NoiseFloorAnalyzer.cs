using WPEP.Core.Benchmark;

namespace WPEP.Statistics;

/// <summary>
/// Quantifies natural run-to-run variance from repeated runs of the SAME
/// configuration (spec §6). Any future "improvement" smaller than this range
/// is noise by definition, and the compare verdict should never beat it.
/// </summary>
public static class NoiseFloorAnalyzer
{
    public const int MinRunsForEstimate = 4;

    public sealed record MetricNoise(
        string Metric, double Median, double Min, double Max)
    {
        public double Range => Max - Min;
        public double RangePercent => Median != 0 ? Range / Median * 100 : 0;
    }

    public sealed record NoiseReport(int Runs, bool Reliable, IReadOnlyList<MetricNoise> Metrics);

    public static NoiseReport Analyze(IReadOnlyList<BenchmarkRun> runs)
    {
        if (runs.Count < 2)
            throw new ArgumentException("Servono almeno 2 run della stessa configurazione.");

        var metrics = new List<MetricNoise>
        {
            Build("Median frametime (ms)", runs, m => m.MedianFrameTimeMs),
            Build("1% low frametime (ms)", runs, m => m.P99FrameTimeMs),
            Build("0.2% low frametime (ms)", runs, m => m.P998FrameTimeMs),
            Build("Avg frametime (ms)", runs, m => m.AvgFrameTimeMs),
        };

        return new NoiseReport(runs.Count, runs.Count >= MinRunsForEstimate, metrics);
    }

    private static MetricNoise Build(
        string name, IReadOnlyList<BenchmarkRun> runs, Func<RunMetrics, double> selector)
    {
        var values = runs.Select(r => selector(r.Metrics)).ToArray();
        return new MetricNoise(name, Bootstrap.Median(values), values.Min(), values.Max());
    }
}
