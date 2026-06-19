using WPEP.Advisor;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class OptimizeForGameTests
{
    private static TweakEntry E(string id, EvidenceLevel ev, string? game = null) => new()
    {
        Id = id, Name = id.ToUpperInvariant(), Category = "gpu", Description = "d",
        ExpectedImpact = "impact", EvidenceLevel = ev, Sources = ["https://x"],
        Risk = RiskLevel.None, Rollback = "r", ManualSteps = "m", Measurable = true, Game = game,
    };

    private static readonly TweakEntry[] Kb =
    [
        E("sys-strong", EvidenceLevel.EvidenceStrong),
        E("sys-plausible", EvidenceLevel.Plausible),
        E("sys-placebo", EvidenceLevel.Placebo),
        E("sys-risky", EvidenceLevel.Risky),
        E("val-1", EvidenceLevel.EvidenceStrong, "valorant"),
        E("val-2", EvidenceLevel.Plausible, "valorant"),
        E("cs2-1", EvidenceLevel.EvidenceStrong, "cs2"),
    ];

    [Fact]
    public void AvailableGames_ListsDistinctSorted()
    {
        Assert.Equal(["cs2", "valorant"], OptimizeForGame.AvailableGames(Kb));
    }

    [Fact]
    public void Build_SystemTweaks_ExcludePlaceboRiskyAndGameEntries()
    {
        var plan = OptimizeForGame.Build("valorant", Kb);
        Assert.All(plan.SystemTweaks, t => Assert.Null(t.Game));
        Assert.Contains(plan.SystemTweaks, t => t.Id == "sys-strong");
        Assert.Contains(plan.SystemTweaks, t => t.Id == "sys-plausible");
        Assert.DoesNotContain(plan.SystemTweaks, t => t.Id is "sys-placebo" or "sys-risky");
    }

    [Fact]
    public void Build_InGame_OnlyThatGame()
    {
        var plan = OptimizeForGame.Build("valorant", Kb);
        Assert.Equal(2, plan.InGameSettings.Count);
        Assert.All(plan.InGameSettings, t => Assert.Equal("valorant", t.Game));
    }

    [Fact]
    public void Build_UnknownGame_HasSystemButNoInGame()
    {
        var plan = OptimizeForGame.Build("minesweeper", Kb);
        Assert.NotEmpty(plan.SystemTweaks);
        Assert.Empty(plan.InGameSettings);
    }
}
