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

    // Single source of truth lives in WPEP.Execution.ApplyPolicy (shared with the CLI).
    public bool CanApply(TweakEntry entry) => ApplyPolicy.CanApply(entry);
    public bool NeedsAdmin(TweakEntry entry) => ApplyPolicy.NeedsAdmin(entry);

    public ExecutionPlan BuildPlan(TweakEntry entry) => _engine.BuildPlan(entry);
    public string Execute(ExecutionPlan plan) => _engine.Execute(plan);
    public (int Applied, string? StoppedAt) ExecuteAll(IReadOnlyList<ExecutionPlan> plans) =>
        _engine.ExecuteAll(plans);
    public UndoOutcome Undo(string journalFile) => _engine.Undo(journalFile);
    public IReadOnlyList<DriftItem> DetectDrift() => _engine.DetectDrift();
    public IReadOnlyList<string> Sessions() =>
        ExecutionEngine.ListSessions(ExecutionEngine.DefaultJournalDirectory);

    public static bool IsElevated => Elevation.IsElevated();

    /// <summary>Opens a Windows settings page / control panel for a gui-only
    /// tweak. Navigation only — never a system write. Handles both URI schemes
    /// (ms-settings:, windowsdefender:) and "command args" forms
    /// (control.exe powercfg.cpl, mmsys.cpl, services.msc).</summary>
    public static void OpenSettings(string uri)
    {
        try
        {
            // URI schemes launch as-is; "command args" forms are split so the
            // arguments aren't treated as part of the file name.
            bool isScheme = uri.Contains(':') && !uri.Contains(' ');
            System.Diagnostics.ProcessStartInfo psi;
            if (isScheme)
            {
                psi = new(uri) { UseShellExecute = true };
            }
            else
            {
                int space = uri.IndexOf(' ');
                psi = space < 0
                    ? new(uri) { UseShellExecute = true }
                    : new(uri[..space], uri[(space + 1)..]) { UseShellExecute = true };
            }
            System.Diagnostics.Process.Start(psi);
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
    public bool Applied { get => _applied; set { Set(ref _applied, value); Raise(nameof(CanConfirm)); Raise(nameof(ShowApplyButton)); } }
    public bool IsBusy { get => _busy; set { Set(ref _busy, value); Raise(nameof(CanConfirm)); } }
    public bool AdminBlocked => NeedsAdmin && !ExecutionService.IsElevated;
    public bool HasPlan => _plan is not null;
    public bool IsAlreadyApplied => _plan?.IsAlreadyApplied == true;
    public bool ShowApplyButton => HasPlan && !Applied && !IsAlreadyApplied;
    public bool CanConfirm => _plan is not null && !Applied && !IsBusy && !AdminBlocked && !IsAlreadyApplied;

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
                (_plan.RequiresReboot ? "\n\nRichiede un riavvio per avere effetto." : "");
            if (_plan.IsAlreadyApplied)
                Status = "Già al valore desiderato: nessuna modifica necessaria.";
            else if (AdminBlocked)
                Status = "Questa modifica scrive in HKLM e serve amministratore. " +
                         "Riavvia come amministratore prima.";
        }
        catch (Exception ex)
        {
            _plan = null;
            PlanText = "";
            Status = "Questo tweak non è applicabile automaticamente — fallo a mano " +
                     "(vedi Come fare). Motivo: " + ex.Message;
        }
        Raise(nameof(AdminBlocked));
        Raise(nameof(HasPlan));
        Raise(nameof(ShowApplyButton));
        Raise(nameof(CanConfirm));
        IsOpen = true;
    }

    private async void Confirm()
    {
        if (_plan is null)
            return;
        IsBusy = true;
        Status = "Scrivo il valore…";
        try
        {
            // Execute fa restore-point + scritture: gira su un thread di background così la UI
            // resta viva (mostra "in corso") invece di congelarsi fino a 12s. La continuazione
            // riprende sul thread UI (binding sicuri).
            var plan = _plan;
            var file = await System.Threading.Tasks.Task.Run(() => _exec.Execute(plan));
            // L'engine ha già riletto per verificare; mostriamo la fase un attimo (percezione del design).
            Status = "Rileggo per verificare…";
            await System.Threading.Tasks.Task.Delay(450);
            Applied = true;
            Status = $"Scritto e verificato. Puoi annullare da Modifiche.\nJournal: {Path.GetFileName(file)}";
            _main.TerminalLine = $"$ verdict apply {plan.TweakId} · {plan.Operations.Count} scritture · journaled";
            _main.Changes.Refresh();
            _main.ShowToast("Scritto e verificato · annullabile da Modifiche", "Ok");
        }
        catch (Exception ex)
        {
            Status = $"FERMATO: {ex.Message}";
            _main.ShowToast("Applicazione fermata · niente è stato lasciato a metà", "Danger");
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

/// <summary>Batch consent dialog: applies ALL recommended+applicable tweaks behind a
/// SINGLE dry-run. Each tweak is still executed, journaled and undoable individually on
/// the Changes page — "Apply all" is a convenience over the same safe per-tweak engine,
/// never an "optimize everything" shortcut. Placebo/risky/gui-only are excluded upstream
/// (only Classification.Recommended ∩ CanApply reaches here). Stops at the first verify
/// failure; everything already applied stays journaled and reversible.</summary>
public sealed class ApplyAllViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly ExecutionService _exec;
    private readonly List<(TweakEntry Entry, ExecutionPlan Plan)> _ready = [];
    private string _title = "";
    private string _planText = "";
    private string _status = "";
    private bool _isOpen;
    private bool _applied;
    private bool _busy;
    private int _adminSkipped;

    public ApplyAllViewModel(MainViewModel main, ExecutionService exec)
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
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool IsBusy { get => _busy; set { Set(ref _busy, value); Raise(nameof(CanConfirm)); } }
    public bool Applied { get => _applied; set { Set(ref _applied, value); Raise(nameof(CanConfirm)); Raise(nameof(ShowApplyButton)); } }
    public bool HasPlan => _ready.Count > 0;
    public bool ShowApplyButton => HasPlan && !Applied;
    public bool AdminBlocked => _adminSkipped > 0 && !ExecutionService.IsElevated;
    public bool CanConfirm => HasPlan && !Applied && !IsBusy;

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RelaunchAsAdminCommand { get; }

    public void Open(IReadOnlyList<TweakEntry> entries)
    {
        Applied = false;
        Status = "";
        _ready.Clear();
        _adminSkipped = 0;
        bool elevated = ExecutionService.IsElevated;
        var lines = new List<string>();
        int reboots = 0;

        // Never apply two mutually-exclusive tweaks in one batch.
        var (kept, conflicts) = WPEP.Advisor.ConflictResolver.Resolve(entries);
        foreach (var d in conflicts)
            lines.Add($"• {d.Entry.Name}\n    [saltato: {d.Reason}]");

        foreach (var e in kept)
        {
            // Honest partition: anything needing admin while unelevated is shown
            // as skipped, not silently dropped.
            if (_exec.NeedsAdmin(e) && !elevated)
            {
                _adminSkipped++;
                lines.Add($"• {e.Name}\n    [serve amministratore — saltato]");
                continue;
            }
            try
            {
                var plan = _exec.BuildPlan(e);
                if (plan.IsAlreadyApplied)
                {
                    lines.Add($"• {e.Name}\n    [già al valore desiderato — niente da fare]");
                    continue;
                }
                _ready.Add((e, plan));
                lines.Add($"• {e.Name}\n{plan.Describe()}");
                if (plan.RequiresReboot)
                    reboots++;
            }
            catch (Exception ex)
            {
                lines.Add($"• {e.Name}\n    [non applicabile automaticamente: {ex.Message}]");
            }
        }

        Title = $"Applica {_ready.Count} tweak consigliati";
        PlanText = string.Join("\n\n", lines) +
            (reboots > 0 ? $"\n\n{reboots} di questi richiedono un riavvio per avere effetto." : "");
        Status = _adminSkipped > 0 && !elevated
            ? $"{_adminSkipped} tweak richiedono amministratore — riavvia come admin per includerli."
            : "";
        Raise(nameof(HasPlan));
        Raise(nameof(ShowApplyButton));
        Raise(nameof(AdminBlocked));
        Raise(nameof(CanConfirm));
        IsOpen = true;
    }

    private async void Confirm()
    {
        IsBusy = true;
        Status = "Scrivo i tweak…";
        try
        {
            // Su thread di background: N tweak × (restore-point + scritture) non congelano la UI.
            var plans = _ready.Select(r => r.Plan).ToArray();
            var (ok, stoppedAt) = await System.Threading.Tasks.Task.Run(() => _exec.ExecuteAll(plans));
            Applied = true;
            Status = stoppedAt is null
                ? $"Applicati e verificati {ok} tweak. Annulla ognuno da Modifiche."
                : $"Applicati {ok}, poi FERMATO a {stoppedAt}. I tweak applicati sono journaled e annullabili.";
            _main.TerminalLine = $"$ verdict apply-all · {ok} tweak · journaled";
            _main.Changes.Refresh();
            _main.ShowToast(
                stoppedAt is null
                    ? $"{ok} tweak scritti e verificati · annullabili da Modifiche"
                    : $"Applicati {ok}, poi fermato · niente a metà",
                stoppedAt is null ? "Ok" : "Warn");
        }
        catch (Exception ex)
        {
            // Parità col percorso a singolo apply: un errore inatteso non deve diventare un crash
            // grezzo. Aggiorno comunque Modifiche — i tweak journaled PRIMA del throw restano
            // applicati e reversibili, e l'utente deve vederli.
            Status = $"FERMATO: {ex.Message}";
            _main.Changes.Refresh();
            _main.ShowToast("Applica-tutto fermato · niente è stato lasciato a metà", "Danger");
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
    private readonly AppSettings _settings;
    private string _status = "";

    public ChangesViewModel(ExecutionService exec, AppSettings settings)
    {
        _exec = exec;
        _settings = settings;
        Refresh();
    }

    public ObservableCollection<ChangeSession> Sessions { get; } = [];
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool IsEmpty => Sessions.Count == 0;

    /// <summary>Watchdog (Lab feature) lives as a section on this page. Set by MainViewModel since it
    /// needs the whole app (scan + execution), not just this page's pieces.</summary>
    public WatchdogViewModel? Watchdog { get; set; }

    /// <summary>Set by MainViewModel so undo / revert-all can fire a toast.</summary>
    public MainViewModel? Main { get; set; }

    /// <summary>True if at least one session still has changes to undo (drives "Ripristina tutto").</summary>
    public bool HasRevertableSessions => Sessions.Any(s => !s.AllUndone);

    /// <summary>Undo EVERY journaled session in one go — "back to the starting point".</summary>
    public RelayCommand RevertAllCommand => new(RevertAll, () => HasRevertableSessions);

    // ── Trust mode (Lab feature): the full "what Verdict could touch" manifest ──
    public bool ShowTrustMode => _settings.IsFeatureEnabled(WPEP.Execution.FeatureCatalog.TrustMode);
    public ObservableCollection<TrustEntry> TrustEntries { get; } = [];
    private string _trustSummary = "";
    public string TrustSummary { get => _trustSummary; set => Set(ref _trustSummary, value); }

    /// <summary>Builds the read-only Trust manifest from the KB apply specs (no system access).
    /// Cheap; rebuilt on each Changes-page visit so a toggle in the Lab takes effect.</summary>
    public void RefreshTrustManifest()
    {
        Raise(nameof(ShowTrustMode));
        TrustEntries.Clear();
        TrustSummary = "";
        if (!ShowTrustMode) return;
        try
        {
            var kb = KnowledgeBaseLoader.Load();
            var manifest = TrustManifest.Build(kb);
            foreach (var t in manifest)
                TrustEntries.Add(t);
            TrustSummary = TrustManifest.Summarize(manifest);
        }
        catch { /* KB unreadable must never break the page */ }
    }

    private string _selfTest = "";
    public string SelfTestStatus { get => _selfTest; set => Set(ref _selfTest, value); }

    /// <summary>Runs the engine self-test (scratch HKCU key, full cleanup) off the UI
    /// thread, so users can confirm the apply engine works on their machine.</summary>
    public RelayCommand SelfTestCommand => new(() =>
    {
        SelfTestStatus = "Verifica in corso…";
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var r = Execution.EngineSelfTest.RunReal();
                var detail = string.Join("  ·  ", r.Steps.Select(s => $"{s.Name}: {(s.Ok ? "OK" : "FALLITO")}"));
                SelfTestStatus = (r.Passed
                    ? "✓ PASS — il motore di apply (write/verify/undo) funziona su questo PC."
                    : "✗ FAIL — vedi dettagli.") + "\n" + detail;
            }
            catch (Exception ex)
            {
                // Mai lasciare lo stato bloccato su "in corso…": un errore inatteso del self-test
                // dev'essere onesto e visibile, non un crash-dialog differito (UnobservedTask).
                SelfTestStatus = $"✗ FAIL — il self-test ha sollevato un errore: {ex.Message}";
            }
        });
    });

    public void Refresh()
    {
        Sessions.Clear();
        foreach (var file in _exec.Sessions().Reverse())
            Sessions.Add(Describe(file));
        Raise(nameof(IsEmpty));
        Raise(nameof(HasRevertableSessions));
    }

    private void RevertAll()
    {
        int total = 0, sessions = 0;
        foreach (var s in Sessions.Where(s => !s.AllUndone).ToList())
        {
            try { total += _exec.Undo(s.File).Restored; sessions++; } catch { /* skip a bad session */ }
        }
        Refresh();
        Status = total > 0
            ? $"{total} modifiche ripristinate da {sessions} sessioni · sistema al punto di partenza."
            : "Niente da ripristinare.";
        Main?.ShowToast(
            total > 0 ? $"{total} modifiche ripristinate · sistema al punto di partenza" : "Niente da ripristinare",
            total > 0 ? "Ok" : "Info");
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
                    (e.Undone ? "  [annullato]" : e.Verified ? "  [applicato]" : "  [fallito]"));
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
            var outcome = _exec.Undo(session.File);
            Status = outcome.Restored > 0
                ? $"Annullate {outcome.Restored} modifiche da {session.Display}."
                : $"{session.Display} era già annullato.";
            if (outcome.Skipped.Count > 0)
                Status += $"\n{outcome.Skipped.Count} saltate (cambiate fuori da Verdict): "
                    + string.Join("; ", outcome.Skipped);
            Refresh();
            if (outcome.Restored > 0)
                Main?.ShowToast("Annullato · valore precedente ripristinato", "Ok");
        }
        catch (Exception ex)
        {
            Status = $"Annulla fallito: {ex.Message}";
        }
    });
}
