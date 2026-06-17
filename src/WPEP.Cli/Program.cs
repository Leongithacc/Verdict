using System.Text.Json;
using Microsoft.Win32;
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
    case "validate":
        return RunValidate(args.Skip(1).ToArray());
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
    case "apply":
        return RunApply(args.Skip(1).ToArray());
    case "apply-all":
        return RunApplyAll(args.Skip(1).ToArray());
    case "changes":
        return RunChanges();
    case "undo":
        return RunUndo(args.Skip(1).ToArray());
    case "selftest":
        return RunSelfTest();
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

    // F3: fail fast se il processo non esiste, prima di sprecare una cattura.
    var processBase = Path.GetFileNameWithoutExtension(processName);
    if (System.Diagnostics.Process.GetProcessesByName(processBase).Length == 0)
    {
        Console.Error.WriteLine(
            $"Nessun processo '{processBase}' trovato. Il gioco è avviato?");
        return 5;
    }

    // F10: fotografa l'ambiente e allegalo a ogni run.
    var envSnapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    var environment = new RunEnvironment(
        envSnapshot.GpuName, envSnapshot.GpuDriverVersion,
        envSnapshot.DisplayWidth, envSnapshot.DisplayHeight,
        envSnapshot.MonitorCurrentHz, envSnapshot.PowerPlanGuid);

    Directory.CreateDirectory(outDir);
    var runner = new PresentMonRunner(exe);

    Console.WriteLine($"Benchmark '{label}': {runs} run da {seconds}s su {processName}");
    Console.WriteLine(
        "Warm-up: shader cache e temperature richiedono qualche minuto di gioco\n" +
        "prima della run 1 — la prima run dopo un cambio driver/patch va scartata.");
    if (runs < 5)
        Console.WriteLine(
            "Nota: con meno di 5 run per configurazione nessun confronto sarà statisticamente\n" +
            "difendibile (spec §6). Va bene per esplorare, non per concludere.");
    Console.WriteLine();

    var collected = new List<BenchmarkRun>(runs);
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
            label, processName, DateTimeOffset.UtcNow, seconds, m, result.FrameTimesMs, environment);
        var path = Path.Combine(outDir, $"{label}-{r:D2}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(run));
        collected.Add(run);
    }

    Console.WriteLine();
    // F5: outlier flag anche in CLI, mai esclusione silenziosa.
    foreach (var outlier in WPEP.Statistics.OutlierDetector.Find(collected))
        Console.WriteLine(
            $"⚠ Run {outlier.RunNumber} sembra un outlier (mediana {1000 / outlier.MedianMs:F0} fps " +
            $"contro ~{1000 / outlier.GroupMedianMs:F0} del gruppo): cambio scena, alt-tab o shader? " +
            "Valuta di rifare il gruppo.");

    // Noise gate preview (HANDOFF_R7 §1): avvisa SUBITO se lo scenario è rumoroso,
    // prima che l'utente perda tempo col post.
    if (collected.Count >= 3)
    {
        var medians = collected.Select(r => r.Metrics.MedianFrameTimeMs).ToArray();
        double mde = WPEP.Statistics.Mde.Percent(medians);
        if (mde > WPEP.Statistics.Mde.DefaultGateThresholdPercent)
            Console.WriteLine(
                $"⚠ SCENARIO RUMOROSO: con queste run si rilevano solo effetti ≥{mde:F0}% " +
                $"(gate: {WPEP.Statistics.Mde.DefaultGateThresholdPercent:F0}%).\n" +
                "  Un compare su questo scenario NON emetterà verdetti. Usa uno scenario\n" +
                "  più ripetibile (benchmark integrato, mappa creativa con route fissa).");
        else
            Console.WriteLine(
                $"Scenario utilizzabile: effetto minimo rilevabile ≈ {mde:F1}% sulla mediana.");
    }

    Console.WriteLine(
        $"""
        Run salvate in: {Path.GetFullPath(outDir)}
        Queste sono misure, non conclusioni. Per dire se una modifica ha effetto serve
        il confronto statistico baseline/post (comando 'compare').
        """);
    return 0;
}

