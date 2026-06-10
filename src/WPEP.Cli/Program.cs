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
    case "compare":
        return RunCompare(args.Skip(1).ToArray());
    case "noise":
        return RunNoise(args.Skip(1).ToArray());
    case "kb":
        return RunKb(args.Skip(1).ToArray());
    case "analyze":
        return RunAnalyze(args.Skip(1).ToArray());
    case "report":
        return RunReport(args.Skip(1).ToArray());
    case "advise":
        return RunAdvise();
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

static int RunCompare(string[] args)
{
    string? baselineDir = null;
    string? postDir = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--baseline" or "-b" when i + 1 < args.Length:
                baselineDir = args[++i];
                break;
            case "--post" or "-p" when i + 1 < args.Length:
                postDir = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}");
                return 2;
        }
    }

    if (baselineDir is null || postDir is null)
    {
        Console.Error.WriteLine("Servono --baseline <dir> e --post <dir> (cartelle di run salvate da 'bench').");
        return 2;
    }

    WPEP.Statistics.ComparisonEngine.ComparisonReport report;
    try
    {
        var baseline = BenchmarkRunStore.LoadDirectory(baselineDir);
        var post = BenchmarkRunStore.LoadDirectory(postDir);
        report = WPEP.Statistics.ComparisonEngine.Compare(baseline, post);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Confronto fallito: {ex.Message}");
        return 1;
    }

    Console.WriteLine($"Baseline: {report.BaselineRuns} run   Post: {report.PostRuns} run");
    if (!report.Conclusive)
        Console.WriteLine(
            $"""
            ⚠ Meno di {WPEP.Statistics.ComparisonEngine.MinRunsForConclusion} run per lato:
              i numeri sotto sono INDICATIVI, non conclusioni (spec §6).
            """);
    Console.WriteLine();
    Console.WriteLine($"{"Metrica",-26} {"Base",9} {"Post",9} {"Δ%",7} {"p",7} {"CI 95%",20}  Verdetto");
    Console.WriteLine(new string('-', 96));

    foreach (var m in report.Metrics)
    {
        string verdict = m.Verdict switch
        {
            WPEP.Statistics.Verdict.Improvement => "MIGLIORAMENTO",
            WPEP.Statistics.Verdict.Regression => "PEGGIORAMENTO",
            _ => "nessun effetto misurabile",
        };
        Console.WriteLine(
            $"{m.Metric,-26} {m.BaselineMedian,9:F2} {m.PostMedian,9:F2} {m.DeltaPercent,6:+0.0;-0.0;0.0}% " +
            $"{m.PValue,7:F3} [{m.Ci.Lower,8:F3}, {m.Ci.Upper,8:F3}]  {verdict}");
    }

    Console.WriteLine();
    if (report.Metrics.All(m => m.Verdict == WPEP.Statistics.Verdict.NoMeasurableEffect))
        Console.WriteLine("Conclusione onesta: nessun effetto misurabile su questo sistema.");
    Console.WriteLine(
        """
        Note di lettura:
        - Frametime: più basso = meglio. Δ% negativo = post più veloce.
        - L'unità statistica è la RUN, non il singolo frame: i frame aggregati
          renderebbero "significativo" qualsiasi delta microscopico (placebo).
        - p = Mann–Whitney (permutation test), CI = bootstrap sulla differenza
          delle mediane tra run.
        """);
    return 0;
}

