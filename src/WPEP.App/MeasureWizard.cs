using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WPEP.Benchmark;
using WPEP.Core.Benchmark;
using WPEP.Core.Platform;
using WPEP.Execution;
using WPEP.Statistics;
using WPEP.SystemAnalyzer;

namespace WPEP.App;

public enum WizardStep { Pick, Baseline, ApplyChange, Post, Verdict }

/// <summary>One before/after metric for the Latency Lab chart: values + precomputed bar widths.</summary>
public sealed record LatencyRow(
    string Metric, double BaselineMs, double PostMs, double DeltaPercent,
    string DeltaLabel, string DeltaColor, double BaselineBar, double PostBar);

/// <summary>A measurement protocol: exact instructions that make runs
/// repeatable. The map codes are community benchmark islands, picked because
/// they remove human variance (automated camera) or minimize it (AFK).</summary>
public sealed record ScenarioPreset(string Name, string Instructions)
{
    public static readonly IReadOnlyList<ScenarioPreset> All =
    [
        new("Fortnite — LA TUA isola, AFK (nessuno sblocco)",
            "Creativa → CREA → apri la TUA isola (la tua non è mai bloccata dai filtri " +
            "contenuti). Cammina fino a un punto fisso, inquadra la stessa vista, poi " +
            "resta completamente immobile — mani lontane da mouse e tastiera per tutta la cattura. " +
            "Stesso punto, stessa vista, ogni run."),
        new("Fortnite — mappa benchmark, automatica",
            "Codice mappa Creativa 4135-2210-3629 (\"BENCHMARK\" di DweEroz). Se i filtri " +
            "contenuti la bloccano, prova: 5492-6089-6665 (test fps, camera fissa) · " +
            "5608-7013-5653 · 0240-9716-3198 — oppure usa il preset TUA-isola AFK, che " +
            "non richiede sblocco. Sessione privata, premi il pulsante di avvio della mappa, poi NON " +
            "toccare niente: la sequenza della camera è automatica. Un giro di warm-up prima " +
            "della run 1, riavvia la sequenza a ogni run."),
        new("CS2 — mappa benchmark del workshop",
            "Steam Workshop → cerca \"FPS Benchmark\" (uLLeticaL) → Iscriviti. In CS2: " +
            "Gioca → Mappe Workshop → FPS Benchmark → premi il pulsante di avvio nella mappa: " +
            "il fly-through è completamente automatico. Un giro di warm-up, poi una cattura per giro."),
        new("HITMAN World of Assassination — benchmark integrato",
            "Launcher → Opzioni/Benchmark (scena Dartmoor o Dubai): completamente automatico ed " +
            "estremamente ripetibile. Un giro di warm-up, poi avvia una cattura per ogni run del benchmark."),
        new("Fortnite — percorso fisso a piedi",
            "Isola Creativa PRIVATA. Cammina lo stesso percorso allo stesso ritmo per tutta " +
            "la cattura, stessa direzione ogni run. Più simile al gioco vero dell'AFK ma più rumoroso: verifica " +
            "che l'MDE della baseline resti sotto la soglia prima di giudicare un tweak."),
        new("Cyberpunk 2077 — benchmark integrato",
            "Impostazioni → Grafica → Benchmark, su un'installazione PULITA (niente mod/CET — alterano " +
            "i frametime). Lancia il benchmark una volta come warm-up, poi avvia una cattura per ogni " +
            "run del benchmark."),
        new("Personalizzato / partita live",
            "Misura quello che vuoi — i numeri escono sempre. Le partite live però di solito " +
            "superano il noise gate: aspettati dati, non un verdetto."),
    ];
}

public sealed record ProcessChoice(string ProcessName, string WindowTitle)
{
    public string Display => $"{ProcessName} — {WindowTitle}";
}

