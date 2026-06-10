using System.Runtime.InteropServices;
using System.Text;
using WPEP.Core.Diagnostics;

namespace WPEP.Diagnostics;

/// <summary>
/// Enumerates loaded kernel drivers via psapi (EnumDeviceDrivers).
/// Base addresses are only meaningful when running elevated; without elevation
/// recent Windows builds zero them out, which surfaces here as an empty list.
/// </summary>
public static class KernelDriverEnumerator
{
    public static IReadOnlyList<DriverModule> Enumerate()
    {
        if (!EnumDeviceDrivers(null, 0, out uint needed) || needed == 0)
            return [];

        int count = (int)(needed / (uint)IntPtr.Size);
        var bases = new IntPtr[count];
        if (!EnumDeviceDrivers(bases, needed, out _))
            return [];

        var modules = new List<DriverModule>(count);
        var nameBuf = new StringBuilder(260);
        var ordered = bases
            .Select(b => (ulong)b.ToInt64())
            .Where(b => b != 0)
            .OrderBy(b => b)
            .ToArray();

        for (int i = 0; i < ordered.Length; i++)
        {
            nameBuf.Clear();
            if (GetDeviceDriverBaseName((IntPtr)(long)ordered[i], nameBuf, nameBuf.Capacity) == 0)
                continue;

            // psapi does not expose image sizes; approximate each module's extent
            // as the gap to the next base address (capped at 64 MB for the last one).
            ulong size = i + 1 < ordered.Length
                ? ordered[i + 1] - ordered[i]
                : 64UL * 1024 * 1024;
            modules.Add(new DriverModule(nameBuf.ToString(), ordered[i], size));
        }

        return modules;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumDeviceDrivers(
        [Out] IntPtr[]? lpImageBase, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetDeviceDriverBaseName(
        IntPtr imageBase, StringBuilder lpBaseName, int nSize);
}
