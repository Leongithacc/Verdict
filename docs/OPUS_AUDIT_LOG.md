# Registro di lavoro — Opus 4.8 (per revisione di Fable 5)

*Léon di solito lavora con Claude Fable 5. Durante la sua indisponibilità, Opus 4.8
ha continuato il progetto. Questo file marca CHI ha fatto COSA, così Fable può
ricontrollare ogni modifica fatta da Opus quando torna. Tracciabilità = coerente
con la filosofia del progetto (niente claim non verificabili).*

## Confine Fable → Opus

- **Ultimo commit dell'era Fable 5:** `53e9fdc` ("Public name decided: Verdict").
  Tutto ciò che precede (32 commit, fino a R7 + naming) è lavoro Fable, già
  validato da Léon sul campo (certificazione pipeline, test su 2 macchine).
- **Da qui in poi:** commit firmati `Co-Authored-By: Claude Opus 4.8`.
  NOTA: i commit dell'era Fable erano firmati "Claude Fable 5" per convenzione;
  Opus usa la propria firma per rendere il confine visibile in `git log`.

## Lavoro svolto da Opus 4.8 — DA RICONTROLLARE

### 1. Execution Engine V2 (commit successivo a 53e9fdc)
Nuovo progetto `src/WPEP.Execution` — il motore che APPLICA i tweak. Léon ha
deciso di saltare il gate "repo open source" e procedere con l'esecuzione.

**Cosa controllare con attenzione (è codice che SCRIVE sul sistema):**
- `RegistryAccess.cs` — lettura/scrittura/delete registry, parsing path HKCU/HKLM.
  Verificare lo split del path e la conversione dword (uint↔int unchecked).
- `ExecutionEngine.cs`:
  - `BuildPlan` — dry-run, legge i valori CORRENTI prima di proporre. Rifiuta
    placebo e gui-only. Solo metodo "registry" in questa build (powercfg/bcdedit
    ancora NON implementati — throw esplicito).
  - `Execute` — ordine: journal-PRIMA-della-scrittura → write → verify (rilettura)
    → stop immediato se la verify fallisce. Restore point best-effort via
    Checkpoint-Computer (richiede admin + System Restore attivo).
  - `Undo` — ordine inverso, ripristina valore precedente o cancella se non
    esisteva, verifica ogni ripristino, idempotente.
- KB: aggiunto campo `kind` (dword/string) alle 9 operazioni registry programmatiche.
- Test: `ExecutionEngineTests.cs` usa un FakeRegistry in-memory — **nessun test
  tocca il registry vero**. 8 test nuovi. Totale suite: 109 (era 101 a fine Fable).

**Principi di sicurezza rispettati (da spec EXECUTION_ENGINE_V2):** solo apply-spec
della KB, niente "ottimizza tutto", dry-run obbligatorio, journal granulare,
verify-after-write, undo reversibile. Placebo non applicabili by design.

