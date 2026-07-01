using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using WPEP.Advisor;
using WPEP.Core.Diagnostics;
using WPEP.Core.SystemInfo;
using WPEP.Diagnostics;
using WPEP.Execution;
using WPEP.KnowledgeBase;
using WPEP.SystemAnalyzer;

namespace WPEP.App;

// ============================== MAIN / NAV ==============================

public sealed class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;
    private string _terminalLine = "$ wpep · ready · 0 writes";
    private bool _showWelcome;
    private string _toastMessage = "";
    private string _toastColor = "Ok";
    private bool _toastVisible;
    private readonly System.Windows.Threading.DispatcherTimer _toastTimer;

    public AppSettings Settings { get; }
    public ExecutionService Execution { get; } = new();
    public VerdictViewModel Verdict { get; }
    public ScanViewModel Scan { get; }
    public MeasureWizardViewModel Measure { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public KbViewModel Kb { get; }
    public ReportViewModel Report { get; }
    public ChangesViewModel Changes { get; }
    public SettingsViewModel SettingsPage { get; }
    public LabViewModel Lab { get; }
    public ProfilesViewModel Profiles { get; }
    public CoPilotViewModel CoPilot { get; }
    public ApplyAllViewModel ApplyAll { get; }

    public ViewModelBase CurrentPage { get => _currentPage; set => Set(ref _currentPage, value); }
    public string TerminalLine { get => _terminalLine; set => Set(ref _terminalLine, value); }

    /// <summary>First-run welcome overlay (EDGE_CASES §2): the moment of trust.
    /// No scan happens until the user clicks "Scan my system".</summary>
    public bool ShowWelcome { get => _showWelcome; set => Set(ref _showWelcome, value); }

    // ── Toast (design handoff): transient bottom-right feedback, auto-dismiss ~2.8s ──
    public string ToastMessage { get => _toastMessage; set => Set(ref _toastMessage, value); }
    public string ToastColor { get => _toastColor; set => Set(ref _toastColor, value); }
    public bool ToastVisible { get => _toastVisible; set => Set(ref _toastVisible, value); }

    /// <summary>Show a transient toast. <paramref name="color"/> is a token name (Ok/Info/Warn/Danger)
    /// for the accent bar + icon. Restarts the auto-dismiss timer on each call.</summary>
    public void ShowToast(string message, string color = "Ok")
    {
        ToastMessage = message;
        ToastColor = color;
        ToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public RelayCommand StartFirstScanCommand { get; }

    public MainViewModel()
    {
        _toastTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.8) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastVisible = false; };
        Settings = AppSettings.Load();
        Scan = new ScanViewModel(Settings);
        Verdict = new VerdictViewModel(this);
        Measure = new MeasureWizardViewModel(this, Settings);
        Measure.InitCommands();
        Measure.RefreshProcesses();
        Diagnostics = new DiagnosticsViewModel(this);
        Kb = new KbViewModel(Settings);
        Report = new ReportViewModel(this);
        Changes = new ChangesViewModel(Execution, Settings);
        Changes.Watchdog = new WatchdogViewModel(this);
        Changes.Main = this;
        SettingsPage = new SettingsViewModel(Settings) { Main = this };
        Lab = new LabViewModel(Settings);
        BuildCommunity(); // imposta Community in base a Settings.CommunityShareEnabled
        // EXPO state lands with the hardware scan → refresh the Verdict Score when it does.
        Scan.ScanCompleted += () => Verdict.RecomputeScore();
        ApplyAll = new ApplyAllViewModel(this, Execution);
        Profiles = new ProfilesViewModel(this);
        CoPilot = new CoPilotViewModel(this);
        _currentPage = Verdict;

        StartFirstScanCommand = new(() =>
        {
            ShowWelcome = false;
            Settings.Save(); // creates the settings file: next launch is not first-run
            Verdict.ShowFirstRunHint = true; // one-time onboarding banner after this first scan
            _ = Verdict.ScanAsync();
        });

        if (Settings.IsFirstRun)
        {
            ShowWelcome = true;
            Verdict.SetIdle("Benvenuto — nessuna scansione ancora eseguita.");
        }
        else
        {
            _ = Verdict.ScanAsync();
        }
        _ = Scan.ScanAsync(); // hardware inventory in the background
    }

    /// <summary>V7 community evidence: local-first, consent-first. Records YOUR anonymized outcomes;
    /// nothing leaves the PC unless a backend is configured and you opt in.</summary>
    public WPEP.Execution.CommunityService Community { get; private set; } = new();

    /// <summary>Ricostruisce <see cref="Community"/> in base alle Settings correnti: RemoteBackend
    /// quando opt-in attivo E endpoint configurato, altrimenti LocalOnlyBackend. Chiamata dal
    /// constructor e dal SettingsViewModel quando l'utente flippa il checkbox.</summary>
    public void BuildCommunity()
    {
        WPEP.Execution.ICommunityBackend backend =
            (Settings.CommunityShareEnabled && WPEP.Execution.CommunityConfig.IsConfigured)
                ? new WPEP.Execution.RemoteBackend(WPEP.Execution.CommunityConfig.Endpoint)
                : new WPEP.Execution.LocalOnlyBackend();
        Community = new WPEP.Execution.CommunityService(backend);
        Raise(nameof(Community));
    }

    /// <summary>Record an anonymized outcome for a tweak (best-effort; skipped if no rig signature yet).
    /// CommunityService.Record fa sia append locale che (eventuale) submit al backend remoto.</summary>
    public void RecordEvidence(string tweakId, string outcome, double? deltaPercent)
    {
        var dna = Scan.Dna;
        if (dna is null)
            return;
        Community.Record(dna.Code, dna.Tier, tweakId, outcome, deltaPercent, DateTimeOffset.UtcNow.ToString("o"));
    }

    /// <summary>Jump to a KB entry from anywhere (Verdict "How to" buttons).</summary>
    public void ShowKbEntry(string id)
    {
        Kb.Filter = "All";
        Kb.SearchText = "";
        Kb.Selected = Kb.Entries.FirstOrDefault(e =>
            e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        CurrentPage = Kb;
    }
}

// ============================== VERDICT ==============================

/// <summary>A tweak row, now an ON/OFF TOGGLE (V6.5). ON applies immediately (write→verify→journal),
/// OFF undoes the REAL journaled apply (restores the captured "before" — never a guessed value).
/// Risky tweaks ask a confirm first. Manual/gui-only tweaks aren't toggleable: they keep the info
/// affordances (and, phase 2, a QR to the on-phone guide).</summary>
public sealed class VerdictItem : ViewModelBase
{
    private readonly TweakEntry _entry;
    private readonly MainViewModel _main;
    private bool _isOn;
    private bool _busy;
    private string _status;
    private string? _lastJournal;

    public VerdictItem(TweakEntry entry, string stateNote, MainViewModel main, bool isOn)
    {
        _entry = entry;
        _main = main;
        StateNote = stateNote;
        _isOn = isOn;
        _status = NeedsAdminBlocked ? "serve admin — riavvia Verdict come amministratore"
                                    : isOn ? "attivo" : stateNote;
        HowToCommand = new(() => main.ShowKbEntry(entry.Id));
        OpenSettingsCommand = new(() => ExecutionService.OpenSettings(_entry.Apply!.SettingsUri!));
    }

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public string StateNote { get; }

