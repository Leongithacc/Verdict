using WPEP.KnowledgeBase;

namespace WPEP.Advisor.CoPilot;

// ── V6 — AI CO-PILOT ────────────────────────────────────────────────────────
// Linguaggio naturale ("rendi Valorant più fluido") → Verdict spiega e propone.
// PRINCIPIO DI INTEGRITÀ: l'LLM NON è una fonte di consigli. È un interprete in
// linguaggio naturale SOPRA la KB verificata di Verdict. Non può consigliare nulla
// fuori dal catalogo perché gli id inventati vengono SCARTATI nel codice (non solo
// "chiesto gentilmente" nel prompt). Resta read-only: spiega e propone, mai applica.

/// <summary>Un riferimento, VALIDATO contro il catalogo reale, a un tweak di Verdict.</summary>
public sealed record CoPilotSuggestion(string TweakId, string Name, Classification Classification, string Impact);

/// <summary>La risposta del co-pilota: spiegazione in lingua naturale + i tweak
/// (reali, validati) a cui si riferisce. <see cref="Error"/> != null se il cervello
/// LLM non era raggiungibile — il chiamante mostra il messaggio, niente crash.</summary>
public sealed record CoPilotReply(
    string Answer,
    IReadOnlyList<CoPilotSuggestion> Suggestions,
    string? Error)
{
    public static CoPilotReply Failed(string error) => new("", [], error);
}

