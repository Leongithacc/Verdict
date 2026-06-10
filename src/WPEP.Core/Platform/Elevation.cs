using System.Security.Principal;

namespace WPEP.Core.Platform;

public static class Elevation
{
    /// <summary>True when the current process runs with administrator rights.
    /// Both ETW kernel sessions (diag) and PresentMon (bench) require it.</summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
