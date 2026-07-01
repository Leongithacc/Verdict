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

try
{
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
        return RunAdvise(args.Skip(1).ToArray());
    case "applicable":
        return RunApplicable(args.Skip(1).ToArray());
    case "apply":
        return RunApply(args.Skip(1).ToArray());
    case "apply-all":
        return RunApplyAll(args.Skip(1).ToArray());
    case "profiles":
        return RunProfiles();
    case "apply-profile":
        return RunApplyProfile(args.Skip(1).ToArray());
    case "changes":
        return RunChanges();
    case "undo":
        return RunUndo(args.Skip(1).ToArray());
    case "panic":
        return RunPanic(args.Skip(1).ToArray());
    case "selftest":
        return RunSelfTest(args.Skip(1).ToArray());
    case "doctor":
        return RunDoctor();
    case "scan":
        return RunScan();
    case "score":
        return RunScore();
    case "dna":
        return RunDna();
    case "fresh":
        return RunFresh();
    case "network":
        return RunNetwork(args.Skip(1).ToArray());
    case "timeline":
        return RunTimeline();
    case "museum":
        return RunMuseum();
    case "games":
        return RunGames();
    case "optimize":
        return RunOptimize(args.Skip(1).ToArray());
    case "watch":
        return RunWatch();
    case "sentinel":
        return RunSentinel(args.Skip(1).ToArray());
    case "nvidia":
        return RunNvidia();
    case "tools" when args.Length >= 2 && args[1] == "install-presentmon":
        return await InstallPresentMon();
    case "version":
        Console.WriteLine(WPEP.Core.AppVersion.Label);
        return 0;
    case "update-check":
        return await RunUpdateCheck();
    case "copilot":
        return await RunCopilot(args.Skip(1).ToArray());
    case "evidence":
        return RunEvidence();
    case "community":
        return RunCommunity(args.Skip(1).ToArray());
    case "session":
        return await RunSession(args.Skip(1).ToArray());
    default:
        Console.Error.WriteLine($"Comando sconosciuto: {args[0]}");
        PrintUsage();
        return 2;
}
}
catch (System.Exception ex)
{
    // Global safety net: any command that throws (WMI hiccup, missing file, ...) reports cleanly
    // instead of dumping a stack trace. Everything here is read-only, so failing is always safe.
    System.Console.Error.WriteLine($"Errore durante '{args[0]}': {ex.Message}");
    return 1;
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
        var recommendations = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries, LiveDetector(entries));

        WPEP.Statistics.NoiseFloorAnalyzer.NoiseReport? noise = null;
        if (noiseDir is not null)
            noise = WPEP.Statistics.NoiseFloorAnalyzer.Analyze(BenchmarkRunStore.LoadDirectory(noiseDir));

        WPEP.Statistics.ComparisonEngine.ComparisonReport? comparison = null;
        if (baselineDir is not null && postDir is not null)
            comparison = WPEP.Statistics.ComparisonEngine.Compare(
                BenchmarkRunStore.LoadDirectory(baselineDir),
                BenchmarkRunStore.LoadDirectory(postDir));

        var html = WPEP.Reporting.ReportBuilder.BuildHtml(new WPEP.Reporting.ReportData(
            DateTimeOffset.UtcNow, snapshot, recommendations, noise, comparison,
            ReadAppliedChanges()));
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

