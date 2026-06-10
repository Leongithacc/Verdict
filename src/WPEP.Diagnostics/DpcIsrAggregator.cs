using WPEP.Core.Diagnostics;

namespace WPEP.Diagnostics;

/// <summary>
/// Accumulates per-driver DPC/ISR statistics from a stream of decoded events.
/// Events whose routine cannot be mapped to a driver are counted as unresolved,
/// never silently dropped.
/// </summary>
public sealed class DpcIsrAggregator
{
    public const string UnresolvedKey = "<unresolved>";

    private readonly DriverMap _map;
    private readonly Dictionary<string, DriverStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private long _total;
    private long _unresolved;

    public DpcIsrAggregator(DriverMap map) => _map = map;

    public void Add(in DpcIsrEvent e)
    {
        _total++;
        string key;
        var module = _map.Resolve(e.Routine);
        if (module is null)
        {
            _unresolved++;
            key = UnresolvedKey;
        }
        else
        {
            key = module.Name;
        }

        if (!_stats.TryGetValue(key, out var s))
        {
            s = new DriverStats { Driver = key };
            _stats[key] = s;
        }

        if (e.Kind == KernelEventKind.Isr)
            s.IsrCount++;
        else
            s.DpcCount++;

        s.TotalUs += e.DurationUs;
        if (e.DurationUs > s.MaxUs)
            s.MaxUs = e.DurationUs;
        if (e.DurationUs > 100)
            s.SpikesOver100Us++;
        if (e.DurationUs > 500)
            s.SpikesOver500Us++;
        if (e.DurationUs > 1000)
            s.SpikesOver1000Us++;
    }

    public DpcIsrReport Build(double captureDurationSeconds) => new()
    {
        CaptureDurationSeconds = captureDurationSeconds,
        TotalEvents = _total,
        UnresolvedEvents = _unresolved,
        Drivers = _stats.Values
            .OrderByDescending(s => s.MaxUs)
            .ToArray(),
    };
}
