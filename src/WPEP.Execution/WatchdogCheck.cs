using WPEP.SystemAnalyzer;

namespace WPEP.Execution;

public enum WatchLevel { Ok, Info, Warn }

/// <summary>One thing the Watchdog noticed. <paramref name="Key"/> is a STABLE identity for the
/// alert (independent of any count in the title) so the continuous monitor can de-dupe across passes
/// and notify only on real changes. Defaults to empty for ad-hoc construction.</summary>
public sealed record WatchAlert(WatchLevel Level, string Title, string Detail, string Key = "");

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
                "La RAM non gira più al profilo: probabile reset del BIOS o instabilità. Riattivalo per recuperare le prestazioni.",
                "expo"));

        // Applied tweaks that no longer hold = something reverted Verdict's work.
        foreach (var d in i.Reverted)
            alerts.Add(new(WatchLevel.Warn, $"Tweak annullato: {d.TweakId}",
                $"Avevi applicato questo tweak ma il valore è cambiato ({d.Path}: atteso '{d.Expected}', ora '{d.Actual}'). " +
                "Spesso è colpa di un Windows Update o di una reinstallazione driver.",
                $"revert:{d.TweakId}"));

        // Startup bloat creeping up. Stable key (no count) so the monitor notifies once, not per +1.
        int grew = i.StartupNow - i.StartupBaseline;
        if (grew >= StartupGrowthThreshold)
            alerts.Add(new(WatchLevel.Info, $"{grew} nuovi programmi all'avvio",
                "Da quando hai impostato la baseline sono comparsi nuovi avvii automatici: occhio al boot più lento.",
                "startup"));

        if (alerts.Count == 0)
            alerts.Add(new(WatchLevel.Ok, "Tutto a posto",
                "Nessuna deriva rilevata: EXPO, tweak applicati e avvii sono come te li aspetti.",
                "ok"));

        return alerts;
    }

    /// <summary>Worst level among the alerts (drives the tray icon / headline color).</summary>
    public static WatchLevel Worst(IReadOnlyList<WatchAlert> alerts) =>
        alerts.Count == 0 ? WatchLevel.Ok : alerts.Max(a => a.Level);
}

/// <summary>Turns a stream of Watchdog passes into NOTIFICATIONS without spam. It remembers which
/// alerts are currently active (by their stable <see cref="WatchAlert.Key"/>) and reports only the
/// ones NEWLY appearing since the previous pass; an alert that clears drops out, so it re-notifies
/// if it returns later. Deterministic and unit-tested — the tray timer is a thin shell over Update.</summary>
public sealed class WatchdogMonitor
{
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);

    /// <summary>Feed one pass' alerts; returns the actionable ones (Info/Warn) that are new since the
    /// previous pass. Ok is never actionable. Idempotent for an unchanged system: repeated identical
    /// passes return nothing after the first.</summary>
    public IReadOnlyList<WatchAlert> Update(IReadOnlyList<WatchAlert> pass)
    {
        var actionable = pass.Where(a => a.Level != WatchLevel.Ok).ToList();
        var fresh = actionable.Where(a => !_active.Contains(KeyOf(a))).ToList();
        _active.Clear();
        foreach (var a in actionable) _active.Add(KeyOf(a));
        return fresh;
    }

    private static string KeyOf(WatchAlert a) => a.Key.Length > 0 ? a.Key : $"{a.Level}:{a.Title}";
}

/// <summary>The result of one full Watchdog pass: the alerts plus whether a baseline existed (so the
/// caller can hint "set a baseline first" honestly).</summary>
public sealed record WatchdogPass(IReadOnlyList<WatchAlert> Alerts, bool HasBaseline);

/// <summary>Gathers the live inputs for a Watchdog pass and evaluates them — the SINGLE place that
/// reads EXPO (scan), startup count (fresh-install scan), the saved baseline (Time Machine) and the
/// applied-tweak drift (execution journal). Shared by CLI `wpep watch`, the GUI Watchdog panel and
/// the background tray host so all three behave identically. Read-only: it reports, never "fixes".</summary>
public static class WatchdogProbe
{
    /// <summary><paramref name="detectDrift"/> is the engine's drift check (e.g. ExecutionEngine or
    /// the GUI's ExecutionService DetectDrift) — passed as a delegate so this stays decoupled from
    /// which wrapper the caller holds.</summary>
    public static WatchdogPass RunPass(Func<IReadOnlyList<DriftItem>> detectDrift)
    {
        var hw = HardwareScanner.Scan();
        int startupNow = FreshInstallScanner.EnumerateStartup().Count(i => !i.IsMicrosoft);
        var baseline = SystemTimeline.LoadAll().LastOrDefault();
        var reverted = detectDrift();

        var inputs = new WatchInputs(
            ExpoBaseline: baseline?.ExpoEnabled, ExpoNow: hw.ExpoEnabled,
            StartupBaseline: baseline?.ThirdPartyStartup ?? startupNow, StartupNow: startupNow,
            Reverted: reverted);

        return new WatchdogPass(WatchdogCheck.Evaluate(inputs), baseline is not null);
    }
}
