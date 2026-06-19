using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class NetworkDuelTests
{
    private static List<long?> R(params long?[] v) => [.. v];

    [Fact]
    public void Analyze_LowLatencyNoJitter_GradesA()
    {
        var r = NetworkDuel.Analyze("t", "h", R(10, 10, 11, 10, 10));
        Assert.StartsWith("A", r.Grade);
        Assert.Equal(0, r.LossPercent);
        Assert.Equal("Ok", r.GradeColor);
    }

    [Fact]
    public void Analyze_AllLost_GradesF_NoResponse()
    {
        var r = NetworkDuel.Analyze("t", "h", R(null, null, null));
        Assert.Equal(0, r.Received);
        Assert.Equal(100, r.LossPercent);
        Assert.Contains("nessuna risposta", r.Grade);
        Assert.Equal("Danger", r.GradeColor);
    }

    [Fact]
    public void Analyze_PacketLoss_GradesF()
    {
        // 2 of 10 lost = 20% loss → F regardless of latency.
        var r = NetworkDuel.Analyze("t", "h", R(10, 10, null, 10, 10, 10, 10, 10, null, 10));
        Assert.Equal(20, r.LossPercent);
        Assert.StartsWith("F", r.Grade);
    }

    [Fact]
    public void Analyze_ComputesJitterFromConsecutiveDiffs()
    {
        // diffs: |20-10|=10, |10-20|=10 → jitter 10
        var r = NetworkDuel.Analyze("t", "h", R(10, 20, 10));
        Assert.Equal(10, r.JitterMs, 1);
    }

    [Fact]
    public void Analyze_HighLatency_GradesDown()
    {
        var r = NetworkDuel.Analyze("t", "h", R(90, 95, 92, 88));
        Assert.StartsWith("D", r.Grade);
    }

    [Fact]
    public void Anchors_AreNonEmpty()
    {
        Assert.NotEmpty(NetworkDuel.Anchors);
        Assert.All(NetworkDuel.Anchors, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Target));
            Assert.False(string.IsNullOrWhiteSpace(a.Host));
        });
    }
}
