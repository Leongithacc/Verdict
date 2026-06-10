using System.Text.Json;
using WPEP.Benchmark;
using WPEP.Core.Benchmark;
using WPEP.Core.Diagnostics;
using WPEP.Core.Platform;
using WPEP.Diagnostics;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

switch (args[0])
{
    case "diag":
        return RunDiag(args.Skip(1).ToArray());
    case "bench":
        return await RunBench(args.Skip(1).ToArray());
    case "tools" when args.Length >= 2 && args[1] == "install-presentmon":
        return await InstallPresentMon();
    default:
        Console.Error.WriteLine($"Comando sconosciuto: {args[0]}");
        PrintUsage();
        return 2;
}

static int RunDiag(string[] args)
{
    int seconds = 30;
    string? jsonPath = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--seconds" or "-s" when i + 1 < args.Length && int.TryParse(args[i + 1], out var s):
                seconds = Math.Clamp(s, 1, 600);
                i++;
                break;
            case "--json" when i + 1 < args.Length:
                jsonPath = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}");
                return 2;
        }
    }

    if (!Elevation.IsElevated())
    {
        PrintElevationRequired("Le sessioni ETW kernel");
        return 3;
    }

    Console.WriteLine($"Cattura DPC/ISR per {seconds}s... (genera carico ora: gioco, video, input)");
    var collector = new EtwDpcIsrCollector();
    DpcIsrReport report;
    try
    {
        report = collector.Capture(TimeSpan.FromSeconds(seconds), msg => Console.WriteLine($"  {msg}"));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Cattura fallita: {ex.Message}");
        return 1;
    }

    PrintDiagReport(report);

    if (jsonPath is not null)
    {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nReport JSON: {jsonPath}");
    }

    return 0;
}

static async Task<int> RunBench(string[] args)
{
    string? processName = null;
    int seconds = 60;
    int runs = 1;
    string label = "run";
    string outDir = "wpep-runs";
    string? presentMonPath = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--process" or "-p" when i + 1 < args.Length:
                processName = args[++i];
                break;
            case "--seconds" or "-s" when i + 1 < args.Length && int.TryParse(args[i + 1], out var s):
                seconds = Math.Clamp(s, 5, 3600);
                i++;
                break;
            case "--runs" or "-n" when i + 1 < args.Length && int.TryParse(args[i + 1], out var n):
                runs = Math.Clamp(n, 1, 50);
                i++;
                break;
            case "--label" or "-l" when i + 1 < args.Length:
                label = args[++i];
                break;
            case "--out" or "-o" when i + 1 < args.Length:
                outDir = args[++i];
                break;
            case "--presentmon" when i + 1 < args.Length:
                presentMonPath = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}");
                return 2;
        }
    }

    if (processName is null)
    {
        Console.Error.WriteLine("Manca --process <nome.exe> (il processo del gioco da misurare).");
        return 2;
    }

    if (!Elevation.IsElevated())
    {
        PrintElevationRequired("PresentMon (sessione ETW)");
        return 3;
    }

    var exe = PresentMonLocator.Find(presentMonPath);
    if (exe is null)
    {
        Console.Error.WriteLine(
            """
            PresentMon non trovato. Installalo con:
              wpep tools install-presentmon
            oppure indica il percorso con --presentmon <path>.
            """);
        return 4;
    }

    Directory.CreateDirectory(outDir);
    var runner = new PresentMonRunner(exe);

    Console.WriteLine($"Benchmark '{label}': {runs} run da {seconds}s su {processName}");
    if (runs < 5)
        Console.WriteLine(
            "Nota: con meno di 5 run per configurazione nessun confronto sarà statisticamente\n" +
            "difendibile (spec §6). Va bene per esplorare, non per concludere.");
    Console.WriteLine();

    Console.WriteLine($"{"Run",-6} {"Frame",8} {"Avg FPS",9} {"Median",8} {"1% low",8} {"0.2% low",9}");
    Console.WriteLine(new string('-', 55));

    for (int r = 1; r <= runs; r++)
    {
        PresentMonRunner.CaptureResult result;
        try
        {
            result = runner.Capture(processName, seconds);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Run {r} fallita: {ex.Message}");
            return 1;
        }

        var m = result.Metrics;
        Console.WriteLine(
            $"{r,-6} {m.FrameCount,8:N0} {m.AvgFps,9:F1} {m.MedianFps,8:F1} " +
            $"{m.OnePercentLowFps,8:F1} {m.ZeroPointTwoPercentLowFps,9:F1}");

        if (m.FrameCount < MetricsCalculator.LowSampleThreshold)
            Console.WriteLine(
                $"       ⚠ solo {m.FrameCount} frame: i percentili di coda (1%/0.2% low) sono rumore.");
        if (m.ExcludedNonApplicationFrames > 0)
            Console.WriteLine(
                $"       ({m.ExcludedNonApplicationFrames} frame generati (FG) esclusi dalle metriche)");

        var run = new BenchmarkRun(
            label, processName, DateTimeOffset.UtcNow, seconds, m, result.FrameTimesMs);
        var path = Path.Combine(outDir, $"{label}-{r:D2}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(run));
    }

    Console.WriteLine();
    Console.WriteLine(
        $"""
        Run salvate in: {Path.GetFullPath(outDir)}
        Queste sono misure, non conclusioni. Per dire se una modifica ha effetto serve
        il confronto statistico baseline/post (comando 'compare', milestone R3).
        """);
    return 0;
}