    public bool IsApplicable => _main.Execution.CanApply(_entry);
    public bool IsManual => !IsApplicable;
    public bool CanOpenSettings => !IsApplicable && _entry.Apply?.SettingsUri is not null;
    public bool IsRisky => _entry.Risk == RiskLevel.High;
    public bool NeedsAdminBlocked =>
        IsApplicable && _main.Execution.NeedsAdmin(_entry) && !ExecutionService.IsElevated;

    /// <summary>The switch is live only for applicable, non-busy, non-admin-blocked tweaks.</summary>
    public bool ToggleEnabled => IsApplicable && !_busy && !NeedsAdminBlocked;

    public string KindLabel => IsApplicable ? "1-CLICK" : CanOpenSettings ? "IMPOSTAZIONI" : "MANUALE";
    public string KindColor => IsApplicable ? "Ok" : CanOpenSettings ? "Info" : "Neutral";

    public string StatusLine { get => _status; private set => Set(ref _status, value); }

    public bool IsBusy
    {
        get => _busy;
        private set { Set(ref _busy, value); Raise(nameof(ToggleEnabled)); }
    }

    /// <summary>Two-way bound to the switch. The setter kicks off apply/revert; the visual is
    /// corrected via <see cref="SetOn"/> on success/failure (no setter recursion).</summary>
    public bool IsOn
    {
        get => _isOn;
        set { if (value != _isOn && ToggleEnabled) _ = HandleToggleAsync(value); }
    }

    private void SetOn(bool v) { _isOn = v; Raise(nameof(IsOn)); }

