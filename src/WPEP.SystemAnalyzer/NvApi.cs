using System.Runtime.InteropServices;
using System.Text;

namespace WPEP.SystemAnalyzer;

/// <summary>Result of probing the NVIDIA driver API.</summary>
public sealed record NvApiProbe(bool Available, string Message);

/// <summary>Thin wrapper over NVIDIA's user-mode driver API (NVAPI). User-mode only — NO kernel
/// driver — so it's anti-cheat safe (it's the same channel NVIDIA's own control panel / profile
/// tools use). This file is the FOUNDATION: a probe that proves the P/Invoke mechanism works on
/// this machine. The DRS (profile) read/write that automates Control-Panel settings builds on the
/// exact same QueryInterface mechanism once the probe is validated on real hardware.
///
/// NVAPI is reached through ONE exported function, nvapi_QueryInterface(id), which returns a
/// function pointer for a numeric API id; you then call it through a matching delegate.</summary>
public static class NvApi
{
    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    // Well-known NVAPI function ids (stable across driver versions).
    private const uint Id_Initialize = 0x0150E828;
    private const uint Id_Unload = 0xD22BDD7E;
    private const uint Id_GetInterfaceVersionString = 0x01053FA5;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Initialize_t();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetInterfaceVersionString_t(StringBuilder desc);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Unload_t();

    private static T? Resolve<T>(uint id) where T : Delegate
    {
        var p = QueryInterface(id);
        return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    /// <summary>Read-only health check: load NVAPI, initialise it, read the interface version, unload.
    /// Proves the interop works on this GPU/driver without touching any setting.</summary>
    public static NvApiProbe Probe()
    {
        try
        {
            var init = Resolve<Initialize_t>(Id_Initialize);
            if (init is null) return new(false, "nvapi64.dll caricata ma Initialize non risolto.");
            int status = init();
            if (status != 0) return new(false, $"NvAPI_Initialize ha restituito stato {status}.");

            string version = "sconosciuta";
            var getVer = Resolve<GetInterfaceVersionString_t>(Id_GetInterfaceVersionString);
            if (getVer is not null)
            {
                var sb = new StringBuilder(64);
                if (getVer(sb) == 0) version = sb.ToString();
            }

            Resolve<Unload_t>(Id_Unload)?.Invoke();
            return new(true, $"NVAPI operativa — interfaccia {version}. Interop pronto per DRS (pannello NVIDIA).");
        }
        catch (DllNotFoundException)
        {
            return new(false, "nvapi64.dll non trovata: nessuna GPU NVIDIA o driver non installato.");
        }
        catch (Exception ex)
        {
            return new(false, $"NVAPI non disponibile: {ex.Message}");
        }
    }
}
