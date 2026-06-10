using WPEP.Core.SystemInfo;
using WPEP.KnowledgeBase;

namespace WPEP.Advisor;

public enum Classification
{
    Recommended,        // evidenza forte, applicabile, non già attivo
    Optional,           // plausibile
    OptionalWithWarning,// controverso
    Placebo,            // non lo tocchiamo, spiegato
    NotRecommended,     // rischioso: mostrato con warning forte
    AlreadyActive,      // già configurato così
    NotApplicable,      // prerequisiti hardware non soddisfatti
}

public sealed record Recommendation(
    TweakEntry Entry,
    Classification Classification,
    string StateNote);

/// <summary>
/// Deterministic rules (spec §3F, NO ML): for each KB entry decide
/// applicability from the snapshot, detect whether it is already active when
/// the snapshot allows it, then classify by evidence level. Unknown state is
/// reported as unknown — the engine never guesses.
/// </summary>
public static class AdvisorEngine
{
    private const string HighPerfPlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimatePlanGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public static IReadOnlyList<Recommendation> Advise(
        SystemSnapshot snapshot, IReadOnlyList<TweakEntry> entries)
    {
        return entries
            .Select(e => Classify(snapshot, e))
            .OrderBy(r => r.Classification)
            .ThenBy(r => r.Entry.Id)
            .ToArray();
    }

    private static Recommendation Classify(SystemSnapshot s, TweakEntry e)
    {
        if (!IsApplicable(s, e, out string applicabilityNote))
            return new Recommendation(e, Classification.NotApplicable, applicabilityNote);

        (bool? active, string stateNote) = DetectState(s, e);
        if (active == true)
            return new Recommendation(e, Classification.AlreadyActive, stateNote);

        var classification = e.EvidenceLevel switch
        {
            EvidenceLevel.EvidenceStrong => Classification.Recommended,
            EvidenceLevel.Plausible => Classification.Optional,
            EvidenceLevel.Controversial => Classification.OptionalWithWarning,
            EvidenceLevel.Placebo => Classification.Placebo,
            EvidenceLevel.Risky => Classification.NotRecommended,
            _ => Classification.OptionalWithWarning,
        };
        return new Recommendation(e, classification, stateNote);
    }

    private static bool IsApplicable(SystemSnapshot s, TweakEntry e, out string note)
    {
        foreach (var prerequisite in e.HardwarePrerequisites)
        {
            switch (prerequisite)
            {
                case "desktop":
                    if (s.IsDesktop == false)
                    {
                        note = "Sistema portatile: tweak pensato per desktop.";
                        return false;
                    }
                    break;
                case "monitor:high-refresh":
                    if (s.MonitorMaxHz is <= 60)
                    {
                        note = $"Il monitor supporta al massimo {s.MonitorMaxHz}Hz.";
                        return false;
                    }
                    break;
                case "gpu:wddm2.7+":
                    if (s.HagsEnabled is null)
                    {
                        note = "Supporto HAGS non rilevato su questa GPU/driver.";
                        return false;
                    }
                    break;
                case "gpu:nvidia":
                    if (s.GpuName.Length > 0 &&
                        !s.GpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
                        !s.GpuName.Contains("GeForce", StringComparison.OrdinalIgnoreCase))
                    {
                        note = $"Richiede GPU NVIDIA (rilevata: {s.GpuName}).";
                        return false;
                    }
                    break;
                case "cpu:amd":
                    if (s.CpuName.Length > 0 &&
                        !s.CpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) &&
                        !s.CpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
                    {
                        note = $"Richiede CPU AMD (rilevata: {s.CpuName}).";
                        return false;
                    }
                    break;
            }
        }
        note = "";
        return true;
    }

