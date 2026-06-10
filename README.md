# WPEP — Windows Performance Engineering Platform

Tool diagnostico per gaming competitivo che **misura, diagnostica e consiglia** — senza
mai toccare il sistema (V1 è read-only by design) e senza mai gonfiare i risultati.

Principio fondante: *dire la verità su cosa migliora davvero, con prove.* Per la maggior
parte dei "gaming tweak" la risposta onesta è "nessun effetto misurabile" — e questo tool
è costruito per dirlo.

## Stato

| Milestone | Stato |
|---|---|
| R1 — Diagnostics CLI (ETW DPC/ISR per driver) | ✅ in build |
| R2 — Benchmark (PresentMon, percentili) | ⬜ |
| R3 — Statistics (Mann–Whitney, bootstrap, noise floor) | ⬜ |
| R4 — KnowledgeBase con fonti primarie | ⬜ |
| R5 — Advisor + SystemAnalyzer | ⬜ |
| R6 — Reporting | ⬜ |
| R7 — UI WPF (tema scuro/viola) | ⬜ |

Documenti: [BUILDSPEC_V1.1.md](BUILDSPEC_V1.1.md) (decisioni correnti).

## Uso (R1)

```
wpep diag --seconds 30 [--json report.json]
```

Cattura una sessione ETW kernel e produce la classifica dei driver per latenza DPC/ISR
(max, media, conteggio spike >100µs/>500µs/>1ms). Richiede terminale **amministratore**
(vincolo di Windows sulle sessioni ETW kernel, non una scelta nostra).

Cosa NON misura: l'input latency end-to-end. Non è misurabile in puro software,
e questo tool non fingerà mai il contrario.

## Build

```
dotnet build
dotnet test
```

Richiede .NET 10 SDK.
