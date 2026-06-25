using WPEP.Core.Update;
using Xunit;

namespace WPEP.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("1.1", "1.0", 1)]    // newer minor
    [InlineData("1.0", "1.1", -1)]   // older minor
    [InlineData("1.0", "1.0", 0)]    // equal
    [InlineData("1.0", "1.0.0", 0)]  // missing parts treated as 0
    [InlineData("1.0.1", "1.0", 1)]  // patch bump
    [InlineData("2.0", "1.9", 1)]    // major beats minor
    [InlineData("v1.2", "1.1", 1)]   // tolerates leading 'v'
    [InlineData("1.2-beta", "1.2", 0)] // drops prerelease suffix → equal numeric
    [InlineData("10.0", "9.0", 1)]   // numeric, not lexicographic (10 > 9)
    public void VersionCompare_orders_correctly(string a, string b, int expectedSign)
        => Assert.Equal(expectedSign, Math.Sign(UpdateChecker.VersionCompare(a, b)));

    [Fact]
    public async Task CheckAsync_with_no_source_is_not_configured_and_hits_no_network()
    {
        // A null source models "no host chosen" — must short-circuit honestly, no network.
        var info = await UpdateChecker.CheckAsync("1.0", source: null);
        Assert.False(info.Configured);
        Assert.False(info.UpdateAvailable);
        Assert.Equal("1.0", info.CurrentVersion);
        Assert.Null(info.Error);
    }

    private sealed class FakeSource(UpdateInfo canned) : IUpdateSource
    {
        public Task<UpdateInfo> CheckAsync(string currentVersion, CancellationToken ct = default)
            => Task.FromResult(canned);
    }

    [Fact]
    public async Task CheckAsync_passes_through_a_configured_source()
    {
        var canned = new UpdateInfo(Configured: true, UpdateAvailable: true, "1.0", "1.3",
            "https://x/Verdict-1.3.zip", "note", null);
        var info = await UpdateChecker.CheckAsync("1.0", new FakeSource(canned));
        Assert.True(info.Configured);
        Assert.True(info.UpdateAvailable);
        Assert.Equal("1.3", info.LatestVersion);
        Assert.Equal("https://x/Verdict-1.3.zip", info.DownloadUrl);
    }

    [Fact]
    public void Host_is_wired_to_a_release_source()
    {
        // The update host has been configured (GitHub Releases) → the default source exists.
        Assert.True(UpdateConfig.IsConfigured);
        Assert.NotNull(UpdateChecker.DefaultSource());
    }

    [Fact]
    public void Parse_flags_a_newer_release_and_prefers_the_zip_asset()
    {
        const string json = """
        {
          "tag_name": "v1.2",
          "html_url": "https://github.com/x/y/releases/tag/v1.2",
          "body": "Note di rilascio",
          "assets": [
            { "name": "checksums.txt", "browser_download_url": "https://x/checksums.txt" },
            { "name": "Verdict-1.2.zip", "browser_download_url": "https://x/Verdict-1.2.zip" }
          ]
        }
        """;
        var info = GitHubReleaseSource.Parse(json, "1.0");
        Assert.True(info.Configured);
        Assert.True(info.UpdateAvailable);
        Assert.Equal("1.2", info.LatestVersion);
        Assert.Equal("https://x/Verdict-1.2.zip", info.DownloadUrl);
        Assert.Equal("Note di rilascio", info.ReleaseNotes);
        Assert.Null(info.Error);
    }

    [Fact]
    public void Parse_does_not_flag_an_update_when_current_is_same_or_newer()
    {
        const string json = """{ "tag_name": "1.0", "html_url": "https://x/rel" }""";
        Assert.False(GitHubReleaseSource.Parse(json, "1.0").UpdateAvailable); // equal
        Assert.False(GitHubReleaseSource.Parse(json, "2.0").UpdateAvailable); // we're ahead
    }

    [Fact]
    public void Parse_falls_back_to_release_page_when_no_zip_asset()
    {
        const string json = """{ "tag_name": "1.5", "html_url": "https://x/releases/tag/1.5" }""";
        var info = GitHubReleaseSource.Parse(json, "1.0");
        Assert.True(info.UpdateAvailable);
        Assert.Equal("https://x/releases/tag/1.5", info.DownloadUrl);
    }
}
