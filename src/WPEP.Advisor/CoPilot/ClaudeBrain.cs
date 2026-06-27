using System.Text;
using System.Text.Json;

namespace WPEP.Advisor.CoPilot;

/// <summary>Cervello cloud via Anthropic Messages API (https://api.anthropic.com/v1/messages).
/// Opzionale, opt-in: scelto dall'utente in Impostazioni / pagina Co-pilota. Quando attivo,
/// la domanda + il catalogo Verdict (id, evidenza, rischio, stato live) vanno a Anthropic.
/// Tutto il resto (grounding, scarto degli id inventati, applicazione tweak) resta uguale a
/// Ollama: l'LLM è solo un interprete sopra il catalogo. Se manca la API key o l'endpoint non
/// risponde, fallisce con un messaggio onesto — mai un crash, mai un consiglio inventato.</summary>
public sealed class ClaudeBrain : ICoPilotBrain
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;

    public ClaudeBrain(string? apiKey, string? model = null, string? endpoint = null)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? "" : apiKey!.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? CoPilotConfig.DefaultClaudeModel : model!;
        _endpoint = (string.IsNullOrWhiteSpace(endpoint) ? CoPilotConfig.AnthropicEndpoint : endpoint!).TrimEnd('/');
    }

    public string Name => $"Claude · {_model} (cloud)";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_apiKey.Length == 0) return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var req = BuildRequest(new
            {
                model = _model,
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "ping" } },
            });
            using var resp = await Http.SendAsync(req, cts.Token);
            // 200 = key + modello + connettività ok. 401/403/404 = problema configurazione (no).
            // 429/529 = transienti del lato Anthropic ma il setup è valido → consideriamo "vivo".
            var code = (int)resp.StatusCode;
            return resp.IsSuccessStatusCode || code == 429 || code == 529;
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
                "Manca l'API key di Anthropic. Impostala nella pagina Co-pilota o con la variabile d'ambiente ANTHROPIC_API_KEY.");

        using var req = BuildRequest(new
        {
            model = _model,
            max_tokens = 2048,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } },
        });
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var hint = (int)resp.StatusCode switch
            {
                401 or 403 => "API key non valida o senza accesso al modello.",
                404 => $"Modello '{_model}' non trovato. Controlla il nome (es. claude-sonnet-4-6).",
                429 => "Rate limit Anthropic raggiunto. Attendi qualche secondo e riprova.",
                529 => "Servizio Anthropic sovraccarico. Riprova tra poco.",
                _ => "Errore Anthropic.",
            };
            throw new InvalidOperationException(
                $"Claude ha risposto {(int)resp.StatusCode}: {hint}");
        }

        using var doc = JsonDocument.Parse(body);
        // Messages API: { content: [{ type: "text", text: "..." }, ...], stop_reason, usage, ... }
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return "";
        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                && block.TryGetProperty("text", out var txt))
            {
                sb.Append(txt.GetString());
            }
        }
        return sb.ToString();
    }

    private HttpRequestMessage BuildRequest(object payload)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/v1/messages");
        req.Headers.Add("x-api-key", _apiKey);
        req.Headers.Add("anthropic-version", CoPilotConfig.AnthropicVersion);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return req;
    }
}