static int RunAdvise(string[] args)
{
    bool json = args.Contains("--json");

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
    var all = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries, LiveDetector(entries));

    if (json)
    {
        // Stable, machine-readable shape for automation on top of Verdict.
        var items = all
            .Where(r => r.Entry.Game is null || snapshot.GameInstalled(r.Entry.Game) != false)
            .Select(r => new
            {
                id = r.Entry.Id,
                name = r.Entry.Name,
                category = r.Entry.Category,
                game = r.Entry.Game,
                classification = r.Classification.ToString(),
                evidence = r.Entry.EvidenceLevel.ToString(),
                applicable = WPEP.Execution.ApplyPolicy.CanApply(r.Entry),
                needsAdmin = WPEP.Execution.ApplyPolicy.NeedsAdmin(r.Entry),
                stateNote = r.StateNote,
            });
        Console.WriteLine(JsonSerializer.Serialize(items,
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

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

// Stato live per l'advisor: i nvidia-drs letti in UNA sola sessione NVAPI (batch), il resto via
// motore. true = già a posto, false = da attivare, null = non determinabile (gui-only/admin/errore).
static Func<WPEP.KnowledgeBase.TweakEntry, bool?> LiveDetector(
    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries) =>
    WPEP.Execution.LiveState.Detector(entries, WPEP.Execution.ApplyPolicy.CanApply, NewEngine().BuildPlan);

// Regole condivise con la GUI: unica fonte in WPEP.Execution.ApplyPolicy.
static bool EntryNeedsAdmin(WPEP.KnowledgeBase.TweakEntry e) => WPEP.Execution.ApplyPolicy.NeedsAdmin(e);
static bool CanApplyEntry(WPEP.KnowledgeBase.TweakEntry e) => WPEP.Execution.ApplyPolicy.CanApply(e);

// Vista a colpo d'occhio: TUTTO ciò che Verdict può applicare con un click, con lo
// stato LIVE di ognuno (sola lettura — non scrive niente). Risponde alla domanda di
// Léon: "quali sono i tweak applicabili con un click?".
static int RunApplicable(string[] args)
{
    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try { entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load(); }
    catch (Exception ex) { Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}"); return 1; }

    // I tweak per-gioco dipendono dal gioco in esecuzione: fuori da questa panoramica di sistema.
    var oneClick = entries.Where(e => CanApplyEntry(e) && e.Game is null).ToList();

    // Gate hardware: non elenchiamo tweak NVIDIA su GPU AMD, desktop su portatile, ecc.
    // Fail-open quando l'hardware non è rilevabile (l'utente vede comunque la voce).
    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    int hwExcluded = oneClick.Count(e =>
        !WPEP.Advisor.AdvisorEngine.MeetsHardwarePrerequisites(snapshot, e, out _));
    var applicable = oneClick
        .Where(e => WPEP.Advisor.AdvisorEngine.MeetsHardwarePrerequisites(snapshot, e, out _))
        .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
        .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    if (applicable.Count == 0) { Console.WriteLine("Nessun tweak applicabile automaticamente su questo hardware."); return 0; }

    bool elevated = Elevation.IsElevated();
    var engine = NewEngine();
    int done = 0, readyNow = 0, adminNeeded = 0, unreadable = 0;
    var lines = new List<(string Category, string Text)>();

    foreach (var e in applicable)
    {
        string icon;
        var extra = "";
        bool needsAdmin = EntryNeedsAdmin(e);
        try
        {
            var plan = engine.BuildPlan(e); // legge solo i valori correnti
            if (plan.IsAlreadyApplied) { icon = "✓"; done++; }
            else if (needsAdmin && !elevated) { icon = "○"; extra = "  · richiede amministratore"; adminNeeded++; }
            else { icon = "○"; readyNow++; }
        }
        catch
        {
            // Alcune letture (es. bcdedit) richiedono admin: se è quel caso, è più onesto
            // dirlo che spacciarlo per "non leggibile".
            if (needsAdmin && !elevated) { icon = "○"; extra = "  · richiede amministratore"; adminNeeded++; }
            else { icon = "?"; extra = "  · stato non leggibile"; unreadable++; }
        }

        var riskTag = e.Risk switch
        {
            WPEP.KnowledgeBase.RiskLevel.Medium => "  ⚠ rischio medio",
            WPEP.KnowledgeBase.RiskLevel.High => "  ⚠ RISCHIO ALTO",
            _ => "",
        };
        var impact = e.ExpectedImpact.Length > 96 ? e.ExpectedImpact[..93] + "…" : e.ExpectedImpact;
        lines.Add((e.Category, $"  {icon} {e.Name}  [{e.Id}]{riskTag}{extra}\n      {impact}"));
    }

    Console.WriteLine($"\nTweak applicabili con un click ({applicable.Count}) — sola lettura, niente è stato scritto:\n");
    foreach (var group in lines.GroupBy(l => l.Category))
    {
        Console.WriteLine($"━━ {group.Key.ToUpperInvariant()}");
        foreach (var l in group) Console.WriteLine(l.Text);
        Console.WriteLine();
    }

    Console.WriteLine("Legenda:  ✓ già a posto   ○ applicabile   ? stato non leggibile");
    Console.Write($"Totale {applicable.Count}:  {done} già applicati,  {readyNow} applicabili ora");
    if (adminNeeded > 0) Console.Write($",  {adminNeeded} richiedono admin");
    if (unreadable > 0) Console.Write($",  {unreadable} non leggibili");
    Console.WriteLine(".");
    if (hwExcluded > 0)
        Console.WriteLine($"({hwExcluded} altri one-click esclusi perché non adatti al tuo hardware.)");
    if (!elevated && adminNeeded > 0)
        Console.WriteLine("Rilancia da un terminale amministratore per includere i tweak che scrivono in HKLM/boot.");
    Console.WriteLine("\nApplica:  wpep apply <id> [--yes]   ·   tutti i consigliati:  wpep apply-all [--yes]");
    return 0;
}

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
    // Gate hardware: niente NVAPI su GPU AMD, niente tweak desktop su un portatile, ecc.
    // Messaggio pulito invece di un'eccezione grezza da BuildPlan. Fail-open se non rilevabile.
    var sysSnapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    if (!WPEP.Advisor.AdvisorEngine.MeetsHardwarePrerequisites(sysSnapshot, entry, out var hwNote))
    {
        Console.Error.WriteLine($"'{entry.Id}' non è adatto a questo hardware: {hwNote}");
        return 2;
    }
    // BuildPlan only READS the current value (no admin needed), so we can report
    // "already applied" even for HKLM/boot tweaks without elevation. The admin gate
    // applies only when a real write is actually required (below).
    var engine = NewEngine();
    WPEP.Execution.ExecutionPlan plan;
    try { plan = engine.BuildPlan(entry); }
    catch (Exception ex) { Console.Error.WriteLine($"Impossibile costruire il piano: {ex.Message}"); return 1; }

    bool needsAdmin = EntryNeedsAdmin(entry);
    var action = WPEP.Execution.ApplyPolicy.DecideAction(
        canApply: true, plan.IsAlreadyApplied, needsAdmin, Elevation.IsElevated(), confirmYes: yes);

    Console.WriteLine($"\n{entry.Name}  [{entry.Id}]");
    if (action == WPEP.Execution.ApplyAction.AlreadyApplied)
    {
        Console.WriteLine("Già al valore desiderato: nessuna modifica necessaria.");
        Console.WriteLine(plan.Describe());
        return 0;
    }

    Console.WriteLine(action == WPEP.Execution.ApplyAction.Execute
        ? "Applico ora — modifiche:"
        : "Dry run — esattamente cosa cambierà:");
    Console.WriteLine(plan.Describe());
    if (plan.RequiresReboot) Console.WriteLine("\n(richiede un riavvio per avere effetto)");
    bool risky = entry.Risk is WPEP.KnowledgeBase.RiskLevel.High or WPEP.KnowledgeBase.RiskLevel.Medium
                 || entry.EvidenceLevel == WPEP.KnowledgeBase.EvidenceLevel.Risky;
    if (risky && !string.IsNullOrWhiteSpace(entry.RiskNotes))
        Console.WriteLine($"\n⚠ RISCHIO: {entry.RiskNotes}");
    if (needsAdmin && !Elevation.IsElevated())
        Console.WriteLine($"\n'{entry.Id}' scrive in HKLM/boot: per applicarlo serve un terminale amministratore.");

    if (action == WPEP.Execution.ApplyAction.NeedsAdmin)
        return 3;
    if (action == WPEP.Execution.ApplyAction.DryRun)
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
    int adminSkipped = 0, alreadyOk = 0, failed = 0;

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
        try
        {
            var p = engine.BuildPlan(e);
            if (p.IsAlreadyApplied) { alreadyOk++; Console.WriteLine($"• {e.Name}\n    [già al valore desiderato — niente da fare]"); continue; }
            ready.Add(p);
            Console.WriteLine($"• {e.Name}\n{p.Describe()}");
        }
        catch (Exception ex) { failed++; Console.WriteLine($"• {e.Name}\n    [non applicabile: {ex.Message}]"); }
    }
    Console.WriteLine($"\nRiepilogo: {ready.Count} da applicare · {alreadyOk} già a posto · " +
        $"{conflicts.Count} in conflitto · {adminSkipped} richiedono admin" +
        (failed > 0 ? $" · {failed} non applicabili" : "") + ".");
    if (adminSkipped > 0)
        Console.WriteLine($"{adminSkipped} richiedono un terminale amministratore (rilancia da admin per includerli).");
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

// V3 §2 — profili di tweak.
static int RunProfiles()
{
    var profiles = WPEP.Execution.ProfileStore.All();
    Console.WriteLine($"Profili ({profiles.Count}):\n");
    foreach (var p in profiles)
    {
        Console.WriteLine($"  {p.Name}{(p.BuiltIn ? "  (predefinito)" : "")} — {p.Description}");
        Console.WriteLine($"    {p.TweakIds.Count} tweak: {string.Join(", ", p.TweakIds)}\n");
    }
    Console.WriteLine("Applica: wpep apply-profile <nome> [--yes]");
    return 0;
}

static int RunApplyProfile(string[] args)
{
    bool yes = args.Contains("--yes") || args.Contains("-y");
    var name = args.FirstOrDefault(a => !a.StartsWith('-'));
    if (name is null) { Console.Error.WriteLine("Uso: wpep apply-profile <nome> [--yes]"); return 2; }

    var profile = WPEP.Execution.ProfileStore.Get(name);
    if (profile is null) { Console.Error.WriteLine($"Profilo '{name}' non trovato. 'wpep profiles' per la lista."); return 2; }

    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> kb;
    try { kb = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load(); }
    catch (Exception ex) { Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}"); return 1; }

    var entries = profile.TweakIds
        .Select(id => kb.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        .Where(e => e is not null && CanApplyEntry(e!)).Select(e => e!).ToList();
    if (entries.Count == 0)
    {
        Console.WriteLine($"Profilo '{profile.Name}': nessun tweak applicabile automaticamente (gui-only o id non trovati).");
        return 0;
    }

    bool elevated = Elevation.IsElevated();
    var engine = NewEngine();
    var (kept, conflicts) = WPEP.Advisor.ConflictResolver.Resolve(entries);
    var ready = new List<WPEP.Execution.ExecutionPlan>();
    int adminSkipped = 0, alreadyOk = 0, failed = 0;

    Console.WriteLine($"Profilo '{profile.Name}' — dry run:\n");
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
        try
        {
            var p = engine.BuildPlan(e);
            if (p.IsAlreadyApplied) { alreadyOk++; Console.WriteLine($"• {e.Name}\n    [già al valore desiderato]"); continue; }
            ready.Add(p);
            Console.WriteLine($"• {e.Name}\n{p.Describe()}");
        }
        catch (Exception ex) { failed++; Console.WriteLine($"• {e.Name}\n    [non applicabile: {ex.Message}]"); }
    }
    Console.WriteLine($"\nRiepilogo: {ready.Count} da applicare · {alreadyOk} già a posto · " +
        $"{conflicts.Count} in conflitto · {adminSkipped} richiedono admin" +
        (failed > 0 ? $" · {failed} non applicabili" : "") + ".");
    if (adminSkipped > 0)
        Console.WriteLine($"{adminSkipped} richiedono un terminale amministratore (rilancia da admin per includerli).");
    if (ready.Count == 0) { Console.WriteLine("\nNiente da applicare adesso."); return 0; }
    if (!yes)
    {
        Console.WriteLine($"\nNiente scritto. Rilancia con --yes per applicare i {ready.Count} tweak del profilo " +
            "(journaled, undo singolo).");
        return 0;
    }
    var (applied, stopped) = engine.ExecuteAll(ready);
    Console.WriteLine(stopped is null
        ? $"\nProfilo '{profile.Name}' applicato: {applied} tweak. 'wpep changes' / 'wpep undo'."
        : $"\nApplicati {applied}, poi FERMATO a {stopped}. I gia applicati restano annullabili.");
    return stopped is null ? 0 : 1;
}

// V3 — Panic restore: annulla TUTTO il journaled in un colpo (drift-aware).
static int RunPanic(string[] args)
{
    bool yes = args.Contains("--yes") || args.Contains("-y");
    var sessions = WPEP.Execution.ExecutionEngine.ListSessions(
        WPEP.Execution.ExecutionEngine.DefaultJournalDirectory);
    if (sessions.Count == 0) { Console.WriteLine("Niente da ripristinare: Verdict non ha applicato nulla."); return 0; }

    if (!yes)
    {
        Console.WriteLine($"PANIC RESTORE annullerebbe TUTTE le {sessions.Count} sessioni applicate da Verdict\n" +
            "(ripristina i valori precedenti; salta ciò che hai cambiato a mano).\n" +
            "Rilancia con --yes per ripristinare tutto.");
        return 0;
    }

    var outcome = NewEngine().UndoAll();
    Console.WriteLine($"Panic restore: ripristinate {outcome.Restored} modifiche.");
    foreach (var s in outcome.Skipped)
        Console.WriteLine($"  [saltato] {s}");
    return 0;
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
        var outcome = engine.Undo(file);
        Console.WriteLine(outcome.Restored > 0
            ? $"Annullate {outcome.Restored} modifiche da {Path.GetFileName(file)}."
            : "Niente da ripristinare (gia annullata o gia allo stato precedente).");
        foreach (var s in outcome.Skipped)
            Console.WriteLine($"  [saltato] {s}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Undo fallito (serve admin per HKLM/boot?): {ex.Message}");
        return 1;
    }
}

// Riepilogo delle modifiche journaled per il report (tutte le sessioni, con stato).
static IReadOnlyList<string>? ReadAppliedChanges()
{
    var sessions = WPEP.Execution.ExecutionEngine.ListSessions(
        WPEP.Execution.ExecutionEngine.DefaultJournalDirectory);
    var lines = new List<string>();
    foreach (var f in sessions)
    {
        try
        {
            var s = JsonSerializer.Deserialize<WPEP.Execution.JournalSession>(File.ReadAllText(f));
            if (s is null) continue;
            foreach (var e in s.Entries)
            {
                string state = e.Undone ? "annullato" : e.Verified ? "applicato" : "fallito";
                lines.Add($"{e.TweakId} · {e.Path}: " +
                    $"{(e.ExistedBefore ? e.ValueBefore : "<non impostato>")} → {e.ValueAfter} [{state}]");
            }
        }
        catch { /* una sessione illeggibile non deve rompere il report */ }
    }
    return lines.Count > 0 ? lines : null;
}

// V3 §1 — inventario hardware completo (WMI, zero driver).
static int RunScan()
{
    Console.WriteLine("Verdict scan — inventario hardware (WMI, nessun driver kernel)\n");
    WPEP.SystemAnalyzer.HardwareInventory hw;
    try { hw = WPEP.SystemAnalyzer.HardwareScanner.Scan(); }
    catch (Exception ex) { Console.Error.WriteLine($"Scan fallito: {ex.Message}"); return 1; }

    Console.WriteLine($"Scheda madre : {hw.Motherboard}");
    Console.WriteLine($"BIOS         : {hw.Bios}{(hw.BiosDate.Length > 0 ? $"  ({hw.BiosDate})" : "")}");
    Console.WriteLine($"CPU          : {hw.Cpu}  {hw.Cores?.ToString() ?? "?"}C/{hw.Threads?.ToString() ?? "?"}T");
    Console.WriteLine($"RAM          : {hw.RamTotalGb?.ToString("F0") ?? "?"} GB totali");
    foreach (var m in hw.Memory)
        Console.WriteLine($"   - {m.Slot,-12} {m.CapacityGb:F0} GB @ {m.SpeedMtps?.ToString() ?? "?"} MT/s  {m.Vendor} {m.Part}".TrimEnd());
    foreach (var g in hw.Gpus)
        Console.WriteLine($"GPU          : {g}");
    foreach (var d in hw.Disks)
        Console.WriteLine($"Disco        : {d.Model}  {d.CapacityGb:F0} GB"
            + (d.Media.Length > 0 ? $"  · {d.Media}" : "").TrimEnd());

    if (hw.Findings.Count > 0)
    {
        Console.WriteLine("\nDiagnosi:");
        foreach (var fi in hw.Findings)
        {
            string mark = fi.Level switch { "Warn" => "[!]", "Ok" => "[ok]", _ => "[i]" };
            Console.WriteLine($"  {mark} {fi.Text}");
        }
    }

    Console.WriteLine("\n(Sensori live VRM/ventole/temp-CPU non inclusi: richiederebbero un driver kernel,\n vietato per anti-cheat. Temp/clock GPU: vedi 'wpep doctor'.)");
    return 0;
}

// ===================== Moduli "Lab" via CLI (V3) =====================
// Espongono la stessa logica pura della GUI: sola lettura, niente scritture.

static int RunScore()
{
    Console.WriteLine("Verdict Score — quanto è ottimizzato questo PC (onesto: i placebo non contano)\n");
    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    var entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
    var recs = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries).Where(r => r.Entry.Game is null).ToArray();
    int done = recs.Count(r => r.Classification == WPEP.Advisor.Classification.AlreadyActive);
    int pending = recs.Count(r => r.Classification == WPEP.Advisor.Classification.Recommended);
    bool? expo = SafeExpo();

    var result = WPEP.Execution.VerdictScore.Compute(
        new WPEP.Execution.ScoreInput(done, pending, 0, 0, expo));
    Console.WriteLine($"   ┌─────────┐");
    Console.WriteLine($"   │  {result.Score,3}    │   {result.Band}");
    Console.WriteLine($"   └─────────┘\n");
    foreach (var r in result.Breakdown)
        Console.WriteLine($"   {(r.Delta == 0 ? "  " : r.Delta.ToString()),4}  {r.Text}");
    Console.WriteLine($"\n   {result.HonestyNote}");
    return 0;
}