### Punti dove Opus ha preso decisioni autonome (Fable valuti se confermare)
- Scartato LibreHardwareMonitor (era nella spec originale) per i sensori termici,
  usato nvidia-smi + ACPI → motivo: WinRing0 driver kernel viola leave-no-trace.
  (Questa decisione è dell'era Fable in realtà — commit da92d70. Confermata da Opus.)
- Engine: solo registry per ora. powercfg (power plan) e service mode: TODO.
- Restore point: best-effort, non bloccante se fallisce (il journal è l'undo primario).

### 2. Apply flow nella UI (commit 0452351)
- `src/WPEP.App/ApplyFlow.cs`: ExecutionService (wrap engine), ApplyDialogViewModel
  (dry-run + consent), ChangesViewModel (lista journal + undo).
- VerdictItem ora porta la TweakEntry e ha ApplyCommand (visibile solo se CanApply =
  registry method + non placebo). Bottone Apply accanto a How to.
- Dialog overlay: mostra l'operazione esatta before→after, risk text se rischioso,
  blocco admin se HKLM e non elevato. NIENTE scrittura finché l'utente non preme "Apply now".
- Pagina "Changes" (nuova voce sidebar): ogni sessione journaled, Undo per riga.
- RelayCommand<T> generico aggiunto a Infrastructure.cs.
- **Verificato visivamente da Opus** (computer-use): dialog renderizza corretto su
  Game DVR (`HKCU\...\AppCaptureEnabled before:1 → after:0`). NON applicato (scrittura
  reale = decisione dell'utente in chat). Léon farà lui il primo Apply vero.

### 3. Fix falso positivo managed-device (commit 0452351)
- Bug era nel probe di Fable (commit precedente): contava le chiavi placeholder
  `Enrollments\*` con EnrollmentState=1 che OGNI Windows ha → banner "company-managed"
  sul desktop personale di Léon. Verificato live: zero DiscoveryServiceFullURL/UPN/dominio.
  Fix: richiedere URL server MDM o UPN reale. Ora IsManagedDevice=False sul suo PC.

### 4. Sessione autonoma 2026-06-14 (Léon gioca, Opus lavora senza toccare il PC)
Nessun computer-use, nessuna apertura app, solo build/test/commit.
- **powercfg executor**: power-plan-high-performance ora applicabile one-click.
  Engine generalizzato per metodo (PlannedOperation/JournalEntry portano `Method`).
  IPowerCfg astratto, FakePowerCfg nei test (mai schema reale). DA RIVEDERE: RealPowerCfg
  lancia powercfg /setactive — controllare il parsing del GUID e l'undo.
- **KB 64→66**: Valorant (Raw Input Buffer/Reflex, fonti Riot+NVIDIA), CS2 (mito
  -tickrate placebo via sub-tick Valve, fps cap VRR), laptop per-app dGPU + Windows VRR
  (fonti MS DirectX blog). Tutte gui-only tranne le esistenti. DA RIVEDERE: gli URL fonte
  (alcuni potrebbero richiedere verifica, es. devblogs VRR).
- **Rilevamento giochi**: Valorant (metadata Riot) + CS2 (appmanifest_730 Steam) oltre
  Fortnite; sezioni per-gioco nascoste se assente. Verificato live: tutti e 3 =True sul PC.
- **Changes page**: ora mostra le operazioni di ogni sessione (path, before→after, stato)
  e disabilita Undo se già annullato.
- **Report**: include le modifiche applicate (journaled, non annullate).

### 5. Open settings deep-links (commit 8a69eac)
- ApplySpec.SettingsUri (nuovo campo): ms-settings:/control panel URI per voci gui-only.
- 8 voci gui-only collegate (gaming-gamebar, display-advancedgraphics, mousetouchpad,
  windowsdefender, network-ethernet, visualeffects, startupapps, ecc.).
- UI: voci gui-only con URI mostrano "Open settings" (apre la pagina, NON scrive).
  ExecutionService.OpenSettings via ShellExecute. DA RIVEDERE: alcune URI control-panel
  (services.msc, SystemPropertiesPerformance.exe) — verificare che aprano la pagina giusta.
- Verificato sul campo: power-plan apply+undo funziona (journal: BXTool→High Perf verified,
  poi undo → schema attivo di nuovo BXTool Gaming Profile Unpark). Engine certificato
  per registry E powercfg su macchina reale.
- NOTA DEPLOY: a fine sessione artifacts/app è STALE perché l'app di Léon era APERTA
  (PID lock). Source committato e test verdi; publish verificato pulito in cartella temp.
  Va ripubblicato in artifacts/app quando l'app è chiusa.

## Stato a fine sessione Opus (2026-06-14)
- `dotnet test`: **110/110 verdi**. `dotnet build WPEP.sln -c Release`: 0 errori.
- KB: 66 voci (19 forti, 17 plausibili, 14 controverse, 10 placebo, 6 risky);
  8 applicabili one-click (registry+powercfg), il resto gui-only (onesto: BIOS/in-game).
- Nessuna modifica distruttiva ai moduli core di Fable; engine/apply/detection sono additivi.
- DA RIVEDERE da Fable con PRIORITÀ: tutto WPEP.Execution (scrive su registry+powercfg),
  le 9 apply-spec registry + 1 powercfg in tweaks.json (path/valori uno a uno),
  i probe di rilevamento giochi (path Riot/Steam), gli URL fonte delle voci KB nuove.
