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
    public ApplyDialogViewModel ApplyDialog { get; }
    public ApplyAllViewModel ApplyAll { get; }

    public ViewModelBase CurrentPage { get => _currentPage; set => Set(ref _currentPage, value); }
    public string TerminalLine { get => _terminalLine; set => Set(ref _terminalLine, value); }

    /// <summary>First-run welcome overlay (EDGE_CASES §2): the moment of trust.
    /// No scan happens until the user clicks "Scan my system".</summary>
    public bool ShowWelcome { get => _showWelcome; set => Set(ref _showWelcome, value); }

    public RelayCommand StartFirstScanCommand { get; }

    public MainViewModel()
    {
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
        SettingsPage = new SettingsViewModel(Settings);
        Lab = new LabViewModel(Settings);
        // EXPO state lands with the hardware scan → refresh the Verdict Score when it does.
        Scan.ScanCompleted += () => Verdict.RecomputeScore();
        ApplyDialog = new ApplyDialogViewModel(this, Execution);
        ApplyAll = new ApplyAllViewModel(this, Execution);
        Profiles = new ProfilesViewModel(this);
        _currentPage = Verdict;

        StartFirstScanCommand = new(() =>
        {
            ShowWelcome = false;
            Settings.Save(); // creates the settings file: next launch is not first-run
            _ = Verdict.ScanAsync();
        });

        if (Settings.IsFirstRun)
        {
            ShowWelcome = true;
            Verdict.SetIdle("Welcome — no scan has run yet.");
        }
        else
        {
            _ = Verdict.ScanAsync();
        }
        _ = Scan.ScanAsync(); // hardware inventory in the background
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

public sealed class VerdictItem
{
    private readonly TweakEntry _entry;
    private readonly MainViewModel _main;

    public VerdictItem(TweakEntry entry, string stateNote, MainViewModel main)
    {
        _entry = entry;
        _main = main;
        StateNote = stateNote;
        HowToCommand = new(() => main.ShowKbEntry(entry.Id));
        ApplyCommand = new(() => main.ApplyDialog.Open(entry));
        OpenSettingsCommand = new(() => ExecutionService.OpenSettings(_entry.Apply!.SettingsUri!));
    }

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public string StateNote { get; }
    public bool CanApply => _main.Execution.CanApply(_entry);
    // Show "Open settings" only for gui-only tweaks (applicable ones get Apply).
    public bool CanOpenSettings => !CanApply && _entry.Apply?.SettingsUri is not null;

    /// <summary>At-a-glance capability badge so the user instantly sees what Verdict can do for this
    /// tweak: apply it itself, jump to the right Windows page, or only explain the manual steps.</summary>
    public string KindLabel => CanApply ? "1-CLICK" : CanOpenSettings ? "IMPOSTAZIONI" : "MANUALE";
    public string KindColor => CanApply ? "Ok" : CanOpenSettings ? "Info" : "Neutral";

    public RelayCommand HowToCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
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

    public string Header { get => _header; set => Set(ref _header, value); }
    public string SubHeader { get => _subHeader; set => Set(ref _subHeader, value); }
    public bool IsScanning { get => _isScanning; set => Set(ref _isScanning, value); }
    public int WorthDoing { get => _worthDoing; set => Set(ref _worthDoing, value); }
    public int AlreadyOptimal { get => _alreadyOptimal; set => Set(ref _alreadyOptimal, value); }
    public int PlaceboAvoided { get => _placeboAvoided; set => Set(ref _placeboAvoided, value); }
    public ObservableCollection<VerdictGroup> Groups { get; } = [];

    public int ApplicableRecommendedCount => _applicableRecommended.Count;
    public bool HasApplicableRecommended => _applicableRecommended.Count > 0;
    public string ApplyAllLabel => $"Applica tutti i consigliati ({_applicableRecommended.Count})";

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
            foreach (var r in items) g.Items.Add(new VerdictItem(r.Entry, r.StateNote, main));
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
                group.Items.Add(new VerdictItem(r.Entry, r.StateNote, main));
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
        ? "Ready. Capture reads kernel events for 15 seconds — generate load while it runs."
        : "Diagnostics needs administrator to read kernel events. Everything else works without it.\nRelaunch WPEP as administrator to use this page.";
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
        Status = $"Capturing DPC/ISR for {seconds} seconds…";
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
                ? "No DPC offender found. Your driver stack is healthy — stutter, if any, is coming from the game itself."
                : $"{worst.Driver}: max DPC {worst.MaxUs:F0}µs during capture. This is worth investigating.";
            Status = $"Done — {report.TotalEvents:N0} events analyzed.";
            main.TerminalLine = $"$ wpep diag · {report.CaptureDurationSeconds:F0}s · 0 writes";
        }
        catch (Exception ex)
        {
            // F6: the usual cause of a kernel-session failure is another tracer.
            Status = $"Capture failed: {ex.Message}\n" +
                     "If this persists: another kernel trace session may be running " +
                     "(often LatencyMon, WPR or another capture tool). Close it and retry.";
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
        EvidenceLevel.EvidenceStrong => new("Strong evidence", "Ok"),
        EvidenceLevel.Plausible => new("Plausible", "Info"),
        EvidenceLevel.Controversial => new("Controversial", "Warn"),
        EvidenceLevel.Placebo => new("Placebo", "Neutral"),
        EvidenceLevel.Risky => new("Risky", "Danger"),
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
        ["Applicabili", "Tutti", "Strong evidence", "Plausible", "Controversial", "Placebo", "Risky"];

    public ObservableCollection<KbItemViewModel> Entries { get; } = [];
    public string Footer => _loadError.Length > 0
        ? $"Knowledge base failed to load: {_loadError}"
        : "Every entry cites a primary source. No source, no recommendation.";

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
    private string _status = "Generates the shareable dark-theme HTML report: snapshot, every advisor verdict (placebos included), and measurements when available.";
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
        Status = "Generating…";
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
            Status = $"Report written: {path}";
            main.TerminalLine = "$ wpep report · 0 writes (outside the app folder)";
        }
        catch (Exception ex)
        {
            Status = $"Report failed: {ex.Message}";
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

    public string About =>
        "Verdict — the only optimizer that tells you when to stop optimizing.\n" +
        "(engine codename: WPEP)\n\n" +
        "What WPEP will never do:\n" +
        "  · Write to your system\n" +
        "  · Claim to measure end-to-end input latency\n" +
        "  · Show you an improvement that isn't statistically real\n\n" +
        "Verdict never touches your game. No code injection, no process hooks, no game memory " +
        "access, no overlay. Frame data comes from Windows' own event tracing (ETW) — the same " +
        "passive channel used by Intel PresentMon. We cannot offer formal guarantees on behalf " +
        "of anti-cheat vendors, but WPEP belongs to no category anti-cheat systems target.\n\n" +
        "Portable by design. One folder, no installer, no services, no registry writes. " +
        "Delete the folder and Verdict was never here.\n\n" +
        "License: MIT · V1 read-only";
}