    private async Task HandleToggleAsync(bool wantOn)
    {
        IsBusy = true;
        try
        {
            if (wantOn)
            {
                if (IsRisky)
                {
                    var ok = System.Windows.MessageBox.Show(
                        $"\"{Name}\" è un tweak a rischio più alto.\n\n{_entry.RiskNotes}\n\n" +
                        "Applicare comunque? Resta annullabile.",
                        "Verdict — conferma", System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);
                    if (ok != System.Windows.MessageBoxResult.Yes) { SetOn(false); return; }
                }
                StatusLine = "Applico…";
                var plan = _main.Execution.BuildPlan(_entry);
                var file = await Task.Run(() => _main.Execution.ApplyToggle(plan));
                _lastJournal = file;
                SetOn(true);
                StatusLine = "attivo · annullabile";
                _main.Changes.Refresh();
                _main.RecordEvidence(Id, "applied", null); // V7: chi l'ha provato (anonimo, locale)
                _main.ShowToast($"{Name} · attivato (annullabile da Modifiche)", "Ok");
            }
            else
            {
                var session = _lastJournal ?? _main.Execution.LatestActiveSessionFor(Id);
                if (session is null)
                {
                    // Already-active before Verdict touched it: there's no journaled "before", so
                    // there's no SAFE off value. We never guess — we say so honestly.
                    SetOn(true);
                    StatusLine = "era già attivo (di fabbrica): nessun valore 'spento' verificato";
                    _main.ShowToast($"{Name}: era già attivo, non lo spengo alla cieca", "Warn");
                    return;
                }
                StatusLine = "Disattivo…";
                await Task.Run(() => _main.Execution.Undo(session));
                _lastJournal = null;
                SetOn(false);
                StatusLine = StateNote;
                _main.Changes.Refresh();
                _main.ShowToast($"{Name} · disattivato (ripristinato)", "Ok");
            }
        }
        catch (Exception ex)
        {
            SetOn(!wantOn);
            StatusLine = $"non riuscito: {ex.Message}";
            _main.ShowToast($"{Name} · operazione fermata", "Danger");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public RelayCommand HowToCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    // ── BIOS guide (V6.5 fase 2): un QR PER QUESTO tweak → guida sul telefono ──
    public bool HasBiosGuide => IsManual && WPEP.Core.Bios.BiosGuide.HasGuide(Id);
    public string BiosGuideUrl => WPEP.Core.Bios.BiosGuide.Url(
        Id, WPEP.Core.Bios.BiosGuide.VendorSlug(WPEP.SystemAnalyzer.HardwareScanner.BoardManufacturer()));

    private System.Windows.Media.Imaging.BitmapImage? _qr;
    public System.Windows.Media.Imaging.BitmapImage? QrImage =>
        HasBiosGuide ? (_qr ??= QrCode.ForUrl(BiosGuideUrl)) : null;

    private bool _biosOpen;
    public bool IsBiosPopupOpen { get => _biosOpen; set => Set(ref _biosOpen, value); }
    public RelayCommand ToggleBiosGuideCommand => new(() => IsBiosPopupOpen = !IsBiosPopupOpen);
    public RelayCommand OpenBiosGuideCommand => new(() => ExecutionService.OpenSettings(BiosGuideUrl));
}

public sealed class VerdictGroup(string label, string badgeColorKey)
{
    public string Label { get; } = label;
    public string BadgeColorKey { get; } = badgeColorKey;
    public ObservableCollection<VerdictItem> Items { get; } = [];
    public int Count => Items.Count;
}

/// <summary>An in-game/driver setting row for the Optimize-for-game panel.</summary>
public sealed record GameSettingRow(string Name, string Detail);

public sealed class VerdictViewModel(MainViewModel main) : ViewModelBase
{
    private string _header = "Scansione in corso…";
    private string _subHeader = "Sola lettura — Verdict non scrive nulla finché non premi tu";
    private bool _isScanning;
    private int _worthDoing, _alreadyOptimal, _placeboAvoided;
    // The recommended tweaks that can be applied programmatically — fuels "Apply all".
    private readonly List<TweakEntry> _applicableRecommended = [];

    private IReadOnlyList<Recommendation> _allRecommendations = [];
    /// <summary>Tutti i Recommendation dell'ultimo scan (sistema + per-gioco), col loro stato
    /// live: il co-pilota (V6) li usa come grounding, così l'AI cita solo tweak reali e sa
    /// cosa è già attivo per questo PC.</summary>
    public IReadOnlyList<Recommendation> AllRecommendations => _allRecommendations;

    // ── Card "Rumore di sistema" (ispirato all'analisi Hone, docs/VS_HONE.md sez. 3.1) ──
    /// <summary>System Noise Score 0-100 letto dallo snapshot. null = probe insufficienti.</summary>
    public int? NoiseScore => _snapshotCache?.NoiseScore;
    public string? NoiseBand => _snapshotCache?.NoiseBand;
    public bool ShowNoiseCard => _snapshotCache is not null && _snapshotCache.NoiseScore is not null;
    public string NoiseHeadline => NoiseBand switch
    {
        "basso" => $"Sistema pulito ({NoiseScore}/100)",
        "medio" => $"Rumore medio ({NoiseScore}/100)",
        "alto" => $"Sistema rumoroso ({NoiseScore}/100)",
        _ => "Rumore di sistema",
    };
    public string NoiseBody => NoiseBand switch
    {
        "basso" => "Il tuo sistema ha poco rumore background. I tweak nella categoria \"Background\" qui sotto probabilmente NON produrranno FPS misurabili sul TUO PC — non applicarli in massa. Se vuoi verificare, misura col Ghost Tweak.",
        "medio" => "Il tuo sistema ha rumore medio. Alcuni tweak background possono migliorare frametime consistency; verifica con Ghost Tweak invece che applicarli tutti alla cieca.",
        "alto" => "Il tuo sistema è rumoroso. I tweak background qui sotto hanno probabilmente effetto misurabile — ma applicali uno alla volta con Ghost Tweak per capire quali contano davvero.",
        _ => "",
    };
    public string NoiseColor => NoiseBand switch
    {
        "basso" => "Ok",
        "medio" => "Warn",
        "alto" => "Danger",
        _ => "Neutral",
    };
    public string NoiseFactorsText => _snapshotCache?.NoiseFactors is { Count: > 0 } factors
        ? string.Join(" · ", factors)
        : "";

    // ── Gaming Session Mode (docs/VS_HONE.md sez. 3.3) ──
    private readonly WPEP.Execution.GamingSession _session = new();
    private string _sessionStatus = "";
    public bool SessionActive => _session.IsActive;
    public string SessionStatus
    {
        get
        {
            if (_sessionStatus.Length > 0) return _sessionStatus;
            return _session.IsActive
                ? $"Sessione attiva: {_session.TouchedProcesses.Count} processi a BelowNormal."
                : "Sessione non attiva.";
        }
        private set { Set(ref _sessionStatus, value); Raise(nameof(SessionActive)); }
    }
    public string SessionButtonLabel => _session.IsActive ? "Ripristina priorità" : "Attiva modalità gaming";
    public RelayCommand ToggleSessionCommand => new(() =>
    {
        if (_session.IsActive)
        {
            int origCount = _session.TouchedProcesses.Count;
            int restored = _session.Stop();
            SessionStatus = origCount == 0
                ? "Sessione chiusa (nessun processo era stato toccato)."
                : $"Ripristinati {restored}/{origCount} processi a priorità normale.";
        }
        else
        {
            int down = _session.Start();
            SessionStatus = down > 0
                ? $"Sessione avviata: {down} processi a BelowNormal ({string.Join(", ", _session.TouchedProcesses.Select(t => t.ProcessName))})."
                : "Nessun processo rumoroso in esecuzione — sistema già pulito per il gaming.";
        }
        Raise(nameof(SessionActive));
        Raise(nameof(SessionButtonLabel));
    });

    // ── Vista bucket UX (docs/VS_HONE.md sez. 3.2): 4 macro-categorie invece di stato. ──
    /// <summary>Toggle persistito: false = raggruppamento tecnico (default), true = 4 bucket
    /// FPS/Network/QoL/Background. La vista tecnica resta il default per compatibilità.</summary>
    public bool ShowByBucket
    {
        get => main.Settings.ShowByBucket;
        set
        {
            if (main.Settings.ShowByBucket == value) return;
            main.Settings.ShowByBucket = value;
            main.Settings.Save();
            Raise();
            Raise(nameof(ShowByTechnical));
            Raise(nameof(BucketedGroups));
            Raise(nameof(ActiveGroups));
        }
    }
    /// <summary>Inverso di <see cref="ShowByBucket"/> per binding Visibility semplice in XAML.</summary>
    public bool ShowByTechnical => !ShowByBucket;

    /// <summary>Groups derivati raggruppando <see cref="_allRecommendations"/> per macro-bucket
    /// (FPS/Network/QoL/Background). Ricalcolato ogni get — costo O(N) su ~130 voci, trascurabile.
    /// Voci NotApplicable escluse (uguale ai Groups tecnici).</summary>
    public IReadOnlyList<VerdictGroup> BucketedGroups
    {
        get
        {
            var byBucket = _allRecommendations
                .Where(r => r.Entry.Game is null && r.Classification != Classification.NotApplicable)
                .GroupBy(r => WPEP.Advisor.MacroCategory.Bucket(r.Entry.Category))
                .OrderBy(g => WPEP.Advisor.MacroCategory.All.ToList().IndexOf(g.Key));
            var result = new List<VerdictGroup>();
            foreach (var group in byBucket)
            {
                var vg = new VerdictGroup(group.Key, BucketColor(group.Key));
                foreach (var r in group.OrderBy(x => x.Classification))
                    vg.Items.Add(new VerdictItem(r.Entry, r.StateNote, main, r.Classification == Classification.AlreadyActive));
                if (vg.Items.Count > 0) result.Add(vg);
            }
            return result;
        }
    }

    private static string BucketColor(string bucket) => bucket switch
    {
        WPEP.Advisor.MacroCategory.FpsLatency => "Accent",
        WPEP.Advisor.MacroCategory.NetworkPing => "Info",
        WPEP.Advisor.MacroCategory.StabilityQoL => "Ok",
        WPEP.Advisor.MacroCategory.Background => "Neutral",
        _ => "Neutral",
    };

    /// <summary>Sorgente unica per l'ItemsControl principale: switch runtime tra la vista
    /// tecnica (default) e la vista a bucket UX (ShowByBucket=true).</summary>
    public IEnumerable<VerdictGroup> ActiveGroups => ShowByBucket ? BucketedGroups : Groups;

    // ── Card "Pronto per Vanguard": Secure Boot + TPM 2.0 sono prerequisiti Win11+Vanguard. ──
    /// <summary>null = non rilevato (es. boot Legacy/MBR), true/false = attivo o da abilitare.</summary>
    /// <summary>True quando c'è almeno uno snapshot in cache: card mostrata. Prima dello scan
    /// resta nascosta (niente stati 'non rilevato' confusi all'avvio).</summary>
    public bool ShowVanguardCard => _snapshotCache is not null;
    public bool? VanguardSecureBootOk => _snapshotCache?.SecureBootEnabled;
    public bool? VanguardTpmOk => _snapshotCache?.Tpm2Enabled;
    public bool VanguardReady => VanguardSecureBootOk == true && VanguardTpmOk == true;
    public bool VanguardActionNeeded => VanguardSecureBootOk == false || VanguardTpmOk == false;
    // Per-riga (binding XAML via Visibility): true se attivo, false se da abilitare, unknown se null.
    public bool SecureBootOnUi => VanguardSecureBootOk == true;
    public bool SecureBootOffUi => VanguardSecureBootOk == false;
    public bool TpmOnUi => VanguardTpmOk == true;
    public bool TpmOffUi => VanguardTpmOk == false;
    public string VanguardHeadline => VanguardReady
        ? "Pronto per Vanguard"
        : (VanguardActionNeeded ? "Da abilitare per Vanguard / Win11" : "Pronto per Vanguard");

    public RelayCommand OpenSecureBootGuideCommand => new(() => OpenBiosGuide("secure-boot-enable"));
    public RelayCommand OpenTpmGuideCommand => new(() => OpenBiosGuide("tpm-enable"));

    private void OpenBiosGuide(string tweakId)
    {
        // Vendor omesso: il sito mostra il picker. Manteniamo l'azione semplice e zero-dipendenze.
        var url = WPEP.Core.Bios.BiosGuide.Url(tweakId, vendorSlug: null, lang: "it");
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* user può sempre aprire il link dall'item della lista */ }
    }

    public string Header { get => _header; set => Set(ref _header, value); }
    public string SubHeader { get => _subHeader; set => Set(ref _subHeader, value); }
    public bool IsScanning { get => _isScanning; set => Set(ref _isScanning, value); }
    public int WorthDoing { get => _worthDoing; set { Set(ref _worthDoing, value); Raise(nameof(FirstRunHintText)); } }
    public int AlreadyOptimal { get => _alreadyOptimal; set { Set(ref _alreadyOptimal, value); Raise(nameof(FirstRunHintText)); } }
    public int PlaceboAvoided { get => _placeboAvoided; set => Set(ref _placeboAvoided, value); }
    public ObservableCollection<VerdictGroup> Groups { get; } = [];

    // ── First-run onboarding hint (TIER 1): a one-time welcome banner after the first scan ──
    private bool _showFirstRunHint;
    public bool ShowFirstRunHint { get => _showFirstRunHint; set => Set(ref _showFirstRunHint, value); }
    public string FirstRunHintText =>
        $"Benvenuto in Verdict! Ho scansionato il tuo PC: {WorthDoing} tweak consigliati con un click" +
        (AlreadyOptimal > 0 ? $", {AlreadyOptimal} già a posto" : "") +
        ". Quelli da attivare li trovi qui sotto in «Da attivare ora — un click». Ogni modifica è reversibile dalla pagina Modifiche.";
    public RelayCommand DismissFirstRunCommand => new(() => ShowFirstRunHint = false);

    public int ApplicableRecommendedCount => _applicableRecommended.Count;
    public bool HasApplicableRecommended => _applicableRecommended.Count > 0;
    public string ApplyAllLabel => $"Accendi i consigliati ({_applicableRecommended.Count})";

    // ── Verdict Score (Lab feature, default-ON) ──────────────────────────────
    // The honest 0–100 number. Gated by the feature flag so the user can hide it.
    private int _score;
    private string _scoreBand = "", _scoreColor = "Ok", _honestyNote = "";
    private int _scoreDone, _scorePending;
    public bool ShowScore => main.Settings.IsFeatureEnabled(FeatureCatalog.Score);
    public int Score { get => _score; private set => Set(ref _score, value); }
    public string ScoreBand { get => _scoreBand; private set => Set(ref _scoreBand, value); }
    public string ScoreColor { get => _scoreColor; private set => Set(ref _scoreColor, value); }
    public string HonestyNote { get => _honestyNote; private set => Set(ref _honestyNote, value); }
    public ObservableCollection<ScoreReason> ScoreBreakdown { get; } = [];

    // ── Risk Slider (Lab feature) ────────────────────────────────────────────
    // One knob safe↔extreme deciding which tweaks are "in scope". Placebos never count.
    private readonly List<(int RiskTier, bool IsPlacebo)> _scopeItems = [];
    private string _riskName = "", _riskTagline = "", _riskColor = "Ok", _riskSummary = "";
    public bool ShowRiskSlider => main.Settings.IsFeatureEnabled(FeatureCatalog.RiskSlider);
    public string RiskName { get => _riskName; private set => Set(ref _riskName, value); }
    public string RiskTagline { get => _riskTagline; private set => Set(ref _riskTagline, value); }
    public string RiskColor { get => _riskColor; private set => Set(ref _riskColor, value); }
    public string RiskSummary { get => _riskSummary; private set => Set(ref _riskSummary, value); }
    public int RiskLevel
    {
        get => (int)main.Settings.RiskTolerance;
        set
        {
            var tol = (RiskTolerance)Math.Clamp(value, 0, 3);
            if (tol == main.Settings.RiskTolerance) return;
            main.Settings.RiskTolerance = tol;
            main.Settings.Save();
            Raise();
            RecomputeRiskScope();
        }
    }

    public RelayCommand RescanCommand => new(() => _ = ScanAsync());
    public RelayCommand ApplyAllCommand => new(
        () => main.ApplyAll.Open(_applicableRecommended),
        () => _applicableRecommended.Count > 0);

    // ── Optimize for [game] (Lab feature) ────────────────────────────────────
    private IReadOnlyList<TweakEntry> _kbCache = [];
    private SystemSnapshot? _snapshotCache;
    private string? _selectedGame;
    public bool ShowOptimizeForGame => main.Settings.IsFeatureEnabled(FeatureCatalog.OptimizeForGame);
    public ObservableCollection<string> Games { get; } = [];
    public ObservableCollection<string> GameSystemTweaks { get; } = [];
    public ObservableCollection<GameSettingRow> GameInGameSettings { get; } = [];
    public string? SelectedGame
    {
        get => _selectedGame;
        set { if (Set(ref _selectedGame, value)) RebuildGamePlan(); }
    }

    /// <summary>Loads the game list for the optimizer (cached KB). Cheap; safe to call on nav.</summary>
    public void RefreshGames()
    {
        Raise(nameof(ShowOptimizeForGame));
        if (!ShowOptimizeForGame) return;
        if (_kbCache.Count == 0)
            try { _kbCache = KnowledgeBaseLoader.Load(); } catch { return; }
        if (Games.Count == 0)
            foreach (var g in OptimizeForGame.AvailableGames(_kbCache)) Games.Add(g);
    }

    private void RebuildGamePlan()
    {
        GameSystemTweaks.Clear();
        GameInGameSettings.Clear();
        if (_selectedGame is null || _kbCache.Count == 0) return;
        var plan = OptimizeForGame.Build(_selectedGame, _kbCache, _snapshotCache);
        foreach (var t in plan.SystemTweaks) GameSystemTweaks.Add(t.Name);
        foreach (var s in plan.InGameSettings)
            GameInGameSettings.Add(new GameSettingRow(s.Name, s.ExpectedImpact));
    }

    public void SetIdle(string header)
    {
        Header = header;
        SubHeader = "Sola lettura — Verdict non scrive nulla finché non premi tu";
    }

    public async Task ScanAsync()
    {
        IsScanning = true;
        Header = "Scansione in corso…";
        var sw = Stopwatch.StartNew();
        try
        {
            var (snapshot, recommendations) = await Task.Run(() =>
            {
                var s = SnapshotBuilder.Build(DateTimeOffset.UtcNow);
                var kb = KnowledgeBaseLoader.Load();
                // Stato live: "già attivo / da attivare" invece di "non rilevabile" per tutto ciò che
                // Verdict sa fare. I nvidia-drs sono letti in UNA sola sessione NVAPI (batch) via LiveState.
                return (s, AdvisorEngine.Advise(s, kb,
                    WPEP.Execution.LiveState.Detector(kb, main.Execution.CanApply, main.Execution.BuildPlan)));
            });
            sw.Stop();
            Apply(snapshot, recommendations);
            main.TerminalLine = $"$ wpep advise · {sw.Elapsed.TotalSeconds:F1}s · 0 writes";
        }
        catch (Exception ex)
        {
            Header = "Scansione fallita";
            SubHeader = ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Apply(SystemSnapshot snapshot, IReadOnlyList<Recommendation> allRecommendations)
    {
        _snapshotCache = snapshot; // reused by the Optimize-for-game filter
        _allRecommendations = allRecommendations; // grounding del co-pilota (V6)
        // Refresh card "Pronto per Vanguard" (derivata da snapshot)
        Raise(nameof(VanguardSecureBootOk));
        Raise(nameof(VanguardTpmOk));
        Raise(nameof(VanguardReady));
        Raise(nameof(VanguardActionNeeded));
        Raise(nameof(VanguardHeadline));
        Raise(nameof(SecureBootOnUi));
        Raise(nameof(SecureBootOffUi));
        Raise(nameof(TpmOnUi));
        Raise(nameof(TpmOffUi));
        Raise(nameof(ShowVanguardCard));
        Raise(nameof(NoiseScore));
        Raise(nameof(NoiseBand));
        Raise(nameof(NoiseHeadline));
        Raise(nameof(NoiseBody));
        Raise(nameof(NoiseColor));
        Raise(nameof(NoiseFactorsText));
        Raise(nameof(ShowNoiseCard));
        Raise(nameof(BucketedGroups));
        Raise(nameof(ActiveGroups));
        // Game-specific entries live in their own section and never count toward
        // the system verdict header (R7_COPY_AND_KB3 open question, resolved).
        var recommendations = allRecommendations.Where(r => r.Entry.Game is null).ToArray();
        var gameSpecific = allRecommendations.Where(r => r.Entry.Game is not null).ToArray();

        // "Che FA, non insegna": raggruppa per AZIONE, non per classificazione. In alto ciò che
        // Verdict può accendere con un click; i manuali (gui-only/in-game/BIOS) declassati in fondo.
        Groups.Clear();
        static bool Actionable(Classification c) => c is Classification.Recommended
            or Classification.Optional or Classification.OptionalWithWarning;

        void AddGroup(string label, string color, IEnumerable<Recommendation> items)
        {
            var g = new VerdictGroup(label, color);
            foreach (var r in items) g.Items.Add(new VerdictItem(r.Entry, r.StateNote, main, r.Classification == Classification.AlreadyActive));
            if (g.Items.Count > 0) Groups.Add(g);
        }

        AddGroup("Da attivare ora — un click", "Ok",
            recommendations.Where(r => Actionable(r.Classification) && main.Execution.CanApply(r.Entry)));
        AddGroup("Già a posto", "OkDim",
            recommendations.Where(r => r.Classification == Classification.AlreadyActive));
        AddGroup("Manuali — li fai tu (Verdict ti dice come)", "Info",
            recommendations.Where(r => Actionable(r.Classification) && !main.Execution.CanApply(r.Entry)));
        AddGroup("Da evitare / placebo", "Neutral",
            recommendations.Where(r => r.Classification is Classification.NotRecommended or Classification.Placebo));

        foreach (var gameGroup in gameSpecific.GroupBy(r => r.Entry.Game!))
        {
            // Hide a game's section only when we KNOW it is not installed;
            // detection failure (null) keeps it visible — honest default.
            if (snapshot.GameInstalled(gameGroup.Key) == false)
                continue;
            var group = new VerdictGroup($"Game-specific — {gameGroup.Key}", "Accent");
            foreach (var r in gameGroup.OrderBy(r => r.Entry.EvidenceLevel))
                group.Items.Add(new VerdictItem(r.Entry, r.StateNote, main, r.Classification == Classification.AlreadyActive));
            Groups.Add(group);
        }

        WorthDoing = recommendations.Count(r => r.Classification == Classification.Recommended);
        AlreadyOptimal = recommendations.Count(r => r.Classification == Classification.AlreadyActive);
        PlaceboAvoided = recommendations.Count(r => r.Classification == Classification.Placebo);

        // "Apply all" only ever touches Recommended tweaks that are programmatically
        // applicable (no placebo, no risky, no gui-only). Each still goes through the
        // same dry-run + journal + per-tweak undo as a single apply.
        _applicableRecommended.Clear();
        _applicableRecommended.AddRange(recommendations
            .Where(r => r.Classification == Classification.Recommended
                        && main.Execution.CanApply(r.Entry))
            .Select(r => r.Entry));
        Raise(nameof(ApplicableRecommendedCount));
        Raise(nameof(HasApplicableRecommended));
        Raise(nameof(ApplyAllLabel));

        int toApplyCount = recommendations.Count(r =>
            Actionable(r.Classification) && main.Execution.CanApply(r.Entry));
        Header = toApplyCount > 0
            ? $"{toApplyCount} tweak da attivare con un click"
            : "Niente da attivare — sei già a posto. Vai a giocare.";
        SubHeader = $"Scansionato alle {DateTime.Now:HH:mm} · {snapshot.GpuName} · sola lettura, Verdict non scrive nulla finché non premi tu";

        if (snapshot.IsManagedDevice == true)
            SubHeader += "\n⚠ Sembra un dispositivo gestito dall'azienda. Usare strumenti di terze " +
                         "parti potrebbe violare la policy IT: chiedi prima l'ok all'IT.";

        // Verdict Score: remember the inputs we have now; EXPO arrives with the hardware scan,
        // so recompute when that completes too (see ScanCompleted wiring in ScanAsync).
        _scoreDone = AlreadyOptimal;
        _scorePending = WorthDoing;
        RecomputeScore();

        // Risk Slider scope: every actionable tweak as (risk tier, is-placebo). AlreadyActive /
        // NotApplicable aren't things to apply, so they don't count toward "in scope".
        _scopeItems.Clear();
        _scopeItems.AddRange(recommendations
            .Where(r => r.Classification is not (Classification.AlreadyActive or Classification.NotApplicable))
            .Select(r => ((int)r.Entry.Risk, r.Entry.EvidenceLevel == EvidenceLevel.Placebo)));
        RecomputeRiskScope();
    }

    /// <summary>Updates the Risk Slider card: profile description + how many tweaks fall within the
    /// chosen tolerance. Placebos are reported separately and never counted as in-scope.</summary>
    public void RecomputeRiskScope()
    {
        Raise(nameof(ShowRiskSlider));
        var tol = main.Settings.RiskTolerance;
        var p = RiskSlider.Describe(tol);
        RiskName = p.Name;
        RiskTagline = p.Tagline;
        RiskColor = p.Color;

        int inScope = _scopeItems.Count(i => RiskSlider.Includes(tol, i.RiskTier, i.IsPlacebo));
        int placebo = _scopeItems.Count(i => i.IsPlacebo);
        int riskyOut = _scopeItems.Count(i => !i.IsPlacebo && !RiskSlider.Includes(tol, i.RiskTier, false));
        RiskSummary = $"{inScope} tweak in ambito a questo livello" +
            (riskyOut > 0 ? $" · {riskyOut} troppo rischiosi per ora" : "") +
            (placebo > 0 ? $" · {placebo} placebo sempre esclusi" : "");
    }

    /// <summary>Computes the honest 0–100 score from the latest advisor counts + EXPO state.
    /// Cheap and idempotent: called after each advise and again when the hardware scan lands.</summary>
    public void RecomputeScore()
    {
        Raise(nameof(ShowScore));
        var result = VerdictScore.Compute(new ScoreInput(
            RecommendedDone: _scoreDone,
            RecommendedPending: _scorePending,
            RiskyActive: 0,        // honest: we don't penalize risky tweaks we can't confirm are ON
            PlaceboActive: 0,      // we don't claim to detect applied placebos — the note still holds
            ExpoEnabled: main.Scan.ExpoEnabled));
        Score = result.Score;
        ScoreBand = result.Band;
        ScoreColor = result.BandColor;
        HonestyNote = result.HonestyNote;
        ScoreBreakdown.Clear();
        foreach (var r in result.Breakdown)
            ScoreBreakdown.Add(r);
    }
}

// ============================== DIAGNOSTICS ==============================

public sealed record DpcRow(string Driver, string Events, string MaxUs, string AvgUs, string Spikes);

/// <summary>A translated "Explain my Stutter" line for the UI.</summary>
public sealed record StutterRow(string ColorKey, string Component, string Plain, string Tip);

public sealed class DiagnosticsViewModel(MainViewModel main) : ViewModelBase
{
    private string _status = EtwDpcIsrCollector.IsElevated()
        ? "Pronto. La cattura legge gli eventi del kernel per 15 secondi — genera carico mentre gira."
        : "La diagnostica serve l'amministratore per leggere gli eventi del kernel. Tutto il resto funziona senza.\nRiavvia Verdict come amministratore per usare questa pagina.";
    private string _verdict = "";
    private bool _isRunning;
    private int _seconds = 15;

    public int Seconds { get => _seconds; set => Set(ref _seconds, Math.Clamp(value, 5, 120)); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string VerdictLine { get => _verdict; set => Set(ref _verdict, value); }
    public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }
    public bool IsElevated => EtwDpcIsrCollector.IsElevated();
    public ObservableCollection<DpcRow> Rows { get; } = [];

    // ── Explain my Stutter (Lab feature) ─────────────────────────────────────
    private string _stutterHeadline = "", _stutterColor = "Ok";
    public bool ShowStutterExplain => main.Settings.IsFeatureEnabled(FeatureCatalog.ExplainStutter);
    /// <summary>Riflette un toggle Explain-my-Stutter fatto nel Lab quando torni su Verdict.</summary>
    public void RefreshStutterFlag() => Raise(nameof(ShowStutterExplain));
    public string StutterHeadline { get => _stutterHeadline; set => Set(ref _stutterHeadline, value); }
    public string StutterColor { get => _stutterColor; set => Set(ref _stutterColor, value); }
    public bool HasStutterResult => StutterHeadline.Length > 0;
    public ObservableCollection<StutterRow> StutterFindings { get; } = [];

    // ── Network Duel (Lab feature) ───────────────────────────────────────────
    private bool _netRunning;
    private string _netStatus = "Misura ping, jitter e perdita verso anchor pubblici (route quality).";
    public bool ShowNetworkDuel => main.Settings.IsFeatureEnabled(FeatureCatalog.NetworkDuel);
    public bool NetRunning { get => _netRunning; set => Set(ref _netRunning, value); }
    public string NetStatus { get => _netStatus; set => Set(ref _netStatus, value); }
    public ObservableCollection<NetworkResult> NetResults { get; } = [];
    public RelayCommand RunNetworkDuelCommand => new(() => _ = RunNetworkDuelAsync(), () => !NetRunning);
    public void RefreshNetworkFlag() => Raise(nameof(ShowNetworkDuel));

    private async Task RunNetworkDuelAsync()
    {
        NetRunning = true;
        NetResults.Clear();
        NetStatus = "Test in corso… (ICMP, ~pochi secondi)";
        try
        {
            var results = await Task.Run(() =>
                NetworkDuel.Anchors.Select(a =>
                    NetworkDuel.Analyze(a.Target, a.Host, NetworkDuel.PingHost(a.Host, 10))).ToList());
            foreach (var r in results) NetResults.Add(r);
            NetStatus = "Fatto. Nota: molti server di gioco bloccano l'ICMP — questi sono anchor di rotta, non il match server.";
        }
        catch (Exception ex) { NetStatus = $"Test fallito: {ex.Message}"; }
        finally { NetRunning = false; }
    }

    public RelayCommand CaptureCommand => new(() => _ = CaptureAsync(), () => IsElevated && !IsRunning);
    public RelayCommand RelaunchAsAdminCommand => new(() => main.Measure.RelaunchAsAdminCommand.Execute(null));

    private async Task CaptureAsync()
    {
        IsRunning = true;
        int seconds = Seconds;
        Status = $"Cattura DPC/ISR per {seconds} secondi…";
        Rows.Clear();
        StutterFindings.Clear();
        StutterHeadline = "";
        VerdictLine = "";
        try
        {
            var report = await Task.Run(() =>
                new EtwDpcIsrCollector().Capture(TimeSpan.FromSeconds(seconds)));

            // Explain my Stutter (Lab feature): translate the raw report into plain Italian.
            Raise(nameof(ShowStutterExplain));
            if (ShowStutterExplain)
            {
                var ex = StutterExplainer.Explain(report);
                StutterHeadline = ex.Headline;
                StutterColor = ex.Overall switch
                {
                    StutterSeverity.Severe => "Danger",
                    StutterSeverity.Likely => "Warn",
                    StutterSeverity.Minor => "Info",
                    _ => "Ok",
                };
                foreach (var f in ex.Findings)
                    StutterFindings.Add(new StutterRow(
                        f.Severity switch { StutterSeverity.Severe => "Danger", StutterSeverity.Likely => "Warn", _ => "Info" },
                        f.Component, f.Plain, f.Tip));
                Raise(nameof(HasStutterResult));
            }

            foreach (var d in report.Drivers.Take(12))
                Rows.Add(new DpcRow(d.Driver, d.TotalCount.ToString("N0"),
                    d.MaxUs.ToString("F1"), d.AvgUs.ToString("F1"),
                    $"{d.SpikesOver500Us:N0} >500µs"));

            var worst = report.Drivers.FirstOrDefault();
            VerdictLine = worst is null || worst.MaxUs < 500
                ? "Nessun driver colpevole di DPC. Lo stack driver è sano — lo stutter, se c'è, arriva dal gioco stesso."
                : $"{worst.Driver}: DPC max {worst.MaxUs:F0}µs durante la cattura. Vale la pena indagare.";
            Status = $"Fatto — {report.TotalEvents:N0} eventi analizzati.";
            main.TerminalLine = $"$ wpep diag · {report.CaptureDurationSeconds:F0}s · 0 writes";
        }
        catch (Exception ex)
        {
            // F6: the usual cause of a kernel-session failure is another tracer.
            Status = $"Cattura fallita: {ex.Message}\n" +
                     "Se persiste: potrebbe esserci un'altra sessione di kernel trace attiva " +
                     "(spesso LatencyMon, WPR o un altro tool di cattura). Chiudila e riprova.";
        }
        finally
        {
            IsRunning = false;
        }
    }
}

// ============================== KNOWLEDGE BASE ==============================

public sealed record KbBadge(string Label, string ColorKey);

public sealed class KbItemViewModel(TweakEntry entry)
{
    public TweakEntry Entry { get; } = entry;
    public string Id => Entry.Id;
    public string Name => Entry.Name;
    public string Category => Entry.Category;
    public KbBadge Badge => Entry.EvidenceLevel switch
    {
        EvidenceLevel.EvidenceStrong => new("Evidenza forte", "Ok"),
        EvidenceLevel.Plausible => new("Plausibile", "Info"),
        EvidenceLevel.Controversial => new("Controverso", "Warn"),
        EvidenceLevel.Placebo => new("Placebo", "Neutral"),
        EvidenceLevel.Risky => new("Rischioso", "Danger"),
        _ => new("?", "Neutral"),
    };
}

public sealed class KbViewModel : ViewModelBase
{
    private readonly IReadOnlyList<KbItemViewModel> _all;
    private string _filter = "Applicabili";
    private string _searchText = "";
    private KbItemViewModel? _selected;
    private string _loadError = "";

    private readonly AppSettings _settings;

    public KbViewModel(AppSettings settings)
    {
        _settings = settings;
        try
        {
            var entries = KnowledgeBaseLoader.Load();
            _all = entries.Select(e => new KbItemViewModel(e)).ToArray();
            foreach (var x in PlaceboMuseum.Build(entries)) // Placebo Museum (Lab feature)
                Museum.Add(x);
        }
        catch (Exception ex)
        {
            _all = [];
            _loadError = ex.Message;
        }
        Refresh();
    }

    // ── Placebo Museum (Lab feature): gallery of debunked myth-tweaks ──
    public bool ShowPlaceboMuseum => _settings.IsFeatureEnabled(WPEP.Execution.FeatureCatalog.PlaceboMuseum);
    public ObservableCollection<PlaceboExhibit> Museum { get; } = [];
    public string MuseumSummary => $"{Museum.Count} miti sfatati con l'evidenza";
    public void RefreshMuseumFlag() => Raise(nameof(ShowPlaceboMuseum));

    // Action-first: "Applicabili" (solo one-click) è il default — Verdict mostra ciò che FA.
    // "Tutti" e i livelli di evidenza restano per chi vuole esplorare, ma non sono il primo impatto.
    public IReadOnlyList<string> Filters { get; } =
        ["Applicabili", "Tutti", "Evidenza forte", "Plausibile", "Controverso", "Placebo", "Rischioso"];

    public ObservableCollection<KbItemViewModel> Entries { get; } = [];
    public string Footer => _loadError.Length > 0
        ? $"Caricamento della Knowledge Base fallito: {_loadError}"
        : "Ogni voce cita una fonte primaria. Niente fonte, niente consiglio.";

    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) Refresh(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) Refresh(); }
    }

    public KbItemViewModel? Selected { get => _selected; set => Set(ref _selected, value); }

    private void Refresh()
    {
        Entries.Clear();
        var query = _filter switch
        {
            "Applicabili" => _all.Where(i => WPEP.Execution.ApplyPolicy.CanApply(i.Entry)),
            "Tutti" => _all,
            _ => _all.Where(i => i.Badge.Label == _filter),
        };
        if (_searchText.Trim() is { Length: > 0 } search)
            query = query.Where(i =>
                i.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Entry.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        foreach (var item in query.OrderBy(i => i.Entry.EvidenceLevel).ThenBy(i => i.Id))
            Entries.Add(item);
        Selected = Entries.FirstOrDefault();
    }
}

// ============================== REPORT ==============================

public sealed class ReportViewModel(MainViewModel main) : ViewModelBase
{
    private string _status = "Genera il report HTML condivisibile (tema scuro): snapshot, ogni verdetto dell'advisor (placebo inclusi) e le misure quando disponibili.";
    private string? _lastPath;
    private bool _busy;

    public string Status { get => _status; set => Set(ref _status, value); }
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }
    public string? LastPath { get => _lastPath; set => Set(ref _lastPath, value); }

    public RelayCommand GenerateCommand => new(() => _ = GenerateAsync(), () => !IsBusy);
    public RelayCommand OpenCommand => new(
        () => Process.Start(new ProcessStartInfo(LastPath!) { UseShellExecute = true }),
        () => LastPath is not null);

    private async Task GenerateAsync()
    {
        IsBusy = true;
        Status = "Generazione in corso…";
        try
        {
            var path = await Task.Run(() =>
            {
                var snapshot = SnapshotBuilder.Build(DateTimeOffset.UtcNow);
                var kb = KnowledgeBaseLoader.Load();
                var recommendations = AdvisorEngine.Advise(snapshot, kb);
                var (noise, comparison) = LoadLatestWizardSession();
                var applied = LoadAppliedChanges();
                var html = Reporting.ReportBuilder.BuildHtml(new Reporting.ReportData(
                    DateTimeOffset.UtcNow, snapshot, recommendations, noise, comparison, applied));

                var dir = Path.Combine(AppContext.BaseDirectory, "reports");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"wpep-report-{DateTime.Now:yyyyMMdd-HHmmss}.html");
                File.WriteAllText(file, html);
                return file;
            });
            LastPath = path;
            Status = $"Report scritto: {path}";
            main.TerminalLine = "$ wpep report · 0 scritture (fuori dalla cartella dell'app)";
        }
        catch (Exception ex)
        {
            Status = $"Report fallito: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Pulls the most recent wizard session into the report: noise
    /// floor from its baseline, comparison when a post group exists. Legacy or
    /// broken sessions degrade to "not included", never to a failed report.</summary>
    private (Statistics.NoiseFloorAnalyzer.NoiseReport?, Statistics.ComparisonEngine.ComparisonReport?)
        LoadLatestWizardSession()
    {
        try
        {
            var runsRoot = Path.Combine(AppContext.BaseDirectory, "runs");
            if (!Directory.Exists(runsRoot))
                return (null, null);
            var latest = Directory.EnumerateDirectories(runsRoot, "wizard-*")
                .OrderByDescending(d => d).FirstOrDefault();
            if (latest is null)
                return (null, null);

            var baselineDir = Path.Combine(latest, "baseline");
            if (!Directory.Exists(baselineDir))
                return (null, null);
            var baseline = Benchmark.BenchmarkRunStore.LoadDirectory(baselineDir);
            var noise = baseline.Count >= 2
                ? Statistics.NoiseFloorAnalyzer.Analyze(baseline) : null;

            var postDir = Path.Combine(latest, "post");
            Statistics.ComparisonEngine.ComparisonReport? comparison = null;
            if (Directory.Exists(postDir))
            {
                var post = Benchmark.BenchmarkRunStore.LoadDirectory(postDir);
                if (post.Count > 0 &&
                    Statistics.EnvironmentValidator.Validate(baseline, post).Valid)
                    comparison = Statistics.ComparisonEngine.Compare(
                        baseline, post, main.Settings.NoiseGateThresholdPercent);
            }
            return (noise, comparison);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>Currently-active applied changes from the journal (not undone),
    /// so the report honestly shows what Verdict changed on this system.</summary>
    private IReadOnlyList<string>? LoadAppliedChanges()
    {
        try
        {
            var lines = new List<string>();
            foreach (var file in main.Execution.Sessions())
            {
                var session = System.Text.Json.JsonSerializer
                    .Deserialize<Execution.JournalSession>(File.ReadAllText(file));
                foreach (var e in session?.Entries ?? [])
                {
                    if (!e.Undone && e.Verified)
                        lines.Add($"{e.TweakId}: {e.Path} = {e.ValueAfter} (was {(e.ExistedBefore ? e.ValueBefore : "not set")})");
                }
            }
            return lines.Count > 0 ? lines : null;
        }
        catch
        {
            return null;
        }
    }
}

// ============================== SETTINGS ==============================

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    /// <summary>Ref al MainViewModel per chiedere il rebuild del community backend quando
    /// l'utente flippa <see cref="CommunityShareEnabled"/>. Stesso pattern di Changes.Main.</summary>
    public MainViewModel? Main { get; set; }

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        ThemePresets.Apply(settings.Theme);
    }

    public IReadOnlyList<ThemeOption> Themes { get; } = ThemePresets.Options();

    public string SelectedTheme
    {
        get => ThemePresets.Normalize(_settings.Theme);
        set
        {
            _settings.Theme = value;
            ThemePresets.Apply(value);
            _settings.Save();
            Raise();
        }
    }

    public int DefaultBenchmarkRuns
    {
        get => _settings.DefaultBenchmarkRuns;
        set { _settings.DefaultBenchmarkRuns = Math.Clamp(value, 3, 10); _settings.Save(); Raise(); }
    }

    public double NoiseGateThresholdPercent
    {
        get => _settings.NoiseGateThresholdPercent;
        set { _settings.NoiseGateThresholdPercent = Math.Clamp(value, 1, 50); _settings.Save(); Raise(); }
    }

    public bool CompactLists
    {
        get => _settings.CompactLists;
        set { _settings.CompactLists = value; _settings.Save(); Raise(); }
    }

    // ── Aggiornamenti: controllo consent-first. Riporta soltanto se c'è una versione
    //    più recente + dove scaricarla; non scarica né installa MAI da solo. Finché non
    //    è configurato un host (UpdateConfig vuoto) resta onesto e non tocca la rete. ──
    private string _updateStatus = $"Sei sulla {AppInfo.VersionLabel}.";
    public string UpdateStatus { get => _updateStatus; private set => Set(ref _updateStatus, value); }

    private string? _updateUrl;
    public bool HasUpdateDownload => _updateUrl is not null;

    private bool _checkingUpdate;

    public RelayCommand CheckUpdatesCommand => new(async () =>
    {
        if (_checkingUpdate)
            return;
        _checkingUpdate = true;
        UpdateStatus = "Controllo aggiornamenti…";
        _updateUrl = null;
        Raise(nameof(HasUpdateDownload));
        try
        {
            var info = await WPEP.Core.Update.UpdateChecker.CheckAsync(AppInfo.Version);
            if (!info.Configured)
                UpdateStatus = $"Sei sulla {AppInfo.VersionLabel}. Aggiornamenti non ancora configurati.";
            else if (info.Error is not null)
                UpdateStatus = $"Controllo non riuscito: {info.Error}";
            else if (info.UpdateAvailable)
            {
                UpdateStatus = $"Disponibile v{info.LatestVersion} (hai v{info.CurrentVersion}).";
                _updateUrl = info.DownloadUrl;
            }
            else
                UpdateStatus = $"Sei aggiornato (v{info.CurrentVersion}).";
        }
        catch (Exception ex)
        {
            // Difensivo (async void): un errore inatteso dev'essere onesto, mai un crash.
            UpdateStatus = $"Controllo non riuscito: {ex.Message}";
        }
        finally
        {
            Raise(nameof(HasUpdateDownload));
            _checkingUpdate = false;
        }
    });

    public RelayCommand OpenDownloadCommand => new(() =>
    {
        if (_updateUrl is null)
            return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(_updateUrl) { UseShellExecute = true });
        }
        catch { /* best-effort: il browser apre la pagina di download */ }
    });

