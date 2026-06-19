using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class ReactionStatsTests
{
    [Fact]
    public void Empty_ReturnsNeutralPlaceholder()
    {
        var r = ReactionStats.Analyze([]);
        Assert.Equal(0, r.Count);
        Assert.Equal("—", r.Grade);
    }

    [Fact]
    public void ComputesBestAverageMedian()
    {
        var r = ReactionStats.Analyze([200, 180, 220]);
        Assert.Equal(3, r.Count);
        Assert.Equal(180, r.BestMs);
        Assert.Equal(200, r.AverageMs);
        Assert.Equal(200, r.MedianMs);
    }

    [Fact]
    public void Median_ResistsAnOutlier()
    {
        // One fumble (900ms) shouldn't wreck the grade — median ignores it.
        var r = ReactionStats.Analyze([190, 200, 210, 195, 900]);
        Assert.Equal(200, r.MedianMs);
        Assert.Equal("Ottimo", r.Grade);
    }

    [Theory]
    [InlineData(150, "Élite")]
    [InlineData(200, "Ottimo")]
    [InlineData(240, "Buono")]
    [InlineData(300, "Nella media")]
    [InlineData(400, "Lento — c'è margine")]
    public void GradesByMedian(int value, string expectedGrade)
    {
        var r = ReactionStats.Analyze([value]);
        Assert.Equal(expectedGrade, r.Grade);
    }

    [Fact]
    public void EvenCount_MedianIsAverageOfMiddleTwo()
    {
        var r = ReactionStats.Analyze([180, 220]);
        Assert.Equal(200, r.MedianMs);
    }
}
