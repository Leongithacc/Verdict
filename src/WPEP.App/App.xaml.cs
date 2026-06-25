using System;
using System.IO;
using System.Windows;

namespace WPEP.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Global safety net (TIER 1 resilience): a crash must never vanish silently or dump a raw
        // Windows error box. The journal is already flushed to disk on every apply, so nothing
        // half-applied is ever lost — we just log the error and show a calm, honest message.
        DispatcherUnhandledException += (_, ex) => { Handle(ex.Exception, "UI"); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Handle(ex.ExceptionObject as Exception, "AppDomain");
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Handle(ex.Exception, "Task");
            ex.SetObserved();
        };

        base.OnStartup(e);
    }

    private static void Handle(Exception? ex, string source)
    {
        if (ex is null)
            return;
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dir);
            var log = Path.Combine(dir, "crash.log");
            File.AppendAllText(log, $"--- {DateTime.Now:o} [{source}] ---\n{ex}\n\n");

            MessageBox.Show(
                "Verdict ha incontrato un errore imprevisto, ma le tue modifiche applicate sono al sicuro: " +
                "ogni modifica è salvata su disco e reversibile dalla pagina Modifiche.\n\n" +
                $"Dettaglio tecnico salvato in:\n{log}\n\n{ex.Message}",
                "Verdict — errore imprevisto", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch
        {
            // The crash handler must itself never throw.
        }
    }
}
