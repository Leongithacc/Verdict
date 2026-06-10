namespace WPEP.Benchmark;

/// <summary>
/// Finds the PresentMon executable. Probe order: explicit path, "tools" next to
/// the app, %LOCALAPPDATA%\WPEP\tools, then PATH.
/// </summary>
public static class PresentMonLocator
{
    public const string ExeName = "PresentMon.exe";

    public static string ToolsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WPEP", "tools");

    public static string? Find(string? explicitPath = null)
    {
        if (explicitPath is not null)
            return File.Exists(explicitPath) ? explicitPath : null;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", ExeName),
            Path.Combine(AppContext.BaseDirectory, ExeName),
            Path.Combine(ToolsDirectory, ExeName),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (dir.Length == 0)
                continue;
            var candidate = Path.Combine(dir.Trim(), ExeName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
