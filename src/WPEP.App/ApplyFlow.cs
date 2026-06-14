using System.Collections.ObjectModel;
using System.IO;
using WPEP.Core.Platform;
using WPEP.Execution;
using WPEP.KnowledgeBase;

namespace WPEP.App;

/// <summary>Wraps the ExecutionEngine for the UI. This is the ONLY place the app
/// writes to the system — and only via KB apply specs, behind a dry-run consent.</summary>
public sealed class ExecutionService
{
    private readonly ExecutionEngine _engine =
        new(new RealRegistryAccess(), ExecutionEngine.DefaultJournalDirectory);

    public bool CanApply(TweakEntry entry) =>
        entry.Apply is { Method: "registry" or "powercfg" } &&
        entry.EvidenceLevel != EvidenceLevel.Placebo;

    public bool NeedsAdmin(TweakEntry entry) =>
        entry.Apply?.Operations.Any(o =>
            o.Path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
            o.Path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) ?? false;

    public ExecutionPlan BuildPlan(TweakEntry entry) => _engine.BuildPlan(entry);
    public string Execute(ExecutionPlan plan) => _engine.Execute(plan);
    public int Undo(string journalFile) => _engine.Undo(journalFile);
    public IReadOnlyList<string> Sessions() =>
        ExecutionEngine.ListSessions(ExecutionEngine.DefaultJournalDirectory);

    public static bool IsElevated => Elevation.IsElevated();

    /// <summary>Opens a Windows settings page / control panel for a gui-only
    /// tweak. Navigation only — never a system write.</summary>
    public static void OpenSettings(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch { /* a missing page must never crash the app */ }
    }
}

/// <summary>The dry-run consent + result dialog (EXECUTION_ENGINE_V2 §2). Shows
/// the EXACT before→after before any write; risky tweaks show the risk text.</summary>
public sealed class ApplyDialogViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly ExecutionService _exec;
    private ExecutionPlan? _plan;
    private string _title = "";
    private string _planText = "";
    private string _riskText = "";
    private string _status = "";
    private bool _isOpen;
    private bool _isRisky;
    private bool _needsAdmin;
    private bool _applied;
    private bool _busy;

    public ApplyDialogViewModel(MainViewModel main, ExecutionService exec)
    {
        _main = main;
        _exec = exec;
        ConfirmCommand = new(Confirm, () => CanConfirm);
        CancelCommand = new(Close);
        RelaunchAsAdminCommand = new(() => _main.Measure.RelaunchAsAdminCommand.Execute(null));
    }

    public bool IsOpen { get => _isOpen; set => Set(ref _isOpen, value); }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string PlanText { get => _planText; set => Set(ref _planText, value); }
    public string RiskText { get => _riskText; set => Set(ref _riskText, value); }
    public bool IsRisky { get => _isRisky; set => Set(ref _isRisky, value); }
    public bool NeedsAdmin { get => _needsAdmin; set => Set(ref _needsAdmin, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool Applied { get => _applied; set { Set(ref _applied, value); Raise(nameof(CanConfirm)); } }
    public bool IsBusy { get => _busy; set { Set(ref _busy, value); Raise(nameof(CanConfirm)); } }
    public bool AdminBlocked => NeedsAdmin && !ExecutionService.IsElevated;
    public bool CanConfirm => _plan is not null && !Applied && !IsBusy && !AdminBlocked;

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RelaunchAsAdminCommand { get; }

    public void Open(TweakEntry entry)
    {
        Applied = false;
        Status = "";
        Title = entry.Name;
        IsRisky = entry.Risk is RiskLevel.High or RiskLevel.Medium ||
                  entry.EvidenceLevel == EvidenceLevel.Risky;
        RiskText = entry.RiskNotes;
        NeedsAdmin = _exec.NeedsAdmin(entry);
        try
        {
            _plan = _exec.BuildPlan(entry);
            PlanText = _plan.Describe() +
                (_plan.RequiresReboot ? "\n\nRequires a reboot to take effect." : "");
            if (AdminBlocked)
                Status = "This change writes to HKLM and needs administrator. " +
                         "Relaunch as administrator first.";
        }
        catch (Exception ex)
        {
            _plan = null;
            PlanText = "";
            Status = ex.Message;
        }
        Raise(nameof(AdminBlocked));
        Raise(nameof(CanConfirm));
        IsOpen = true;
    }

    private void Confirm()
    {
        if (_plan is null)
            return;
        IsBusy = true;
        try
        {
            var file = _exec.Execute(_plan);
            Applied = true;
            Status = $"Applied and verified. Undo available in Changes.\nJournal: {Path.GetFileName(file)}";
            _main.TerminalLine = $"$ verdict apply {_plan.TweakId} · {_plan.Operations.Count} writes · journaled";
            _main.Changes.Refresh();
        }
        catch (Exception ex)
        {
            Status = $"STOPPED: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Close()
    {
        bool wasApplied = Applied;
        IsOpen = false;
        if (wasApplied)
            _ = _main.Verdict.ScanAsync();
    }
}

public sealed record ChangeSession(string File, string Display, string Detail, bool AllUndone);

/// <summary>The Changes page: every journaled apply, undoable in reverse.</summary>
public sealed class ChangesViewModel : ViewModelBase
{
    private readonly ExecutionService _exec;
    private string _status = "";

    public ChangesViewModel(ExecutionService exec)
    {
        _exec = exec;
        Refresh();
    }

    public ObservableCollection<ChangeSession> Sessions { get; } = [];
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool IsEmpty => Sessions.Count == 0;

    public void Refresh()
    {
        Sessions.Clear();
        foreach (var file in _exec.Sessions().Reverse())
            Sessions.Add(Describe(file));
        Raise(nameof(IsEmpty));
    }

    private static ChangeSession Describe(string file)
    {
        // Show exactly what each session changed; degrade gracefully if unreadable.
        try
        {
            var session = System.Text.Json.JsonSerializer.Deserialize<WPEP.Execution.JournalSession>(
                File.ReadAllText(file));
            if (session is { Entries.Count: > 0 })
            {
                string tweak = session.Entries[0].TweakId;
                var lines = session.Entries.Select(e =>
                    $"  {e.Path}: {(e.ExistedBefore ? e.ValueBefore : "<not set>")} → {e.ValueAfter}" +
                    (e.Undone ? "  [undone]" : e.Verified ? "  [applied]" : "  [failed]"));
                bool allUndone = session.Entries.All(e => e.Undone);
                string when = session.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                return new ChangeSession(file, $"{tweak} · {when}",
                    string.Join("\n", lines), allUndone);
            }
        }
        catch { /* fall through to filename-only */ }
        return new ChangeSession(file, Path.GetFileNameWithoutExtension(file), "", false);
    }

    public RelayCommand<ChangeSession> UndoCommand => new(session =>
    {
        try
        {
            int n = _exec.Undo(session.File);
            Status = n > 0
                ? $"Undone {n} change(s) from {session.Display}."
                : $"{session.Display} was already undone.";
            Refresh();
        }
        catch (Exception ex)
        {
            Status = $"Undo failed: {ex.Message}";
        }
    });
}
