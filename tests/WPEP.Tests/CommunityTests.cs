using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class CommunityTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"verdict-evidence-{Guid.NewGuid():N}.json");

    private static EvidenceRecord Rec(string outcome, double? delta = null) =>
        new("sig-abc", "high-end", "xmp-expo-enable", outcome, delta, "2026-06-26T10:00:00Z");

    private sealed class FakeBackend(CommunityStats? stats) : ICommunityBackend
    {
        public string Name => "fake-remote";
        public bool IsConfigured => true;
        public Task SubmitAsync(IReadOnlyList<EvidenceRecord> records, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CommunityStats?> QueryAsync(string tweakId, string rigTier, CancellationToken ct = default)
            => Task.FromResult(stats);
    }

    [Fact]
    public void Ledger_appends_and_round_trips()
    {
        var path = TempPath();
        try
        {
            EvidenceLedger.Append(Rec("helped", 4.2), path);
            EvidenceLedger.Append(Rec("no-effect"), path);
            var all = EvidenceLedger.Load(path);
            Assert.Equal(2, all.Count);
            Assert.Equal("xmp-expo-enable", all[0].TweakId);
            Assert.Equal(4.2, all[0].DeltaPercent);
            Assert.Null(all[1].DeltaPercent);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_is_empty_not_a_throw()
        => Assert.Empty(EvidenceLedger.Load(TempPath()));

    [Fact]
    public void Aggregate_uses_only_measured_outcomes_for_percentages()
    {
        // 3 helped + 1 no-effect = 4 measured; the "applied" record is NOT counted in the percentages.
        var s = EvidenceLedger.Aggregate(
            [Rec("helped"), Rec("helped"), Rec("helped"), Rec("no-effect"), Rec("applied")]);
        Assert.Equal(4, s.SampleSize);
        Assert.Equal(75, s.HelpedPercent);
        Assert.Equal(25, s.NoEffectPercent);
        Assert.Equal(0, s.HurtPercent);
        Assert.Contains("75%", s.Headline);
    }

    [Fact]
    public void Aggregate_empty_is_all_zero()
    {
        var s = EvidenceLedger.Aggregate([]);
        Assert.Equal(0, s.SampleSize);
        Assert.Equal("nessun dato", s.Headline);
    }

    [Fact]
    public async Task LocalOnly_backend_is_not_configured_and_returns_no_stats()
    {
        var b = new LocalOnlyBackend();
        Assert.False(b.IsConfigured);
        Assert.Null(await b.QueryAsync("xmp-expo-enable", "high-end"));
    }

    [Fact]
    public async Task Service_community_stats_is_null_when_not_configured()
    {
        var svc = new CommunityService(); // local-only by default
        Assert.False(svc.CommunityActive);
        Assert.Null(await svc.CommunityStatsAsync("xmp-expo-enable", "high-end"));
    }

    [Fact]
    public async Task Service_passes_through_a_configured_backend()
    {
        var stats = new CommunityStats(120, 73, 22, 5);
        var svc = new CommunityService(new FakeBackend(stats));
        Assert.True(svc.CommunityActive);
        var got = await svc.CommunityStatsAsync("xmp-expo-enable", "high-end");
        Assert.Equal(73, got!.HelpedPercent);
        Assert.Contains("73%", got.Headline);
    }
}
