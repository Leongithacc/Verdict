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

    /// <summary>Cheap refresh of just the Displays section (no WMI) so toggling the Multi-monitor
    /// module in the Lab takes effect when the user returns to the Scan page.</summary>
    public async Task RefreshMultiMonitorAsync()
    {
        Raise(nameof(ShowMultiMonitor));
        if (!ShowMultiMonitor)
        {
            Displays.Clear();
            MonitorFindings.Clear();
            return;
        }
        if (Displays.Count > 0) return; // already populated by the last full scan
        var displays = await Task.Run(DisplayScanner.Enumerate);
        Displays.Clear();
        foreach (var d in displays)
            Displays.Add($"{d.Name}   ·   {d.Width}×{d.Height} @ {d.RefreshHz} Hz" +
                         (d.IsPrimary ? "   ·   PRIMARIO" : ""));
        MonitorFindings.Clear();
        foreach (var f in DisplayScanner.Analyze(displays))
            MonitorFindings.Add(ToRow(f));
    }

    private static FindingRow ToRow(Finding f) => new(f.Text,
        f.Level switch { "Warn" => "Warn", "Ok" => "Ok", _ => "Info" },
        f.Level switch { "Warn" => "⚠", "Ok" => "✓", _ => "ℹ" });
}
