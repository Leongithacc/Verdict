using System.Text.Json;

namespace WPEP.SystemAnalyzer;

/// <summary>A snapshot of the system facts Time Machine tracks over time. Small on purpose: just
/// the things whose change actually matters for gaming performance.</summary>
public sealed record SystemState(
    string TakenAtIso,
    bool? ExpoEnabled,
    double? RamGb,
    string Gpu,
    string Bios,
    int ThirdPartyStartup);

/// <summary>One difference between two snapshots, in plain language.</summary>
public sealed record TimelineChange(string Field, string Before, string After);

/// <summary>Time Machine (Lab feature): keeps a baseline of your system and shows a timeline of what
/// changed since last time — EXPO turned off, RAM swapped, BIOS updated, startup bloat grew. The
/// diff is pure/testable; persistence is small timestamped JSON files (no system writes beyond the
/// app's own data folder). The capture itself reuses the existing hardware scan.</summary>
public static class SystemTimeline
{
    public static string Directory =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "data", "timeline");

    /// <summary>Pure diff: what changed from <paramref name="older"/> to <paramref name="newer"/>.
    /// Only fields that actually differ are reported; unknown→unknown is not a change.</summary>
    public static IReadOnlyList<TimelineChange> Diff(SystemState older, SystemState newer)
    {
        var changes = new List<TimelineChange>();

        void Cmp(string field, string a, string b)
        {
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                changes.Add(new TimelineChange(field, a, b));
        }

        Cmp("RAM EXPO/XMP", Expo(older.ExpoEnabled), Expo(newer.ExpoEnabled));
        Cmp("RAM totale", Gb(older.RamGb), Gb(newer.RamGb));
        Cmp("GPU", Norm(older.Gpu), Norm(newer.Gpu));
        Cmp("BIOS", Norm(older.Bios), Norm(newer.Bios));
        Cmp("App all'avvio (terze parti)", older.ThirdPartyStartup.ToString(), newer.ThirdPartyStartup.ToString());
        return changes;
    }

    private static string Expo(bool? v) => v switch { true => "attivo", false => "spento", _ => "n/d" };
    private static string Gb(double? v) => v is { } g ? $"{g:F0} GB" : "n/d";
    private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "n/d" : s.Trim();

    public static void Save(SystemState state)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            // Sortable filename so the newest is last alphabetically; sanitize the ISO stamp.
            string stamp = new string(state.TakenAtIso.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
            var file = System.IO.Path.Combine(Directory, $"state-{stamp}.json");
            System.IO.File.WriteAllText(file, JsonSerializer.Serialize(state,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* a failed snapshot must never break the scan */ }
    }

    /// <summary>The saved snapshots, oldest first.</summary>
    public static IReadOnlyList<SystemState> LoadAll()
    {
        var list = new List<SystemState>();
        if (!System.IO.Directory.Exists(Directory)) return list;
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "state-*.json").OrderBy(f => f))
        {
            try
            {
                var s = JsonSerializer.Deserialize<SystemState>(System.IO.File.ReadAllText(file));
                if (s is not null) list.Add(s);
            }
            catch { /* skip a corrupt snapshot */ }
        }
        return list;
    }
}
