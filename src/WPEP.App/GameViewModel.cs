using System.Collections.ObjectModel;
using WPEP.Advisor;
using WPEP.Execution;
using WPEP.KnowledgeBase;

namespace WPEP.App;

/// <summary>Pagina "Gioco": Ottimizza-per-gioco di prima classe (promossa dal Lab). Scegli un titolo
/// → Verdict mostra i tweak di sistema con evidenza, applicabili col toggle journal/undo (riusa
/// <see cref="VerdictItem"/>), più le impostazioni in-game/driver da mettere a mano per QUEL gioco.
/// Proiezione read-only sulla KB + stato live dell'ultima scansione; nessuna nuova capacità di
/// scrittura (usa l'engine esistente). Il bottone "Misura l'effetto" porta al wizard Misura.</summary>
public sealed class GameViewModel(MainViewModel main) : ViewModelBase
{
    private IReadOnlyList<TweakEntry> _kbCache = [];
    private string? _selectedGame;

    /// <summary>Ottimizza-per-gioco è on di default (feature promossa). Il modulo Lab resta
    /// disattivabile: se spento, la pagina mostra un invito ad attivarlo. Riletto a ogni nav.</summary>
    public bool IsEnabled => main.Settings.IsFeatureEnabled(FeatureCatalog.OptimizeForGame);

    public ObservableCollection<string> Games { get; } = [];
    /// <summary>Tweak di sistema con evidenza, azionabili col toggle (riusa VerdictItem: journal/undo).</summary>
    public ObservableCollection<VerdictItem> SystemTweaks { get; } = [];
    /// <summary>Impostazioni in-game/driver del titolo: informative (si mettono a mano nel gioco).</summary>
    public ObservableCollection<GameSettingRow> InGameSettings { get; } = [];

    public string? SelectedGame
    {
        get => _selectedGame;
        set { if (Set(ref _selectedGame, value)) { RebuildPlan(); RaiseStateFlags(); } }
    }

    public bool HasSelection => _selectedGame is not null;
    /// <summary>Serve una scansione per lo stato live dei tweak (già attivo / da attivare).</summary>
    public bool NeedsScan => _selectedGame is not null && main.Verdict.AllRecommendations.Count == 0;
    public bool ShowPlan => _selectedGame is not null && main.Verdict.AllRecommendations.Count > 0;
    public bool HasSystemTweaks => SystemTweaks.Count > 0;
    public bool HasInGameSettings => InGameSettings.Count > 0;

    private void RaiseStateFlags()
    {
        Raise(nameof(HasSelection));
        Raise(nameof(NeedsScan));
        Raise(nameof(ShowPlan));
        Raise(nameof(HasSystemTweaks));
        Raise(nameof(HasInGameSettings));
    }

    /// <summary>Popola la lista giochi dalla KB (cache). Sicuro da chiamare a ogni nav; se nel
    /// frattempo è arrivata una scansione, ricostruisce anche il piano del gioco selezionato.</summary>
    public void RefreshGames()
    {
        Raise(nameof(IsEnabled));
        if (!IsEnabled) return;
        if (_kbCache.Count == 0)
            try { _kbCache = KnowledgeBaseLoader.Load(); } catch { return; }
        if (Games.Count == 0)
            foreach (var g in OptimizeForGame.AvailableGames(_kbCache)) Games.Add(g);
        if (_selectedGame is not null) { RebuildPlan(); RaiseStateFlags(); }
    }

    private void RebuildPlan()
    {
        SystemTweaks.Clear();
        InGameSettings.Clear();
        if (_selectedGame is null || _kbCache.Count == 0) return;

        var plan = OptimizeForGame.Build(_selectedGame, _kbCache, main.Verdict.Snapshot);

        // Stato live dai risultati dell'ultima scansione: per ogni tweak di sistema del piano che lo
        // scan conosce, mostriamo una riga azionabile (toggle journal/undo). Senza scan → lista vuota
        // e prompt "scansiona prima" (NeedsScan).
        var byId = main.Verdict.AllRecommendations
            .GroupBy(r => r.Entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var t in plan.SystemTweaks)
            if (byId.TryGetValue(t.Id, out var r))
                SystemTweaks.Add(new VerdictItem(
                    r.Entry, r.StateNote, main, r.Classification == Classification.AlreadyActive));

        foreach (var s in plan.InGameSettings)
            InGameSettings.Add(new GameSettingRow(s.Name, s.ExpectedImpact));
    }

    /// <summary>"Misura l'effetto": porta alla pagina Misura. Non c'è una mappa gioco→exe affidabile,
    /// quindi il processo lo sceglie l'utente nel wizard.</summary>
    public RelayCommand MeasureCommand => new(
        () => main.CurrentPage = main.Measure,
        () => _selectedGame is not null);
}