static int RunNoise(string[] args)
{
    string? dir = null;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--dir" or "-d" when i + 1 < args.Length:
                dir = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}");
                return 2;
        }
    }

    if (dir is null)
    {
        Console.Error.WriteLine("Manca --dir <cartella> (run ripetute della STESSA configurazione).");
        return 2;
    }

    WPEP.Statistics.NoiseFloorAnalyzer.NoiseReport report;
    try
    {
        report = WPEP.Statistics.NoiseFloorAnalyzer.Analyze(BenchmarkRunStore.LoadDirectory(dir));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Analisi fallita: {ex.Message}");
        return 1;
    }

    Console.WriteLine($"Noise floor da {report.Runs} run della stessa configurazione");
    if (!report.Reliable)
        Console.WriteLine(
            $"⚠ Meno di {WPEP.Statistics.NoiseFloorAnalyzer.MinRunsForEstimate} run: stima poco affidabile.");
    Console.WriteLine();
    Console.WriteLine($"{"Metrica",-26} {"Mediana",9} {"Min",9} {"Max",9} {"Range",9} {"Range%",8}");
    Console.WriteLine(new string('-', 75));
    foreach (var m in report.Metrics)
    {
        Console.WriteLine(
            $"{m.Metric,-26} {m.Median,9:F2} {m.Min,9:F2} {m.Max,9:F2} {m.Range,9:F2} {m.RangePercent,7:F1}%");
    }

    Console.WriteLine();
    var worst = report.Metrics.MaxBy(m => m.RangePercent)!;
    Console.WriteLine(
        $"""
        Come leggerlo:
        - Questo è quanto il sistema varia DA SOLO, senza cambiare nulla.
        - Qualsiasi "miglioramento" più piccolo di questi range è rumore, punto (spec §6).
        - Range% alto (>10-15%) = scenario poco ripetibile: i confronti rileveranno
          solo effetti enormi. Peggiore qui: {worst.Metric} ({worst.RangePercent:F0}%).
          Per abbassarlo: stesso percorso/scenario per ogni run (replay, mappa privata,
          benchmark integrato), niente partite live mescolate.
        """);
    return 0;
}

static int RunAnalyze(string[] args)
{
    string? jsonPath = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--json" && i + 1 < args.Length)
            jsonPath = args[++i];
    }

    var s = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);

    Console.WriteLine("System snapshot (read-only)\n");
    Console.WriteLine($"CPU         : {Unknown(s.CpuName)}  ({s.CpuCores?.ToString() ?? "?"}C/{s.CpuThreads?.ToString() ?? "?"}T{(s.CpuIsX3D ? ", X3D" : "")})");
    Console.WriteLine($"GPU         : {Unknown(s.GpuName)}  driver {Unknown(s.GpuDriverVersion)}");
    Console.WriteLine($"RAM         : {(s.RamTotalGb?.ToString("F0") ?? "?")} GB @ {(s.RamSpeedMtps?.ToString() ?? "?")} MT/s (dichiarata: {(s.RamRatedMtps?.ToString() ?? "?")})");
    Console.WriteLine($"Chassis     : {(s.IsDesktop switch { true => "desktop", false => "portatile", null => "sconosciuto" })}");
    Console.WriteLine($"Monitor     : {(s.MonitorCurrentHz?.ToString() ?? "?")}Hz attivi / max {(s.MonitorMaxHz?.ToString() ?? "?")}Hz alla risoluzione corrente");
    Console.WriteLine($"Power plan  : {Unknown(s.PowerPlanName)}");
    Console.WriteLine($"HAGS        : {OnOff(s.HagsEnabled)}");
    Console.WriteLine($"Game Mode   : {OnOff(s.GameModeEnabled)}");
    Console.WriteLine($"Mem.Integrity (HVCI): {OnOff(s.HvciEnabled)}");
    Console.WriteLine($"Pointer precision   : {OnOff(s.PointerPrecisionEnabled)}");
    Console.WriteLine($"Game DVR (registrazione bg): {OnOff(s.GameDvrEnabled)}");
    Console.WriteLine($"Rete attiva : {(s.ActiveNicIsWifi switch { true => "Wi-Fi", false => "cablata", null => "sconosciuta" })}");
    Console.WriteLine($"SysMain     : {OnOff(s.SysMainRunning)}    Avvio rapido: {OnOff(s.FastStartupEnabled)}    MPO disattivato: {OnOff(s.MpoDisabled)}");
    Console.WriteLine($"Pagefile auto: {OnOff(s.PagefileAutomatic)}   Voci autostart (registry): {(s.StartupAppsCount?.ToString() ?? "?")}");
    Console.WriteLine($"IPv6 disattivato: {OnOff(s.Ipv6Disabled)}   Win Search: {OnOff(s.SearchIndexingRunning)}   HDD presenti: {OnOff(s.AnyHddPresent)}");

    if (jsonPath is not null)
    {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(s,
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nSnapshot JSON: {jsonPath}");
    }
    return 0;

    static string Unknown(string v) => v.Length == 0 ? "sconosciuto" : v;
    static string OnOff(bool? v) => v switch { true => "attivo", false => "disattivo", null => "sconosciuto" };
}

