using System.Collections.ObjectModel;
using WPEP.Advisor;
using WPEP.Advisor.CoPilot;
using WPEP.KnowledgeBase;

namespace WPEP.App;

/// <summary>V6 — il co-pilota AI in linguaggio naturale. Interpreta la domanda e propone
/// SOLO tweak del catalogo verificato di Verdict (l'engine scarta gli id inventati), poi
/// l'utente sceglie se applicarli col solito flusso sicuro. Non applica mai da solo.
/// Cervello swappable: Ollama locale (gratis + privato, default) o Claude cloud (qualità
/// superiore, opt-in via API key cifrata DPAPI). La scelta è in <see cref="AppSettings"/>.</summary>
public sealed class CoPilotViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public CoPilotViewModel(MainViewModel main)
    {
        _main = main;
        _status = DefaultStatus();
    }

    /// <summary>Modello Ollama, persistito in Impostazioni. Vuoto = default (qwen2.5).
    /// Léon ha "qwen2.5vl:32b": lo imposta qui e la GUI usa quello.</summary>
    public string Model
    {
        get => _main.Settings.CoPilotModel;
        set { _main.Settings.CoPilotModel = value?.Trim() ?? ""; _main.Settings.Save(); Raise(); }
    }

    /// <summary>Modello Claude per il co-pilota cloud, persistito. Vuoto = default
    /// (claude-sonnet-4-6). Editabile per puntare a Opus 4.8 (qualità+, costo+) o Haiku 4.5 (costo-).</summary>
    public string ClaudeModel
    {
        get => _main.Settings.ClaudeModel;
        set { _main.Settings.ClaudeModel = value?.Trim() ?? ""; _main.Settings.Save(); Raise(); }
    }

    /// <summary>Modello Gemini per il co-pilota cloud, persistito. Vuoto = default (gemini-2.5-pro).</summary>
    public string GeminiModel
    {
        get => _main.Settings.GeminiModel;
        set { _main.Settings.GeminiModel = value?.Trim() ?? ""; _main.Settings.Save(); Raise(); }
    }

    /// <summary>Modello OpenAI per il co-pilota cloud, persistito. Vuoto = default (gpt-5).</summary>
    public string OpenAiModel
    {
        get => _main.Settings.OpenAiModel;
        set { _main.Settings.OpenAiModel = value?.Trim() ?? ""; _main.Settings.Save(); Raise(); }
    }

    /// <summary>True quando l'utente ha già configurato una API key (non la espone in chiaro).
    /// Usate dall'UI per mostrare "✓ configurata" o un placeholder vuoto sulla PasswordBox.</summary>
    public bool HasClaudeApiKey => _main.Settings.ClaudeApiKey.Length > 0;
    public bool HasGeminiApiKey => _main.Settings.GeminiApiKey.Length > 0;
    public bool HasOpenAiApiKey => _main.Settings.OpenAiApiKey.Length > 0;

    /// <summary>Chiamati dal code-behind quando l'utente digita nelle PasswordBox della pagina
    /// (PasswordBox non bind-abile in MVVM puro). Salvano subito, cifrate at-rest.</summary>
    public void SetClaudeApiKey(string? apiKey)
    {
        _main.Settings.ClaudeApiKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;
        _main.Settings.Save();
        Raise(nameof(HasClaudeApiKey));
        Raise(nameof(CanAsk));
    }
    public void SetGeminiApiKey(string? apiKey)
    {
        _main.Settings.GeminiApiKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;
        _main.Settings.Save();
        Raise(nameof(HasGeminiApiKey));
        Raise(nameof(CanAsk));
    }
    public void SetOpenAiApiKey(string? apiKey)
    {
        _main.Settings.OpenAiApiKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;
        _main.Settings.Save();
        Raise(nameof(HasOpenAiApiKey));
        Raise(nameof(CanAsk));
    }

    /// <summary>Cervello attivo: enum a 4 valori ("ollama"|"claude"|"gemini"|"openai") persistito
    /// in Settings.CoPilotBrain. L'UI usa 4 RadioButton (group radio "copilotBrain") bindati alle
    /// 4 property IsOllama/IsClaude/IsGemini/IsOpenAi (TwoWay).</summary>
    public bool IsOllama
    {
        get => Brain == "ollama";
        set { if (value) SetBrain("ollama"); }
    }
    public bool IsClaude
    {
        get => Brain == "claude";
        set { if (value) SetBrain("claude"); }
    }
    public bool IsGemini
    {
        get => Brain == "gemini";
        set { if (value) SetBrain("gemini"); }
    }
    public bool IsOpenAi
    {
        get => Brain == "openai";
        set { if (value) SetBrain("openai"); }
    }

    private string Brain
    {
        get
        {
            var b = (_main.Settings.CoPilotBrain ?? "ollama").ToLowerInvariant();
            return b is "claude" or "gemini" or "openai" ? b : "ollama";
        }
    }

    private void SetBrain(string brain)
    {
        if (string.Equals(_main.Settings.CoPilotBrain, brain, StringComparison.OrdinalIgnoreCase))
            return;
        _main.Settings.CoPilotBrain = brain;
        _main.Settings.Save();
        Raise(nameof(IsOllama));
        Raise(nameof(IsClaude));
        Raise(nameof(IsGemini));
        Raise(nameof(IsOpenAi));
        Raise(nameof(CanAsk));
        // Status iniziale aggiornato in base al nuovo brain.
        Status = DefaultStatus();
    }

    private CoPilotService BuildService()
    {
        switch (Brain)
        {
            case "claude":
                return new CoPilotService(new ClaudeBrain(
                    _main.Settings.ClaudeApiKey,
                    string.IsNullOrWhiteSpace(ClaudeModel) ? null : ClaudeModel));
            case "gemini":
                return new CoPilotService(new GeminiBrain(
                    _main.Settings.GeminiApiKey,
                    string.IsNullOrWhiteSpace(GeminiModel) ? null : GeminiModel));
            case "openai":
                return new CoPilotService(new OpenAiBrain(
                    _main.Settings.OpenAiApiKey,
                    string.IsNullOrWhiteSpace(OpenAiModel) ? null : OpenAiModel));
            default:
                return new CoPilotService(new OllamaBrain(
                    string.IsNullOrWhiteSpace(Model) ? null : Model));
        }
    }

    private string DefaultStatus() => Brain switch
    {
        "claude" => "Chiedi in parole tue. Cervello: Claude (cloud Anthropic). La domanda + il "
                  + "catalogo Verdict viaggiano cifrati in TLS; la API key è cifrata at-rest sul tuo PC.",
        "gemini" => "Chiedi in parole tue. Cervello: Gemini (cloud Google). Stesso flusso del co-pilota "
                  + "Claude: TLS in transito, key DPAPI a riposo.",
        "openai" => "Chiedi in parole tue. Cervello: GPT (cloud OpenAI). Stesso flusso degli altri "
                  + "brain cloud: TLS in transito, key DPAPI a riposo.",
        _ => "Chiedi in parole tue (es. \"rendi Valorant più fluido\"). Il co-pilota cita solo tweak "
           + "verificati di Verdict e non applica nulla da solo. Cervello: Ollama locale, i dati restano sul PC.",
    };

    private bool HasActiveCloudKey => Brain switch
    {
        "claude" => HasClaudeApiKey,
        "gemini" => HasGeminiApiKey,
        "openai" => HasOpenAiApiKey,
        _ => true, // Ollama non richiede key
    };

    private string _question = "";
    public string Question
    {
        get => _question;
        set { Set(ref _question, value); Raise(nameof(CanAsk)); }
    }

    private string _answer = "";
    public string Answer
    {
        get => _answer;
        private set { Set(ref _answer, value); Raise(nameof(HasAnswer)); }
    }
    public bool HasAnswer => _answer.Length > 0;

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private bool _busy;
    public bool IsBusy
    {
        get => _busy;
        private set { Set(ref _busy, value); Raise(nameof(CanAsk)); }
    }

    public bool CanAsk => !_busy && _question.Trim().Length > 0 && HasActiveCloudKey;

    public ObservableCollection<CoPilotSuggestionVM> Suggestions { get; } = [];

    public RelayCommand AskCommand => new(async () => await Ask());

    private async Task Ask()
    {
        if (!CanAsk)
            return;
        IsBusy = true;
        Answer = "";
        Suggestions.Clear();
        Status = "Sto pensando…";
        try
        {
            var catalog = _main.Verdict.AllRecommendations;
            if (catalog.Count == 0)
            {
                Status = "Fai prima una scansione (pagina Verdict) così posso vedere il tuo PC.";
                return;
            }
            var service = BuildService();
            if (!await service.IsAvailableAsync())
            {
                Status = IsOllama
                    ? $"{service.BrainName} non raggiungibile. Avvia Ollama (\"ollama serve\") e installa "
                      + "un modello, poi riprova. (Resta tutto locale.)"
                    : $"{service.BrainName} non raggiungibile. Verifica la API key e la connessione internet.";
                return;
            }

            var reply = await service.AskAsync(_question, catalog);
            if (reply.Error is not null)
            {
                Status = reply.Error;
                return;
            }

            Answer = reply.Answer;
            var byId = new Dictionary<string, TweakEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in catalog)
                byId[r.Entry.Id] = r.Entry;
            foreach (var s in reply.Suggestions)
                Suggestions.Add(new CoPilotSuggestionVM(s, byId.GetValueOrDefault(s.TweakId), _main));

            Status = Suggestions.Count > 0
                ? ""
                : "Nessun tweak verificato pertinente — il co-pilota non inventa consigli.";
        }
        catch (Exception ex)
        {
            Status = $"Errore: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Una proposta del co-pilota, già validata contro il catalogo: si applica col
/// solito flusso (anteprima → consenso → journal → undo) o si apre la voce KB per capire.</summary>
public sealed class CoPilotSuggestionVM
{
    private readonly MainViewModel _main;
    private readonly TweakEntry? _entry;

    public CoPilotSuggestionVM(CoPilotSuggestion s, TweakEntry? entry, MainViewModel main)
    {
        _main = main;
        _entry = entry;
        Id = s.TweakId;
        Name = s.Name;
        Impact = s.Impact;
        State = StateLabel(s.Classification);
        CanApply = entry is not null && main.Execution.CanApply(entry);
        // Reuse the exact same row VM as the Verdict list: it carries the ON/OFF toggle when
        // applicable AND the per-tweak BIOS QR when it's a manual BIOS tweak. Built for any real
        // entry (not only applicable ones) so a manual BIOS suggestion also gets its QR.
        Toggle = entry is not null
            ? new VerdictItem(entry, Impact, main, s.Classification == Classification.AlreadyActive)
            : null;
    }

    public string Id { get; }
    public string Name { get; }
    public string Impact { get; }
    public string State { get; }
    public bool CanApply { get; }

    /// <summary>The ON/OFF toggle (same as the Verdict list) when this suggestion is applicable; else null.</summary>
    public VerdictItem? Toggle { get; }

    public RelayCommand HowToCommand => new(() => _main.ShowKbEntry(Id));

    private static string StateLabel(Classification c) => c switch
    {
        Classification.Recommended => "consigliato",
        Classification.Optional => "opzionale",
        Classification.OptionalWithWarning => "opzionale · attenzione",
        Classification.Placebo => "placebo",
        Classification.NotRecommended => "rischioso",
        Classification.AlreadyActive => "già attivo",
        _ => "",
    };
}
