using Microsoft.Win32;

namespace WPEP.App;

/// <summary>Opt-in "start the Watchdog tray with Windows", via the per-user Run key (HKCU — no admin).
/// Fully reversible: disabling just deletes the value. Read-only until the user ticks the box.</summary>
internal static class TrayAutostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VerdictWatchdog";

    /// <summary>The tray agent shipped next to the GUI.</summary>
    public static string TrayExePath => System.IO.Path.Combine(System.AppContext.BaseDirectory, "wpep-tray.exe");

    /// <summary>The exact command written to Run — quoted so a path with spaces still launches.</summary>
    public static string Command => $"\"{TrayExePath}\"";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch { return false; }
    }

    public static bool Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(ValueName, Command, RegistryValueKind.String);
            return true;
        }
        catch { return false; }
    }

    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch { return false; }
    }
}