    // ── V7 community evidence: stato + le TUE prove (sempre locali, anonime) ──
    public string CommunityStatus
    {
        get
        {
            var svc = Main?.Community ?? new WPEP.Execution.CommunityService();
            return svc.CommunityActive
                ? $"Community attiva: {svc.BackendName}. I tuoi esiti vengono inviati anonimi al server."
                : "Community non attiva — i tuoi esiti restano SOLO sul tuo PC, anonimi e in locale.";
        }
    }

    /// <summary>V7 opt-in: spedire/ricevere stats dal backend pubblico. Default OFF.
    /// Al cambio: persiste + chiede al MainViewModel di ri-istanziare CommunityService col
    /// backend giusto (LocalOnlyBackend → RemoteBackend o viceversa).</summary>
    public bool CommunityShareEnabled
    {
        get => _settings.CommunityShareEnabled;
        set
        {
            if (_settings.CommunityShareEnabled == value) return;
            _settings.CommunityShareEnabled = value;
            _settings.Save();
            Main?.BuildCommunity();
            Raise();
            Raise(nameof(CommunityStatus));
        }
    }

    public string EvidenceSummary
    {
        get
        {
            var all = WPEP.Execution.EvidenceLedger.Load();
            if (all.Count == 0)
                return "Nessuna prova ancora. Applica o misura un tweak (anche col Ghost Tweak) e Verdict registra qui com'è andato.";
            int tweaks = all.Select(r => r.TweakId).Distinct().Count();
            int measured = all.Count(r => r.Outcome is "helped" or "no-effect" or "hurt");
            return $"{all.Count} prove su {tweaks} tweak ({measured} misurate). Tutto sul tuo PC, in forma anonima (firma RigDna).";
        }
    }

