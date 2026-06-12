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

    public RelayCommand RefreshProcessesCommand => new(RefreshProcesses);
    public RelayCommand StartBaselineCommand => new(() => _ = RunGroupAsync(baseline: true), () => Target is not null && !IsBusy);
    public RelayCommand ChangeAppliedCommand => new(() => Step = WizardStep.Post);
    public RelayCommand StartPostCommand => new(() => _ = RunGroupAsync(baseline: false), () => !IsBusy);
    public RelayCommand RestartCommand => new(Reset);
    public RelayCommand InstallPresentMonCommand => new(() => _ = InstallPresentMonAsync(), () => !IsBusy);

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
        IsBusy = true;
        Step = baseline ? WizardStep.Baseline : WizardStep.Post;
        if (baseline)
        {
            _sessionDir = Path.Combine(AppContext.BaseDirectory, "runs",
                $"wizard-{DateTime.Now:yyyyMMdd-HHmmss}");
            RunLog.Add("Warm up first: shader caches and temperatures need a few minutes of play before run 1.");
        }

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

        var runs = new List<BenchmarkRun>(Runs);
        for (int i = 1; i <= Runs; i++)
        {
            App.Current.Dispatcher.Invoke(() =>
                RunLog.Add($"{label} run {i}/{Runs} — capturing {Seconds}s…"));
            var result = runner.Capture(processName, Seconds);
            var run = new BenchmarkRun(label, processName, DateTimeOffset.UtcNow,
                Seconds, result.Metrics, result.FrameTimesMs, environment);
            File.WriteAllText(Path.Combine(dir, $"{label}-{i:D2}.json"),
                JsonSerializer.Serialize(run));
            runs.Add(run);
            var m = result.Metrics;
            App.Current.Dispatcher.Invoke(() => RunLog.Add(
                $"  {m.FrameCount:N0} frames · avg {m.AvgFps:F1} fps · median {m.MedianFps:F1} · " +
                $"1% low {m.OnePercentLowFps:F1} · 0.2% low {m.ZeroPointTwoPercentLowFps:F1}"));
        }
        return runs;
    }

    private void BuildVerdict()
    {
        var env = EnvironmentValidator.Validate(_baseline, _post);
        if (!env.Valid)
        {
            VerdictText = $"No verdict. {env.BlockReason}";
            return;
        }

        var report = ComparisonEngine.Compare(_baseline, _post, settings.NoiseGateThresholdPercent);
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
