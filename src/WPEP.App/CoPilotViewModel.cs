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

    /// <summary>True quando l'utente ha già configurato una API key Anthropic (non la espone in chiaro).
    /// Usato dall'UI per mostrare "configurata" o un placeholder vuoto sulla PasswordBox.</summary>
    public bool HasClaudeApiKey => _main.Settings.ClaudeApiKey.Length > 0;

    /// <summary>Chiamato dal code-behind quando l'utente digita nella PasswordBox della pagina
    /// (PasswordBox non bind-abile in MVVM puro). Salva subito, cifrato at-rest.</summary>
    public void SetClaudeApiKey(string? apiKey)
    {
        _main.Settings.ClaudeApiKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;
        _main.Settings.Save();
        Raise(nameof(HasClaudeApiKey));
        Raise(nameof(CanAsk));
    }

    /// <summary>Cervello attivo: true se Ollama (default). Cambiare via <see cref="IsClaude"/>.
    /// L'UI usa due RadioButton bindati a IsOllama/IsClaude (group radio implicito).</summary>
    public bool IsOllama
    {
        get => !string.Equals(_main.Settings.CoPilotBrain, "claude", StringComparison.OrdinalIgnoreCase);
        set { if (value) SetBrain("ollama"); }
    }

    public bool IsClaude
    {
        get => string.Equals(_main.Settings.CoPilotBrain, "claude", StringComparison.OrdinalIgnoreCase);
        set { if (value) SetBrain("claude"); }
    }

    private void SetBrain(string brain)
    {
        if (string.Equals(_main.Settings.CoPilotBrain, brain, StringComparison.OrdinalIgnoreCase))
            return;
        _main.Settings.CoPilotBrain = brain;
        _main.Settings.Save();
        Raise(nameof(IsOllama));
        Raise(nameof(IsClaude));
        Raise(nameof(CanAsk));
        // Status iniziale aggiornato in base al nuovo brain.
        Status = DefaultStatus();
    }

    private CoPilotService BuildService()
    {
        if (IsClaude)
        {
            var cModel = string.IsNullOrWhiteSpace(ClaudeModel) ? null : ClaudeModel;
            return new CoPilotService(new ClaudeBrain(_main.Settings.ClaudeApiKey, cModel));
        }
        var oModel = string.IsNullOrWhiteSpace(Model) ? null : Model;
        return new CoPilotService(new OllamaBrain(oModel));
    }

    private string DefaultStatus() => IsClaude
        ? "Chiedi in parole tue. Cervello: Claude (cloud). La domanda + il catalogo di Verdict "
          + "vengono inviati a Anthropic; la chiave è cifrata at-rest sul tuo PC."
        : "Chiedi in parole tue (es. \"rendi Valorant più fluido\"). Il co-pilota cita solo tweak "
          + "verificati di Verdict e non applica nulla da solo. Cervello: Ollama locale, i dati restano sul PC.";

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

    public bool CanAsk => !_busy && _question.Trim().Length > 0 && (IsOllama || HasClaudeApiKey);

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
                Status = IsClaude
                    ? $"{service.BrainName} non raggiungibile. Verifica la API key Anthropic e la connessione internet."
                    : $"{service.BrainName} non raggiungibile. Avvia Ollama (\"ollama serve\") e installa "
                      + "un modello, poi riprova. (Resta tutto locale.)";
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