    public string About =>
        "Verdict — l'unico ottimizzatore che ti dice quando smettere di ottimizzare.\n" +
        "(engine: WPEP)\n\n" +
        "Come lavora Verdict:\n" +
        "  · Accendi un interruttore e Verdict scrive SOLO quel tweak (i rischiosi chiedono conferma); " +
        "\"Accendi i consigliati\" mostra prima l'anteprima di tutto\n" +
        "  · Ogni modifica è tracciata e REVERSIBILE: spegni l'interruttore, o usa Ripristina tutto\n" +
        "  · Rilegge dopo aver scritto per VERIFICARE che il valore sia davvero cambiato\n" +
        "  · Non promette FPS finti: misura il prima/dopo con rigore statistico\n\n" +
        "Verdict non tocca MAI il tuo gioco. Niente code injection, hook di processo, accesso alla " +
        "memoria del gioco o overlay. I dati sui frame arrivano dall'event tracing di Windows (ETW) — " +
        "lo stesso canale passivo usato da Intel PresentMon. Non appartiene a nessuna categoria presa " +
        "di mira dagli anti-cheat.\n\n" +
        "Portabile per design. Una cartella, niente installer, niente servizi. " +
        "Cancella la cartella e Verdict non è mai esistito.\n\n" +
        $"{AppInfo.VersionLabel} · Licenza: MIT";
}
