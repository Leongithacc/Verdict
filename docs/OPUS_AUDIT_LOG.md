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

### 17. Conflict guard per apply-all (2026-06-16) — sicurezza batch
Il campo KB `conflicts_with` esisteva ma NON era usato da nessuna parte: apply-all poteva
(in teoria) applicare due tweak mutuamente esclusivi. Chiuso.
- `WPEP.Advisor.ConflictResolver.Resolve(entries)`: conflitto UNDIRECTED (A vs B se uno dei
  due lista l'altro), tiene l'evidenza più forte (ordinal EvidenceLevel più basso), parità →
  ordine d'input; ritorna (Keep, Dropped con motivo).
- Agganciato in BOTH ApplyAllViewModel (GUI) e RunApplyAll (CLI): i tweak scartati per
  conflitto sono MOSTRATI nel dry-run con la ragione, non spariti in silenzio.
- Sul PC di Leon non si triggera (i pair in conflitto non sono tutti recommended+applicable),
  ma e corretto per un'app distribuibile e a prova di futuro (nuove voci recommended).
- Test: 4 nuovi (stronger vince, undirected, no-conflict, parità→ordine). Suite 117→**121**.
- App+CLI ripubblicati.

### 18. KB RTX 50 / NVIDIA 2025 (2026-06-16) — rilevante per la 5080 di Leon
KB 80→**82**, fonti primarie NVIDIA verificate in sessione (WebSearch):
- `nvidia-reflex-2-frame-warp` (evidence_strong, NVIDIA CES 2025): Frame Warp, -75%
  latenza dichiarata, Valorant <3ms. ONESTO: al lancio esclusivo RTX 50 (la sua 5080) +
  pochi titoli (Valorant, THE FINALS). Super rilevante per il suo competitive.
- `nvidia-smooth-motion` (controversial, NVIDIA support): frame-gen driver-level RTX 50,
  ~2x FPS percepiti MA aggiunge latenza → ONESTO: SCONSIGLIATO nei competitivi (Valorant/
  CS2/Apex), solo dove la fluidita conta piu della reattivita.
- Build 0 err, 121 test (KnowledgeBaseTests valida le 2 nuove). App+CLI ripubblicati (KB=82).

### 19. `wpep selftest` — validato il path di scrittura REALE sul campo (2026-06-16)
Fino ad ora TUTTO l'engine era testato solo con i fake (FakeRegistry/FakeBcdEdit/FakePowerCfg):
il path di scrittura reale non era mai stato esercitato su una macchina vera in sessione.
Chiuso il buco con un comando che e anche feature utile per il tool distribuibile.
- `wpep selftest`: usa le classi di PRODUZIONE (RealRegistryAccess + ExecutionEngine) per
  fare BuildPlan→Execute→verify→Undo su una chiave USA-E-GETTA `HKCU\Software\VerdictSelfTest`.
  Journal in dir TEMP (non sporca 'wpep changes'). Cleanup totale: valore + sottochiave
  (DeleteSubKeyTree) + journal temp. Exit 0 = PASS.
- **ESEGUITO LIVE sul PC di Leon → PASS**: before=<non impostato>, scritto+riletto 424242,
  undo rimosso, zero residui (Test-Path HKCU:\Software\VerdictSelfTest = false). Il metodo
  'registry' (HAGS/StickyKeys/GameDVR/SystemResponsiveness/...) e ora provato sul campo.
- Restano DA PROVARE con un apply vero: RealPowerCfg e RealBcdEdit (scrivono su power/boot,
  non scratch-testabili). Il selftest lo dichiara esplicitamente nell'output.

### 20. Report HTML arricchito (2026-06-16)
- ReportBuilder: ogni voce advisor applicabile one-click (registry/powercfg/bcdedit, no
  placebo) ora ha un badge "⚡ one-click" + riga riassuntiva "⚡ N applicabili in un clic".
  Helper IsOneClick (stessa regola di engine/UI). Vale per GUI E CLI (e nel ReportBuilder).
- CLI `wpep report`: ora popola AppliedChanges leggendo il journal (ReadAppliedChanges) →
  la sezione "Changes applied by Verdict" appare anche da CLI (prima solo GUI la riempiva).
- Test: 2 nuovi (placebo NON marcato; applicabile marcato + sezione changes). Suite 121→**123**.
- Verificato: report generato sul PC di Leon contiene il badge. App+CLI ripubblicati.

### 21. EngineSelfTest condiviso + bottone GUI + fix rumore restore-point (2026-06-16)
- Estratto il self-test da inline-CLI a `WPEP.Execution.EngineSelfTest`:
  `Run(IRegistryAccess, journalDir)` testabile coi fake; `RunReal()` = produzione
  (RealRegistryAccess + journal temp + cleanup totale subtree). Ritorna SelfTestResult/Steps.
- CLI `RunSelfTest` ora e un thin printer di RunReal() (rimosso using Microsoft.Win32 inutile).
- GUI: ChangesViewModel.SelfTestCommand (off-UI-thread) + bottone "Verifica motore" e esito
  nella pagina Changes → la feature di fiducia ora e anche nell'interfaccia, non solo CLI.
- Test: 2 nuovi (EngineSelfTest pass+cleanup coi fake; fail se le scritture non attecchiscono).
  Suite 123→**125**.
- FIX RUMORE: TryCreateRestorePoint rediretto solo stderr → l'AVVISO PowerShell di
  Checkpoint-Computer (restore point rate-limited a 1/24h) trapelava in console ad OGNI apply.
  Ora -WarningAction SilentlyContinue + redirect anche stdout → output pulito ovunque.
- Verificato: CLI selftest pulito (niente AVVISO), GUI si avvia col bottone (smoke 5s).

### 22. KB DLSS Override + DirectStorage + config G-SYNC ottimale (2026-06-16)
KB 82→**85**, fonti primarie verificate (WebSearch):
- `nvidia-dlss-override-transformer` (evidence_strong, NVIDIA App support): forza il modello
  'Latest' (transformer, Preset K) per Super Resolution/Ray Reconstruction. ONESTO: e QUALITA
  d'immagine (stabilita temporale, meno ghosting), NON FPS/latenza; nessuno svantaggio competitivo.
- `directstorage-game-dependent` (placebo, MS Learn GDK + GitHub): mito-buster. DirectStorage
  e integrata in Win11 ma NON ha toggle utente; i benefici dipendono dal singolo gioco. prereq ssd:nvme.
- `nvidia-optimal-gsync-vsync-reflex` (evidence_strong, NVIDIA guida latenza): la ricetta
  COMBINATA G-SYNC + V-Sync pannello ON + in-game OFF + Reflex/cap-3. Le singole c'erano gia;
  questa e il setup unico di frame-pacing, il piu impattante per il competitive.
- prereq ssd:nvme/monitor:hdr: cadono nel default del switch Advisor (non gata) — verificato.
- Build 0 err, **125 test verdi**. App+CLI ripubblicati (KB=85).
- NOTA INFRA: il build server parallelo crashava i nodi MSBuild (MSB4166) per contesa di
  processi orfani. Risolto buildando a nodo singolo (`-m:1 --disable-build-servers`) dopo aver
  chiuso gli helper (MSBuild/VBCSCompiler/testhost). Per Fable: se ricapita, stesso rimedio.

### 23. Rilevamento no-op + riordino gate admin (2026-06-16) — onesta UX
- `ExecutionPlan.IsAlreadyApplied`: true quando ogni op ha gia il valore target
  (ExistedBefore && Before==After). Applicare scriverebbe gli stessi byte → no-op.
- GUI ApplyDialog: mostra "Gia al valore desiderato: nessuna modifica necessaria" e nasconde
  "Apply now" (ShowApplyButton/CanConfirm escludono IsAlreadyApplied). Prima diceva
  "Applied and verified" pur non cambiando nulla → ora onesto.
- CLI `apply`: corto-circuito con lo stesso messaggio. INOLTRE riordinato: BuildPlan (sola
  LETTURA, no admin) e check no-op PRIMA del gate admin → "HAGS gia attivo" si puo dire
  senza terminale elevato (prima chiedeva admin solo per scoprire che non serviva nulla).
- Verificato live: HAGS (HKLM, gia ON) e mouse-precision (HKCU) → "gia al valore desiderato".
- Test: 2 nuovi (IsAlreadyApplied true/false). Suite 125→**127**. App+CLI ripubblicati.

### 24. `wpep doctor` — prontezza + valida read-only powercfg (2026-06-16)
Nuovo comando aggregatore, tutto sola-lettura/sicuro: sistema (CPU/GPU), admin, giochi
rilevati, verdetto (applicabili one-click / gia ottimali / placebo evitati), self-test del
motore (registry, chiave usa-e-getta) e LETTURA dello schema energetico.
- Bonus: esercita `RealPowerCfg.GetActiveScheme()` in lettura → valida il parsing del GUID
  schema SENZA scrivere. Sul PC di Leon: PASS + schema 80e4528b-... (il suo BXTool) letto OK.
  Chiude read-only parte del "DA RIVEDERE RealPowerCfg parsing".
- Eseguito live: Ryzen 7 9800X3D · RTX 5080, giochi Fortnite/Valorant/CS2, 0 applicabili /
  11 gia ottimali / 13 placebo (sistema gia super-tweakato), self-test PASS.
- Aggregatore di pezzi gia testati → verificato live, niente unit test dedicato. 127 test.

### 25. ApplyPolicy: unica fonte per CanApply/NeedsAdmin/DecideAction (2026-06-16)
La logica safety-critical "posso applicare? serve admin? cosa faccio?" era DUPLICATA in 3
punti (ExecutionService App, helper CLI, e in parte ReportBuilder) e NON testata nella CLI.
- Nuovo `WPEP.Execution.ApplyPolicy`: CanApply, NeedsAdmin, e `DecideAction` (enum ApplyAction:
  NotApplicable/AlreadyApplied/NeedsAdmin/DryRun/Execute) con precedenza esplicita
  (already-applied batte il gate admin: dire "niente da fare" non richiede elevazione).
- App ExecutionService e CLI ora delegano ad ApplyPolicy (no piu copie divergenti).
  CLI RunApply riscritto attorno a DecideAction (hint admin preservato anche nel dry-run).
- ReportBuilder.IsOneClick: lasciata locale (display, non decisione di scrittura) ma allineata.
- Test: 14 nuovi (CanApply theory, NeedsAdmin HKLM/HKCU/bcdedit, DecideAction tutti i rami +
  precedenza). Suite 127→**141**. Verificato live: HAGS=AlreadyApplied, StickyKeys=DryRun,
  network-throttling(HKLM gia applicato)=AlreadyApplied senza admin, placebo=NotApplicable.

### 26. Coerenza no-op in apply-all + cs2-reflex (2026-06-16)
- apply-all (GUI + CLI) ora salta i tweak gia al valore desiderato ("gia al valore
  desiderato — niente da fare") come fa il single-apply. Difensivo: l'Advisor gia esclude
  i "gia attivi" dai Recommended, ma per le voci senza detection di stato e piu onesto.
  Auto-review: ConflictResolver in ApplyAllViewModel.Open verificato (kept/conflicts ok).
- KB 85→86: `cs2-reflex-on` (evidence_strong, NVIDIA) — CS2 integra Reflex; compare in cima
  alla sezione per-gioco CS2 del verdetto di Leon (verificato live con `wpep advise`).

### 27. Undo drift-aware (2026-06-18) — non clobbera le modifiche manuali
Prima `Undo` riscriveva SEMPRE il valore "before": se l'utente cambiava quel valore a mano
DOPO l'apply, l'undo glielo sovrascriveva in silenzio. Ora e drift-aware.
- `Undo` ritorna `UndoOutcome(Restored, Skipped)`. Per ogni voce legge il valore CORRENTE:
  - == before (gia ripristinato) → no-op;
  - == quello che Verdict ha scritto (ValueAfter) → ripristina + verify (comportamento storico);
  - altro (DRIFT, cambiato fuori da Verdict) → SALTA, lascia la modifica manuale, lo riporta.
- Refactor: estratti `ReadCurrent` (valore live per metodo) e `RestoreOne` (ripristino+verify).
- Chiamanti aggiornati: ExecutionService, ChangesViewModel (GUI mostra gli skip), CLI RunUndo
  (stampa "[saltato] ..."), EngineSelfTest (.Restored).
- Test: 2 nuovi (drift → skip + preserva il valore manuale; gia-ripristinato → no-op non drift).
  Suite 141→**143**. Verify storico preservato; selftest live ancora PASS.
- DA RIVEDERE da Fable: la semantica drift su powercfg/bcdedit (logica condivisa, ma il path
  reale di quei due metodi resta da provare con un apply vero).

### 28. Test d'integrazione apply-all (2026-06-18)
`ApplyOrchestrationTests`: pinna l'INTERAZIONE del flusso che CLI/GUI eseguono
(ConflictResolver → BuildPlan → ExecuteAll → Undo), non solo i pezzi singoli.
2 scenari: apply-all+undo round-trip allo stato originale; conflitto → applica solo il lato
tenuto (l'altro mai toccato). Suite 143→**145**.

### 29. VALIDAZIONE SUL CAMPO con Leon (2026-06-18) — chiude diversi "DA TESTARE"
Leon ha eseguito gli apply reali (decisione sua, in chat). Risultati:
- ✅ **Scrittura registry reale**: `wpep apply disable-sticky-keys-gaming --yes` → before 510 →
  after 506, "Applicato e verificato", journal creato; `wpep undo last` → "Annullate 1
  modifiche". Il ciclo write→verify→journal→undo funziona DAL COMANDO VERO (non solo selftest).
- ✅ **RealBcdEdit in LETTURA**: `wpep apply disable-dynamic-tick` (admin) → "Già al valore
  desiderato, before yes → after yes". Query/parsing del boot config reale OK + no-op su bcdedit.
- ✅ **Launcher Win+R "verdict"** apre l'app. NOTA: lasciava una finestra cmd (Run usa
  C:\Scripts\verdict.cmd via PATH; App Paths HKCU ignorato dal Run, conferma il vecchio dubbio).
  FIX dato a Leon: App Paths in HKLM (admin) + rimozione verdict.cmd → lancio diretto WPEP.exe.
- FIX cosmetico: col `--yes` la CLI stampava ancora "Dry run —"; ora "Applico ora — modifiche:".
- ✅ **Scrittura powercfg reale** (Task 4): `wpep apply power-plan-high-performance --yes` →
  schema attivo 80e4528b (BXTool) → 8c5e7fda (High Perf), "Applicato e verificato",
  `powercfg /getactivescheme` conferma "Prestazioni elevate"; `undo last` → ripristina BXTool.
  RealPowerCfg.SetActiveScheme WRITE + undo drift-aware VALIDATI sul campo.
- BILANCIO: ora validati SUL PC reale → registry WRITE+undo, powercfg WRITE+undo, bcdedit READ.
  Resta solo RealBcdEdit WRITE (Set/Delete): non testabile pulito sul suo PC (gia 'yes');
  logica identica al pattern registry/powercfg (gia provati) + coperta dai fake.
- COSMETICO NOTO: il CLI in artifacts non si ripubblica se la GUI WPEP e aperta (Get-Process
  WPEP matcha sia wpep.exe CLI che WPEP.exe GUI → publish saltato). Ripubblicare a GUI chiusa.
- ✅ **Launcher Win+R RISOLTO**: la cmd veniva da C:\Scripts\verdict.cmd (Run consulta App
  Paths HKLM+PATH, NON HKCU). Fix no-admin: C:\Scripts\verdict.vbs (lancio nascosto via
  WScript.Shell, wscript = no console) + rimosso verdict.cmd. Verificato: 0 console.
  (File di launcher fuori dal repo; VBScript confermato attivo su 26200.)

### 30. V3 — Framework "Lab" a feature-flag (2026-06-18) — la fondazione richiesta da Leon
Decisione architetturale di Leon: troppe feature premium → non tutte sempre-attive (non clean).
Soluzione: una pagina **"Lab"** dove ogni modulo premium è un TOGGLE; l'utente accende solo ciò
che vuole. Costruito il framework completo:
- `WPEP.Execution/FeatureCatalog.cs` (dati puri, NO-WPF → testabile e condivisibile col CLI):
  `FeatureModule(Id,Name,Tagline,Category,DefaultEnabled,Status,Heavy,Glyph)` + `FeatureStatus`
  {Stable,Beta,Experimental}. Catalogo di 18 moduli (tutte le idee che Leon ha scelto: Score,
  Ghost Tweak, Time Machine, Regression Sentinel, Watchdog, Ottimizza-per-gioco, Multi-monitor,
  Explain-my-Stutter, Risk Slider, Reaction/Latency Lab, Network Duel, Rig DNA, AI co-pilot,
  Trust mode, Fresh-install, Evidence community, Placebo Museum), raggruppati per categoria.
- Persistenza in `AppSettings` (settings.json): `Dictionary<string,bool> Features` +
  `IsFeatureEnabled(id)` (override utente, else default catalogo) + `SetFeature(id,on)` che
  RIMUOVE la voce quando == default (file piccolo, default ri-modificabili in futuro senza
  stompare le scelte). I moduli **pesanti** (Watchdog, Sentinel) partono OFF di default.
- GUI: `LabViewModel`/`FeatureRow`/`FeatureGroup` + pagina XAML (nav "Lab") con card raggruppate,
  badge stato (BETA/SPERIMENTALE) + badge BACKGROUND per i moduli heavy, contatore "N di M attivi".
- Test: `FeatureCatalogTests` (no-dup id, moduli completi, heavy=>default OFF, costanti→modulo
  reale, Get). **154/154 verdi.** Build App 0/0.
- I MODULI sono ancora gusci: il framework c'è, le feature si "riempiono" una a una leggendo
  `settings.IsFeatureEnabled(id)` nei rispettivi hook. Prossimo: implementare i primi moduli
  backend-testabili (Multi-monitor, Explain-my-Stutter, Risk Slider, Ghost Tweak).

### 31. V3 — Primo modulo Lab cablato: VERDICT SCORE (2026-06-18) — onesto, anti-placebo
Prova end-to-end che il framework feature-flag funziona: il modulo `score` (default-ON) ora
esiste davvero, non è più un guscio.
- `WPEP.Execution/VerdictScore.cs` (puro, deterministico, testabile): `Compute(ScoreInput)` →
  `ScoreResult(Score 0-100, Band, BandColor, Breakdown[], HonestyNote)`. Modello: parte da 100 e
  sottrae solo deduzioni REALI ed evidence-backed: −6 per ogni tweak consigliato non fatto (cap 54,
  non azzera mai da solo), −15 EXPO/XMP spento (perf gratis misurabile), −5 per tweak rischioso
  attivo (cap 20). BAND: Eccellente≥90 / Buono≥75 / Discreto≥55 / Da sistemare. **L'ONESTÀ È LA
  FEATURE**: i placebo NON muovono il numero (test lo prova) + nota esplicita "non gonfiamo il
  punteggio". Nessun altro tool lo fa — è l'identità del progetto resa numero.
- EXPO: aggiunto `HardwareInventory.ExpoEnabled` (tri-state) + `HardwareScanner.DetectExpo` (stessa
  euristica del finding, esposta pulita). `ScanViewModel` espone `ExpoEnabled` + evento
  `ScanCompleted` → la Verdict ricomputa lo Score quando lo scan hardware atterra (EXPO arriva dopo).
- GUI: card "hero" nella pagina Verdict (numerone colorato per band via TokenBrush + breakdown
  con delta firmati + nota onestà), gated da `ShowScore` (= `IsFeatureEnabled("score")`). Toggle
  nel Lab → si riflette al rientro nella pagina Verdict (RecomputeScore in OnNavVerdict).
- Onestà conservativa: RiskyActive/PlaceboActive passati a 0 perché NON possiamo confermare quali
  tweak rischiosi/placebo siano davvero attivi ora → non penalizziamo ciò che non sappiamo provare.
- Test: `VerdictScoreTests` (6: sistema perfetto=100, cap pending, EXPO=−15, placebo non muove,
  clamp 0-100, EXPO unknown=no penalità). **160/160 verdi**, build App 0/0.

### 32. V3 — Moduli Lab 2 e 3: RISK SLIDER + MULTI-MONITOR (2026-06-18, scelti da Léon via cps)
Léon ha scelto questi due (cps). Entrambi: logica pura in lib (testabile) + GUI gated dal flag.
**Risk Slider** (`WPEP.Execution/RiskSlider.cs`): manopola `RiskTolerance` {Sicuro/Bilanciato/
  Aggressivo/Estremo}. `Includes(tol, riskTier, isPlacebo)`: include i tweak il cui tier di rischio
  KB (None=0..High=3) ≤ tolleranza; **i placebo MAI a nessun livello** (allarga quanto sei
  RISCHIOSO, non quanto sei INUTILE). `Describe` per la UI. Persistito in `AppSettings.RiskTolerance`
  (default Bilanciato). GUI: card con Slider 0-3 sulla pagina Verdict (gated `ShowRiskSlider`),
  mostra profilo + "N tweak in ambito · M troppo rischiosi · K placebo esclusi" dai risultati
  advise correnti. `RiskSliderTests` (11, inclusa monotonìa: alzare la tolleranza non toglie mai
  nulla). Self-determina lo scope da `recommendations` in Apply().
**Multi-monitor optimizer** (`WPEP.SystemAnalyzer/DisplayScanner.cs`): enumera i display via Win32
  `EnumDisplayDevices`/`EnumDisplaySettings` (P/Invoke, **NO driver kernel** → anti-cheat safe, solo
  lettura) → `DisplayInfo(Name,W,H,RefreshHz,IsPrimary)`. `Analyze` (puro, testato): consigli onesti
  per gaming — gioca/imposta-primario il pannello a Hz più alto, avviso refresh misti (micro-stutter
  su alcune GPU), suggerimento Win+P "solo schermo PC" in competitiva (meno carico compositore/input
  lag), nudge VRR/G-SYNC per-display. Verdict NON cambia la config display, rimanda alle impostazioni
  Windows. GUI: sezione "MONITOR" nel build-sheet della pagina Scan (gated `ShowMultiMonitor`);
  `ScanViewModel` ora prende `AppSettings`, refresh leggero su nav Scan. `DisplayScannerTests` (6).
- NB: aggiunto ProjectReference WPEP.SystemAnalyzer al progetto test (mancava). **177/177 verdi**, build 0/0.

### 33. V3 — Modulo Lab 4: EXPLAIN MY STUTTER (2026-06-18) — DPC/ISR in italiano
Riusa il motore diagnostico esistente (nessuna nuova misura, nessuna scrittura).
- `WPEP.Diagnostics/StutterExplainer.cs` (puro/testato): `Explain(DpcIsrReport)` → `StutterReport`
  (Overall severity + headline + findings). Soglie: <400µs sano, 500µs+ o spike500 = Likely,
  1000µs+ o spike1000 = Severe. Salta gli `<unresolved>`. `DescribeDriver` mappa il file driver →
  componente in italiano (nvlddmkm→GPU NVIDIA, ndis/tcpip→scheda di rete, portcls/rtkvhd→audio,
  stornvme→disco/SSD, usbxhci→USB, acpi→energia, ecc.) con fallback onesto "un driver di sistema".
  Tip per categoria (aggiorna driver GPU/rete/audio...). Sul PC di Léon (nvlddmkm max 277µs, zero
  spike) → "Nessun colpevole, sistema pulito" (coerente col risultato reale documentato).
- GUI: card "Explain my Stutter" nella pagina Diagnostics (MultiDataTrigger su ShowStutterExplain
  + HasStutterResult), headline colorata per severità + finding (componente/spiegazione/tip).
  Popolata dopo la cattura DPC/ISR. `StutterExplainerTests` (8). **188/188 verdi**, build 0/0.

### 34. V3 — Modulo Lab 5: TRUST MODE (2026-06-18) — manifesto security-review, on-brand
Per i paranoici: mostra ESATTAMENTE cosa Verdict scriverebbe, prima di fidarsi. Solo lettura.
- `WPEP.Execution/TrustManifest.cs` (puro/testato, NESSUN accesso al sistema): `Build(tweaks)` →
  `TrustEntry(TweakId,TweakName,Operations[])` dove `ChangeOperation(Method,Target,NewValue,Kind,
  NeedsAdmin,Reversible,RequiresReboot)`. Costruito staticamente dalle ApplySpec della KB; include
  solo gli applicabili (riusa `ApplyPolicy.CanApply`/`NeedsAdmin`); reversibile = undo≠none; metodi
  senza operations path-based (es. powercfg plan switch) ottengono comunque una riga (mai nascondere
  nulla). `Summarize` → "N tweak · M operazioni · tutte reversibili · K richiedono admin".
- GUI: sezione "Trust mode" nella pagina Changes (gated `ShowTrustMode`), card per tweak con badge
  metodo + target/valore mono + badge admin/reversibile/reboot. Changes ora in ScrollViewer.
  Rebuild del manifesto su nav Changes. ChangesViewModel ora prende anche AppSettings.
- `TrustManifestTests` (6). **194/194 verdi**, build 0/0.
- STATO LAB: 5 moduli VIVI su 18 → Verdict Score, Risk Slider, Multi-monitor, Explain-my-Stutter,
  Trust mode. Pattern consolidato (logica pura in lib + test + Show<Feature> flag + GUI gated + refresh su nav).

### 35. V3 — Modulo Lab 6: RIG DNA (2026-06-18) — firma/trading-card generativa
Per il gusto estetico di Léon: trasforma l'inventario hardware in un'identità unica da collezione.
- `WPEP.SystemAnalyzer/RigDna.cs` (puro/deterministico, hash FNV-1a, NIENTE random → testabile e
  stabile tra run): `Compute(inv)` → `RigDnaResult(Code "RIG-XXXX-XXXX", Tier, TierColor, Traits[],
  Hue 0-359)`. Code = base32 Crockford (no I/L/O/U) dell'hash della firma canonica (mobo|cpu|cores|
  gpu|ram|disk). Tier = euristica di potenza (core, GPU top, RAM≥32, EXPO on, NVMe) → COMUNE/RARO/
  EPICO/LEGGENDARIO/MITICO. Traits = "8-Core/16T", GPU short, "32GB RAM", "EXPO ✓/✗". Sul rig di
  Léon → MITICO.
- GUI: card "RIG DNA" nel build-sheet (pagina Scan, gated `ShowRigDna`), tinta da un colore HSL
  derivato dalla Hue generata (TintFromHue, frozen), code grande + badge tier + chip traits (WrapPanel).
- Refresh Lab sections su nav Scan unificato (`EnsureLabSectionsAsync`: ri-scansiona solo se una
  sezione ora-attiva non ha dati; spegnerla la pulisce senza rescan). `RigDnaTests` (6: determinismo,
  shape code, tier beast/weak, EXPO trait). **200/200 verdi**, build 0/0.
- STATO LAB: **6 moduli VIVI su 18** → Verdict Score, Risk Slider, Multi-monitor, Explain-my-Stutter,
  Trust mode, Rig DNA.

### 36. V3 — Modulo Lab 7: GHOST TWEAK (2026-06-18) — A/B alla cieca, l'idea-firma anti-placebo
La ⭐⭐ del progetto: applica un tweak SENZA dire quale, tu misuri, poi RIVELA se ha aiutato davvero.
Doppio-cieco su te stesso. Apply CIECO REALE (journaled) + reveal + undo automatico.
- `WPEP.Execution/GhostTweak.cs` (puro/testato): `Pick(candidateIds, seed)` selezione cieca
  deterministica-nel-seed (l'app passa seed random → imprevedibile; modulo abs-safe per ogni seed
  incl. int.MinValue). `Reveal(name, GhostOutcome, delta)` → testo onesto: Helped="non è placebo per
  te, tienilo" / NoEffect="placebo per te anche se popolare" / Hurt="già annullato" / Inconclusive=
  "misura troppo rumorosa". `GhostTweakTests` (7).
- `GhostTweakViewModel` (App): candidati = KB CanApply && !NeedsAdmin (un round cieco non chiede mai
  UAC); StartRound prova pick finché uno non-already-applied → Execute reale (journaled, salva il
  journal file) → stato Applied. Reveal: Undo (ripristina SEMPRE) → mappa il verdetto da
  `MeasureWizard.LastComparison` (Improvement→Helped, Regression→Hurt, NoMeasurableEffect→NoEffect,
  gate/assente→Inconclusive) → mostra il reveal. Esposto `MeasureWizard.LastComparison` (nuovo).
- GUI: sezione "Ghost Tweak" in cima alla pagina Measure (gated `Ghost.ShowGhostTweak`), spiegazione
  + bottone Inizia/Rivela + card reveal colorata. Riusa il loop di misura del wizard per il verdetto.
- **207/207 verdi**, build 0/0. STATO LAB: **7 moduli VIVI su 18** → Score, Risk Slider, Multi-monitor,
  Explain-my-Stutter, Trust mode, Rig DNA, Ghost Tweak.

### 37. V3 — Modulo Lab 8: PLACEBO MUSEUM (2026-06-18) — galleria miti sfatati, condivisibile
L'onestà del progetto resa contenuto virale: i tweak che tutti consigliano ma non fanno niente.
- `WPEP.KnowledgeBase/PlaceboMuseum.cs` (puro/testato): `Build(entries)` filtra EvidenceLevel.Placebo
  → `PlaceboExhibit(Id,Name,Category,Myth=ExpectedImpact,Truth=Description,Sources)`, ordinato per
  categoria/nome. `Count` per il riepilogo. `PlaceboMuseumTests` (5).
- GUI: pannello "Placebo Museum" nella colonna detail della pagina KB, mostrato quando il modulo è
  ON e nessuna voce è selezionata (MultiDataTrigger ShowPlaceboMuseum + Selected==null). Ogni mito:
  badge PLACEBO + "Il mito:" / "La verità:" (rosso). KbViewModel ora prende AppSettings.
- **212/212 verdi**, build 0/0. STATO LAB: **8 moduli VIVI su 18**.

### 38. V3 — Modulo Lab 9: FRESH-INSTALL SCORE (2026-06-18) — drift avvii di terze parti
- `WPEP.SystemAnalyzer/FreshInstallScanner.cs`: `EnumerateStartup` (WMI Win32_StartupCommand, no
  driver) → `StartupItem(Name,Command,Location,IsMicrosoft)`. `IsMicrosoft` euristica path
  (\windows\/system32/microsoft/windowsapps). `Analyze` (puro/testato): score = clamp(100 − terze
  parti×6), band Pulito/Normale/Affollato/Sovraccarico, headline onesta ("N programmi di terze parti
  all'avvio oltre al Windows base"). Framing onesto: contiamo il drift di terze parti, non fingiamo
  un diff con un'immagine pulita specifica. `FreshInstallScannerTests` (5, 8 casi).
- GUI: sezione "FRESH-INSTALL" nel build-sheet (pagina Scan, gated `ShowFreshInstall`): score/100 +
  band + headline + chip dei programmi di terze parti (WrapPanel). EnsureLabSectionsAsync esteso.
- **220/220 verdi**, build 0/0. STATO LAB: **9 moduli VIVI su 18** (metà!).

### 39. V3 — Modulo Lab 10: OPTIMIZE FOR [GAME] (2026-06-18) — piano su misura per titolo
- `WPEP.Advisor/OptimizeForGame.cs` (puro/testato, proiezione KB): `AvailableGames(entries)` (giochi
  con entry dedicate) + `Build(game, entries)` → `GameOptimization(Game, SystemTweaks[], InGameSettings[])`.
  SystemTweaks = entry di sistema (Game==null) strong+plausible (NO placebo/risky). InGameSettings =
  entry per quel gioco. `OptimizeForGameTests` (4). Léon NON ha access PC (remote control fino 4:30) →
  modulo backend-testabile, GUI a basso rischio.
- GUI: card "Ottimizza per gioco" sulla pagina Verdict (gated `ShowOptimizeForGame`): ComboBox giochi
  + due colonne (tweak di sistema / impostazioni in-game-driver). VerdictViewModel cache KB +
  RefreshGames su nav. **224/224 verdi**, build 0/0. STATO LAB: **10 moduli VIVI su 18**.

### 40. V3 — Modulo Lab 11: TIME MACHINE (2026-06-18) — timeline "cos'è cambiato"
- `WPEP.SystemAnalyzer/SystemTimeline.cs`: `SystemState` (TakenAtIso, ExpoEnabled, RamGb, Gpu, Bios,
  ThirdPartyStartup) + `Diff(older,newer)` PURO/testato (riporta solo i campi diversi; unknown→unknown
  non è un cambiamento) + persistenza JSON in data/timeline/ (Save/LoadAll, filename ordinabile).
  `SystemTimelineTests` (6).
- App: in ScanAsync (gated `ShowTimeMachine`) costruisce lo stato dall'inventario + conteggio avvii,
  carica la baseline precedente, diffa, e salva SOLO stati distinti (prima volta = baseline). GUI:
  sezione "TIME MACHINE" nel build-sheet con headline + righe Campo: before → after.
- **230/230 verdi**, build 0/0. STATO LAB: **11 moduli VIVI su 18**.
- NOTA: i restanti 7 moduli richiedono o l'occhio di Léon sulla GUI (Latency Lab grafici, Reaction
  Lab minigioco) o infra/servizi (AI co-pilot=LLM, Evidence community=server, Watchdog/Regression
  Sentinel=background tray). Backend-autonomo resta solo Network Duel.

### 41. V3 — Modulo Lab 12: NETWORK DUEL (2026-06-18) — qualità rete con voto
- `WPEP.SystemAnalyzer/NetworkDuel.cs`: `Anchors` (Cloudflare/Google baseline + CDN Riot/Steam/Epic),
  `PingHost` (ICMP best-effort, null=loss), `Analyze` PURO/testato → `NetworkResult(avg, jitter=media
  |diff consecutivi|, loss%, Grade A-F + colore). Soglie gaming. Framing ONESTO: molti server di
  gioco bloccano ICMP → sono anchor di rotta, non il match server. `NetworkDuelTests` (6).
- GUI: card "Network Duel" nella pagina Diagnostics (gated `ShowNetworkDuel`) con bottone "Test rete"
  + righe target/avg/jitter/loss/grade. Diagnostics ora in ScrollViewer. **236/236 verdi**, build 0/0.
- **STATO LAB: 12 moduli VIVI su 18.** I 6 rimasti NON sono autonomi: Latency Lab (grafici, serve
  occhio), Reaction Lab (minigioco interattivo), AI co-pilot (LLM), Evidence community (server — non
  costruire fake), Watchdog + Regression Sentinel (servizi tray background). → da fare con Léon al PC
  o come lavori dedicati più grandi.

### 42. V3 — CLI parity dei moduli Lab + 2 BUG VERI trovati sul PC di Léon (2026-06-18)
Léon (su remote-control) ha chiesto "altro di utile". Ho esposto i moduli via CLI e LANCIATI sul suo
PC reale → dati veri + due bug scoperti e fixati.
- **CLI parity** (`src/WPEP.Cli/Program.cs`): nuovi comandi sola-lettura `score`, `dna`, `fresh`,
  `network`, `timeline`, `museum`, `games`, `optimize <gioco>` — stessa logica pura della GUI. Help
  aggiornato. Eseguiti live sul suo PC (es: Score 31/100 "Da sistemare" per EXPO off + 11 pending;
  Rig DNA EPICO; Fresh 0/100 con 23 avvii terze parti; rete A/B; Epic blocca ICMP).
- **BUG 1 — GPU picker (RigDna pescava la iGPU)**: il suo PC ha iGPU AMD (9800X3D) + dGPU NVIDIA
  RTX 5080; `Gpus.FirstOrDefault()` prendeva la iGPU → DNA "Graphics", tier RARO sbagliato. Fix:
  `GpuPicker.Best()` (preferisce discrete RTX/GTX/RX dddd/Arc, poi non-integrata) + `HardwareInventory.
  PrimaryGpu`. RigDna + Time Machine ora usano PrimaryGpu. Ora: RTX 5080, tier EPICO. `GpuPickerTests` (5).
- **BUG 2 — advisor mostrava tweak AMD-GPU a un gamer NVIDIA**: l'AdvisorEngine non aveva il case
  `gpu:amd` (le voci AMD ce l'avevano già in KB ma cadeva nel default=applicabile) né `laptop`.
  Aggiunti entrambi i case (gpu:amd → richiede GpuName AMD/Radeon; laptop → IsDesktop==false) +
  taggato `laptop-dgpu-preference` con "laptop". Ora optimize/advise filtra correttamente: a Léon
  spariscono Anti-Lag/HYPR-RX/RSR/laptop, resta "fTPM AMD BIOS" (corretto, ha CPU AMD). Migliora
  TUTTO l'advisor, non solo il modulo. `AdvisorEngineTests` +3.
- **OptimizeForGame** ora accetta `SystemSnapshot?` opzionale → filtra i tweak di sistema per
  l'hardware reale (via AdvisorEngine, esclude NotApplicable). CLI+GUI passano lo snapshot.
- **246/246 verdi**, solution build 0/0. (Snapshot GpuName era già corretto = NVIDIA, doctor lo conferma;
  il bug GPU era solo in RigDna/timeline che usavano Gpus[0].)

### 43. V3 — Modulo Lab 13: WATCHDOG (core + CLI) (2026-06-18)
Il modulo "funziona da solo": sorveglia le derive di un sistema messo a punto. Costruito il MOTORE
(puro/testabile) + comando CLI; il loop tray continuo è una shell sottile da aggiungere con Léon al PC.
- `WPEP.Execution/WatchdogCheck.cs` (puro/testato): `Evaluate(WatchInputs)` → `WatchAlert(Level,
  Title,Detail)[]`. Rileva: EXPO passato da on→off (regressione), tweak applicati ANNULLATI (drift),
  crescita degli avvii (≥3 nuovi). `Worst()` per il colore icona. Read-only: segnala, non "ripara"
  di nascosto.
- `ExecutionEngine.DetectDrift()` (nuovo, READ-ONLY): per ogni voce journaled non annullata, confronta
  il valore live con `ValueAfter` → `DriftItem(TweakId,Path,Expected,Actual)` se non regge più
  (riusa `ReadCurrent`, nessuna scrittura). Esposto da `ExecutionService.DetectDrift`.
- CLI `wpep watch`: raccoglie EXPO (scan) + baseline (Time Machine) + startup + drift (journal) →
  Evaluate. Guida a `wpep timeline` se manca la baseline. Help aggiornato.
- `WatchdogCheckTests` (7). **253/253 verdi**, App+CLI build 0/0.
- DA FARE (con Léon al PC): GUI Watchdog (sezione/tray) — è cross-cutting (scan+execution), meglio a video.

### 44. V3 — Modulo Lab 14: REGRESSION SENTINEL (core + CLI) (2026-06-18)
Il companion del Watchdog: ti avvisa quando le prestazioni PEGGIORANO nel tempo (es. un Windows
Update rompe qualcosa). Nessun tool ti dice quando regredisci.
- `WPEP.Statistics/RegressionSentinel.cs` (puro/testato): `Evaluate(ComparisonReport?)` →
  `SentinelResult(Status{NoBaseline,Stable,Improved,Regressed,Inconclusive}, Headline, DeltaPercent,
  Color)`. Riusa il verdetto statistico di ComparisonEngine (frametime lower-is-better → delta+
  = regressione). Onesto: gate troppo rumoroso → Inconclusive, niente baseline → guida.
- CLI `wpep sentinel --baseline <dir> --now <dir>`: carica due set di run (BenchmarkRunStore),
  ComparisonEngine.Compare → Evaluate → esito (exit 1 se regressione). Help aggiornato.
- `RegressionSentinelTests` (5). **258/258 verdi**, build 0/0.
- BILANCIO LAB: **14 moduli su 18**. I 4 rimasti NON sono fattibili bene in autonomia: Latency Lab
  (grafici, serve occhio), Reaction Lab (minigioco interattivo), AI co-pilot (serve LLM/API),
  Evidence community (serve server — non fare fake). Sono da fare con Léon al PC o come lavori dedicati.

### 45. V3 — Onestà del Lab: flag "Available" sui moduli non ancora implementati (2026-06-18)
Il Lab offriva toggle anche per i 4 moduli non ancora costruiti (Latency Lab, Reaction Lab, AI
co-pilot, Evidence community) → attivarli non faceva nulla. Fix di onestà (on-brand):
- `FeatureModule.Available` (default true); i 4 non-implementati marcati `Available: false`.
- `AppSettings.IsFeatureEnabled` ora ritorna SEMPRE false per un modulo non-Available, anche se un
  settings.json vecchio lo avesse on → un flag stale non può accendere un modulo inesistente.
- `FeatureRow`: `IsAvailable`/`IsComingSoon`; il setter di Enabled rifiuta i non-disponibili.
- GUI Lab: badge "IN ARRIVO" + CheckBox disabilitato per i coming-soon.
- `FeatureCatalogTests` +2 (coming-soon mai default-on; implementati ≥12). **260/260 verdi**, build 0/0.
- STATO LAB: **14 IMPLEMENTATI + 4 in arrivo** = catalogo onesto. I 4 restanti per design richiedono
  GUI grafici (Latency/Reaction Lab) o infra esterna (LLM per AI co-pilot, server per Evidence).

### 46. V3 — GUI Watchdog + GUI Profili §2 (scheletri funzionali) (2026-06-18)
Léon: "prima finiamo tutto, poi irrobustiamo; l'estetica a fine progetto con Claude Design".
Costruiti gli scheletri funzionali (no fronzoli, wiring corretto).
- **GUI Watchdog** (`WatchdogViewModel`): sezione sulla pagina Changes (gated dal flag). "Controlla
  ora" → scan EXPO/startup + baseline Time Machine + `Execution.DetectDrift()` → `WatchdogCheck.
  Evaluate` → alert colorati. Esposto via `ChangesViewModel.Watchdog`. Read-only.
- **GUI Profili §2** (`ProfilesViewModel` + pagina nav "Profili"): la batch-selection a checkbox che
  mancava. Colonna sx = profili salvati (Applica/Carica/Elimina; built-in non eliminabili); colonna
  dx = lista CanApply a spunte + "Applica selezionati (N)" (apre il dry-run batch esistente
  ApplyAll.Open → ognuno journaled+annullabile singolarmente) + campo nome + "Salva come profilo"
  (ProfileStore.Save). Carica un profilo = spunta le sue voci per revisione prima di applicare.
- **260/260 verdi**, App build 0/0. (Logica già coperta: ProfileStore, WatchdogCheck. GUI = wiring.)
- NOTA: durante un build ho dovuto chiudere un WPEP.exe aperto (lock file) — la GUI era aperta sul
  remote di Léon.
- STATO: 14 moduli Lab + GUI Watchdog + GUI Profili §2. Resta: i 4 moduli "in arrivo" (Latency/
  Reaction Lab, AI co-pilot, Evidence) e la rifinitura estetica finale (Claude Design).

### 47. V3 — Modulo Lab 15: LATENCY LAB (2026-06-18) — grafico before/after (era "in arrivo")
Promosso da "in arrivo" a implementato. Visualizza l'ultimo confronto del wizard Measure.
- `MeasureWizardViewModel`: `LatencyRow(Metric,BaselineMs,PostMs,DeltaPercent,DeltaLabel,DeltaColor,
  BaselineBar,PostBar)` con larghezze barra PRE-CALCOLATE nel VM (scala a max fisso 260px → niente
  libreria grafica né converter). `BuildLatencyRows(report)` chiamato in BuildVerdict; frametime
  lower-is-better → delta negativo = verde (Improvement), positivo = rosso (Regression). Gated
  `ShowLatencyLab` + `HasLatencyData`.
- GUI: sezione "Latency Lab" sulla pagina Measure (MultiDataTrigger), una riga per metrica con barra
  baseline (grigia) + barra post (colorata per esito) + valori + delta%. Si popola dopo un A/B Measure.
- Catalogo: LatencyLab ora `Available` (Beta). Resta "in arrivo": Reaction Lab (minigioco), AI
  co-pilot (LLM), Evidence community (server).
- Build 0/0. (Grafico = wiring deterministico; estetica premium a fine progetto con Claude Design.)

### 48. V3 — Modulo Lab 16: REACTION LAB (2026-06-18) — minigioco reflex (era "in arrivo")
- `WPEP.Statistics/ReactionStats.cs` (puro/testato): `Analyze(samplesMs)` → best/media/MEDIANA +
  grade (Élite<180 / Ottimo<220 / Buono<260 / Nella media<320 / Lento). Usa la mediana → un fumble
  o un click fortunato non definisce il voto. Framing onesto: misura umano+sistema insieme, non un
  numero di sistema puro. `ReactionStatsTests` (5 + theory).
- `ReactionLabViewModel` (App): macchina a stati Idle→Wait(rosso)→Go(verde)→record, DispatcherTimer
  con delay random 1.2–2.8s (non anticipabile), Stopwatch per la reazione, falsa-partenza = click
  durante il rosso (non conta, ripete il round), 5 round → ReactionStats. Esposto su MeasureWizard.
- GUI: sezione "Reaction Lab" sulla pagina Measure: bersaglio grande cliccabile (colore per stato)
  + Avvia + chip dei tempi + riga risultato. Catalogo: ReactionLab `Available` (Beta).
- App build 0/0. **16 moduli implementati su 18.** Restano "in arrivo" SOLO i 2 che richiedono infra
  esterna: **AI co-pilot** (LLM/API key di Léon) + **Evidence community** (server backend). Per design.

### 49. V3 — Robustness pass #1 (2026-06-18) — "prima finire, poi irrobustire" (Léon)
Finiti i moduli (16/18), inizio l'irrobustimento. Giro mirato sui punti a rischio eccezione:
- `ExecutionEngine.DetectDrift()`: `ReadCurrent` per powercfg/bcdedit ESEGUE processi esterni e può
  lanciare (alcuni richiedono admin). Una sola voce illeggibile faceva crashare l'intero check del
  Watchdog. Fix: try/catch per-voce → salta l'illeggibile invece di affondare tutto (best-effort).
  +3 test (`DetectDrift_NoDriftRightAfterApply`, `_FlagsExternallyRevertedValue`, `_IgnoresUndoneEntries`).
- `ScanViewModel.ScanAsync`: aveva try/finally ma NESSUN catch, ed è chiamato fire-and-forget → un
  fallimento WMI/P-Invoke di una sezione Lab (DisplayScanner, FreshInstall...) abortiva tutto lo scan
  con eccezione non osservata (rischio crash). Fix: catch che aggiunge un finding "Scansione parziale"
  e tiene ciò che è stato caricato.
- CLI: rete di sicurezza GLOBALE — lo `switch(args[0])` ora è in try/catch: qualsiasi comando che
  lancia (WMI, file mancante...) stampa un errore pulito + exit 1 invece di uno stack trace. Tutto
  read-only → fallire è sempre sicuro. Smoke OK (`wpep dna` ancora funzionante).
- **272/272 verdi** (+3 DetectDrift), App+CLI build 0/0.

### 50. BASI — Léon: "manca l'app in sé, tutti i tweak applicabili con un click" (2026-06-18)
Censimento onesto: solo 12/86 erano one-click. I 56 gui-only NON sono quasi mai automatizzabili
via registro: ~15 in-game (anti-cheat), ~15 pannello NVIDIA/AMD (serve NVAPI), BIOS (EXPO/fTPM/ReBAR),
hardware/consigli, 7 placebo. → la via per crescere le basi: (A) NUOVI tweak registro sicuri,
(B) NVIDIA Control Panel via NVAPI, (C) UX core di apply. Léon ha scelto TUTTI E TRE (cps).
**FASE A (questa entry)** — +3 tweak registro HKCU (no admin, reversibili, fonte primaria MS
SystemParametersInfoA): `foreground-lock-timeout-off` (il gioco prende il fuoco subito — gaming),
`menu-show-delay-instant`, `disable-window-animations`. KB 86→89, one-click 12→15. KB valida (12/12
test KB). Le scritture restano da field-validare da Léon come da prassi.
**FASE C (UX core apply)** — `VerdictItem.KindLabel`/`KindColor`: badge esplicito su ogni tweak
nella pagina Verdict → "1-CLICK" (verde, applicabile da Verdict) / "IMPOSTAZIONI" (apre la pagina
Windows) / "MANUALE". Ora l'utente vede a colpo d'occhio cosa l'app sa fare da sola — risolve la
percezione "non applica niente". I 3 nuovi tweak (Optional) appaiono nel gruppo "Maybe" con badge
1-CLICK + bottone Apply (applicabili uno a uno, come Léon voleva). Build App 0/0.
PROSSIMO: FASE B (NVAPI DRS, read-only prima per testare sulla sua RTX 5080).

### 51. BASI/FASE B — Fondazione NVAPI PROVATA sulla RTX 5080 di Léon (2026-06-18)
La via per i tweak del pannello NVIDIA (Reflex, Low Latency, Power Management) = NVAPI user-mode
(anti-cheat safe, è il canale dei tool NVIDIA). Era l'incognita: l'interop funziona?
- `WPEP.SystemAnalyzer/NvApi.cs`: wrapper sul meccanismo NVAPI (un solo export `nvapi_QueryInterface(id)`
  → function pointer → delegate). `Probe()` read-only: Initialize → GetInterfaceVersionString → Unload.
  Gestisce DllNotFound (no GPU NVIDIA). CLI `wpep nvidia`.
- ✅ **TESTATO SUL CAMPO**: `wpep nvidia` sulla RTX 5080 → "NVAPI operativa — interfaccia NVidia
  Complete Version 1.10". L'interop P/Invoke FUNZIONA sul suo hardware reale. Incognita critica risolta.
- PROSSIMO PASSO DRS (non fatto, per disciplina): leggere/scrivere le impostazioni del pannello serve
  marshalling delle struct DRS (NVDRS_SETTING ~12KB, version-encoded, union binarie) — interop delicato
  da ITERARE con cura. Piano: NvAPI_DRS_CreateSession→LoadSettings→GetBaseProfile→GetSetting (READ
  prima, incrociando i valori col pannello NVIDIA reale di Léon per validare la mappatura), poi
  SetSetting→SaveSettings (WRITE, field-validate da Léon). Ids noti: CreateSession 0x0694D52E,
  LoadSettings 0x375DBD6B, GetBaseProfile 0xDA8466A0, GetSetting 0x73BF8338, SetSetting 0x577DD202,
  SaveSettings 0xFCBC7E14, DestroySession 0xDAD9CFF8. Setting PREFERRED_PSTATE 0x1057EB71.

### 52. BASI/FASE B — DRS read + struct marshalling VALIDATO sulla RTX 5080 (2026-06-18)
Spinto oltre la fondazione (lettura = sicura): `NvApi.ReadDwordSetting(settingId)` fa il flusso DRS
completo READ-ONLY — Initialize → DRS_CreateSession → LoadSettings → GetBaseProfile → GetSetting →
DestroySession/Unload. Struct `NVDRS_SETTING` (version = Marshal.SizeOf | 1<<16, settingName wchar
[2048], 2 union da NVDRS_BINARY_SETTING=4104B). Ritorna `NvDrsRead(Ok, MarshallingOk, Value, Message)`.
- ✅ **TESTATO**: `wpep nvidia` sulla RTX 5080 → DRS_GetSetting status **-9 (NOT_FOUND)**, NON -130
  (INCOMPATIBLE_STRUCT_VERSION). Quindi: sessione DRS + LoadSettings + GetBaseProfile OK, e il
  **marshalling delle struct è CORRETTO** (NVAPI ha accettato il layout/version). Il -9 = il setting
  non è impostato nel profilo globale (Léon è sul default; NVAPI non salva i default).
- BILANCIO NVIDIA: interop ✓, sessione DRS ✓, struct marshalling ✓ — le 3 incognite dure risolte.
  PROSSIMO: (1) leggere un setting EFFETTIVAMENTE impostato o GetSettingDefault per vedere un valore;
  (2) mappare gli id/valori (PREFERRED_PSTATE: prefer-max=1?, Low Latency Mode id, Reflex); (3) WRITE
  via DRS_SetSetting(0x577DD202)+DRS_SaveSettings(0xFCBC7E14) + nuovo metodo apply "nvidia-drs"
  nell'ExecutionEngine + verify-after-write + undo-journal, field-validate da Léon. Anti-cheat safe.

### 53. ROBUSTEZZA — Timeout su TryCreateRestorePoint (2026-06-20, commit 92bfffa)
Il restore-point (Checkpoint-Computer via PowerShell) poteva impallarsi per minuti (VSS occupato/
provider hung) e `ReadToEnd()` bloccava PRIMA di `WaitForExit`, così il timeout non scattava mai →
OGNI apply rischiava lo stallo (è ciò che aveva impallato il field-test NVIDIA). Fix: drain async dei
pipe (BeginOutput/ErrorReadLine), cap 12s (`RestorePointTimeoutMs`), kill dell'albero processi su
timeout, ExitCode solo dopo uscita confermata, escape `'` nella description. Apply non blocca più >12s.

### 54. BASI/FASE B — +2 tweak NVIDIA one-click, costanti header-verified (2026-06-20, commit 06ebcbc)
`nvidia-low-latency-on` (PRERENDERLIMIT 0x007BA09E=1) e `nvidia-vsync-off` (VSYNCMODE 0x00A879CF=
VSYNCMODE_FORCEOFF 0x08416747). Costanti prese VERBATIM dall'header ufficiale NVIDIA/nvapi (2 fetch
indipendenti, NON a memoria — così ho corretto un mio ricordo errato di OGL_THREAD_CONTROL_ID/
PREFER_MIN). Engine: la KB può scrivere il valore DRS in hex o decimale; `ParseDword` in BuildPlan
normalizza a decimale (verify/drift confrontano decimale==decimale). Dry-run su RTX 5080: entrambi
`<not set>` → target (0x08416747 → 138504007 dec). Descrizione vsync-off AVVISA: non usare con G-SYNC.

