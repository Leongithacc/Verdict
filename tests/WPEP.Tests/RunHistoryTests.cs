using System.Text.Json;
using WPEP.Benchmark;
using WPEP.Core.Benchmark;
using Xunit;

namespace WPEP.Tests;

public class RunHistoryTests
{
    private static BenchmarkRun Run(string process, DateTimeOffset at, double median) => new(
        "x", process, at, 30,
        new RunMetrics(1000, 0, median, median, median * 1.5, median * 2, null),
        [median], Environment: null, Tags: null);

    private static void WritePhase(string sessionDir, string phase, params BenchmarkRun[] runs)
    {
        var dir = Path.Combine(sessionDir, phase);
        Directory.CreateDirectory(dir);
        for (int i = 0; i < runs.Length; i++)
            File.WriteAllText(Path.Combine(dir, $"{phase}-{i:D2}.json"), JsonSerializer.Serialize(runs[i]));
    }

    [Fact]
    public void Load_MissingRoot_ReturnsEmpty()
    {
        Assert.Empty(RunHistory.Load(Path.Combine(Path.GetTempPath(), "wpep-nope-" + Guid.NewGuid())));
    }

    [Fact]
    public void Load_GroupsPhases_NewestFirst_WithVerdictFlagAndMedians()
    {
        var root = Path.Combine(Path.GetTempPath(), "wpep-hist-" + Guid.NewGuid());
        try
        {
            // sessione più vecchia: solo baseline → nessun verdetto
            WritePhase(Path.Combine(root, "wizard-1"), "baseline",
                Run("valorant.exe", DateTimeOffset.UnixEpoch, 7.0));
            // sessione più nuova: baseline + post → verdetto possibile
            WritePhase(Path.Combine(root, "wizard-2"), "baseline",
                Run("cs2.exe", DateTimeOffset.UnixEpoch.AddDays(1), 8.0));
            WritePhase(Path.Combine(root, "wizard-2"), "post",
                Run("cs2.exe", DateTimeOffset.UnixEpoch.AddDays(1), 6.0));

            var sessions = RunHistory.Load(root);

            Assert.Equal(2, sessions.Count);
            Assert.Equal("wizard-2", sessions[0].SessionId);   // newest first
            Assert.True(sessions[0].CanVerdict);
            Assert.False(sessions[1].CanVerdict);              // baseline-only
            Assert.Equal(8.0, sessions[0].BaselineMedianMs);
            Assert.Equal(6.0, sessions[0].PostMedianMs);
            Assert.Equal("cs2.exe", sessions[0].ProcessName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Load_SkipsSessionsWithNoRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), "wpep-hist-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "wizard-empty")); // nessuna fase
            WritePhase(Path.Combine(root, "wizard-real"), "baseline",
                Run("game.exe", DateTimeOffset.UnixEpoch, 5.0));

            var sessions = RunHistory.Load(root);

            Assert.Single(sessions);
            Assert.Equal("wizard-real", sessions[0].SessionId);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
