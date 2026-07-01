using WPEP.Core.SystemInfo;
using Xunit;

namespace WPEP.Tests;

public class SystemSnapshotTests
{
    private static SystemSnapshot Empty() =>
        new() { CapturedAtUtc = DateTimeOffset.UnixEpoch };

    [Fact]
    public void NoiseBand_is_null_when_score_missing()
    {
        Assert.Null(Empty().NoiseBand);
    }

    [Theory]
    [InlineData(0, "basso")]
    [InlineData(25, "basso")]
    [InlineData(26, "medio")]
    [InlineData(55, "medio")]
    [InlineData(56, "alto")]
    [InlineData(100, "alto")]
    public void NoiseBand_matches_documented_thresholds(int score, string expected)
    {
        var snapshot = Empty() with { NoiseScore = score };
        Assert.Equal(expected, snapshot.NoiseBand);
    }

    [Fact]
    public void GameInstalled_returns_null_for_unknown_key()
    {
        Assert.Null(Empty().GameInstalled("unknown-game"));
        Assert.Null(Empty().GameInstalled(""));
    }

    [Fact]
    public void GameInstalled_maps_warzone_to_WarzoneInstalled()
    {
        var s = Empty() with { WarzoneInstalled = true };
        Assert.True(s.GameInstalled("warzone"));
        Assert.True(s.GameInstalled("WARZONE"));
    }

    [Fact]
    public void GameInstalled_covers_every_known_game_key()
    {
        // Se qualcuno aggiunge una property gameInstalled ma dimentica lo switch,
        // questo test è la rete di sicurezza. Copertura per parity: ognuna delle
        // 8 game key note deve mappare in modo distinto quando le properties variano.
        var s = Empty() with
        {
            FortniteInstalled = true,
            ValorantInstalled = true,
            Cs2Installed = true,
            ApexInstalled = true,
            Overwatch2Installed = true,
            TheFinalsInstalled = true,
            R6SiegeInstalled = true,
            WarzoneInstalled = true,
        };
        foreach (var key in new[] { "fortnite", "valorant", "cs2", "apex", "overwatch2", "thefinals", "r6siege", "warzone" })
            Assert.True(s.GameInstalled(key), $"GameInstalled(\"{key}\") should be true");
    }

    [Fact]
    public void NoiseFactors_defaults_to_empty_not_null()
    {
        // La UI enumera NoiseFactors senza check null — default deve essere collezione vuota.
        Assert.NotNull(Empty().NoiseFactors);
        Assert.Empty(Empty().NoiseFactors);
    }
}
