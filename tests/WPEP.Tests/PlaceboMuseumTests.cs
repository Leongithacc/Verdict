using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class PlaceboMuseumTests
{
    private static TweakEntry Entry(string id, EvidenceLevel ev, string cat = "gpu",
        string impact = "Più FPS", string desc = "In realtà non fa nulla.") => new()
    {
        Id = id, Name = id.ToUpperInvariant(), Category = cat, Description = desc,
        ExpectedImpact = impact, EvidenceLevel = ev, Sources = ["https://x"],
        Risk = RiskLevel.None, Rollback = "r", ManualSteps = "m", Measurable = false,
    };

    [Fact]
    public void Build_KeepsOnlyPlacebos()
    {
        var m = PlaceboMuseum.Build([
            Entry("real", EvidenceLevel.EvidenceStrong),
            Entry("myth", EvidenceLevel.Placebo),
            Entry("risky", EvidenceLevel.Risky),
        ]);
        Assert.Single(m);
        Assert.Equal("myth", m[0].Id);
    }

    [Fact]
    public void Exhibit_MapsMythAndTruth()
    {
        var m = PlaceboMuseum.Build([Entry("x", EvidenceLevel.Placebo,
            impact: "Promette -50% input lag", desc: "Misurato: zero differenza.")]);
        Assert.Equal("Promette -50% input lag", m[0].Myth);
        Assert.Equal("Misurato: zero differenza.", m[0].Truth);
    }

    [Fact]
    public void Build_SortsByCategoryThenName()
    {
        var m = PlaceboMuseum.Build([
            Entry("zebra", EvidenceLevel.Placebo, cat: "audio"),
            Entry("alpha", EvidenceLevel.Placebo, cat: "gpu"),
            Entry("beta", EvidenceLevel.Placebo, cat: "audio"),
        ]);
        Assert.Equal(["BETA", "ZEBRA", "ALPHA"], m.Select(e => e.Name));
    }

    [Fact]
    public void Count_MatchesPlacebos()
    {
        var e = new[] { Entry("a", EvidenceLevel.Placebo), Entry("b", EvidenceLevel.Plausible) };
        Assert.Equal(1, PlaceboMuseum.Count(e));
    }

    [Fact]
    public void EmptyImpact_GetsAFallbackMyth()
    {
        var m = PlaceboMuseum.Build([Entry("x", EvidenceLevel.Placebo, impact: "")]);
        Assert.False(string.IsNullOrWhiteSpace(m[0].Myth));
    }
}