### 55. BASI/BLOCCO 1 — gui-only → one-click: nuovo metodo `dxuser` + fix locale powercfg (2026-06-20)
Léon (cps): "Promuovi gui-only → one-click". Censimento onesto del KB (91 voci): solo 18 erano
one-click, 55 gui-only (la maggioranza GIUSTAMENTE non automatizzabile: in-game/BIOS/cavi), 18 solo
informative. Promossi i 3 candidati ad alta confidenza:
- **Nuovo metodo `dxuser`** (read-modify-write del REG_SZ globale `DirectXUserGlobalSettings` sotto
  HKCU\…\DirectX\UserGpuPreferences): le tre toggle Win11 (windowed-opts, VRR, AutoHDR) vivono in UN
  solo valore come coppie `chiave=valore;`. Una scrittura SZ ingenua le cancellerebbe a vicenda →
  helper puro `DxUserSettings` (Parse/Build/Get/Set/Remove) che fonde UNA chiave preservando le altre,
  con undo granulare (ripristina/rimuove solo quella coppia). `win11-windowed-optimizations`
  (SwapEffectUpgradeEnable=1) e `win11-variable-refresh-rate` (VRROptimizeEnable=1) ora one-click.
  Letto il registro LIVE di Léon per confermare il formato: `SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;
  AutoHDREnable=0;`. Dry-run reali: windowed-opts già=1, VRR 0→1. ✓
