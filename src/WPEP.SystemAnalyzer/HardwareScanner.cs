using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WPEP.SystemAnalyzer;

public sealed record MemoryModule(string Slot, double CapacityGb, int? SpeedMtps, int? RatedMtps, string Vendor, string Part);
public sealed record DiskInfo(string Model, double CapacityGb, string Media);

/// <summary>A diagnostic judgment about the setup (the "trova problemi" part). Ok=good,
/// Warn=worth fixing, Info=neutral note.</summary>
public sealed record Finding(string Level, string Text);

/// <summary>A full, driver-free hardware inventory (V3 §1). Everything via WMI/CIM — no
/// kernel driver, so it's safe with anti-cheat (the project's golden rule). Deep live
/// sensors (VRM, fan RPM) are deliberately NOT here: they'd need a kernel driver.</summary>
public sealed record HardwareInventory(
    string Motherboard,
    string Chipset,
    string Bios,
    string BiosDate,
    string Cpu,
    int? Cores,
    int? Threads,
    double? RamTotalGb,
    IReadOnlyList<MemoryModule> Memory,
    IReadOnlyList<DiskInfo> Disks,
    IReadOnlyList<string> Gpus,
    IReadOnlyList<Finding> Findings,
    bool? ExpoEnabled)
{
    /// <summary>The GPU that actually matters for gaming: the discrete card, not the CPU's
    /// integrated graphics. Many AMD CPUs (e.g. 9800X3D) expose an iGPU that WMI lists first as
    /// "AMD Radeon(TM) Graphics" — picking that would mislabel the rig. Falls back to the first.</summary>
    public string PrimaryGpu => GpuPicker.Best(Gpus);
}

