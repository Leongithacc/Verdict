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

## Round 2 (13 voci aggiunte → 51 totali)

Forti: upscaling DLSS/FSR (doc NVIDIA), giochi su NVMe (DirectStorage, doc Microsoft GDK),
Raw Input in-game (doc Microsoft). Plausibili: monitor sulla dGPU, bufferbloat/SQM
(bufferbloat.net — probabilmente il fix di rete più sottovalutato), polling mouse 1000Hz.
Controversi: Frame Generation nel competitivo (NVIDIA stessa documenta che FG richiede
Reflex per compensare la latenza aggiunta). Placebo: IPv6 off (Microsoft documenta
esplicitamente di NON disattivarlo), mito della defrag su SSD (Optimize-Volume fa Retrim),
Windows Search off, script di debloat (con risk note: rotture differite). Rischiosi:
PBO/Curve Optimizer (AMD: invalida la garanzia), Defender completamente off.

## Bilancio onesto

Su 51 tweak totali: **11 con evidenza forte**, 13 plausibili, **12 controversi**,
**9 placebo documentati**, **6 rischiosi**. Il pattern si conferma allargando la rete:
quasi tutto ciò che ha evidenza forte è "configura correttamente ciò che hai"
(refresh, profilo RAM, Reflex, upscaling, SSD, Raw Input, BIOS aggiornato) — le magie
da registry stanno tutte tra controverso e placebo.

## Fonti primarie principali

- Microsoft Learn: power/PPM tuning, core parking, timeBeginPeriod, bcdedit,
  MSI interrupts, MMCSS, TcpAckFrequency/Nagle, HVCI/VBS, DPC guidelines (100µs),
  high-definition mouse movement (WM_INPUT vs WM_MOUSEMOVE).
- DirectX Developer Blog: HAGS, Demystifying Fullscreen Optimizations, DXGI flip model.
- Microsoft Support: Game Mode.
- NVIDIA: System Latency Optimization Guide (Reflex, FPS cap con G-SYNC).

URL completi nel JSON: `src/WPEP.KnowledgeBase/kb/tweaks.json` (validati a load time:
fonte obbligatoria se non placebo, rollback e passi manuali obbligatori sempre).

## Vetting ricerca Perplexity 2026-07 (post-v1.1)

Léon ha fatto vagliare 3 ricerche Perplexity (threat analysis di Platinum+Optimizer,
tassonomia tweak Windows, workflow A/B). **~90% era già in KB** — spesso già smontato
come placebo/controversial (Game Mode, HAGS, core parking, NetworkThrottlingIndex,
SysMain, trasparenze, dGPU preference, USB selective suspend, dynamic tick, Nagle…) —
e la "metodologia A/B" proposta è ciò che Verdict già implementa con statistica migliore
(Mann-Whitney + bootstrap + noise gate + journal/undo). L'unico asset davvero nuovo è il
doc competitor [`VS_PLATINUM.md`](VS_PLATINUM.md).

Sono stati vagliati **3 candidati KB** con la regola d'oro (fonte primaria Microsoft
verificata live via WebFetch, 2026-07-04):

| Candidato | Esito | Fonte primaria |
|---|---|---|
| **TCP Chimney Offload disable** (`netsh int tcp set global chimney=disabled`) | ✅ **Aggiunto — placebo** (`tcp-chimney-offload-disable`). Feature deprecata da Windows Server 2016 e già `disabled` di default: il comando spegne qualcosa di già spento. | [net-sub-performance-tuning-nics](https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics) — *"Don't use… TCP Chimney Offload. These technologies are deprecated in Windows Server 2016"* |
| **TCP Auto-Tuning disable** (`autotuninglevel=disabled`) | ✅ **Aggiunto — placebo** (`tcp-autotuning-disable`, `risk=low` con risk_notes). Placebo per il gaming (traffico UDP); disattivarlo fissa la finestra TCP e **danneggia** il throughput. MS lo documenta solo come workaround per router/firewall vecchi non-RFC1323. | [net-sub-performance-tuning-nics](https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics) + [receive-window-auto-tuning-for-http](https://learn.microsoft.com/en-us/troubleshoot/windows-server/networking/receive-window-auto-tuning-for-http) |
| **RSS / NIC offload toggles disable** | ⛔ **Skipped, no gaming source.** MS documenta gli offload come *"usually beneficial"* e per bassa latenza raccomanda di **abilitarli**, non disattivarli. Nessuna fonte su un beneficio gaming del disabilitare RSS/offload; l'opposto (RSS *enabled*) è già in KB come `nic-rss-enable`. | — (nessuna fonte primaria a favore del disable → non aggiunto) |

KB: **135 → 137 voci** (placebo 14 → 16). Vedi anche [`VS_PLATINUM.md`](VS_PLATINUM.md).
Nota per fra tre mesi: non rifare questo vetting — questi tre sono già stati chiusi.