static int RunDna()
{
    var hw = WPEP.SystemAnalyzer.HardwareScanner.Scan();
    var dna = WPEP.SystemAnalyzer.RigDna.Compute(hw);
    Console.WriteLine("Rig DNA — la firma unica di questo PC\n");
    Console.WriteLine($"   {dna.Code}   [{dna.Tier}]");
    Console.WriteLine($"   {string.Join("  ·  ", dna.Traits)}");
    Console.WriteLine($"   (hue {dna.Hue}°)");
    return 0;
}

static int RunFresh()
{
    var items = WPEP.SystemAnalyzer.FreshInstallScanner.EnumerateStartup();
    var report = WPEP.SystemAnalyzer.FreshInstallScanner.Analyze(items);
    Console.WriteLine("Fresh-install score — drift degli avvii rispetto a un Windows pulito\n");
    Console.WriteLine($"   Score: {report.Score}/100  [{report.Band}]");
    Console.WriteLine($"   {report.Headline}\n");
    if (report.ThirdParty.Count > 0)
    {
        Console.WriteLine("   Terze parti all'avvio:");
        foreach (var i in report.ThirdParty)
            Console.WriteLine($"     - {i.Name}");
    }
    return 0;
}

static int RunNetwork(string[] args)
{
    string? game = args.Length > 0 ? args[0] : null;
    var anchors = WPEP.SystemAnalyzer.NetworkDuel.AnchorsFor(game);
    bool gameKnown = game is { Length: > 0 }
        && WPEP.SystemAnalyzer.NetworkDuel.GamePublisher.ContainsKey(game);

    if (game is { Length: > 0 } && gameKnown)
        Console.WriteLine($"Network Duel — qualità di rotta per '{game}' (baseline + ecosistema, ICMP best-effort)\n");
    else if (game is { Length: > 0 })
        Console.WriteLine($"Network Duel — '{game}' non ha un anchor dedicato: uso il set di default (ICMP best-effort)\n");
    else
        Console.WriteLine("Network Duel — qualità di rotta verso anchor pubblici (ICMP, best-effort)\n");

    foreach (var (target, host) in anchors)
    {
        var rtts = WPEP.SystemAnalyzer.NetworkDuel.PingHost(host, 10);
        var r = WPEP.SystemAnalyzer.NetworkDuel.Analyze(target, host, rtts);
        Console.WriteLine($"   {r.Grade,-18} {target,-26} avg {r.AvgMs,4:F0}ms · jit {r.JitterMs,3:F0} · loss {r.LossPercent,3:F0}%");
    }
    Console.WriteLine("\n(Molti server di gioco bloccano l'ICMP: questi sono anchor di rotta verso l'ecosistema, non il match server.)");
    return 0;
}