/// <summary>Chooses the discrete gaming GPU from a possibly-mixed list (iGPU + dGPU).</summary>
public static class GpuPicker
{
    public static string Best(IReadOnlyList<string> gpus)
    {
        if (gpus.Count == 0) return "";
        // 1) A clearly discrete card: RTX/GTX, Radeon RX <model>, or Intel Arc.
        var discrete = gpus.FirstOrDefault(g =>
            Regex.IsMatch(g, @"\b(RTX|GTX)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(g, @"\bRX\s?\d{3,4}\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(g, @"\bArc\b", RegexOptions.IgnoreCase));
        if (discrete is not null) return discrete;
        // 2) Anything that isn't an obvious integrated GPU ("…Graphics", "UHD", "Iris").
        var notIntegrated = gpus.FirstOrDefault(g => !IsIntegrated(g));
        return notIntegrated ?? gpus[0];
    }

    /// <summary>True for an obvious integrated GPU (iGPU): "…Graphics", Intel UHD/Iris.
    /// Ignora un eventuale suffisso "· N GB VRAM" così la classificazione regge sul nome arricchito.</summary>
    public static bool IsIntegrated(string g)
    {
        var name = g.Split('·')[0].TrimEnd();
        return name.Contains("UHD", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Graphics", StringComparison.OrdinalIgnoreCase);
    }
}

public static class HardwareScanner
{
    public static HardwareInventory Scan()
    {
        var (mb, chipset) = ReadBaseBoard();
        var (bios, biosDate) = ReadBios();
        var (cpu, cores, threads) = ReadCpu();
        var mem = ReadMemory();
        var disks = ReadDisks();
        var gpus = ReadGpus();
        return new HardwareInventory(
            Motherboard: mb,
            Chipset: chipset,
            Bios: bios,
            BiosDate: biosDate,
            Cpu: cpu,
            Cores: cores,
            Threads: threads,
            RamTotalGb: mem.Count > 0 ? mem.Sum(m => m.CapacityGb) : null,
            Memory: mem,
            Disks: disks,
            Gpus: gpus,
            Findings: ComputeFindings(mem, biosDate, gpus),
            ExpoEnabled: DetectExpo(mem));
    }

    /// <summary>EXPO/XMP state from the first module: true=running at (or near) rated speed,
    /// false=running below rated (profile off), null=can't tell (no speed or no rated info).
    /// Same heuristic the EXPO finding uses, exposed as a clean tri-state for the Verdict Score.</summary>
    public static bool? DetectExpo(IReadOnlyList<MemoryModule> mem)
    {
        var m0 = mem.FirstOrDefault();
        if (m0?.SpeedMtps is not { } cur) return null;
        int rated = Math.Max(m0.RatedMtps ?? 0, RatedFromPart(m0.Part) ?? 0);
        if (rated <= 0) return null;          // no rated reference → honest "unknown"
        return cur + 50 >= rated;             // within tolerance of rated = profile enabled
    }

    /// <summary>Honest, conservative diagnostics — only flag what we can actually tell.</summary>
    private static List<Finding> ComputeFindings(
        List<MemoryModule> mem, string biosDate, List<string> gpus)
    {
        var f = new List<Finding>();

        // RAM below its rated speed = XMP/EXPO not enabled. WMI rarely exposes the SPD/EXPO
        // max, but the kit's part number usually encodes it (e.g. ...6000...), so use that.
        var m0 = mem.FirstOrDefault();
        if (m0?.SpeedMtps is { } cur)
        {
            int rated = Math.Max(m0.RatedMtps ?? 0, RatedFromPart(m0.Part) ?? 0);
            if (rated > cur + 50)
                f.Add(new Finding("Warn",
                    $"RAM a {cur} MT/s ma il kit e rated {rated} — abilita EXPO/XMP nel BIOS (+{rated - cur} MT/s gratis)."));
            else if (cur >= 6000)
                f.Add(new Finding("Ok", $"RAM a {cur} MT/s (EXPO/XMP attivo)."));
        }

        // BIOS older than ~18 months: worth checking for an update.
        if (biosDate.Length >= 4 && int.TryParse(biosDate[..4], out var year) && year <= 2024)
            f.Add(new Finding("Info", $"BIOS del {biosDate}: valuta un aggiornamento se ce n'e uno piu recente."));

        if (gpus.Any(g => g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || g.Contains("GeForce", StringComparison.OrdinalIgnoreCase)))
            f.Add(new Finding("Ok", "GPU NVIDIA rilevata — Reflex / DLSS disponibili."));

        return f;
    }

    private static (string, string) ReadBaseBoard()
    {
        foreach (var o in Query("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
            return ($"{Str(o["Manufacturer"])} {Str(o["Product"])}".Trim(), "");
        return ("sconosciuta", "");
    }

    private static (string, string) ReadBios()
    {
        foreach (var o in Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
            return (Str(o["SMBIOSBIOSVersion"]), FormatWmiDate(Str(o["ReleaseDate"])));
        return ("?", "");
    }

    private static (string, int?, int?) ReadCpu()
    {
        foreach (var o in Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
            return (Str(o["Name"]).Trim(), ToInt(o["NumberOfCores"]), ToInt(o["NumberOfLogicalProcessors"]));
        return ("?", null, null);
    }

    private static List<MemoryModule> ReadMemory()
    {
        var list = new List<MemoryModule>();
        foreach (var o in Query("SELECT DeviceLocator, Capacity, ConfiguredClockSpeed, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory"))
        {
            double gb = (ToLong(o["Capacity"]) ?? 0) / 1024d / 1024d / 1024d;
            int? configured = ToInt(o["ConfiguredClockSpeed"]);
            int? rated = ToInt(o["Speed"]); // SPD-rated max (often the EXPO/XMP speed)
            list.Add(new MemoryModule(Str(o["DeviceLocator"]), Math.Round(gb),
                configured ?? rated, rated, Str(o["Manufacturer"]).Trim(), Str(o["PartNumber"]).Trim()));
        }
        return list;
    }

    private static List<DiskInfo> ReadDisks()
    {
        // Win32_DiskDrive.MediaType dice solo "Fixed hard disk media" anche per gli SSD: inutile.
        // Il tipo vero (SSD/HDD + NVMe/SATA) viene da MSFT_PhysicalDisk.
        var types = ReadDiskTypes();
        var list = new List<DiskInfo>();
        foreach (var o in Query("SELECT Model, Size FROM Win32_DiskDrive"))
        {
            double gb = (ToLong(o["Size"]) ?? 0) / 1024d / 1024d / 1024d;
            if (gb < 1) continue; // skip card readers / phantom drives
            var model = Str(o["Model"]).Trim();
            // Match deterministico: prima nome esatto (caso normale, Model == FriendlyName), poi
            // come fallback substring. Evita di accoppiare il disco sbagliato con modelli simili.
            var media = types.TryGetValue(model, out var exact) ? exact
                : types.FirstOrDefault(t =>
                    model.Contains(t.Key, StringComparison.OrdinalIgnoreCase) ||
                    t.Key.Contains(model, StringComparison.OrdinalIgnoreCase)).Value ?? "";
            list.Add(new DiskInfo(model, Math.Round(gb), media));
        }
        return list;
    }

    /// <summary>Tipo disco reale (FriendlyName → "NVMe SSD" / "SATA SSD" / "HDD") da
    /// MSFT_PhysicalDisk. Best-effort: se il namespace Storage non risponde, niente tipo.</summary>
    private static Dictionary<string, string> ReadDiskTypes()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in Query(@"root\Microsoft\Windows\Storage",
            "SELECT FriendlyName, MediaType, BusType FROM MSFT_PhysicalDisk"))
        {
            var name = Str(o["FriendlyName"]).Trim();
            string kind = (ToInt(o["MediaType"]) ?? 0) switch { 4 => "SSD", 3 => "HDD", _ => "" };
            string bus = (ToInt(o["BusType"]) ?? 0) switch { 17 => "NVMe", 11 => "SATA", 7 => "USB", _ => "" };
            string label = bus.Length > 0 && kind.Length > 0 ? $"{bus} {kind}"
                : kind.Length > 0 ? kind : bus;
            if (name.Length > 0 && label.Length > 0)
                map[name] = label;
        }
        return map;
    }

    private static List<string> ReadGpus()
    {
        var vram = ReadGpuVramMap();
        var list = new List<string>();
        string[] virt = ["Basic", "Virtual", "Meta", "Parsec", "Remote", "Mirror", "IddCx", "OBS"];
        foreach (var o in Query("SELECT Name FROM Win32_VideoController"))
        {
            var n = Str(o["Name"]).Trim();
            if (n.Length == 0 || virt.Any(v => n.Contains(v, StringComparison.OrdinalIgnoreCase)))
                continue;
            // Arricchisci col VRAM reale (registro qwMemorySize: corretto anche >4 GB, a differenza
            // di Win32_VideoController.AdapterRAM che è un uint32 e tronca a 4 GB).
            // ≥1 GB: gli iGPU hanno una carveout minuscola (sotto il GB) → niente "0 GB VRAM".
            if (vram.TryGetValue(n, out var bytes) && bytes >= 1024L * 1024 * 1024)
                n = $"{n}  ·  {Math.Round(bytes / 1024d / 1024d / 1024d)} GB VRAM";
            list.Add(n);
        }
        return list;
    }

    /// <summary>VRAM reale per GPU (nome → byte) dal registro: la classe display adapters espone
    /// HardwareInformation.qwMemorySize, accurato anche per schede &gt;4 GB. Best-effort: se non
    /// disponibile, i nomi restano semplici.</summary>
    private static Dictionary<string, long> ReadGpuVramMap()
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (cls is null) return map;
            foreach (var sub in cls.GetSubKeyNames())
            {
                if (!Regex.IsMatch(sub, @"^\d{4}$")) continue;
                using var k = cls.OpenSubKey(sub);
                if (k?.GetValue("DriverDesc") is string desc && desc.Trim().Length > 0 &&
                    k.GetValue("HardwareInformation.qwMemorySize") is { } qw &&
                    long.TryParse(qw.ToString(), out var bytes) && bytes > 0)
                    map[desc.Trim()] = bytes;
            }
        }
        catch { /* registro non leggibile: nessun VRAM, nomi semplici */ }
        return map;
    }

    private static IEnumerable<ManagementBaseObject> Query(string wql)
    {
        ManagementObjectCollection results;
        try { results = new ManagementObjectSearcher(wql).Get(); }
        catch { yield break; } // WMI hiccup must never crash the scan
        foreach (ManagementBaseObject o in results)
            yield return o;
    }

    /// <summary>Query in un namespace WMI specifico (es. root\Microsoft\Windows\Storage).</summary>
    private static IEnumerable<ManagementBaseObject> Query(string scope, string wql)
    {
        ManagementObjectCollection results;
        try { results = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql)).Get(); }
        catch { yield break; }
        foreach (ManagementBaseObject o in results)
            yield return o;
    }

    /// <summary>Pull the rated DDR5 speed out of a memory kit part number (e.g.
    /// "CMH32GX5M2E6000Z36" → 6000). Returns null if no plausible speed token is found.</summary>
    private static int? RatedFromPart(string part)
    {
        foreach (Match m in Regex.Matches(part, @"\d{4}"))
            if (int.TryParse(m.Value, out var v) && v is >= 4800 and <= 8400)
                return v;
        return null;
    }

    private static string Str(object? v) => v?.ToString() ?? "";
    private static int? ToInt(object? v) => v is null ? null : int.TryParse(v.ToString(), out var i) ? i : null;
    private static long? ToLong(object? v) => v is null ? null : long.TryParse(v.ToString(), out var l) ? l : null;

    /// <summary>WMI dates are "yyyymmddHHMMSS.ffffff+zzz" — keep the yyyy-mm-dd.</summary>
    private static string FormatWmiDate(string raw) =>
        raw.Length >= 8 ? $"{raw[..4]}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)}" : "";
}
