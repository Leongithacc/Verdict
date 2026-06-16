using WPEP.Advisor;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class ConflictResolverTests
{
    private static TweakEntry Entry(string id, EvidenceLevel evidence,
        params string[] conflictsWith) => new()
    {
        Id = id,
        Name = id,
        Category = "gpu",
        Description = "d",
        ExpectedImpact = "i",
        EvidenceLevel = evidence,
        Sources = ["https://x"],
        Risk = RiskLevel.Low,
        Rollback = "r",
        ManualSteps = "m",
        Measurable = true,
        ConflictsWith = conflictsWith,
    };

    [Fact]
    public void KeepsStrongerEvidence_DropsWeakerConflict()
    {
        var strong = Entry("a", EvidenceLevel.EvidenceStrong, "b");
        var weak = Entry("b", EvidenceLevel.Controversial);

        var (keep, dropped) = ConflictResolver.Resolve([strong, weak]);

        Assert.Single(keep);
        Assert.Equal("a", keep[0].Id);
        Assert.Single(dropped);
        Assert.Equal("b", dropped[0].Entry.Id);
        Assert.Equal("a", dropped[0].KeptInstead.Id);
    }

    [Fact]
    public void ConflictIsUndirected_EvenIfOnlyOneSideDeclaresIt()
    {
        // weaker entry "b" declares the conflict; stronger "a" does not.
        var strong = Entry("a", EvidenceLevel.EvidenceStrong);
        var weak = Entry("b", EvidenceLevel.Plausible, "a");

        var (keep, dropped) = ConflictResolver.Resolve([strong, weak]);

        Assert.Equal(["a"], keep.Select(e => e.Id));
        Assert.Equal("b", Assert.Single(dropped).Entry.Id);
    }

    [Fact]
    public void NoConflict_KeepsAll()
    {
        var (keep, dropped) = ConflictResolver.Resolve(
            [Entry("a", EvidenceLevel.EvidenceStrong), Entry("b", EvidenceLevel.Plausible)]);

        Assert.Equal(2, keep.Count);
        Assert.Empty(dropped);
    }

    [Fact]
    public void Tie_KeepsInputOrder_DropsLater()
    {
        var first = Entry("a", EvidenceLevel.Plausible, "b");
        var second = Entry("b", EvidenceLevel.Plausible);

        var (keep, dropped) = ConflictResolver.Resolve([first, second]);

        Assert.Equal(["a"], keep.Select(e => e.Id));
        Assert.Equal("b", Assert.Single(dropped).Entry.Id);
    }
}
