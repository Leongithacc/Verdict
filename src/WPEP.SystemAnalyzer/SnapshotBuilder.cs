using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WPEP.Core.SystemInfo;

namespace WPEP.SystemAnalyzer;

/// <summary>
/// Builds a SystemSnapshot from WMI, registry and display APIs. Read-only, no
/// admin required. Every probe degrades to null on failure instead of crashing:
/// "sconosciuto" is an acceptable answer, a crash is not.
/// </summary>
public static class SnapshotBuilder
{
    public static SystemSnapshot Build(DateTimeOffset nowUtc)
    {
        var (cpuName, cores, threads) = Probe(ReadCpu, ("", (int?)null, (int?)null));
        var (gpuName, gpuDriver, currentHz) = Probe(ReadGpu, ("", "", (int?)null));
        var (ramGb, ramSpeed, ramRated) = Probe(ReadRam, ((double?)null, (int?)null, (int?)null));
        var (planGuid, planName) = Probe(ReadPowerPlan, ("", ""));
        var (throttlingIndex, systemResponsiveness) = Probe(ReadMultimediaProfile, ((int?)null, (int?)null));
        var (gpuTempC, gpuThrottling) = Probe(ReadGpuTelemetry, ((int?)null, (bool?)null));

        return new SystemSnapshot
        {
            CapturedAtUtc = nowUtc,
            CpuName = cpuName,
            CpuCores = cores,
            CpuThreads = threads,
            CpuIsX3D = cpuName.Contains("X3D", StringComparison.OrdinalIgnoreCase),
            GpuName = gpuName,
            GpuDriverVersion = gpuDriver,
            RamTotalGb = ramGb,
            RamSpeedMtps = ramSpeed,
            RamRatedMtps = ramRated,
            IsDesktop = Probe(ReadIsDesktop, (bool?)null),
            MonitorCurrentHz = currentHz,
            MonitorMaxHz = Probe(ReadMaxRefreshRate, (int?)null),
            DisplayWidth = Probe(() => ReadCurrentDisplayMode().width, (int?)null),
            DisplayHeight = Probe(() => ReadCurrentDisplayMode().height, (int?)null),
            PowerPlanGuid = planGuid,
            PowerPlanName = planName,
            HagsEnabled = Probe(ReadHags, (bool?)null),
            GameModeEnabled = Probe(ReadGameMode, (bool?)null),
            HvciEnabled = Probe(ReadHvci, (bool?)null),
            PointerPrecisionEnabled = Probe(ReadPointerPrecision, (bool?)null),
            GameDvrEnabled = Probe(ReadGameDvr, (bool?)null),
            ActiveNicIsWifi = Probe(ReadActiveNicIsWifi, (bool?)null),
            SysMainRunning = Probe(ReadSysMainRunning, (bool?)null),
            TransparencyEnabled = Probe(ReadTransparency, (bool?)null),
            FastStartupEnabled = Probe(ReadFastStartup, (bool?)null),
            MpoDisabled = Probe(ReadMpoDisabled, (bool?)null),
            PagefileAutomatic = Probe(ReadPagefileAutomatic, (bool?)null),
            NetworkThrottlingIndex = throttlingIndex,
            SystemResponsiveness = systemResponsiveness,
            StartupAppsCount = Probe(ReadStartupAppsCount, (int?)null),
            Ipv6Disabled = Probe(ReadIpv6Disabled, (bool?)null),
            SearchIndexingRunning = Probe(() => ReadServiceRunning("WSearch"), (bool?)null),
            AnyHddPresent = Probe(ReadAnyHddPresent, (bool?)null),
            IsManagedDevice = Probe(ReadIsManagedDevice, (bool?)null),
            GpuTempC = gpuTempC,
            GpuThermalThrottling = gpuThrottling,
            CpuTempC = Probe(ReadCpuTempAcpi, (double?)null),
            CpuLoadPercent = Probe(ReadCpuLoadPercent, (int?)null),
            FortniteInstalled = Probe(ReadFortniteInstalled, (bool?)null),
        };
    }

