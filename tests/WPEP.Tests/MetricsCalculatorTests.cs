using WPEP.Benchmark;
using WPEP.Core.Benchmark;
using Xunit;

namespace WPEP.Tests;

public class MetricsCalculatorTests
{
    [Fact]
    public void Compute_UniformSeries_ProducesConsistentMetrics()
    {
        // 1000 frames at exactly 10ms: every percentile must be 10, FPS 100.
        var samples = Enumerable.Repeat(new FrameSample(10.0, 8.0), 1000).ToArray();

        var m = MetricsCalculator.Compute(samples, excludedNonApplicationFrames: 0);

        Assert.Equal(1000, m.FrameCount);
        Assert.Equal(10.0, m.MedianFrameTimeMs);
        Assert.Equal(10.0, m.P99FrameTimeMs);
        Assert.Equal(10.0, m.P998FrameTimeMs);
        Assert.Equal(100.0, m.AvgFps, precision: 10);
        Assert.Equal(100.0, m.OnePercentLowFps, precision: 10);
        Assert.Equal(8.0, m.AvgGpuBusyMs);
    }

    [Fact]
    public void Compute_SeriesWithStutterTail_TailPercentilesCatchIt()
    {
        // 990 smooth frames + 10 stutter frames: the median must ignore the
        // stutter, the 0.2% low must expose it.
        var samples = Enumerable.Repeat(new FrameSample(10.0, null), 990)
            .Concat(Enumerable.Repeat(new FrameSample(50.0, null), 10))
            .ToArray();

        var m = MetricsCalculator.Compute(samples, 0);

        Assert.Equal(10.0, m.MedianFrameTimeMs);
        Assert.True(m.P998FrameTimeMs >= 49,
            $"0.2% low frametime atteso ~50ms, era {m.P998FrameTimeMs}");
        Assert.True(m.OnePercentLowFps < m.MedianFps);
    }

    [Fact]
    public void Compute_NoGpuData_AvgGpuBusyIsNull()
    {
        var m = MetricsCalculator.Compute([new FrameSample(10, null)], 0);
        Assert.Null(m.AvgGpuBusyMs);
    }

    [Fact]
    public void Compute_EmptySamples_Throws()
    {
        Assert.Throws<ArgumentException>(() => MetricsCalculator.Compute([], 0));
    }
}
