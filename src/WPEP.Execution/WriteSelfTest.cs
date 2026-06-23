using System.Diagnostics;
using System.Text.RegularExpressions;
using WPEP.Core.Platform;
using WPEP.SystemAnalyzer;

namespace WPEP.Execution;

public enum ProbeStatus { Pass, Fail, Skip }

/// <summary>Result of exercising one apply method end-to-end on the real machine.</summary>
public sealed record WriteProbe(string Method, ProbeStatus Status, string Detail);

/// <summary>Trust self-test: proves that EVERY apply method can write→verify→(undo) on THIS PC,
/// using only SAFE targets so nothing real changes — a throwaway key/scheme, or a no-op rewrite of a
/// value to itself. Closes the long-standing gap that the bcdedit WRITE path was never exercised live.
/// Read-the-result, not a tweak: each probe leaves the system exactly as it found it.</summary>
public static class WriteSelfTest
{
    public static IReadOnlyList<WriteProbe> RunAll() =>
        [Registry(), DxUser(), PowerCfg(), BcdEdit(), NvidiaDrs()];

    /// <summary>True only if every non-skipped probe passed.</summary>
    public static bool AllOk(IReadOnlyList<WriteProbe> probes) =>
        probes.All(p => p.Status != ProbeStatus.Fail);

    // ── registry: full engine pipeline (BuildPlan→Execute→verify→Undo) on a throwaway HKCU key ──
    private static WriteProbe Registry()
    {
        var r = EngineSelfTest.RunReal();
        return new("registry", r.Passed ? ProbeStatus.Pass : ProbeStatus.Fail,
            r.Passed
                ? "chiave usa-e-getta HKCU: write→verify→undo OK"
                : string.Join(" · ", r.Steps.Where(s => !s.Ok).Select(s => s.Detail)));
    }

    // ── dxuser: add a probe pair to the global REG_SZ, read it back, restore the original EXACTLY ──
    private static WriteProbe DxUser()
    {
        const string probeKey = "VerdictSelfTest";
        var reg = new RealRegistryAccess();
        try
        {
            var before = reg.Read(DxUserSettings.GlobalValuePath);
            string? original = before.Exists ? before.Value : null;

            reg.Write(DxUserSettings.GlobalValuePath, "string", DxUserSettings.Set(original, probeKey, "1"));
            var mid = reg.Read(DxUserSettings.GlobalValuePath);
            var (found, val) = DxUserSettings.Get(mid.Value, probeKey);
            bool wrote = found && val == "1";

            // restore exactly: delete the whole value if it didn't exist before, else put the original back.
            if (original is null) reg.Delete(DxUserSettings.GlobalValuePath);
            else reg.Write(DxUserSettings.GlobalValuePath, "string", original);

            var after = reg.Read(DxUserSettings.GlobalValuePath);
            bool restored = original is null ? !after.Exists : after.Exists && after.Value == original;

            return new("dxuser", wrote && restored ? ProbeStatus.Pass : ProbeStatus.Fail,
                wrote && restored
                    ? "REG_SZ globale: coppia di prova scritta, riletta e rimossa (le altre voci restano intatte)"
                    : $"scritto={wrote} ripristinato={restored}");
        }
        catch (Exception ex) { return new("dxuser", ProbeStatus.Fail, ex.Message); }
    }

