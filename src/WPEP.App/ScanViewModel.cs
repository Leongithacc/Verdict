using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WPEP.SystemAnalyzer;

namespace WPEP.App;

public sealed record FindingRow(string Text, string ColorKey, string Mark);

/// <summary>V3 §1 — the hardware "build-sheet": full WMI inventory + diagnostic badges,
/// styled with the current theme and exportable as a PNG (export lives in code-behind,
/// which has the visual to render).</summary>
public sealed class ScanViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private bool _isScanning;
    private string _motherboard = "—", _bios = "—", _cpu = "—", _ram = "—";

    public ScanViewModel(AppSettings settings) => _settings = settings;

    /// <summary>Multi-monitor optimizer (Lab feature): the Displays section only shows when on.</summary>
    public bool ShowMultiMonitor => _settings.IsFeatureEnabled(WPEP.Execution.FeatureCatalog.MultiMonitor);

    // ── Rig DNA (Lab feature): generated trading-card identity of this build ──
    private string _rigCode = "", _rigTier = "", _rigTierColor = "Accent";
    private System.Windows.Media.Brush _rigTint = System.Windows.Media.Brushes.Transparent;
    public bool ShowRigDna => _settings.IsFeatureEnabled(WPEP.Execution.FeatureCatalog.RigDna);
    public string RigCode { get => _rigCode; set => Set(ref _rigCode, value); }
    public string RigTier { get => _rigTier; set => Set(ref _rigTier, value); }
    public string RigTierColor { get => _rigTierColor; set => Set(ref _rigTierColor, value); }
    public System.Windows.Media.Brush RigTint { get => _rigTint; set => Set(ref _rigTint, value); }
    public ObservableCollection<string> RigTraits { get; } = [];

    // ── Fresh-install score (Lab feature): third-party startup drift ──
    private int _freshScore;
    private string _freshBand = "", _freshColor = "Ok", _freshHeadline = "";
    public bool ShowFreshInstall => _settings.IsFeatureEnabled(WPEP.Execution.FeatureCatalog.FreshInstall);
    public int FreshScore { get => _freshScore; set => Set(ref _freshScore, value); }
    public string FreshBand { get => _freshBand; set => Set(ref _freshBand, value); }
    public string FreshColor { get => _freshColor; set => Set(ref _freshColor, value); }
    public string FreshHeadline { get => _freshHeadline; set => Set(ref _freshHeadline, value); }
    public bool HasFreshResult => FreshHeadline.Length > 0;
    public ObservableCollection<string> FreshThirdParty { get; } = [];

    // ── Time Machine (Lab feature): what changed since last scan ──
    private string _timelineHeadline = "";
    public bool ShowTimeMachine => _settings.IsFeatureEnabled(WPEP.Execution.FeatureCatalog.TimeMachine);
    public string TimelineHeadline { get => _timelineHeadline; set => Set(ref _timelineHeadline, value); }
    public bool HasTimelineResult => TimelineHeadline.Length > 0;
    public ObservableCollection<TimelineChange> TimelineChanges { get; } = [];

    public bool IsScanning { get => _isScanning; set => Set(ref _isScanning, value); }
    public string Motherboard { get => _motherboard; set => Set(ref _motherboard, value); }
    public string Bios { get => _bios; set => Set(ref _bios, value); }
    public string Cpu { get => _cpu; set => Set(ref _cpu, value); }
    public string Ram { get => _ram; set => Set(ref _ram, value); }

    public ObservableCollection<string> MemoryModules { get; } = [];
    public ObservableCollection<string> Disks { get; } = [];
    public ObservableCollection<string> Gpus { get; } = [];
    public ObservableCollection<FindingRow> Findings { get; } = [];
    public ObservableCollection<string> Displays { get; } = [];
    public ObservableCollection<FindingRow> MonitorFindings { get; } = [];

    /// <summary>RAM EXPO/XMP state from the last scan (tri-state, null until known). The Verdict
    /// Score reads this; <see cref="ScanCompleted"/> lets it recompute when the scan finishes.</summary>
    public bool? ExpoEnabled { get; private set; }

    /// <summary>Raised on the UI thread after a hardware scan finishes (build-sheet ready).</summary>
    public event Action? ScanCompleted;

    public RelayCommand RescanCommand => new(() => _ = ScanAsync());

    public async Task ScanAsync()
    {
        IsScanning = true;
        try
        {
            var hw = await Task.Run(HardwareScanner.Scan);
            ExpoEnabled = hw.ExpoEnabled;
            Motherboard = hw.Motherboard;
            Bios = hw.Bios + (hw.BiosDate.Length > 0 ? $"  ({hw.BiosDate})" : "");
            Cpu = $"{hw.Cpu}  ·  {hw.Cores?.ToString() ?? "?"}C/{hw.Threads?.ToString() ?? "?"}T";
            Ram = $"{hw.RamTotalGb?.ToString("F0") ?? "?"} GB";

            MemoryModules.Clear();
            foreach (var m in hw.Memory)
                MemoryModules.Add($"{m.CapacityGb:F0} GB @ {m.SpeedMtps?.ToString() ?? "?"} MT/s   ·   {m.Vendor} {m.Part}".Trim());
            Disks.Clear();
            foreach (var d in hw.Disks)
                Disks.Add($"{d.Model}   ·   {d.CapacityGb:F0} GB");
            Gpus.Clear();
            foreach (var g in hw.Gpus)
                Gpus.Add(g);
            Findings.Clear();
            foreach (var f in hw.Findings)
                Findings.Add(ToRow(f));

            // Rig DNA (Lab feature): generate the build's collectible identity from the inventory.
            Raise(nameof(ShowRigDna));
            RigTraits.Clear();
            if (ShowRigDna)
            {
                var dna = RigDna.Compute(hw);
                RigCode = dna.Code;
                RigTier = dna.Tier;
                RigTierColor = dna.TierColor;
                RigTint = TintFromHue(dna.Hue);
                foreach (var tr in dna.Traits) RigTraits.Add(tr);
            }

            // Time Machine (Lab feature): diff key system facts against the last saved snapshot.
            Raise(nameof(ShowTimeMachine));
            TimelineChanges.Clear();
            TimelineHeadline = "";
            if (ShowTimeMachine)
            {
                int startup = await Task.Run(() =>
                    FreshInstallScanner.EnumerateStartup().Count(i => !i.IsMicrosoft));
                var state = new SystemState(System.DateTime.Now.ToString("o"),
                    hw.ExpoEnabled, hw.RamTotalGb, hw.Gpus.FirstOrDefault() ?? "", hw.Bios, startup);
                var prev = SystemTimeline.LoadAll().LastOrDefault();
                if (prev is null)
                {
                    TimelineHeadline = "Prima istantanea salvata: questa è la tua baseline. " +
                                       "Torna dopo una scansione futura per vedere cos'è cambiato.";
                    SystemTimeline.Save(state);
                }
                else
                {
                    var diff = SystemTimeline.Diff(prev, state);
                    foreach (var c in diff) TimelineChanges.Add(c);
                    TimelineHeadline = diff.Count == 0
                        ? "Nessun cambiamento rilevante dall'ultima istantanea. Sistema stabile."
                        : $"{diff.Count} cambiament{(diff.Count == 1 ? "o" : "i")} dall'ultima istantanea:";
                    if (diff.Count > 0) SystemTimeline.Save(state); // only record distinct states
                }
                Raise(nameof(HasTimelineResult));
            }

            // Fresh-install score (Lab feature): count third-party startup drift (WMI).
            Raise(nameof(ShowFreshInstall));
            FreshThirdParty.Clear();
            FreshHeadline = "";
            if (ShowFreshInstall)
            {
                var report = await Task.Run(() =>
                    FreshInstallScanner.Analyze(FreshInstallScanner.EnumerateStartup()));
                FreshScore = report.Score;
                FreshBand = report.Band;
                FreshColor = report.BandColor;
                FreshHeadline = report.Headline;
                foreach (var i in report.ThirdParty) FreshThirdParty.Add(i.Name);
                Raise(nameof(HasFreshResult));
            }

            // Multi-monitor optimizer (Lab feature): only scan displays when the module is on.
            Displays.Clear();
            MonitorFindings.Clear();
            Raise(nameof(ShowMultiMonitor));
            if (ShowMultiMonitor)
            {
                var displays = await Task.Run(DisplayScanner.Enumerate);
                foreach (var d in displays)
                    Displays.Add($"{d.Name}   ·   {d.Width}×{d.Height} @ {d.RefreshHz} Hz" +
                                 (d.IsPrimary ? "   ·   PRIMARIO" : ""));
                foreach (var f in DisplayScanner.Analyze(displays))
                    MonitorFindings.Add(ToRow(f));
            }
        }
        finally
        {
            IsScanning = false;
        }
        ScanCompleted?.Invoke();
    }

    /// <summary>Makes the Lab-gated sections (Rig DNA, Multi-monitor) reflect their current toggle
    /// when the user returns to the Scan page: re-raises visibility, and re-scans only if a now-on
    /// section is missing its data. Turning a section off just clears it (no rescan).</summary>
    public async Task EnsureLabSectionsAsync()
    {
        Raise(nameof(ShowMultiMonitor));
        Raise(nameof(ShowRigDna));
        Raise(nameof(ShowFreshInstall));
        Raise(nameof(ShowTimeMachine));
        if (!ShowMultiMonitor) { Displays.Clear(); MonitorFindings.Clear(); }
        if (!ShowRigDna) { RigTraits.Clear(); RigCode = ""; }
        if (!ShowFreshInstall) { FreshThirdParty.Clear(); FreshHeadline = ""; }
        if (!ShowTimeMachine) { TimelineChanges.Clear(); TimelineHeadline = ""; }

        bool needMon = ShowMultiMonitor && Displays.Count == 0;
        bool needRig = ShowRigDna && RigCode.Length == 0;
        bool needFresh = ShowFreshInstall && FreshHeadline.Length == 0;
        bool needTimeline = ShowTimeMachine && TimelineHeadline.Length == 0;
        if ((needMon || needRig || needFresh || needTimeline) && !IsScanning)
            await ScanAsync(); // full rescan repopulates every section from one inventory
    }

    /// <summary>A vivid-but-dark tint from the Rig DNA hue (HSL, fixed S/L), frozen for the card.</summary>
    private static System.Windows.Media.Brush TintFromHue(int hue)
    {
        double h = hue / 60.0, s = 0.55, l = 0.5;
        double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h % 2 - 1)), m = l - c / 2;
        (double r, double g, double b) = h switch
        {
            < 1 => (c, x, 0.0), < 2 => (x, c, 0.0), < 3 => (0.0, c, x),
            < 4 => (0.0, x, c), < 5 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        byte B(double v) => (byte)Math.Clamp((v + m) * 255, 0, 255);
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(B(r), B(g), B(b)));
        brush.Freeze();
        return brush;
    }

    private static FindingRow ToRow(Finding f) => new(f.Text,
        f.Level switch { "Warn" => "Warn", "Ok" => "Ok", _ => "Info" },
        f.Level switch { "Warn" => "⚠", "Ok" => "✓", _ => "ℹ" });
}
