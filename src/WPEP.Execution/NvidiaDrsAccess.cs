using WPEP.SystemAnalyzer;

namespace WPEP.Execution;

/// <summary>Read/write access to NVIDIA driver-profile (DRS) settings — the NVIDIA Control Panel
/// options. Abstracted like IPowerCfg/IBcdEdit so the engine stays testable with a fake and never
/// touches the real driver in tests. User-mode NVAPI only (anti-cheat safe), NO admin needed.</summary>
public interface INvidiaDrs
{
    /// <summary>Current DWORD value of a global-profile setting. Found=false means it isn't set
    /// explicitly (the driver default is in effect). THROWS se l'interop NVAPI fallisce (struct
    /// rifiutata, GPU non NVIDIA, init KO) — un fallimento NON deve mai essere mascherato come
    /// "non impostato" (è il bug che ha nascosto per sessioni il rifiuto della struct -9).</summary>
    (bool Found, uint Value) ReadDword(uint settingId);

    /// <summary>Sets a global-profile DWORD setting and saves. Throws on NVAPI failure.</summary>
    void WriteDword(uint settingId, uint value);

    /// <summary>Removes the setting from the global profile (back to driver default). Throws on failure.</summary>
    void DeleteSetting(uint settingId);
}

/// <summary>Real INvidiaDrs over <see cref="NvApi"/>. Only constructed when a tweak actually needs
/// it; on a non-NVIDIA machine the NVAPI calls fail and surface as a clear exception.</summary>
public sealed class RealNvidiaDrs : INvidiaDrs
{
    public (bool Found, uint Value) ReadDword(uint settingId)
    {
        var r = NvApi.ReadDwordSetting(settingId);
        // MarshallingOk=false => l'interop NON ha eseguito la lettura in modo affidabile
        // (struct rifiutata -9, NVAPI non inizializzata, GPU non NVIDIA...). Fallire FORTE,
        // mai restituire un falso "non impostato": è proprio la maschera che ci ha ingannato.
        if (!r.MarshallingOk)
            throw new InvalidOperationException(
                $"NVIDIA DRS read: interop NVAPI non affidabile su 0x{settingId:X} — {r.Message}");
        // Eseguita davvero: Ok=true => valore valido; Ok=false => setting non impostato (default driver).
        return (r.Ok, r.Value);
    }

    public void WriteDword(uint settingId, uint value)
    {
        var (ok, message) = NvApi.WriteDwordSetting(settingId, value);
        if (!ok) throw new InvalidOperationException($"NVIDIA DRS write fallita: {message}");
    }

    public void DeleteSetting(uint settingId)
    {
        var (ok, message) = NvApi.DeleteSetting(settingId);
        if (!ok) throw new InvalidOperationException($"NVIDIA DRS delete fallita: {message}");
    }
}