static async Task<int> InstallPresentMon()
{
    try
    {
        await PresentMonInstaller.InstallAsync(Console.WriteLine);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Download fallito: {ex.Message}");
        return 1;
    }
}

static void PrintElevationRequired(string what)
{
    Console.Error.WriteLine(
        $"""
        Questo comando richiede un terminale elevato (amministratore).

        {what} è riservato agli amministratori da Windows.
        Apri un terminale "Esegui come amministratore" e rilancia il comando.

        Cosa fa esattamente: legge eventi diagnostici (sola lettura),
        non modifica nulla sul sistema.
        """);
}

static void PrintDiagReport(DpcIsrReport report)
{
    Console.WriteLine();
    Console.WriteLine($"Durata cattura : {report.CaptureDurationSeconds:F1}s");
    Console.WriteLine($"Eventi totali  : {report.TotalEvents:N0} (non risolti: {report.UnresolvedEvents:N0})");
    Console.WriteLine();

    if (report.TotalEvents == 0)
    {
        Console.WriteLine("Nessun evento DPC/ISR catturato. Finestra troppo breve o sistema in idle profondo.");
        return;
    }

    Console.WriteLine($"{"Driver",-28} {"Eventi",10} {"Max µs",9} {"Avg µs",8} {">100µs",7} {">500µs",7} {">1ms",6}");
    Console.WriteLine(new string('-', 80));
    foreach (var d in report.Drivers.Take(20))
    {
        Console.WriteLine(
            $"{Truncate(d.Driver, 28),-28} {d.TotalCount,10:N0} {d.MaxUs,9:F1} {d.AvgUs,8:F1} " +
            $"{d.SpikesOver100Us,7:N0} {d.SpikesOver500Us,7:N0} {d.SpikesOver1000Us,6:N0}");
    }

    Console.WriteLine();
    Console.WriteLine(
        """
        Lettura del report — cosa conta davvero:
        - Max µs > 500 ricorrenti su un driver = candidato reale per stutter/audio glitch.
        - Avg alto ma Max basso = driver occupato ma regolare: di solito non è un problema.
        - Questa è latenza di esecuzione DPC/ISR, NON "input latency" end-to-end
          (quella non è misurabile in puro software).
        - Uno spike singolo in una cattura breve può essere caso: ripeti la cattura
          prima di accusare un driver.
        """);
}

static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

static void PrintUsage()
{
    Console.WriteLine(
        """
        WPEP — Windows Performance Engineering Platform (V1, read-only)

        Uso:
          wpep diag [--seconds N] [--json file.json]
              Cattura ETW DPC/ISR e mostra la classifica dei driver sospetti.

          wpep bench --process <gioco.exe> [--seconds N] [--runs N]
                     [--label nome] [--out dir] [--presentmon path]
              Misura frametime con PresentMon: avg/median FPS, 1% low, 0.2% low.
              Salva ogni run in JSON per il confronto statistico (R3).

          wpep tools install-presentmon
              Scarica PresentMon (Intel, MIT) nella cartella tools di WPEP.

        diag e bench richiedono terminale elevato (vincolo ETW di Windows).
        V1 non scrive MAI nulla sul sistema: misura e basta.
        """);
}
