# WPEP — Windows Performance Engineering Platform

Tool diagnostico per gaming competitivo che **misura, diagnostica e consiglia** — senza
mai toccare il sistema (V1 è read-only by design) e senza mai gonfiare i risultati.

Principio fondante: *dire la verità su cosa migliora davvero, con prove.* Per la maggior
parte dei "gaming tweak" la risposta onesta è "nessun effetto misurabile" — e questo tool
è costruito per dirlo.

## Stato

| Milestone | Stato |
|---|---|
| R1 — Diagnostics CLI (ETW DPC/ISR per driver) | ✅ validata live (idle + in partita) |
| R2 — Benchmark (PresentMon, percentili) | ✅ validata live (smoke su dwm.exe) |
| R3 — Statistics (Mann–Whitney, bootstrap CI, compare) | ✅ 46 test, validato su run reali |
| R4 — KnowledgeBase con fonti primarie | ✅ 15 voci verificate e ri-graduate ([ricerca](docs/KB_RESEARCH.md)) |
| R5 — Advisor + SystemAnalyzer | ⬜ |
| R6 — Reporting | ⬜ |
| R7 — UI WPF (tema scuro/viola) | ⬜ |

Documenti: [BUILDSPEC_V1.1.md](BUILDSPEC_V1.1.md) (decisioni correnti).

## Uso

```
wpep diag --seconds 30 [--json report.json]
```

Cattura una sessione ETW kernel e produce la classifica dei driver per latenza DPC/ISR
(max, media, conteggio spike >100µs/>500µs/>1ms). Richiede terminale **amministratore**
(vincolo di Windows sulle sessioni ETW kernel, non una scelta nostra).

```
wpep bench --process gioco.exe --seconds 60 --runs 5 --label baseline --out runs\
wpep tools install-presentmon    # scarica PresentMon 2.4.1 (Intel, MIT) se manca
```

Misura i frametime del processo con PresentMon: avg/median FPS, 1% low (99° percentile
del frametime), 0.2% low (99.8°). Ogni run è salvata in JSON con la serie completa dei
frametime, pronta per il confronto statistico di R3. Con FrameType disponibile i frame
generati (FG) sono esclusi dalle metriche, mai mescolati a quelli reali.

Cosa NON misura: l'input latency end-to-end. Non è misurabile in puro software,
e questo tool non fingerà mai il contrario.

## Build

```
dotnet build
dotnet test
```

Richiede .NET 10 SDK.
