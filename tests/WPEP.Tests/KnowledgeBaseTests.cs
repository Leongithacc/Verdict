using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class KnowledgeBaseTests
{
    private static readonly string KbPath = Path.Combine(
        AppContext.BaseDirectory, "kb", "tweaks.json");

    [Fact]
    public void Load_ShippedKb_ParsesAndPassesValidation()
    {
        var entries = KnowledgeBaseLoader.Load(KbPath);
        Assert.True(entries.Count >= 15, $"Attese >=15 voci seed, trovate {entries.Count}");
    }

    [Fact]
    public void ShippedKb_EveryNonPlaceboEntryHasPrimarySources()
    {
        var entries = KnowledgeBaseLoader.Load(KbPath);
        foreach (var e in entries.Where(e => e.EvidenceLevel != EvidenceLevel.Placebo))
            Assert.True(e.Sources.Count > 0, $"{e.Id}: senza fonti");
    }

    [Fact]
    public void ShippedKb_EveryEntryHasStepsAndRollback()
    {
        var entries = KnowledgeBaseLoader.Load(KbPath);
        foreach (var e in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.ManualSteps), $"{e.Id}: manual_steps");
            Assert.False(string.IsNullOrWhiteSpace(e.Rollback), $"{e.Id}: rollback");
        }
    }

    [Fact]
    public void Validator_MissingSourcesOnNonPlacebo_IsRejected()
    {
        var entry = ValidEntry() with { Sources = [], EvidenceLevel = EvidenceLevel.Plausible };
        var problems = KnowledgeBaseValidator.Validate([entry]);
        Assert.Contains(problems, p => p.Contains("fonte"));
    }

    [Fact]
    public void Validator_PlaceboWithoutSources_IsAccepted()
    {
        var entry = ValidEntry() with { Sources = [], EvidenceLevel = EvidenceLevel.Placebo };
        Assert.Empty(KnowledgeBaseValidator.Validate([entry]));
    }

    [Fact]
    public void Validator_HighRiskWithoutNotes_IsRejected()
    {
        var entry = ValidEntry() with { Risk = RiskLevel.High, RiskNotes = "" };
        var problems = KnowledgeBaseValidator.Validate([entry]);
        Assert.Contains(problems, p => p.Contains("risk_notes"));
    }

    [Fact]
    public void Validator_DanglingConflictId_IsRejected()
    {
        var entry = ValidEntry() with { ConflictsWith = ["non-esiste"] };
        var problems = KnowledgeBaseValidator.Validate([entry]);
        Assert.Contains(problems, p => p.Contains("inesistente"));
    }

    [Fact]
    public void Validator_DuplicateIds_AreRejected()
    {
        var problems = KnowledgeBaseValidator.Validate([ValidEntry(), ValidEntry()]);
        Assert.Contains(problems, p => p.Contains("duplicato"));
    }

    private static TweakEntry ValidEntry() => new()
    {
        Id = "test-entry",
        Name = "Test",
        Category = "power",
        Description = "desc",
        ExpectedImpact = "impact",
        EvidenceLevel = EvidenceLevel.Plausible,
        Sources = ["https://learn.microsoft.com/x"],
        Risk = RiskLevel.Low,
        Rollback = "undo",
        ManualSteps = "do",
        Measurable = true,
    };
}
