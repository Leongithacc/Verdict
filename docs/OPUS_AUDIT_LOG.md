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

### 6. powercfg-value executor (commit c6d81be)
- IPowerCfg esteso: QuerySettingIndex / SetSettingIndex (AC+DC su SCHEME_CURRENT + re-apply).
- Engine: metodo "powercfg-value", path = "subgroupGuid/settingGuid", value = indice.
- KB: core-parking (CPMINCORES=100) e usb-selective-suspend (USB_SUSPEND=0) ora applicabili.
  Validator esteso col nuovo metodo. FakePowerCfg con dict valori nei test.
- DA RIVEDERE: RealPowerCfg.QuerySettingIndex parsing "Current AC Power Setting Index: 0xNN";
  SetSettingIndex modifica lo schema ATTIVO (sul PC di Léon = BXTool Gaming Profile Unpark).
  GUID subgroup/setting (SUB_PROCESSOR/CPMINCORES, SUB_USB/USB_SUSPEND) da verificare.

### 7. BUGFIX dialog vuoto + reverts (commit 64bc9f8, d1f4938)
- **Bug riportato da Léon**: "clicco Apply ed e vuoto". Causa: core-parking e
  usb-selective-suspend usano setting powercfg NASCOSTI → `powercfg /query` non
  restituisce "Current AC Power Setting Index" → QuerySettingIndex throw → BuildPlan
  fallisce → dialog vuoto. Verificato live sul PC di Léon.
- **Fix**: core-parking + usb-suspend riportati a gui-only con deep-link a powercfg.cpl.
  Dialog ora mostra messaggio chiaro "can't apply automatically: <reason>" invece del box
  vuoto; "Apply now" nascosto se non c'e piano (HasPlan/ShowApplyButton).
  powercfg-value executor RESTA nel codice (corretto per setting non-nascosti, testato),
  ma nessuna voce KB lo usa piu.
