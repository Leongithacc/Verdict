using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace WPEP.App;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    // CommandManager re-queries CanExecute on every input/focus change: without
    // this hookup, buttons stay disabled even after their condition becomes true.
    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _) => execute();
}

public sealed class RelayCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => p is T t ? canExecute?.Invoke(t) ?? true : false;
    public void Execute(object? p) { if (p is T t) execute(t); }
}

/// <summary>Portable settings: a JSON file next to the exe (PORTABILITY:
/// nothing outside the app folder, delete folder = never existed).</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Violet";
    public int DefaultBenchmarkRuns { get; set; } = 5;
    public double NoiseGateThresholdPercent { get; set; } = 10;
    public bool CompactLists { get; set; }

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data");
    private static string FilePath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>True when no settings file existed at load: first launch.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsFirstRun { get; private set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException) { }
        return new() { IsFirstRun = true };
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>The 4 curated theme presets (DESIGN_DIRECTION). Only the accent
/// changes — semantic colors never do.</summary>
public static class ThemePresets
{
    public static readonly IReadOnlyDictionary<string, (string Accent, string AccentDeep)> All =
        new Dictionary<string, (string, string)>
        {
            ["Violet"] = ("#8B5CF6", "#4A0080"),
            ["Villain"] = ("#7C3AED", "#3A0A5C"),
            ["Stealth"] = ("#7C8CA0", "#3A4250"),
            ["Crimson"] = ("#D45D6A", "#6E1F2A"),
            ["Emerald"] = ("#34B98F", "#0F5C44"),
        };

    public static void Apply(string name)
    {
        if (!All.TryGetValue(name, out var preset))
            preset = All["Violet"];
        var app = System.Windows.Application.Current;
        app.Resources["C.Accent"] =
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(preset.Accent);
        app.Resources["C.AccentDeep"] =
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(preset.AccentDeep);
    }
}
