using System.Text.Json;

namespace WPEP.Execution;

/// <summary>A named set of tweak ids to apply together (V3 §2). Built-in profiles ship
/// curated; the user can save/delete their own. (Per-tweak revert-on-exit for the gaming
/// session comes later — kept simple here.)</summary>
public sealed record TweakProfile(
    string Name,
    IReadOnlyList<string> TweakIds,
    string Description = "",
    bool BuiltIn = false);

/// <summary>Loads/saves tweak profiles. Built-ins are curated and always present; user
/// profiles live as JSON next to the exe (data/profiles/*.json). Self-contained, portable.</summary>
public static class ProfileStore
{
    public static string Directory =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "data", "profiles");

    /// <summary>Curated defaults. They reference KB tweak ids; non-existent or non-applicable
    /// ones are simply skipped at apply time, so this list is forgiving.</summary>
    public static IReadOnlyList<TweakProfile> Defaults { get; } =
    [
        new("Competitive",
            [
                "power-plan-high-performance",
                "systemresponsiveness-gpupriority-registry",
                "network-throttling-index",
                "disable-gamedvr-background-recording",
                "hags-hardware-gpu-scheduling",
                "disable-enhance-pointer-precision",
                "disable-sticky-keys-gaming",
                "nvidia-low-latency-on",
                "nvidia-prefer-max-performance",
                "win11-windowed-optimizations",
                "windows-game-mode",
            ],
            // V-Sync Off NON è qui di proposito: con G-SYNC è l'opposto giusto (V-Sync ON + framecap),
            // quindi resta una scelta manuale per chi sa di NON usare il refresh variabile.
            "Latenza al massimo per gli sparatutto competitivi.", BuiltIn: true),
        new("Streaming",
            [
                "power-plan-high-performance",
                "hags-hardware-gpu-scheduling",
                "network-throttling-index",
            ],
            "Equilibrio per giocare e streammare insieme.", BuiltIn: true),
        new("Daily",
            [
                "disable-sticky-keys-gaming",
                "disable-enhance-pointer-precision",
                "menu-show-delay-instant",
                "foreground-lock-timeout-off",
            ],
            "Ritocchi leggeri di tutti i giorni (QoL, nessun rischio).", BuiltIn: true),
        new("Single-player",
            [
                "power-plan-high-performance",
                "hags-hardware-gpu-scheduling",
                "disable-enhance-pointer-precision",
                "win11-variable-refresh-rate",
                "win11-windowed-optimizations",
                "windows-game-mode",
            ],
            "Giochi single-player: fluidità e qualità, niente estremismi di latenza.", BuiltIn: true),
        new("Pulizia max",
            [
                "fast-startup-disable",
                "network-throttling-index",
                "systemresponsiveness-gpupriority-registry",
                "disable-gamedvr-background-recording",
                "hags-hardware-gpu-scheduling",
                "qos-disable-user-presence",
            ],
            "Spegne più roba di background per un PC sempre reattivo.", BuiltIn: true),
    ];

    /// <summary>All profiles: built-ins first, then user profiles from disk (a user profile
    /// with a built-in's name overrides it).</summary>
    public static IReadOnlyList<TweakProfile> All()
    {
        var byName = new Dictionary<string, TweakProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Defaults)
            byName[p.Name] = p;
        if (System.IO.Directory.Exists(Directory))
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
            {
                try
                {
                    var p = JsonSerializer.Deserialize<TweakProfile>(System.IO.File.ReadAllText(file));
                    if (p is { Name.Length: > 0 })
                        byName[p.Name] = p with { BuiltIn = false };
                }
                catch { /* a corrupt profile file must not break the list */ }
            }
        }
        return [.. byName.Values];
    }

    public static TweakProfile? Get(string name) =>
        All().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static void Save(TweakProfile profile)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var file = System.IO.Path.Combine(Directory, SafeFileName(profile.Name) + ".json");
        System.IO.File.WriteAllText(file, JsonSerializer.Serialize(
            profile with { BuiltIn = false }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Deletes a USER profile. Built-ins can't be deleted (they re-appear).</summary>
    public static bool Delete(string name)
    {
        var file = System.IO.Path.Combine(Directory, SafeFileName(name) + ".json");
        if (System.IO.File.Exists(file)) { System.IO.File.Delete(file); return true; }
        return false;
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c)));
}