- KB 67: aggiunto disable-sticky-keys-gaming (fonte MS, gui-only). Scartata una voce
  focus-assist perche non avevo URL verificato (regola d'oro).

### 8. Win+R launcher + giro di ricerca KB (commit 14e34a6, f51b3ce)
- **Launcher** (richiesto da Léon): Win+R "verdict" via App Paths HKCU + collegamento
  Desktop\Verdict.lnk. Reversibile (cancellare la chiave App Paths\verdict.exe).
- **KB 67→74** (7 voci, ricerca con fonti primarie verificate):
  - AMD Radeon Anti-Lag (evidence_strong, AMD DH-033, gpu:amd), RSR (plausible), HYPR-RX
    (plausible) — non intasano il Verdetto NVIDIA grazie al prereq gpu:amd.
  - RSS Receive Side Scaling (controversial, MS), audio exclusive mode (controversial, MS).
  - memory-compression-disable myth (placebo, MS), Spectre/Meltdown mitigations off
    (risky, MS KB - gemello di VBS: guadagno CPU reale, costo sicurezza).
- **Bug collaterale risolto**: OpenSettings non separava comando+args → i deep-link
  control.exe/powercfg.cpl/mmsys.cpl NON si aprivano. Ora splitta su primo spazio.
  DA RIVEDERE: le URI deep-link (mmsys.cpl, control.exe srchadmin.dll, ecc.) — verificare
  che aprano la pagina giusta sul campo.

### 9. Launcher fix + Win32PrioritySeparation (commit 1d23c04)
- Win+R "verdict" non funzionava (App Paths HKCU ignorato dal Run, serve HKLM/admin).
  Fix: `C:\Scripts\verdict.vbs` su PATH (PATHEXT include .VBS), apre l'app senza console.
  Verificato sul campo. App Paths HKCU rimossa.
- KB 74→75: win32priorityseparation-myth (controversial, MS archive). On-brand:
  l'impostazione "Programmi" fa gia il boost; i numeri magici da forum non hanno fonte.

### 10. Launcher restore — "Verdict non funziona piu" (2026-06-16)
- Diagnosi: il "non funziona piu" segnalato da Leon era il **Win+R**, non l'app.
  In un cleanup precedente avevo cancellato `C:\Scripts\verdict.vbs` E rimosso la chiave
  App Paths → `where verdict` vuoto, Run non risolveva nulla. L'app era integra
  (cartella artifacts/app completa, 43 file, Desktop\Verdict.lnk valido, nessun crash log).
- Fix (no admin, no VBScript deprecato su 26200):
  - chiave `HKCU\...\App Paths\verdict.exe` (default = exe, + Path) → Run risolve "verdict".
  - `C:\Scripts\verdict.cmd` di backup (start dell'exe) per risoluzione via PATH.
- DA TESTARE da Leon: Win+R → verdict. Fallback garantito = Desktop\Verdict.lnk.
  NB: contraddizione storica nel punto 9 (vbs "verificato sul campo") vs summary
  ("vbs deprecato"): risolta abbandonando del tutto VBScript.

### 11. Giro ricerca KB + Apex detection (2026-06-16)
KB **75→79** (ricerca con fonti primarie verificate via WebFetch in sessione):
- `qos-disable-user-presence` (plausible, MS Learn QoS) — **registry HKLM applicabile**
  (DisableUserPresenceQos=1). ONESTO: MS dice che l'abbassamento QoS per inattivita e
  SOLO a batteria → su desktop AC effetto nullo. expected_impact lo dichiara apertamente.
  DA RIVEDERE: path HKLM\...\Power\PowerThrottling\DisableUserPresenceQos, dword, reboot.
- `power-throttling-global-myth` (placebo, stessa fonte MS) — il foreground in focus e gia
  High QoS per design → PowerThrottlingOff globale non aggiunge nulla; chiave non documentata MS.
- `auto-hdr-visual-not-perf` (placebo, MS support) — Auto HDR e visivo, zero FPS/latenza.
  settings_uri ms-settings:display-hdr.
- `apex-reflex-on` (evidence_strong, NVIDIA, game:apex) — Apex Reflex -37% system latency
  (stessa pagina NVIDIA gia usata per Valorant/Fortnite). gui-only.
- **Detection Apex**: SystemSnapshot.ApexInstalled + GameInstalled("apex"); SnapshotBuilder
  ReadApexInstalled via Steam app 1172470. Refactor: estratti EnumerateSteamLibraries() +
  SteamAppInstalled(appId), CS2 ora li riusa (no duplicazione). DA RIVEDERE: app id Apex.
- OW2 NON aggiunto: detection Battle.net poco affidabile, evitato probe traballante.
- Validazione: build 0 err, 112/112 test (KnowledgeBaseTests carica la KB reale + validator).
  79 voci, 0 id duplicati. App+CLI ripubblicati in artifacts (app non era in esecuzione).

### 12. Esecutore bcdedit (2026-06-16) — chiude il TODO 'bcdedit' dell'engine
Nuovo metodo di esecuzione per la config di boot. SCRIVE SU BCD → da rivedere con cura.
- `BcdEditAccess.cs`: IBcdEdit + RealBcdEdit. Query parsa `bcdedit /enum {current}` per
  un elemento (regex per nome riga), normalizza il valore a lowercase (bcdedit mostra "Yes"
  ma accetta "yes" → verify case-insensitive). Set = `/set {current} <el> <val>`,
  Delete = `/deletevalue {current} <el>` (= ritorno al DEFAULT Windows). Solo elementi
  timer/tick passati dalla KB; mai identifier/device.
- `ExecutionEngine`: ctor accetta IBcdEdit (default RealBcdEdit). BuildPlan/ApplyOne/Undo
  hanno il ramo "bcdedit". Undo: se l'elemento ESISTEVA prima → ripristina il valore
  precedente; se NON esisteva → deletevalue (default). Verify dopo ogni write e dopo undo.
- `ApplyFlow`: CanApply include "bcdedit"; NeedsAdmin = SEMPRE true per bcdedit (boot store).
- KB: `disable-dynamic-tick` (risky) ora method "bcdedit" (disabledynamictick=yes,
  requires_reboot). SCELTA ONESTA: la voce resta gradata risky e "SCONSIGLIATA"; renderla
  applicabile serve come banco di prova REVERSIBILE+MISURATO (restore point + journal + undo
  one-click), il workflow che i suoi manual_steps gia descrivono. L'Advisor non la raccomanda.
- `hypervisorlaunchtype off` NON collegato di proposito: spegnerebbe l'hypervisor → romperebbe
  VBS/HVCI/Vanguard che Leon tiene attivi. Resta gui-only.
- Test: FakeBcdEdit in-memory (mai BCD reale). 3 nuovi test (apply+undo-delete, apply+undo-
  restore, shipped KB dynamic-tick costruisce il piano). Suite 112→**115 verdi**.
- **DA TESTARE da Leon/Fable sul campo**: RealBcdEdit (parsing /enum, casing valori, che
  /set accetti lowercase per tutti gli elementi). NB: Leon ha gia disabledynamictick=yes via
  BX Tool → applicare via Verdict legge "yes", target "yes" (no-op), undo ripristina "yes"
  (il suo stato), NON il default: semantica corretta.

### 13. "Apply all recommended" — batch dietro singolo dry-run (2026-06-16)
Bottone sul Verdict che applica TUTTI i tweak consigliati+applicabili in un colpo, dietro
una sola schermata di consenso. NON e un "ottimizza tutto": prende solo
Classification.Recommended ∩ CanApply (niente placebo/risky/gui-only), e ogni tweak passa
comunque per Execute → journal → verify → undo INDIVIDUALE nella pagina Changes.
- `ExecutionEngine.ExecuteAll(plans)` (primitiva, in WPEP.Execution): esegue in sequenza,
  si ferma al PRIMO verify fallito, ritorna (Applied, StoppedAt). I gia-applicati restano
  journaled e reversibili (nessun rollback automatico — coerente con la filosofia).
- `ApplyAllViewModel` (ApplyFlow.cs): Open() costruisce il piano combinato, PARTIZIONA
  onestamente i tweak che richiedono admin se non elevato (mostrati come "skipped", non
  persi) e quelli che BuildPlan rifiuta. Confirm() chiama ExecuteAll. Mai scrittura prima
  del consenso. AdminBlocked → bottone "Relaunch as administrator".
- VerdictViewModel: raccoglie `_applicableRecommended` durante Apply(); espone
  ApplyAllCommand + HasApplicableRecommended + label con conteggio.
- XAML: bottone PrimaryButton nell'header Verdict (visibile solo se ci sono consigliati
  applicabili) + overlay batch con lista scrollabile, rispecchia lo stile del dialog singolo.
- Test: ExecuteAll all-success (2 journaled) + stop-al-primo-fail. Suite 115→**117 verdi**.
- Verificato a runtime: app avviata 5s, XAML+binding caricano senza crash (smoke test),
  poi chiusa. App+CLI ripubblicati. DA RIVEDERE da Fable: la partizione admin e il fatto
  che ExecuteAll non fa rollback dei precedenti su stop (scelta deliberata, documentata).

### 14. Overwatch 2: detection + KB (2026-06-16) — chiude la coda
- Detection: OW2 e su Steam (app 2357570) dal 2023 → rilevabile in modo affidabile col
  helper SteamAppInstalled gia usato per Apex/CS2, niente probe Battle.net traballante.
  ReadOverwatch2Installed: Steam prima, poi fallback sul path default Battle.net
  (ProgramFilesX86\Overwatch\_retail_\Overwatch.exe). Null solo se davvero non determinabile.
  SystemSnapshot.Overwatch2Installed + GameInstalled("overwatch2").
- KB 79→**80**: `ow2-reflex-on` (evidence_strong, NVIDIA, game:overwatch2): Reflex +
  'Riduci buffering' Blizzard. gui-only. Gating per-gioco nasconde la sezione se assente.
- Build 0 err, 117 test (KnowledgeBaseTests valida ow2). App+CLI ripubblicati (KB=80).

### 15. Due gui-only → applicabili (2026-06-16) — valori registry verificati a fonte
Convertite due voci NON-placebo da gui-only a registry-applicabile (one-click + undo).
Valori di registro VERIFICATI via ricerca prima di scrivere (regola d'oro: no guessing):
- `hags-hardware-gpu-scheduling` (controversial): HwSchMode in
  HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers, dword 2=on/1=off, reboot. Fonte
  primaria gia presente (MS DirectX blog). HAGS ha gia detection (HagsEnabled) + stato
  Advisor → per Leon risultera "gia attivo" (niente Apply); per altri diventa one-click.
  Honesto: utile soprattutto perche REQUISITO di DLSS Frame Generation, neutro sugli FPS puri.
- `disable-sticky-keys-gaming` (plausible): Flags in HKCU\Control Panel\Accessibility\
  StickyKeys, REG_SZ "506" (disabilita hotkey Shift x5; default "510"). HKCU = NO admin.
  Effetto al prossimo accesso. QoL: stop al popup che ruba focus a meta partita.
- Applicabili one-click: 10 → **12**. Validati dal test ShippedKb_AllProgrammaticRegistry
  (costruisce il piano per ogni voce registry, kind dword/string). 117 test verdi.
  App+CLI ripubblicati. DA RIVEDERE da Fable: HwSchMode (no fonte MS *del registro*, solo
  della feature) e Flags=506 (puo variare per build Windows — verify lo intercetta).

### 16. CLI apply/undo — parita GUI↔CLI per applicare (2026-06-16)
La CLI (distribuibile, "CLI-first") ora puo APPLICARE, non solo misurare. Stesso engine
testato, stessa sicurezza.
- Nuovi comandi: `wpep apply <id> [--yes]`, `wpep apply-all [--yes]`, `wpep changes`,
  `wpep undo <file|last>`. WPEP.Cli ora referenzia WPEP.Execution.
- SICUREZZA: **dry-run di DEFAULT** — senza --yes stampa il before→after e NON scrive.
  Scrive solo con --yes. Rifiuta placebo/gui-only (CanApplyEntry). HKLM/boot → richiede
  terminale admin (EntryNeedsAdmin: bcdedit o path HKLM). Stop al primo verify fallito in
  apply-all. Undo idempotente.
- Verificato sul PC reale (dry-run, niente scritto): apply legge il valore LIVE
  (StickyKeys Flags=510→506), placebo rifiutato (exit 2), id inesistente (exit 2),
  apply-all = "nessun consigliato applicabile" (sistema di Leon gia ottimizzato → corretto).
- PrintUsage aggiornato; corretta la riga obsoleta "V1 non scrive MAI" (vero solo per i
  comandi di misura; apply e la via di scrittura deliberata con journal).
- FIX BUILD collaterale: WPEP.Diagnostics usava TraceEvent `Version="3.*"` (flottante) →
  conflitto publish NETSDK1152 (3.2.3 vs 3.2.4) quando il CLI ha tirato dentro Execution.
  Pinnato a `3.2.4` concreto + clean obj. DA RIVEDERE da Fable: il pin di versione.
- 117 test verdi. App+CLI ripubblicati.

## Stato a fine sessione Opus (2026-06-15)
- `dotnet test`: **112/112 verdi**. `dotnet build WPEP.sln -c Release`: 0 errori.
- KB: **75 voci** (20 forti, 20 plausibili, 17 controverse, 11 placebo, 7 risky);
  **8 applicabili one-click** (4 HKCU/powercfg no-admin, 4 HKLM admin),
  ~10 gui-only con deep-link "Open settings", il resto gui-only puro (BIOS/in-game).
  NB: core-parking/usb gui-only (setting powercfg nascosti, non scriptabili).
- Nessuna modifica distruttiva ai moduli core di Fable; engine/apply/detection sono additivi.
- DA RIVEDERE da Fable con PRIORITÀ: tutto WPEP.Execution (scrive su registry+powercfg),
  le 9 apply-spec registry + 1 powercfg in tweaks.json (path/valori uno a uno),
  i probe di rilevamento giochi (path Riot/Steam), gli URL fonte delle voci KB nuove.
