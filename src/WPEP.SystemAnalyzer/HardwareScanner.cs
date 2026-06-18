using System.Management;

namespace WPEP.SystemAnalyzer;

public sealed record MemoryModule(string Slot, double CapacityGb, int? SpeedMtps, string Vendor, string Part);
public sealed record DiskInfo(string Model, double CapacityGb, string Media);

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
    IReadOnlyList<string> Gpus);

public static class HardwareScanner
{
    public static HardwareInventory Scan()
    {
        var (mb, chipset) = ReadBaseBoard();
        var (bios, biosDate) = ReadBios();
        var (cpu, cores, threads) = ReadCpu();
        var mem = ReadMemory();
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
            Disks: ReadDisks(),
            Gpus: ReadGpus());
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
            int? speed = ToInt(o["ConfiguredClockSpeed"]) ?? ToInt(o["Speed"]);
            list.Add(new MemoryModule(Str(o["DeviceLocator"]), Math.Round(gb), speed,
                Str(o["Manufacturer"]).Trim(), Str(o["PartNumber"]).Trim()));
        }
        return list;
    }

    private static List<DiskInfo> ReadDisks()
    {
        var list = new List<DiskInfo>();
        foreach (var o in Query("SELECT Model, Size, MediaType FROM Win32_DiskDrive"))
        {
            double gb = (ToLong(o["Size"]) ?? 0) / 1024d / 1024d / 1024d;
            if (gb < 1) continue; // skip card readers / phantom drives
            list.Add(new DiskInfo(Str(o["Model"]).Trim(), Math.Round(gb), Str(o["MediaType"]).Trim()));
        }
        return list;
    }

    private static List<string> ReadGpus()
    {
        var list = new List<string>();
        foreach (var o in Query("SELECT Name FROM Win32_VideoController"))
        {
            var n = Str(o["Name"]).Trim();
            if (n.Length > 0 && !n.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                list.Add(n);
        }
        return list;
    }

    private static IEnumerable<ManagementBaseObject> Query(string wql)
    {
        ManagementObjectCollection results;
        try { results = new ManagementObjectSearcher(wql).Get(); }
        catch { yield break; } // WMI hiccup must never crash the scan
        foreach (ManagementBaseObject o in results)
            yield return o;
    }

    private static string Str(object? v) => v?.ToString() ?? "";
    private static int? ToInt(object? v) => v is null ? null : int.TryParse(v.ToString(), out var i) ? i : null;
    private static long? ToLong(object? v) => v is null ? null : long.TryParse(v.ToString(), out var l) ? l : null;

    /// <summary>WMI dates are "yyyymmddHHMMSS.ffffff+zzz" — keep the yyyy-mm-dd.</summary>
    private static string FormatWmiDate(string raw) =>
        raw.Length >= 8 ? $"{raw[..4]}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)}" : "";
}
