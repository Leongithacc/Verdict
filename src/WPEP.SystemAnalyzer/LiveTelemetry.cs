using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace WPEP.SystemAnalyzer;

/// <summary>One live GPU read. Ogni campo è null se non disponibile (driver assente, GPU non-NVIDIA,
/// o singola chiamata NVAPI fallita) — mai un valore inventato.</summary>
public sealed record GpuSample(double? UtilPercent, double? TempC, double? CoreClockMhz);

/// <summary>One instantaneous telemetry read. CPU% è il carico dall'ultima chiamata (0 alla prima).</summary>
public sealed record TelemetrySample(double CpuPercent, double RamUsedGb, double RamTotalGb, GpuSample? Gpu);

/// <summary>Sorgente di telemetria live, read-only. Interfaccia per testabilità (fake nei test, VM
/// che pilota un DispatcherTimer in produzione).</summary>
public interface ITelemetrySource
{
    /// <summary>Una lettura istantanea. Sincrona e veloce (nessun ETW pesante).</summary>
    TelemetrySample Sample();
}

/// <summary>Provider di metriche GPU live (separato per poter iniettare NVAPI o un no-op).</summary>
public interface IGpuTelemetry
{
    GpuSample? TrySample();
}

/// <summary>GPU non disponibile (default): sempre null, mai crash.</summary>
public sealed class NoGpuTelemetry : IGpuTelemetry
{
    public GpuSample? TrySample() => null;
}

/// <summary>GPU live via <c>nvidia-smi</c> (il tool ufficiale NVIDIA, installato col driver). Scelta
/// deliberata al posto dell'interop NVAPI diretto: i layout struct NVAPI non sono auditati
/// (HANDOFF_AUDIT §5) e un offset sbagliato causa una AccessViolation NON catchabile in .NET =
/// crash. nvidia-smi è un processo esterno read-only: nessun rischio di corruzione memoria, output
/// CSV stabile e documentato, parsing puro e testabile. Fallback onesto: se nvidia-smi non c'è
/// (GPU non-NVIDIA), TrySample ritorna null e la GUI mostra "solo NVIDIA per ora". Anti-cheat safe.</summary>
public sealed class NvidiaSmiGpuTelemetry : IGpuTelemetry
{
    private const string Query =
        "--query-gpu=utilization.gpu,temperature.gpu,clocks.current.graphics --format=csv,noheader,nounits";
    private readonly string? _exe = Locate();

    public GpuSample? TrySample()
    {
        if (_exe is null) return null;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(_exe, Query)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return null;
            string? line = p.StandardOutput.ReadLine();
            p.WaitForExit(2000);
            return ParseGpuCsv(line);
        }
        catch { return null; } // nvidia-smi assente/lento/errore → nessun dato, mai crash
    }

    /// <summary>Parsa una riga CSV di nvidia-smi ("util, temp, clock"). Campi non-numerici
    /// ("[N/A]") diventano null; se tutti null o riga malformata → null. Puro, testabile.</summary>
    public static GpuSample? ParseGpuCsv(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return null;
        double? util = Num(parts[0]), temp = Num(parts[1]), clock = Num(parts[2]);
        return (util is null && temp is null && clock is null) ? null : new GpuSample(util, temp, clock);
    }

    private static double? Num(string s) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static string? Locate()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
        ];
        return Array.Find(candidates, File.Exists);
    }
}

/// <summary>Math puro del carico CPU dai contatori cumulativi idle/total (GetSystemTimes). Estratto
/// per essere unit-testato senza toccare il SO.</summary>
public static class CpuLoad
{
    /// <summary>Percentuale occupata nell'intervallo, dai delta dei tick idle e totali. Il tempo
    /// "kernel" di Windows INCLUDE l'idle, quindi total = kernel + user e busy = total − idle.</summary>
    public static double Percent(ulong idleDelta, ulong totalDelta) =>
        totalDelta == 0 ? 0 : Math.Clamp((1.0 - (double)idleDelta / totalDelta) * 100.0, 0, 100);
}

/// <summary>Sorgente reale su Windows: CPU via GetSystemTimes (delta tra chiamate), RAM via
/// GlobalMemoryStatusEx, GPU delegata a un <see cref="IGpuTelemetry"/>. Tutto read-only → anti-cheat
/// safe. Mantiene lo stato dei contatori precedenti per il delta CPU.</summary>
public sealed class WindowsTelemetrySource(IGpuTelemetry? gpu = null) : ITelemetrySource
{
    private const double BytesPerGb = 1024.0 * 1024.0 * 1024.0;
    private readonly IGpuTelemetry _gpu = gpu ?? new NoGpuTelemetry();
    private ulong _prevIdle, _prevTotal;
    private bool _hasPrev;

    public TelemetrySample Sample()
    {
        double cpu = 0;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong i = ToU(idle), total = ToU(kernel) + ToU(user); // kernel include idle
            if (_hasPrev)
                cpu = CpuLoad.Percent(i - _prevIdle, total - _prevTotal);
            _prevIdle = i;
            _prevTotal = total;
            _hasPrev = true;
        }

        double usedGb = 0, totalGb = 0;
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            totalGb = mem.ullTotalPhys / BytesPerGb;
            usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / BytesPerGb;
        }

        GpuSample? gpu = null;
        try { gpu = _gpu.TrySample(); } catch { gpu = null; } // la GPU non deve mai rompere il polling

        return new TelemetrySample(cpu, usedGb, totalGb, gpu);
    }

    private static ulong ToU(FILETIME ft) => ((ulong)ft.High << 32) | ft.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Low; public uint High; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
