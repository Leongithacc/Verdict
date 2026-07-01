using WPEP.Advisor;
using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class SessionModeTests
{
    [Fact]
    public void KnownNoiseProcesses_is_curated_not_empty()
    {
        Assert.NotEmpty(GamingSession.KnownNoiseProcesses);
        // Sanity: la lista deve includere almeno Discord, OneDrive, Dropbox.
        Assert.Contains("Discord", GamingSession.KnownNoiseProcesses);
        Assert.Contains("OneDrive", GamingSession.KnownNoiseProcesses);
        Assert.Contains("Dropbox", GamingSession.KnownNoiseProcesses);
    }

    [Fact]
    public void Fresh_session_is_inactive_and_empty()
    {
        var s = new GamingSession();
        Assert.False(s.IsActive);
        Assert.Empty(s.TouchedProcesses);
    }

    [Fact]
    public void Stop_on_never_started_is_noop()
    {
        var s = new GamingSession();
        int restored = s.Stop();
        Assert.Equal(0, restored);
        Assert.False(s.IsActive);
    }

    [Fact]
    public void MacroCategory_maps_all_known_kb_categories()
    {
        // Ogni categoria KB nota deve avere un bucket assegnato.
        Assert.Equal(MacroCategory.FpsLatency, MacroCategory.Bucket("power"));
        Assert.Equal(MacroCategory.FpsLatency, MacroCategory.Bucket("gpu"));
        Assert.Equal(MacroCategory.FpsLatency, MacroCategory.Bucket("scheduler"));
        Assert.Equal(MacroCategory.FpsLatency, MacroCategory.Bucket("input"));
        Assert.Equal(MacroCategory.NetworkPing, MacroCategory.Bucket("network"));
        Assert.Equal(MacroCategory.Background, MacroCategory.Bucket("background"));
        Assert.Equal(MacroCategory.StabilityQoL, MacroCategory.Bucket("security"));
    }

    [Fact]
    public void MacroCategory_case_insensitive_and_null_safe()
    {
        Assert.Equal(MacroCategory.FpsLatency, MacroCategory.Bucket("POWER"));
        Assert.Equal(MacroCategory.FpsLatency, MacroCategory.Bucket("Power"));
        Assert.Equal(MacroCategory.StabilityQoL, MacroCategory.Bucket(""));    // unknown → catch-all
        Assert.Equal(MacroCategory.StabilityQoL, MacroCategory.Bucket(null!)); // null → catch-all
    }

    [Fact]
    public void KnownNoiseProcesses_names_have_no_exe_suffix()
    {
        // Process.ProcessName restituisce SENZA .exe: se un entry ha .exe qui, il match fallirebbe silenzioso.
        foreach (var name in GamingSession.KnownNoiseProcesses)
            Assert.DoesNotContain(".exe", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnownNoiseProcesses_no_duplicates_case_insensitive()
    {
        var set = GamingSession.KnownNoiseProcesses
            .Select(n => n.ToLowerInvariant())
            .ToHashSet();
        Assert.Equal(GamingSession.KnownNoiseProcesses.Count, set.Count);
    }

    [Fact]
    public void KnownNoiseProcesses_names_are_trimmed_nonempty()
    {
        foreach (var name in GamingSession.KnownNoiseProcesses)
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.Equal(name.Trim(), name);
        }
    }

    [Fact]
    public void KnownNoiseProcesses_covers_the_documented_families()
    {
        // Il doc VS_HONE.md §3.3 promette coverage esplicita di queste famiglie.
        // Se qualcuno rimuove una entry, il claim nel doc va rivisto insieme.
        Assert.Contains("DiscordCanary", GamingSession.KnownNoiseProcesses);
        Assert.Contains("DiscordPTB", GamingSession.KnownNoiseProcesses);
        Assert.Contains("GoogleDriveFS", GamingSession.KnownNoiseProcesses);
        Assert.Contains("Spotify", GamingSession.KnownNoiseProcesses);
        Assert.Contains("EpicGamesLauncher", GamingSession.KnownNoiseProcesses);
    }

    [Fact]
    public void OriginalState_record_supports_value_equality()
    {
        // Record C# = strutturale. Serve al restore (comparazione di identità della sessione).
        var a = new GamingSession.OriginalState("Discord", 1234, System.Diagnostics.ProcessPriorityClass.Normal);
        var b = new GamingSession.OriginalState("Discord", 1234, System.Diagnostics.ProcessPriorityClass.Normal);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
