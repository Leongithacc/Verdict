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
            ["ms-settings:", "windowsdefender:", "control", "services.msc", "SystemProperties", "mmsys"];
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

    [Fact]
    public void ShippedKb_EveryGameFieldIsInAllowlist()
    {
        // Rete di sicurezza contro i typo silenziosi (es. "warzone2" invece di "warzone")
        // che farebbero fallire GameInstalled → null senza errore visibile all'utente.
        // Se aggiungi un titolo, aggiorna sia questo set che il switch in SystemSnapshot.GameInstalled
        // e la tuple in wpep doctor (Program.cs).
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fortnite", "valorant", "cs2", "apex",
            "overwatch2", "thefinals", "r6siege", "warzone",
        };
        var entries = KnowledgeBaseLoader.Load(KbPath);
        foreach (var e in entries.Where(e => e.Game is not null))
            Assert.True(known.Contains(e.Game!),
                $"{e.Id}: game='{e.Game}' non è tra i noti [{string.Join(", ", known)}]");
    }

    [Fact]
    public void ShippedKb_EverySourceIsAbsoluteHttpsUrl()
    {
        // Rete di sicurezza: una fonte scritta come "www.foo.com" (senza schema) o "learn.microsoft.com/x"
        // (senza https://) passerebbe il validator ma la UI (browser open) fallirebbe silenziosamente.
        var entries = KnowledgeBaseLoader.Load(KbPath);
        foreach (var e in entries)
        {
            foreach (var url in e.Sources)
            {
                Assert.False(string.IsNullOrWhiteSpace(url), $"{e.Id}: source vuoto");
                Assert.True(
                    url.StartsWith("https://", StringComparison.Ordinal) ||
                    url.StartsWith("http://", StringComparison.Ordinal),
                    $"{e.Id}: source non è URL assoluto: '{url}'");
                Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var _),
                    $"{e.Id}: source non è Uri.TryCreate-valido: '{url}'");
            }
        }
    }

    [Fact]
    public void ShippedKb_EveryCategoryIsInAllowlist()
    {
        // Rete di sicurezza contro typo di categoria: una categoria sconosciuta cadrebbe
        // silently in MacroCategory.StabilityQoL come catch-all, invisibile in review.
        // Le 7 categorie devono restare allineate col switch in MacroCategory.Bucket().
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "power", "gpu", "scheduler", "input",
            "network", "background", "security",
        };
        var entries = KnowledgeBaseLoader.Load(KbPath);
        foreach (var e in entries)
            Assert.True(known.Contains(e.Category),
                $"{e.Id}: category='{e.Category}' non è tra le note [{string.Join(", ", known)}]");
    }

    // ── F1: apply-safety validation (audit 2026-07-02) ───────────────────────

    private static TweakEntry WithApply(string method, string path, string? valueAfter, string kind = "dword") =>
        ValidEntry() with
        {
            Apply = new ApplySpec
            {
                Method = method,
                Operations = [new ApplyOperation { Path = path, ValueAfter = valueAfter, Kind = kind }],
            },
        };

    [Theory]
    [InlineData("registry", @"HKLM\SYSTEM\CurrentControlSet\Control\X\Y", "2", "dword")]
    [InlineData("registry", @"HKCU\Control Panel\Mouse\MouseSpeed", "0", "string")]
    [InlineData("powercfg", "active-scheme", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "powercfg")]
    [InlineData("powercfg-value", "501a4d13-42af-4429-9fd1-a8218c268e20/ee12f906-d277-404b-b6da-e5fa1a576df5", "0", "dword")]
    [InlineData("bcdedit", "disabledynamictick", "yes", "bcdedit")]
    [InlineData("nvidia-drs", "0x1057EB71", "0x08416747", "dword")]
    [InlineData("dxuser", "VRROptimizeEnable", "1", "dxuser")]
    public void Validator_WellFormedApplyOp_IsAccepted(string method, string path, string value, string kind)
        => Assert.Empty(KnowledgeBaseValidator.Validate([WithApply(method, path, value, kind)]));

    [Theory]
    // hive fuori allowlist / senza hive
    [InlineData("registry", @"HKCR\Something\Bad", "1", "dword", "registry path")]
    [InlineData("registry", @"Software\NoHive\Value", "1", "dword", "registry path")]
    // dword con valore non numerico
    [InlineData("registry", @"HKLM\A\B", "abc", "dword", "uint")]
    // kind sconosciuto
    [InlineData("registry", @"HKLM\A\B", "1", "binary", "kind")]
    // powercfg value non-GUID
    [InlineData("powercfg", "active-scheme", "not-a-guid", "powercfg", "GUID")]
    // powercfg-value path malformato
    [InlineData("powercfg-value", "solo-un-guid", "0", "dword", "guid/guid")]
    // bcdedit value con spazio → argument-splitting (il punto centrale di F1/F4)
    [InlineData("bcdedit", "disabledynamictick", "yes on", "bcdedit", "value")]
    // bcdedit element con carattere illegale
    [InlineData("bcdedit", "disable-tick", "yes", "bcdedit", "element")]
    // nvidia-drs id non hex/dec
    [InlineData("nvidia-drs", "0xZZZ", "1", "dword", "id")]
    // dxuser value non 0/1
    [InlineData("dxuser", "VRROptimizeEnable", "5", "dxuser", "0 o 1")]
    public void Validator_MalformedApplyOp_IsRejected(
        string method, string path, string value, string kind, string expectedFragment)
    {
        var problems = KnowledgeBaseValidator.Validate([WithApply(method, path, value, kind)]);
        Assert.Contains(problems, p => p.Contains(expectedFragment));
    }

    [Fact]
    public void Validator_BcdEditValueWithQuote_IsRejected()
    {
        // Un apice nel valore bcdedit potrebbe rompere il quoting dell'argomento:
        // deve essere impossibile che una voce così arrivi al Process.Start.
        var problems = KnowledgeBaseValidator.Validate(
            [WithApply("bcdedit", "disabledynamictick", "yes\" extra", "bcdedit")]);
        Assert.Contains(problems, p => p.Contains("value"));
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
