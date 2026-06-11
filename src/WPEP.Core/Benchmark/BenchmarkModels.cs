namespace WPEP.Core.Benchmark;

/// <summary>One presented frame as parsed from PresentMon CSV.</summary>
public readonly record struct FrameSample(double FrameTimeMs, double? GpuBusyMs);

/// <summary>Summary metrics for one capture run. "1% low" follows the spec:
/// it is the 99th percentile of the frametime distribution (and its FPS equivalent),
/// not an average of the slowest frames.</summary>
public sealed record RunMetrics(
    int FrameCount,
    int ExcludedNonApplicationFrames,
    double AvgFrameTimeMs,
    double MedianFrameTimeMs,
    double P99FrameTimeMs,
    double P998FrameTimeMs,
    double? AvgGpuBusyMs)
{
    public double AvgFps => AvgFrameTimeMs > 0 ? 1000.0 / AvgFrameTimeMs : 0;
    public double MedianFps => MedianFrameTimeMs > 0 ? 1000.0 / MedianFrameTimeMs : 0;
    public double OnePercentLowFps => P99FrameTimeMs > 0 ? 1000.0 / P99FrameTimeMs : 0;
    public double ZeroPointTwoPercentLowFps => P998FrameTimeMs > 0 ? 1000.0 / P998FrameTimeMs : 0;
}

/// <summary>Environment fingerprint captured with every run (EDGE_CASES F10):
/// a comparison between runs taken under different environments is invalid
/// and must be blocked, not silently computed.</summary>
public sealed record RunEnvironment(
    string GpuName,
    string GpuDriverVersion,
    int? DisplayWidth,
    int? DisplayHeight,
    int? RefreshHz,
    string PowerPlanGuid)
{
    public string Describe() =>
        $"{GpuName} (driver {GpuDriverVersion}), " +
        $"{DisplayWidth?.ToString() ?? "?"}x{DisplayHeight?.ToString() ?? "?"}@{RefreshHz?.ToString() ?? "?"}Hz, " +
        $"plan {PowerPlanGuid}";
}

/// <summary>A complete benchmark run: metrics plus the full frametime series,
/// kept so the Statistics module can compare whole distributions later.
/// Environment is null only for runs recorded before F10 (legacy).</summary>
public sealed record BenchmarkRun(
    string Label,
    string ProcessName,
    DateTimeOffset CapturedAtUtc,
    double RequestedSeconds,
    RunMetrics Metrics,
    IReadOnlyList<double> FrameTimesMs,
    RunEnvironment? Environment = null);
