# WPEP — Build Spec V1.1 (revisione concordata 2026-06-10)

Revisione della V1 (`WPEP_BuildSpec_V1.md` in Downloads) dopo discussione critica con l'utente.
I principi di V1 §1 (onestà), §4 (limiti di misura), §6 (statistica) e §9 (qualità) restano
validi integralmente e non vengono ripetuti qui.

## Decisioni prese (delta rispetto a V1)

1. **Destinazione: tool distribuibile**, non solo per il rig dell'autore.
   → build self-contained `win-x64`, zero assunzioni hardcoded sull'hardware,
   degradazione esplicita quando mancano privilegi o sensori.
2. **CLI prima, WPF dopo.** La UI di V1 §2 non è cancellata, è posticipata: arriva
   solo quando il motore è provato sul campo. Tutta la logica resta fuori dalla UI
   (come già richiesto da V1 §3), quindi il costo del rinvio è zero.
3. **Roadmap ribaltata: Diagnostics ETW è la milestone 1**, non la 6.
   V1 §3C lo definisce "il modulo a più alto valore reale" e poi lo metteva per ultimo —
   contraddizione rimossa.
4. **Multi-gioco by design.** Nessun supporto speciale per un titolo singolo.
   Il problema della ripetibilità degli scenari (Valorant/Fortnite non hanno benchmark
   integrato) si risolve con protocolli di misura documentati per gioco, non nel codice.
5. **Meno progetti in solution.** V1 prevedeva 10 progetti subito; si parte con 4
   (Core, Diagnostics, Cli, Tests) e si aggiungono Benchmark/Statistics/KnowledgeBase/
   Advisor/Reporting quando tocca a loro. Stessa separazione, meno scheletro vuoto.

## Roadmap rivista

- **R1 — Diagnostics CLI.** `wpep diag` : sessione ETW kernel (DPC/ISR), aggregazione
  per driver, classifica dei sospetti con max/avg/count e conteggio spike. Richiede admin;
  senza admin spiega cosa manca e perché. ← *milestone corrente*
- **R2 — Benchmark.** Wrapper PresentMon, parsing CSV, N run, percentili (mediana, 1% low,
  0.2% low). Process-agnostic.
- **R3 — Statistics.** Mann–Whitney U + bootstrap CI, noise floor, regola "nessun effetto
  misurabile". Unit test su dataset sintetici.
- **R4 — KnowledgeBase (ricerca).** Le ~15 voci seed di V1 §5 verificate su fonti primarie
  e ri-graduate. Deliverable doppio: JSON per l'app + documento leggibile.
- **R5 — Advisor + SystemAnalyzer.** Snapshot hardware/config, incrocio con KB, classificazione.
- **R6 — Reporting.**
- **R7 — UI WPF** (tema scuro, accento viola) sopra il motore ormai provato.

## Assunzioni
- .NET 10 SDK installato sulla macchina di sviluppo (runtime-only non basta).
- L'utente può lanciare la CLI da terminale elevato per le sessioni ETW.
- ETW in lettura passiva è compatibile con gli anti-cheat (Vanguard, EAC): non c'è injection
  né handle sul processo del gioco. Da verificare comunque sul campo alla prima cattura
  con un gioco attivo — se un anti-cheat si lamenta, lo si documenta e si cattura a gioco
  chiuso/in lobby.

## Rischi identificati
- I nomi delle proprietà TraceEvent per durata DPC/ISR vanno validati a build time
  (la libreria ha API storiche con naming irregolare).
- Senza elevazione `EnumDeviceDrivers` può restituire indirizzi azzerati → la risoluzione
  routine→driver degrada a "unresolved" e la CLI lo dice esplicitamente.
- Spike DPC sono visibili solo se accadono durante la finestra di cattura: una cattura
  breve e pulita può dare falsi negativi. Mitigazione: durata configurabile + indicazione
  nel report di quanto è rappresentativa la finestra.

## Alternative scartate
- **Script PowerShell + report senza app**: scartata perché l'utente vuole un tool
  distribuibile anche ad altri.
- **WPF da subito**: scartata, motore prima della pelle.
- **System.CommandLine / Spectre.Console**: rinviate; parsing argomenti manuale finché
  la CLI ha 2-3 comandi. Meno dipendenze = meno superficie.

## Domande aperte
- Firma/packaging per distribuzione (SmartScreen tratterà male un exe non firmato).
- Se PresentMon vada vendorizzato nel repo o scaricato al primo avvio (licenza MIT lo permette).
