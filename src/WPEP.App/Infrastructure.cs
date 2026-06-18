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

/// <summary>A full dark palette. Each theme changes the WHOLE look (background, surfaces,
/// lines, accent) — not just the accent — so switching is clearly visible. Semantic colors
/// (Ok/Warn/Danger) never change. All backgrounds stay near-black with light text.</summary>
public sealed record ThemePreset(
    string Accent, string AccentDeep, string Bg, string Surface, string Surface2, string Line);

/// <summary>A theme as shown in the picker: name + ready-made preview brushes.</summary>
public sealed record ThemeOption(
    string Name, System.Windows.Media.Brush Bg,
    System.Windows.Media.Brush Surface, System.Windows.Media.Brush Accent);

public static class ThemePresets
{
    public static readonly IReadOnlyDictionary<string, ThemePreset> All =
        new Dictionary<string, ThemePreset>
        {
            // name          accent      deep        bg          surface     surface2    line
            ["Violet"]   = new("#8B5CF6", "#4A0080", "#0A0A0F", "#15151C", "#1C1C25", "#262633"),
            ["Villain"]  = new("#7C3AED", "#3A0A5C", "#08070C", "#161019", "#1F1528", "#2C1F3C"),
            ["Stealth"]  = new("#90A4C0", "#3A4250", "#0A0B0D", "#15171C", "#1D2027", "#2A2F3A"),
            ["Crimson"]  = new("#E0525F", "#6E1F2A", "#0C0809", "#1A1315", "#241A1D", "#34262A"),
            ["Emerald"]  = new("#34D399", "#0F5C44", "#070C0A", "#121A16", "#19241E", "#26332C"),
            ["Midnight"] = new("#5B8DEF", "#1E3A6E", "#070A10", "#11151F", "#181F2C", "#252E42"),
            ["Inferno"]  = new("#F97316", "#7C2D12", "#0C0907", "#1A1410", "#241B14", "#34281C"),
            ["Toxic"]    = new("#A3E635", "#3F6212", "#090C07", "#161A12", "#1F2618", "#2D3422"),
            ["Ice"]      = new("#38BDF8", "#0E4F6E", "#070C0F", "#111A1F", "#18242B", "#25353E"),
            ["Gold"]     = new("#F5C542", "#7A5C12", "#0C0A06", "#1A1710", "#241F14", "#342A1C"),
            ["Mono"]     = new("#C7CBD4", "#3A3D45", "#090909", "#151515", "#1E1E1E", "#2C2C2C"),
            ["Sakura"]   = new("#F472B6", "#831843", "#0C080A", "#1A1216", "#241921", "#33252C"),
            ["Cyber"]    = new("#22D3EE", "#155E75", "#060B0D", "#0F1A1E", "#16252B", "#22353D"),
            ["Royal"]    = new("#818CF8", "#312E81", "#08080F", "#131320", "#1B1B2E", "#28283F"),
            ["Blood"]    = new("#DC2626", "#7F1D1D", "#0A0606", "#170F0F", "#1F1414", "#2E1D1D"),
            ["Forest"]   = new("#4ADE80", "#14532D", "#070C08", "#111A13", "#18241A", "#243328"),
            ["Vapor"]    = new("#C084FC", "#6B21A8", "#0A080F", "#16111F", "#1F182B", "#2D2440"),
        };

    /// <summary>The themes as pickable options carrying their own preview swatches
    /// (background stripe + accent), so the menu shows each theme's look inline.</summary>
    public static IReadOnlyList<ThemeOption> Options()
    {
        static System.Windows.Media.SolidColorBrush B(string hex)
        {
            var br = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            br.Freeze();
            return br;
        }
        return [.. All.Select(kv => new ThemeOption(
            kv.Key, B(kv.Value.Bg), B(kv.Value.Surface2), B(kv.Value.Accent)))];
    }

    public static void Apply(string name)
    {
        if (!All.TryGetValue(name, out var p))
            p = All["Violet"];
        if (System.Windows.Application.Current is not { } app)
            return;

        // REPLACE each brush resource with a fresh SolidColorBrush. The XAML brushes get
        // frozen at load, so mutating their Color is a no-op; but every consumer references
        // the brush via DynamicResource, so swapping the resource value refreshes the whole
        // UI instantly. (Also keep the C.* color token in sync for completeness.)
        System.Windows.Media.Color C(string hex) =>
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

        void SetBrush(string key, string hex)
        {
            var color = C(hex);
            app.Resources["C." + key] = color;
            app.Resources[key] = new System.Windows.Media.SolidColorBrush(color);
        }

        SetBrush("Accent", p.Accent);
        SetBrush("AccentDeep", p.AccentDeep);
        SetBrush("Bg", p.Bg);
        SetBrush("Surface", p.Surface);
        SetBrush("Surface2", p.Surface2);
        SetBrush("Line", p.Line);
    }
}