static int RunTimeline()
{
    Console.WriteLine("Time Machine — cos'è cambiato dall'ultima istantanea\n");
    var hw = WPEP.SystemAnalyzer.HardwareScanner.Scan();
    int startup = WPEP.SystemAnalyzer.FreshInstallScanner.EnumerateStartup().Count(i => !i.IsMicrosoft);
    var state = new WPEP.SystemAnalyzer.SystemState(DateTime.Now.ToString("o"),
        hw.ExpoEnabled, hw.RamTotalGb, hw.PrimaryGpu, hw.Bios, startup);
    var prev = WPEP.SystemAnalyzer.SystemTimeline.LoadAll().LastOrDefault();
    if (prev is null)
    {
        WPEP.SystemAnalyzer.SystemTimeline.Save(state);
        Console.WriteLine("   Prima istantanea salvata: questa è la baseline. Riesegui più tardi per il diff.");
        return 0;
    }
    var diff = WPEP.SystemAnalyzer.SystemTimeline.Diff(prev, state);
    if (diff.Count == 0)
        Console.WriteLine("   Nessun cambiamento rilevante. Sistema stabile.");
    else
    {
        foreach (var c in diff)
            Console.WriteLine($"   {c.Field,-28} {c.Before}  →  {c.After}");
        WPEP.SystemAnalyzer.SystemTimeline.Save(state);
    }
    return 0;
}

static int RunMuseum()
{
    var entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
    var museum = WPEP.KnowledgeBase.PlaceboMuseum.Build(entries);
    Console.WriteLine($"Placebo Museum — {museum.Count} miti sfatati con l'evidenza\n");
    foreach (var x in museum)
    {
        Console.WriteLine($"   ✗ {x.Name}  [{x.Category}]");
        Console.WriteLine($"     Mito  : {x.Myth}");
        Console.WriteLine($"     Verità: {x.Truth}\n");
    }
    return 0;
}

static int RunGames()
{
    var entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
    var games = WPEP.Advisor.OptimizeForGame.AvailableGames(entries);
    Console.WriteLine("Giochi con un piano dedicato (usa 'wpep optimize <gioco>'):\n");
    foreach (var g in games) Console.WriteLine($"   - {g}");
    return 0;
}

static int RunOptimize(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Uso: wpep optimize <gioco>   (vedi 'wpep games')");
        return 2;
    }
    var entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load();
    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    var plan = WPEP.Advisor.OptimizeForGame.Build(args[0], entries, snapshot);
    Console.WriteLine($"Ottimizza per {plan.Game}  (filtrato per il tuo hardware)\n");
    Console.WriteLine("Tweak di sistema (evidenza solida):");
    foreach (var t in plan.SystemTweaks) Console.WriteLine($"   - {t.Name}");
    Console.WriteLine("\nImpostazioni in-game / driver:");
    if (plan.InGameSettings.Count == 0)
        Console.WriteLine("   (nessuna voce dedicata in KB per questo gioco)");
    foreach (var s in plan.InGameSettings)
        Console.WriteLine($"   - {s.Name}: {s.ExpectedImpact}");
    if (WPEP.SystemAnalyzer.NetworkDuel.GamePublisher.ContainsKey(plan.Game))
        Console.WriteLine($"\nTesta la rotta verso l'ecosistema di {plan.Game}:  wpep network {plan.Game}");
    return 0;
}

