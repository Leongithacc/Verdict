using System.Net.NetworkInformation;

namespace WPEP.SystemAnalyzer;

/// <summary>The network quality toward one target: latency, jitter, packet loss, and a grade.</summary>
public sealed record NetworkResult(
    string Target, string Host, int Sent, int Received,
    double AvgMs, double JitterMs, double LossPercent, string Grade, string GradeColor);

/// <summary>Network Duel (Lab feature): pings a few public anchors and grades latency / jitter /
/// loss for gaming. Honest framing — many game servers block ICMP, so these are route-quality
/// anchors, not the exact match server. The grading is pure/testable; the ping is best-effort I/O.</summary>
public static class NetworkDuel
{
    /// <summary>Always-on reference points: low-latency public DNS that (usually) answer ICMP.
    /// They tell you your baseline route quality, independent of any game.</summary>
    public static IReadOnlyList<(string Target, string Host)> Baselines { get; } =
    [
        ("Cloudflare (baseline)", "1.1.1.1"),
        ("Google (baseline)", "8.8.8.8"),
    ];

    /// <summary>One publisher route-anchor per supported title. HONEST: these are the publisher's
    /// public CDN/site, NOT the actual match server (which is regional and usually blocks ICMP).
    /// It measures the quality of the path toward that ecosystem, which is the best a user-mode,
    /// no-admin tool can do safely. Keys match the KB `game` slugs.</summary>
    public static IReadOnlyDictionary<string, (string Target, string Host)> GamePublisher { get; } =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["valorant"] = ("Riot / Valorant (CDN)", "riotgames.com"),
            ["cs2"] = ("Steam / CS2 (CDN)", "steamcommunity.com"),
            ["fortnite"] = ("Epic / Fortnite (CDN)", "epicgames.com"),
            ["apex"] = ("EA / Apex (CDN)", "ea.com"),
            ["overwatch2"] = ("Blizzard / Overwatch 2 (CDN)", "blizzard.com"),
            ["thefinals"] = ("Embark / THE FINALS (sito)", "embark-studios.com"),
            ["r6siege"] = ("Ubisoft / Rainbow Six (CDN)", "ubisoft.com"),
            ["warzone"] = ("Activision / Warzone (CDN)", "callofduty.com"),
        };

    /// <summary>Default anchor set (no game chosen): baselines + the three most common ecosystems.
    /// Honest: it's your route quality, not a specific match server.</summary>
    public static IReadOnlyList<(string Target, string Host)> Anchors { get; } =
    [
        .. Baselines,
        GamePublisher["valorant"],
        GamePublisher["cs2"],
        GamePublisher["fortnite"],
    ];

    /// <summary>Game-aware anchors: the baselines plus THAT title's publisher route-anchor.
    /// Unknown/empty game falls back to the default <see cref="Anchors"/> set. Pure and testable.</summary>
    public static IReadOnlyList<(string Target, string Host)> AnchorsFor(string? game)
    {
        if (game is { Length: > 0 } g && GamePublisher.TryGetValue(g, out var pub))
            return [.. Baselines, pub];
        return Anchors;
    }

    /// <summary>Best-effort ICMP ping. Returns one entry per attempt: the RTT in ms, or null on
    /// timeout/error (counted as loss). Pure-ish wrapper around Ping; no analysis here.</summary>
    public static IReadOnlyList<long?> PingHost(string host, int count, int timeoutMs = 1000)
    {
        var rtts = new List<long?>(count);
        using var ping = new Ping();
        for (int i = 0; i < count; i++)
        {
            try
            {
                var reply = ping.Send(host, timeoutMs);
                rtts.Add(reply.Status == IPStatus.Success ? reply.RoundtripTime : null);
            }
            catch { rtts.Add(null); }
        }
        return rtts;
    }

    /// <summary>Pure grading from a set of ping samples (null = lost).</summary>
    public static NetworkResult Analyze(string target, string host, IReadOnlyList<long?> rtts)
    {
        int sent = rtts.Count;
        var got = rtts.Where(r => r.HasValue).Select(r => (double)r!.Value).ToList();
        int received = got.Count;
        double loss = sent == 0 ? 100 : (sent - received) * 100.0 / sent;
        double avg = received == 0 ? 0 : got.Average();

        // Jitter = mean absolute difference between consecutive successful pings.
        double jitter = 0;
        if (received > 1)
        {
            double sum = 0;
            for (int i = 1; i < got.Count; i++) sum += Math.Abs(got[i] - got[i - 1]);
            jitter = sum / (got.Count - 1);
        }

        var (grade, color) = Grade(received, avg, jitter, loss);
        return new NetworkResult(target, host, sent, received, avg, jitter, loss, grade, color);
    }

    private static (string Grade, string Color) Grade(int received, double avg, double jitter, double loss)
    {
        if (received == 0) return ("F — nessuna risposta", "Danger");
        if (loss >= 5) return ("F — perdita pacchetti", "Danger");
        if (avg < 20 && jitter < 3 && loss == 0) return ("A — ottimo", "Ok");
        if (avg < 40 && jitter < 6 && loss < 2) return ("B — buono", "Ok");
        if (avg < 70 && jitter < 12 && loss < 5) return ("C — discreto", "Warn");
        if (avg < 120) return ("D — mediocre", "Warn");
        return ("F — scarso", "Danger");
    }
}