    /// <summary>Epic Games Launcher keeps one .item manifest per installed
    /// game. No Epic dir = definitely no Fortnite. Read-only as everything.</summary>
    private static bool? ReadFortniteInstalled()
    {
        var manifests = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifests))
            return false;
        foreach (var file in Directory.EnumerateFiles(manifests, "*.item"))
        {
            if (File.ReadAllText(file).Contains("Fortnite", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>PORTABILITY §2: a benchmark on battery is invalid (power
    /// throttling) and must be blocked like F10. Null = no battery (desktop).</summary>
    public static bool? IsOnBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT BatteryStatus FROM Win32_Battery");
            foreach (var battery in searcher.Get())
                return Convert.ToInt32(battery["BatteryStatus"]) == 1; // 1 = discharging
            return null; // nessuna batteria: desktop
        }
        catch
        {
            return null;
        }
    }

    /// <summary>GPU temperature and thermal-throttle state via nvidia-smi
    /// (ships with the NVIDIA driver, no admin, no kernel driver of ours).
    /// Returns nulls on AMD/Intel GPUs or when nvidia-smi is unavailable.</summary>
    private static (int? tempC, bool? throttling) ReadGpuTelemetry()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
        };
        var exe = candidates.FirstOrDefault(File.Exists);
        if (exe is null)
            return (null, null);

        var psi = new ProcessStartInfo(exe,
            "--query-gpu=temperature.gpu,clocks_throttle_reasons.hw_thermal_slowdown,clocks_throttle_reasons.sw_thermal_slowdown " +
            "--format=csv,noheader,nounits")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(5000);
        if (process.ExitCode != 0 || output.Length == 0)
            return (null, null);

        var fields = output.Split(',', StringSplitOptions.TrimEntries);
        int? temp = fields.Length > 0 && int.TryParse(fields[0], out var t) ? t : null;
        bool? throttling = fields.Length > 2
            ? fields[1].Contains("Active", StringComparison.OrdinalIgnoreCase) &&
              !fields[1].Contains("Not", StringComparison.OrdinalIgnoreCase) ||
              fields[2].Contains("Active", StringComparison.OrdinalIgnoreCase) &&
              !fields[2].Contains("Not", StringComparison.OrdinalIgnoreCase)
            : null;
        return (temp, throttling);
    }

    /// <summary>Best-effort CPU temperature from the ACPI thermal zone
    /// (tenths of Kelvin). Many desktop boards don't expose it: null is normal.</summary>
    private static double? ReadCpuTempAcpi()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
        foreach (var zone in searcher.Get())
        {
            double kelvin10 = Convert.ToDouble(zone["CurrentTemperature"]);
            double celsius = kelvin10 / 10.0 - 273.15;
            if (celsius is > 0 and < 120)
                return Math.Round(celsius, 1);
        }
        return null;
    }

    private static int? ReadCpuLoadPercent()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT LoadPercentage FROM Win32_Processor");
        foreach (var cpu in searcher.Get())
        {
            if (cpu["LoadPercentage"] is not null)
                return Convert.ToInt32(cpu["LoadPercentage"]);
        }
        return null;
    }

    private static bool? ReadIsManagedDevice()
    {
        using (var searcher = new ManagementObjectSearcher(
            "SELECT PartOfDomain FROM Win32_ComputerSystem"))
        {
            foreach (var cs in searcher.Get())
            {
                if (cs["PartOfDomain"] is true)
                    return true;
            }
        }

        // MDM/Intune enrollment leaves per-enrollment keys here.
        using var enrollments = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Enrollments");
        if (enrollments is not null)
        {
            foreach (var name in enrollments.GetSubKeyNames())
            {
                using var key = enrollments.OpenSubKey(name);
                if (key?.GetValue("EnrollmentState") is int state and > 0 &&
                    key.GetValue("ProviderID") is string provider && provider.Length > 0)
                    return true;
            }
        }

        return false;
    }

    private static T Probe<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch { return fallback; }
    }

    private static (string, int?, int?) ReadCpu()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        foreach (var cpu in searcher.Get())
        {
            return (
                (cpu["Name"] as string ?? "").Trim(),
                Convert.ToInt32(cpu["NumberOfCores"]),
                Convert.ToInt32(cpu["NumberOfLogicalProcessors"]));
        }
        return ("", null, null);
    }

    private static (string, string, int?) ReadGpu()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DriverVersion, CurrentRefreshRate FROM Win32_VideoController");
        // Prefer the controller actually driving a display (has a refresh rate).
        (string, string, int?) best = ("", "", null);
        foreach (var gpu in searcher.Get())
        {
            var entry = (
                gpu["Name"] as string ?? "",
                gpu["DriverVersion"] as string ?? "",
                gpu["CurrentRefreshRate"] is null ? (int?)null : Convert.ToInt32(gpu["CurrentRefreshRate"]));
            if (entry.Item3 is > 0)
                return entry;
            if (best.Item1.Length == 0)
                best = entry;
        }
        return best;
    }

    private static (double?, int?, int?) ReadRam()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
        ulong totalBytes = 0;
        int configured = 0;
        int rated = 0;
        foreach (var stick in searcher.Get())
        {
            totalBytes += Convert.ToUInt64(stick["Capacity"]);
            if (stick["ConfiguredClockSpeed"] is not null)
                configured = Math.Max(configured, Convert.ToInt32(stick["ConfiguredClockSpeed"]));
            if (stick["Speed"] is not null)
                rated = Math.Max(rated, Convert.ToInt32(stick["Speed"]));
        }
        return (totalBytes == 0 ? null : Math.Round(totalBytes / 1073741824.0, 1),
                configured == 0 ? null : configured,
                rated == 0 ? null : rated);
    }

    private static bool? ReadIsDesktop()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT ChassisTypes FROM Win32_SystemEnclosure");
        foreach (var enclosure in searcher.Get())
        {
            if (enclosure["ChassisTypes"] is ushort[] types && types.Length > 0)
            {
                // 8..14, 30..32 = portatili/tablet; 3..7, 15..17, 23, 24 = desktop/tower.
                int t = types[0];
                if (t is >= 8 and <= 14 or >= 30 and <= 32)
                    return false;
                if (t is >= 3 and <= 7 or >= 15 and <= 17 or 23 or 24)
                    return true;
            }
        }
        return null;
    }

    private static (string, string) ReadPowerPlan()
    {
        var psi = new ProcessStartInfo("powercfg", "/getactivescheme")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        // Output: "GUID schema risparmio energia: <guid>  (<nome>)"
        var guidMatch = System.Text.RegularExpressions.Regex.Match(
            output, @"([0-9a-fA-F]{8}-[0-9a-fA-F-]{27})");
        var nameMatch = System.Text.RegularExpressions.Regex.Match(output, @"\(([^)]+)\)");
        return (
            guidMatch.Success ? guidMatch.Groups[1].Value.ToLowerInvariant() : "",
            nameMatch.Success ? nameMatch.Groups[1].Value : "");
    }

    private static bool? ReadHags()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
        return key?.GetValue("HwSchMode") switch
        {
            2 => true,
            1 => false,
            _ => null, // assente = non supportato o stato non determinabile
        };
    }

    private static bool? ReadGameMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
        // Assente = default = attivo su Win10/11.
        return key?.GetValue("AutoGameModeEnabled") switch
        {
            0 => false,
            _ => true,
        };
    }

    private static bool? ReadHvci()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
        return key?.GetValue("Enabled") switch
        {
            1 => true,
            0 => false,
            _ => null,
        };
    }

    private static bool? ReadPointerPrecision()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
        return (key?.GetValue("MouseSpeed") as string) switch
        {
            "0" => false,
            "1" or "2" => true,
            _ => null,
        };
    }

    private static bool? ReadGameDvr()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR");
        // Assente = default = registrazione in background disponibile/attiva.
        return key?.GetValue("AppCaptureEnabled") switch
        {
            0 => false,
            _ => true,
        };
    }

    private static bool? ReadActiveNicIsWifi()
    {
        var active = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                        n.GetIPProperties().GatewayAddresses.Count > 0)
            .ToArray();
        if (active.Length == 0)
            return null;
        // Wi-Fi solo se NESSUNA interfaccia attiva con gateway è cablata.
        return active.All(n =>
            n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);
    }

    private static bool? ReadSysMainRunning() => ReadServiceRunning("SysMain");

    private static bool? ReadServiceRunning(string serviceName)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT State FROM Win32_Service WHERE Name='{serviceName}'");
        foreach (var service in searcher.Get())
            return (service["State"] as string) == "Running";
        return null;
    }

    private static bool? ReadIpv6Disabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters");
        // Assente o 0 = IPv6 attivo (default). 0xFF = tutto disabilitato.
        return key?.GetValue("DisabledComponents") switch
        {
            null or 0 => false,
            int v => (v & 0xFF) == 0xFF,
            _ => null,
        };
    }

    private static bool? ReadAnyHddPresent()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\Microsoft\Windows\Storage",
            "SELECT MediaType FROM MSFT_PhysicalDisk");
        bool sawAny = false;
        foreach (var disk in searcher.Get())
        {
            sawAny = true;
            // MediaType: 3 = HDD, 4 = SSD, 0 = Unspecified.
            if (Convert.ToInt32(disk["MediaType"]) == 3)
                return true;
        }
        return sawAny ? false : null;
    }

    private static bool? ReadTransparency()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("EnableTransparency") switch
        {
            0 => false,
            1 => true,
            _ => null,
        };
    }

    private static bool? ReadFastStartup()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
        return key?.GetValue("HiberbootEnabled") switch
        {
            1 => true,
            0 => false,
            _ => null,
        };
    }

    private static bool? ReadMpoDisabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Dwm");
        // OverlayTestMode=5 è il workaround community per disattivare MPO.
        return key?.GetValue("OverlayTestMode") switch
        {
            5 => true,
            null => false, // assente = MPO attivo (default)
            _ => null,
        };
    }

    private static bool? ReadPagefileAutomatic()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem");
        foreach (var cs in searcher.Get())
            return cs["AutomaticManagedPagefile"] as bool?;
        return null;
    }

    private static (int?, int?) ReadMultimediaProfile()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
        if (key is null)
            return (null, null);
        return (
            key.GetValue("NetworkThrottlingIndex") is int t ? t : null,
            key.GetValue("SystemResponsiveness") is int r ? r : null);
    }

    private static int? ReadStartupAppsCount()
    {
        int count = 0;
        foreach (var (hive, path) in new[]
        {
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
        })
        {
            using var key = hive.OpenSubKey(path);
            count += key?.ValueCount ?? 0;
        }
        return count;
    }

    private static (int? width, int? height) ReadCurrentDisplayMode()
    {
        var current = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };
        if (!EnumDisplaySettingsW(null, ENUM_CURRENT_SETTINGS, ref current))
            return (null, null);
        return ((int)current.dmPelsWidth, (int)current.dmPelsHeight);
    }

    private static int? ReadMaxRefreshRate()
    {
        var current = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };
        if (!EnumDisplaySettingsW(null, ENUM_CURRENT_SETTINGS, ref current))
            return null;

        int max = 0;
        var mode = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };
        for (int i = 0; EnumDisplaySettingsW(null, i, ref mode); i++)
        {
            // Same resolution as current: the refresh the user could actually pick.
            if (mode.dmPelsWidth == current.dmPelsWidth &&
                mode.dmPelsHeight == current.dmPelsHeight &&
                (int)mode.dmDisplayFrequency > max)
            {
                max = (int)mode.dmDisplayFrequency;
            }
        }
        return max > 0 ? max : null;
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsW(
        string? lpszDeviceName, int iModeNum, ref DEVMODEW lpDevMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODEW
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight;
        public uint dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
}