static int RunSentinel(string[] args)
{
    string? baselineDir = null, nowDir = null;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--baseline" or "-b" when i + 1 < args.Length: baselineDir = args[++i]; break;
            case "--now" or "-n" when i + 1 < args.Length: nowDir = args[++i]; break;
            default: Console.Error.WriteLine($"Argomento sconosciuto: {args[i]}"); return 2;
        }
    }
    Console.WriteLine("Regression Sentinel — le prestazioni sono peggiorate dalla baseline?\n");
    if (baselineDir is null || nowDir is null)
    {
        Console.Error.WriteLine("Servono --baseline <dir> (benchmark di riferimento) e --now <dir> (ri-benchmark).");
        return 2;
    }
    try
    {
        var baseline = BenchmarkRunStore.LoadDirectory(baselineDir);
        var now = BenchmarkRunStore.LoadDirectory(nowDir);
        var report = WPEP.Statistics.ComparisonEngine.Compare(baseline, now);
        var verdict = WPEP.Statistics.RegressionSentinel.Evaluate(report);
        string mark = verdict.Status switch
        {
            WPEP.Statistics.SentinelStatus.Regressed => "[!]",
            WPEP.Statistics.SentinelStatus.Improved or WPEP.Statistics.SentinelStatus.Stable => "[ok]",
            _ => "[i]",
        };
        Console.WriteLine($"  {mark} {verdict.Headline}");
        // Persisti il verdetto: il tray lo legge e ti ricorda una regressione finché non rifai il check.
        WPEP.Execution.SentinelStatusStore.Save(new WPEP.Execution.SentinelSnapshot(
            verdict.Status.ToString(), verdict.Headline, DateTimeOffset.UtcNow.ToString("o")));
        return verdict.Status == WPEP.Statistics.SentinelStatus.Regressed ? 1 : 0;
    }
    catch (Exception ex) { Console.Error.WriteLine($"Sentinel fallito: {ex.Message}"); return 1; }
}

static int RunNvidia()
{
    Console.WriteLine("NVIDIA — prova interop NVAPI (user-mode, anti-cheat safe, sola lettura)\n");
    var probe = WPEP.SystemAnalyzer.NvApi.Probe();
    Console.WriteLine($"  {(probe.Available ? "[ok]" : "[!]")} {probe.Message}");
    if (!probe.Available) return 1;

    Console.WriteLine("\n  DRS read (sola lettura):");
    var r = WPEP.SystemAnalyzer.NvApi.ReadDwordSetting(WPEP.SystemAnalyzer.NvApi.Setting_PreferredPState);
    Console.WriteLine($"  {(r.Ok ? "[ok]" : r.MarshallingOk ? "[i]" : "[!]")} Power Management Mode: {r.Message}");
    if (r.Ok)
        Console.WriteLine("\n  → DRS READ+WRITE VALIDATI dal vivo: leggo e scrivo i valori del pannello NVIDIA\n    (write+verify+undo journaled). Struct NVDRS_SETTING corretta (sizeof|version accettata).");
    else if (r.MarshallingOk)
        Console.WriteLine("\n  → Sessione DRS e struct OK: questa setting non è impostata (sei sul default del driver).");
    else
        Console.WriteLine("\n  → [!] struct RIFIUTATA (-9 INCOMPATIBLE_STRUCT_VERSION): layout/version NVDRS_SETTING da rivedere.");
    return 0;
}

static int RunWatch()
{
    Console.WriteLine("Watchdog — controlla derive: EXPO, tweak annullati, bloat all'avvio (sola lettura)\n");
    var hw = WPEP.SystemAnalyzer.HardwareScanner.Scan();
    int startupNow = WPEP.SystemAnalyzer.FreshInstallScanner.EnumerateStartup().Count(i => !i.IsMicrosoft);
    var baseline = WPEP.SystemAnalyzer.SystemTimeline.LoadAll().LastOrDefault();

    var engine = new WPEP.Execution.ExecutionEngine(
        new WPEP.Execution.RealRegistryAccess(), WPEP.Execution.ExecutionEngine.DefaultJournalDirectory);
    var reverted = engine.DetectDrift();

    var inputs = new WPEP.Execution.WatchInputs(
        ExpoBaseline: baseline?.ExpoEnabled, ExpoNow: hw.ExpoEnabled,
        StartupBaseline: baseline?.ThirdPartyStartup ?? startupNow, StartupNow: startupNow,
        Reverted: reverted);
    var alerts = WPEP.Execution.WatchdogCheck.Evaluate(inputs);

    foreach (var a in alerts)
    {
        string mark = a.Level switch
        {
            WPEP.Execution.WatchLevel.Warn => "[!]",
            WPEP.Execution.WatchLevel.Info => "[i]",
            _ => "[ok]",
        };
        Console.WriteLine($"  {mark} {a.Title}");
        Console.WriteLine($"      {a.Detail}");
    }
    if (baseline is null)
        Console.WriteLine("\n(Nessuna baseline: esegui 'wpep timeline' una volta per dare al watchdog un riferimento.)");
    return 0;
}

// EXPO senza propagare eccezioni WMI: la Score resta utile anche se il banco non si legge.
static bool? SafeExpo()
{
    try { return WPEP.SystemAnalyzer.HardwareScanner.Scan().ExpoEnabled; }
    catch { return null; }
}

// Riepilogo di prontezza: stato sistema + verdetto + diagnostica del motore.
// Tutto sola-lettura/sicuro (self-test su chiave usa-e-getta; powercfg in lettura).
static int RunDoctor()
{
    Console.WriteLine("Verdict doctor — stato e prontezza su QUESTO PC\n");
    bool elevated = Elevation.IsElevated();

    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try { entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load(); }
    catch (Exception ex) { Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}"); return 1; }

    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    static string Known(string v) => v.Length == 0 ? "sconosciuto" : v;
    Console.WriteLine($"Sistema    : {Known(snapshot.CpuName)} · {Known(snapshot.GpuName)}");
    Console.WriteLine($"Admin      : {(elevated ? "si (Diagnostics/ETW e tweak HKLM/boot disponibili)" : "no (misura base + tweak HKCU ok; HKLM/boot richiedono admin)")}");

    var games = new[]
        {
            ("fortnite", "Fortnite"), ("valorant", "Valorant"), ("cs2", "CS2"),
            ("apex", "Apex Legends"), ("overwatch2", "Overwatch 2"),
            ("thefinals", "THE FINALS"), ("r6siege", "Rainbow Six Siege"),
            ("warzone", "Call of Duty Warzone"),
        }
        .Where(g => snapshot.GameInstalled(g.Item1) == true)
        .Select(g => g.Item2).ToList();
    Console.WriteLine($"Giochi     : {(games.Count > 0 ? string.Join(", ", games) : "nessuno dei noti rilevato")}");

    // Prontezza Vanguard / Win11: Secure Boot + TPM 2.0 detection da snapshot.
    static string Readiness(bool? v, string okLabel, string koLabel) =>
        v switch { true => okLabel, false => koLabel, _ => "non rilevato" };
    Console.WriteLine($"Secure Boot: {Readiness(snapshot.SecureBootEnabled, "attivo", "DA ABILITARE (richiesto da Vanguard / Win11)")}");
    Console.WriteLine($"TPM 2.0    : {Readiness(snapshot.Tpm2Enabled,        "attivo", "DA ABILITARE (richiesto da Vanguard / Win11)")}");
    if (snapshot.NoiseScore is int noise)
        Console.WriteLine($"Rumore     : {noise}/100 ({snapshot.NoiseBand}) · {(snapshot.NoiseFactors.Count > 0 ? string.Join(", ", snapshot.NoiseFactors) : "nessun fattore")}");
    else
        Console.WriteLine($"Rumore     : non rilevato (probe insufficienti)");

    var recs = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries)
        .Where(r => r.Entry.Game is null).ToArray();
    int oneClick = recs.Count(r => r.Classification == WPEP.Advisor.Classification.Recommended && CanApplyEntry(r.Entry));
    int optimal = recs.Count(r => r.Classification == WPEP.Advisor.Classification.AlreadyActive);
    int placebo = recs.Count(r => r.Classification == WPEP.Advisor.Classification.Placebo);
    Console.WriteLine($"Verdetto   : {oneClick} consigliati applicabili one-click · {optimal} gia ottimali · {placebo} placebo evitati");

    // Self-test del motore di scrittura (registry) — chiave usa-e-getta, cleanup totale.
    var st = WPEP.Execution.EngineSelfTest.RunReal();
    Console.WriteLine($"Engine     : self-test registry {(st.Passed ? "PASS" : "FAIL")} (write/verify/undo su chiave usa-e-getta)");

    // powercfg in LETTURA (non distruttivo): valida il parsing dello schema attivo.
    try
    {
        var scheme = new WPEP.Execution.RealPowerCfg().GetActiveScheme();
        Console.WriteLine($"powercfg   : lettura schema attivo OK ({scheme})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"powercfg   : lettura schema FALLITA ({ex.Message})");
    }

    Console.WriteLine($"\nPronto: {(st.Passed ? "il motore di apply funziona su questo PC." : "self-test fallito — vedi sopra.")}");
    return st.Passed ? 0 : 1;
}

