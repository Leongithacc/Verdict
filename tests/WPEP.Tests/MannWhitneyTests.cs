using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class MannWhitneyTests
{
    [Fact]
    public void Test_SameDistribution_DoesNotRejectNull()
    {
        // Interleaved values from the same series: any p below alpha here
        // would be the tool inventing an effect that does not exist.
        double[] a = [10.1, 10.3, 10.5, 10.7, 10.9];
        double[] b = [10.2, 10.4, 10.6, 10.8, 11.0];

        var result = MannWhitney.Test(a, b);

        Assert.True(result.PValueTwoSided > 0.05,
            $"p={result.PValueTwoSided} su distribuzioni identiche: falso positivo.");
    }

    [Fact]
    public void Test_ClearlySeparatedGroups_RejectsNull()
    {
        // Complete separation, 5v5: the strongest evidence this sample size allows.
        double[] a = [10, 11, 12, 13, 14];
        double[] b = [20, 21, 22, 23, 24];

        var result = MannWhitney.Test(a, b);

        Assert.True(result.PValueTwoSided < 0.05,
            $"p={result.PValueTwoSided} su gruppi completamente separati.");
    }

    [Fact]
    public void Test_IsDeterministicForFixedSeed()
    {
        double[] a = [1, 2, 3, 4, 5];
        double[] b = [2, 3, 4, 5, 6];

        var r1 = MannWhitney.Test(a, b);
        var r2 = MannWhitney.Test(a, b);

        Assert.Equal(r1.PValueTwoSided, r2.PValueTwoSided);
    }

    [Fact]
    public void Test_AllValuesTied_DoesNotCrashAndDoesNotReject()
    {
        double[] same = [5, 5, 5, 5, 5];
        var result = MannWhitney.Test(same, same);
        Assert.True(result.PValueTwoSided > 0.5);
    }

    [Fact]
    public void AverageRanks_TiesGetAverageOfTheirSpan()
    {
        // values: 3, 1, 1, 2 → sorted: 1,1,2,3 → ranks 1.5, 1.5, 3, 4
        var ranks = MannWhitney.AverageRanks([3, 1, 1, 2]);
        Assert.Equal([4, 1.5, 1.5, 3], ranks);
    }
}
