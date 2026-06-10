# Knowledge Base — ricerca e ri-grading (R4, 2026-06-10)

Verifica con fonti primarie delle 15 voci seed della spec V1 §5. Regola applicata:
senza fonte primaria il grading non può superare `controversial`; meglio declassare
che inventare evidenza.

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

Su 15 tweak "famosi": **3 con evidenza forte**, 2 plausibili, **6 controversi**,
**2 placebo documentati**, **2 rischiosi**. Tradotto: la maggioranza dei tweak che
circolano su YouTube/TikTok non ha evidenza primaria a supporto. Era l'ipotesi di
partenza del progetto, ora è documentato voce per voce.

## Fonti primarie principali

- Microsoft Learn: power/PPM tuning, core parking, timeBeginPeriod, bcdedit,
  MSI interrupts, MMCSS, TcpAckFrequency/Nagle, HVCI/VBS, DPC guidelines (100µs),
  high-definition mouse movement (WM_INPUT vs WM_MOUSEMOVE).
- DirectX Developer Blog: HAGS, Demystifying Fullscreen Optimizations, DXGI flip model.
- Microsoft Support: Game Mode.
- NVIDIA: System Latency Optimization Guide (Reflex, FPS cap con G-SYNC).

URL completi nel JSON: `src/WPEP.KnowledgeBase/kb/tweaks.json` (validati a load time:
fonte obbligatoria se non placebo, rollback e passi manuali obbligatori sempre).