static int RunCompare(string[] args)
{
    string? baselineDir = null;
    string? postDir = null;
    double gate = WPEP.Statistics.Mde.DefaultGateThresholdPercent;

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
            case "--gate" when i + 1 < args.Length && double.TryParse(args[i + 1],
                System.Globalization.CultureInfo.InvariantCulture, out var g):
                gate = Math.Clamp(g, 1, 50);
                i++;
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

        // F10: verdetto bloccato se l'ambiente è cambiato tra le run.
        var env = WPEP.Statistics.EnvironmentValidator.Validate(baseline, post);
        if (env.Warning is not null)
            Console.WriteLine($"⚠ {env.Warning}");
        if (!env.Valid)
        {
            Console.Error.WriteLine($"VERDETTO BLOCCATO. {env.BlockReason}");
            return 6;
        }

        report = WPEP.Statistics.ComparisonEngine.Compare(baseline, post, gate);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Confronto fallito: {ex.Message}");
        return 1;
    }

    PrintComparison(report);
    return report.GateTriggered ? 7 : 0;
}

static void PrintComparison(WPEP.Statistics.ComparisonEngine.ComparisonReport report)
{
    Console.WriteLine($"Baseline: {report.BaselineRuns} run   Post: {report.PostRuns} run");
    if (!report.Conclusive)
        Console.WriteLine(
            $"""
            ⚠ Meno di {WPEP.Statistics.ComparisonEngine.MinRunsForConclusion} run per lato:
              i numeri sotto sono INDICATIVI, non conclusioni (spec §6).
            """);
    Console.WriteLine();
    Console.WriteLine($"{"Metrica",-26} {"Base",9} {"Post",9} {"Δ%",7} {"p",7} {"MDE%",6} {"CI 95%",20}  Verdetto");
    Console.WriteLine(new string('-', 104));

    foreach (var m in report.Metrics)
    {
        string verdict = m.Verdict switch
        {
            WPEP.Statistics.Verdict.Improvement => "MIGLIORAMENTO",
            WPEP.Statistics.Verdict.Regression => "PEGGIORAMENTO",
            WPEP.Statistics.Verdict.ScenarioTooNoisy => "— (troppo rumore)",
            _ => $"nessun effetto (soglia {m.MdePercent:F1}%)",
        };
        Console.WriteLine(
            $"{m.Metric,-26} {m.BaselineMedian,9:F2} {m.PostMedian,9:F2} {m.DeltaPercent,6:+0.0;-0.0;0.0}% " +
            $"{m.PValue,7:F3} {m.MdePercent,5:F1}% [{m.Ci.Lower,8:F3}, {m.Ci.Upper,8:F3}]  {verdict}");
    }

    Console.WriteLine();
    if (report.GateTriggered)
    {
        Console.WriteLine(
            $"""
            NESSUN VERDETTO. Lo scenario è troppo rumoroso per rilevare effetti sotto il
            {report.Metrics[0].MdePercent:F0}% (gate: {report.GateThresholdPercent:F0}%). Non è il tweak a essere inutile:
            è la misura che qui non può vedere niente. Passa a uno scenario ripetibile
            (benchmark integrato, mappa creativa con route fissa) e rifai baseline + post.
            """);
        return;
    }
    if (report.Metrics.All(m => m.Verdict == WPEP.Statistics.Verdict.NoMeasurableEffect))
        Console.WriteLine(
            "Conclusione onesta: nessun effetto misurabile su questo sistema.\n" +
            "Fai rollback della modifica, salvo altri motivi per tenerla.");
    Console.WriteLine(
        """
        Note di lettura:
        - Frametime: più basso = meglio. Δ% negativo = post più veloce.
        - MDE% = effetto minimo rilevabile con il rumore di questa baseline.
        - L'unità statistica è la RUN, non il singolo frame: i frame aggregati
          renderebbero "significativo" qualsiasi delta microscopico (placebo).
        - p = Mann–Whitney (permutation test), CI = bootstrap sulla differenza
          delle mediane tra run.
        """);
}