    private static (bool? active, string note) DetectState(SystemSnapshot s, TweakEntry e) => e.Id switch
    {
        "power-plan-high-performance" =>
            s.PowerPlanGuid is HighPerfPlanGuid or UltimatePlanGuid
                ? (true, $"Piano attivo: {s.PowerPlanName}")
                : s.PowerPlanGuid.Length > 0
                    ? (false, $"Piano attivo: {s.PowerPlanName}")
                    : (null, "Piano energetico non rilevato."),

        "disable-enhance-pointer-precision" => s.PointerPrecisionEnabled switch
        {
            false => (true, "Precisione puntatore già disattivata."),
            true => (false, "Precisione puntatore ATTIVA."),
            null => (null, "Stato non rilevato."),
        },

        "hags-hardware-gpu-scheduling" => s.HagsEnabled switch
        {
            true => (true, "HAGS già attivo."),
            false => (false, "HAGS disattivato."),
            null => (null, "Stato HAGS non rilevato."),
        },

        "windows-game-mode" => s.GameModeEnabled switch
        {
            true => (true, "Game Mode attivo (default)."),
            false => (false, "Game Mode DISATTIVATO manualmente."),
            null => (null, "Stato non rilevato."),
        },

        "memory-integrity-vbs-off" => s.HvciEnabled switch
        {
            false => (true, "Memory Integrity già disattivata (valuta se riattivarla: costo sicurezza)."),
            true => (false, "Memory Integrity attiva (stato di default, più sicuro)."),
            null => (null, "Stato non rilevato."),
        },

        "correct-refresh-rate-and-fps-cap" when s.MonitorCurrentHz is int cur && s.MonitorMaxHz is int max =>
            cur >= max
                ? (true, $"Refresh corretto: {cur}Hz (max {max}Hz). Resta da verificare il cap FPS in-game.")
                : (false, $"Monitor a {cur}Hz ma supporta {max}Hz: correzione gratuita e immediata."),

        "xmp-expo-enable" when s.RamSpeedMtps is int conf && s.RamRatedMtps is int rated =>
            conf >= rated
                ? (true, $"RAM a {conf} MT/s (profilo attivo o velocità dichiarata raggiunta).")
                : (false, $"RAM configurata a {conf} MT/s ma dichiarata {rated} MT/s: profilo probabilmente NON attivo."),

        "ethernet-over-wifi" => s.ActiveNicIsWifi switch
        {
            false => (true, "Connessione attiva già cablata."),
            true => (false, "Connessione attiva su Wi-Fi."),
            null => (null, "Tipo di connessione non rilevato."),
        },

        "disable-gamedvr-background-recording" => s.GameDvrEnabled switch
        {
            false => (true, "Registrazione in background già disattivata."),
            true => (false, "Registrazione in background ATTIVA (Game DVR)."),
            null => (null, "Stato non rilevato."),
        },

        "sysmain-superfetch-disable" => s.SysMainRunning switch
        {
            false => (true, "SysMain già disattivato (nessuna evidenza che serva, comunque)."),
            true => (false, "SysMain attivo (stato di default, va bene così)."),
            null => (null, "Stato non rilevato."),
        },

        "visual-effects-transparency-off" => s.TransparencyEnabled switch
        {
            false => (true, "Trasparenze già disattivate (per gli FPS in gioco era comunque irrilevante)."),
            true => (false, "Trasparenze attive (irrilevante per gli FPS in gioco)."),
            null => (null, "Stato non rilevato."),
        },

        "fast-startup-disable" => s.FastStartupEnabled switch
        {
            false => (true, "Avvio rapido già disattivato: ogni boot è pulito."),
            true => (false, "Avvio rapido attivo: lo stato dei driver sopravvive agli spegnimenti."),
            null => (null, "Stato non rilevato."),
        },

        "mpo-disable" => s.MpoDisabled switch
        {
            true => (true, "MPO già disattivato via OverlayTestMode (valuta se serve ancora)."),
            false => (false, "MPO attivo (default corretto: toccare solo per troubleshooting)."),
            null => (null, "Stato non determinabile (valore OverlayTestMode anomalo)."),
        },

        "pagefile-disable" => s.PagefileAutomatic switch
        {
            true => (false, "Pagefile gestito automaticamente (configurazione corretta: NON disattivarlo)."),
            false => (null, "Pagefile in gestione manuale: verificare che esista e sia adeguato."),
            null => (null, "Stato non rilevato."),
        },

        "network-throttling-index" => s.NetworkThrottlingIndex switch
        {
            null => (false, "Valore di default (assente): throttling standard attivo."),
            int v when (uint)v == 0xFFFFFFFF => (true, "Throttling già disattivato (0xFFFFFFFF): valuta il rollback al default."),
            int v => (null, $"Valore personalizzato: {v}."),
        },

        "systemresponsiveness-gpupriority-registry" => s.SystemResponsiveness switch
        {
            null or 20 => (false, $"SystemResponsiveness al default (20)."),
            int v => (true, $"SystemResponsiveness modificato: {v} (default 20). Già applicato: valuta rollback."),
        },

        "disable-startup-bloat" when s.StartupAppsCount is int n =>
            n <= 5
                ? (true, $"{n} voci in autostart: situazione pulita.")
                : (false, $"{n} voci in autostart nel registry (più eventuali task pianificati): da rivedere."),

        _ => (null, "Stato non rilevabile automaticamente."),
    };
}
