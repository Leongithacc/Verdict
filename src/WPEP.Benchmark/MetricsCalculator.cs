using WPEP.Core.Benchmark;

namespace WPEP.Benchmark;

public static class MetricsCalculator
{
    /// <summary>Minimum frames below which percentile tails are statistically
    /// meaningless; callers should warn the user, not hide the number.</summary>
    public const int LowSampleThreshold = 500;

    public static RunMetrics Compute(
        IReadOnlyList<FrameSample> samples, int excludedNonApplicationFrames)
    {
        if (samples.Count == 0)
            throw new ArgumentException(
                "Nessun frame catturato: processo non trovato o non presenta frame.",
                nameof(samples));

        var frameTimes = samples.Select(s => s.FrameTimeMs).ToArray();
        var gpuBusy = samples.Where(s => s.GpuBusyMs.HasValue)
                             .Select(s => s.GpuBusyMs!.Value)
                             .ToArray();

        return new RunMetrics(
            FrameCount: samples.Count,
            ExcludedNonApplicationFrames: excludedNonApplicationFrames,
            AvgFrameTimeMs: frameTimes.Average(),
            MedianFrameTimeMs: Percentiles.Compute(frameTimes, 50),
            P99FrameTimeMs: Percentiles.Compute(frameTimes, 99),
            P998FrameTimeMs: Percentiles.Compute(frameTimes, 99.8),
            AvgGpuBusyMs: gpuBusy.Length > 0 ? gpuBusy.Average() : null);
    }
}
