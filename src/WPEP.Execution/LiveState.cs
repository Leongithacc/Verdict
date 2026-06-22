using WPEP.KnowledgeBase;
using WPEP.SystemAnalyzer;

namespace WPEP.Execution;

/// <summary>Costruisce il detector "già attivo?" usato dall'advisor/scan per trasformare
/// "non rilevabile" nello stato reale di ogni tweak applicabile. Ottimizzazione chiave: legge
/// TUTTI i setting nvidia-drs in UNA sola sessione NVAPI invece di aprirne una per tweak (prima
/// erano ~7 cicli Initialize/LoadSettings/Unload per scansione). Per gli altri metodi usa il motore.
/// Condiviso tra CLI e GUI così la logica vive in un posto solo.</summary>
public static class LiveState
{
    public static Func<TweakEntry, bool?> Detector(
        IReadOnlyList<TweakEntry> entries,
        Func<TweakEntry, bool> canApply,
        Func<TweakEntry, ExecutionPlan> buildPlan)
    {
        // Pre-leggi in batch tutti gli id nvidia-drs applicabili: una sessione NVAPI, un LoadSettings.
        var nvIds = entries
            .Where(e => e.Apply?.Method == "nvidia-drs" && canApply(e))
            .SelectMany(e => e.Apply!.Operations)
            .Select(o => ParseId(o.Path))
            .Where(id => id.HasValue).Select(id => id!.Value)
            .Distinct().ToArray();
        var (nvOk, nvValues) = NvApi.ReadDwordSettings(nvIds);

        return e =>
        {
            if (e.Apply is not { } apply || !canApply(e)) return null;
            if (apply.Method == "nvidia-drs")
            {
                if (!nvOk) return null; // interop NVAPI inaffidabile → non determinabile (niente eccezioni per-tweak)
                // Già attivo = OGNI operazione del tweak è al valore target.
                return apply.Operations.All(o =>
                {
                    var id = ParseId(o.Path);
                    var target = ParseId(o.ValueAfter);
                    return id.HasValue && target.HasValue
                        && nvValues.TryGetValue(id.Value, out var v) && v == target.Value;
                });
            }
            try { return buildPlan(e).IsAlreadyApplied; }
            catch { return null; }
        };
    }

    /// <summary>Parse di un uint scritto in hex ("0x...") o decimale; null se mancante/illeggibile.</summary>
    private static uint? ParseId(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        try
        {
            return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt32(s[2..], 16)
                : uint.Parse(s);
        }
        catch { return null; }
    }
}
