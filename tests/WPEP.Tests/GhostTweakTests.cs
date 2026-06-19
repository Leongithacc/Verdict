using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class GhostTweakTests
{
    private static readonly string[] Candidates = ["a", "b", "c"];

    [Fact]
    public void Pick_IsDeterministicInSeed()
    {
        Assert.Equal(GhostTweak.Pick(Candidates, 42), GhostTweak.Pick(Candidates, 42));
    }

    [Fact]
    public void Pick_StaysInRange_ForAnySeed()
    {
        foreach (var seed in new[] { 0, 1, -1, int.MaxValue, int.MinValue, 999999 })
            Assert.Contains(GhostTweak.Pick(Candidates, seed), Candidates);
    }

    [Fact]
    public void Pick_EmptyCandidates_Throws()
    {
        Assert.Throws<ArgumentException>(() => GhostTweak.Pick([], 1));
    }

    [Fact]
    public void Reveal_Helped_IsPositive_AndNamesTheTweak()
    {
        var r = GhostTweak.Reveal("HAGS", GhostOutcome.Helped, -8.0);
        Assert.Equal(GhostOutcome.Helped, r.Outcome);
        Assert.Contains("HAGS", r.Plain);
        Assert.Contains("8", r.Plain);          // magnitude shown
        Assert.Equal("Ok", r.Color);
    }

    [Fact]
    public void Reveal_NoEffect_CallsItPlaceboForYou()
    {
        var r = GhostTweak.Reveal("Timer tweak", GhostOutcome.NoEffect, 0);
        Assert.Contains("placebo per te", r.Plain);
    }

    [Fact]
    public void Reveal_Hurt_SaysItWasUndone()
    {
        var r = GhostTweak.Reveal("Risky tweak", GhostOutcome.Hurt, 5);
        Assert.Equal("Danger", r.Color);
        Assert.Contains("annullato", r.Plain);
    }

    [Fact]
    public void Reveal_Inconclusive_AsksForRepeatableScenario()
    {
        var r = GhostTweak.Reveal("X", GhostOutcome.Inconclusive, 0);
        Assert.Equal("Warn", r.Color);
    }
}
