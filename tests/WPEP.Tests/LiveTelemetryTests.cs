using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class LiveTelemetryTests
{
    [Theory]
    [InlineData(0UL, 100UL, 100.0)]   // nessun idle → 100% occupato
    [InlineData(100UL, 100UL, 0.0)]   // tutto idle → 0%
    [InlineData(50UL, 100UL, 50.0)]
    [InlineData(0UL, 0UL, 0.0)]       // intervallo nullo → 0 (niente divisione per zero)
    public void CpuLoad_Percent_FromDeltas(ulong idleDelta, ulong totalDelta, double expected)
        => Assert.Equal(expected, CpuLoad.Percent(idleDelta, totalDelta), 3);

    [Fact]
    public void CpuLoad_Percent_ClampsToZeroWhenIdleExceedsTotal()
        => Assert.Equal(0, CpuLoad.Percent(200, 100)); // caso impossibile → clamp, non negativo

    [Fact]
    public void WindowsSource_ReadsPlausibleRamAndCpu()
    {
        var src = new WindowsTelemetrySource();
        src.Sample();                        // prima lettura: stabilisce i contatori (CPU=0)
        System.Threading.Thread.Sleep(50);
        var s = src.Sample();

        Assert.True(s.RamTotalGb > 0, "RAM totale deve essere > 0");
        Assert.InRange(s.RamUsedGb, 0, s.RamTotalGb);
        Assert.InRange(s.CpuPercent, 0, 100);
        Assert.Null(s.Gpu);                  // NoGpuTelemetry di default in 3a
    }

    [Fact]
    public void NvidiaSmiCsv_ParsesFullLine()
    {
        var g = NvidiaSmiGpuTelemetry.ParseGpuCsv("45, 62, 2100");
        Assert.NotNull(g);
        Assert.Equal(45, g!.UtilPercent);
        Assert.Equal(62, g.TempC);
        Assert.Equal(2100, g.CoreClockMhz);
    }

    [Fact]
    public void NvidiaSmiCsv_HandlesNaFields()
    {
        var g = NvidiaSmiGpuTelemetry.ParseGpuCsv("[N/A], 55, 1500");
        Assert.NotNull(g);
        Assert.Null(g!.UtilPercent);
        Assert.Equal(55, g.TempC);
        Assert.Equal(1500, g.CoreClockMhz);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("45")]                       // troppo pochi campi
    [InlineData("[N/A], [N/A], [N/A]")]      // tutti non-numerici
    public void NvidiaSmiCsv_ReturnsNullOnUnusable(string? line)
        => Assert.Null(NvidiaSmiGpuTelemetry.ParseGpuCsv(line));
}
