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
    public async Task CheckAsync_is_not_configured_by_default_and_makes_no_network_call()
    {
        // With UpdateConfig owner/repo empty, the façade must short-circuit honestly
        // (no host chosen yet) rather than hit the network.
        Assert.False(UpdateConfig.IsConfigured);
        var info = await UpdateChecker.CheckAsync("1.0");
        Assert.False(info.Configured);
        Assert.False(info.UpdateAvailable);
        Assert.Equal("1.0", info.CurrentVersion);
        Assert.Null(info.Error);
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
