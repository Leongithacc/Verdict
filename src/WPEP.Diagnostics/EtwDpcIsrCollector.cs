using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using WPEP.Core.Diagnostics;

namespace WPEP.Diagnostics;

/// <summary>
/// Real-time kernel ETW session collecting DPC and ISR events.
/// Requires elevation: kernel providers cannot be enabled otherwise.
/// TraceEvent exposes the routine execution time directly as ElapsedTimeMSec.
/// </summary>
public sealed class EtwDpcIsrCollector
{
    public const string SessionName = "WPEP-DpcIsr";

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Captures for the given duration and returns the aggregated report.
    /// Driver resolution combines a psapi snapshot with ImageLoad events seen
    /// during the session, so late-loading drivers still resolve.
    /// </summary>
    public DpcIsrReport Capture(TimeSpan duration, Action<string>? progress = null)
    {
        if (!IsElevated())
            throw new UnauthorizedAccessException(
                "Le sessioni ETW kernel richiedono un terminale elevato (admin).");

        var modules = new List<DriverModule>(KernelDriverEnumerator.Enumerate());
        progress?.Invoke($"Driver caricati rilevati: {modules.Count}");

        var pending = new List<DpcIsrEvent>(capacity: 1 << 16);
        var sw = Stopwatch.StartNew();

        using (var session = new TraceEventSession(SessionName))
        {
            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.Interrupt |
                KernelTraceEventParser.Keywords.DeferedProcedureCalls |
                KernelTraceEventParser.Keywords.ImageLoad);

            var kernel = session.Source.Kernel;

            void OnDpc(DPCTraceData data, KernelEventKind kind)
            {
                double durationUs = data.ElapsedTimeMSec * 1000.0;
                if (durationUs < 0)
                    return;
                pending.Add(new DpcIsrEvent(
                    kind, data.Routine, data.ProcessorNumber,
                    data.TimeStampRelativeMSec, durationUs));
            }

            kernel.PerfInfoDPC += d => OnDpc(d, KernelEventKind.Dpc);
            kernel.PerfInfoTimerDPC += d => OnDpc(d, KernelEventKind.TimerDpc);
            kernel.PerfInfoThreadedDPC += d => OnDpc(d, KernelEventKind.ThreadedDpc);

            kernel.PerfInfoISR += data =>
            {
                double durationUs = data.ElapsedTimeMSec * 1000.0;
                if (durationUs < 0)
                    return;
                pending.Add(new DpcIsrEvent(
                    KernelEventKind.Isr, data.Routine, data.ProcessorNumber,
                    data.TimeStampRelativeMSec, durationUs));
            };

            kernel.ImageGroup += data =>
            {
                // Kernel-mode images load under the System process (PID 0/4);
                // captured so drivers loaded mid-session still resolve.
                if (data.ProcessID is 0 or 4 && data.ImageBase != 0)
                    modules.Add(new DriverModule(
                        Path.GetFileName(data.FileName), (ulong)data.ImageBase, (ulong)data.ImageSize));
            };

            using var timer = new Timer(_ => session.Stop(), null, duration, Timeout.InfiniteTimeSpan);
            session.Source.Process(); // blocks until session.Stop()
        }

        sw.Stop();
        progress?.Invoke($"Eventi raccolti: {pending.Count}");

        var aggregator = new DpcIsrAggregator(new DriverMap(modules));
        foreach (var e in pending)
            aggregator.Add(e);

        return aggregator.Build(sw.Elapsed.TotalSeconds);
    }
}
