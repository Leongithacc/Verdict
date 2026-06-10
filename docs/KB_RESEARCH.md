# Knowledge Base — ricerca e ri-grading (R4 + espansione, 2026-06-10)

Verifica con fonti primarie delle 15 voci seed della spec V1 §5, poi espansione a
**38 voci**. Regola applicata: senza fonte primaria il grading non può superare
`controversial`; meglio declassare che inventare evidenza.

## Espansione (23 voci aggiunte)

Con evidenza forte: XMP/EXPO (AMD), fix fTPM stutter (AMD PA-410), NVIDIA Reflex,
G-SYNC/VRR combo (guida NVIDIA), ottimizzazioni giochi in finestra Win11 (Microsoft).
Plausibili: overlay/GameDVR off, startup bloat, driver GPU aggiornati, Resizable BAR,
ULL mode, prefer max performance, interrupt moderation off (trade-off documentato MS),
ethernet vs Wi-Fi. Controversi: MPO disable (workaround non documentato), fast startup,
priorità processo, USB selective suspend, NetworkThrottlingIndex. Placebo: SysMain off,
trasparenze/animazioni (fuori dal present path dei giochi), mito HPET/useplatformclock.
Rischiosi: pagefile off (crash da commit), esclusioni Defender (gap di protezione
documentato da Microsoft).

## Cambi di grading rispetto alla valutazione preliminare

| id | preliminare | finale | perché |
|---|---|---|---|
| `timer-resolution-0.5ms` | controversial | **placebo** | La doc Microsoft di `timeBeginPeriod` attesta che da Win10 2004 la risoluzione non è più globale: un tool esterno non può alzare quella del gioco. Su Win11 è smentito per design. |
| `disable-dynamic-tick` | controversial | **risky** | La doc bcdedit dice esplicitamente *"should only be used for debugging"*. Vendor contro l'uso in produzione + segnalazioni di effetti collaterali. |
| `gpu-msi-mode` | plausible | **controversial** | Il meccanismo MSI è documentato, ma i driver GPU moderni lo usano già di default. Nessuna fonte primaria dimostra benefici nel forzarlo. |
| `disable-core-parking` | plausible | **controversial** | Il parking è documentato (i core parcheggiati processano comunque interrupt/DPC), ma nessuna fonte primaria mostra benefici gaming su sistemi moderni con driver chipset aggiornati. |
| `disable-fullscreen-optimizations` | plausible | **controversial** | Microsoft documenta che FSO ≈ prestazioni del fullscreen esclusivo con flip model. Disattivarle è un workaround per giochi specifici, non un tweak generale. |
| `disable-background-overlays` | evidence_strong | **plausible** | Le linee guida DPC (soglia 100µs) sono la base tecnica, ma l'impatto degli overlay è specifico del software: dichiararlo "forte" in generale sarebbe sovra-vendere. Misurabile per-sistema con wpep diag/bench. |
| `nagle-tcpackfrequency` | controversial | **controversial** (confermato, con precisazione) | Reale e documentato per flussi TCP; completamente inerte per i giochi UDP (la quasi totalità dei competitivi moderni). |

Confermati: `power-plan-high-performance` (plausible), `disable-enhance-pointer-precision`
(evidence_strong — con la precisazione documentata che i giochi Raw Input non sono toccati),
`hags` (controversial — nota: richiesto da DLSS FG), `memory-integrity-vbs-off` (risky),
`dns-change` (placebo), `windows-game-mode` (evidence_strong, effetto minore),
`systemresponsiveness-gpupriority-registry` (controversial — MMCSS tocca solo thread
registrati al servizio, i game engine di solito non lo fanno),
`correct-refresh-rate-and-fps-cap` (evidence_strong — guida NVIDIA system latency).

## Bilancio onesto

Su 38 tweak totali: **8 con evidenza forte**, 11 plausibili, **10 controversi**,
**5 placebo documentati**, **4 rischiosi**. Tradotto: anche allargando la rete, meno
di un quarto dei tweak che circolano ha evidenza primaria forte — e quasi tutti quelli
forti sono "configura correttamente ciò che hai" (refresh, profilo RAM, Reflex, BIOS
aggiornato), non magie da registry.

## Fonti primarie principali

- Microsoft Learn: power/PPM tuning, core parking, timeBeginPeriod, bcdedit,
  MSI interrupts, MMCSS, TcpAckFrequency/Nagle, HVCI/VBS, DPC guidelines (100µs),
  high-definition mouse movement (WM_INPUT vs WM_MOUSEMOVE).
- DirectX Developer Blog: HAGS, Demystifying Fullscreen Optimizations, DXGI flip model.
- Microsoft Support: Game Mode.
- NVIDIA: System Latency Optimization Guide (Reflex, FPS cap con G-SYNC).

URL completi nel JSON: `src/WPEP.KnowledgeBase/kb/tweaks.json` (validati a load time:
fonte obbligatoria se non placebo, rollback e passi manuali obbligatori sempre).
