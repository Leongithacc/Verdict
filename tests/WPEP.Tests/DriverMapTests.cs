using WPEP.Core.Diagnostics;
using WPEP.Diagnostics;
using Xunit;

namespace WPEP.Tests;

public class DriverMapTests
{
    private static readonly DriverMap Map = new([
        new DriverModule("a.sys", 0x1000, 0x1000),
        new DriverModule("b.sys", 0x5000, 0x2000),
        new DriverModule("c.sys", 0xA000, 0x500),
    ]);

    [Theory]
    [InlineData(0x1000, "a.sys")] // exact base
    [InlineData(0x1FFF, "a.sys")] // last byte
    [InlineData(0x5800, "b.sys")] // middle
    [InlineData(0xA4FF, "c.sys")]
    public void Resolve_AddressInsideModule_ReturnsModule(ulong address, string expected)
    {
        Assert.Equal(expected, Map.Resolve(address)?.Name);
    }

    [Theory]
    [InlineData(0x0500)]  // below all
    [InlineData(0x2000)]  // gap after a.sys
    [InlineData(0x7000)]  // gap after b.sys
    [InlineData(0xB000)]  // above all
    public void Resolve_AddressOutsideModules_ReturnsNull(ulong address)
    {
        Assert.Null(Map.Resolve(address));
    }

    [Fact]
    public void Constructor_DiscardsZeroBaseAndZeroSizeModules()
    {
        var map = new DriverMap([
            new DriverModule("zero-base.sys", 0, 0x1000),
            new DriverModule("zero-size.sys", 0x1000, 0),
        ]);
        Assert.Equal(0, map.Count);
    }
}