    // ── powercfg: esercita il path REALMENTE usato (switch schema attivo) su un CLONE identico ──
    // Clono lo schema attivo, attivo il clone (byte-identico → zero cambiamenti di comportamento),
    // ripristino l'originale, cancello il clone. Lo schema reale non viene mai modificato.
    private static WriteProbe PowerCfg()
    {
        var pc = new RealPowerCfg();
        string? original = null;
        string? scratch = null;
        try
        {
            original = pc.GetActiveScheme();
            scratch = ExtractGuid(PowerCfgRun($"/duplicatescheme {original}"));

            pc.SetActiveScheme(scratch);
            bool switched = string.Equals(pc.GetActiveScheme(), scratch, StringComparison.OrdinalIgnoreCase);

            pc.SetActiveScheme(original);
            bool back = string.Equals(pc.GetActiveScheme(), original, StringComparison.OrdinalIgnoreCase);

            return new("powercfg", switched && back ? ProbeStatus.Pass : ProbeStatus.Fail,
                switched && back
                    ? "schema usa-e-getta (clone identico): attivato e poi ripristinato l'originale, comportamento invariato"
                    : $"attivato={switched} ripristinato={back}");
        }
        catch (Exception ex) { return new("powercfg", ProbeStatus.Skip, $"non eseguito: {ex.Message}"); }
        finally
        {
            if (original is not null) { try { pc.SetActiveScheme(original); } catch { /* best effort */ } }
            if (scratch is not null) { try { PowerCfgRun($"/delete {scratch}"); } catch { /* best effort */ } }
        }
    }

    // ── bcdedit: no-op rewrite of an existing element to its SAME value on {current} (solo admin) ──
    private static readonly string[] BcdSafeElements =
        ["disabledynamictick", "bootmenupolicy", "nx", "recoveryenabled"];

    private static WriteProbe BcdEdit()
    {
        if (!Elevation.IsElevated())
            return new("bcdedit", ProbeStatus.Skip, "serve admin (scrive sul boot store) — rilancia elevato per provarlo");
        try
        {
            var bcd = new RealBcdEdit();
            foreach (var el in BcdSafeElements)
            {
                var v = bcd.Query(el);
                if (!v.Exists || v.Value is null) continue;
                bcd.Set(el, v.Value);                  // riscrive lo STESSO valore → no-op netto
                var after = bcd.Query(el);
                bool ok = after.Exists && after.Value == v.Value;
                return new("bcdedit", ok ? ProbeStatus.Pass : ProbeStatus.Fail,
                    ok
                        ? $"no-op su '{el}={v.Value}' su {{current}}: write su BCD esercitato, valore invariato"
                        : $"'{el}': atteso {v.Value}, riletto {(after.Exists ? after.Value : "<niente>")}");
            }
            return new("bcdedit", ProbeStatus.Skip, "nessun elemento sicuro presente da riscrivere");
        }
        catch (Exception ex) { return new("bcdedit", ProbeStatus.Fail, ex.Message); }
    }

    // ── nvidia-drs: no-op rewrite of Power Management Mode to its SAME value (solo NVIDIA) ──
    private static WriteProbe NvidiaDrs()
    {
        try
        {
            var drs = new RealNvidiaDrs();
            var (found, value) = drs.ReadDword(NvApi.Setting_PreferredPState);
            if (!found)
                return new("nvidia-drs", ProbeStatus.Skip,
                    "Power Management Mode sul default driver: non scrivo per non introdurre un valore");
            drs.WriteDword(NvApi.Setting_PreferredPState, value);   // riscrive lo STESSO valore
            var (f2, v2) = drs.ReadDword(NvApi.Setting_PreferredPState);
            bool ok = f2 && v2 == value;
            return new("nvidia-drs", ok ? ProbeStatus.Pass : ProbeStatus.Fail,
                ok
                    ? $"no-op su Power Management Mode (val {value}): write+verify via NVAPI OK"
                    : $"atteso {value}, riletto {(f2 ? v2.ToString() : "<niente>")}");
        }
        catch (Exception ex) { return new("nvidia-drs", ProbeStatus.Skip, $"NVAPI non disponibile: {ex.Message}"); }
    }

    // ── helpers ──
    private static string ExtractGuid(string s)
    {
        var m = Regex.Match(s, @"[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}");
        return m.Success ? m.Value
            : throw new InvalidOperationException("GUID non trovato nell'output di powercfg.");
    }

    private static string PowerCfgRun(string args)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(10000))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* già uscito */ }
            throw new InvalidOperationException($"powercfg {args} non risponde (timeout).");
        }
        string output = outTask.GetAwaiter().GetResult();
        string err = errTask.GetAwaiter().GetResult();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"powercfg {args} fallito: {err.Trim()}");
        return output;
    }
}
