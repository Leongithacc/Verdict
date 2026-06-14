using Microsoft.Win32;

namespace WPEP.Execution;

public sealed record RegistryValue(bool Exists, string Kind, string? Value);

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
