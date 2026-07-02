using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

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
                string[] methods = ["registry", "powercfg", "powercfg-value", "bcdedit", "nvidia-drs", "dxuser", "service", "gui-only"];
                if (!methods.Contains(apply.Method))
                    problems.Add($"{e.Id}: apply.method '{apply.Method}' non valido.");
                if (apply.Method == "gui-only" && string.IsNullOrWhiteSpace(apply.GuiOnlyReason))
                    problems.Add($"{e.Id}: apply gui-only richiede gui_only_reason.");
                if (apply.Method != "gui-only" && apply.Operations.Count == 0)
                    problems.Add($"{e.Id}: apply.{apply.Method} senza operations.");
                if (e.EvidenceLevel == EvidenceLevel.Placebo && apply.Method != "gui-only")
                    problems.Add($"{e.Id}: un placebo non può avere apply programmatico (che senso avrebbe?).");

                // F1 (audit 2026-07-02): apply-safety. Gli honesty-check sopra NON
                // validano i VALORI delle operations, che finiscono in scritture
                // registry e in argomenti di processo (bcdedit/powercfg/nvidia) in
                // contesto admin. Qui li vincoliamo fail-closed: un'op malformata non
                // può mai raggiungere un sink. Le regex combaciano con la forma reale
                // delle 135 voci shippate (verificata empiricamente) e con i test.
                ValidateApplyOperations(e, apply, problems);
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

    // ── F1: apply-safety regex (fail-closed) ─────────────────────────────────
    // Solo hive HKCU/HKLM, poi key\name. Il write registry usa l'API managed
    // (niente shell), quindi qui basta bloccare hive fuori allowlist + newline.
    private static readonly Regex RegistryPath =
        new(@"^(HKCU|HKLM|HKEY_CURRENT_USER|HKEY_LOCAL_MACHINE)\\[^\r\n]+\\[^\r\n]+$", RegexOptions.Compiled);
    private static readonly Regex Guid =
        new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
    private static readonly Regex GuidPair =
        new(@"^[0-9a-fA-F-]{36}/[0-9a-fA-F-]{36}$", RegexOptions.Compiled);
    // bcdedit element/value diventano ARGOMENTI di processo: niente spazi/apici →
    // impossibile l'argument-splitting (chiude alla radice il rischio latente L1/F4).
    private static readonly Regex BcdElement = new(@"^[a-z][a-z0-9]*$", RegexOptions.Compiled);
    private static readonly Regex BcdValue = new(@"^[A-Za-z0-9]+$", RegexOptions.Compiled);
    private static readonly Regex HexOrDec = new(@"^(0x[0-9a-fA-F]+|[0-9]+)$", RegexOptions.Compiled);
    private static readonly Regex Identifier = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private static void ValidateApplyOperations(TweakEntry e, ApplySpec apply, List<string> problems)
    {
        foreach (var op in apply.Operations)
        {
            string path = op.Path ?? "";
            string value = op.ValueAfter ?? "";
            switch (apply.Method)
            {
                case "registry":
                    if (!RegistryPath.IsMatch(path))
                        problems.Add($"{e.Id}: registry path fuori formato (serve HKCU/HKLM\\key\\name): '{path}'.");
                    if (op.Kind is not ("dword" or "string"))
                        problems.Add($"{e.Id}: registry kind '{op.Kind}' non valido (dword|string).");
                    if (op.Kind == "dword" && !uint.TryParse(value, out _))
                        problems.Add($"{e.Id}: registry dword value_after non è un uint: '{value}'.");
                    break;
                case "powercfg":
                    if (!Guid.IsMatch(value))
                        problems.Add($"{e.Id}: powercfg value_after non è un GUID: '{value}'.");
                    break;
                case "powercfg-value":
                    if (!GuidPair.IsMatch(path))
                        problems.Add($"{e.Id}: powercfg-value path non è 'guid/guid': '{path}'.");
                    if (!int.TryParse(value, out _))
                        problems.Add($"{e.Id}: powercfg-value value_after non è un intero: '{value}'.");
                    break;
                case "bcdedit":
                    if (!BcdElement.IsMatch(path))
                        problems.Add($"{e.Id}: bcdedit element fuori formato: '{path}'.");
                    if (!BcdValue.IsMatch(value))
                        problems.Add($"{e.Id}: bcdedit value fuori formato (niente spazi/apici): '{value}'.");
                    break;
                case "nvidia-drs":
                    if (!HexOrDec.IsMatch(path) || !TryParseHexOrDec(path))
                        problems.Add($"{e.Id}: nvidia-drs id non hex/dec valido (uint32): '{path}'.");
                    if (!HexOrDec.IsMatch(value) || !TryParseHexOrDec(value))
                        problems.Add($"{e.Id}: nvidia-drs value non hex/dec valido (uint32): '{value}'.");
                    break;
                case "dxuser":
                    if (!Identifier.IsMatch(path))
                        problems.Add($"{e.Id}: dxuser path non è un identificatore: '{path}'.");
                    if (value is not ("0" or "1"))
                        problems.Add($"{e.Id}: dxuser value_after deve essere 0 o 1: '{value}'.");
                    break;
            }
        }
    }

    /// <summary>True se la stringa è un uint32 in hex ("0x..") o decimale — stessa
    /// grammatica di ExecutionEngine.ParseSettingId/ParseDword, così un valore che
    /// passa la validazione non può poi overflow-are al momento dell'apply.</summary>
    private static bool TryParseHexOrDec(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            : uint.TryParse(s, out _);
}
