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

    /// <summary>Every operation is already at its target value: applying would write
    /// the same bytes back (a no-op). Callers surface this honestly instead of
    /// claiming a change was made.</summary>
    public bool IsAlreadyApplied => Operations.Count > 0 &&
        Operations.All(o => o.ExistedBefore && string.Equals(o.Before, o.After, StringComparison.Ordinal));
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

/// <summary>Result of an undo: how many entries were restored, and which were SKIPPED
/// because their current value no longer matches what Verdict wrote (changed outside
/// Verdict) — those are left untouched so a manual edit isn't silently clobbered.</summary>
public sealed record UndoOutcome(int Restored, IReadOnlyList<string> Skipped);

/// <summary>An applied tweak whose live value drifted away from what Verdict wrote.</summary>
public sealed record DriftItem(string TweakId, string Path, string Expected, string Actual);

/// <summary>
/// The V2 execution engine, EXECUTION_ENGINE_V2 principles enforced in code:
/// only KB apply specs, dry-run plan with live before-values, journal-before-write,
/// verify-after-write, stop on any incoherence, per-entry undo in reverse order.
/// Supports registry, powercfg and bcdedit methods (service: TODO).
/// </summary>
public sealed class ExecutionEngine(
    IRegistryAccess registry, string journalDirectory,
    IPowerCfg? powerCfg = null, IBcdEdit? bcdEdit = null, INvidiaDrs? nvidiaDrs = null)
{
    private readonly IPowerCfg _powerCfg = powerCfg ?? new RealPowerCfg();
    private readonly IBcdEdit _bcdEdit = bcdEdit ?? new RealBcdEdit();
    private readonly INvidiaDrs _nvidiaDrs = nvidiaDrs ?? new RealNvidiaDrs();

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

            "nvidia-drs" => apply.Operations.Select(op =>
            {
                // op.Path = NVIDIA DRS setting id (hex, e.g. "0x1057EB71"); value_after = DWORD value.
                var (found, value) = _nvidiaDrs.ReadDword(ParseSettingId(op.Path));
                string target = op.ValueAfter ?? throw new InvalidOperationException(
                    $"{entry.Id}: value_after (valore DRS) mancante");
                return new PlannedOperation(op.Path, "nvidia-drs", found,
                    found ? value.ToString() : null, target);
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

    /// <summary>Undo a journaled session: reverse order, restore the previous value (or
    /// delete what did not exist), verify each restore. DRIFT-AWARE: an entry whose
    /// current value is neither what Verdict wrote nor the original "before" value was
    /// changed outside Verdict — it is SKIPPED (not clobbered) and reported, leaving the
    /// user's manual edit intact. Already-reverted entries are a no-op.</summary>
    public UndoOutcome Undo(string journalFile)
    {
        var session = JsonSerializer.Deserialize<JournalSession>(
            System.IO.File.ReadAllText(journalFile))
            ?? throw new InvalidDataException($"Journal illeggibile: {journalFile}");

        int restored = 0;
        var skipped = new List<string>();
        foreach (var entry in Enumerable.Reverse(session.Entries))
        {
            if (entry.Undone)
                continue;

            var (exists, value) = ReadCurrent(entry);
            bool isCreate = entry.Method is "registry" or "bcdedit" && !entry.ExistedBefore;
            bool alreadyReverted = isCreate ? !exists : (exists && value == entry.ValueBefore);
            bool matchesOurWrite = exists && value == entry.ValueAfter;

            if (alreadyReverted)
            {
                // Nothing to do: the system is already in its pre-Verdict state.
            }
            else if (matchesOurWrite)
            {
                RestoreOne(entry); // restore previous value / delete; throws on verify fail
            }
            else
            {
                // Drift: someone changed this since Verdict applied it. Don't clobber.
                skipped.Add($"{entry.Path}: valore attuale " +
                    $"'{(exists ? value : "<non impostato>")}' modificato fuori da Verdict, non ripristinato");
                continue;
            }

            entry.Undone = true;
            restored++;
            Save(journalFile, session);
        }
        return new UndoOutcome(restored, skipped);
    }

    /// <summary>Panic restore (V3): undo EVERY journaled session, newest first. Drift-aware
    /// (skips what was changed outside Verdict). One button to put the system back.</summary>
    public UndoOutcome UndoAll()
    {
        int restored = 0;
        var skipped = new List<string>();
        foreach (var file in ListSessions(journalDirectory).Reverse())
        {
            try
            {
                var o = Undo(file);
                restored += o.Restored;
                skipped.AddRange(o.Skipped);
            }
            catch (Exception ex) { skipped.Add($"{System.IO.Path.GetFileName(file)}: {ex.Message}"); }
        }
        return new UndoOutcome(restored, skipped);
    }

    /// <summary>Read-only drift check (Watchdog): across every journaled session, find applied
    /// tweaks whose live value no longer matches what Verdict wrote — i.e. something reverted them
    /// (a Windows update, a driver reinstall, a manual edit). Writes nothing; just reports.</summary>
    public IReadOnlyList<DriftItem> DetectDrift()
    {
        var drifted = new List<DriftItem>();
        foreach (var file in ListSessions(journalDirectory))
        {
            JournalSession? session;
            try { session = JsonSerializer.Deserialize<JournalSession>(System.IO.File.ReadAllText(file)); }
            catch { continue; }
            if (session is null) continue;

            foreach (var entry in session.Entries)
            {
                if (entry.Undone) continue;
                // One unreadable entry (e.g. a bcdedit value needing admin) must not sink the whole
                // check — skip it rather than throw. Drift detection is best-effort by nature.
                bool exists; string? value;
                try { (exists, value) = ReadCurrent(entry); }
                catch { continue; }
                bool stillApplied = exists && value == entry.ValueAfter;
                if (!stillApplied)
                    drifted.Add(new DriftItem(entry.TweakId, entry.Path,
                        entry.ValueAfter, exists ? value ?? "" : "<non impostato>"));
            }
        }
        return drifted;
    }

    /// <summary>Reads the current live value for a journal entry (per method).</summary>
    private (bool Exists, string? Value) ReadCurrent(JournalEntry entry)
    {
        switch (entry.Method)
        {
            case "registry":
                var r = registry.Read(entry.Path);
                return (r.Exists, r.Value);
            case "powercfg":
                return (true, _powerCfg.GetActiveScheme());
            case "powercfg-value":
                var (sg, st) = SplitPowerPath(entry.Path);
                return (true, _powerCfg.QuerySettingIndex(sg, st).ToString());
            case "bcdedit":
                var b = _bcdEdit.Query(entry.Path);
                return (b.Exists, b.Value);
            case "nvidia-drs":
                var (found, value) = _nvidiaDrs.ReadDword(ParseSettingId(entry.Path));
                return (found, found ? value.ToString() : null);
            default:
                return (false, null);
        }
    }

    /// <summary>Performs one restore (previous value, or delete what we created) and
    /// verifies it. Only called when the current value is still what Verdict wrote.</summary>
    private void RestoreOne(JournalEntry entry)
    {
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
            case "nvidia-drs" when entry.ExistedBefore:
                var idR = ParseSettingId(entry.Path);
                _nvidiaDrs.WriteDword(idR, uint.Parse(entry.ValueBefore!));
                var (fR, vR) = _nvidiaDrs.ReadDword(idR);
                if (!fR || vR.ToString() != entry.ValueBefore)
                    throw new InvalidOperationException($"Undo VERIFY fallita su {entry.Path} (nvidia-drs).");
                break;
            case "nvidia-drs":
                _nvidiaDrs.DeleteSetting(ParseSettingId(entry.Path)); // back to driver default
                break;
        }
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
            case "nvidia-drs":
                uint id = ParseSettingId(path);
                _nvidiaDrs.WriteDword(id, uint.Parse(after));
                var (found, value) = _nvidiaDrs.ReadDword(id);
                return found ? value.ToString() : "<niente>";
            default:
                throw new NotSupportedException($"Metodo '{method}' non eseguibile.");
        }
    }

    /// <summary>NVIDIA DRS setting id from a hex (e.g. "0x1057EB71") or decimal string.</summary>
    private static uint ParseSettingId(string path)
    {
        path = path.Trim();
        return path.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(path[2..], 16)
            : uint.Parse(path);
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
