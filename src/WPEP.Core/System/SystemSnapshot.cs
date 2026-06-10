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
    public bool? IsDesktop { get; init; }

    // Monitor
    public int? MonitorCurrentHz { get; init; }
    public int? MonitorMaxHz { get; init; }

    // Config rilevante per la KB
    public string PowerPlanGuid { get; init; } = "";
    public string PowerPlanName { get; init; } = "";
    public bool? HagsEnabled { get; init; }
    public bool? GameModeEnabled { get; init; }
    public bool? HvciEnabled { get; init; }
    public bool? PointerPrecisionEnabled { get; init; }
}
