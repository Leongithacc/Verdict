using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class FeatureCatalogTests
{
    [Fact]
    public void All_HasNoDuplicateIds()
    {
        var ids = FeatureCatalog.All.Select(f => f.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void All_EveryModuleIsFullyPopulated()
    {
        Assert.NotEmpty(FeatureCatalog.All);
        Assert.All(FeatureCatalog.All, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Id));
            Assert.False(string.IsNullOrWhiteSpace(f.Name));
            Assert.False(string.IsNullOrWhiteSpace(f.Tagline));
            Assert.False(string.IsNullOrWhiteSpace(f.Category));
        });
    }

    [Fact]
    public void Get_ReturnsMatchingModule_OrNull()
    {
        Assert.Equal("Verdict Score", FeatureCatalog.Get(FeatureCatalog.Score)?.Name);
        Assert.Null(FeatureCatalog.Get("does-not-exist"));
    }

    [Fact]
    public void HeavyModules_ShipDisabledByDefault()
    {
        // Léon's rule: background/heavy modules must not auto-run — the app stays clean.
        Assert.All(FeatureCatalog.All.Where(f => f.Heavy), f =>
            Assert.False(f.DefaultEnabled, $"{f.Id} is heavy but defaults ON"));
    }

    [Fact]
    public void ComingSoonModules_AreNeverDefaultEnabled()
    {
        // A not-yet-built module must not ship on.
        Assert.All(FeatureCatalog.All.Where(f => !f.Available), f =>
            Assert.False(f.DefaultEnabled, $"{f.Id} is unavailable but defaults ON"));
    }

    [Fact]
    public void ImplementedModules_AreMajority()
    {
        // Sanity: most of the catalog is actually built (14 of 18 at time of writing).
        Assert.True(FeatureCatalog.All.Count(f => f.Available) >= 12);
    }

    [Fact]
    public void PublicConstants_AllResolveToARealModule()
    {
        // Every id constant must point at an actual catalog entry (guards typos/renames).
        string[] ids =
        [
            FeatureCatalog.Score, FeatureCatalog.GhostTweak, FeatureCatalog.TimeMachine,
            FeatureCatalog.RegressionSentinel, FeatureCatalog.Watchdog, FeatureCatalog.OptimizeForGame,
            FeatureCatalog.MultiMonitor, FeatureCatalog.ExplainStutter, FeatureCatalog.RiskSlider,
            FeatureCatalog.ReactionLab, FeatureCatalog.LatencyLab, FeatureCatalog.NetworkDuel,
            FeatureCatalog.RigDna, FeatureCatalog.AiCopilot, FeatureCatalog.TrustMode,
            FeatureCatalog.FreshInstall, FeatureCatalog.EvidenceCommunity, FeatureCatalog.PlaceboMuseum,
        ];
        Assert.All(ids, id => Assert.NotNull(FeatureCatalog.Get(id)));
    }
}
