namespace WPEP.Core.SystemInfo;

/// <summary>
/// Read-only photograph of the machine, used by the Advisor to decide tweak
/// applicability. Nullable fields mean "non rilevabile": the Advisor must say
/// "sconosciuto", never guess.
/// </summary>
public sealed record SystemSnapshot
{
    public required DateTimeOffset CapturedAtUtc { get; init; }

    // Hardware
    public string CpuName { get; init; } = "";
    public int? CpuCores { get; init; }
    public int? CpuThreads { get; init; }
    public bool CpuIsX3D { get; init; }
    public string GpuName { get; init; } = "";
    public string GpuDriverVersion { get; init; } = "";
    public double? RamTotalGb { get; init; }
    public int? RamSpeedMtps { get; init; }
    public int? RamRatedMtps { get; init; }
    public bool? IsDesktop { get; init; }

    // Monitor
    public int? MonitorCurrentHz { get; init; }
    public int? MonitorMaxHz { get; init; }
    public int? DisplayWidth { get; init; }
    public int? DisplayHeight { get; init; }

    // Config rilevante per la KB
    public string PowerPlanGuid { get; init; } = "";
    public string PowerPlanName { get; init; } = "";
    public bool? HagsEnabled { get; init; }
    public bool? GameModeEnabled { get; init; }
    public bool? HvciEnabled { get; init; }

    /// <summary>UEFI Secure Boot attivo: requisito di Windows 11 e Valorant Vanguard.
    /// null se la chiave registry non esiste (es. Windows installato in modalità Legacy/BIOS).</summary>
    public bool? SecureBootEnabled { get; init; }

    /// <summary>TPM 2.0 / fTPM (Ryzen) / PTT (Intel) presente e abilitato. Requisito di Windows 11
    /// e Valorant Vanguard. null se WMI ROOT\CIMV2\Security\MicrosoftTpm non è raggiungibile.</summary>
    public bool? Tpm2Enabled { get; init; }
    public bool? PointerPrecisionEnabled { get; init; }
    public bool? GameDvrEnabled { get; init; }
    public bool? ActiveNicIsWifi { get; init; }
    public bool? SysMainRunning { get; init; }
    public bool? TransparencyEnabled { get; init; }
    public bool? FastStartupEnabled { get; init; }
    public bool? MpoDisabled { get; init; }
    public bool? PagefileAutomatic { get; init; }
    public int? NetworkThrottlingIndex { get; init; }
    public int? SystemResponsiveness { get; init; }
    public int? StartupAppsCount { get; init; }
    public bool? Ipv6Disabled { get; init; }
    public bool? SearchIndexingRunning { get; init; }
    public bool? AnyHddPresent { get; init; }

    /// <summary>Domain-joined or MDM-enrolled (PORTABILITY §3): running
    /// third-party diagnostic tools may violate the org's IT policy — the
    /// user must see a notice, in app and in reports.</summary>
    public bool? IsManagedDevice { get; init; }

    // Thermal/load (PORTABILITY §2) — via nvidia-smi and ACPI, deliberately
    // NOT LibreHardwareMonitor: its elevated mode loads a kernel driver
    // (WinRing0), which breaks leave-no-trace and risks AV/anti-cheat flags.
    public int? GpuTempC { get; init; }
    public bool? GpuThermalThrottling { get; init; }
    public double? CpuTempC { get; init; }
    public int? CpuLoadPercent { get; init; }

    /// <summary>Null = detection failed (show game sections anyway, honestly).
    /// False = definitely not installed (hide that game's KB section).</summary>
    public bool? FortniteInstalled { get; init; }
    public bool? ValorantInstalled { get; init; }
    public bool? Cs2Installed { get; init; }
    public bool? ApexInstalled { get; init; }
    public bool? Overwatch2Installed { get; init; }
    public bool? TheFinalsInstalled { get; init; }
    public bool? R6SiegeInstalled { get; init; }
    public bool? WarzoneInstalled { get; init; }

    /// <summary>Maps a KB entry's `game` key to its detection result.</summary>
    public bool? GameInstalled(string game) => game switch
    {
        "fortnite" => FortniteInstalled,
        "valorant" => ValorantInstalled,
        "cs2" => Cs2Installed,
        "apex" => ApexInstalled,
        "overwatch2" => Overwatch2Installed,
        "thefinals" => TheFinalsInstalled,
        "r6siege" => R6SiegeInstalled,
        "warzone" => WarzoneInstalled,
        _ => null,
    };

    /// <summary>System Noise Score (0-100): quanto è rumoroso il sistema per il gaming.
    /// Calcolato da fattori DOCUMENTATI (startup apps count, servizi background attivi,
    /// indexing, Game DVR, effetti visivi). Onestà attiva contro il placebo: i tweak background
    /// hanno effetto misurabile solo se il rumore è già alto. Vedi docs/VS_HONE.md sez. 3.1.</summary>
    public int? NoiseScore { get; init; }

    /// <summary>Fattori individuali che contribuiscono al <see cref="NoiseScore"/>, con il loro
    /// peso in punti. Usato dalla UI per spiegare "perché il tuo score è X".</summary>
    public IReadOnlyList<string> NoiseFactors { get; init; } = [];

    /// <summary>Livello leggibile del <see cref="NoiseScore"/>. Null quando lo score non è
    /// stato calcolato (probe falliti). Soglie fisse: 25 (basso→medio), 55 (medio→alto).</summary>
    public string? NoiseBand => NoiseScore switch
    {
        null => null,
        <= 25 => "basso",
        <= 55 => "medio",
        _ => "alto",
    };
}