// Verifica il motore di apply su QUESTO PC. Logica condivisa con la GUI in
// WPEP.Execution.EngineSelfTest (chiave usa-e-getta, journal temp, cleanup totale).
static int RunSelfTest(string[] args)
{
    if (args.Contains("--writes"))
        return RunSelfTestWrites();

    Console.WriteLine("Verdict self-test — verifica il motore di apply (write+verify+undo) su QUESTO PC.");
    Console.WriteLine($"Chiave usa-e-getta: {WPEP.Execution.EngineSelfTest.ScratchPath}  (nessuna impostazione reale viene toccata)\n");

    var result = WPEP.Execution.EngineSelfTest.RunReal();
    int n = 0;
    foreach (var step in result.Steps)
        Console.WriteLine($"{++n}) {step.Name,-38} {(step.Ok ? "OK" : "FALLITO")}  ({step.Detail})");

    Console.WriteLine($"\nRisultato: {(result.Passed ? "PASS — il motore di apply funziona su questo PC (registry write+verify+undo)." : "FAIL — vedi sopra.")}");
    Console.WriteLine("Suggerimento: 'wpep selftest --writes' prova TUTTI i metodi (powercfg/bcdedit/dxuser/nvidia-drs).");
    return result.Passed ? 0 : 1;
}

// Trust: esercita OGNI write-path su target sicuri (throwaway/no-op). Niente cambia davvero.
static int RunSelfTestWrites()
{
    Console.WriteLine("Verdict self-test SCRITTURE — esercita OGNI metodo di apply su QUESTO PC.");
    Console.WriteLine("Solo target sicuri: chiave/schema usa-e-getta o riscrittura di un valore su se stesso.");
    Console.WriteLine("Niente di reale viene modificato.\n");

    var probes = WPEP.Execution.WriteSelfTest.RunAll();
    foreach (var p in probes)
    {
        string mark = p.Status switch
        {
            WPEP.Execution.ProbeStatus.Pass => "PASS",
            WPEP.Execution.ProbeStatus.Fail => "FAIL",
            _ => "SALTATO",
        };
        Console.WriteLine($"  [{mark,-7}] {p.Method,-11} {p.Detail}");
    }

    bool ok = WPEP.Execution.WriteSelfTest.AllOk(probes);
    int passed = probes.Count(p => p.Status == WPEP.Execution.ProbeStatus.Pass);
    int skipped = probes.Count(p => p.Status == WPEP.Execution.ProbeStatus.Skip);
    Console.WriteLine($"\nRisultato: {(ok ? "PASS" : "FAIL")} — {passed} provati, {skipped} saltati, " +
                      $"{probes.Count(p => p.Status == WPEP.Execution.ProbeStatus.Fail)} falliti.");
    if (skipped > 0)
        Console.WriteLine("(I 'SALTATO' non sono errori: di solito mancano admin (bcdedit) o GPU NVIDIA. " +
                          "Per bcdedit, rilancia da admin.)");
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

static int RunEvidence()
{
    var all = WPEP.Execution.EvidenceLedger.Load();
    if (all.Count == 0)
    {
        Console.WriteLine("Nessuna prova registrata ancora. Applica/misura qualche tweak (anche col Ghost Tweak):");
        Console.WriteLine("Verdict salva l'esito qui in locale e in forma anonima (firma rig RigDna, zero dati personali).");
        return 0;
    }
    Console.WriteLine($"Le tue prove ({all.Count} record · {WPEP.Execution.EvidenceLedger.FilePath}):\n");
    foreach (var g in all.GroupBy(r => r.TweakId).OrderBy(x => x.Key))
    {
        var s = WPEP.Execution.EvidenceLedger.Aggregate(g);
        int applied = g.Count(r => r.Outcome == "applied");
        Console.WriteLine(s.SampleSize > 0
            ? $"  {g.Key}: {s.HelpedPercent}% aiutato · {s.NoEffectPercent}% nessun effetto · {s.HurtPercent}% peggiorato ({s.SampleSize} misure)"
            : $"  {g.Key}: applicato {applied}× (nessuna misura ancora)");
    }
    return 0;
}

static int RunCommunity(string[] args)
{
    // Subcomandi: --enable / --disable / --status (default). Toccano il file settings.json
    // della GUI direttamente, evitando dipendenza da WPEP.App lato CLI.
    string action = "status";
    foreach (var a in args)
    {
        if (a == "--enable") action = "enable";
        else if (a == "--disable") action = "disable";
        else if (a == "--status") action = "status";
        else
        {
            Console.Error.WriteLine($"Flag sconosciuto: {a}. Usa --enable, --disable, --status.");
            return 2;
        }
    }

    var settingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Verdict", "data", "settings.json");

    System.Text.Json.Nodes.JsonObject settings;
    try
    {
        var json = System.IO.File.Exists(settingsPath)
            ? System.IO.File.ReadAllText(settingsPath)
            : "{}";
        settings = (System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject)
                   ?? new System.Text.Json.Nodes.JsonObject();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Impossibile leggere settings.json ({settingsPath}): {ex.Message}");
        return 1;
    }

    if (action is "enable" or "disable")
    {
        settings["CommunityShareEnabled"] = action == "enable";
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath)!);
            System.IO.File.WriteAllText(settingsPath, settings.ToJsonString(
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Impossibile salvare settings.json: {ex.Message}");
            return 1;
        }
        Console.WriteLine(action == "enable"
            ? "✓ Opt-in community attivato. La GUI userà RemoteBackend al prossimo avvio."
            : "✓ Opt-in community disattivato. La GUI tornerà a LocalOnly al prossimo avvio.");
        Console.WriteLine();
    }

    // Status sempre stampato in coda.
    bool enabled = settings["CommunityShareEnabled"] is System.Text.Json.Nodes.JsonValue v
                   && v.TryGetValue<bool>(out var b) && b;
    bool configured = WPEP.Execution.CommunityConfig.IsConfigured;
    var endpoint = WPEP.Execution.CommunityConfig.Endpoint;

    WPEP.Execution.ICommunityBackend backend = (enabled && configured)
        ? new WPEP.Execution.RemoteBackend(endpoint)
        : new WPEP.Execution.LocalOnlyBackend();
    var svc = new WPEP.Execution.CommunityService(backend);

    Console.WriteLine($"Backend attivo: {svc.BackendName}");
    Console.WriteLine($"Opt-in utente: {(enabled ? "ON" : "OFF")}");
    Console.WriteLine($"Endpoint configurato: {(configured ? endpoint : "(nessuno)")}");
    if (!svc.CommunityActive)
    {
        Console.WriteLine();
        if (!configured)
            Console.WriteLine("Nessun backend configurato (CommunityConfig.Endpoint vuoto).");
        else if (!enabled)
            Console.WriteLine("Per attivare: wpep community --enable");
    }
    return 0;
}

