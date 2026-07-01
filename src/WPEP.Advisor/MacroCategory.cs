namespace WPEP.Advisor;

/// <summary>Mapping runtime dalle 7 categorie tecniche della KB (power/gpu/scheduler/input/
/// network/security/background) ai 4 bucket UX ispirati all'analisi Hone
/// (docs/VS_HONE.md sez. 3.2): FPS/Latency, Network/Ping, Stability/QoL, Background.
/// Non tocca la KB (le voci restano con la loro categoria tecnica): è solo una vista
/// alternativa per rendere il catalogo più leggibile a un utente non tecnico.</summary>
public static class MacroCategory
{
    /// <summary>I 4 bucket, in ordine di rilevanza percepita per l'utente gamer.</summary>
    public const string FpsLatency = "FPS & Latenza";
    public const string NetworkPing = "Network & Ping";
    public const string StabilityQoL = "Stabilità & QoL";
    public const string Background = "Sfondo";

    /// <summary>Tutti i bucket noti, in ordine di visualizzazione.</summary>
    public static readonly IReadOnlyList<string> All =
        new[] { FpsLatency, NetworkPing, StabilityQoL, Background };

    /// <summary>Mappa una categoria tecnica KB al bucket UX. Categoria sconosciuta →
    /// Stability/QoL (bucket "catch-all" onesto invece di crash).</summary>
    public static string Bucket(string category) => (category ?? "").ToLowerInvariant() switch
    {
        "power" => FpsLatency,       // power plans, boost, latency
        "gpu" => FpsLatency,          // FPS + input latency
        "scheduler" => FpsLatency,    // CPU scheduling → framepacing
        "input" => FpsLatency,        // mouse polling → input latency
        "network" => NetworkPing,
        "background" => Background,
        "security" => StabilityQoL,   // Vanguard prereqs, VBS trade-offs
        _ => StabilityQoL,
    };
}
