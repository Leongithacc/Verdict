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
    private const uint Id_DRS_CreateSession = 0x0694D52E;
    private const uint Id_DRS_DestroySession = 0xDAD9CFF8;
    private const uint Id_DRS_LoadSettings = 0x375DBD6B;
    private const uint Id_DRS_GetBaseProfile = 0xDA8466A0;
    private const uint Id_DRS_GetSetting = 0x73BF8338;
    private const uint Id_DRS_SetSetting = 0x577DD202;
    private const uint Id_DRS_SaveSettings = 0xFCBC7E14;
    private const uint Id_DRS_DeleteProfileSetting = 0xE4A26362;

    // Well-known DRS setting ids (DWORD-typed).
    public const uint Setting_PreferredPState = 0x1057EB71; // "Power management mode"

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Initialize_t();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetInterfaceVersionString_t(StringBuilder desc);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Unload_t();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_CreateSession_t(out IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_DestroySession_t(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_LoadSettings_t(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_GetBaseProfile_t(IntPtr session, out IntPtr profile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_GetSetting_t(IntPtr session, IntPtr profile, uint settingId, ref NVDRS_SETTING setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_SetSetting_t(IntPtr session, IntPtr profile, ref NVDRS_SETTING setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_SaveSettings_t(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DRS_DeleteProfileSetting_t(IntPtr session, IntPtr profile, uint settingId);

    // NVDRS_SETTING (v1). The two value "unions" are sized to their largest member
    // (NVDRS_BINARY_SETTING = 4 + 4 + NVAPI_BINARY_DATA_MAX). version is sizeof | (1<<16),
    // computed at runtime via Marshal.SizeOf so it always matches THIS layout.
    private const int NVAPI_UNICODE_STRING_MAX = 2048;
    private const int NVAPI_BINARY_DATA_MAX = 4096;
    private const int UnionSize = 4 + 4 + NVAPI_BINARY_DATA_MAX; // NVDRS_BINARY_SETTING

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NVDRS_SETTING
    {
        public uint version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NVAPI_UNICODE_STRING_MAX)] public string settingName;
        public uint settingId;
        public uint settingType;
        public uint settingLocation;
        public uint isCurrentPredefined;
        public uint isPredefinedValid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnionSize)] public byte[] predefinedValue;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnionSize)] public byte[] currentValue;
    }

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

    /// <summary>-130 = the struct version/layout was rejected; any other status means GetSetting ran
    /// and our marshalling was accepted (NOT_FOUND/-9 included).</summary>
    private const int NVAPI_INCOMPATIBLE_STRUCT_VERSION = -130;

    /// <summary>READ-ONLY: reads the current DWORD value of a global-profile DRS setting (a NVIDIA
    /// Control Panel option). Opens a DRS session, loads settings, reads the base (global) profile,
    /// gets the setting, then tears the session down. Writes nothing.</summary>
    public static NvDrsRead ReadDwordSetting(uint settingId)
    {
        IntPtr session = IntPtr.Zero;
        DRS_DestroySession_t? destroy = null;
        try
        {
            var init = Resolve<Initialize_t>(Id_Initialize);
            if (init is null || init() != 0) return new(false, false, 0, "NvAPI_Initialize fallito.");

            var create = Resolve<DRS_CreateSession_t>(Id_DRS_CreateSession);
            var load = Resolve<DRS_LoadSettings_t>(Id_DRS_LoadSettings);
            var getBase = Resolve<DRS_GetBaseProfile_t>(Id_DRS_GetBaseProfile);
            var getSetting = Resolve<DRS_GetSetting_t>(Id_DRS_GetSetting);
            destroy = Resolve<DRS_DestroySession_t>(Id_DRS_DestroySession);
            if (create is null || load is null || getBase is null || getSetting is null)
                return new(false, false, 0, "Funzioni DRS non risolte dall'NVAPI.");

            int s = create(out session);
            if (s != 0) return new(false, false, 0, $"DRS_CreateSession status {s}.");
            s = load(session);
            if (s != 0) return new(false, false, 0, $"DRS_LoadSettings status {s}.");
            s = getBase(session, out var profile);
            if (s != 0) return new(false, false, 0, $"DRS_GetBaseProfile status {s}.");

            var setting = new NVDRS_SETTING
            {
                version = (uint)(Marshal.SizeOf<NVDRS_SETTING>() | (1 << 16)),
                settingName = "",
                predefinedValue = new byte[UnionSize],
                currentValue = new byte[UnionSize],
            };
            s = getSetting(session, profile, settingId, ref setting);
            bool marshallingOk = s != NVAPI_INCOMPATIBLE_STRUCT_VERSION;
            if (s != 0)
                return new(false, marshallingOk,
                    0, $"status {s} su settingId 0x{settingId:X} (-9 = non impostato/default).");

            uint value = BitConverter.ToUInt32(setting.currentValue, 0);
            return new(true, true, value, $"0x{settingId:X} = {value} (tipo {setting.settingType}).");
        }
        catch (DllNotFoundException) { return new(false, false, 0, "nvapi64.dll non trovata."); }
        catch (Exception ex) { return new(false, false, 0, $"Errore DRS: {ex.Message}"); }
        finally
        {
            if (session != IntPtr.Zero) destroy?.Invoke(session);
            Resolve<Unload_t>(Id_Unload)?.Invoke();
        }
    }

    private const uint NVDRS_DWORD_TYPE = 0;

    /// <summary>Writes a DWORD value into a global-profile DRS setting (a NVIDIA Control Panel option)
    /// and saves it. Anti-cheat safe (user-mode NVAPI). Returns NVAPI status text.</summary>
    public static (bool Ok, string Message) WriteDwordSetting(uint settingId, uint value)
    {
        IntPtr session = IntPtr.Zero;
        DRS_DestroySession_t? destroy = null;
        try
        {
            var init = Resolve<Initialize_t>(Id_Initialize);
            if (init is null || init() != 0) return (false, "NvAPI_Initialize fallito.");

            var create = Resolve<DRS_CreateSession_t>(Id_DRS_CreateSession);
            var load = Resolve<DRS_LoadSettings_t>(Id_DRS_LoadSettings);
            var getBase = Resolve<DRS_GetBaseProfile_t>(Id_DRS_GetBaseProfile);
            var setSetting = Resolve<DRS_SetSetting_t>(Id_DRS_SetSetting);
            var save = Resolve<DRS_SaveSettings_t>(Id_DRS_SaveSettings);
            destroy = Resolve<DRS_DestroySession_t>(Id_DRS_DestroySession);
            if (create is null || load is null || getBase is null || setSetting is null || save is null)
                return (false, "Funzioni DRS di scrittura non risolte.");

            int s = create(out session); if (s != 0) return (false, $"CreateSession {s}.");
            s = load(session); if (s != 0) return (false, $"LoadSettings {s}.");
            s = getBase(session, out var profile); if (s != 0) return (false, $"GetBaseProfile {s}.");

            var setting = new NVDRS_SETTING
            {
                version = (uint)(Marshal.SizeOf<NVDRS_SETTING>() | (1 << 16)),
                settingName = "",
                settingId = settingId,
                settingType = NVDRS_DWORD_TYPE,
                predefinedValue = new byte[UnionSize],
                currentValue = new byte[UnionSize],
            };
            BitConverter.GetBytes(value).CopyTo(setting.currentValue, 0);

            s = setSetting(session, profile, ref setting); if (s != 0) return (false, $"SetSetting {s}.");
            s = save(session); if (s != 0) return (false, $"SaveSettings {s}.");
            return (true, $"Scritto 0x{settingId:X} = {value} e salvato.");
        }
        catch (DllNotFoundException) { return (false, "nvapi64.dll non trovata."); }
        catch (Exception ex) { return (false, $"Errore DRS write: {ex.Message}"); }
        finally
        {
            if (session != IntPtr.Zero) destroy?.Invoke(session);
            Resolve<Unload_t>(Id_Unload)?.Invoke();
        }
    }

    /// <summary>Removes a setting from the global profile (back to driver default) and saves.
    /// Used to undo a write when the setting wasn't explicitly set before.</summary>
    public static (bool Ok, string Message) DeleteSetting(uint settingId)
    {
        IntPtr session = IntPtr.Zero;
        DRS_DestroySession_t? destroy = null;
        try
        {
            var init = Resolve<Initialize_t>(Id_Initialize);
            if (init is null || init() != 0) return (false, "NvAPI_Initialize fallito.");

            var create = Resolve<DRS_CreateSession_t>(Id_DRS_CreateSession);
            var load = Resolve<DRS_LoadSettings_t>(Id_DRS_LoadSettings);
            var getBase = Resolve<DRS_GetBaseProfile_t>(Id_DRS_GetBaseProfile);
            var del = Resolve<DRS_DeleteProfileSetting_t>(Id_DRS_DeleteProfileSetting);
            var save = Resolve<DRS_SaveSettings_t>(Id_DRS_SaveSettings);
            destroy = Resolve<DRS_DestroySession_t>(Id_DRS_DestroySession);
            if (create is null || load is null || getBase is null || del is null || save is null)
                return (false, "Funzioni DRS di delete non risolte.");

            int s = create(out session); if (s != 0) return (false, $"CreateSession {s}.");
            s = load(session); if (s != 0) return (false, $"LoadSettings {s}.");
            s = getBase(session, out var profile); if (s != 0) return (false, $"GetBaseProfile {s}.");
            s = del(session, profile, settingId);
            if (s != 0 && s != -9) return (false, $"DeleteProfileSetting {s}."); // -9 = già assente = ok
            s = save(session); if (s != 0) return (false, $"SaveSettings {s}.");
            return (true, $"Setting 0x{settingId:X} riportato al default.");
        }
        catch (DllNotFoundException) { return (false, "nvapi64.dll non trovata."); }
        catch (Exception ex) { return (false, $"Errore DRS delete: {ex.Message}"); }
        finally
        {
            if (session != IntPtr.Zero) destroy?.Invoke(session);
            Resolve<Unload_t>(Id_Unload)?.Invoke();
        }
    }
}

/// <summary>Result of a DRS read. <see cref="MarshallingOk"/> is true whenever NVAPI executed the
/// call without rejecting our struct (no -130), even if the setting wasn't found — that's the proof
/// the interop layout is correct.</summary>
public sealed record NvDrsRead(bool Ok, bool MarshallingOk, uint Value, string Message);
