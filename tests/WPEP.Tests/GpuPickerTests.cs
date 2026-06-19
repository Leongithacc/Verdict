using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class GpuPickerTests
{
    [Fact]
    public void PrefersDiscreteOverIntegrated_RealLeonCase()
    {
        // Léon's actual machine: 9800X3D iGPU listed first, RTX 5080 second.
        var best = GpuPicker.Best(["AMD Radeon(TM) Graphics", "NVIDIA GeForce RTX 5080"]);
        Assert.Equal("NVIDIA GeForce RTX 5080", best);
    }

    [Fact]
    public void PrefersDiscrete_RegardlessOfOrder()
    {
        Assert.Equal("NVIDIA GeForce RTX 5080",
            GpuPicker.Best(["NVIDIA GeForce RTX 5080", "AMD Radeon(TM) Graphics"]));
    }

    [Theory]
    [InlineData("NVIDIA GeForce GTX 1660")]
    [InlineData("AMD Radeon RX 7900 XTX")]
    [InlineData("Intel Arc A770")]
    public void RecognizesDiscreteFamilies(string discrete)
    {
        Assert.Equal(discrete, GpuPicker.Best(["Intel UHD Graphics 770", discrete]));
    }

    [Fact]
    public void OnlyIntegrated_FallsBackToIt()
    {
        Assert.Equal("AMD Radeon(TM) Graphics", GpuPicker.Best(["AMD Radeon(TM) Graphics"]));
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        Assert.Equal("", GpuPicker.Best([]));
    }
}