static int RunValidate(string[] args)
{
    string? dirA = null;
    string? dirB = null;
    string expect = "none";

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--a" when i + 1 < args.Length:
                dirA = args[++i];
                break;
            case "--b" when i + 1 < args.Length:
                dirB = args[++i];
                break;
            case "--expect" when i + 1 < args.Length:
                expect = args[++i].ToLowerInvariant();
                break;
            default:
                Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}");
                return 2;
        }
    }

    if (dirA is null || dirB is null || expect is not ("none" or "effect"))
    {
        Console.Error.WriteLine(
            """
            Uso: wpep validate --a <dir> --b <dir> --expect none|effect
              --expect none    A/A test: due gruppi raccolti SENZA cambiare nulla.
                               Atteso: nessun effetto. Se ne trova uno → falso positivo.
              --expect effect  Known-effect: una modifica garantita e grande (es. DLSS
                               Quality vs nativo). Atteso: effetto rilevato.
            La pipeline va certificata così PRIMA di fidarsi dei verdetti sui tweak.
            """);
        return 2;
    }

    try
    {
        var a = BenchmarkRunStore.LoadDirectory(dirA);
        var b = BenchmarkRunStore.LoadDirectory(dirB);

        var env = WPEP.Statistics.EnvironmentValidator.Validate(a, b);
        if (env.Warning is not null)
            Console.WriteLine($"⚠ {env.Warning}");
        if (!env.Valid && expect == "none")
        {
            // Per un A/A test l'ambiente DEVE essere identico: se è cambiato,
            // il test non è un A/A.
            Console.Error.WriteLine($"VALIDAZIONE IMPOSSIBILE. {env.BlockReason}");
            return 6;
        }

        var result = WPEP.Statistics.PipelineValidator.Run(a, b,
            expect == "none"
                ? WPEP.Statistics.PipelineValidator.Expectation.None
                : WPEP.Statistics.PipelineValidator.Expectation.Effect);

        Console.WriteLine(result.Summary);
        Console.WriteLine();
        PrintComparison(result.Report);
        return result.Passed ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Validazione fallita: {ex.Message}");
        return 1;
    }
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
    Console.WriteLine($"GPU temp    : {(s.GpuTempC?.ToString() ?? "?")}°C (throttling termico: {OnOff(s.GpuThermalThrottling)})   CPU load: {(s.CpuLoadPercent?.ToString() ?? "?")}%   CPU temp (ACPI): {(s.CpuTempC?.ToString("F0") ?? "n/d")}°C");

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
    var all = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries);
    var recommendations = all.Where(r => r.Entry.Game is null).ToArray();
    var gameSpecific = all.Where(r => r.Entry.Game is not null).ToArray();

    Console.WriteLine($"Advisor — {recommendations.Length} voci di sistema valutate");
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

    foreach (var gameGroup in gameSpecific.GroupBy(r => r.Entry.Game!))
    {
        if (snapshot.GameInstalled(gameGroup.Key) == false)
        {
            Console.WriteLine($"(voci {gameGroup.Key} nascoste: gioco non rilevato su questo PC — 'wpep kb' per vederle)\n");
            continue;
        }
        Console.WriteLine($"== PER-GIOCO: {gameGroup.Key.ToUpperInvariant()} (impostazioni in-game, fuori dal conteggio sistema) ==");
        foreach (var r in gameGroup.OrderBy(r => r.Entry.EvidenceLevel).ThenBy(r => r.Entry.Id))
        {
            Console.WriteLine($"  {r.Entry.Id,-42} [{EvidenceLabel(r.Entry.EvidenceLevel)}]");
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

// ===================== APPLY / UNDO (Execution Engine V2, scrive sul sistema) =====================
// Default = dry-run: stampa il piano, NON scrive. Scrive solo con --yes. Stessa sicurezza
// della GUI: solo apply-spec della KB, niente placebo/gui-only, journal + verify, undo per sessione.

static WPEP.Execution.ExecutionEngine NewEngine() =>
    new(new WPEP.Execution.RealRegistryAccess(),
        WPEP.Execution.ExecutionEngine.DefaultJournalDirectory);

static bool EntryNeedsAdmin(WPEP.KnowledgeBase.TweakEntry e) =>
    e.Apply?.Method == "bcdedit" ||
    (e.Apply?.Operations.Any(o =>
        o.Path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
        o.Path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) ?? false);

static bool CanApplyEntry(WPEP.KnowledgeBase.TweakEntry e) =>
    e.Apply is { Method: "registry" or "powercfg" or "powercfg-value" or "bcdedit" } &&
    e.EvidenceLevel != WPEP.KnowledgeBase.EvidenceLevel.Placebo;

static int RunApply(string[] args)
{
    bool yes = args.Contains("--yes") || args.Contains("-y");
    var id = args.FirstOrDefault(a => !a.StartsWith('-'));
    if (id is null) { Console.Error.WriteLine("Uso: wpep apply <id> [--yes]"); return 2; }

    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try { entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load(); }
    catch (Exception ex) { Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}"); return 1; }

    var entry = entries.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    if (entry is null) { Console.Error.WriteLine($"Voce '{id}' non trovata. 'wpep kb' per la lista."); return 2; }
    if (!CanApplyEntry(entry))
    {
        Console.Error.WriteLine($"'{entry.Id}' non è applicabile automaticamente " +
            $"({entry.Apply?.GuiOnlyReason ?? "placebo o solo manuale"}). 'wpep kb show {entry.Id}' per i passi manuali.");
        return 2;
    }
    if (EntryNeedsAdmin(entry) && !Elevation.IsElevated())
    {
        Console.Error.WriteLine($"'{entry.Id}' scrive in HKLM/boot: serve un terminale amministratore.");
        return 3;
    }

    var engine = NewEngine();
    WPEP.Execution.ExecutionPlan plan;
    try { plan = engine.BuildPlan(entry); }
    catch (Exception ex) { Console.Error.WriteLine($"Impossibile costruire il piano: {ex.Message}"); return 1; }

    Console.WriteLine($"\n{entry.Name}  [{entry.Id}]");
    Console.WriteLine("Dry run — esattamente cosa cambierà:");
    Console.WriteLine(plan.Describe());
    if (plan.RequiresReboot) Console.WriteLine("\n(richiede un riavvio per avere effetto)");
    bool risky = entry.Risk is WPEP.KnowledgeBase.RiskLevel.High or WPEP.KnowledgeBase.RiskLevel.Medium
                 || entry.EvidenceLevel == WPEP.KnowledgeBase.EvidenceLevel.Risky;
    if (risky && !string.IsNullOrWhiteSpace(entry.RiskNotes))
        Console.WriteLine($"\n⚠ RISCHIO: {entry.RiskNotes}");

    if (!yes)
    {
        Console.WriteLine("\nNiente è stato scritto. Rilancia con --yes per applicare " +
            "(journaled; annulla con 'wpep undo').");
        return 0;
    }
    try
    {
        var file = engine.Execute(plan);
        Console.WriteLine($"\nApplicato e verificato. Journal: {Path.GetFileName(file)}");
        Console.WriteLine($"Undo: wpep undo {Path.GetFileName(file)}   (o 'wpep undo last')");
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine($"FERMATO: {ex.Message}"); return 1; }
}

static int RunApplyAll(string[] args)
{
    bool yes = args.Contains("--yes") || args.Contains("-y");
    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try { entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load(); }
    catch (Exception ex) { Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}"); return 1; }

    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    var recs = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries)
        .Where(r => r.Entry.Game is null
                    && r.Classification == WPEP.Advisor.Classification.Recommended
                    && CanApplyEntry(r.Entry))
        .Select(r => r.Entry).ToList();

    if (recs.Count == 0)
    {
        Console.WriteLine("Nessun tweak consigliato E applicabile su questo PC. (Di solito è un bel segno.)");
        return 0;
    }

    bool elevated = Elevation.IsElevated();
    var engine = NewEngine();
    var ready = new List<WPEP.Execution.ExecutionPlan>();
    int adminSkipped = 0;

    // Never apply two mutually-exclusive tweaks in one batch.
    var (kept, conflicts) = WPEP.Advisor.ConflictResolver.Resolve(recs);
    Console.WriteLine("Dry run — Applica tutti i consigliati:\n");
    foreach (var d in conflicts)
        Console.WriteLine($"• {d.Entry.Name}\n    [saltato: {d.Reason}]");
    foreach (var e in kept)
    {
        if (EntryNeedsAdmin(e) && !elevated)
        {
            adminSkipped++;
            Console.WriteLine($"• {e.Name}\n    [serve amministratore — saltato]");
            continue;
        }
        try { var p = engine.BuildPlan(e); ready.Add(p); Console.WriteLine($"• {e.Name}\n{p.Describe()}"); }
        catch (Exception ex) { Console.WriteLine($"• {e.Name}\n    [non applicabile: {ex.Message}]"); }
    }
    if (adminSkipped > 0)
        Console.WriteLine($"\n{adminSkipped} richiedono un terminale amministratore (rilancia da admin per includerli).");
    if (ready.Count == 0) { Console.WriteLine("\nNiente da applicare adesso."); return 0; }

    if (!yes)
    {
        Console.WriteLine($"\nNiente scritto. Rilancia con --yes per applicare i {ready.Count} piani " +
            "(ognuno journaled, undo singolo con 'wpep undo').");
        return 0;
    }
    var (applied, stopped) = engine.ExecuteAll(ready);
    Console.WriteLine(stopped is null
        ? $"\nApplicati e verificati {applied} tweak. 'wpep changes' per la lista."
        : $"\nApplicati {applied}, poi FERMATO a {stopped}. I già applicati restano journaled e annullabili.");
    return stopped is null ? 0 : 1;
}

static int RunChanges()
{
    var sessions = WPEP.Execution.ExecutionEngine.ListSessions(
        WPEP.Execution.ExecutionEngine.DefaultJournalDirectory);
    if (sessions.Count == 0) { Console.WriteLine("Nessuna modifica applicata. Verdict non ha scritto nulla."); return 0; }

    Console.WriteLine($"{sessions.Count} sessioni journaled (più recenti in fondo):\n");
    foreach (var f in sessions)
    {
        try
        {
            var s = JsonSerializer.Deserialize<WPEP.Execution.JournalSession>(File.ReadAllText(f));
            string tweak = s?.Entries.FirstOrDefault()?.TweakId ?? "?";
            bool allUndone = s is { Entries.Count: > 0 } && s.Entries.All(e => e.Undone);
            Console.WriteLine($"  {Path.GetFileName(f),-54} {tweak} {(allUndone ? "[annullato]" : "[attivo]")}");
        }
        catch { Console.WriteLine($"  {Path.GetFileName(f)}  (illeggibile)"); }
    }
    Console.WriteLine("\nAnnulla con: wpep undo <nomefile>   (o 'wpep undo last')");
    return 0;
}

static int RunUndo(string[] args)
{
    var target = args.FirstOrDefault(a => !a.StartsWith('-'));
    if (target is null) { Console.Error.WriteLine("Uso: wpep undo <nomefile-sessione|last>"); return 2; }

    var dir = WPEP.Execution.ExecutionEngine.DefaultJournalDirectory;
    var sessions = WPEP.Execution.ExecutionEngine.ListSessions(dir);
    if (sessions.Count == 0) { Console.Error.WriteLine("Nessuna sessione da annullare."); return 2; }

    string file = target.Equals("last", StringComparison.OrdinalIgnoreCase)
        ? sessions[^1]
        : sessions.FirstOrDefault(s => Path.GetFileName(s).Equals(target, StringComparison.OrdinalIgnoreCase))
          ?? (File.Exists(target) ? target : "");
    if (string.IsNullOrEmpty(file))
    {
        Console.Error.WriteLine($"Sessione '{target}' non trovata. 'wpep changes' per la lista.");
        return 2;
    }

    var engine = NewEngine();
    try
    {
        int n = engine.Undo(file);
        Console.WriteLine(n > 0
            ? $"Annullate {n} modifiche da {Path.GetFileName(file)}."
            : "Era già annullata.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Undo fallito (serve admin per HKLM/boot?): {ex.Message}");
        return 1;
    }
}

// Esercita il path di scrittura REALE (RealRegistryAccess + ExecutionEngine: write,
// verify rileggendo, journal, undo) su una chiave USA-E-GETTA. Non tocca nessuna
// impostazione reale e usa un journal temporaneo (non sporca 'wpep changes').
static int RunSelfTest()
{
    const string scratch = @"HKCU\Software\VerdictSelfTest\Probe";
    var reg = new WPEP.Execution.RealRegistryAccess();
    var tmpJournal = Path.Combine(Path.GetTempPath(), "verdict-selftest-journal");

    Console.WriteLine("Verdict self-test — verifica il motore di apply (write+verify+undo) su QUESTO PC.");
    Console.WriteLine($"Chiave usa-e-getta: {scratch}  (nessuna impostazione reale viene toccata)\n");

    bool ok = true;
    try
    {
        reg.Delete(scratch); // pulisci eventuali residui di un run precedente

        var entry = new WPEP.KnowledgeBase.TweakEntry
        {
            Id = "selftest-probe",
            Name = "Self-test probe",
            Category = "background",
            Description = "scratch",
            ExpectedImpact = "scratch",
            EvidenceLevel = WPEP.KnowledgeBase.EvidenceLevel.Plausible,
            Sources = ["https://localhost"],
            Risk = WPEP.KnowledgeBase.RiskLevel.None,
            Rollback = "auto",
            ManualSteps = "n/a",
            Measurable = false,
            Apply = new WPEP.KnowledgeBase.ApplySpec
            {
                Method = "registry",
                Operations = [new WPEP.KnowledgeBase.ApplyOperation
                    { Path = scratch, ValueAfter = "424242", Kind = "dword" }],
            },
        };

        var engine = new WPEP.Execution.ExecutionEngine(reg, tmpJournal);

        var plan = engine.BuildPlan(entry);
        Console.WriteLine($"1) BuildPlan legge il valore corrente:  OK  (before: {(plan.Operations[0].ExistedBefore ? plan.Operations[0].Before : "<non impostato>")})");

        var file = engine.Execute(plan);
        var after = reg.Read(scratch);
        bool wrote = after.Exists && after.Value == "424242";
        Console.WriteLine($"2) Execute + verify rilettura:          {(wrote ? "OK  (scritto e riletto 424242)" : "FALLITO")}");
        Console.WriteLine($"   journal scritto: {Path.GetFileName(file)}");
        ok &= wrote;

        int undone = engine.Undo(file);
        bool gone = !reg.Read(scratch).Exists;
        Console.WriteLine($"3) Undo ripristina lo stato precedente: {(undone > 0 && gone ? "OK  (valore rimosso, com'era prima)" : "FALLITO")}");
        ok &= undone > 0 && gone;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"SELF-TEST ECCEZIONE: {ex.Message}");
        ok = false;
    }
    finally
    {
        // Pulizia totale: valore, sottochiave scratch, journal temporaneo.
        try { reg.Delete(scratch); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\VerdictSelfTest", throwOnMissingSubKey: false); } catch { }
        try { if (Directory.Exists(tmpJournal)) Directory.Delete(tmpJournal, recursive: true); } catch { }
    }

    Console.WriteLine($"\nRisultato: {(ok ? "PASS — il motore di apply funziona su questo PC (registry write+verify+undo)." : "FAIL — vedi sopra.")}");
    Console.WriteLine("NB: questo valida il metodo 'registry'. I path reali powercfg/bcdedit\n" +
                      "vanno verificati applicando un tweak vero (richiedono scrivere su power/boot).");
    return ok ? 0 : 1;
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
              bootstrap CI + noise gate). Tre esiti: effetto / nessun effetto /
              scenario troppo rumoroso (= nessun verdetto). Blocca il verdetto
              se l'ambiente è cambiato tra le run.

          wpep validate --a <dir> --b <dir> --expect none|effect
              Certifica la pipeline: A/A test (atteso: nessun effetto) e
              known-effect test (atteso: effetto rilevato). Da fare PRIMA di
              fidarsi dei verdetti sui tweak.

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

          wpep apply <id> [--yes]
              Applica un tweak della KB. SENZA --yes mostra solo il dry-run
              (before→after) e non scrive nulla. Con --yes: scrive, verifica
              rileggendo, e salva il journal per l'undo. Rifiuta placebo/gui-only;
              HKLM/boot richiedono un terminale admin.

          wpep apply-all [--yes]
              Dry-run (o, con --yes, applica) TUTTI i tweak consigliati E
              applicabili su questo PC. Ognuno journaled e annullabile singolarmente.
              Si ferma al primo verify fallito.

          wpep changes
              Elenca le sessioni journaled (cosa è stato applicato, e se annullato).

          wpep undo <file|last>
              Annulla una sessione: ripristina i valori precedenti (o cancella ciò
              che non esisteva), verificando ogni ripristino.

          wpep selftest
              Verifica che il motore di apply funzioni su questo PC: write→verify→undo
              su una chiave di registro usa-e-getta (nessuna impostazione reale toccata).
              Exit 0 = PASS. Da lanciare prima di fidarsi degli apply su una macchina nuova.

        diag e bench richiedono terminale elevato (vincolo ETW di Windows).
        Misura (diag/bench/compare/analyze/advise) è SEMPRE sola lettura.
        apply/apply-all/undo sono l'UNICA via di scrittura: dry-run di default,
        scrivono solo con --yes, sempre con journal + undo (Execution Engine V2).
        """);
}
