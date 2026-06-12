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

    /// <summary>SHA256 of PresentMon-2.4.1-x64.exe, verified 2026-06-11.
    /// F1: a downloaded binary that fails verification is NEVER executed.</summary>
    public const string PinnedSha256 =
        "d74183e7ae630f72cd3690be0373ecbfdc6cbb86578148aab8fa2a7166068f34";

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

        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if (hash != PinnedSha256)
            throw new InvalidOperationException(
                "Il file scaricato NON supera la verifica SHA256 e non verrà salvato.\n" +
                $"  atteso : {PinnedSha256}\n  trovato: {hash}\n" +
                "Riprova, o scarica manualmente dalla pagina GitHub di PresentMon.");

        await File.WriteAllBytesAsync(target, bytes);
        progress?.Invoke($"Installato e verificato (SHA256 ok): {target} ({bytes.Length / 1024} KB)");
        return target;
    }
}
