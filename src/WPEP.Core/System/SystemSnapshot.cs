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
}
