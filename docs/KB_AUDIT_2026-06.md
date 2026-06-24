# Audit onestà della Knowledge Base — 2026-06-23 (TIER 0, post-design)

> Passata in rassegna l'intera KB (120 voci) per fonti, coerenza evidence/onestà, gate hardware,
> metodi e rischio. Audit programmatico (`tweaks.json`) + revisione manuale dei flag. Conflict-free.

## Foto della KB (sana)
- **120 voci**, 0 id duplicati, 0 nomi duplicati.
- Evidence: plausible 46 · evidence_strong 32 · controversial 21 · placebo 14 · risky 7.
- Risk: none 78 · low 31 · medium 6 · high 5.
- Method (dopo fix): gui-only 92 · registry 15 · nvidia-drs 7 · powercfg-value 2 · dxuser 2 · powercfg 1 · bcdedit 1.

## Finding 1 — 18 voci SENZA blocco `apply` (risolto)
18 voci avevano `apply: null` → rendevano come "manuale" ma senza dire *perché*. Ho dato a ognuna un
blocco `apply: gui-only` ESPLICITO con un `gui_only_reason` ONESTO, tarato per categoria. Diagnosi
chiave: **quasi tutte DEVONO restare manuali** — un placebo non si applica, una cosa rischiosa Verdict
non la fa al posto tuo. Niente è stato promosso a one-click a forza.
- **8 placebo/miti** (niente da applicare): dns-change, timer-resolution-0.5ms, sysmain-superfetch-disable,
  visual-effects-transparency-off, hpet-platformclock-myth, disable-drive-optimization-myth, ipv6-disable,
  windows-search-indexing-off.
- **5 rischiose** (Verdict spiega ma NON applica): memory-integrity-vbs-off, pagefile-disable,
  defender-game-exclusions, pbo-curve-optimizer, defender-disable-completely.
- **3 controverse** (dipende / manuale): nagle-tcpackfrequency, high-priority-process,
  frame-gen-competitive-tradeoff.
- **1 plausibile** (candidato one-click futuro → task #7): nic-interrupt-moderation-off.

## Finding 2 — 4 voci SENZA fonte (risolto)
Aggiunte fonti autorevoli VERIFICATE (regola d'oro: niente URL inventati):
- dns-change → Cloudflare "What is DNS" (il DNS risolve nomi, non tocca il ping in partita).
- sysmain-superfetch-disable → MS Learn troubleshoot SuperFetch/SysMain.
- windows-search-indexing-off → MS Support "Search indexing in Windows".
- debloat-scripts-telemetry → MS Learn "Configure Windows diagnostic data" (verificata live).

## Finding 3 — gate NVIDIA/AMD (rivisto: FALSI POSITIVI, nessuna azione)
L'audit ha segnalato 18 voci "NVIDIA senza gpu:nvidia" + 3 "AMD senza gpu:amd". Revisione manuale:
sono **correttamente cross-vendor** — citano G-SYNC/Reflex/DLSS/FSR/ReBAR come ESEMPIO ma il consiglio
vale per tutti (fullscreen, cap FPS, ReBAR, driver aggiornati, VRR di Windows). Gatarle nasconderebbe
consigli validi agli utenti AMD. Verificato a campione (win11-vrr "separato dal G-SYNC/FreeSync",
mouse-polling, resizable-bar, val-fps-cap). **Lasciate non-gated: è corretto.**

## Garanzie verificate (post-fix)
- Ogni voce ha **almeno una fonte**. Ogni voce ha un **metodo valido**. Nessuna gui-only con `operations`.
- Tutte le evidence_strong hanno una fonte. JSON valido, 120 voci. **Test KB 33/33 verdi.**

## Deferred → task #7 (esplodi one-click sistema/rete)
`nic-interrupt-moderation-off` (plausibile) è un candidato a one-click reale (registry/netsh) quando
si farà il giro di promozione, con field-test. Per ora resta manuale e onesto.
