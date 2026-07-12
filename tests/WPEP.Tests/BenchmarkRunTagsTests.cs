using System.Text.Json;
using WPEP.Core.Benchmark;
using Xunit;

namespace WPEP.Tests;

/// <summary>RunTags è opzionale su BenchmarkRun: le run salvate PRIMA della feature (nessun campo
/// "Tags" nel JSON) devono deserializzare con Tags=null, non crashare — la pagina Storico legge run
/// vecchie e nuove insieme.</summary>
public class BenchmarkRunTagsTests
{
    private static BenchmarkRun Sample(RunTags? tags = null) => new(
        "baseline", "valorant.exe", DateTimeOffset.UnixEpoch, 30,
        new RunMetrics(10_000, 0, 6.9, 6.8, 9.9, 12.0, null),
        [6.8, 6.9, 7.0], Environment: null, Tags: tags);

    [Fact]
    public void RoundTrip_WithTags_Preserved()
    {
        var run = Sample(new RunTags("valorant", "windows-game-mode", "post"));
        var back = JsonSerializer.Deserialize<BenchmarkRun>(JsonSerializer.Serialize(run))!;
        Assert.Equal("valorant", back.Tags!.Game);
        Assert.Equal("windows-game-mode", back.Tags.TweakId);
        Assert.Equal("post", back.Tags.Phase);
    }

    [Fact]
    public void RoundTrip_NoTags_IsNull()
    {
        var back = JsonSerializer.Deserialize<BenchmarkRun>(JsonSerializer.Serialize(Sample()))!;
        Assert.Null(back.Tags);
    }

    [Fact]
    public void LegacyJson_WithoutTagsOrEnvironment_DeserializesWithNulls()
    {
        // JSON di una run salvata prima di RunTags/Environment: campi assenti → default null, no crash.
        const string legacy =
            """
            {
              "Label": "baseline",
              "ProcessName": "valorant.exe",
              "CapturedAtUtc": "1970-01-01T00:00:00+00:00",
              "RequestedSeconds": 30,
              "Metrics": {
                "FrameCount": 10000, "ExcludedNonApplicationFrames": 0,
                "AvgFrameTimeMs": 6.9, "MedianFrameTimeMs": 6.8,
                "P99FrameTimeMs": 9.9, "P998FrameTimeMs": 12.0, "AvgGpuBusyMs": null
              },
              "FrameTimesMs": [6.8, 6.9, 7.0]
            }
            """;
        var back = JsonSerializer.Deserialize<BenchmarkRun>(legacy)!;
        Assert.Null(back.Tags);
        Assert.Null(back.Environment);
        Assert.Equal("valorant.exe", back.ProcessName);
        Assert.Equal(6.8, back.Metrics.MedianFrameTimeMs);
    }
}
