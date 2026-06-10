using WPEP.Benchmark;
using Xunit;

namespace WPEP.Tests;

public class PercentilesTests
{
    [Fact]
    public void Compute_MedianOfOddCount_IsMiddleValue()
    {
        Assert.Equal(3, Percentiles.Compute([5, 1, 3, 2, 4], 50));
    }

    [Fact]
    public void Compute_MedianOfEvenCount_InterpolatesBetweenMiddleValues()
    {
        Assert.Equal(2.5, Percentiles.Compute([1, 2, 3, 4], 50));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(100, 10)]
    public void Compute_Extremes_ReturnMinAndMax(double p, double expected)
    {
        double[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(expected, Percentiles.Compute(values, p));
    }

    [Fact]
    public void Compute_P99OnUniformSeries_InterpolatesLinearly()
    {
        // 1..101: rank for p99 = 0.99 * 100 = 99 → value 100 exactly.
        var values = Enumerable.Range(1, 101).Select(i => (double)i).ToArray();
        Assert.Equal(100, Percentiles.Compute(values, 99), precision: 10);
    }

    [Fact]
    public void Compute_SingleValue_ReturnsThatValueForAnyPercentile()
    {
        Assert.Equal(7, Percentiles.Compute([7], 99.8));
    }

    [Fact]
    public void Compute_EmptySeries_Throws()
    {
        Assert.Throws<ArgumentException>(() => Percentiles.Compute([], 50));
    }
}