- **`usb-selective-suspend-off` → powercfg-value** (GUID USB 2a737441…/48e6b7a6…). Ha smascherato un
  **bug latente importante**: `RealPowerCfg.QuerySettingIndex` cercava l'etichetta INGLESE "Current AC
  Power Setting Index" → su Windows ITALIANO (Léon) non matchava mai → powercfg-value ROTTO su ogni
  Windows localizzato. Fix locale-indipendente: estratto `ParseAcIndex(output)` testabile che prende il
  PRIMO valore `0x…` (gli indici possibili sono 000/001 senza 0x; il 1° 0x = AC). Test theory IT+EN.
- Tolto `settings_uri` dalle 3 voci promosse (rispettata l'invariante esistente "le applicabili non
  hanno deep-link"; manual_steps resta come documentazione). One-click: 18 → 21.
- Test: +2 dxuser (preservazione sorelle + undo), +2 powercfg locale. Build 0/0.

### 56. BLOCCO 2 — UX core apply: comando `applicable` + riepiloghi onesti (2026-06-20, commit 98b3e41)
Léon: "manca la app in sé, tutti i tweak applicabili con un click". Risposta diretta: **`wpep
applicable`** = panoramica a colpo d'occhio di TUTTO il one-click con stato LIVE (✓ già a posto / ○
applicabile / richiede admin), raggruppato per categoria, sola lettura (BuildPlan legge, non scrive).
Letture che richiedono admin (bcdedit) etichettate "richiede amministratore", non "non leggibile".
+ riga "Riepilogo" onesta nel dry-run di apply-all/apply-profile (N da applicare · N già a posto ·
N in conflitto · N admin · N falliti). Sul PC di Léon: 21 → 10 già, 9 ora, 2 admin.

