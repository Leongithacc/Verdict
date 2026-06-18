using System.Collections.ObjectModel;
using WPEP.Execution;

namespace WPEP.App;

/// <summary>One feature in the Lab, bound to a toggle. Flipping <see cref="Enabled"/> persists
/// immediately via <see cref="AppSettings.SetFeature"/> and bumps the live on/off count.</summary>
public sealed class FeatureRow : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly LabViewModel _owner;
    public FeatureModule Module { get; }

    public FeatureRow(FeatureModule module, AppSettings settings, LabViewModel owner)
    {
        Module = module;
        _settings = settings;
        _owner = owner;
    }

    public string Name => Module.Name;
    public string Tagline => Module.Tagline;
    public string Glyph => Module.Glyph;
    public string StatusLabel => Module.Status switch
    {
        FeatureStatus.Stable => "STABILE",
        FeatureStatus.Beta => "BETA",
        _ => "SPERIMENTALE",
    };
    /// <summary>Heavy modules run in background and cost resources — flagged so the user knows.</summary>
    public bool IsHeavy => Module.Heavy;
    public string HeavyLabel => Module.Heavy ? "BACKGROUND" : "";

    public bool Enabled
    {
        get => _settings.IsFeatureEnabled(Module.Id);
        set
        {
            if (value == Enabled) return;
            _settings.SetFeature(Module.Id, value);
            Raise();
            _owner.OnFeatureToggled();
        }
    }
}

/// <summary>A category header + its features, for the grouped Lab list.</summary>
public sealed class FeatureGroup(string name, IReadOnlyList<FeatureRow> features)
{
    public string Name { get; } = name;
    public IReadOnlyList<FeatureRow> Features { get; } = features;
}

/// <summary>The "Lab" page: a library of optional premium modules, each a toggle. Léon's call —
/// keep the app clean by shipping heavy/experimental features OFF and letting the user opt in.
/// This VM is the single front-end for the feature-flag framework; the modules themselves read
/// <c>settings.IsFeatureEnabled(id)</c> wherever they hook into the rest of the app.</summary>
public sealed class LabViewModel : ViewModelBase
{
    private readonly AppSettings _settings;

    public LabViewModel(AppSettings settings)
    {
        _settings = settings;
        var rows = FeatureCatalog.All.Select(m => new FeatureRow(m, settings, this)).ToList();
        AllRows = rows;
        Groups = new ObservableCollection<FeatureGroup>(
            rows.GroupBy(r => r.Module.Category)
                .Select(g => new FeatureGroup(g.Key, g.ToList())));
    }

    public IReadOnlyList<FeatureRow> AllRows { get; }
    public ObservableCollection<FeatureGroup> Groups { get; }

    public int EnabledCount => AllRows.Count(r => r.Enabled);
    public int TotalCount => AllRows.Count;
    public string Summary => $"{EnabledCount} di {TotalCount} moduli attivi";

    public string Intro =>
        "Il Laboratorio: ogni modulo è opzionale. Accendi solo ciò che ti serve — i moduli pesanti " +
        "(in background) e quelli sperimentali partono spenti, così l'app resta pulita e veloce.";

    /// <summary>Called by a row when toggled, to refresh the summary count.</summary>
    public void OnFeatureToggled()
    {
        Raise(nameof(EnabledCount));
        Raise(nameof(Summary));
    }
}
