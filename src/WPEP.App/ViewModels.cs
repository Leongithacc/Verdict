using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using WPEP.Advisor;
using WPEP.Core.Diagnostics;
using WPEP.Core.SystemInfo;
using WPEP.Diagnostics;
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
    public VerdictViewModel Verdict { get; }
    public MeasureWizardViewModel Measure { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public KbViewModel Kb { get; }
    public ReportViewModel Report { get; }
    public SettingsViewModel SettingsPage { get; }

    public ViewModelBase CurrentPage { get => _currentPage; set => Set(ref _currentPage, value); }
    public string TerminalLine { get => _terminalLine; set => Set(ref _terminalLine, value); }

    /// <summary>First-run welcome overlay (EDGE_CASES §2): the moment of trust.
    /// No scan happens until the user clicks "Scan my system".</summary>
    public bool ShowWelcome { get => _showWelcome; set => Set(ref _showWelcome, value); }

    public RelayCommand StartFirstScanCommand { get; }

    public MainViewModel()
    {
        Settings = AppSettings.Load();
        Verdict = new VerdictViewModel(this);
        Measure = new MeasureWizardViewModel(this, Settings);
        Measure.InitCommands();
        Measure.RefreshProcesses();
        Diagnostics = new DiagnosticsViewModel(this);
        Kb = new KbViewModel();
        Report = new ReportViewModel(this);
        SettingsPage = new SettingsViewModel(Settings);
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
    }

    /// <summary>Jump to a KB entry from anywhere (Verdict "How to" buttons).</summary>
    public void ShowKbEntry(string id)
    {
        Kb.Filter = "All";
        Kb.Selected = Kb.Entries.FirstOrDefault(e =>
            e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        CurrentPage = Kb;
    }
}

// ============================== VERDICT ==============================

public sealed class VerdictItem(string id, string name, string stateNote, MainViewModel main)
{
    public string Id => id;
    public string Name => name;
    public string StateNote => stateNote;
    public RelayCommand HowToCommand { get; } = new(() => main.ShowKbEntry(id));
}

public sealed class VerdictGroup(string label, string badgeColorKey)
{
    public string Label { get; } = label;
    public string BadgeColorKey { get; } = badgeColorKey;
    public ObservableCollection<VerdictItem> Items { get; } = [];
    public int Count => Items.Count;
}

public sealed class VerdictViewModel(MainViewModel main) : ViewModelBase
{
    private string _header = "Scanning…";
    private string _subHeader = "Read-only — WPEP never modifies your system";
    private bool _isScanning;
    private int _worthDoing, _alreadyOptimal, _placeboAvoided;

    public string Header { get => _header; set => Set(ref _header, value); }
    public string SubHeader { get => _subHeader; set => Set(ref _subHeader, value); }
    public bool IsScanning { get => _isScanning; set => Set(ref _isScanning, value); }
    public int WorthDoing { get => _worthDoing; set => Set(ref _worthDoing, value); }
    public int AlreadyOptimal { get => _alreadyOptimal; set => Set(ref _alreadyOptimal, value); }
    public int PlaceboAvoided { get => _placeboAvoided; set => Set(ref _placeboAvoided, value); }
    public ObservableCollection<VerdictGroup> Groups { get; } = [];

    public RelayCommand RescanCommand => new(() => _ = ScanAsync());

    public void SetIdle(string header)
    {
        Header = header;
        SubHeader = "Read-only — WPEP never modifies your system";
    }

    public async Task ScanAsync()
    {
        IsScanning = true;
        Header = "Scanning…";
        var sw = Stopwatch.StartNew();
        try
        {
            var (snapshot, recommendations) = await Task.Run(() =>
            {
                var s = SnapshotBuilder.Build(DateTimeOffset.UtcNow);
                var kb = KnowledgeBaseLoader.Load();
                return (s, AdvisorEngine.Advise(s, kb));
            });
            sw.Stop();
            Apply(snapshot, recommendations);
            main.TerminalLine = $"$ wpep advise · {sw.Elapsed.TotalSeconds:F1}s · 0 writes";
        }
        catch (Exception ex)
        {
            Header = "Scan failed";
            SubHeader = ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void Apply(SystemSnapshot snapshot, IReadOnlyList<Recommendation> allRecommendations)
    {
        // Game-specific entries live in their own section and never count toward
        // the system verdict header (R7_COPY_AND_KB3 open question, resolved).
        var recommendations = allRecommendations.Where(r => r.Entry.Game is null).ToArray();
        var gameSpecific = allRecommendations.Where(r => r.Entry.Game is not null).ToArray();

        Groups.Clear();
        var groupDefs = new (Classification[] Classes, string Label, string Color)[]
        {
            ([Classification.Recommended], "Worth doing", "Ok"),
            ([Classification.Optional, Classification.OptionalWithWarning], "Maybe — judge for yourself", "Info"),
            ([Classification.NotRecommended], "Risky — read first", "Danger"),
            ([Classification.Placebo], "Skip it — placebo", "Neutral"),
            ([Classification.AlreadyActive], "Already optimal", "OkDim"),
            ([Classification.NotApplicable], "Not applicable to this PC", "Neutral"),
        };

        foreach (var (classes, label, color) in groupDefs)
        {
            var group = new VerdictGroup(label, color);
            foreach (var r in recommendations.Where(r => classes.Contains(r.Classification)))
                group.Items.Add(new VerdictItem(r.Entry.Id, r.Entry.Name, r.StateNote, main));
            if (group.Items.Count > 0)
                Groups.Add(group);
        }

        foreach (var gameGroup in gameSpecific.GroupBy(r => r.Entry.Game!))
        {
            var group = new VerdictGroup($"Game-specific — {gameGroup.Key}", "Accent");
            foreach (var r in gameGroup.OrderBy(r => r.Entry.EvidenceLevel))
                group.Items.Add(new VerdictItem(r.Entry.Id, r.Entry.Name, r.StateNote, main));
            Groups.Add(group);
        }

        int optimal = recommendations.Count(r => r.Classification == Classification.AlreadyActive);
        int actionable = recommendations.Count(r => r.Classification
            is Classification.AlreadyActive or Classification.Recommended
            or Classification.Optional or Classification.OptionalWithWarning);

        WorthDoing = recommendations.Count(r => r.Classification == Classification.Recommended);
        AlreadyOptimal = optimal;
        PlaceboAvoided = recommendations.Count(r => r.Classification == Classification.Placebo);

        Header = actionable > 0 && optimal == actionable && WorthDoing == 0
            ? "Nothing left to optimize. Seriously. Go play."
            : $"Your system: {optimal}/{actionable} checks optimal";
        SubHeader = $"Last scanned {DateTime.Now:HH:mm} · {snapshot.CpuName} · {snapshot.GpuName} · " +
                    "Read-only — WPEP never modifies your system";
        if (snapshot.IsManagedDevice == true)
            SubHeader += "\n⚠ This looks like a company-managed device. Running third-party " +
                         "diagnostic tools may violate your organization's IT policy. Get IT approval first.";
    }
}

// ============================== DIAGNOSTICS ==============================

public sealed record DpcRow(string Driver, string Events, string MaxUs, string AvgUs, string Spikes);

public sealed class DiagnosticsViewModel(MainViewModel main) : ViewModelBase
{
    private string _status = EtwDpcIsrCollector.IsElevated()
        ? "Ready. Capture reads kernel events for 15 seconds — generate load while it runs."
        : "Diagnostics needs administrator to read kernel events. Everything else works without it.\nRelaunch WPEP as administrator to use this page.";
    private string _verdict = "";
    private bool _isRunning;

    public string Status { get => _status; set => Set(ref _status, value); }
    public string VerdictLine { get => _verdict; set => Set(ref _verdict, value); }
    public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }
    public bool IsElevated => EtwDpcIsrCollector.IsElevated();
    public ObservableCollection<DpcRow> Rows { get; } = [];

    public RelayCommand CaptureCommand => new(() => _ = CaptureAsync(), () => IsElevated && !IsRunning);
    public RelayCommand RelaunchAsAdminCommand => new(() => main.Measure.RelaunchAsAdminCommand.Execute(null));

    private async Task CaptureAsync()
    {
        IsRunning = true;
        Status = "Capturing DPC/ISR for 15 seconds…";
        Rows.Clear();
        VerdictLine = "";
        try
        {
            var report = await Task.Run(() =>
                new EtwDpcIsrCollector().Capture(TimeSpan.FromSeconds(15)));

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
    private string _filter = "All";
    private KbItemViewModel? _selected;
    private string _loadError = "";

    public KbViewModel()
    {
        try
        {
            _all = KnowledgeBaseLoader.Load().Select(e => new KbItemViewModel(e)).ToArray();
        }
        catch (Exception ex)
        {
            _all = [];
            _loadError = ex.Message;
        }
        Refresh();
    }

    public IReadOnlyList<string> Filters { get; } =
        ["All", "Strong evidence", "Plausible", "Controversial", "Placebo", "Risky"];

    public ObservableCollection<KbItemViewModel> Entries { get; } = [];
    public string Footer => _loadError.Length > 0
        ? $"Knowledge base failed to load: {_loadError}"
        : "Every entry cites a primary source. No source, no recommendation.";

    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) Refresh(); }
    }

    public KbItemViewModel? Selected { get => _selected; set => Set(ref _selected, value); }

    private void Refresh()
    {
        Entries.Clear();
        foreach (var item in _all.Where(i => _filter == "All" || i.Badge.Label == _filter)
                     .OrderBy(i => i.Entry.EvidenceLevel).ThenBy(i => i.Id))
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
                var html = Reporting.ReportBuilder.BuildHtml(new Reporting.ReportData(
                    DateTimeOffset.UtcNow, snapshot, recommendations, noise, comparison));

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

    public IReadOnlyList<string> Themes { get; } = [.. ThemePresets.All.Keys];

    public string SelectedTheme
    {
        get => _settings.Theme;
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
        "WPEP — the only optimizer that tells you when to stop optimizing.\n\n" +
        "What WPEP will never do:\n" +
        "  · Write to your system\n" +
        "  · Claim to measure end-to-end input latency\n" +
        "  · Show you an improvement that isn't statistically real\n\n" +
        "WPEP never touches your game. No code injection, no process hooks, no game memory " +
        "access, no overlay. Frame data comes from Windows' own event tracing (ETW) — the same " +
        "passive channel used by Intel PresentMon. We cannot offer formal guarantees on behalf " +
        "of anti-cheat vendors, but WPEP belongs to no category anti-cheat systems target.\n\n" +
        "Portable by design. One folder, no installer, no services, no registry writes. " +
        "Delete the folder and WPEP was never here.\n\n" +
        "License: MIT · V1 read-only";
}
