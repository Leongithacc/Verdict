namespace WPEP.SystemAnalyzer;

/// <summary>The generated identity of a rig: a stable shareable code, a trading-card tier, a few
/// traits, and a hue to tint the card. Deterministic — same hardware always yields the same DNA.</summary>
public sealed record RigDnaResult(
    string Code, string Tier, string TierColor, IReadOnlyList<string> Traits, int Hue);

/// <summary>Rig DNA (Lab feature): turns the hardware inventory into a unique, collectible
/// "fingerprint" — a memorable code + a trading-card tier derived from the build's muscle. Pure
/// and deterministic (FNV-1a hash, no randomness), so it's testable and stable across runs. Pure
/// fun + flex; touches nothing on the system.</summary>
public static class RigDna
{
    // Crockford-ish base32 alphabet (no I/L/O/U to avoid confusion when sharing the code).
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static RigDnaResult Compute(HardwareInventory inv)
    {
        string gpu = inv.Gpus.FirstOrDefault() ?? "";
        // Canonical signature: the parts that define THIS build. Stable ordering, normalized.
        string canonical = string.Join("|",
            inv.Motherboard, inv.Cpu, inv.Cores?.ToString() ?? "?", gpu,
            inv.RamTotalGb?.ToString("F0") ?? "?",
            inv.Disks.FirstOrDefault()?.Model ?? "").ToUpperInvariant();

        uint h = Fnv1a(canonical);
        string code = "RIG-" + Encode(h, 4) + "-" + Encode(h >> 7 | h << 25, 4);
        int hue = (int)(h % 360);
        var (tier, color) = TierOf(inv, gpu);
        var traits = BuildTraits(inv, gpu);
        return new RigDnaResult(code, tier, color, traits, hue);
    }

    /// <summary>Power heuristic → trading-card tier. Counts real muscle: many cores, a top GPU,
    /// plenty of fast RAM with EXPO on, NVMe storage.</summary>
    private static (string Tier, string Color) TierOf(HardwareInventory inv, string gpu)
    {
        int p = 0;
        if (inv.Cores >= 8) p++;
        if (inv.Cores >= 12) p++;
        string g = gpu.ToUpperInvariant();
        if (g.Contains("RTX") || g.Contains("RX 7") || g.Contains("RX 9")) p++;
        if (g.Contains("RTX 40") || g.Contains("RTX 50") || g.Contains("4080") || g.Contains("4090") ||
            g.Contains("5080") || g.Contains("5090")) p++;
        if (inv.RamTotalGb >= 32) p++;
        if (inv.ExpoEnabled == true) p++;
        if (inv.Disks.Any(d => d.Media.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                               d.Model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))) p++;

        return p switch
        {
            >= 6 => ("MITICO", "Accent"),
            5 => ("LEGGENDARIO", "Warn"),
            4 => ("EPICO", "Ok"),
            >= 2 => ("RARO", "Info"),
            _ => ("COMUNE", "Neutral"),
        };
    }

    private static List<string> BuildTraits(HardwareInventory inv, string gpu)
    {
        var t = new List<string>();
        if (inv.Cores is { } c) t.Add($"{c}-Core" + (inv.Threads is { } th ? $"/{th}T" : ""));
        if (gpu.Length > 0) t.Add(ShortGpu(gpu));
        if (inv.RamTotalGb is { } r) t.Add($"{r:F0}GB RAM");
        t.Add(inv.ExpoEnabled switch { true => "EXPO ✓", false => "EXPO ✗", _ => "EXPO ?" });
        return t;
    }

    /// <summary>Trims vendor noise from a GPU string: "NVIDIA GeForce RTX 5080" → "RTX 5080".</summary>
    private static string ShortGpu(string gpu)
    {
        foreach (var noise in new[] { "NVIDIA", "GeForce", "AMD", "Radeon", "Intel", "(R)", "(TM)" })
            gpu = gpu.Replace(noise, "", StringComparison.OrdinalIgnoreCase);
        return string.Join(' ', gpu.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static uint Fnv1a(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return h;
    }

    private static string Encode(uint value, int chars)
    {
        var sb = new System.Text.StringBuilder(chars);
        for (int i = 0; i < chars; i++) { sb.Append(Alphabet[(int)(value & 31)]); value >>= 5; }
        return sb.ToString();
    }
}
