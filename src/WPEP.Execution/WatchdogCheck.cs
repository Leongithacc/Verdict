namespace WPEP.Execution;

public enum WatchLevel { Ok, Info, Warn }

/// <summary>One thing the Watchdog noticed.</summary>
public sealed record WatchAlert(WatchLevel Level, string Title, string Detail);

/// <summary>Inputs for one Watchdog pass — primitive, so the logic is unit-testable without
/// touching the system. The caller gathers these from the scan, the last snapshot, and the
/// journal drift check.</summary>
public sealed record WatchInputs(
    bool? ExpoBaseline, bool? ExpoNow,
    int StartupBaseline, int StartupNow,
    IReadOnlyList<DriftItem> Reverted);

/// <summary>Watchdog (Lab feature): watches for the things that silently break a tuned system —
/// EXPO turning off, an applied tweak getting reverted (e.g. by a Windows update), startup bloat
/// creeping up. Pure/deterministic evaluation; the continuous tray loop is a thin shell on top.
/// Read-only: it reports, it never "fixes" behind your back.</summary>
public static class WatchdogCheck
{
    public const int StartupGrowthThreshold = 3; // new autostarts before we mention it

    public static IReadOnlyList<WatchAlert> Evaluate(WatchInputs i)
    {
        var alerts = new List<WatchAlert>();

        // EXPO/XMP went from on to off → real, free performance silently lost.
        if (i.ExpoBaseline == true && i.ExpoNow == false)
            alerts.Add(new(WatchLevel.Warn, "EXPO/XMP si è spento",
                "La RAM non gira più al profilo: probabile reset del BIOS o instabilità. Riattivalo per recuperare le prestazioni."));

        // Applied tweaks that no longer hold = something reverted Verdict's work.
        foreach (var d in i.Reverted)
            alerts.Add(new(WatchLevel.Warn, $"Tweak annullato: {d.TweakId}",
                $"Avevi applicato questo tweak ma il valore è cambiato ({d.Path}: atteso '{d.Expected}', ora '{d.Actual}'). " +
                "Spesso è colpa di un Windows Update o di una reinstallazione driver."));

        // Startup bloat creeping up.
        int grew = i.StartupNow - i.StartupBaseline;
        if (grew >= StartupGrowthThreshold)
            alerts.Add(new(WatchLevel.Info, $"{grew} nuovi programmi all'avvio",
                "Da quando hai impostato la baseline sono comparsi nuovi avvii automatici: occhio al boot più lento."));

        if (alerts.Count == 0)
            alerts.Add(new(WatchLevel.Ok, "Tutto a posto",
                "Nessuna deriva rilevata: EXPO, tweak applicati e avvii sono come te li aspetti."));

        return alerts;
    }

    /// <summary>Worst level among the alerts (drives the tray icon / headline color).</summary>
    public static WatchLevel Worst(IReadOnlyList<WatchAlert> alerts) =>
        alerts.Count == 0 ? WatchLevel.Ok : alerts.Max(a => a.Level);
}
