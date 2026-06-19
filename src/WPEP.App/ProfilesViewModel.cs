using System.Collections.ObjectModel;
using WPEP.Execution;
using WPEP.KnowledgeBase;

namespace WPEP.App;

/// <summary>One applicable tweak with a checkbox, for building a batch / profile.</summary>
public sealed class TweakCheckRow : ViewModelBase
{
    private readonly System.Action _onToggle;
    private bool _selected;
    public TweakEntry Entry { get; }

    public TweakCheckRow(TweakEntry entry, System.Action onToggle)
    {
        Entry = entry;
        _onToggle = onToggle;
    }

    public string Name => Entry.Name;
    public string Id => Entry.Id;
    public string Category => Entry.Category;
    public bool IsSelected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) _onToggle(); }
    }
}

/// <summary>A saved profile as shown in the list.</summary>
public sealed record ProfileRow(string Name, string Description, bool BuiltIn, int Count);

/// <summary>Profiles &amp; batch apply (V3 §2): pick any set of applicable tweaks with checkboxes,
/// apply them together (each still journaled + undoable one-by-one), and save the selection as a
/// reusable profile. Built-in profiles ship curated; the user's own live on disk. Applying always
/// goes through the same dry-run consent + conflict guard as a single apply.</summary>
public sealed class ProfilesViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private string _newProfileName = "";
    private string _status = "";

    public ProfilesViewModel(MainViewModel main)
    {
        _main = main;
        try
        {
            foreach (var e in KnowledgeBaseLoader.Load()
                .Where(e => _main.Execution.CanApply(e))
                .OrderBy(e => e.Category, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, System.StringComparer.OrdinalIgnoreCase))
                Tweaks.Add(new TweakCheckRow(e, OnToggle));
        }
        catch { /* KB unreadable → empty list, page still works */ }
        RefreshProfiles();
    }

    public ObservableCollection<TweakCheckRow> Tweaks { get; } = [];
    public ObservableCollection<ProfileRow> Profiles { get; } = [];

    public int SelectedCount => Tweaks.Count(t => t.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public string SelectionLabel => $"Applica selezionati ({SelectedCount})";
    public string NewProfileName { get => _newProfileName; set => Set(ref _newProfileName, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public RelayCommand ApplySelectedCommand => new(ApplySelected, () => HasSelection);
    public RelayCommand SaveProfileCommand => new(SaveProfile,
        () => HasSelection && NewProfileName.Trim().Length > 0);
    public RelayCommand<ProfileRow> LoadProfileCommand => new(LoadProfile);
    public RelayCommand<ProfileRow> ApplyProfileCommand => new(ApplyProfile);
    public RelayCommand<ProfileRow> DeleteProfileCommand => new(DeleteProfile, p => p is { BuiltIn: false });

    private void OnToggle()
    {
        Raise(nameof(SelectedCount));
        Raise(nameof(HasSelection));
        Raise(nameof(SelectionLabel));
    }

    private IReadOnlyList<TweakEntry> Selected() =>
        [.. Tweaks.Where(t => t.IsSelected).Select(t => t.Entry)];

    /// <summary>Opens the existing batch dry-run/consent dialog for the checked tweaks. Each one is
    /// still applied (and undoable) individually — the user keeps full one-by-one control.</summary>
    private void ApplySelected()
    {
        var entries = Selected();
        if (entries.Count > 0) _main.ApplyAll.Open(entries);
    }

    private void SaveProfile()
    {
        var name = NewProfileName.Trim();
        if (name.Length == 0) return;
        ProfileStore.Save(new TweakProfile(name, [.. Selected().Select(e => e.Id)],
            $"Profilo personalizzato ({SelectedCount} tweak)."));
        NewProfileName = "";
        RefreshProfiles();
        Status = $"Profilo «{name}» salvato.";
    }

    /// <summary>Checks the boxes that belong to a profile (so the user can review/edit before applying).</summary>
    private void LoadProfile(ProfileRow? row)
    {
        if (row is null) return;
        var profile = ProfileStore.Get(row.Name);
        if (profile is null) return;
        var ids = profile.TweakIds.ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        foreach (var t in Tweaks) t.IsSelected = ids.Contains(t.Id);
        Status = $"Profilo «{row.Name}» caricato: rivedi le spunte, poi applica.";
    }

    private void ApplyProfile(ProfileRow? row)
    {
        LoadProfile(row);
        ApplySelected();
    }

    private void DeleteProfile(ProfileRow? row)
    {
        if (row is null || row.BuiltIn) return;
        ProfileStore.Delete(row.Name);
        RefreshProfiles();
        Status = $"Profilo «{row.Name}» eliminato.";
    }

    public void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var p in ProfileStore.All())
            Profiles.Add(new ProfileRow(p.Name, p.Description, p.BuiltIn, p.TweakIds.Count));
    }
}