static int RunReport(string[] args)
{
    string outPath = "wpep-report.html";
    string? noiseDir = null;
    string? baselineDir = null;
    string? postDir = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--out" or "-o" when i + 1 < args.Length:
                outPath = args[++i];
                break;
            case "--runs" when i + 1 < args.Length:
                noiseDir = args[++i];
                break;
            case "--baseline" or "-b" when i + 1 < args.Length:
                baselineDir = args[++i];
                break;
            case "--post" or "-p" when i + 1 < args.Length:
                postDir = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}");
                return 2;
        }
    }

    try
    {
        var entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
        var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
        var recommendations = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries);

        WPEP.Statistics.NoiseFloorAnalyzer.NoiseReport? noise = null;
        if (noiseDir is not null)
            noise = WPEP.Statistics.NoiseFloorAnalyzer.Analyze(BenchmarkRunStore.LoadDirectory(noiseDir));

        WPEP.Statistics.ComparisonEngine.ComparisonReport? comparison = null;
        if (baselineDir is not null && postDir is not null)
            comparison = WPEP.Statistics.ComparisonEngine.Compare(
                BenchmarkRunStore.LoadDirectory(baselineDir),
                BenchmarkRunStore.LoadDirectory(postDir));

        var html = WPEP.Reporting.ReportBuilder.BuildHtml(new WPEP.Reporting.ReportData(
            DateTimeOffset.UtcNow, snapshot, recommendations, noise, comparison));
        File.WriteAllText(outPath, html);
        Console.WriteLine($"Report scritto: {Path.GetFullPath(outPath)}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Report fallito: {ex.Message}");
        return 1;
    }
}

static int RunAdvise()
{
    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try
    {
        entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}");
        return 1;
    }

    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    var recommendations = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries);

    Console.WriteLine($"Advisor — {recommendations.Count} voci valutate su questo sistema");
    Console.WriteLine($"({snapshot.CpuName}, {snapshot.GpuName})\n");

    foreach (var group in recommendations.GroupBy(r => r.Classification))
    {
        Console.WriteLine($"== {ClassificationLabel(group.Key)} ==");
        foreach (var r in group)
        {
            Console.WriteLine($"  {r.Entry.Id,-42} {r.StateNote}");
        }
        Console.WriteLine();
    }

    Console.WriteLine(
        """
        Per ogni voce: 'wpep kb show <id>' → descrizione, fonti, passi manuali, rollback.
        Prima di applicare qualcosa: baseline con 'wpep bench' (5+ run, scenario ripetibile),
        UNA modifica alla volta, poi 'wpep compare'. Senza misura non è un miglioramento,
        è una speranza.
        """);
    return 0;
}

static string ClassificationLabel(WPEP.Advisor.Classification c) => c switch
{
    WPEP.Advisor.Classification.Recommended => "CONSIGLIATO (evidenza forte)",
    WPEP.Advisor.Classification.Optional => "OPZIONALE (plausibile, da misurare)",
    WPEP.Advisor.Classification.OptionalWithWarning => "OPZIONALE CON RISERVA (evidenza controversa)",
    WPEP.Advisor.Classification.Placebo => "PLACEBO — non lo tocchiamo",
    WPEP.Advisor.Classification.NotRecommended => "SCONSIGLIATO (rischio reale)",
    WPEP.Advisor.Classification.AlreadyActive => "GIÀ A POSTO",
    WPEP.Advisor.Classification.NotApplicable => "NON APPLICABILE A QUESTO SISTEMA",
    _ => c.ToString(),
};

