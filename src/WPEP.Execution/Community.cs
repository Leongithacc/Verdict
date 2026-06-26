using System.Text.Json;

namespace WPEP.Execution;

// ── V7 — COMMUNITY EVIDENCE (privacy-first, consent-first) ───────────────────
// Verdict già MISURA gli esiti (Ghost Tweak: aiutato/placebo/peggiorato; Measure: delta) e ha una
// firma rig ANONIMA (RigDna, zero PII). V7.0 li raccoglie in un registro LOCALE in forma pronta da
// federare. Il backend community è dietro un'interfaccia swappable: di default è LOCALE (nessuna rete).
// "Ha aiutato il 73% dei rig simili" si accende SOLO quando un backend è configurato E l'utente acconsente.

/// <summary>Un esito anonimo: questo rig ha applicato questo tweak, ecco com'è andata.
/// Nessun dato personale — <see cref="RigSignature"/> è il codice RigDna deterministico,
/// mai un nome/IP/identità.</summary>
public sealed record EvidenceRecord(
    string RigSignature,
    string RigTier,
    string TweakId,
    string Outcome,         // "helped" | "no-effect" | "hurt" | "applied"
    double? DeltaPercent,
    string CapturedAtIso);

public sealed record CommunityStats(int SampleSize, int HelpedPercent, int NoEffectPercent, int HurtPercent)
{
    public string Headline => SampleSize == 0
        ? "nessun dato"
        : $"ha aiutato il {HelpedPercent}% dei rig simili ({SampleSize} misure)";
}

/// <summary>Registro append-only dei TUOI esiti, in forma anonima e pronta da inviare.
/// %LOCALAPPDATA%\Verdict\evidence.json. È la base che V7 federerà in seguito.</summary>
public static class EvidenceLedger
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Verdict", "evidence.json");

    public static IReadOnlyList<EvidenceRecord> Load() => Load(FilePath);

    public static IReadOnlyList<EvidenceRecord> Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<EvidenceRecord>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch { return []; }
    }

    public static void Append(EvidenceRecord record) => Append(record, FilePath);

    public static void Append(EvidenceRecord record, string path)
    {
        try
        {
            var all = new List<EvidenceRecord>(Load(path)) { record };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* l'evidence è best-effort: non deve MAI rompere un apply */ }
    }

    /// <summary>Aggrega gli esiti in statistiche stile-community (pura: usata localmente ora e dal
    /// backend remoto poi). Solo gli esiti MISURATI (helped/no-effect/hurt) contano per le percentuali;
    /// i record "applied" sono il campione di chi l'ha provato.</summary>
    public static CommunityStats Aggregate(IEnumerable<EvidenceRecord> records)
    {
        var list = records.ToList();
        int helped = list.Count(r => r.Outcome == "helped");
        int none = list.Count(r => r.Outcome == "no-effect");
        int hurt = list.Count(r => r.Outcome == "hurt");
        int measured = helped + none + hurt;
        return measured == 0
            ? new CommunityStats(0, 0, 0, 0)
            : new CommunityStats(measured, Pct(helped, measured), Pct(none, measured), Pct(hurt, measured));
    }

    private static int Pct(int n, int total) => (int)Math.Round(100.0 * n / total);
}

/// <summary>Il backend community, swappable. Default = locale (nessuna rete). Quando Léon sceglierà un
/// server (decisione privacy + hosting) un RemoteBackend si innesta qui senza toccare il resto.</summary>
public interface ICommunityBackend
{
    string Name { get; }
    bool IsConfigured { get; }
    Task SubmitAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken ct = default);
    Task<CommunityStats?> QueryAsync(string tweakId, string rigTier, CancellationToken ct = default);
}

/// <summary>Backend predefinito: SOLO locale. Niente rete, mai. <see cref="IsConfigured"/>=false →
/// la UI dice onestamente "community non ancora attiva".</summary>
public sealed class LocalOnlyBackend : ICommunityBackend
{
    public string Name => "Locale (community non attiva)";
    public bool IsConfigured => false;
    public Task SubmitAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task<CommunityStats?> QueryAsync(string tweakId, string rigTier, CancellationToken ct = default)
        => Task.FromResult<CommunityStats?>(null);
}

public static class CommunityConfig
{
    // Vuoto finché Léon non sceglie un server. Niente rete fino ad allora.
    public const string Endpoint = "";
    public static bool IsConfigured => Endpoint.Length > 0;
}

/// <summary>Façade V7: registra SEMPRE i tuoi esiti in locale e, SE un backend è configurato e hai dato
/// consenso, può inviare/interrogare. Consent-first: di default nulla lascia il PC.</summary>
public sealed class CommunityService
{
    private readonly ICommunityBackend _backend;
    public CommunityService() : this(new LocalOnlyBackend()) { }
    public CommunityService(ICommunityBackend backend) => _backend = backend;

    public bool CommunityActive => _backend.IsConfigured;
    public string BackendName => _backend.Name;

    public void Record(string rigSignature, string rigTier, string tweakId,
        string outcome, double? deltaPercent, string capturedAtIso)
        => EvidenceLedger.Append(new EvidenceRecord(
            rigSignature, rigTier, tweakId, outcome, deltaPercent, capturedAtIso));

    /// <summary>Il TUO storico per un tweak (sempre disponibile, offline).</summary>
    public IReadOnlyList<EvidenceRecord> MyHistory(string tweakId)
        => EvidenceLedger.Load().Where(r => r.TweakId == tweakId).ToList();

    /// <summary>Statistiche community per un tweak su rig simili — solo se un backend è configurato;
    /// altrimenti null (la UI mostra "non attiva"). Non inventa MAI numeri.</summary>
    public Task<CommunityStats?> CommunityStatsAsync(string tweakId, string rigTier, CancellationToken ct = default)
        => _backend.IsConfigured ? _backend.QueryAsync(tweakId, rigTier, ct) : Task.FromResult<CommunityStats?>(null);
}
