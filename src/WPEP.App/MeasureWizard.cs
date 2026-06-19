using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WPEP.Benchmark;
using WPEP.Core.Benchmark;
using WPEP.Core.Platform;
using WPEP.Statistics;
using WPEP.SystemAnalyzer;

namespace WPEP.App;

public enum WizardStep { Pick, Baseline, ApplyChange, Post, Verdict }

/// <summary>A measurement protocol: exact instructions that make runs
/// repeatable. The map codes are community benchmark islands, picked because
/// they remove human variance (automated camera) or minimize it (AFK).</summary>
public sealed record ScenarioPreset(string Name, string Instructions)
{
    public static readonly IReadOnlyList<ScenarioPreset> All =
    [
        new("Fortnite — YOUR OWN island, AFK (no unlock needed)",
            "Creative → CREATE → open your own island (yours is never blocked by " +
            "parental content ratings). Walk to a fixed spot, frame the same view, then " +
            "stand completely still — hands off mouse and keyboard for the whole capture. " +
            "Same spot, same view, every run."),
        new("Fortnite — benchmark map, automated",
            "Creative map code 4135-2210-3629 (\"BENCHMARK\" by DweEroz). If parental " +
            "content ratings block it, try: 5492-6089-6665 (fps test, fixed camera) · " +
            "5608-7013-5653 · 0240-9716-3198 — or use the own-island AFK preset, which " +
            "needs no unlock. Private session, press the map's launch button, then DON'T " +
            "touch anything: the camera sequence is automated. One warm-up pass before " +
            "run 1, restart the sequence for every run."),
        new("CS2 — workshop benchmark map",
            "Steam Workshop → search \"FPS Benchmark\" (uLLeticaL) → Subscribe. In CS2: " +
            "Play → Workshop Maps → FPS Benchmark → press the start button in the map: " +
            "the fly-through is fully automated. One warm-up pass, then one capture per pass."),
        new("HITMAN World of Assassination — integrated benchmark",
            "Launcher → Options/Benchmark (Dartmoor or Dubai scene): fully automated and " +
            "extremely repeatable. One warm-up pass, then start one capture per benchmark run."),
        new("Fortnite — fixed route on foot",
            "PRIVATE Creative island. Walk the same route at the same pace for the whole " +
            "capture, same direction every run. More game-like than AFK but noisier: check " +
            "that the baseline MDE stays under the gate before judging any tweak."),
        new("Cyberpunk 2077 — integrated benchmark",
            "Settings → Graphics → Benchmark, on a CLEAN install (no mods/CET — they alter " +
            "frametimes). Run the benchmark once as warm-up, then start one capture per " +
            "benchmark run."),
        new("Custom / live match",
            "Measure whatever you want — numbers always come out. Live matches usually " +
            "exceed the noise gate though: expect data, not a verdict."),
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
            ? "Measuring needs administrator (PresentMon reads ETW). Relaunch WPEP as administrator to use the wizard. Everything else works without it."
            : !PresentMonAvailable
                ? "PresentMon is not installed yet. WPEP uses Intel PresentMon (open source, MIT) to capture frame data. Use 'wpep tools install-presentmon' or the button below (pinned 2.4.1, SHA256 verified)."
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
            Status = "Elevation cancelled. The wizard stays disabled without administrator.";
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
            RunLog.Add("⚠ BLOCKED: this machine is running on battery. Power throttling makes " +
                       "every measurement invalid. Plug in the charger and try again.");
            return;
        }