/// <summary>
/// The guided Measure wizard (HANDOFF_R7 §4): baseline → ONE change → post →
/// three-state verdict. The user cannot get the order wrong, the noise gate
/// is checked as soon as the baseline exists, and the environment is
/// fingerprinted per run (F10).
/// </summary>
public sealed class MeasureWizardViewModel(MainViewModel main, AppSettings settings) : ViewModelBase
{
    private WizardStep _step = WizardStep.Pick;
    private ProcessChoice? _target;
    private int _seconds = 60;
    private string _status = "";
    private string _verdictText = "";
    private string _sessionDir = "";
    private bool _busy;
    private IReadOnlyList<BenchmarkRun> _baseline = [];
    private IReadOnlyList<BenchmarkRun> _post = [];

    public WizardStep Step { get => _step; private set { Set(ref _step, value); RaiseSteps(); } }
    public bool IsStepPick => Step == WizardStep.Pick;
    public bool IsStepBaseline => Step == WizardStep.Baseline;
    public bool IsStepApplyChange => Step == WizardStep.ApplyChange;
    public bool IsStepPost => Step == WizardStep.Post;
    public bool IsStepVerdict => Step == WizardStep.Verdict;

    public ObservableCollection<ProcessChoice> Processes { get; } = [];
    public ObservableCollection<string> RunLog { get; } = [];

    /// <summary>Ghost Tweak (Lab feature) lives as a section on the Measure page — it reuses this
    /// wizard's last comparison for its blind reveal.</summary>
    public GhostTweakViewModel Ghost { get; } = new(main);

    /// <summary>Reaction Lab (Lab feature): the reflex minigame, surfaced on the Measure page.</summary>
    public ReactionLabViewModel Reaction { get; } = new(settings);

    // ── Latency Lab (Lab feature): before/after chart of the last comparison ──
    public bool ShowLatencyLab => settings.IsFeatureEnabled(FeatureCatalog.LatencyLab);
    public bool HasLatencyData => LatencyRows.Count > 0;
    public ObservableCollection<LatencyRow> LatencyRows { get; } = [];
    public void RefreshLatencyFlag() => Raise(nameof(ShowLatencyLab));

    public IReadOnlyList<ScenarioPreset> Scenarios => ScenarioPreset.All;
    private ScenarioPreset _scenario = ScenarioPreset.All[0];
    public ScenarioPreset Scenario { get => _scenario; set => Set(ref _scenario, value); }

    public ProcessChoice? Target { get => _target; set => Set(ref _target, value); }
    public int Seconds { get => _seconds; set => Set(ref _seconds, Math.Clamp(value, 10, 600)); }
    public int Runs => settings.DefaultBenchmarkRuns;
    public string Status { get => _status; set => Set(ref _status, value); }
    public string VerdictText { get => _verdictText; set => Set(ref _verdictText, value); }
    public bool IsBusy { get => _busy; set => Set(ref _busy, value); }

    public bool IsElevated => Elevation.IsElevated();
    public bool PresentMonAvailable => PresentMonLocator.Find() is not null;
    public string BlockerText =>
        !IsElevated
            ? "La misura richiede l'amministratore (PresentMon legge l'ETW). Riavvia Verdict come amministratore per usare il wizard. Tutto il resto funziona senza."
            : !PresentMonAvailable
                ? "PresentMon non è ancora installato. Verdict usa Intel PresentMon (open source, MIT) per catturare i dati sui frame. Usa 'wpep tools install-presentmon' o il pulsante qui sotto (versione 2.4.1, SHA256 verificato)."
                : "";
    public bool CanMeasure => IsElevated && PresentMonAvailable;

    public RelayCommand RefreshProcessesCommand { get; private set; } = null!;
    public RelayCommand StartBaselineCommand { get; private set; } = null!;
    public RelayCommand StartPostCommand { get; private set; } = null!;
    public RelayCommand RestartCommand { get; private set; } = null!;
    public RelayCommand InstallPresentMonCommand { get; private set; } = null!;
    public RelayCommand RelaunchAsAdminCommand { get; private set; } = null!;

