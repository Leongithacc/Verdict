using WPEP.Core.Diagnostics;

namespace WPEP.Diagnostics;

public enum StutterSeverity { Clean, Minor, Likely, Severe }

/// <summary>One plain-language explanation: which driver, what hardware it is, and what to do.</summary>
public sealed record StutterFinding(
    StutterSeverity Severity, string Driver, string Component, string Plain, string Tip);

public sealed record StutterReport(
    StutterSeverity Overall, string Headline, IReadOnlyList<StutterFinding> Findings);

/// <summary>Explain my Stutter (Lab feature): turns the raw DPC/ISR report into plain Italian —
/// names the driver most likely to cause stutter and what hardware it belongs to. Pure and
/// deterministic, so it's unit-tested and identical wherever it runs. It reuses the diagnostics
/// engine we already have; no new measurement, no system writes.</summary>
public static class StutterExplainer
{
    // A long DPC blocks the CPU from delivering the next frame / audio buffer. Standard guidance:
    // under ~500µs is healthy (matches the rest of the tool); we only nudge as it approaches that.
    private const double GoodMaxUs = 400;    // below this, nothing to worry about (e.g. 277µs is fine)
    private const double LikelyUs = 500;     // a routine over this can cause audible/visible hitches
    private const double SevereUs = 1000;    // clearly bad

    public static StutterReport Explain(DpcIsrReport report)
    {
        var findings = new List<StutterFinding>();

        // Only real, resolved drivers matter; rank by worst single latency then by 500µs+ spikes.
        var ranked = report.Drivers
            .Where(d => !d.Driver.Equals(DpcIsrAggregator.UnresolvedKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.MaxUs)
            .ThenByDescending(d => d.SpikesOver500Us)
            .ToList();

        foreach (var d in ranked)
        {
            var sev = Severity(d);
            if (sev == StutterSeverity.Clean) continue; // don't list innocent drivers
            string comp = DescribeDriver(d.Driver);
            string plain = sev switch
            {
                StutterSeverity.Severe =>
                    $"{comp} ({d.Driver}) ha tenuto la CPU occupata fino a {d.MaxUs:F0} µs " +
                    $"({d.SpikesOver500Us} picchi oltre 500 µs): è la causa più probabile dei tuoi stutter.",
                StutterSeverity.Likely =>
                    $"{comp} ({d.Driver}) ha avuto picchi fino a {d.MaxUs:F0} µs — abbastanza da causare " +
                    "micro-scatti occasionali in gioco o nell'audio.",
                _ =>
                    $"{comp} ({d.Driver}) ha latenze un po' alte (max {d.MaxUs:F0} µs), di solito innocue " +
                    "ma da tenere d'occhio.",
            };
            findings.Add(new StutterFinding(sev, d.Driver, comp, plain, TipFor(d.Driver, comp)));
        }

        var overall = findings.Count == 0 ? StutterSeverity.Clean : findings.Max(f => f.Severity);
        string headline = overall switch
        {
            StutterSeverity.Clean =>
                "Nessun colpevole: nessun driver ha latenze DPC/ISR abbastanza alte da causare stutter. Sistema pulito.",
            StutterSeverity.Minor =>
                "Tutto sommato pulito: qualche latenza un po' alta ma niente che spieghi stutter reali.",
            StutterSeverity.Likely =>
                "Trovato un sospetto: un driver con picchi capaci di causare micro-scatti. Dettagli sotto.",
            _ =>
                "Trovato il colpevole: un driver con latenze elevate è la causa probabile dei tuoi stutter.",
        };
        return new StutterReport(overall, headline, findings);
    }

    private static StutterSeverity Severity(DriverStats d)
    {
        if (d.SpikesOver1000Us > 0 || d.MaxUs >= SevereUs) return StutterSeverity.Severe;
        if (d.SpikesOver500Us > 0 || d.MaxUs >= LikelyUs) return StutterSeverity.Likely;
        if (d.MaxUs >= GoodMaxUs) return StutterSeverity.Minor;
        return StutterSeverity.Clean;
    }

    /// <summary>Maps a kernel driver file to the hardware/component it drives, in plain Italian.
    /// Unknown drivers fall back to a generic label — honest, never guessed wrong.</summary>
    public static string DescribeDriver(string driver)
    {
        string d = driver.ToLowerInvariant();
        bool Has(params string[] keys) => keys.Any(k => d.Contains(k));

        if (Has("nvlddmkm", "nvgpu")) return "GPU NVIDIA";
        if (Has("amdkmdag", "atikmdag", "amdgpu")) return "GPU AMD";
        if (Has("igdkmd", "igdkmdn", "intelgfx")) return "GPU Intel";
        if (Has("dxgkrnl")) return "sottosistema grafico Windows";
        if (Has("ndis", "tcpip", "rt6", "rt8", "e1d", "e22", "nvenet", "killer", "rtwlan", "athw", "mtkwl", "netwtw"))
            return "scheda di rete";
        if (Has("portcls", "ksthunk", "hdaudio", "wdmaud", "rtkvhd", "nvhda", "usbaudio"))
            return "scheda audio";
        if (Has("storport", "stornvme", "nvme", "iastor", "storahci", "ahci"))
            return "disco / SSD";
        if (Has("usbport", "usbxhci", "usbhub", "ucx01000", "hidusb"))
            return "controller USB";
        if (Has("acpi", "intelpep", "amdppm", "processr")) return "gestione energia / ACPI";
        if (Has("wdf01000", "ntoskrnl", "ntkrnl")) return "kernel di Windows";
        if (Has("win32k", "cdd")) return "interfaccia grafica di Windows";
        return "un driver di sistema";
    }

    private static string TipFor(string driver, string component) => component switch
    {
        "GPU NVIDIA" or "GPU AMD" or "GPU Intel" =>
            "Aggiorna i driver grafici a una versione recente (clean install) e verifica HAGS/MPO.",
        "scheda di rete" =>
            "Aggiorna il driver di rete dal sito del produttore (non Windows Update) e disattiva l'energy saving della NIC.",
        "scheda audio" =>
            "Aggiorna il driver audio e prova a disattivare i miglioramenti/effetti audio.",
        "disco / SSD" =>
            "Aggiorna firmware SSD + driver storage; un disco che satura può alzare le latenze.",
        "controller USB" =>
            "Stacca periferiche USB sospette una per volta; un dispositivo USB difettoso alza le latenze.",
        "gestione energia / ACPI" =>
            "Verifica il piano energetico (Prestazioni elevate) e gli stati C nel BIOS.",
        _ =>
            "Aggiorna il driver alla versione più recente del produttore e ripeti la misura.",
    };
}