static int RunKb(string[] args)
{
    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try
    {
        entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}");
        return 1;
    }

    if (args.Length >= 2 && args[0] == "show")
    {
        var entry = entries.FirstOrDefault(e =>
            e.Id.Equals(args[1], StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            Console.Error.WriteLine($"Voce '{args[1]}' non trovata. Usa 'wpep kb' per la lista.");
            return 2;
        }
        PrintKbEntry(entry);
        return 0;
    }

    Console.WriteLine($"Knowledge Base: {entries.Count} voci (dettaglio: wpep kb show <id>)\n");
    Console.WriteLine($"{"Id",-42} {"Categoria",-11} {"Evidenza",-16} {"Rischio",-8}");
    Console.WriteLine(new string('-', 80));
    foreach (var e in entries.OrderBy(e => e.EvidenceLevel).ThenBy(e => e.Id))
    {
        Console.WriteLine($"{e.Id,-42} {e.Category,-11} {EvidenceLabel(e.EvidenceLevel),-16} {e.Risk.ToString().ToLowerInvariant(),-8}");
    }
    Console.WriteLine(
        """

        Legenda evidenza (spec §5):
          forte         fonte primaria + misurabile      → può essere consigliato
          plausibile    ragionamento valido, non provato → opzionale
          controversa   fonti in disaccordo              → opzionale con warning
          placebo       nessuna evidenza / smentito      → non lo tocchiamo
          rischiosa     guadagno possibile, rischio reale → warning forte
        """);
    return 0;
}

static void PrintKbEntry(WPEP.KnowledgeBase.TweakEntry e)
{
    Console.WriteLine($"\n{e.Name}  [{e.Id}]");
    Console.WriteLine(new string('=', Math.Min(70, e.Name.Length + e.Id.Length + 4)));
    Console.WriteLine($"Categoria : {e.Category}    Evidenza: {EvidenceLabel(e.EvidenceLevel)}    Rischio: {e.Risk.ToString().ToLowerInvariant()}");
    Console.WriteLine($"\n{e.Description}");
    Console.WriteLine($"\nImpatto atteso:\n  {e.ExpectedImpact}");
    if (!string.IsNullOrWhiteSpace(e.RiskNotes))
        Console.WriteLine($"\nNote di rischio:\n  {e.RiskNotes}");
    Console.WriteLine($"\nPassi manuali:\n  {e.ManualSteps}");
    Console.WriteLine($"\nRollback:\n  {e.Rollback}");
    if (e.Sources.Count > 0)
    {
        Console.WriteLine("\nFonti:");
        foreach (var s in e.Sources)
            Console.WriteLine($"  {s}");
    }
    Console.WriteLine($"\nMisurabile con wpep bench/diag: {(e.Measurable ? "sì" : "no (effetto sotto la soglia di misura)")}");
}

static string EvidenceLabel(WPEP.KnowledgeBase.EvidenceLevel level) => level switch
{
    WPEP.KnowledgeBase.EvidenceLevel.EvidenceStrong => "forte",
    WPEP.KnowledgeBase.EvidenceLevel.Plausible => "plausibile",
    WPEP.KnowledgeBase.EvidenceLevel.Controversial => "controversa",
    WPEP.KnowledgeBase.EvidenceLevel.Placebo => "placebo",
    WPEP.KnowledgeBase.EvidenceLevel.Risky => "rischiosa",
    _ => level.ToString(),
};

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

          wpep compare --baseline <dir> --post <dir>
              Confronto statistico onesto tra due set di run (Mann–Whitney +
              bootstrap CI). Se il delta è dentro la varianza, lo dice.

          wpep noise --dir <dir>
              Misura la varianza naturale run-to-run da run ripetute della
              stessa configurazione. Sotto questa soglia è tutto rumore.

          wpep analyze [--json file.json]
              Snapshot read-only di hardware e config rilevante (CPU, GPU,
              monitor, power plan, HAGS, Game Mode, HVCI, pointer precision).

          wpep advise
              Incrocia lo snapshot con la knowledge base: cosa è già a posto,
              cosa è consigliato, cosa è placebo, cosa è rischioso — su QUESTO pc.

          wpep kb [show <id>]
              Knowledge base dei tweak: grading di evidenza con fonti primarie,
              passi manuali e rollback. Include i placebo, spiegando perché.

          wpep report [--out file.html] [--runs dir] [--baseline dir --post dir]
              Report HTML (tema scuro) con snapshot + advisor; opzionalmente
              noise floor e confronto statistico. Condivisibile.

          wpep tools install-presentmon
              Scarica PresentMon (Intel, MIT) nella cartella tools di WPEP.

        diag e bench richiedono terminale elevato (vincolo ETW di Windows).
        V1 non scrive MAI nulla sul sistema: misura e basta.
        """);
}
