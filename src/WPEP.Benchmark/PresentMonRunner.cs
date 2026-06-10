using System.Diagnostics;
using WPEP.Core.Benchmark;

namespace WPEP.Benchmark;

/// <summary>
/// Runs one timed PresentMon capture for a target process and parses the result.
/// PresentMon needs elevation for its ETW session, same as the diag module.
/// </summary>
public sealed class PresentMonRunner(string presentMonPath)
{
    public sealed record CaptureResult(RunMetrics Metrics, IReadOnlyList<double> FrameTimesMs);

    public CaptureResult Capture(string processName, int seconds, Action<string>? progress = null)
    {
        if (!processName.Contains('.'))
            processName += ".exe";

        var csvPath = Path.Combine(Path.GetTempPath(), $"wpep-bench-{Guid.NewGuid():N}.csv");
        var args =
            $"--process_name \"{processName}\" --output_file \"{csvPath}\" " +
            $"--timed {seconds} --terminate_after_timed --no_console_stats " +
            $"--session_name WPEP-Bench --stop_existing_session";

        progress?.Invoke($"PresentMon su '{processName}' per {seconds}s...");

        var psi = new ProcessStartInfo(presentMonPath, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Avvio di PresentMon fallito.");
            string stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(TimeSpan.FromSeconds(seconds + 60)))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("PresentMon non è terminato nel tempo previsto.");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"PresentMon è uscito con codice {process.ExitCode}. {stderr}".Trim());

            if (!File.Exists(csvPath))
                throw new InvalidOperationException(
                    $"PresentMon non ha prodotto il CSV. Il processo '{processName}' " +
                    "sta presentando frame? (Deve essere in esecuzione e renderizzare.)");

            using var reader = new StreamReader(csvPath);
            var parsed = PresentMonCsvParser.Parse(reader);
            if (parsed.Samples.Count == 0)
                throw new InvalidOperationException(
                    $"CSV senza frame per '{processName}': processo non trovato o nessun present.");

            var metrics = MetricsCalculator.Compute(parsed.Samples, parsed.ExcludedNonApplicationFrames);
            return new CaptureResult(
                metrics, parsed.Samples.Select(s => s.FrameTimeMs).ToArray());
        }
        finally
        {
            try { File.Delete(csvPath); } catch (IOException) { }
        }
    }
}