static async Task<int> RunCopilot(string[] args)
{
    // Flag: --brain ollama|claude (default ollama), --model <nome> (Ollama o Claude in base a --brain),
    // --api-key <key> per Claude (oppure env ANTHROPIC_API_KEY). La domanda = argomenti non-flag uniti.
    string? model = null;
    string brainKind = "ollama";
    string? apiKey = null;
    var qparts = new List<string>();
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--model" && i + 1 < args.Length) { model = args[++i]; continue; }
        if (args[i] == "--brain" && i + 1 < args.Length) { brainKind = args[++i].ToLowerInvariant(); continue; }
        if (args[i] == "--api-key" && i + 1 < args.Length) { apiKey = args[++i]; continue; }
        qparts.Add(args[i]);
    }
    var question = string.Join(' ', qparts).Trim();
    if (question.Length == 0)
    {
        Console.Error.WriteLine(
            "Uso: wpep copilot \"la tua domanda\" [--brain ollama|claude] [--model <nome>] [--api-key <key>]");
        return 2;
    }
    if (brainKind is not ("ollama" or "claude" or "gemini" or "openai"))
    {
        Console.Error.WriteLine($"--brain sconosciuto: '{brainKind}'. Usa ollama, claude, gemini o openai.");
        return 2;
    }

    IReadOnlyList<WPEP.KnowledgeBase.TweakEntry> entries;
    try { entries = WPEP.KnowledgeBase.KnowledgeBaseLoader.Load(); }
    catch (Exception ex) { Console.Error.WriteLine($"Caricamento KB fallito: {ex.Message}"); return 1; }

    var snapshot = WPEP.SystemAnalyzer.SnapshotBuilder.Build(DateTimeOffset.UtcNow);
    var catalog = WPEP.Advisor.AdvisorEngine.Advise(snapshot, entries, LiveDetector(entries));

    WPEP.Advisor.CoPilot.ICoPilotBrain brain;
    switch (brainKind)
    {
        case "claude":
        {
            var key = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
            if (key.Length == 0)
            {
                Console.Error.WriteLine("Per --brain claude serve la API key Anthropic: passa --api-key <key>");
                Console.Error.WriteLine("oppure imposta la variabile d'ambiente ANTHROPIC_API_KEY.");
                return 2;
            }
            brain = new WPEP.Advisor.CoPilot.ClaudeBrain(key, model);
            break;
        }
        case "gemini":
        {
            var key = apiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            if (key.Length == 0)
            {
                Console.Error.WriteLine("Per --brain gemini serve la API key Google AI: passa --api-key <key>");
                Console.Error.WriteLine("oppure imposta la variabile d'ambiente GEMINI_API_KEY.");
                return 2;
            }
            brain = new WPEP.Advisor.CoPilot.GeminiBrain(key, model);
            break;
        }
        case "openai":
        {
            var key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
            if (key.Length == 0)
            {
                Console.Error.WriteLine("Per --brain openai serve la API key OpenAI: passa --api-key <key>");
                Console.Error.WriteLine("oppure imposta la variabile d'ambiente OPENAI_API_KEY.");
                return 2;
            }
            brain = new WPEP.Advisor.CoPilot.OpenAiBrain(key, model);
            break;
        }
        default:
            brain = new WPEP.Advisor.CoPilot.OllamaBrain(model);
            break;
    }
    Console.WriteLine($"Co-pilota: {brain.Name}");
    if (!await brain.IsAvailableAsync())
    {
        if (brainKind == "ollama")
        {
            Console.WriteLine("Ollama non raggiungibile su localhost:11434. Avvialo (ollama serve) e assicurati");
            Console.WriteLine($"che il modello sia installato:  ollama pull {model ?? WPEP.Advisor.CoPilot.CoPilotConfig.DefaultModel}");
        }
        else
        {
            Console.WriteLine($"{brain.Name} non raggiungibile. Verifica la API key e la connessione internet.");
        }
        return 0;
    }

    Console.WriteLine("Sto pensando…\n");
    var reply = await new WPEP.Advisor.CoPilot.CoPilotService(brain).AskAsync(question, catalog);
    if (reply.Error is not null) { Console.WriteLine(reply.Error); return 0; }

    Console.WriteLine(reply.Answer);
    if (reply.Suggestions.Count > 0)
    {
        Console.WriteLine("\nTweak citati (verificati nel catalogo Verdict):");
        foreach (var s in reply.Suggestions)
            Console.WriteLine($"  · [{s.TweakId}] {s.Name} — {ClassificationLabel(s.Classification)}");
        Console.WriteLine("\nApplicali con:  wpep apply <id>   (dry-run; aggiungi --yes per scrivere)");
    }
    return 0;
}

static async Task<int> RunSession(string[] args)
{
    // wpep session [--wait <secs>]
    // Applica priority BelowNormal ai processi noti come bloater (Discord, OneDrive, cloud sync,
    // updater). Idea da docs/VS_HONE.md sez. 3.3 (Gaming Session Mode). Reversibile a Ctrl+C
    // o al timeout --wait. Non uccide, non stoppa servizi, non tocca il gioco.
    int? waitSecs = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--wait" && i + 1 < args.Length && int.TryParse(args[++i], out var s))
            waitSecs = s;
        else if (args[i] == "--help")
        {
            Console.WriteLine("Uso: wpep session [--wait <secondi>]");
            Console.WriteLine("Senza --wait: attende Ctrl+C per ripristinare.");
            return 0;
        }
    }

    var session = new WPEP.Execution.GamingSession();
    int down = session.Start();
    Console.WriteLine($"Gaming Session avviata: {down} processi abbassati a BelowNormal.");
    foreach (var t in session.TouchedProcesses)
        Console.WriteLine($"  · {t.ProcessName} (pid {t.Pid}): {t.OriginalPriority} → BelowNormal");
    if (down == 0)
        Console.WriteLine("(nessun bloater in esecuzione — sistema già pulito per il gaming)");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    if (waitSecs is int seconds)
    {
        Console.WriteLine($"\nRipristino automatico tra {seconds}s (Ctrl+C anticipa).");
        try { await Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token); } catch (TaskCanceledException) { }
    }
    else
    {
        Console.WriteLine("\nSessione attiva. Ctrl+C per ripristinare e uscire.");
        try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (TaskCanceledException) { }
    }

    int originalCount = session.TouchedProcesses.Count;
    int restored = session.Stop();
    Console.WriteLine($"\nRipristinati {restored}/{originalCount} processi.");
    return 0;
}

