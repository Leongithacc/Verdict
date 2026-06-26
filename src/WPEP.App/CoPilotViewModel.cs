using System.Collections.ObjectModel;
using WPEP.Advisor;
using WPEP.Advisor.CoPilot;
using WPEP.KnowledgeBase;

namespace WPEP.App;

/// <summary>V6 — il co-pilota AI in linguaggio naturale. Interpreta la domanda e propone
/// SOLO tweak del catalogo verificato di Verdict (l'engine scarta gli id inventati), poi
/// l'utente sceglie se applicarli col solito flusso sicuro. Non applica mai da solo.
/// Cervello = Ollama locale (gratis + privato): nessun dato lascia il PC.</summary>
public sealed class CoPilotViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public CoPilotViewModel(MainViewModel main) => _main = main;

    /// <summary>Modello Ollama, persistito in Impostazioni. Vuoto = default (qwen2.5).
    /// Léon ha "qwen2.5vl:32b": lo imposta qui e la GUI usa quello.</summary>
    public string Model
    {
        get => _main.Settings.CoPilotModel;
        set { _main.Settings.CoPilotModel = value?.Trim() ?? ""; _main.Settings.Save(); Raise(); }
    }

    private CoPilotService BuildService()
    {
        var model = string.IsNullOrWhiteSpace(Model) ? null : Model;
        return new CoPilotService(new OllamaBrain(model));
    }

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

    private string _status =
        "Chiedi in parole tue (es. \"rendi Valorant più fluido\"). Il co-pilota cita solo tweak " +
        "verificati di Verdict e non applica nulla da solo. Cervello: Ollama locale, i dati restano sul PC.";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private bool _busy;
    public bool IsBusy
    {
        get => _busy;
        private set { Set(ref _busy, value); Raise(nameof(CanAsk)); }
    }

    public bool CanAsk => !_busy && _question.Trim().Length > 0;

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
                Status = $"{service.BrainName} non raggiungibile. Avvia Ollama (\"ollama serve\") e installa "
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
        // Reuse the exact same ON/OFF toggle as the Verdict list when applicable.
        Toggle = CanApply
            ? new VerdictItem(entry!, Impact, main, s.Classification == Classification.AlreadyActive)
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