/// <summary>Il "cervello": una semplice text-completion. Swappable — locale (Ollama,
/// gratis+privato, default) o cloud (Claude API) — senza toccare il grounding/parsing.</summary>
public interface ICoPilotBrain
{
    string Name { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

/// <summary>Façade brain-agnostica: prende una domanda + il catalogo reale (gli stessi
/// Recommendation dello scan), costruisce il grounding, chiama il cervello, e VALIDA la
/// risposta contro il catalogo. Non lancia mai: un errore diventa <see cref="CoPilotReply.Failed"/>.</summary>
public sealed class CoPilotService(ICoPilotBrain brain)
{
    public string BrainName => brain.Name;
    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => brain.IsAvailableAsync(ct);

    public async Task<CoPilotReply> AskAsync(
        string question, IReadOnlyList<Recommendation> catalog, CancellationToken ct = default)
    {
        try
        {
            var user = $"Domanda dell'utente: {question.Trim()}\n\n{CoPilotGrounding.BuildCatalog(catalog)}";
            var raw = await brain.CompleteAsync(CoPilotGrounding.SystemPrompt, user, ct);
            return CoPilotGrounding.ParseReply(raw, catalog);
        }
        catch (Exception ex)
        {
            return CoPilotReply.Failed($"Co-pilota non disponibile: {ex.Message}");
        }
    }
}

/// <summary>La parte pura e testabile: il contratto di onestà (system prompt), come
/// si presenta il catalogo all'LLM, e come si validano gli id che restituisce.</summary>
public static class CoPilotGrounding
{
    /// <summary>Il contratto: l'LLM può SOLO interpretare e citare il catalogo di Verdict.</summary>
    public const string SystemPrompt =
        """
        Sei il co-pilota di Verdict, uno strumento di ottimizzazione gaming ONESTO e anti-placebo.
        REGOLE FERREE (non derogabili):
        1. Consiglia SOLO tweak presenti nel CATALOGO qui sotto, citandone l'id ESATTO. Non inventare
           MAI chiavi di registro, comandi o tweak che non sono in catalogo. Se non esiste una voce
           adatta alla richiesta, dillo chiaramente invece di inventare.
        2. Rispetta l'evidenza. Se un tweak è 'placebo', dì che è un placebo e NON proporlo come
           miglioria. Se è 'risky', avvisa del rischio. Non promettere mai FPS o risultati garantiti.
        3. Usa lo STATO dell'utente: se un tweak risulta 'già attivo', dillo e non riproporlo.
        4. Non applichi nulla. Spieghi e proponi; sarà l'utente a premere "Applica" dentro Verdict.
        5. Rispondi in ITALIANO, breve e diretto, senza fronzoli.
        Alla FINE della risposta elenca gli id rilevanti, separati da virgola, su una riga che inizia
        ESATTAMENTE con:  TWEAKS:
        Esempio ultima riga →  TWEAKS: fullscreen-exclusive, nvidia-low-latency-ultra
        """;

    /// <summary>Compila i Recommendation reali in un catalogo compatto che l'LLM DEVE usare.
    /// Include id, evidenza, rischio e lo stato live per QUESTO pc (così il modello sa cosa è
    /// già a posto e cosa è placebo). Esclude le voci NotApplicable (prerequisiti non soddisfatti).</summary>
    public static string BuildCatalog(IReadOnlyList<Recommendation> catalog)
    {
        var lines = catalog
            .Where(r => r.Classification != Classification.NotApplicable)
            .Select(r =>
            {
                var e = r.Entry;
                var game = e.Game is null ? "" : $" · gioco:{e.Game}";
                return $"- [{e.Id}] {e.Name} — {e.Category} · evidenza:{Evidence(e.EvidenceLevel)} · "
                     + $"rischio:{Risk(e.Risk)} · stato:{State(r.Classification)}{game} · effetto: {e.ExpectedImpact}";
            });
        return "CATALOGO VERDICT (usa SOLO questi id; non inventarne altri):\n" + string.Join('\n', lines);
    }

    /// <summary>Estrae gli id citati dall'LLM e li VALIDA contro il catalogo: ogni id non
    /// presente viene scartato (anti-hallucination a livello di codice). Toglie la riga TWEAKS:
    /// dal testo mostrato. Mantiene l'ordine di citazione, dedup case-insensitive.</summary>
    public static CoPilotReply ParseReply(string raw, IReadOnlyList<Recommendation> catalog)
    {
        raw ??= "";
        var byId = new Dictionary<string, Recommendation>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in catalog)
            byId[r.Entry.Id] = r;

        var cited = new List<string>();
        var answerLines = new List<string>();
        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("TWEAKS:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var part in trimmed["TWEAKS:".Length..].Split(',', ';'))
                {
                    var id = part.Trim().Trim('`', '"', '\'', '.', '[', ']', ' ');
                    if (id.Length > 0)
                        cited.Add(id);
                }
            }
            else
            {
                answerLines.Add(line);
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = new List<CoPilotSuggestion>();
        foreach (var id in cited)
        {
            if (byId.TryGetValue(id, out var rec) && seen.Add(rec.Entry.Id))
                suggestions.Add(new CoPilotSuggestion(
                    rec.Entry.Id, rec.Entry.Name, rec.Classification, rec.Entry.ExpectedImpact));
        }

        return new CoPilotReply(string.Join('\n', answerLines).Trim(), suggestions, null);
    }

    private static string Evidence(EvidenceLevel e) => e switch
    {
        EvidenceLevel.EvidenceStrong => "forte",
        EvidenceLevel.Plausible => "plausibile",
        EvidenceLevel.Controversial => "controverso",
        EvidenceLevel.Placebo => "PLACEBO",
        EvidenceLevel.Risky => "RISCHIOSO",
        _ => "?",
    };

    private static string Risk(RiskLevel r) => r switch
    {
        RiskLevel.None => "nessuno",
        RiskLevel.Low => "basso",
        RiskLevel.Medium => "medio",
        RiskLevel.High => "alto",
        _ => "?",
    };

    private static string State(Classification c) => c switch
    {
        Classification.Recommended => "consigliato",
        Classification.Optional => "opzionale",
        Classification.OptionalWithWarning => "opzionale-con-avviso",
        Classification.Placebo => "placebo",
        Classification.NotRecommended => "sconsigliato-rischioso",
        Classification.AlreadyActive => "GIÀ-ATTIVO",
        _ => "?",
    };
}
