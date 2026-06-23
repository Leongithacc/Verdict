using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WPEP.Execution;

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
    public RelayCommand StartTrayCommand => new(StartTray);
    public void RefreshFlag()
    {
        Raise(nameof(ShowWatchdog));
        Raise(nameof(AutostartEnabled)); // reflect the real Run-key state when the page opens
    }

    /// <summary>Opt-in: launch the tray guardian automatically at Windows logon (HKCU Run, reversible,
    /// no admin). Bound to a checkbox; reads/writes the real registry value on toggle.</summary>
    public bool AutostartEnabled
    {
        get => TrayAutostart.IsEnabled();
        set
        {
            if (value == TrayAutostart.IsEnabled()) return;
            bool ok = value ? TrayAutostart.Enable() : TrayAutostart.Disable();
            Raise(nameof(AutostartEnabled));
            Status = !ok
                ? "Non sono riuscito a cambiare l'avvio automatico (registro)."
                : value
                    ? "Fatto: la sorveglianza partirà da sola a ogni avvio di Windows."
                    : "Avvio automatico disattivato: la sorveglianza non parte più da sola.";
        }
    }

    /// <summary>Launch the background tray guardian (wpep-tray.exe) shipped next to the GUI. It runs
    /// the same read-only Watchdog pass on a timer and only notifies on NEW drift — no spam.</summary>
    private void StartTray()
    {
        try
        {
            var exe = System.IO.Path.Combine(System.AppContext.BaseDirectory, "wpep-tray.exe");
            if (!System.IO.File.Exists(exe))
            {
                Status = "Agente di sorveglianza non trovato (wpep-tray.exe). Ripubblica l'app e riprova.";
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
            Status = "Sorveglianza avviata: cerca l'icona scudo «Verdict» nel tray, vicino all'orologio. Ti avvisa solo se qualcosa cambia.";
        }
        catch (System.Exception ex) { Status = $"Avvio sorveglianza fallito: {ex.Message}"; }
    }

    private async Task CheckAsync()
    {
        IsBusy = true;
        Status = "Controllo in corso…";
        Alerts.Clear();
        try
        {
            // Same single gather/evaluate the CLI and the background tray host use.
            var pass = await Task.Run(() => WatchdogProbe.RunPass(_main.Execution.DetectDrift));

            foreach (var a in pass.Alerts)
                Alerts.Add(new WatchAlertRow(ColorFor(a.Level), a.Title, a.Detail));
            WorstColor = ColorFor(WatchdogCheck.Worst(pass.Alerts));
            Status = pass.HasBaseline
                ? "Controllo eseguito."
                : "Controllo eseguito. Nota: nessuna baseline salvata — apri la pagina Scan una volta per darle un riferimento.";
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
