using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WPEP.Execution;
using WPEP.SystemAnalyzer;

namespace WPEP.App;

/// <summary>A Watchdog alert ready for the UI: a colour token + the human text.</summary>
public sealed record WatchAlertRow(string ColorKey, string Title, string Detail);

/// <summary>Watchdog (Lab feature) front-end: runs one drift check on demand and shows the alerts.
/// Cross-cutting — it reads EXPO/startup from a fresh scan, the baseline from Time Machine, and the
/// applied-tweak drift from the execution journal — so it lives on the MainViewModel, surfaced as a
/// section on the Changes page. Read-only: it reports, it never "fixes" silently. (The continuous
/// tray loop can sit on top of this same CheckAsync later.)</summary>
public sealed class WatchdogViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private bool _busy;
    private string _status = "Premi «Controlla ora» per cercare derive nel sistema.";
    private string _worstColor = "Ok";

    public WatchdogViewModel(MainViewModel main) => _main = main;

    public bool ShowWatchdog => _main.Settings.IsFeatureEnabled(FeatureCatalog.Watchdog);
    public bool IsBusy { get => _busy; set { Set(ref _busy, value); Raise(nameof(CanCheck)); } }
    public bool CanCheck => !IsBusy;
    public string Status { get => _status; set => Set(ref _status, value); }
    public string WorstColor { get => _worstColor; set => Set(ref _worstColor, value); }
    public ObservableCollection<WatchAlertRow> Alerts { get; } = [];

    public RelayCommand CheckCommand => new(() => _ = CheckAsync(), () => CanCheck);
    public void RefreshFlag() => Raise(nameof(ShowWatchdog));

    private async Task CheckAsync()
    {
        IsBusy = true;
        Status = "Controllo in corso…";
        Alerts.Clear();
        try
        {
            var (expoNow, startupNow, reverted) = await Task.Run(() =>
            {
                var hw = HardwareScanner.Scan();
                int startup = FreshInstallScanner.EnumerateStartup().Count(i => !i.IsMicrosoft);
                var drift = _main.Execution.DetectDrift();
                return (hw.ExpoEnabled, startup, drift);
            });

            var baseline = SystemTimeline.LoadAll().LastOrDefault();
            var inputs = new WatchInputs(
                ExpoBaseline: baseline?.ExpoEnabled, ExpoNow: expoNow,
                StartupBaseline: baseline?.ThirdPartyStartup ?? startupNow, StartupNow: startupNow,
                Reverted: reverted);

            var alerts = WatchdogCheck.Evaluate(inputs);
            foreach (var a in alerts)
                Alerts.Add(new WatchAlertRow(ColorFor(a.Level), a.Title, a.Detail));
            WorstColor = ColorFor(WatchdogCheck.Worst(alerts));
            Status = baseline is null
                ? "Controllo eseguito. Nota: nessuna baseline salvata — apri la pagina Scan una volta per darle un riferimento."
                : "Controllo eseguito.";
        }
        catch (System.Exception ex) { Status = $"Controllo fallito: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private static string ColorFor(WatchLevel level) => level switch
    {
        WatchLevel.Warn => "Warn",
        WatchLevel.Info => "Info",
        _ => "Ok",
    };
}
