using WPEP.Core.Bios;
using Xunit;

namespace WPEP.Tests;

public class BiosGuideTests
{
    [Theory]
    [InlineData("xmp-expo-enable", true)]
    [InlineData("resizable-bar-enable", true)]
    [InlineData("amd-ftpm-bios-update", true)]
    [InlineData("pbo-curve-optimizer", true)]
    [InlineData("nvidia-low-latency-ultra", false)]   // applicable, not a BIOS guide
    [InlineData("dns-change", false)]
    public void HasGuide_only_for_bios_tweaks(string id, bool expected)
        => Assert.Equal(expected, BiosGuide.HasGuide(id));

    [Theory]
    [InlineData("ASUSTeK COMPUTER INC.", "asus")]
    [InlineData("Micro-Star International Co., Ltd.", "msi")]
    [InlineData("MSI", "msi")]
    [InlineData("Gigabyte Technology Co., Ltd.", "gigabyte")]
    [InlineData("ASRock", "asrock")]
    [InlineData("Some Unknown OEM", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void VendorSlug_maps_known_manufacturers(string? mfr, string? expected)
        => Assert.Equal(expected, BiosGuide.VendorSlug(mfr));

    [Fact]
    public void Url_is_per_tweak_with_vendor_and_language()
    {
        var url = BiosGuide.Url("xmp-expo-enable", "asus", "it");
        Assert.StartsWith(BiosGuide.SiteBaseUrl + "/bios.html?", url);
        Assert.Contains("t=xmp-expo-enable", url);   // ← solo QUESTO tweak, non un indice
        Assert.Contains("v=asus", url);
        Assert.Contains("lang=it", url);
    }

    [Fact]
    public void Url_omits_vendor_when_unknown_and_defaults_lang_to_it()
    {
        var url = BiosGuide.Url("resizable-bar-enable", null);
        Assert.DoesNotContain("v=", url);
        Assert.Contains("lang=it", url);
    }
}
