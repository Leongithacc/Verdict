namespace WPEP.Benchmark;

/// <summary>
/// Downloads a pinned PresentMon release (Intel, MIT license) into the WPEP
/// tools directory. Version is pinned, not "latest": reproducible captures
/// beat silent upgrades.
/// </summary>
public static class PresentMonInstaller
{
    public const string PinnedVersion = "2.4.1";
    public static string DownloadUrl =>
        $"https://github.com/GameTechDev/PresentMon/releases/download/v{PinnedVersion}/PresentMon-{PinnedVersion}-x64.exe";

    public static async Task<string> InstallAsync(Action<string>? progress = null)
    {
        Directory.CreateDirectory(PresentMonLocator.ToolsDirectory);
        var target = Path.Combine(PresentMonLocator.ToolsDirectory, PresentMonLocator.ExeName);

        progress?.Invoke($"Scarico PresentMon {PinnedVersion} da:\n  {DownloadUrl}");
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(DownloadUrl);
        await File.WriteAllBytesAsync(target, bytes);
        progress?.Invoke($"Installato: {target} ({bytes.Length / 1024} KB)");
        return target;
    }
}
