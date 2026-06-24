using System.Text.Json;

namespace WPEP.Execution;

/// <summary>The last Regression Sentinel verdict, persisted so the background tray can surface it.
/// HONEST design: the tray can't run a benchmark by itself (that needs a game + capture), so it can't
/// MEASURE a regression passively. Instead, when YOU run a Sentinel comparison (CLI/GUI), the verdict
/// is saved here; the tray reads it and reminds you if performance regressed — until you re-check.</summary>
public sealed record SentinelSnapshot(string Status, string Headline, string CapturedAtIso);

public static class SentinelStatusStore
{
    /// <summary>%LOCALAPPDATA%\Verdict\sentinel-status.json — shared by the writer (CLI/GUI) and the tray.</summary>
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Verdict", "sentinel-status.json");

    public static void Save(SentinelSnapshot snapshot) => Save(snapshot, FilePath);

    public static SentinelSnapshot? Load() => Load(FilePath);

    // Path-injectable cores so the round-trip is unit-testable without touching the real profile dir.
    public static void Save(SentinelSnapshot snapshot, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
        }
        catch { /* best effort: a status write must never crash the caller */ }
    }

    public static SentinelSnapshot? Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<SentinelSnapshot>(File.ReadAllText(path)) : null;
        }
        catch { return null; }
    }
}
