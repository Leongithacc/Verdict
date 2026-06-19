using System.Management;

namespace WPEP.SystemAnalyzer;

/// <summary>A program that runs at startup.</summary>
public sealed record StartupItem(string Name, string Command, string Location, bool IsMicrosoft);

/// <summary>How far this Windows has drifted from a clean install, startup-wise.</summary>
public sealed record FreshInstallReport(
    int Score, string Band, string BandColor,
    int ThirdPartyCount, int MicrosoftCount,
    IReadOnlyList<StartupItem> ThirdParty, string Headline);

/// <summary>Fresh-install score (Lab feature): compares your machine to a clean Windows by counting
/// the THIRD-PARTY programs you've added to startup. Driver-free (WMI Win32_StartupCommand), read
/// only. Honest framing: we can't know your exact fresh baseline, so we count third-party autostart
/// entries (the real drift) rather than pretending to diff against a specific clean image.</summary>
public static class FreshInstallScanner
{
    public static IReadOnlyList<StartupItem> EnumerateStartup()
    {
        var list = new List<StartupItem>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Command, Location FROM Win32_StartupCommand");
            foreach (var o in searcher.Get())
            {
                string name = o["Name"]?.ToString() ?? "";
                string cmd = o["Command"]?.ToString() ?? "";
                string loc = o["Location"]?.ToString() ?? "";
                list.Add(new StartupItem(name, cmd, loc, IsMicrosoft(cmd, name)));
            }
        }
        catch { /* WMI unavailable → empty, never invented */ }
        return list;
    }

    /// <summary>Heuristic: an entry living under Windows/system dirs or clearly published by
    /// Microsoft counts as part of the base OS; everything else is something the user added.</summary>
    public static bool IsMicrosoft(string command, string name)
    {
        string c = command.ToLowerInvariant();
        if (c.Contains(@"\windows\") || c.Contains("system32") || c.Contains("syswow64") ||
            c.Contains("microsoft") || c.Contains("windowsapps"))
            return true;
        string n = name.ToLowerInvariant();
        return n is "securityhealth" or "windows security notification icon";
    }

    /// <summary>Pure, testable scoring from the startup list.</summary>
    public static FreshInstallReport Analyze(IReadOnlyList<StartupItem> items)
    {
        var thirdParty = items.Where(i => !i.IsMicrosoft).ToList();
        int n = thirdParty.Count;
        int ms = items.Count - n;
        int score = Math.Clamp(100 - n * 6, 0, 100);
        var (band, color) = score switch
        {
            >= 85 => ("Pulito", "Ok"),
            >= 60 => ("Normale", "Ok"),
            >= 35 => ("Affollato", "Warn"),
            _ => ("Sovraccarico", "Danger"),
        };
        string headline = n == 0
            ? "Avvio pulito: nessun programma di terze parti parte con Windows. Come appena installato."
            : $"Hai {n} programm{(n == 1 ? "a" : "i")} di terze parti all'avvio oltre al Windows base. " +
              "Meno avvii = boot più rapido e meno roba in background mentre giochi.";
        return new FreshInstallReport(score, band, color, n, ms, thirdParty, headline);
    }
}
