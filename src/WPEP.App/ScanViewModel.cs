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
    private bool _isScanning;
    private string _motherboard = "—", _bios = "—", _cpu = "—", _ram = "—";

    public bool IsScanning { get => _isScanning; set => Set(ref _isScanning, value); }
    public string Motherboard { get => _motherboard; set => Set(ref _motherboard, value); }
    public string Bios { get => _bios; set => Set(ref _bios, value); }
    public string Cpu { get => _cpu; set => Set(ref _cpu, value); }
    public string Ram { get => _ram; set => Set(ref _ram, value); }

    public ObservableCollection<string> MemoryModules { get; } = [];
    public ObservableCollection<string> Disks { get; } = [];
    public ObservableCollection<string> Gpus { get; } = [];
    public ObservableCollection<FindingRow> Findings { get; } = [];

    public RelayCommand RescanCommand => new(() => _ = ScanAsync());

    public async Task ScanAsync()
    {
        IsScanning = true;
        try
        {
            var hw = await Task.Run(HardwareScanner.Scan);
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
                Findings.Add(new FindingRow(f.Text,
                    f.Level switch { "Warn" => "Warn", "Ok" => "Ok", _ => "Info" },
                    f.Level switch { "Warn" => "⚠", "Ok" => "✓", _ => "ℹ" }));
        }
        finally
        {
            IsScanning = false;
        }
    }
}
