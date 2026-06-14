using System.Diagnostics;
using System.Text.Json;
using WPEP.KnowledgeBase;

namespace WPEP.Execution;

public sealed record PlannedOperation(
    string Path, string Kind, RegistryValue Before, string After);

public sealed record ExecutionPlan(
    string TweakId, string TweakName, string Method, bool RequiresReboot,
    string RiskNotes, IReadOnlyList<PlannedOperation> Operations)
{
    public string Describe() => string.Join("\n", Operations.Select(o =>
        $"  {o.Path}\n    before: {(o.Before.Exists ? o.Before.Value : "<not set>")}  →  after: {o.After}"));
}

public sealed class JournalEntry
{
    public required string TweakId { get; set; }
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
/// </summary>
public sealed class ExecutionEngine(IRegistryAccess registry, string journalDirectory)
{
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
        if (apply.Method != "registry")
            throw new NotSupportedException(
                $"Metodo '{apply.Method}' non ancora supportato dall'engine (solo registry in questa build).");

        var ops = apply.Operations.Select(op => new PlannedOperation(
            op.Path, op.Kind, registry.Read(op.Path),
            op.ValueAfter ?? throw new InvalidOperationException($"{entry.Id}: value_after mancante")))
            .ToList();

        return new ExecutionPlan(entry.Id, entry.Name, apply.Method,
            apply.RequiresReboot, entry.RiskNotes, ops);
    }

    /// <summary>Applies a plan: journal first, write, verify by re-reading.
    /// Any mismatch stops the session immediately — never continue on an
    /// uncertain state. Returns the journal file path.</summary>
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
                Path = op.Path,
                Kind = op.Kind,
                ExistedBefore = op.Before.Exists,
                ValueBefore = op.Before.Value,
                ValueAfter = op.After,
                AppliedAtUtc = DateTimeOffset.UtcNow,
            };
            session.Entries.Add(entry);
            Save(file, session); // journal BEFORE the write

            registry.Write(op.Path, op.Kind, op.After);

            var check = registry.Read(op.Path);
            entry.Verified = check.Exists && check.Value == op.After;
            Save(file, session);

            if (!entry.Verified)
                throw new InvalidOperationException(
                    $"VERIFY FALLITA su {op.Path}: scritto '{op.After}', riletto " +
                    $"'{check.Value ?? "<niente>"}'. Sessione fermata, journal: {file}");
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
            if (entry.ExistedBefore)
            {
                registry.Write(entry.Path, entry.Kind, entry.ValueBefore!);
                var check = registry.Read(entry.Path);
                if (check.Value != entry.ValueBefore)
                    throw new InvalidOperationException($"Undo VERIFY fallita su {entry.Path}.");
            }
            else
            {
                registry.Delete(entry.Path);
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

    private static void Save(string file, JournalSession session) =>
        System.IO.File.WriteAllText(file, JsonSerializer.Serialize(session,
            new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Belt-and-braces: a System Restore checkpoint before writing.
    /// Best effort — requires admin and System Restore enabled; Windows also
    /// rate-limits checkpoints. The journal remains the primary undo.</summary>
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
