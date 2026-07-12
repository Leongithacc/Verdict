using System.Windows.Media;
using System.Windows.Threading;
using WPEP.SystemAnalyzer;

namespace WPEP.App;

/// <summary>Pagina "Live": telemetria in tempo reale (CPU/RAM sempre, GPU via nvidia-smi se NVIDIA).
/// Read-only puro → anti-cheat safe. Il DispatcherTimer gira SOLO quando la pagina è visibile
/// (Start su nav-enter, Stop su nav-leave e minimize) per non spawnare nvidia-smi in background.</summary>
public sealed class LiveViewModel : ViewModelBase
{
    private const int HistoryLen = 60;      // ~1 minuto di storia a 1 Hz
    private const double BoxW = 260, BoxH = 40;

    private readonly ITelemetrySource _source = new WindowsTelemetrySource(new NvidiaSmiGpuTelemetry());
    private readonly DispatcherTimer _timer;
    private readonly Queue<double> _cpuHistory = new();
    private TelemetrySample? _last;
    private bool _polling;

    public LiveViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _ = Poll();
    }

    public string CpuText => _last is null ? "—" : $"{_last.CpuPercent:F0}%";
    public string RamText => _last is null ? "—"
        : $"{(_last.RamTotalGb > 0 ? _last.RamUsedGb / _last.RamTotalGb * 100 : 0):F0}%  ·  {_last.RamUsedGb:F1} / {_last.RamTotalGb:F1} GB";
    public bool HasGpu => _last?.Gpu is not null;
    public bool GpuUnavailable => _last is not null && _last.Gpu is null;
    public string GpuUtilText => _last?.Gpu?.UtilPercent is { } u ? $"{u:F0}%" : "—";
    public string GpuTempText => _last?.Gpu?.TempC is { } t ? $"{t:F0}°C" : "—";
    public string GpuClockText => _last?.Gpu?.CoreClockMhz is { } c ? $"{c:F0} MHz" : "—";

    private PointCollection _cpuTrend = [];
    public PointCollection CpuTrend { get => _cpuTrend; private set => Set(ref _cpuTrend, value); }
    public bool HasTrend => CpuTrend.Count >= 2;

    /// <summary>Avvia il polling con un primo campione immediato (niente attesa di 1s a vuoto).</summary>
    public void Start()
    {
        if (_timer.IsEnabled) return;
        _ = Poll();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    /// <summary>Campiona in background (nvidia-smi può impiegare ~100-300 ms: NON deve bloccare il
    /// thread UI) e aggiorna i binding al ritorno sul contesto UI. La guardia evita sovrapposizioni
    /// se un campione è più lento del tick.</summary>
    private async Task Poll()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            _last = await Task.Run(() => _source.Sample());
        }
        finally { _polling = false; }

        _cpuHistory.Enqueue(_last.CpuPercent);
        while (_cpuHistory.Count > HistoryLen) _cpuHistory.Dequeue();
        BuildCpuTrend();

        Raise(nameof(CpuText));
        Raise(nameof(RamText));
        Raise(nameof(HasGpu));
        Raise(nameof(GpuUnavailable));
        Raise(nameof(GpuUtilText));
        Raise(nameof(GpuTempText));
        Raise(nameof(GpuClockText));
    }

    private void BuildCpuTrend()
    {
        var vals = _cpuHistory.ToList();
        if (vals.Count < 2) { CpuTrend = []; Raise(nameof(HasTrend)); return; }
        var pts = new PointCollection();
        for (int i = 0; i < vals.Count; i++)
        {
            double x = i / (double)(vals.Count - 1) * BoxW;
            double y = BoxH - vals[i] / 100.0 * BoxH; // 0% in basso, 100% in alto
            pts.Add(new System.Windows.Point(x, y));
        }
        CpuTrend = pts;
        Raise(nameof(HasTrend));
    }
}
