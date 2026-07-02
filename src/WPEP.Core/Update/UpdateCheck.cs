using System.Globalization;
using System.Text.Json;

namespace WPEP.Core.Update;

/// <summary>
/// Result of an update check. <see cref="Configured"/> is false when no host has
/// been chosen yet — in that case Verdict says so honestly and never touches the
/// network. Verdict is consent-first everywhere: a check only ever *reports* a new
/// version + where to get it. It never downloads or installs anything on its own.
/// </summary>
public sealed record UpdateInfo(
    bool Configured,
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? DownloadUrl,
    string? ReleaseNotes,
    string? Error)
{
    /// <summary>No update host configured yet → no network call, honest status.</summary>
    public static UpdateInfo NotConfigured(string current) =>
        new(Configured: false, UpdateAvailable: false, current, null, null, null, null);
}

/// <summary>A place Verdict can ask "is there a newer version?". Swappable so the
/// host (GitHub Releases / own URL) is a config decision, not a code rewrite.</summary>
public interface IUpdateSource
{
    Task<UpdateInfo> CheckAsync(string currentVersion, CancellationToken ct = default);
}

/// <summary>
/// Where releases live. Empty until Léon picks a host. The honest contract: with
/// no owner/repo set, <see cref="IsConfigured"/> is false and the check makes ZERO
/// network calls — the UI shows "non configurato" instead of pretending.
/// </summary>
public static class UpdateConfig
{
    // ── Host configurato: GitHub Releases di Léon → github.com/Leongithacc/Verdict ──
    //   L'app interroga  api.github.com/repos/{owner}/{repo}/releases/latest.
    //   Finché non esiste una release pubblicata, il check risponde con grazia
    //   "Nessuna release pubblicata ancora" (404) — nessun crash. Per cambiare host
    //   in futuro basta cambiare queste due stringhe: zero codice da toccare.
    public const string GitHubOwner = "Leongithacc";
    public const string GitHubRepo = "Verdict";

    public static bool IsConfigured => GitHubOwner.Length > 0 && GitHubRepo.Length > 0;
}

/// <summary>Entry point used by the GUI and the CLI. Host-agnostic façade.</summary>
public static class UpdateChecker
{
    /// <summary>Checks the configured host. If none is set, returns
    /// <see cref="UpdateInfo.NotConfigured"/> WITHOUT any network call.</summary>
    public static Task<UpdateInfo> CheckAsync(string currentVersion, CancellationToken ct = default)
        => CheckAsync(currentVersion, DefaultSource(), ct);

    /// <summary>Testable core: a null <paramref name="source"/> means "no host
    /// configured" → <see cref="UpdateInfo.NotConfigured"/>, no network. Decouples the
    /// decision logic from the production <see cref="UpdateConfig"/> constants.</summary>
    public static Task<UpdateInfo> CheckAsync(string currentVersion, IUpdateSource? source,
        CancellationToken ct = default)
        => source is null
            ? Task.FromResult(UpdateInfo.NotConfigured(currentVersion))
            : source.CheckAsync(currentVersion, ct);

    /// <summary>The source built from <see cref="UpdateConfig"/>, or null if no host set.</summary>
    public static IUpdateSource? DefaultSource()
        => UpdateConfig.IsConfigured
            ? new GitHubReleaseSource(UpdateConfig.GitHubOwner, UpdateConfig.GitHubRepo)
            : null;

    /// <summary>
    /// Compares dotted numeric versions ("1.0", "1.2.3"), tolerating a leading 'v'
    /// and a '-prerelease' suffix. Returns &gt;0 if <paramref name="a"/> is newer
    /// than <paramref name="b"/>, 0 if equal, &lt;0 if older. Culture-invariant.
    /// This MUST be correct: a wrong answer either nags forever or hides a real update.
    /// </summary>
    public static int VersionCompare(string a, string b)
    {
        static long[] Parse(string v)
        {
            v = v.Trim().TrimStart('v', 'V');
            int dash = v.IndexOf('-');          // drop "-beta", "-rc1", …
            if (dash >= 0)
                v = v[..dash];
            var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var nums = new long[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                // A numeric component too big even for long (date-stamped tags,
                // garbage) saturates instead of collapsing to 0 — 0 would make a
                // huge version look ancient and silently hide a real update.
                nums[i] = long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    ? n
                    : parts[i].All(char.IsAsciiDigit) ? long.MaxValue : 0;
            return nums;
        }

        long[] an = Parse(a), bn = Parse(b);
        int len = Math.Max(an.Length, bn.Length);
        for (int i = 0; i < len; i++)
        {
            long av = i < an.Length ? an[i] : 0;
            long bv = i < bn.Length ? bn[i] : 0;
            if (av != bv)
                return av.CompareTo(bv);
        }
        return 0;
    }
}

/// <summary>Reads the latest published release from the GitHub Releases API
/// (read-only, public endpoint — no token, no data sent beyond a GET).</summary>
public sealed class GitHubReleaseSource(string owner, string repo) : IUpdateSource
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Verdict-UpdateCheck");   // GitHub requires a UA
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public async Task<UpdateInfo> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        try
        {
            using var resp = await Http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new UpdateInfo(Configured: true, UpdateAvailable: false, currentVersion,
                    null, null, null, "Nessuna release pubblicata ancora (o repo non trovata).");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return Parse(json, currentVersion);
        }
        catch (Exception ex)
        {
            // Un controllo aggiornamenti non deve MAI buttare giù nulla: errore onesto e via.
            return new UpdateInfo(Configured: true, UpdateAvailable: false, currentVersion,
                null, null, null, $"Controllo non riuscito: {ex.Message}");
        }
    }

    /// <summary>Parses a GitHub <c>releases/latest</c> payload. Static + public so the
    /// parsing (and the version decision) is unit-testable without any network call.</summary>
    public static UpdateInfo Parse(string json, string currentVersion)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string latest = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
        string? notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
        string? page = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;

        // Prefer the .zip asset's direct download; fall back to the release page.
        string? download = page;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                string? name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is not null
                    && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    && a.TryGetProperty("browser_download_url", out var u))
                {
                    download = u.GetString();
                    break;
                }
            }
        }

        bool newer = UpdateChecker.VersionCompare(latest, currentVersion) > 0;
        return new UpdateInfo(Configured: true, UpdateAvailable: newer, currentVersion,
            latest.TrimStart('v', 'V'), download, notes, Error: null);
    }
}
