namespace WPEP.Core.Bios;

/// <summary>
/// Maps a BIOS-level manual tweak to its on-phone guide page (GitHub Pages), deep-linked to the
/// detected motherboard vendor and language. The QR in the app encodes <see cref="Url"/>; the page
/// is reachable from the phone even while the PC is in the BIOS — which a local server couldn't be.
/// Single source of the site URL: change <see cref="SiteBaseUrl"/> here if the host ever moves.
/// </summary>
public static class BiosGuide
{
    public const string SiteBaseUrl = "https://leongithacc.github.io/Verdict";

    // Tweak ids that have a VERIFIED per-vendor BIOS guide page. Must match the keys in
    // site/bios.html AND the ids in the Knowledge Base.
    private static readonly HashSet<string> Guided = new(StringComparer.OrdinalIgnoreCase)
    {
        "xmp-expo-enable",
        "resizable-bar-enable",
        "amd-ftpm-bios-update",
        "pbo-curve-optimizer",
    };

    public static bool HasGuide(string tweakId) => Guided.Contains(tweakId);

    /// <summary>Vendor slug ("asus"/"msi"/"gigabyte"/"asrock") from a Win32_BaseBoard manufacturer
    /// string, or null if unknown — in which case the page shows a vendor picker instead of guessing.</summary>
    public static string? VendorSlug(string? manufacturer)
    {
        var m = (manufacturer ?? "").ToLowerInvariant();
        if (m.Contains("asus")) return "asus";
        if (m.Contains("micro-star") || m.Contains("msi")) return "msi";
        if (m.Contains("gigabyte")) return "gigabyte";
        if (m.Contains("asrock")) return "asrock";
        return null;
    }

    /// <summary>The full URL the QR encodes. Vendor omitted when unknown (page shows a picker).</summary>
    public static string Url(string tweakId, string? vendorSlug, string lang = "it")
    {
        var url = $"{SiteBaseUrl}/bios.html?t={Uri.EscapeDataString(tweakId)}";
        if (!string.IsNullOrEmpty(vendorSlug))
            url += $"&v={vendorSlug}";
        url += $"&lang={(lang == "en" ? "en" : "it")}";
        return url;
    }
}
