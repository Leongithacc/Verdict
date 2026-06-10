namespace WPEP.Core.Diagnostics;

/// <summary>Kind of kernel deferred-work event observed via ETW.</summary>
public enum KernelEventKind
{
    Dpc,
    TimerDpc,
    ThreadedDpc,
    Isr,
}

/// <summary>A single DPC or ISR execution, already decoded from ETW.</summary>
public readonly record struct DpcIsrEvent(
    KernelEventKind Kind,
    ulong Routine,
    int Cpu,
    double TimestampMs,
    double DurationUs);

/// <summary>A loaded kernel module (driver) with its address range.</summary>
public sealed record DriverModule(string Name, ulong BaseAddress, ulong Size)
{
    public bool Contains(ulong address) =>
        address >= BaseAddress && address < BaseAddress + Size;
}

/// <summary>Aggregated DPC/ISR statistics for one driver.</summary>
public sealed class DriverStats
{
    public required string Driver { get; init; }
    public long DpcCount { get; set; }
    public long IsrCount { get; set; }
    public double TotalUs { get; set; }
    public double MaxUs { get; set; }
    public long SpikesOver100Us { get; set; }
    public long SpikesOver500Us { get; set; }
    public long SpikesOver1000Us { get; set; }

    public long TotalCount => DpcCount + IsrCount;
    public double AvgUs => TotalCount == 0 ? 0 : TotalUs / TotalCount;
}

/// <summary>Result of one capture session.</summary>
public sealed class DpcIsrReport
{
    public required double CaptureDurationSeconds { get; init; }
    public required long TotalEvents { get; init; }
    public required long UnresolvedEvents { get; init; }
    public required IReadOnlyList<DriverStats> Drivers { get; init; }
}
