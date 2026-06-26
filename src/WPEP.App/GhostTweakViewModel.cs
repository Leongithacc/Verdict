using WPEP.Execution;
using WPEP.KnowledgeBase;
using WPEP.Statistics;

namespace WPEP.App;

/// <summary>Ghost Tweak (Lab feature): the blind A/B on yourself. Verdict applies a hidden tweak
/// for real (journaled), you measure with the wizard, then Reveal names it and tells the honest
/// truth — mapping the statistical verdict to the plain reveal. Always undoes on reveal, so the
/// system is left exactly as it was. No-admin candidates only, so a blind round never needs UAC.</summary>
public sealed class GhostTweakViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private TweakEntry? _hidden;
    private string? _journalFile;
    private string _status = "Pronto. Verdict applicherà un tweak a caso senza dirti quale.";
    private bool _isApplied;
    private string _revealTitle = "", _revealPlain = "", _revealColor = "Ok";
    private bool _isRevealed;

    public GhostTweakViewModel(MainViewModel main) => _main = main;

    public bool ShowGhostTweak => _main.Settings.IsFeatureEnabled(FeatureCatalog.GhostTweak);
    /// <summary>Re-raises the visibility flag so a toggle made in the Lab takes effect on nav.</summary>
    public void RefreshFlag() => Raise(nameof(ShowGhostTweak));
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool IsApplied { get => _isApplied; set { Set(ref _isApplied, value); Raise(nameof(CanStart)); } }
    public bool IsRevealed { get => _isRevealed; set => Set(ref _isRevealed, value); }
    public bool CanStart => !IsApplied;
    public string RevealTitle { get => _revealTitle; set => Set(ref _revealTitle, value); }
    public string RevealPlain { get => _revealPlain; set => Set(ref _revealPlain, value); }
    public string RevealColor { get => _revealColor; set => Set(ref _revealColor, value); }

    public RelayCommand StartRoundCommand => new(StartRound, () => CanStart);
    public RelayCommand RevealCommand => new(Reveal, () => IsApplied);

    /// <summary>Picks a hidden no-admin tweak and applies it for real, blind.</summary>
    private void StartRound()
    {
        IsRevealed = false;
        RevealTitle = RevealPlain = "";
        // Candidates: applicable, non-placebo, and no-admin so the round never needs elevation.
        var candidates = KnowledgeBaseLoader.Load()
            .Where(e => ApplyPolicy.CanApply(e) && !ApplyPolicy.NeedsAdmin(e))
            .ToList();
        if (candidates.Count == 0)
        {
            Status = "Nessun tweak adatto a un round cieco senza admin. Rilancia come amministratore per più scelta.";
            return;
        }

        // Try a few blind picks until one actually changes something (not already applied).
        var ids = candidates.Select(c => c.Id).ToList();
        for (int attempt = 0; attempt < ids.Count; attempt++)
        {
            string id = GhostTweak.Pick(ids, System.Random.Shared.Next());
            var entry = candidates.First(c => c.Id == id);
            var plan = _main.Execution.BuildPlan(entry);
            if (plan.IsAlreadyApplied) { ids.Remove(id); if (ids.Count == 0) break; continue; }

            try
            {
                _main.Execution.Execute(plan);
                _journalFile = _main.Execution.Sessions().LastOrDefault();
                _hidden = entry;
                IsApplied = true;
                Status = "🎭 Un tweak misterioso è ATTIVO. Vai a misurare con lo strumento Measure " +
                         "(baseline prima d'ora idealmente), poi torna e premi Rivela. Verdict lo annullerà comunque.";
                _main.Changes.Refresh();
                return;
            }
            catch (Exception ex)
            {
                Status = $"Round non avviato: {ex.Message}";
                return;
            }
        }
        Status = "Tutti i candidati risultano già applicati: niente da testare alla cieca adesso. " +
                 "(Buon segno: sei già ottimizzato.)";
    }

    /// <summary>Reveals which tweak it was + the honest measured verdict, then undoes it.</summary>
    private void Reveal()
    {
        if (_hidden is null) return;

        // Always restore the system first.
        if (_journalFile is not null)
            try { _main.Execution.Undo(_journalFile); _main.Changes.Refresh(); } catch { /* leave journal for manual undo */ }

        var (outcome, delta) = MapMeasuredOutcome();
        // V7 evidence: a measured Ghost verdict is the best, most honest data point — record it
        // (anonimo, in locale). Inconclusive = nessun verdetto reale → niente da registrare.
        if (outcome != GhostOutcome.Inconclusive)
            _main.RecordEvidence(_hidden.Id, outcome switch
            {
                GhostOutcome.Helped => "helped",
                GhostOutcome.Hurt => "hurt",
                _ => "no-effect",
            }, delta);
        var reveal = GhostTweak.Reveal(_hidden.Name, outcome, delta);
        RevealTitle = reveal.Title;
        RevealPlain = reveal.Plain;
        RevealColor = reveal.Color;
        IsRevealed = true;
        IsApplied = false;
        _hidden = null;
        _journalFile = null;
        Status = "Round concluso. Il tweak è stato annullato: il sistema è tornato esattamente com'era.";
    }

    /// <summary>Maps the wizard's most recent comparison to a Ghost outcome. No fresh measurement
    /// → Inconclusive (honest: without a measured A/B there's no real verdict).</summary>
    private (GhostOutcome, double) MapMeasuredOutcome()
    {
        var cmp = _main.Measure.LastComparison;
        if (cmp is null || cmp.GateTriggered || cmp.Metrics.Count == 0)
            return (GhostOutcome.Inconclusive, 0);
        var primary = cmp.Metrics[0];
        var outcome = primary.Verdict switch
        {
            Verdict.Improvement => GhostOutcome.Helped,
            Verdict.Regression => GhostOutcome.Hurt,
            Verdict.NoMeasurableEffect => GhostOutcome.NoEffect,
            _ => GhostOutcome.Inconclusive,
        };
        return (outcome, primary.DeltaPercent);
    }
}
