namespace WPEP.Execution;

/// <summary>
/// Read/modify/write helper for Windows 11's per-app and global graphics
/// preferences, stored as a single REG_SZ of semicolon-separated key=value
/// pairs under HKCU\Software\Microsoft\DirectX\UserGpuPreferences.
///
/// The GLOBAL toggles ("Optimizations for windowed games", "Variable refresh
/// rate", "Auto HDR") all live inside ONE value — DirectXUserGlobalSettings —
/// e.g. "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;AutoHDREnable=0;".
/// A naive REG_SZ overwrite would clobber the sibling keys, so every change is
/// a parse → merge-one-key → rebuild, preserving order and the other pairs.
///
/// This class is pure string logic (no registry I/O) so it is unit-testable;
/// the ExecutionEngine layers it over IRegistryAccess for the actual read/write.
/// </summary>
public static class DxUserSettings
{
    /// <summary>Full registry path of the global settings REG_SZ value.</summary>
    public const string GlobalValuePath =
        @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings";

    /// <summary>Parse a "K1=V1;K2=V2;" REG_SZ into ordered (key,value) pairs.
    /// Tolerant: ignores empty segments and segments without '='.</summary>
    public static List<KeyValuePair<string, string>> Parse(string? raw)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        if (string.IsNullOrEmpty(raw))
            return pairs;
        foreach (var segment in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;
            pairs.Add(new(segment[..eq].Trim(), segment[(eq + 1)..].Trim()));
        }
        return pairs;
    }

    /// <summary>Rebuild the REG_SZ form: "K1=V1;K2=V2;" (trailing ';', no spaces),
    /// matching how Windows writes it.</summary>
    public static string Build(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in pairs)
            sb.Append(p.Key).Append('=').Append(p.Value).Append(';');
        return sb.ToString();
    }

    /// <summary>Look up one key (case-insensitive). Returns (found, value).</summary>
    public static (bool Found, string? Value) Get(string? raw, string key)
    {
        foreach (var p in Parse(raw))
            if (string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
                return (true, p.Value);
        return (false, null);
    }

    /// <summary>Return a new REG_SZ with <paramref name="key"/> set to
    /// <paramref name="value"/>, preserving every other pair and their order.
    /// Updates in place if present, otherwise appends.</summary>
    public static string Set(string? raw, string key, string value)
    {
        var pairs = Parse(raw);
        for (int i = 0; i < pairs.Count; i++)
        {
            if (string.Equals(pairs[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                pairs[i] = new(pairs[i].Key, value);
                return Build(pairs);
            }
        }
        pairs.Add(new(key, value));
        return Build(pairs);
    }

    /// <summary>Return a new REG_SZ with <paramref name="key"/> removed,
    /// preserving every other pair (used by undo when the key did not exist
    /// before Verdict wrote it).</summary>
    public static string Remove(string? raw, string key)
    {
        var pairs = Parse(raw);
        pairs.RemoveAll(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        return Build(pairs);
    }
}