static async Task<int> RunUpdateCheck()
{
    var info = await WPEP.Core.Update.UpdateChecker.CheckAsync(WPEP.Core.AppVersion.Current);
    if (!info.Configured)
    {
        Console.WriteLine($"Aggiornamenti: non configurati (nessun host scelto). Versione attuale: v{info.CurrentVersion}.");
        return 0;
    }
    if (info.Error is not null)
    {
        Console.WriteLine($"Aggiornamenti: {info.Error} (versione attuale: v{info.CurrentVersion}).");
        return 0;
    }
    if (info.UpdateAvailable)
    {
        Console.WriteLine($"Aggiornamento disponibile: v{info.LatestVersion} (hai v{info.CurrentVersion}).");
        if (info.DownloadUrl is not null)
            Console.WriteLine($"Scarica: {info.DownloadUrl}");
    }
    else
    {
        Console.WriteLine($"Sei aggiornato (v{info.CurrentVersion}).");
    }
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        WPEP — Windows Performance Engineering Platform (motore di Verdict)

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

          wpep advise [--json]
              Incrocia lo snapshot con la knowledge base: cosa è già a posto,
              cosa è consigliato, cosa è placebo, cosa è rischioso — su QUESTO pc.
              Con --json: output strutturato (id, classificazione, evidenza, applicabile)
              per automazione/integrazione.

          wpep kb [show <id>]
              Knowledge base dei tweak: grading di evidenza con fonti primarie,
              passi manuali e rollback. Include i placebo, spiegando perché.

          wpep report [--out file.html] [--runs dir] [--baseline dir --post dir]
              Report HTML (tema scuro) con snapshot + advisor; opzionalmente
              noise floor e confronto statistico. Condivisibile.

          wpep tools install-presentmon
              Scarica PresentMon (Intel, MIT) nella cartella tools di WPEP.

          wpep applicable
              Panoramica a colpo d'occhio di TUTTO ciò che è applicabile con un
              click, con lo stato live di ognuno (✓ già a posto / ○ applicabile /
              richiede admin), raggruppato per categoria. Sola lettura.

          wpep apply <id> [--yes]
              Applica un tweak della KB. SENZA --yes mostra solo il dry-run
              (before→after) e non scrive nulla. Con --yes: scrive, verifica
              rileggendo, e salva il journal per l'undo. Rifiuta placebo/gui-only;
              HKLM/boot richiedono un terminale admin.

          wpep apply-all [--yes]
              Dry-run (o, con --yes, applica) TUTTI i tweak consigliati E
              applicabili su questo PC. Ognuno journaled e annullabile singolarmente.
              Si ferma al primo verify fallito.

          wpep profiles
              Elenca i profili di tweak (predefiniti Competitive/Streaming/Daily + i tuoi).

          wpep apply-profile <nome> [--yes]
              Applica in blocco i tweak di un profilo (dry-run senza --yes). Conflict guard,
              salto dei no-op e gating admin come apply-all; ognuno resta annullabile.

          wpep changes
              Elenca le sessioni journaled (cosa è stato applicato, e se annullato).

          wpep undo <file|last>
              Annulla una sessione: ripristina i valori precedenti (o cancella ciò
              che non esisteva), verificando ogni ripristino.

          wpep panic [--yes]
              PANIC RESTORE: annulla TUTTO ciò che Verdict ha applicato, in un colpo
              (drift-aware). Senza --yes mostra solo quante sessioni annullerebbe.

          wpep selftest
              Verifica che il motore di apply funzioni su questo PC: write→verify→undo
              su una chiave di registro usa-e-getta (nessuna impostazione reale toccata).
              Exit 0 = PASS. Da lanciare prima di fidarsi degli apply su una macchina nuova.

          wpep doctor
              Riepilogo di prontezza: sistema, admin, giochi rilevati, verdetto
              (quanti applicabili/già ottimali/placebo), self-test del motore e lettura
              dello schema energetico. Tutto sola-lettura/sicuro.

        — Moduli "Lab" (V3, sola lettura) —
          wpep score        Verdict Score 0–100 onesto (i placebo non contano).
          wpep dna          Rig DNA: firma unica + tier del tuo PC.
          wpep fresh        Fresh-install score: avvii di terze parti vs Windows pulito.
          wpep network      Network Duel: ping/jitter/loss verso anchor pubblici, con voto.
          wpep timeline     Time Machine: cos'è cambiato dall'ultima istantanea.
          wpep watch        Watchdog: deriva EXPO, tweak annullati, bloat all'avvio.
          wpep sentinel --baseline <dir> --now <dir>
                            Regression Sentinel: rileva se le prestazioni sono peggiorate.
          wpep nvidia       Prova l'interop NVAPI (fondazione per automatizzare il pannello NVIDIA).
          wpep museum       Placebo Museum: i miti sfatati con l'evidenza.
          wpep games        Giochi con un piano dedicato.
          wpep optimize <gioco>   Piano su misura: tweak di sistema + impostazioni in-game.

        — Community evidence (V7, privacy-first) —
          wpep evidence       Le TUE prove: per ogni tweak, com'è andata su questo PC
                              (applicato / aiutato / nessun effetto / peggiorato). Anonimo, in locale.
          wpep community [--enable|--disable|--status]
                              Stato community + controllo opt-in da CLI. Di default LOCALE:
                              nulla lascia il PC. --enable attiva l'invio anonimo al backend
                              configurato (la GUI ricarica al riavvio). --disable lo spegne.

        — Gaming Session Mode (docs/VS_HONE.md sez. 3.3) —
          wpep session [--wait <secondi>]
              Abbassa temporaneamente a BelowNormal la priority dei processi
              noti come "rumore gaming" (Discord, OneDrive, Dropbox, Google
              Drive, Spotify, updater Edge/Chrome/Steam/Epic). Ripristina al
              termine (Ctrl+C o timeout --wait). Non uccide, non stoppa
              servizi, non tocca il gioco — anti-cheat safe by design.

        — AI co-pilot (V6, sola lettura) —
          wpep copilot "<domanda>" [--brain ollama|claude|gemini|openai] [--model <nome>] [--api-key <key>]
              Chiedi in linguaggio naturale ("rendi Valorant più fluido"): il co-pilota
              interpreta e propone SOLO tweak del catalogo verificato di Verdict (gli id
              inventati vengono scartati), spiegando con onestà. Non applica nulla.
              Cervello di default: Ollama locale (gratis+privato) — serve `ollama serve`.
              --brain claude → Anthropic (ANTHROPIC_API_KEY o --api-key).
              --brain gemini → Google AI    (GEMINI_API_KEY o --api-key).
              --brain openai → OpenAI/GPT   (OPENAI_API_KEY o --api-key).

        — Versione & aggiornamenti —
          wpep version        Mostra la versione di Verdict.
          wpep update-check   Controlla se c'è una versione più recente (sola lettura,
                              consent-first: riporta soltanto, non scarica né installa).
                              Finché non è configurato un host, dice "non configurato".

        diag e bench richiedono terminale elevato (vincolo ETW di Windows).
        Misura (diag/bench/compare/analyze/advise) è SEMPRE sola lettura.
        apply/apply-all/undo sono l'UNICA via di scrittura: dry-run di default,
        scrivono solo con --yes, sempre con journal + undo (Execution Engine V2).
        """);
}
