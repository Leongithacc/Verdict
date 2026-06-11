namespace WPEP.Benchmark;

/// <summary>
/// Downloads a pinned PresentMon release (Intel, MIT license) into the tools
/// folder NEXT TO the exe (PORTABILITY: leave-no-trace — nothing outside the
/// app folder). Console app only, never the Service variant. Version is pinned,
/// not "latest": reproducible captures beat silent upgrades.
/// </summary>
public static class PresentMonInstaller
{
    public const string PinnedVersion = "2.4.1";
    public static string DownloadUrl =>
        $"https://github.com/GameTechDev/PresentMon/releases/download/v{PinnedVersion}/PresentMon-{PinnedVersion}-x64.exe";

    public static string PortableToolsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "tools");

    public static async Task<string> InstallAsync(Action<string>? progress = null)
    {
        Directory.CreateDirectory(PortableToolsDirectory);
        var target = Path.Combine(PortableToolsDirectory, PresentMonLocator.ExeName);

        progress?.Invoke($"Scarico PresentMon {PinnedVersion} da:\n  {DownloadUrl}");
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(DownloadUrl);
        await File.WriteAllBytesAsync(target, bytes);
        progress?.Invoke($"Installato: {target} ({bytes.Length / 1024} KB)");
        return target;
    }
}
