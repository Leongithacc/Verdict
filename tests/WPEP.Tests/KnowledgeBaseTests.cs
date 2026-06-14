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
    public void ShippedKb_ApplySpecsAreCoherent()
    {
        var entries = KnowledgeBaseLoader.Load(KbPath); // validator runs at load
        var withApply = entries.Where(e => e.Apply is not null).ToArray();
        Assert.True(withApply.Length >= 30, $"attese >=30 voci con apply, trovate {withApply.Length}");
        Assert.Contains(withApply, e => e.Apply!.Method == "registry");
        Assert.Contains(withApply, e => e.Apply!.Method == "gui-only");
    }

    [Fact]
    public void Validator_ProgrammaticApplyOnPlacebo_IsRejected()
    {
        var entry = ValidEntry() with
        {
            EvidenceLevel = EvidenceLevel.Placebo,
            Sources = [],
            Apply = new ApplySpec
            {
                Method = "registry",
                Operations = [new ApplyOperation { Path = @"HKCU\X" }],
            },
        };
        var problems = KnowledgeBaseValidator.Validate([entry]);
        Assert.Contains(problems, p => p.Contains("placebo"));
    }

    [Fact]
    public void Validator_GuiOnlyWithoutReason_IsRejected()
    {
        var entry = ValidEntry() with { Apply = new ApplySpec { Method = "gui-only" } };
        var problems = KnowledgeBaseValidator.Validate([entry]);
        Assert.Contains(problems, p => p.Contains("gui_only_reason"));
    }

    [Fact]
    public void ShippedKb_SettingsDeepLinks_AreOnGuiOnlyEntriesAndWellFormed()
    {
        var entries = KnowledgeBaseLoader.Load(KbPath);
        var withUri = entries.Where(e => e.Apply?.SettingsUri is not null).ToArray();
        Assert.True(withUri.Length >= 6, $"attesi >=6 deep-link, trovati {withUri.Length}");
        string[] allowedPrefixes =
            ["ms-settings:", "windowsdefender:", "control", "services.msc", "SystemProperties"];
        foreach (var e in withUri)
        {
            Assert.Equal("gui-only", e.Apply!.Method); // applicable ones don't need a deep-link
            Assert.True(allowedPrefixes.Any(p => e.Apply.SettingsUri!.StartsWith(p)),
                $"{e.Id}: URI inattesa '{e.Apply.SettingsUri}'");
        }
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
