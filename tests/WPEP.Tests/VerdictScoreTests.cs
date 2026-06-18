using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class VerdictScoreTests
{
    [Fact]
    public void PerfectSystem_Scores100()
    {
        var r = VerdictScore.Compute(new(RecommendedDone: 8, RecommendedPending: 0,
            RiskyActive: 0, PlaceboActive: 0, ExpoEnabled: true));
        Assert.Equal(100, r.Score);
        Assert.Equal("Eccellente", r.Band);
    }

    [Fact]
    public void PendingTweaks_DeductButNeverZeroAlone()
    {
        // 100 pending tweaks must not drop the score below the pending cap (54 → 46).
        var r = VerdictScore.Compute(new(0, 100, 0, 0, ExpoEnabled: true));
        Assert.Equal(46, r.Score);
    }

    [Fact]
    public void ExpoOff_IsAReal15PointHit()
    {
        var on = VerdictScore.Compute(new(5, 0, 0, 0, ExpoEnabled: true));
        var off = VerdictScore.Compute(new(5, 0, 0, 0, ExpoEnabled: false));
        Assert.Equal(15, on.Score - off.Score);
        Assert.Contains(off.Breakdown, b => b.Text.Contains("EXPO"));
    }

    [Fact]
    public void Placebo_NeverChangesTheScore()
    {
        // The differentiator: applying placebos must not move the number.
        var without = VerdictScore.Compute(new(4, 2, 0, 0, ExpoEnabled: true));
        var with = VerdictScore.Compute(new(4, 2, 0, PlaceboActive: 9, ExpoEnabled: true));
        Assert.Equal(without.Score, with.Score);
        Assert.Contains("NON contano", with.HonestyNote);
    }

    [Fact]
    public void Score_IsAlwaysClampedToValidRange()
    {
        var worst = VerdictScore.Compute(new(0, 100, 100, 0, ExpoEnabled: false));
        Assert.InRange(worst.Score, 0, 100);
    }

    [Fact]
    public void UnknownExpo_NoPenalty()
    {
        var r = VerdictScore.Compute(new(5, 0, 0, 0, ExpoEnabled: null));
        Assert.Equal(100, r.Score);
        Assert.DoesNotContain(r.Breakdown, b => b.Text.Contains("EXPO"));
    }
}
