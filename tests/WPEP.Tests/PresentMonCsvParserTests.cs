using WPEP.Benchmark;
using Xunit;

namespace WPEP.Tests;

public class PresentMonCsvParserTests
{
    private static PresentMonCsvParser.ParseResult Parse(string csv) =>
        PresentMonCsvParser.Parse(new StringReader(csv));

    [Fact]
    public void Parse_V2Header_ReadsFrameTimeAndGpuBusy()
    {
        var result = Parse(
            """
            Application,ProcessID,FrameTime,GPUBusy
            game.exe,123,6.94,5.10
            game.exe,123,7.06,5.30
            """);

        Assert.Equal(2, result.Samples.Count);
        Assert.Equal(6.94, result.Samples[0].FrameTimeMs);
        Assert.Equal(5.10, result.Samples[0].GpuBusyMs);
    }

    [Fact]
    public void Parse_V1Header_ReadsMsBetweenPresents()
    {
        var result = Parse(
            """
            Application,ProcessID,msBetweenPresents,msGPUActive
            game.exe,123,16.67,12.0
            """);

        var sample = Assert.Single(result.Samples);
        Assert.Equal(16.67, sample.FrameTimeMs);
        Assert.Equal(12.0, sample.GpuBusyMs);
    }

    [Fact]
    public void Parse_FrameTypeColumn_ExcludesGeneratedFramesAndCountsThem()
    {
        var result = Parse(
            """
            Application,FrameTime,FrameType
            game.exe,7.0,Application
            game.exe,3.5,Intel_XEFG
            game.exe,3.5,AMD_AFMF
            game.exe,7.2,NotSet
            """);

        Assert.Equal(2, result.Samples.Count);
        Assert.Equal(2, result.ExcludedNonApplicationFrames);
        Assert.All(result.Samples, s => Assert.True(s.FrameTimeMs > 5));
    }

    [Fact]
    public void Parse_NoGpuColumn_GpuBusyIsNull()
    {
        var result = Parse(
            """
            Application,FrameTime
            game.exe,8.33
            """);

        Assert.Null(Assert.Single(result.Samples).GpuBusyMs);
    }

    [Fact]
    public void Parse_MissingFrametimeColumn_ThrowsWithHeaderInMessage()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse("Application,ProcessID\na,1"));
        Assert.Contains("Application,ProcessID", ex.Message);
    }

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Parse(""));
    }
}
