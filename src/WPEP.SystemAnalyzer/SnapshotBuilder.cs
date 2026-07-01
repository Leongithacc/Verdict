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

        var build = new SystemSnapshot
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
            ValorantInstalled = Probe(ReadValorantInstalled, (bool?)null),
            Cs2Installed = Probe(ReadCs2Installed, (bool?)null),
            ApexInstalled = Probe(ReadApexInstalled, (bool?)null),
            Overwatch2Installed = Probe(ReadOverwatch2Installed, (bool?)null),
            TheFinalsInstalled = Probe(ReadTheFinalsInstalled, (bool?)null),
            R6SiegeInstalled = Probe(ReadR6SiegeInstalled, (bool?)null),
            SecureBootEnabled = Probe(ReadSecureBoot, (bool?)null),
            Tpm2Enabled = Probe(ReadTpm2, (bool?)null),
        };
        var (noiseScore, noiseFactors) = ComputeNoiseScore(snapshotBase: build);
        return build with { NoiseScore = noiseScore, NoiseFactors = noiseFactors };
    }

    /// <summary>System Noise Score (0-100): quanto è rumoroso il sistema per il gaming.
    /// Calcolato da 5 fattori DOCUMENTATI. Onestà attiva contro il placebo: i tweak background
    /// hanno effetto misurabile solo se il rumore è già alto. Vedi docs/VS_HONE.md sez. 3.1.
    /// Contributi (max 90 pt, cap a 100):
    ///  - StartupAppsCount: min(30, 2*count) — max 30 punti a 15+ app
    ///  - SearchIndexingRunning: 20 (indexer W10/11 fa disk I/O continuo)
    ///  - SysMainRunning: 15 (Superfetch prefetch continua)
    ///  - GameDvrEnabled: 20 (buffer video sempre attivo, hit reale su GPU)
    ///  - TransparencyEnabled: 5 (compositor blur/vibrancy consuma qualche %)
    /// Ogni fattore null (probe fallito) non contribuisce né al numeratore né al massimo.</summary>
    private static (int? score, IReadOnlyList<string> factors) ComputeNoiseScore(SystemSnapshot snapshotBase)
    {
        int score = 0;
        var factors = new List<string>();
        int signals = 0;

        if (snapshotBase.StartupAppsCount is int apps && apps >= 0)
        {
            signals++;
            int pt = Math.Min(30, apps * 2);
            if (pt > 0)
            {
                score += pt;
                factors.Add($"{apps} app all'avvio (+{pt})");
            }
        }
        if (snapshotBase.SearchIndexingRunning == true)
        {
            score += 20; signals++;
            factors.Add("Search Indexer attivo (+20)");
        }
        else if (snapshotBase.SearchIndexingRunning == false)
        {
            signals++;
        }
        if (snapshotBase.SysMainRunning == true)
        {
            score += 15; signals++;
            factors.Add("SysMain / Superfetch attivo (+15)");
        }
        else if (snapshotBase.SysMainRunning == false)
        {
            signals++;
        }
        if (snapshotBase.GameDvrEnabled == true)
        {
            score += 20; signals++;
            factors.Add("Game DVR / Xbox capture attivo (+20)");
        }
        else if (snapshotBase.GameDvrEnabled == false)
        {
            signals++;
        }
        if (snapshotBase.TransparencyEnabled == true)
        {
            score += 5; signals++;
            factors.Add("Effetti trasparenza attivi (+5)");
        }
        else if (snapshotBase.TransparencyEnabled == false)
        {
            signals++;
        }

        // Se meno di 2 signal utili sono stati rilevati, non emettiamo un noise score:
        // "sconosciuto" è meglio di un numero con 1 solo input.
        if (signals < 2) return (null, factors);

        return (Math.Min(100, score), factors);
    }

    /// <summary>UEFI Secure Boot via registry. Chiave assente = sistema in Legacy/BIOS (null).
    /// Value 1 = attivo, 0 (o assente con chiave presente) = disabilitato.</summary>
    private static bool? ReadSecureBoot()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
        if (key is null) return null;
        return key.GetValue("UEFISecureBootEnabled") is int v && v == 1;
    }

    /// <summary>TPM 2.0 via WMI namespace ROOT\CIMV2\Security\MicrosoftTpm. SpecVersion inizia con
    /// "2" per TPM 2.0; IsEnabled_InitialValue dice se è acceso a livello hardware. Namespace assente
    /// (Windows molto vecchio o WMI rotto) viene wrappato da Probe → null.</summary>
    private static bool? ReadTpm2()
    {
        var scope = new ManagementScope(@"\\.\ROOT\CIMV2\Security\MicrosoftTpm");
        scope.Connect();
        using var searcher = new ManagementObjectSearcher(
            scope, new ObjectQuery("SELECT SpecVersion, IsEnabled_InitialValue FROM Win32_Tpm"));
        using var coll = searcher.Get();
        foreach (ManagementBaseObject mbo in coll)
        {
            using (mbo)
            {
                var spec = (mbo["SpecVersion"] as string) ?? "";
                if (!spec.StartsWith("2", StringComparison.Ordinal)) continue;
                return mbo["IsEnabled_InitialValue"] as bool? ?? false;
            }
        }
        return false; // nessun Win32_Tpm = chip non presente o spento dal BIOS
    }

    /// <summary>THE FINALS is Steam app 2073850. (Epic Games copies aren't detected
    /// here; null/false only means 'not found via Steam', section stays shown on null.)</summary>
    private static bool? ReadTheFinalsInstalled() => SteamAppInstalled(2073850);

    /// <summary>Rainbow Six Siege is Steam app 359550. (Ubisoft Connect copies aren't
    /// detected here; null/false only means 'not found via Steam'.)</summary>
    private static bool? ReadR6SiegeInstalled() => SteamAppInstalled(359550);

    /// <summary>Riot writes per-product metadata here for every installed Riot game.</summary>
    private static bool? ReadValorantInstalled()
    {
        var metadata = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games", "Metadata");
        if (!Directory.Exists(metadata))
            return false;
        return Directory.EnumerateDirectories(metadata, "valorant*").Any();
    }

    /// <summary>CS2 is Steam app 730.</summary>
    private static bool? ReadCs2Installed() => SteamAppInstalled(730);

    /// <summary>Apex Legends is Steam app 1172470. (EA App copies aren't detected
    /// here; null/false only means 'not found via Steam', section stays shown on null.)</summary>
    private static bool? ReadApexInstalled() => SteamAppInstalled(1172470);

    /// <summary>Overwatch 2 ships on Steam (app 2357570) and Battle.net. Check Steam
    /// first, then the default Battle.net install path. Returns null only when we truly
    /// can't tell (no Steam and no Battle.net copy at the default path).</summary>
    private static bool? ReadOverwatch2Installed()
    {
        var steam = SteamAppInstalled(2357570);
        if (steam == true)
            return true;
        var bnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Overwatch", "_retail_", "Overwatch.exe");
        if (File.Exists(bnet))
            return true;
        return steam; // false if Steam is present but OW2 isn't; null if no Steam at all
    }

    /// <summary>True if appmanifest_&lt;appid&gt;.acf exists in any Steam library.
    /// Null = Steam not found (caller shows the game section honestly).</summary>
    private static bool? SteamAppInstalled(uint appId)
    {
        var libraries = EnumerateSteamLibraries();
        if (libraries is null)
            return null;
        var manifest = $"appmanifest_{appId}.acf";
        return libraries.Any(lib =>
            File.Exists(Path.Combine(lib, "steamapps", manifest)));
    }

    /// <summary>The main Steam library plus any extra libraries in libraryfolders.vdf.
    /// Null when Steam isn't installed (no SteamPath in registry).</summary>
    private static List<string>? EnumerateSteamLibraries()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is not string steamPath || steamPath.Length == 0)
            return null;
        steamPath = steamPath.Replace('/', '\\');

        var libraries = new List<string> { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(
                         File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
            {
                libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
        }
        return libraries;
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

        // MDM/Intune enrollment. NB: every Windows install has many placeholder
        // enrollment subkeys with EnrollmentState=1 — those are NOT managed
        // devices (false positive observed on Léon's personal desktop). The
        // reliable signal of a REAL enrollment is an MDM server URL.
        using var enrollments = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Enrollments");
        if (enrollments is not null)
        {
            foreach (var name in enrollments.GetSubKeyNames())
            {
                using var key = enrollments.OpenSubKey(name);
                if (key?.GetValue("EnrollmentState") is int and > 0 &&
                    (NonEmpty(key.GetValue("DiscoveryServiceFullURL")) ||
                     NonEmpty(key.GetValue("UPN"))))
                    return true;
            }
        }

        return false;

        static bool NonEmpty(object? v) => v is string s && s.Trim().Length > 0;
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
