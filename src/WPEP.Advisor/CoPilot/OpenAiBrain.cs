using System.Text;
using System.Text.Json;

namespace WPEP.Advisor.CoPilot;

/// <summary>Cervello cloud via OpenAI Chat Completions API.
/// Opzionale, opt-in. Stesso pattern di ClaudeBrain/GeminiBrain: l'LLM è solo un
/// interprete sopra il catalogo locale di Verdict; gli id inventati vengono scartati
/// nel codice (CoPilotGrounding.ParseReply).</summary>
public sealed class OpenAiBrain : ICoPilotBrain
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    public OpenAiBrain(string? apiKey, string? model = null, string? endpoint = null)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? "" : apiKey!.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? CoPilotConfig.DefaultOpenAiModel : model!;
        _endpoint = (string.IsNullOrWhiteSpace(endpoint) ? CoPilotConfig.OpenAiEndpoint : endpoint!).TrimEnd('/');
    }

    public string Name => $"OpenAI · {_model} (cloud)";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_apiKey.Length == 0) return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            // Probe: chat completion minimale con 1 token. 401 = key invalida.
            using var req = BuildRequest(new
            {
                model = _model,
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "ping" } },
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
                "Manca l'API key di OpenAI. Impostala nella pagina Co-pilota o con la variabile d'ambiente OPENAI_API_KEY.");

        using var req = BuildRequest(new
        {
            model = _model,
            max_tokens = 2048,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        });
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var hint = (int)resp.StatusCode switch
            {
                400 => "Richiesta malformata (modello inesistente o parametri non validi).",
                401 => "API key non valida.",
                403 => "API key senza accesso al modello.",
                404 => $"Modello '{_model}' non trovato.",
                429 => "Rate limit / quota OpenAI superata.",
                503 => "Servizio OpenAI sovraccarico.",
                _ => "Errore OpenAI.",
            };
            throw new InvalidOperationException($"OpenAI ha risposto {(int)resp.StatusCode}: {hint}");
        }

        using var doc = JsonDocument.Parse(body);
        // Chat Completions: { choices: [{ message: { role, content: "..." } }] }
        if (!doc.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
            return "";
        var first = choices[0];
        if (!first.TryGetProperty("message", out var message)) return "";
        if (!message.TryGetProperty("content", out var content)) return "";
        return content.GetString() ?? "";
    }

    private HttpRequestMessage BuildRequest(object payload)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/v1/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {_apiKey}");
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return req;
    }
}
