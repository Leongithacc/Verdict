using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class BootstrapTests
{
    [Fact]
    public void DifferenceCi_SameData_IncludesZero()
    {
        double[] values = [10, 11, 12, 13, 14];
        var ci = Bootstrap.DifferenceCi(values, values, Bootstrap.Median);

        Assert.True(ci.IncludesZero);
        Assert.Equal(0, ci.PointEstimate);
    }

    [Fact]
    public void DifferenceCi_ClearShift_ExcludesZeroWithCorrectSign()
    {
        double[] baseline = [10, 10.2, 10.4, 10.1, 10.3];
        double[] post = [8, 8.2, 8.4, 8.1, 8.3]; // ~2ms faster

        var ci = Bootstrap.DifferenceCi(baseline, post, Bootstrap.Median);

        Assert.False(ci.IncludesZero);
        Assert.True(ci.Upper < 0, "post più veloce → differenza (post-base) negativa");
        Assert.Equal(-2.0, ci.PointEstimate, precision: 6);
    }

    [Fact]
    public void DifferenceCi_IsDeterministicForFixedSeed()
    {
        double[] a = [1, 2, 3, 4, 5];
        double[] b = [1.5, 2.5, 3.5, 4.5, 5.5];

        var c1 = Bootstrap.DifferenceCi(a, b, Bootstrap.Median);
        var c2 = Bootstrap.DifferenceCi(a, b, Bootstrap.Median);

        Assert.Equal(c1, c2);
    }

    [Theory]
    [InlineData(new double[] { 1, 2, 3 }, 2)]
    [InlineData(new double[] { 1, 2, 3, 4 }, 2.5)]
    [InlineData(new double[] { 7 }, 7)]
    public void Median_OddEvenSingle(double[] values, double expected)
    {
        Assert.Equal(expected, Bootstrap.Median(values));
    }
}
