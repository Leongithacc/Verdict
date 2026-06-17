using System.Diagnostics;
using System.Text.Json;
using WPEP.KnowledgeBase;

namespace WPEP.Execution;

public sealed record PlannedOperation(
    string Path, string Kind, bool ExistedBefore, string? Before, string After);

public sealed record ExecutionPlan(
    string TweakId, string TweakName, string Method, bool RequiresReboot,
    string RiskNotes, IReadOnlyList<PlannedOperation> Operations)
{
    public string Describe() => string.Join("\n", Operations.Select(o =>
        $"  {o.Path}\n    before: {(o.ExistedBefore ? o.Before : "<not set>")}  →  after: {o.After}"));
}

public sealed class JournalEntry
{
    public required string TweakId { get; set; }
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required string Kind { get; set; }
    public bool ExistedBefore { get; set; }
    public string? ValueBefore { get; set; }
    public required string ValueAfter { get; set; }
    public bool Verified { get; set; }
    public bool Undone { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }
}

public sealed class JournalSession
{
    public DateTimeOffset StartedAtUtc { get; set; }
    public bool RestorePointCreated { get; set; }
    public List<JournalEntry> Entries { get; set; } = [];
}

/// <summary>
/// The V2 execution engine, EXECUTION_ENGINE_V2 principles enforced in code:
/// only KB apply specs, dry-run plan with live before-values, journal-before-write,
/// verify-after-write, stop on any incoherence, per-entry undo in reverse order.
/// Supports registry, powercfg and bcdedit methods (service: TODO).
/// </summary>
public sealed class ExecutionEngine(
    IRegistryAccess registry, string journalDirectory,
    IPowerCfg? powerCfg = null, IBcdEdit? bcdEdit = null)
{
    private readonly IPowerCfg _powerCfg = powerCfg ?? new RealPowerCfg();
    private readonly IBcdEdit _bcdEdit = bcdEdit ?? new RealBcdEdit();

    public static string DefaultJournalDirectory =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "data", "journal");

    /// <summary>Dry-run: resolves the exact operations with CURRENT values.
    /// Throws when the entry cannot be applied programmatically.</summary>
    public ExecutionPlan BuildPlan(TweakEntry entry)
    {
        if (entry.Apply is not { } apply || apply.Method == "gui-only")
            throw new InvalidOperationException(
                $"{entry.Id} si applica solo a mano: {entry.Apply?.GuiOnlyReason ?? "nessuna apply spec"}.");
        if (entry.EvidenceLevel == EvidenceLevel.Placebo)
            throw new InvalidOperationException("I placebo non si applicano. Che senso avrebbe?");

        var ops = apply.Method switch
        {
            "registry" => apply.Operations.Select(op =>
            {
                var cur = registry.Read(op.Path);
                return new PlannedOperation(op.Path, op.Kind, cur.Exists, cur.Value,
                    op.ValueAfter ?? throw new InvalidOperationException($"{entry.Id}: value_after mancante"));
            }).ToList(),

            "powercfg" => apply.Operations.Select(op =>
            {
                string current = _powerCfg.GetActiveScheme();
                string target = (op.ValueAfter ?? throw new InvalidOperationException(
                    $"{entry.Id}: value_after (GUID schema) mancante")).ToLowerInvariant();
                return new PlannedOperation("Active power scheme", "powercfg", true, current, target);
            }).ToList(),

            "powercfg-value" => apply.Operations.Select(op =>
            {
                var (subgroup, setting) = SplitPowerPath(op.Path);
                int current = _powerCfg.QuerySettingIndex(subgroup, setting);
                string target = op.ValueAfter ?? throw new InvalidOperationException(
                    $"{entry.Id}: value_after (indice) mancante");
                return new PlannedOperation(op.Path, "powercfg-value", true,
                    current.ToString(), target);
            }).ToList(),

            "bcdedit" => apply.Operations.Select(op =>
            {
                var current = _bcdEdit.Query(op.Path); // op.Path = BCD element name
                string target = (op.ValueAfter ?? throw new InvalidOperationException(
                    $"{entry.Id}: value_after (valore bcdedit) mancante")).Trim().ToLowerInvariant();
                return new PlannedOperation(op.Path, "bcdedit", current.Exists,
                    current.Value, target);
            }).ToList(),

            _ => throw new NotSupportedException(
                $"Metodo '{apply.Method}' non ancora supportato dall'engine (registry, powercfg, bcdedit in questa build)."),
        };

        return new ExecutionPlan(entry.Id, entry.Name, apply.Method,
            apply.RequiresReboot, entry.RiskNotes, ops);
    }

    /// <summary>Applies a plan: journal first, write, verify by re-reading.
    /// Any mismatch stops the session immediately.</summary>
    public string Execute(ExecutionPlan plan)
    {
        var session = new JournalSession
        {
            StartedAtUtc = DateTimeOffset.UtcNow,
            RestorePointCreated = TryCreateRestorePoint($"Verdict: {plan.TweakId}"),
        };
        var file = System.IO.Path.Combine(journalDirectory,
            $"session-{DateTime.Now:yyyyMMdd-HHmmss}-{plan.TweakId}.json");
        System.IO.Directory.CreateDirectory(journalDirectory);

        foreach (var op in plan.Operations)
        {
            var entry = new JournalEntry
            {
                TweakId = plan.TweakId,
                Method = plan.Method,
                Path = op.Path,
                Kind = op.Kind,
                ExistedBefore = op.ExistedBefore,
                ValueBefore = op.Before,
                ValueAfter = op.After,
                AppliedAtUtc = DateTimeOffset.UtcNow,
            };
            session.Entries.Add(entry);
            Save(file, session); // journal BEFORE the write

            string actual = ApplyOne(plan.Method, op.Path, op.Kind, op.After);
            entry.Verified = actual == op.After;
            Save(file, session);

            if (!entry.Verified)
                throw new InvalidOperationException(
                    $"VERIFY FALLITA su {op.Path}: scritto '{op.After}', riletto " +
                    $"'{actual}'. Sessione fermata, journal: {file}");
        }

        return file;
    }

    /// <summary>Applies several plans in sequence (the "Apply all" batch). Each plan
    /// is a normal Execute — own journal, own verify, independently undoable. Stops at
    /// the FIRST plan whose verify fails: already-applied plans stay journaled and
    /// reversible, nothing is rolled back automatically. Returns how many applied and,
    /// if it stopped, which plan stopped it.</summary>
    public (int Applied, string? StoppedAt) ExecuteAll(IReadOnlyList<ExecutionPlan> plans)
    {
        int applied = 0;
        foreach (var plan in plans)
        {
            try { Execute(plan); applied++; }
            catch (Exception ex) { return (applied, $"{plan.TweakName}: {ex.Message}"); }
        }
        return (applied, null);
    }

    /// <summary>Undo a journaled session: reverse order, restore the previous
    /// value (or delete what did not exist), verify each restore.</summary>
    public int Undo(string journalFile)
    {
        var session = JsonSerializer.Deserialize<JournalSession>(
            System.IO.File.ReadAllText(journalFile))
            ?? throw new InvalidDataException($"Journal illeggibile: {journalFile}");

        int restored = 0;
        foreach (var entry in Enumerable.Reverse(session.Entries))
        {
            if (entry.Undone)
                continue;
            switch (entry.Method)
            {
                case "registry" when entry.ExistedBefore:
                    registry.Write(entry.Path, entry.Kind, entry.ValueBefore!);
                    if (registry.Read(entry.Path).Value != entry.ValueBefore)
                        throw new InvalidOperationException($"Undo VERIFY fallita su {entry.Path}.");
                    break;
                case "registry":
                    registry.Delete(entry.Path);
                    break;
                case "powercfg":
                    _powerCfg.SetActiveScheme(entry.ValueBefore!);
                    if (_powerCfg.GetActiveScheme() != entry.ValueBefore)
                        throw new InvalidOperationException("Undo VERIFY fallita sullo schema energetico.");
                    break;
                case "powercfg-value":
                    var (sg, st) = SplitPowerPath(entry.Path);
                    _powerCfg.SetSettingIndex(sg, st, int.Parse(entry.ValueBefore!));
                    if (_powerCfg.QuerySettingIndex(sg, st).ToString() != entry.ValueBefore)
                        throw new InvalidOperationException($"Undo VERIFY fallita su {entry.Path}.");
                    break;
                case "bcdedit" when entry.ExistedBefore:
                    _bcdEdit.Set(entry.Path, entry.ValueBefore!);
                    if (_bcdEdit.Query(entry.Path).Value != entry.ValueBefore)
                        throw new InvalidOperationException($"Undo VERIFY fallita su {entry.Path} (bcdedit).");
                    break;
                case "bcdedit":
                    _bcdEdit.Delete(entry.Path); // back to Windows default
                    if (_bcdEdit.Query(entry.Path).Exists)
                        throw new InvalidOperationException($"Undo VERIFY fallita su {entry.Path}: elemento ancora presente.");
                    break;
            }
            entry.Undone = true;
            restored++;
            Save(journalFile, session);
        }
        return restored;
    }

    public static IReadOnlyList<string> ListSessions(string journalDirectory) =>
        System.IO.Directory.Exists(journalDirectory)
            ? [.. System.IO.Directory.EnumerateFiles(journalDirectory, "session-*.json").Order()]
            : [];

    /// <summary>Performs one write and returns the re-read value for verification.</summary>
    private string ApplyOne(string method, string path, string kind, string after)
    {
        switch (method)
        {
            case "registry":
                registry.Write(path, kind, after);
                return registry.Read(path).Value ?? "<niente>";
            case "powercfg":
                _powerCfg.SetActiveScheme(after);
                return _powerCfg.GetActiveScheme();
            case "powercfg-value":
                var (subgroup, setting) = SplitPowerPath(path);
                _powerCfg.SetSettingIndex(subgroup, setting, int.Parse(after));
                return _powerCfg.QuerySettingIndex(subgroup, setting).ToString();
            case "bcdedit":
                _bcdEdit.Set(path, after);
                return _bcdEdit.Query(path).Value ?? "<niente>";
            default:
                throw new NotSupportedException($"Metodo '{method}' non eseguibile.");
        }
    }

    /// <summary>Power-setting path is "subgroupGuid/settingGuid".</summary>
    private static (string subgroup, string setting) SplitPowerPath(string path)
    {
        var parts = path.Split('/');
        if (parts.Length != 2)
            throw new InvalidOperationException($"Path powercfg-value non valido: {path}");
        return (parts[0], parts[1]);
    }

    private static void Save(string file, JournalSession session) =>
        System.IO.File.WriteAllText(file, JsonSerializer.Serialize(session,
            new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Belt-and-braces: a System Restore checkpoint before writing.
    /// Best effort — requires admin and System Restore enabled.</summary>
    private static bool TryCreateRestorePoint(string description)
    {
        try
        {
            // -WarningAction SilentlyContinue: Windows rate-limits restore points to one
            // per 24h and emits a WARNING when skipped; suppress it (best-effort anyway).
            // Redirect BOTH streams so nothing leaks to the caller's console.
            var psi = new ProcessStartInfo("powershell",
                $"-NoProfile -Command \"Checkpoint-Computer -Description '{description}' " +
                "-RestorePointType MODIFY_SETTINGS -ErrorAction Stop -WarningAction SilentlyContinue\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