### 57. ROBUSTEZZA — gate hardware su apply/applicable (2026-06-20, commit d2925f8)
`AdvisorEngine.MeetsHardwarePrerequisites` (pubblico, riusa IsApplicable testato, fail-OPEN). `apply`
dà un messaggio pulito "non adatto a questo hardware" invece dell'eccezione NVAPI grezza su GPU non-NVIDIA;
`applicable` filtra i non-compatibili con conteggio nel footer. Su RTX 5080: 21/21 compatibili.

### 58. FIELD-TEST WRITE+VERIFY+UNDO REALE SUPERATO (2026-06-20)
`apply win11-variable-refresh-rate --yes` sul PC di Léon: VRROptimizeEnable 0→1 scritto+verificato,
**sorelle (SwapEffectUpgradeEnable=1, AutoHDREnable=0) INTATTE** (anti-clobbering provato sul registro
reale via reg query prima/dopo), poi `undo last` → stato identico all'iniziale. **NESSUNO STALLO sul
restore-point** (fix #53 confermato dal vivo). Il write path completo (dxuser+journal+undo+restore-point)
non è più "solo coi fake": provato su hardware reale. È l'ultimo tassello che mancava al motore.

### 59. ROBUSTEZZA — audit parse locale + 2° tweak powercfg-value (2026-06-20)
Audit di TUTTI i parse di output comandi per fragilità rispetto alla lingua di Windows (dopo il bug
locale di #55). Esito: **pulito** — gli altri parser usano GUID (powercfg getactivescheme, ReadPowerPlan),
nomi di config-key non localizzati (bcdedit), o tool sempre-inglesi (nvidia-smi). L'unico bug era
QuerySettingIndex, già corretto. Aggiunto **`pcie-aspm-off`** (powercfg-value, GUID SUB_PCIEXPRESS
501a4d13…/ASPM ee12f906…, value 0=Off) — GUID verificati live sul PC di Léon, framing onesto
(controversial, "tipicamente neutro, preempt del mito"). Esercita il path powercfg-value (fix #55) con
una 2ª voce. One-click: 21 → **22**. Dry-run: già 0 sul suo piano BXTool.

### 60. PROFILI curati con i nuovi tweak + fix conflitto KB errato (2026-06-20)
I profili predefiniti erano scritti PRIMA dei 5 nuovi tweak one-click. Curati:
- **Competitive** (+nvidia-low-latency-on, nvidia-prefer-max-performance, win11-windowed-optimizations,
  windows-game-mode). V-Sync Off deliberatamente FUORI (con G-SYNC è l'opposto giusto → scelta manuale).
- **Single-player** (+win11-variable-refresh-rate, win11-windowed-optimizations, windows-game-mode).
- **Daily** (+menu-show-delay-instant, foreground-lock-timeout-off — QoL puro).
Nuovo test invariante: ogni id dei profili predefiniti DEVE esistere nella KB ed essere applicabile
(CanApply) — niente refusi/voci gui-only silenziose.
**Bug KB trovato e corretto**: `network-throttling-index` dichiarava `conflicts_with
systemresponsiveness-gpupriority-registry`, ma scrivono VALORI DIVERSI (NetworkThrottlingIndex vs
SystemResponsiveness, stessa chiave Multimedia\SystemProfile) → NON sono mutuamente esclusivi, anzi è
la combo classica. Il conflitto errato faceva scartare silenziosamente NetworkThrottlingIndex in ogni
profilo che aveva entrambi (Competitive, Pulizia max). Rimosso. Ora "0 in conflitto", NetworkThrottling
finisce correttamente nel gruppo "richiede admin" (HKLM).

### 61. BLOCCO 3 NVIDIA RTX50 — RICERCA FATTA, CHIUSO con verdetto (2026-06-20)
Ricerca read-only (mentre Léon era via) per sbloccare/chiudere l'unico blocco aperto. Verdetto:
- **Low Latency Mode "On"** = "Maximum pre-rendered frames" 0x007BA09E=1 → GIÀ implementato.
- **Low Latency "Ultra", Reflex, Frame Generation, Smooth Motion, DLSS override** → NVIDIA dichiara
  ESPLICITAMENTE (forum dev, thread 241104) che NON sono supportati via NVAPI SDK; nessun riferimento
  Profile Inspector (DeadManWalkingTO né xHybred Revamped CustomSettingNames.xml) li espone come DRS.
  Sono gestiti dall'app NVIDIA / integrazione in-game → NON automatizzabili in sicurezza.
- Esiste un FPS Limiter via DRS ma richiede un valore scelto (monitor-dipendente) → non one-click.
- **CONCLUSIONE**: superficie DRS one-click esaurita coi 3 tweak NVIDIA già presenti. Blocco 3 chiuso,
  nessuna implementazione (giusto rifiutare di scrivere costanti inesistenti/non documentate).
Preparato `docs/NEXT_SESSION.md`: brief ordinato per importanza per il ritorno di Léon (field-test
write path reali #1, review visiva GUI #2, polish estetico #3 solo-alla-fine, altri tweak #4 opzionale).

### 62. BUG GROSSO trovato dal field-test: NVDRS_SETTING struct rifiutata (-9), nvidia-drs MAI funzionato (2026-06-21)
Field-test reale del WRITE nvidia-drs (richiesto da Léon "field-test completi"): `apply
nvidia-low-latency-on --yes` → **"SetSetting -9"**. Indagine:
- **-9 = NVAPI_INCOMPATIBLE_STRUCT_VERSION** (confermato: archive.docs.nvidia.com/.../group__nvapistatus).
  Il codice credeva ERRONEAMENTE che -130 fosse questo errore e che -9 fosse "NOT_FOUND".
- Conseguenza: NVAPI rifiutava la struct NVDRS_SETTING da SEMPRE (-9), sia in read che write. Il READ
  mascherava il rifiuto come "non impostato" (mappa ogni status≠0 a not-set) → i dry-run mostravano
  falsamente "before: <not set>". La validazione #52 ("marshalling OK, -9=NOT_FOUND") era una DIAGNOSI
  ERRATA. nvidia-drs non ha MAI funzionato davvero finora.
- **ROOT CAUSE**: `UnionSize = 4 + 4 + 4096 = 4104`. NVDRS_BINARY_SETTING ha UN solo NvU32
  (valueLength) + NvU8[4096] = **4100**, non due. +8 byte sulla struct (2 union) → sizeof 12328 invece
  di 12320 → `version = sizeof|(1<<16)` sbagliata → -9. Fix: `UnionSize = 4 + 4096 = 4100`.
- **VALIDATO DAL VIVO (RTX 5080)**: dopo il fix `wpep nvidia` legge `0x1057EB71 = 1` (status OK, type 0),
  VSYNCMODE `0x00A879CF = 138504007` (=0x08416747 ForceOff), PRERENDERLIMIT `0x007BA09E = 1` — TRE
  valori reali diversi (non più -9). I 3 tweak NVIDIA risultano già a target (li ha messi il profilo
  gaming di Léon — la lettura ora è VERITIERA). **WRITE provato**: round-trip controllato e reversibile
  PSTATE 1→0 (`apply --yes`, verify legge 0) → `undo` → 1. SetSetting+SaveSettings+verify OK.
- Corretti: costante NVAPI_INCOMPATIBLE_STRUCT_VERSION=-9, commento DeleteSetting (-9 NON è "già
  assente"), testo CLI `wpep nvidia` (niente più "-130"). **Test di regressione**: `NvApi.
  NvdrsSettingMarshalSize == 12320` (blocca per sempre il ribaltamento della dimensione struct).
- LEZIONE: il field-test del WRITE ha scoperto ciò che il read (che mascherava l'errore) nascondeva.
  Validare i write path dal vivo è essenziale; un -9 scambiato per "not found" aveva ingannato 10 sessioni.

## Stato a fine sessione Opus (AGGIORNATO 2026-06-16)
- `dotnet test`: **145/145 verdi**. `dotnet build WPEP.sln -c Release`: 0 errori/0 warning.
  (Se un nodo MSBuild crasha in parallelo: `-m:1 --disable-build-servers`.)
- `wpep advise --json` per automazione. Test integrazione apply-all (pipeline completa).
- Logica apply consolidata in `WPEP.Execution.ApplyPolicy` (unica fonte App+CLI). `wpep doctor`.
- Undo drift-aware: non sovrascrive valori cambiati a mano dopo l'apply (UndoOutcome.Skipped).
- Apply onesto: rileva i no-op ("gia al valore desiderato"); gate admin solo se serve scrivere.
- `wpep selftest` / GUI "Verifica motore" validano sul campo il path di scrittura registry
  reale (EngineSelfTest condiviso; PASS sul PC di Leon, output pulito).
- Report HTML: badge "one-click" sulle voci applicabili + sezione Changes da journal (GUI+CLI).
- KB: **86 voci** (26 forti, 21 plausibili, 18 controverse, 14 placebo, 7 risky);
  **12 applicabili one-click** via registry/powercfg/bcdedit (con dry-run/journal/undo),
  il resto gui-only (deep-link "Open settings" dove possibile, o in-game/BIOS).
- 🎯 **MOTORE VALIDATO SUL CAMPO (#29)**: Leon ha eseguito apply reali → registry WRITE+undo,
  powercfg WRITE+undo, bcdedit READ tutti OK sul suo PC. NON e piu "solo testato coi fake".
- Executors engine: registry + powercfg + powercfg-value + **bcdedit** (service: ancora TODO).
- **CLI ora applica**: wpep apply/apply-all/changes/undo (dry-run di default, --yes per scrivere).
- **apply-all** con conflict guard (ConflictResolver) e gating admin onesto.
- Detection giochi: Fortnite/Valorant/CS2/**Apex**/**Overwatch2**.
- Nessuna modifica distruttiva ai moduli core di Fable; tutto additivo.
- DA RIVEDERE/TESTARE da Fable con PRIORITÀ (aggiornato dopo validazione sul campo #29):
  - ✅ registry WRITE+undo e powercfg WRITE+undo: VALIDATI sul campo da Leon (#29) — non piu
    priorita di test, ma la code-review resta utile.
  - ⚠️ UNICO write path ancora NON provato dal vivo: **RealBcdEdit WRITE** (Set/Delete) —
    il suo disabledynamictick e gia 'yes' quindi non scrivibile pulito; READ validato (#29),
    pattern identico a registry/powercfg gia provati, coperto dai fake.
  - Undo drift-aware (#27): rivedere la semantica skip-su-drift su powercfg/bcdedit.
  - ApplyPolicy (#25): unica fonte CanApply/NeedsAdmin/DecideAction — verificare le regole.
  - apply-spec in tweaks.json (path/valori), inclusi HwSchMode + StickyKeys Flags=506 (#15),
    disabledynamictick (#12); URL fonte voci KB nuove (#2,#8,#14,#18,#22); cs2-reflex (#... 2026-06-18).
  - il pin TraceEvent 3.2.4 in WPEP.Diagnostics (#16).
- Launcher: C:\Scripts\verdict.vbs (no console), fuori dal repo. Tema GUI: +preset "Villain",
  sfondo piu scuro; report HTML allineato.
- Boundary Fable→Opus invariato: ultimo commit Fable `53e9fdc`. ~37 commit Opus dopo.
