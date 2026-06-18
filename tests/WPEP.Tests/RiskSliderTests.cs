using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class RiskSliderTests
{
    [Theory]
    [InlineData(RiskTolerance.Safe, 0, true)]      // none-risk in scope at Safe
    [InlineData(RiskTolerance.Safe, 1, false)]     // low-risk out at Safe
    [InlineData(RiskTolerance.Balanced, 1, true)]  // low-risk in at Balanced
    [InlineData(RiskTolerance.Balanced, 2, false)] // medium out at Balanced
    [InlineData(RiskTolerance.Aggressive, 2, true)]
    [InlineData(RiskTolerance.Extreme, 3, true)]   // high-risk in only at Extreme
    [InlineData(RiskTolerance.Aggressive, 3, false)]
    public void Includes_RespectsTier(RiskTolerance tol, int tier, bool expected) =>
        Assert.Equal(expected, RiskSlider.Includes(tol, tier, isPlacebo: false));

    [Theory]
    [InlineData(RiskTolerance.Safe)]
    [InlineData(RiskTolerance.Extreme)]
    public void Placebo_NeverInScope_AtAnyLevel(RiskTolerance tol) =>
        Assert.False(RiskSlider.Includes(tol, riskTier: 0, isPlacebo: true));

    [Fact]
    public void HigherTolerance_NeverExcludesWhatLowerIncluded()
    {
        // Monotonic: widening tolerance only ever adds tweaks, never removes them.
        for (int tier = 0; tier <= 3; tier++)
            for (int lo = 0; lo <= 3; lo++)
                for (int hi = lo; hi <= 3; hi++)
                    if (RiskSlider.Includes((RiskTolerance)lo, tier, false))
                        Assert.True(RiskSlider.Includes((RiskTolerance)hi, tier, false));
    }

    [Fact]
    public void Describe_CoversEveryLevel_WithColor()
    {
        Assert.Equal(4, RiskSlider.AllProfiles.Count);
        Assert.All(RiskSlider.AllProfiles, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Tagline));
            Assert.False(string.IsNullOrWhiteSpace(p.Color));
        });
    }
}