    public void InitCommands()
    {
        RefreshProcessesCommand = new(RefreshProcesses);
        StartBaselineCommand = new(() => _ = RunGroupAsync(baseline: true),
            () => CanMeasure && Target is not null && !IsBusy);
        StartPostCommand = new(() => _ = RunGroupAsync(baseline: false), () => !IsBusy);
        RestartCommand = new(Reset);
        InstallPresentMonCommand = new(() => _ = InstallPresentMonAsync(), () => !IsBusy);
        RelaunchAsAdminCommand = new(RelaunchAsAdmin);
    }

    private void RelaunchAsAdmin()
    {
        try
        {
            var exe = Environment.ProcessPath!;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Verb = "runas", // triggers the normal Windows UAC prompt
            });
            System.Windows.Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Status = "Elevazione annullata. Senza amministratore il wizard resta disattivato.";
        }
    }

    public void RefreshProcesses()
    {
        Processes.Clear();
        foreach (var p in System.Diagnostics.Process.GetProcesses()
                     .Where(p => p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0)
                     .OrderBy(p => p.ProcessName))
        {
            Processes.Add(new ProcessChoice($"{p.ProcessName}.exe", p.MainWindowTitle));
        }
    }

    private void Reset()
    {
        Step = WizardStep.Pick;
        RunLog.Clear();
        VerdictText = "";
        Status = "";
        _baseline = [];
        _post = [];
        RefreshProcesses();
    }

    private async Task InstallPresentMonAsync()
    {
        IsBusy = true;
        try
        {
            Status = "Downloading PresentMon (pinned, SHA256 verified)…";
            await PresentMonInstaller.InstallAsync(msg => Status = msg);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Raise(nameof(PresentMonAvailable));
            Raise(nameof(BlockerText));
            Raise(nameof(CanMeasure));
        }
    }

    private async Task RunGroupAsync(bool baseline)
    {
        if (Target is null)
            return;

        // PORTABILITY §2: benchmark su batteria = invalido, bloccato come F10.
        if (SnapshotBuilder.IsOnBattery() == true)
        {
            RunLog.Add("⚠ BLOCCATO: questo PC è a batteria. Il power throttling rende " +
                       "ogni misura invalida. Collega il caricatore e riprova.");
            return;
        }

        IsBusy = true;
        Step = baseline ? WizardStep.Baseline : WizardStep.Post;
        if (baseline)
        {
            _sessionDir = Path.Combine(AppContext.BaseDirectory, "runs",
                $"wizard-{DateTime.Now:yyyyMMdd-HHmmss}");
            RunLog.Add($"PROTOCOLLO — {Scenario.Name}:");
            RunLog.Add(Scenario.Instructions);
            RunLog.Add("Prima scaldati: cache degli shader e temperature hanno bisogno di qualche minuto di gioco prima della run 1.");
        }
        RunLog.Add("Passa al gioco ORA — la prima cattura parte tra 8 secondi…");

        string label = baseline ? "baseline" : "post";
        var dir = Path.Combine(_sessionDir, label);
        Directory.CreateDirectory(dir);

        try
        {
            var runs = await Task.Run(() => CaptureGroup(Target.ProcessName, dir, label));
            if (baseline)
            {
                _baseline = runs;
                double mde = Mde.Percent(runs.Select(r => r.Metrics.MedianFrameTimeMs).ToArray());
                if (mde > settings.NoiseGateThresholdPercent)
                {
                    RunLog.Add($"⚠ Questo scenario è troppo rumoroso: rileva solo effetti ≥{mde:F0}% " +
                               $"(soglia: {settings.NoiseGateThresholdPercent:F0}%). NON verrà emesso un " +
                               "verdetto. Valuta di ricominciare con uno scenario ripetibile.");
                }
                else
                {
                    RunLog.Add($"Scenario utilizzabile — effetto minimo rilevabile ≈ {mde:F1}% sulla mediana.");
                }
                Step = WizardStep.ApplyChange;
            }
            else
            {
                _post = runs;
                BuildVerdict();
                Step = WizardStep.Verdict;
            }
            main.TerminalLine = $"$ wpep bench · {label} {runs.Count} runs · 0 writes (data in runs\\)";
        }
        catch (Exception ex)
        {
            RunLog.Add($"Cattura fallita: {ex.Message}");
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private List<BenchmarkRun> CaptureGroup(string processName, string dir, string label)
    {
        var exe = PresentMonLocator.Find()
            ?? throw new InvalidOperationException("PresentMon not found.");
        var runner = new PresentMonRunner(exe);

        var snapshot = SnapshotBuilder.Build(DateTimeOffset.UtcNow);
        var environment = new RunEnvironment(
            snapshot.GpuName, snapshot.GpuDriverVersion,
            snapshot.DisplayWidth, snapshot.DisplayHeight,
            snapshot.MonitorCurrentHz, snapshot.PowerPlanGuid);

        // PORTABILITY §2: thermal throttling makes runs incomparable. Warn,
        // don't block — the user may be measuring exactly that.
        if (snapshot.GpuThermalThrottling == true)
            App.Current.Dispatcher.Invoke(() => RunLog.Add(
                "⚠ La GPU sta facendo thermal-throttling PROPRIO ORA. Nessun tweak software lo risolve — " +
                "controlla il flusso d'aria del case e le curve delle ventole. Le run catturate sotto throttling " +
                "sono confrontabili solo con altre run sotto throttling."));
        else if (snapshot.GpuTempC is > 85)
            App.Current.Dispatcher.Invoke(() => RunLog.Add(
                $"⚠ GPU a {snapshot.GpuTempC}°C prima della run — vicina al throttling. " +
                "I risultati possono andare alla deriva man mano che il calore sale tra le run."));

        Thread.Sleep(8000); // time to alt-tab back into the game

        var runs = new List<BenchmarkRun>(Runs);
        int invalid = 0;
        for (int i = 1; i <= Runs; i++)
        {
            App.Current.Dispatcher.Invoke(() =>
                RunLog.Add($"{label} run {i}/{Runs} — cattura {Seconds}s…"));
            try
            {
                var result = runner.Capture(processName, Seconds);
                var run = new BenchmarkRun(label, processName, DateTimeOffset.UtcNow,
                    Seconds, result.Metrics, result.FrameTimesMs, environment);
                File.WriteAllText(Path.Combine(dir, $"{label}-{runs.Count + 1:D2}.json"),
                    JsonSerializer.Serialize(run));
                runs.Add(run);
                var m = result.Metrics;
                App.Current.Dispatcher.Invoke(() => RunLog.Add(
                    $"  {m.FrameCount:N0} frames · avg {m.AvgFps:F1} fps · median {m.MedianFps:F1} · " +
                    $"1% low {m.OnePercentLowFps:F1} · 0.2% low {m.ZeroPointTwoPercentLowFps:F1}"));
            }
            catch (Exception ex)
            {
                // F4: run marked INVALID, partial data discarded, never counted.
                invalid++;
                int runNumber = i;
                App.Current.Dispatcher.Invoke(() => RunLog.Add(
                    $"⚠ Run {runNumber} interrotta — {ex.Message} " +
                    $"{runs.Count} di {Runs} run valide finora."));
                if (invalid >= 2)
                    throw new InvalidOperationException(
                        $"Due run interrotte di fila — il gioco è ancora in esecuzione? " +
                        $"Catturate {runs.Count} run valide.");
            }
        }

        if (runs.Count < 2)
            throw new InvalidOperationException("Run valide insufficienti per continuare.");

        FlagOutliers(runs, label);
        return runs;
    }

    /// <summary>F5: shared detector, flagged never silently excluded.</summary>
    private void FlagOutliers(IReadOnlyList<BenchmarkRun> runs, string label)
    {
        foreach (var outlier in OutlierDetector.Find(runs))
        {
            App.Current.Dispatcher.Invoke(() => RunLog.Add(
                $"⚠ {label} run {outlier.RunNumber} sembra un outlier (mediana lontana dalle altre: " +
                "cambio scena, alt-tab o compilazione shader?). Valuta di rifare il gruppo " +
                "seguendo il protocollo alla lettera."));
        }
    }

    /// <summary>The most recent completed comparison, exposed so the Ghost Tweak module can map it
    /// to a blind-reveal verdict. Null until a verdict has been built this run.</summary>
    public ComparisonEngine.ComparisonReport? LastComparison { get; private set; }

    private void BuildVerdict()
    {
        var env = EnvironmentValidator.Validate(_baseline, _post);
        if (!env.Valid)
        {
            VerdictText = $"Nessun verdetto. {env.BlockReason}";
            return;
        }

        var report = ComparisonEngine.Compare(_baseline, _post, settings.NoiseGateThresholdPercent);
        LastComparison = report;
        BuildLatencyRows(report);
        var primary = report.Metrics[0];

        if (report.GateTriggered)
        {
            VerdictText =
                $"Nessun verdetto. Questo scenario è troppo rumoroso per rilevare effetti sotto il {primary.MdePercent:F0}%. " +
                "Passa a uno scenario ripetibile e riprova.";
            return;
        }

        var lines = new List<string>();
        foreach (var m in report.Metrics)
        {
            lines.Add(m.Verdict switch
            {
                Verdict.Improvement =>
                    $"✓ {m.Metric}: migliorato del {Math.Abs(m.DeltaPercent):F1}% " +
                    $"(CI {m.Ci.Lower:F2}–{m.Ci.Upper:F2} ms, p={m.PValue:F3}). Questo è reale.",
                Verdict.Regression =>
                    $"✗ {m.Metric}: PEGGIORATO del {m.DeltaPercent:F1}% (p={m.PValue:F3}). Annullalo.",
                _ =>
                    $"— {m.Metric}: nessun effetto misurabile (soglia di rilevamento {m.MdePercent:F1}%).",
            });
        }
        if (report.Metrics.All(m => m.Verdict == Verdict.NoMeasurableEffect))
            lines.Add("\nNessun effetto misurabile su questo sistema. Annullalo, a meno che tu non abbia un altro motivo per tenerlo.");
        if (!report.Conclusive)
            lines.Add($"\n⚠ Meno di {ComparisonEngine.MinRunsForConclusion} run per lato: indicativo, non conclusivo.");
        VerdictText = string.Join("\n", lines);
    }

    /// <summary>Turns the comparison into before/after rows with bar widths scaled to a fixed max,
    /// so the Latency Lab can draw a simple chart with no charting library. Frametime is
    /// lower-is-better, so a negative delta (post &lt; baseline) is an improvement (green).</summary>
    private void BuildLatencyRows(ComparisonEngine.ComparisonReport report)
    {
        LatencyRows.Clear();
        const double maxBarPx = 260;
        double scaleMax = report.Metrics.Count == 0 ? 1
            : Math.Max(1, report.Metrics.Max(m => Math.Max(m.BaselineMedian, m.PostMedian)));
        foreach (var m in report.Metrics)
        {
            string color = m.Verdict switch
            {
                Verdict.Improvement => "Ok",
                Verdict.Regression => "Danger",
                _ => "TextMuted",
            };
            string label = m.DeltaPercent switch
            {
                < 0 => $"{m.DeltaPercent:F1}%",
                > 0 => $"+{m.DeltaPercent:F1}%",
                _ => "0%",
            };
            LatencyRows.Add(new LatencyRow(m.Metric, m.BaselineMedian, m.PostMedian, m.DeltaPercent,
                label, color,
                BaselineBar: m.BaselineMedian / scaleMax * maxBarPx,
                PostBar: m.PostMedian / scaleMax * maxBarPx));
        }
        Raise(nameof(HasLatencyData));
    }

    private void RaiseSteps()
    {
        Raise(nameof(IsStepPick));
        Raise(nameof(IsStepBaseline));
        Raise(nameof(IsStepApplyChange));
        Raise(nameof(IsStepPost));
        Raise(nameof(IsStepVerdict));
    }
}
