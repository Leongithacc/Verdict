using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using WPEP.Execution;

namespace WPEP.App;

/// <summary>Product identity shown in the UI (sidebar + About). The version itself
/// lives once in <see cref="WPEP.Core.AppVersion"/> so the GUI, tray, CLI and the
/// update check never drift apart.</summary>
public static class AppInfo
{
    public const string Version = WPEP.Core.AppVersion.Current;
    public const string VersionLabel = WPEP.Core.AppVersion.Label;
}

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
    public string Theme { get; set; } = "Villain";
    public int DefaultBenchmarkRuns { get; set; } = 5;
    public double NoiseGateThresholdPercent { get; set; } = 10;
    public bool CompactLists { get; set; }

    /// <summary>Modello Ollama per il co-pilota (V6). Vuoto = default (qwen2.5). Persistito così
    /// l'utente può puntare al modello che ha DAVVERO installato (es. "qwen2.5vl:32b").</summary>
    public string CoPilotModel { get; set; } = "";

    /// <summary>Quale cervello usa il co-pilota: "ollama" (locale, default, privato) o
    /// "claude" (cloud Anthropic, opt-in: serve <see cref="ClaudeApiKey"/>). Persistito.</summary>
    public string CoPilotBrain { get; set; } = "ollama";

    /// <summary>V7 community remoto: opt-in esplicito per inviare i tuoi esiti anonimi al
    /// backend pubblico (Cloudflare Worker) E ricevere le stats community. Default OFF →
    /// LocalOnlyBackend, niente rete. Vedi docs/V7_REMOTE_BACKEND_DESIGN.md.</summary>
    public bool CommunityShareEnabled { get; set; }

    /// <summary>Vista lista Verdict: false = raggruppa per stato (Recommended/AlreadyActive/…),
    /// true = raggruppa per macro-bucket UX (FPS/Network/QoL/Background). Persistito.
    /// Vedi docs/VS_HONE.md sez. 3.2.</summary>
    public bool ShowByBucket { get; set; }

    /// <summary>Modello Claude per il co-pilota cloud. Vuoto = default (claude-sonnet-4-6).
    /// Cambiabile a claude-opus-4-8, claude-haiku-4-5-20251001, ecc.</summary>
    public string ClaudeModel { get; set; } = "";

    /// <summary>Forma serializzata della API key Anthropic: cifrata DPAPI per l'utente Windows
    /// corrente, poi base64. Mai esposta come property in chiaro al di fuori di
    /// <see cref="ClaudeApiKey"/>. Spostare settings.json su un altro utente/PC ⇒ chiave illeggibile
    /// (richiesto re-inserimento), che è il comportamento desiderato.</summary>
    public string ClaudeApiKeyEncrypted { get; set; } = "";

    /// <summary>API key Anthropic in chiaro per uso runtime. Getter/setter convertono trasparentemente
    /// da/a <see cref="ClaudeApiKeyEncrypted"/> via DPAPI (CurrentUser). Mai serializzata.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ClaudeApiKey
    {
        get => DecryptKey(ClaudeApiKeyEncrypted);
        set => ClaudeApiKeyEncrypted = EncryptKey(value);
    }

    // ── Google Gemini (cloud) — opt-in, key cifrata DPAPI come Claude ──
    public string GeminiModel { get; set; } = "";
    public string GeminiApiKeyEncrypted { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string GeminiApiKey
    {
        get => DecryptKey(GeminiApiKeyEncrypted);
        set => GeminiApiKeyEncrypted = EncryptKey(value);
    }

    // ── OpenAI / GPT (cloud) — opt-in, key cifrata DPAPI come Claude ──
    public string OpenAiModel { get; set; } = "";
    public string OpenAiApiKeyEncrypted { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string OpenAiApiKey
    {
        get => DecryptKey(OpenAiApiKeyEncrypted);
        set => OpenAiApiKeyEncrypted = EncryptKey(value);
    }

    /// <summary>Helper DPAPI condiviso dalle 3 API key (Claude/Gemini/OpenAI): legge base64 cifrato
    /// e ritorna in chiaro. File spostato da un altro utente/PC ⇒ illeggibile ⇒ stringa vuota
    /// (l'utente reinserisce la chiave, comportamento desiderato).</summary>
    private static string DecryptKey(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try
        {
            var bytes = Convert.FromBase64String(encrypted);
            var plain = System.Security.Cryptography.ProtectedData.Unprotect(
                bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return "";
        }
    }

    private static string EncryptKey(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var bytes = System.Security.Cryptography.ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes(plain),
            null,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Risk Slider (Lab feature): how far the user wants to go. Default Balanced =
    /// the normal recommended set. Persisted so the choice sticks across launches.</summary>
    public RiskTolerance RiskTolerance { get; set; } = RiskTolerance.Balanced;

    /// <summary>Feature flags for the Lab page. Stores ONLY the user's explicit overrides;
    /// a feature absent from the dictionary falls back to its catalog default. This keeps the
    /// file small and lets us change defaults later without stomping user choices.</summary>
    public Dictionary<string, bool> Features { get; set; } = new();

    /// <summary>Is a Lab feature on? Honors the user's stored choice, else the catalog default.
    /// A not-yet-implemented module ("In arrivo") is always off, even if an old settings file had
    /// it on — so a stale flag can never light up a module that doesn't exist yet. Id is a
    /// <c>FeatureCatalog</c> constant.</summary>
    public bool IsFeatureEnabled(string id)
    {
        var module = FeatureCatalog.Get(id);
        if (module is not { Available: true }) return false;
        return Features.TryGetValue(id, out var on) ? on : module.DefaultEnabled;
    }

    /// <summary>Turn a feature on/off and persist. Pass <c>isDefault</c> to clear the override
    /// so the catalog default rules again (keeps the file from accumulating redundant entries).</summary>
    public void SetFeature(string id, bool on)
    {
        var def = FeatureCatalog.Get(id)?.DefaultEnabled ?? false;
        if (on == def) Features.Remove(id); else Features[id] = on;
        Save();
    }

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

    public void Save() =>
        // Atomico (audit F5): un crash a metà scrittura corromperebbe preferenze +
        // API key cifrate. Helper condiviso col journal e l'evidence ledger.
        WPEP.Core.Io.AtomicJson.Write(FilePath, this,
            new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>A FULL theme system (design handoff 2026-06): palette + text + a chrome-darkening
/// <c>Ink</c> + light/dark <c>Mode</c> + a UI/mono font pair. Switching changes the WHOLE look,
/// not just the accent. Semantic colors (Ok/Warn/Danger…) are re-tuned per mode so they stay
/// readable on light surfaces too. Font keys are short labels resolved to real families (with
/// graceful fallbacks) in <see cref="ThemePresets.Apply"/>.</summary>
public sealed record ThemePreset(
    string Accent, string AccentDeep, string Bg, string Surface, string Surface2, string Line,
    string Text, string TextMuted, string Ink, string Mode, string UiFont, string MonoFont, string Mood);

/// <summary>A theme as shown in the picker card: a mini-mockup of the app (its own surfaces /
/// accent / line / text) plus name, mode badge, mood line and the font pair.</summary>
public sealed record ThemeOption(
    string Name, string ModeLabel, string Mood, string FontLabel,
    System.Windows.Media.Brush Bg, System.Windows.Media.Brush Surface,
    System.Windows.Media.Brush Surface2, System.Windows.Media.Brush Accent,
    System.Windows.Media.Brush Line, System.Windows.Media.Brush Text);

public static class ThemePresets
{
    public const string Default = "Villain";

    // 10 distinct systems (design handoff). Columns:
    // accent · deep · bg · surface · surface2 · line · text · textMuted · ink · mode · UI · mono · mood
    public static readonly IReadOnlyDictionary<string, ThemePreset> All =
        new Dictionary<string, ThemePreset>
        {
            ["Villain"]  = new("#7C3AED", "#3A0A5C", "#08070C", "#161019", "#1F1528", "#2C1F3C", "#E6E6EC", "#8A8A96", "#000000", "dark", "Segoe", "JetBrains", "Viola profondo · villain"),
            ["Stealth"]  = new("#7C93B5", "#2E3A4C", "#0A0B0E", "#15171C", "#1D2027", "#2A2F3A", "#DDE3EC", "#828B99", "#000000", "dark", "Sora", "JetBrains", "Acciaio notturno · ops"),
            ["Daybreak"] = new("#5B54E6", "#C8C5F5", "#F4F5F8", "#FFFFFF", "#EDEEF3", "#DDDFE7", "#1A1C24", "#6A6E7C", "#C9CDD6", "light", "Manrope", "JetBrains", "Indaco chiaro · diurno"),
            ["Terminal"] = new("#3DF07A", "#0C5C30", "#040805", "#0A1109", "#0F190D", "#1B2B18", "#CFF5D8", "#6F9579", "#000000", "dark", "Space Mono", "Space Mono", "Verde fosforo · CRT"),
            ["Ember"]    = new("#F97316", "#7C2D12", "#0C0907", "#1A130D", "#241A11", "#34271B", "#F0E5DC", "#A08D7E", "#000000", "dark", "Space Grotesk", "JetBrains", "Brace calda · forgia"),
            ["Glacier"]  = new("#38BDF8", "#0E4F6E", "#070C0F", "#101A20", "#16242C", "#23353F", "#DCEBF2", "#7B95A2", "#000000", "dark", "Sora", "JetBrains", "Ghiaccio cyan · artico"),
            ["Sakura"]   = new("#F472B6", "#831843", "#0D0A0C", "#1A141A", "#241A23", "#33252F", "#F2E2EB", "#A38595", "#000000", "dark", "Manrope", "JetBrains", "Rosa notturno · fiore"),
            ["Lux"]      = new("#E8B84B", "#6E5414", "#0A0905", "#16130B", "#201B10", "#312A19", "#F0EADB", "#9C9279", "#000000", "dark", "Space Grotesk", "Space Mono", "Oro caldo · lusso"),
            ["Carbon"]   = new("#C7CBD4", "#3A3D45", "#08080A", "#141416", "#1D1D20", "#2C2C30", "#E4E5E8", "#86888F", "#000000", "dark", "Space Grotesk", "JetBrains", "Grafite neutro · carbonio"),
            ["Linen"]    = new("#C2562F", "#E8C9B8", "#F6F2EC", "#FFFDFA", "#EFE9E0", "#E0D8CC", "#2A211A", "#7A6E60", "#D8CFC0", "light", "Sora", "JetBrains", "Terracotta · lino chiaro"),
        };

    /// <summary>The active theme name, falling back to the default for an unknown/legacy value
    /// (older settings stored themes that no longer exist).</summary>
    public static string Normalize(string? name) => name is { } n && All.ContainsKey(n) ? n : Default;

    // Short font keys → real WPF families with graceful fallbacks (design fonts may not be installed).
    private static string UiFamily(string key) => key switch
    {
        "Space Grotesk" => "Space Grotesk, Segoe UI Variable Display, Segoe UI",
        "Sora" => "Sora, Segoe UI Variable Display, Segoe UI",
        "Manrope" => "Manrope, Segoe UI Variable Display, Segoe UI",
        "Space Mono" => "Space Mono, Cascadia Mono, Consolas",
        _ => "Segoe UI Variable Display, Segoe UI",
    };
    private static string MonoFamily(string key) => key switch
    {
        "Space Mono" => "Space Mono, Cascadia Mono, Consolas",
        _ => "JetBrains Mono, Cascadia Code, Cascadia Mono, Consolas",
    };

    /// <summary>The themes as pickable cards: each carries its own mini-mockup brushes + labels.</summary>
    public static IReadOnlyList<ThemeOption> Options()
    {
        static System.Windows.Media.SolidColorBrush B(string hex)
        {
            var br = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            br.Freeze();
            return br;
        }
        return [.. All.Select(kv =>
        {
            var v = kv.Value;
            return new ThemeOption(
                kv.Key, v.Mode == "light" ? "Chiaro" : "Scuro", v.Mood, $"{v.UiFont} · {v.MonoFont}",
                B(v.Bg), B(v.Surface), B(v.Surface2), B(v.Accent), B(v.Line), B(v.Text));
        })];
    }

    public static void Apply(string name)
    {
        var p = All[Normalize(name)];
        if (System.Windows.Application.Current is not { } app)
            return;

        // REPLACE each brush resource with a fresh SolidColorBrush. The XAML brushes get frozen at
        // load, so mutating their Color is a no-op; but every consumer references the brush via
        // DynamicResource, so swapping the resource value refreshes the whole UI instantly. (Also
        // keep the C.* color token in sync.)
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
        SetBrush("Text", p.Text);
        SetBrush("TextMuted", p.TextMuted);
        SetBrush("Ink", p.Ink);

        // Fonts swap with the theme (DynamicResource consumers in Theme.xaml).
        app.Resources["UiFont"] = new System.Windows.Media.FontFamily(UiFamily(p.UiFont));
        app.Resources["MonoFont"] = new System.Windows.Media.FontFamily(MonoFamily(p.MonoFont));

        // Semantic colors are re-tuned for light mode so they stay readable on light surfaces.
        bool light = p.Mode == "light";
        SetBrush("Ok", light ? "#0E9F6E" : "#34D399");
        SetBrush("OkDim", light ? "#0B7355" : "#1F7A58");
        SetBrush("Info", light ? "#2563EB" : "#60A5FA");
        SetBrush("Warn", light ? "#B45309" : "#FBBF24");
        SetBrush("Danger", light ? "#DC2626" : "#F87171");
        SetBrush("Neutral", "#6B7280");
    }
}
