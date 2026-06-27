using System.Text;
using System.Text.Json;

namespace WPEP.Advisor.CoPilot;

/// <summary>Default config per i due cervelli del co-pilota.
/// LOCALE (Ollama) = gratis + privato, default. CLOUD (Claude) = opzionale, qualità superiore
/// ma manda la domanda + il catalogo a Anthropic. La scelta sta in <see cref="AppSettings"/>.</summary>
public static class CoPilotConfig
{
    // ── Ollama (locale, default) ────────────────────────────────────────────
    public const string OllamaEndpoint = "http://localhost:11434";
    // Modello text di Ollama. Léon ha qwen sul PC; se questo non è "pullato" il co-pilota
    // lo dice con grazia (errore visibile, niente crash) e basta `ollama pull <modello>`.
    public const string DefaultModel = "qwen2.5";

    // ── Anthropic / Claude (cloud, opt-in) ──────────────────────────────────
    public const string AnthropicEndpoint = "https://api.anthropic.com";
    // Versione API stabile (Messages). Da bumpare se Anthropic la avanza.
    public const string AnthropicVersion = "2023-06-01";
    // Default = Sonnet 4.6 (bilanciato qualità/costo per testo strutturato e breve).
    // L'utente può puntare a Opus 4.8 (più costoso) o Haiku 4.5 (più economico) da settings.
    public const string DefaultClaudeModel = "claude-sonnet-4-6";
}

/// <summary>Cervello locale via Ollama (http://localhost:11434). Nessun dato lascia il PC.
/// Se Ollama non gira o il modello manca, fallisce con un messaggio onesto, mai un crash.</summary>
public sealed class OllamaBrain : ICoPilotBrain
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly string _endpoint;
    private readonly string _model;

    public OllamaBrain(string? model = null, string? endpoint = null)
    {
        _model = string.IsNullOrWhiteSpace(model) ? CoPilotConfig.DefaultModel : model!;
        _endpoint = (string.IsNullOrWhiteSpace(endpoint) ? CoPilotConfig.OllamaEndpoint : endpoint!).TrimEnd('/');
    }

    public string Name => $"Ollama · {_model} (locale)";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await Http.GetAsync($"{_endpoint}/api/tags", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;   // Ollama non in esecuzione / non raggiungibile
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.PostAsync($"{_endpoint}/api/chat", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Ollama ha risposto {(int)resp.StatusCode}. Il modello '{_model}' è installato? (ollama pull {_model})");

        using var doc = JsonDocument.Parse(body);
        // /api/chat → { "message": { "role": "assistant", "content": "..." }, ... }
        if (doc.RootElement.TryGetProperty("message", out var msg)
            && msg.TryGetProperty("content", out var c))
            return c.GetString() ?? "";
        // Difensivo: alcuni endpoint/modelli usano /api/generate → { "response": "..." }
        if (doc.RootElement.TryGetProperty("response", out var r))
            return r.GetString() ?? "";
        return "";
    }
}
