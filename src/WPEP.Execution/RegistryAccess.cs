using System.Diagnostics;
using Microsoft.Win32;

namespace WPEP.Execution;

public sealed record RegistryValue(bool Exists, string Kind, string? Value);

/// <summary>Power-scheme access (powercfg). Abstracted so the engine stays
/// testable without changing the machine's active power plan.</summary>
public interface IPowerCfg
{
    /// <summary>Active power scheme GUID, lowercase.</summary>
    string GetActiveScheme();
    void SetActiveScheme(string guid);

    /// <summary>Current AC value index of a setting on the active scheme.</summary>
    int QuerySettingIndex(string subgroup, string setting);
    /// <summary>Sets a setting's AC+DC index on the active scheme and applies it.</summary>
    void SetSettingIndex(string subgroup, string setting, int index);
}

public sealed class RealPowerCfg : IPowerCfg
{
    public string GetActiveScheme()
    {
        string output = Run("/getactivescheme");
        var match = System.Text.RegularExpressions.Regex.Match(
            output, @"([0-9a-fA-F]{8}-[0-9a-fA-F-]{27})");
        return match.Success ? match.Groups[1].Value.ToLowerInvariant()
            : throw new InvalidOperationException("Impossibile leggere lo schema attivo.");
    }

    public void SetActiveScheme(string guid) => Run($"/setactive {guid}");

    public int QuerySettingIndex(string subgroup, string setting)
    {
        string output = Run($"/query SCHEME_CURRENT {subgroup} {setting}");
        var match = System.Text.RegularExpressions.Regex.Match(
            output, @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)");
        return match.Success
            ? Convert.ToInt32(match.Groups[1].Value, 16)
            : throw new InvalidOperationException(
                $"Impossibile leggere il valore di {subgroup}/{setting}.");
    }

    public void SetSettingIndex(string subgroup, string setting, int index)
    {
        Run($"/setacvalueindex SCHEME_CURRENT {subgroup} {setting} {index}");
        Run($"/setdcvalueindex SCHEME_CURRENT {subgroup} {setting} {index}");
        Run("/setactive SCHEME_CURRENT"); // re-apply so the change takes effect
    }

    private static string Run(string args)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit(10000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"powercfg {args} fallito: {err.Trim()}");
        return output;
    }
}

/// <summary>Abstraction so the engine is testable without touching the real
/// registry. Paths are "HKCU\Key\Sub\ValueName" / "HKLM\…".</summary>
public interface IRegistryAccess
{
    RegistryValue Read(string path);
    void Write(string path, string kind, string value);
    void Delete(string path);
}

public sealed class RealRegistryAccess : IRegistryAccess
{
    public RegistryValue Read(string path)
    {
        var (hive, key, name) = Split(path);
        using var k = hive.OpenSubKey(key);
        var raw = k?.GetValue(name);
        return raw switch
        {
            null => new RegistryValue(false, "dword", null),
            int i => new RegistryValue(true, "dword", unchecked((uint)i).ToString()),
            string s => new RegistryValue(true, "string", s),
            _ => new RegistryValue(true, "other", raw.ToString()),
        };
    }

    public void Write(string path, string kind, string value)
    {
        var (hive, key, name) = Split(path);
        using var k = hive.CreateSubKey(key)
            ?? throw new InvalidOperationException($"Impossibile aprire {key}");
        if (kind == "string")
            k.SetValue(name, value, RegistryValueKind.String);
        else
            k.SetValue(name, unchecked((int)uint.Parse(value)), RegistryValueKind.DWord);
    }

    public void Delete(string path)
    {
        var (hive, key, name) = Split(path);
        using var k = hive.OpenSubKey(key, writable: true);
        k?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static (RegistryKey hive, string key, string name) Split(string path)
    {
        var parts = path.Split('\\');
        var hive = parts[0].ToUpperInvariant() switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            _ => throw new ArgumentException($"Hive non supportato: {parts[0]}"),
        };
        return (hive, string.Join('\\', parts[1..^1]), parts[^1]);
    }
}