        IsBusy = true;
        Step = baseline ? WizardStep.Baseline : WizardStep.Post;
        if (baseline)
        {
            _sessionDir = Path.Combine(AppContext.BaseDirectory, "runs",
                $"wizard-{DateTime.Now:yyyyMMdd-HHmmss}");
            RunLog.Add($"PROTOCOL — {Scenario.Name}:");
            RunLog.Add(Scenario.Instructions);
            RunLog.Add("Warm up first: shader caches and temperatures need a few minutes of play before run 1.");
        }
        RunLog.Add("Switch to the game NOW — first capture starts in 8 seconds…");

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
                    RunLog.Add($"⚠ This scenario is too noisy: it can only detect effects ≥{mde:F0}% " +
                               $"(gate: {settings.NoiseGateThresholdPercent:F0}%). A verdict will NOT be " +
                               "emitted. Consider restarting with a repeatable scenario.");
                }
                else
                {
                    RunLog.Add($"Scenario usable — minimum detectable effect ≈ {mde:F1}% on the median.");
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
            RunLog.Add($"Capture failed: {ex.Message}");
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
                "⚠ The GPU is thermal-throttling RIGHT NOW. No software tweak fixes this — " +
                "check case airflow and fan curves. Runs captured while throttling are " +
                "comparable only with other throttled runs."));
        else if (snapshot.GpuTempC is > 85)
            App.Current.Dispatcher.Invoke(() => RunLog.Add(
                $"⚠ GPU at {snapshot.GpuTempC}°C before the run — close to throttling. " +
                "Results may drift as heat builds across runs."));

        Thread.Sleep(8000); // time to alt-tab back into the game

        var runs = new List<BenchmarkRun>(Runs);
        int invalid = 0;
        for (int i = 1; i <= Runs; i++)
        {
            App.Current.Dispatcher.Invoke(() =>
                RunLog.Add($"{label} run {i}/{Runs} — capturing {Seconds}s…"));
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
                    $"⚠ Run {runNumber} aborted — {ex.Message} " +
                    $"{runs.Count} of {Runs} valid runs so far."));
                if (invalid >= 2)
                    throw new InvalidOperationException(
                        $"Two runs aborted in a row — is the game still running? " +
                        $"Captured {runs.Count} valid runs.");
            }
        }

        if (runs.Count < 2)
            throw new InvalidOperationException("Not enough valid runs to continue.");

        FlagOutliers(runs, label);
        return runs;
    }

    /// <summary>F5: shared detector, flagged never silently excluded.</summary>
    private void FlagOutliers(IReadOnlyList<BenchmarkRun> runs, string label)
    {
        foreach (var outlier in OutlierDetector.Find(runs))
        {
            App.Current.Dispatcher.Invoke(() => RunLog.Add(
                $"⚠ {label} run {outlier.RunNumber} looks like an outlier (median far from the rest: " +
                "scene change, alt-tab or shader compilation?). Consider redoing the group " +
                "with the protocol followed strictly."));
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
            VerdictText = $"No verdict. {env.BlockReason}";
            return;
        }

        var report = ComparisonEngine.Compare(_baseline, _post, settings.NoiseGateThresholdPercent);
        LastComparison = report;
        var primary = report.Metrics[0];

        if (report.GateTriggered)
        {
            VerdictText =
                $"No verdict. This scenario is too noisy to detect effects below {primary.MdePercent:F0}%. " +
                "Switch to a repeatable scenario and try again.";
            return;
        }

        var lines = new List<string>();
        foreach (var m in report.Metrics)
        {
            lines.Add(m.Verdict switch
            {
                Verdict.Improvement =>
                    $"✓ {m.Metric}: improved by {Math.Abs(m.DeltaPercent):F1}% " +
                    $"(CI {m.Ci.Lower:F2}–{m.Ci.Upper:F2} ms, p={m.PValue:F3}). This one's real.",
                Verdict.Regression =>
                    $"✗ {m.Metric}: REGRESSED by {m.DeltaPercent:F1}% (p={m.PValue:F3}). Roll it back.",
                _ =>
                    $"— {m.Metric}: no measurable effect (detection threshold {m.MdePercent:F1}%).",
            });
        }
        if (report.Metrics.All(m => m.Verdict == Verdict.NoMeasurableEffect))
            lines.Add("\nNo measurable effect on this system. Roll it back unless you have another reason to keep it.");
        if (!report.Conclusive)
            lines.Add($"\n⚠ Fewer than {ComparisonEngine.MinRunsForConclusion} runs per side: indicative, not conclusive.");
        VerdictText = string.Join("\n", lines);
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
