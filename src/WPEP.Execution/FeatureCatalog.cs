namespace WPEP.Execution;

/// <summary>Maturity of a feature module, shown as a small badge in the Lab.
/// Honesty first: we never label something Stable until it really is.</summary>
public enum FeatureStatus { Stable, Beta, Experimental }

/// <summary>One togglable premium module ("feature flag"). The CATALOG below is the fixed list
/// of what Verdict CAN do; the user's chosen on/off set is stored separately (in the app's
/// settings). Heavy modules (watchdog, sentinel, overlays) ship OFF by default so the app stays
/// clean — Léon's architectural decision (2026-06-18): a Lab page of toggles rather than
/// everything always-on. Pure data, no UI dependency, so the CLI can read it too.</summary>
public sealed record FeatureModule(
    string Id,
    string Name,
    string Tagline,
    string Category,
    bool DefaultEnabled = false,
    FeatureStatus Status = FeatureStatus.Experimental,
    bool Heavy = false,
    string Glyph = "",
    bool Available = true, // false = catalogued but not yet implemented ("In arrivo")
    string Where = "");    // dove appare il modulo una volta acceso (pagina della GUI)

/// <summary>The catalog of premium/élite modules. Order within a category is the display order.
/// Adding a feature here makes it appear in the Lab automatically — the feature's own hooks read
/// the stored on/off flag (id) wherever they plug into the rest of the app.</summary>
public static class FeatureCatalog
{
    public const string Score = "score";
    public const string GhostTweak = "ghost-tweak";
    public const string TimeMachine = "time-machine";
    public const string RegressionSentinel = "regression-sentinel";
    public const string Watchdog = "watchdog";
    public const string OptimizeForGame = "optimize-for-game";
    public const string MultiMonitor = "multi-monitor";
    public const string ExplainStutter = "explain-stutter";
    public const string RiskSlider = "risk-slider";
    public const string ReactionLab = "reaction-lab";
    public const string LatencyLab = "latency-lab";
    public const string NetworkDuel = "network-duel";
    public const string RigDna = "rig-dna";
    public const string AiCopilot = "ai-copilot";
    public const string TrustMode = "trust-mode";
    public const string FreshInstall = "fresh-install";
    public const string EvidenceCommunity = "evidence-community";
    public const string PlaceboMuseum = "placebo-museum";

    public static IReadOnlyList<FeatureModule> All { get; } =
    [
        // ── Identità & misura (la "faccia premium") ─────────────────────────────
        new(Score, "Verdict Score", "Un numero 0–100 dello stato del PC, in homepage.",
            "Identità", DefaultEnabled: true, Status: FeatureStatus.Beta, Glyph: "◆", Where: "Home"),
        new(LatencyLab, "Latency Lab", "Grafico before/after dell'ultimo confronto: dimostra a colpo d'occhio che il tweak funziona.",
            "Misura", Status: FeatureStatus.Beta, Glyph: "📈", Where: "Misura"),
        new(ReactionLab, "Reaction Lab", "Minigioco reflex: misura la TUA latenza umana+sistema (clicca al verde).",
            "Misura", Status: FeatureStatus.Beta, Glyph: "⚡", Where: "Misura"),

        // ── Le idee-firma (uniche, anti-placebo) ────────────────────────────────
        new(GhostTweak, "Ghost Tweak", "A/B alla cieca su te stesso: applica un tweak, misura, poi rivela. Uccide il placebo.",
            "Anti-placebo", Status: FeatureStatus.Experimental, Glyph: "🎭", Where: "Misura"),
        new(PlaceboMuseum, "Placebo Museum", "Galleria dei tweak-mito sfatati con l'evidenza. \"Non ci sono cascato.\"",
            "Anti-placebo", Status: FeatureStatus.Beta, Glyph: "🏛", Where: "Knowledge Base"),
        new(EvidenceCommunity, "Evidence community", "Dati anonimi aggregati: \"ha aiutato il 73% dei rig simili\". Onestà crowd-validata.",
            "Anti-placebo", Status: FeatureStatus.Experimental, Glyph: "🌐", Available: false),

        // ── Intelligenza per-gioco ──────────────────────────────────────────────
        new(OptimizeForGame, "Ottimizza per [gioco]", "Un click: tweak di sistema + impostazioni in-game/NVIDIA ottimali per QUEL titolo.",
            "Per-gioco", DefaultEnabled: true, Status: FeatureStatus.Stable, Glyph: "🎯", Where: "Gioco (pagina)"),
        new(NetworkDuel, "Network Duel", "Ping/jitter/bufferbloat verso i server DEI TUOI giochi, con voto.",
            "Per-gioco", Status: FeatureStatus.Experimental, Glyph: "🛰", Where: "Diagnostica"),

        // ── Hardware su misura per il tuo rig ───────────────────────────────────
        new(MultiMonitor, "Multi-monitor optimizer", "Sceglie il primary giusto, VRR per-display, spegne i monitor inutili per l'input lag.",
            "Hardware", Status: FeatureStatus.Beta, Glyph: "🖥", Where: "Scan"),
        new(ExplainStutter, "Explain my Stutter", "Unisce DPC/ISR + frame data e ti dice QUALE driver causa lo stutter, in italiano semplice.",
            "Hardware", Status: FeatureStatus.Experimental, Glyph: "🔍", Where: "Diagnostica"),
        new(RigDna, "Rig DNA", "Firma generativa unica dal tuo hardware+config: una trading card da collezionare.",
            "Hardware", Status: FeatureStatus.Experimental, Glyph: "🧬", Where: "Scan"),

        // ── Automazione & fiducia (off di default, pesanti) ─────────────────────
        new(Watchdog, "Watchdog (tray)", "Monitor in background: ti avvisa se l'EXPO si spegne, un tweak salta, le temp spikano.",
            "Automazione", Status: FeatureStatus.Experimental, Heavy: true, Glyph: "🛡", Where: "Modifiche"),
        new(RegressionSentinel, "Regression Sentinel", "Ri-benchmarka da solo e ti avvisa se le prestazioni PEGGIORANO (es. un Windows Update).",
            "Automazione", Status: FeatureStatus.Experimental, Heavy: true, Glyph: "📉", Where: "CLI: wpep sentinel"),
        new(TimeMachine, "Time Machine", "Timeline \"cos'è cambiato dal sistema\" + rewind a un punto qualsiasi.",
            "Automazione", Status: FeatureStatus.Beta, Glyph: "⏳", Where: "Scan"),

        // ── Controllo & onestà ──────────────────────────────────────────────────
        new(RiskSlider, "Risk Slider", "Una manopola safe ↔ estremo: Verdict sceglie il set di tweak adatto al tuo rischio.",
            "Controllo", Status: FeatureStatus.Beta, Glyph: "🎚", Where: "Verdict"),
        new(TrustMode, "Trust mode", "Mostra ESATTAMENTE cosa toccherà, diff in stile security-review. Per i paranoici.",
            "Controllo", Status: FeatureStatus.Beta, Glyph: "🔒", Where: "dialog Applica"),
        new(FreshInstall, "Fresh-install score", "Confronta col Windows pulito: \"hai aggiunto 47 processi dall'installazione\".",
            "Controllo", Status: FeatureStatus.Experimental, Glyph: "✨", Where: "Scan"),
        new(AiCopilot, "AI co-pilot", "Linguaggio naturale: \"rendi Valorant più fluido\" → Verdict spiega e propone.",
            "Controllo", Status: FeatureStatus.Experimental, Glyph: "🤖", Available: false),
    ];

    public static FeatureModule? Get(string id) => All.FirstOrDefault(f => f.Id == id);
}
