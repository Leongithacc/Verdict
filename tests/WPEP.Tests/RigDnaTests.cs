using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class RigDnaTests
{
    private static HardwareInventory Inv(
        string mobo = "ASUS X870", string cpu = "Ryzen 7 9800X3D", int cores = 8, int threads = 16,
        double ram = 32, string gpu = "NVIDIA GeForce RTX 5080", bool? expo = true,
        string disk = "Samsung 990 PRO", string media = "SSD") => new(
        Motherboard: mobo, Chipset: "X870", Bios: "1804", BiosDate: "2025",
        Cpu: cpu, Cores: cores, Threads: threads, RamTotalGb: ram,
        Memory: [], Disks: [new DiskInfo(disk, ram, media)], Gpus: [gpu],
        Findings: [], ExpoEnabled: expo);

    [Fact]
    public void Compute_IsDeterministic()
    {
        var a = RigDna.Compute(Inv());
        var b = RigDna.Compute(Inv());
        Assert.Equal(a.Code, b.Code);
        Assert.Equal(a.Hue, b.Hue);
        Assert.Equal(a.Tier, b.Tier);
    }

    [Fact]
    public void DifferentHardware_DifferentCode()
    {
        var a = RigDna.Compute(Inv(cpu: "Ryzen 5 7600"));
        var b = RigDna.Compute(Inv(cpu: "Ryzen 9 9950X"));
        Assert.NotEqual(a.Code, b.Code);
    }

    [Fact]
    public void Code_HasExpectedShape()
    {
        var dna = RigDna.Compute(Inv());
        Assert.StartsWith("RIG-", dna.Code);
        Assert.Equal("RIG-XXXX-XXXX".Length, dna.Code.Length);
        Assert.InRange(dna.Hue, 0, 359);
    }

    [Fact]
    public void BeastBuild_RanksHigh()
    {
        // Léon's rig: 8C/16T, RTX 5080, 32GB, EXPO on, NVMe → top tiers.
        var dna = RigDna.Compute(Inv());
        Assert.Contains(dna.Tier, new[] { "MITICO", "LEGGENDARIO", "EPICO" });
    }

    [Fact]
    public void WeakBuild_RanksLow()
    {
        var dna = RigDna.Compute(Inv(cores: 2, threads: 4, ram: 8,
            gpu: "Intel UHD 630", expo: false, media: "HDD"));
        Assert.Contains(dna.Tier, new[] { "COMUNE", "RARO" });
    }

    [Fact]
    public void Traits_IncludeExpoState()
    {
        Assert.Contains("EXPO ✓", RigDna.Compute(Inv(expo: true)).Traits);
        Assert.Contains("EXPO ✗", RigDna.Compute(Inv(expo: false)).Traits);
    }
}
