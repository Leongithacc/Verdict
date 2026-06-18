using System.Runtime.InteropServices;

namespace WPEP.SystemAnalyzer;

/// <summary>One connected display: resolution, refresh rate, and whether it's the primary.
/// Driver-free — read from Win32 display settings, no kernel access (anti-cheat safe).</summary>
public sealed record DisplayInfo(string Name, int Width, int Height, int RefreshHz, bool IsPrimary);

/// <summary>Multi-monitor optimizer (Lab feature §V3). Enumerates displays via Win32 and gives
/// honest, read-only advice for a gaming setup: game on the highest-Hz panel, watch for mixed
/// refresh rates, consider disabling extra monitors in competitive play to cut compositor input
/// lag. Verdict never changes display config — it points you to Windows' own settings.</summary>
public static class DisplayScanner
{
    public static IReadOnlyList<DisplayInfo> Enumerate()
    {
        var list = new List<DisplayInfo>();
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref dd, 0); i++)
        {
            // Only attached, active adapters carry a usable mode.
            if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
            {
                dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
                continue;
            }
            bool primary = (dd.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
            string device = dd.DeviceName;

            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref dm))
            {
                string friendly = ReadFriendlyName(device);
                list.Add(new DisplayInfo(
                    Name: friendly,
                    Width: (int)dm.dmPelsWidth,
                    Height: (int)dm.dmPelsHeight,
                    RefreshHz: (int)dm.dmDisplayFrequency,
                    IsPrimary: primary));
            }
            dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        }
        return list;
    }

    /// <summary>The monitor's friendly name (e.g. "Odyssey G7"); falls back to the adapter string.</summary>
    private static string ReadFriendlyName(string adapter)
    {
        var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (EnumDisplayDevices(adapter, 0, ref mon, 0) && mon.DeviceString.Length > 0)
            return mon.DeviceString;
        return adapter.Replace(@"\\.\", "");
    }

    /// <summary>Pure, testable analysis: honest gaming advice from the display list.</summary>
    public static IReadOnlyList<Finding> Analyze(IReadOnlyList<DisplayInfo> displays)
    {
        var f = new List<Finding>();
        if (displays.Count == 0)
            return f;
        if (displays.Count == 1)
        {
            f.Add(new Finding("Ok", $"Un solo monitor ({displays[0].RefreshHz} Hz) — niente da ottimizzare qui."));
            return f;
        }

        var best = displays.OrderByDescending(d => d.RefreshHz).First();
        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];

        // Game where the Hz is highest: if that panel isn't primary, flag it.
        if (best.RefreshHz > primary.RefreshHz)
            f.Add(new Finding("Warn",
                $"Il monitor più veloce è {best.Name} ({best.RefreshHz} Hz) ma il primario è " +
                $"{primary.Name} ({primary.RefreshHz} Hz). Imposta il {best.RefreshHz} Hz come primario " +
                "o gioca su quello — meno input lag dove conta."));

        // Mixed refresh rates can cause micro-stutter on some multi-monitor setups.
        var rates = displays.Select(d => d.RefreshHz).Distinct().OrderByDescending(x => x).ToList();
        if (rates.Count > 1)
            f.Add(new Finding("Info",
                $"Refresh misti ({string.Join(" / ", rates.Select(r => r + "Hz"))}): su alcune GPU il " +
                "desktop multi-monitor con Hz diversi causa micro-stutter. Valuta di spegnerne qualcuno mentre giochi."));

        // The competitive trick: fewer active panels = less compositor work / fewer lag sources.
        f.Add(new Finding("Info",
            $"{displays.Count} monitor attivi. In competitiva, Win+P → \"Solo schermo PC\" lascia solo il " +
            "principale: meno carico del compositore e possibili sorgenti di input lag in meno."));

        // VRR can't be read driver-free — nudge to verify it per-display.
        f.Add(new Finding("Info",
            "VRR/G-SYNC: verifica che sia attivo sul monitor su cui giochi (Pannello NVIDIA → Imposta G-SYNC, per display)."));

        return f;
    }

    // ── Win32 interop (read-only display settings) ──────────────────────────
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2;
        public uint dmPanningWidth, dmPanningHeight;
    }
}
