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
/// Supports registry and powercfg methods (bcdedit/service: TODO).
/// </summary>
public sealed class ExecutionEngine(
    IRegistryAccess registry, string journalDirectory, IPowerCfg? powerCfg = null)
{
    private readonly IPowerCfg _powerCfg = powerCfg ?? new RealPowerCfg();

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

            _ => throw new NotSupportedException(
                $"Metodo '{apply.Method}' non ancora supportato dall'engine (registry e powercfg in questa build)."),
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
            default:
                throw new NotSupportedException($"Metodo '{method}' non eseguibile.");
        }
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
            var psi = new ProcessStartInfo("powershell",
                $"-NoProfile -Command \"Checkpoint-Computer -Description '{description}' " +
                "-RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(30000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
