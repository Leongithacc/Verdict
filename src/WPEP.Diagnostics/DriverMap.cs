using WPEP.Core.Diagnostics;

namespace WPEP.Diagnostics;

/// <summary>
/// Resolves a kernel routine address to the driver whose image range contains it.
/// Lookup is O(log n) over modules sorted by base address.
/// </summary>
public sealed class DriverMap
{
    private readonly DriverModule[] _sorted;
    private readonly ulong[] _bases;

    public DriverMap(IEnumerable<DriverModule> modules)
    {
        _sorted = modules
            .Where(m => m.BaseAddress > 0 && m.Size > 0)
            .OrderBy(m => m.BaseAddress)
            .ToArray();
        _bases = _sorted.Select(m => m.BaseAddress).ToArray();
    }

    public int Count => _sorted.Length;

    public DriverModule? Resolve(ulong address)
    {
        int idx = Array.BinarySearch(_bases, address);
        if (idx < 0)
            idx = ~idx - 1; // last module starting at or below the address
        if (idx < 0)
            return null;
        var candidate = _sorted[idx];
        return candidate.Contains(address) ? candidate : null;
    }
}
