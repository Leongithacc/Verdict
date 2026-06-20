using System.Text.Json;

namespace WPEP.KnowledgeBase;

public static class KnowledgeBaseLoader
{
    public static readonly string DefaultPath =
        Path.Combine(AppContext.BaseDirectory, "kb", "tweaks.json");

    public static IReadOnlyList<TweakEntry> Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Knowledge base non trovata: {path}");

        var entries = JsonSerializer.Deserialize<List<TweakEntry>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Knowledge base vuota o malformata.");

        var problems = KnowledgeBaseValidator.Validate(entries);
        if (problems.Count > 0)
            throw new InvalidDataException(
                "Knowledge base non valida:\n  " + string.Join("\n  ", problems));

        return entries;
    }
}

/// <summary>
/// Enforces the honesty rules of spec §5 at load time: a KB entry that breaks
/// them is a bug, not a content choice.
/// </summary>
public static class KnowledgeBaseValidator
{
    private static readonly string[] Categories =
        ["power", "scheduler", "gpu", "network", "input", "security", "background"];

    public static IReadOnlyList<string> Validate(IReadOnlyList<TweakEntry> entries)
    {
        var problems = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            if (!ids.Add(e.Id))
                problems.Add($"{e.Id}: id duplicato.");
            if (!Categories.Contains(e.Category))
                problems.Add($"{e.Id}: categoria '{e.Category}' non valida.");

            // Spec §5: sources are MANDATORY unless the verdict is placebo.
            if (e.EvidenceLevel != EvidenceLevel.Placebo && e.Sources.Count == 0)
                problems.Add($"{e.Id}: nessuna fonte ma evidence_level={e.EvidenceLevel} (obbligatoria se non placebo).");

            if (e.Sources.Any(s => !s.StartsWith("https://", StringComparison.Ordinal)))
                problems.Add($"{e.Id}: le fonti devono essere URL https.");

            if (string.IsNullOrWhiteSpace(e.ManualSteps))
                problems.Add($"{e.Id}: manual_steps mancanti.");
            if (string.IsNullOrWhiteSpace(e.Rollback))
                problems.Add($"{e.Id}: rollback mancante.");
            if (e.Risk is RiskLevel.Medium or RiskLevel.High && string.IsNullOrWhiteSpace(e.RiskNotes))
                problems.Add($"{e.Id}: risk={e.Risk} richiede risk_notes.");

            // V2 prep: apply specs must be coherent even before the engine exists.
            if (e.Apply is { } apply)
            {
                string[] methods = ["registry", "powercfg", "powercfg-value", "bcdedit", "nvidia-drs", "service", "gui-only"];
                if (!methods.Contains(apply.Method))
                    problems.Add($"{e.Id}: apply.method '{apply.Method}' non valido.");
                if (apply.Method == "gui-only" && string.IsNullOrWhiteSpace(apply.GuiOnlyReason))
                    problems.Add($"{e.Id}: apply gui-only richiede gui_only_reason.");
                if (apply.Method != "gui-only" && apply.Operations.Count == 0)
                    problems.Add($"{e.Id}: apply.{apply.Method} senza operations.");
                if (e.EvidenceLevel == EvidenceLevel.Placebo && apply.Method != "gui-only")
                    problems.Add($"{e.Id}: un placebo non può avere apply programmatico (che senso avrebbe?).");
            }
        }

        foreach (var e in entries)
        foreach (var conflict in e.ConflictsWith)
        {
            if (!ids.Contains(conflict))
                problems.Add($"{e.Id}: conflicts_with riferisce id inesistente '{conflict}'.");
        }

        return problems;
    }
}
