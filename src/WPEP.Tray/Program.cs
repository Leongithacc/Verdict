using System.Diagnostics;
using WPEP.Execution;

namespace WPEP.Tray;

/// <summary>Verdict's background guardian: a system-tray agent that, every few minutes, runs the
/// same read-only Watchdog pass as `wpep watch` and the GUI panel — and pops a balloon ONLY when
/// something new drifts (EXPO turned off, an applied tweak got reverted, startup bloat crept up).
/// Pure WinForms, isolated from the WPF app so the two toolkits never clash. Read-only: it watches
/// your back, it never changes anything silently.</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const int PollMinutes = 10;

    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly WatchdogMonitor _monitor = new();
    private readonly ExecutionEngine _engine =
        new(new RealRegistryAccess(), ExecutionEngine.DefaultJournalDirectory);

    private bool _busy;
    private bool _paused;

    public TrayContext()
    {
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "Verdict — sorveglianza attiva",
            Visible = true,
            BalloonTipTitle = "Verdict",
        };
        _icon.DoubleClick += (_, _) => OpenApp();

        _pauseItem = new ToolStripMenuItem("Sospendi", null, (_, _) => TogglePause());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Controlla ora", null, async (_, _) => await CheckAsync(announce: true)));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripMenuItem("Apri Verdict", null, (_, _) => OpenApp()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Esci", null, (_, _) => ExitThread()));
        _icon.ContextMenuStrip = menu;

        // First tick fires soon (the WinForms sync-context is live by then, so post-await updates
        // marshal back to the UI thread); afterwards it settles into the real cadence.
        _timer = new System.Windows.Forms.Timer { Interval = 1500 };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Interval = PollMinutes * 60_000;
        await CheckAsync(announce: false);
    }

    private async Task CheckAsync(bool announce)
    {
        if (_busy)
            return;
        if (_paused)
        {
            if (announce)
                Balloon(ToolTipIcon.Info, "Verdict", "Sorveglianza in pausa.");
            return;
        }

        _busy = true;
        try
        {
            var pass = await Task.Run(() => WatchdogProbe.RunPass(_engine.DetectDrift));
            var worst = WatchdogCheck.Worst(pass.Alerts);

            _icon.Icon = IconFor(worst);
            _icon.Text = Clip(worst switch
            {
                WatchLevel.Warn => "Verdict — deriva rilevata",
                WatchLevel.Info => "Verdict — qualcosa da vedere",
                _ => "Verdict — tutto a posto",
            });

            // Notify only the NEWLY-appeared alerts (the monitor de-dupes across passes).
            var fresh = _monitor.Update(pass.Alerts);
            foreach (var a in fresh)
                Balloon(TipFor(a.Level), TitleFor(a.Level), $"{a.Title} — {a.Detail}");

            if (announce && fresh.Count == 0)
                Balloon(ToolTipIcon.Info, "Verdict",
                    worst == WatchLevel.Ok
                        ? "Tutto a posto: nessuna deriva."
                        : "Nessuna novità dall'ultimo controllo.");
        }
        catch
        {
            // Read-only pass: a transient WMI/registry hiccup must never crash the guardian.
        }
        finally
        {
            _busy = false;
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? "Riprendi" : "Sospendi";
        _icon.Icon = _paused ? SystemIcons.Information : SystemIcons.Shield;
        _icon.Text = Clip(_paused ? "Verdict — sorveglianza in pausa" : "Verdict — sorveglianza attiva");
        if (_paused)
            Balloon(ToolTipIcon.Info, "Verdict", "Sorveglianza sospesa. Riprendila dal menu del tray.");
    }

    /// <summary>Launch the Verdict GUI sitting next to this agent (published into the same folder).</summary>
    private void OpenApp()
    {
        try
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "WPEP.exe");
            if (File.Exists(exe))
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort: if the GUI isn't beside us, do nothing rather than crash the tray.
        }
    }

    private void Balloon(ToolTipIcon icon, string title, string text) =>
        _icon.ShowBalloonTip(8000, title, text.Length > 240 ? text[..240] : text, icon);

    private static Icon IconFor(WatchLevel level) => level switch
    {
        WatchLevel.Warn => SystemIcons.Warning,
        WatchLevel.Info => SystemIcons.Information,
        _ => SystemIcons.Shield,
    };

    private static ToolTipIcon TipFor(WatchLevel level) => level switch
    {
        WatchLevel.Warn => ToolTipIcon.Warning,
        _ => ToolTipIcon.Info,
    };

    private static string TitleFor(WatchLevel level) =>
        level == WatchLevel.Warn ? "Verdict — attenzione" : "Verdict";

    // NotifyIcon.Text hard-caps at 63 chars; keep us safely under it.
    private static string Clip(string s) => s.Length > 60 ? s[..60] : s;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
