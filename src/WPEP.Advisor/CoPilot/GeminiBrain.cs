using System.Text;
using System.Text.Json;

namespace WPEP.Advisor.CoPilot;

/// <summary>Cervello cloud via Google Gemini API (generateContent).
/// Opzionale, opt-in: scelto dall'utente in Impostazioni / pagina Co-pilota.
/// Quando attivo, la domanda + il catalogo Verdict vanno a Google.
/// Tutto il resto (grounding, scarto degli id inventati, applicazione tweak) resta
/// uguale a Ollama/Claude: l'LLM è solo un interprete sopra il catalogo.</summary>
public sealed class GeminiBrain : ICoPilotBrain
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    public GeminiBrain(string? apiKey, string? model = null, string? endpoint = null)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? "" : apiKey!.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? CoPilotConfig.DefaultGeminiModel : model!;
        _endpoint = (string.IsNullOrWhiteSpace(endpoint) ? CoPilotConfig.GeminiEndpoint : endpoint!).TrimEnd('/');
    }

    public string Name => $"Gemini · {_model} (cloud)";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_apiKey.Length == 0) return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            // Probe con call minimale: 1 token, prompt vuoto. 401/403 = chiave invalida.
            using var req = BuildRequest(new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = "ping" } } } },
                generationConfig = new { maxOutputTokens = 1 },
            });
            using var resp = await Http.SendAsync(req, cts.Token);
            var code = (int)resp.StatusCode;
            return resp.IsSuccessStatusCode || code == 429 || code == 503;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (_apiKey.Length == 0)
            throw new InvalidOperationException(
                "Manca l'API key di Google AI Studio. Impostala nella pagina Co-pilota o con la variabile d'ambiente GEMINI_API_KEY.");

        using var req = BuildRequest(new
        {
            // systemInstruction: il system prompt va nel campo dedicato (Gemini API distingue).
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new { maxOutputTokens = 2048, temperature = 0.3 },
        });
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var hint = (int)resp.StatusCode switch
            {
                400 => "Richiesta malformata (modello inesistente o quota esaurita).",
                401 or 403 => "API key non valida o senza accesso al modello.",
                404 => $"Modello '{_model}' non trovato.",
                429 => "Rate limit Google superato. Attendi e riprova.",
                503 => "Servizio Gemini sovraccarico.",
                _ => "Errore Google AI.",
            };
            throw new InvalidOperationException($"Gemini ha risposto {(int)resp.StatusCode}: {hint}");
        }

        using var doc = JsonDocument.Parse(body);
        // generateContent: { candidates: [{ content: { parts: [{ text: "..." }] } }] }
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
            return "";
        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content)) return "";
        if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return "";
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var txt))
                sb.Append(txt.GetString());
        }
        return sb.ToString();
    }

    private HttpRequestMessage BuildRequest(object payload)
    {
        var url = $"{_endpoint}/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        // Header x-goog-api-key invece di query string ?key=: evita la key nei log degli accessi.
        req.Headers.Add("x-goog-api-key", _apiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return req;
    }
}
