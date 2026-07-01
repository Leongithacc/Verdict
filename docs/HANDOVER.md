# Verdict — Handover completo (per riprendere a freddo)

> Scritto 2026-06-26, aggiornato end-to-end 2026-07-01. Obiettivo: una nuova istanza di Claude
> (o Léon su un altro PC) riprende il progetto SENZA ricostruire contesto. Leggi anche `MEMORY.md`
> + `project_wpep.md` (memoria globale di Claude) e i blocchi `docs/OPUS_AUDIT_LOG.md` per la
> cronologia delle decisioni. Per la release imminente v1.1 leggi `docs/RELEASE_V1.1_RUNBOOK.md`.

## 0. Come riprendere in 60 secondi
1. `cd C:\Users\leon0\Projects\WPEP` (PowerShell; su bash `/c/Users/leon0/Projects/WPEP`).
2. `git log --oneline -10` per vedere gli ultimi commit (attesi **~26 commit locali oltre `v1.0`** al 2026-07-01, tutti "ciechi" = mai buildati sull'ambiente di sviluppo remoto).
3. Build: `dotnet build WPEP.sln -c Release -m:1 --disable-build-servers -v q` → deve dare **0/0**.
4. Test: `dotnet test WPEP.sln -c Release -m:1 --disable-build-servers -v q` → **~350+** (base 339 + 9 smoke ClaudeBrain + smoke Gemini/OpenAI + snapshot Noise/SecureBoot/TPM + eventuali Session).
5. App: `dotnet run --project src/WPEP.App` (o Win+R → `verdict` se installata).
6. CLI: build → `src/WPEP.Cli/bin/Release/net10.0-windows/win-x64/wpep.exe <verbo>` (es. `doctor`, `advise`, `evidence`, `copilot`, `community`, `session`).

## 1. Cos'è Verdict
App Windows di **tweaking gaming ONESTA e anti-placebo** (engine interno = "WPEP"). Tesi: *l'unico
ottimizzatore che ti dice quando smettere di ottimizzare*. Regole non negoziabili:
- **Solo tweak con FONTE verificata** in Knowledge Base (mai URL/claim inventati = "regola d'oro").
- **Niente promesse di FPS finti**: misura prima/dopo con rigore statistico.
- **Mai overlay/iniezione in-game** (anti-cheat safe): i dati frame arrivano da ETW (come Intel PresentMon).
- **Tutto reversibile**: ogni scrittura è journaled + undo + Ripristina-tutto.
- **Portatile, leave-no-trace**: una cartella, niente installer di sistema. UNSIGNED (deciso).
- **Onestà attiva contro il placebo**: il Noise Score (0-100) dice quando un tweak background NON produrrà differenze misurabili. Vedi `docs/VS_HONE.md` §3.1.

## 2. Architettura (soluzione .NET 10 / C# 13)
`Directory.Build.props`: ImplicitUsings, Nullable, **TreatWarningsAsErrors=true**, InvariantGlobalization=true.
**Eccezione critica**: `WPEP.App` ha `InvariantGlobalization=false` (WPF ha bisogno di culture vere) →
qualsiasi parse/format numerico nel codice condiviso che gira nel processo App usa la cultura OS. Per i
DATI esterni (es. CSV PresentMon con decimali `.`) usare SEMPRE `CultureInfo.InvariantCulture` (già fatto).

Progetti (in `src/`):
- **WPEP.Core** — tipi base, `SystemSnapshot` (esteso con `SecureBootEnabled`, `Tpm2Enabled`, `NoiseScore`, `NoiseFactors`, `NoiseBand`), Update (`Update/UpdateCheck.cs`), BIOS (`Bios/BiosGuide.cs` — HashSet Guided esteso con i 5 nuovi tweak Vanguard; VendorSlug riconosce EVGA/NZXT con fallback ASRock), `AppVersion.cs` (sorgente UNICA della versione, va bumpata a "1.1" al rientro).
- **WPEP.KnowledgeBase** — `TweakEntry` + loader + `kb/tweaks.json` (**133 voci** al 2026-07-01, ogni voce con fonte primaria).
- **WPEP.SystemAnalyzer** — scan hardware (WMI), `SnapshotBuilder` con `ReadSecureBoot()` (registry SecureBoot\State), `ReadTpm2()` (WMI Win32_Tpm), `ComputeNoiseScore()` (5 fattori: StartupApps + Indexing + SysMain + GameDvr + Transparency), `ReadWarzoneInstalled()` (Steam app 1962663). `RigDna` (firma anonima del rig), `HardwareScanner.BoardManufacturer()`.
- **WPEP.Advisor** — `AdvisorEngine.Advise()` (regole deterministiche, NO ML) → Recommendation + Classification. `ConflictResolver`, `OptimizeForGame` (auto-espande al variare di `game` in KB). **`MacroCategory.cs`**: mapping runtime 7 categorie tecniche → 4 bucket UX ("FPS & Latenza"/"Network & Ping"/"Stabilità & QoL"/"Sfondo"). **CoPilot/** = V6 AI, ora **4 brain swappable** (`OllamaBrain`, `ClaudeBrain`, `GeminiBrain`, `OpenAiBrain`), tutti dietro `ICoPilotBrain`.
- **WPEP.Execution** — motore di scrittura V2 (`ExecutionEngine`): dry-run plan → journal → write → verify(rilettura) → undo drift-aware. `ApplyPolicy`, registry/powercfg/bcdedit/nvidia-drs/dxuser access. `Community.cs` = V7 con `LocalOnlyBackend` + **`RemoteBackend`** (endpoint LIVE `https://verdict-community.gz6jk62yk8.workers.dev`). **`SessionMode.cs`**: `GamingSession.Start()/Stop()` abbassa `ProcessPriorityClass=BelowNormal` per bloater noti (Discord/OneDrive/Dropbox/Spotify/updater) e ripristina al Ctrl+C. Zero kill, zero service stop, anti-cheat safe per design.
- **WPEP.Statistics** / **WPEP.Benchmark** — Mann-Whitney + bootstrap + noise gate; PresentMon capture/parse.
- **WPEP.Diagnostics** / **WPEP.Reporting** — ETW DPC/ISR; report HTML.
- **WPEP.App** — GUI WPF MVVM. `MainWindow.xaml` (pagine come DataTemplate per VM), `ViewModels.cs` (VerdictViewModel esteso con Noise/Vanguard/Session/BucketedGroups; `SettingsViewModel` con `CommunityShareEnabled`; `CoPilotViewModel` con 4-way brain switch), `Themes/Theme.xaml` (token DynamicResource + `OnAccent` nuovo, stile `Switch` con Storyboard, `PrimaryButton` scale-in press, `MissileButton` per Session CTA, `Card` con LinearGradient + DropShadow, 5 IconBase Path styles). Nav = RadioButton con Path icons (colore stroke bind a Foreground dinamico).
- **WPEP.Tray** — guardiano WinForms separato (Watchdog ogni 10 min). Progetto isolato.
- **WPEP.Cli** — `wpep <verbo>`. AssemblyName=`wpep`. `doctor` mostra Secure Boot / TPM 2.0 / Noise Score. Nuovi verbi: `session [--wait <secs>]`, `community --enable/--disable/--status`, `copilot --brain ollama|claude|gemini|openai --api-key <k>` (+ env `ANTHROPIC_API_KEY`/`GEMINI_API_KEY`/`OPENAI_API_KEY`).

### Repo compagno — `C:\Users\leon0\Projects\verdict-community`
Cloudflare Worker + D1 in TypeScript. Endpoints: `POST /v1/evidence`, `GET /v1/stats`, `GET /v1/top-tweaks`, scheduled cron. Zod validation, rate limiting per RigDna, idempotency per (rig, tweak, run_id). Soglia `MIN_SAMPLE_SIZE=10` sotto la quale non si emettono percentuali. Test: `npm test` (vitest + `@cloudflare/vitest-pool-workers` con D1 in-memory, 15+ test). Smoke live: `scripts/smoke.sh` o `.ps1`. 2 commit locali, **repo GitHub NON ancora creato** — vedi §4.

## 3. Stato delle feature (V1–V8 tutte FATTE, v1.1 in coda)
- **V1** Fondazione · **V2** Design (10 temi + IT completa) · **V3** Misura (PresentMon + stats onesta) · **V4** Per-gioco (CS2/Apex/OW2/TheFinals/R6/Fortnite/Valorant + Warzone Reflex nuovo) · **V5** Automazione (Watchdog tray + Sentinel + Time Machine).
- **V6 AI Co-pilot**: linguaggio naturale → cita SOLO tweak del catalogo (id inventati SCARTATI nel codice). Read-only. **4 brain swappable**: OllamaBrain (default, locale) · ClaudeBrain (`claude-sonnet-4-6`) · GeminiBrain (`gemini-2.5-pro`) · OpenAiBrain (`gpt-5`). API keys cifrate a riposo con DPAPI (`ProtectedData`), helpers `EncryptKey/DecryptKey` in `AppSettings`. Guida utente in `docs/BRAINS.md`.
- **V6.5 UI a INTERRUTTORI + Noise Score + Vanguard**: toggle ON/OFF nella lista Verdict + co-pilota. Immediato + conferma sui rischiosi; OFF = undo del journal REALE. Card "Rumore di sistema" (gauge cockpit Path Geometry + needle rotante). Card "Pronto per Vanguard" (Secure Boot + TPM 2.0). Toggle vista bucket UX (`ShowByBucket`) ↔ tecnica. **QR guida BIOS** per i tweak manuali-BIOS: 9 tweak × 6 marche (ASUS/MSI/Gigabyte/ASRock/EVGA/NZXT) × IT+EN — EVGA/NZXT con disclaimer AMI Aptio onesto (nessun per-modello inventato).
- **V7.1 Community evidence — remote LIVE**: privacy-first, opt-in default OFF via checkbox Settings o `wpep community --enable`. `RemoteBackend` fires-and-forgets a `verdict-community.gz6jk62yk8.workers.dev`. Vetrina pubblica in `site/community.html` (top tweak con sample ≥ 10). Docs: `docs/V7_REMOTE_BACKEND_DESIGN.md` (14 sezioni), `docs/PRIVACY.md` (GDPR formale).
- **V8 Auto-update + Release workflow**: `UpdateCheck` host-agnostico → GitHub Releases (`Leongithacc/Verdict`). CLI `wpep version`/`update-check`, card Impostazioni consent-first. **`.github/workflows/release.yml`**: push tag `v*` → build + test + zip + `gh release create`. Sanity-check obbligatorio: `AppVersion.Current` deve matchare la tag e `VER` in `tools/package-release.sh`.
- **Gaming Session Mode (nuovo, ispirato da Hone/onesto)**: `wpep session` o MissileButton nella card Rumore. Vedi `docs/VS_HONE.md` §3.3.

## 4. Cosa MANCA (v1.1 imminente — al ritorno col SDK del 2026-07-05)
Ordine di operazioni (~30 min lavoro macchina + verifiche manuali):

1. **PowerShell**: `cd C:\Users\leon0\Projects\WPEP` → `git log --oneline -30` per rivedere i 26 commit ciechi. Conferma nome branch = `main`.
2. `git push origin main` (i 26 commit vanno online).
3. Build + test — se qualcosa fallisce, vedi §5 "Rischi cieco noti" per il pattern probabile.
4. Se 0/0 warning + test verdi: bump `src/WPEP.Core/AppVersion.cs` da `"1.0"` a `"1.1"` + `VER="1.1"` in `tools/package-release.sh`.
5. Commit + `git tag v1.1 && git push origin v1.1` → workflow release.yml automatico prende build/test/package/GitHub Release.
6. Optional: pubblicare anche verdict-community. `cd C:\Users\leon0\Projects\verdict-community && gh repo create Leongithacc/verdict-community --public --source=. --push`.
7. (Opzionale) Update `docs/CLAUDE_DESIGN_BRIEF.md` con feedback post-1.1 se emerge una 3ª passata design.

Blocchi FATTI (non toccare a meno di regressioni):
- ~~Pubblicare su GitHub Verdict~~ FATTO (v1.0 live 2026-06-26).
- ~~Accendere GitHub Pages BIOS QR~~ FATTO.
- ~~V7 remoto~~ FATTO (Worker LIVE, ledger client + docs).
- ~~2ª passata Claude Design~~ FATTO (7 commit "design spike part 1-7", 2026-06-30/07-01).
- ~~ClaudeBrain / GeminiBrain / OpenAiBrain~~ FATTO.

## 5. Rischi cieco noti (i 26 commit locali non hanno mai visto un compilatore)
- **WPF Path icons con `RelativeSource` binding**: `Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=RadioButton}}"` — coloriazione dinamica dell'icona sulla NavButton. Se WPF si lamenta del contesto binding, fallback a `TemplateBinding` o setter dallo Style.
- **LinearGradientBrush su Card con `DynamicResource` in GradientStop**: sintassi WPF può richiedere `<GradientStop Color="{DynamicResource ...}"/>` esplicito invece che shorthand.
- **`MissileButton` deriva da `PrimaryButton` via `BasedOn`**: dovrebbe compilare ma untested.
- **Storyboard sul knob dello Switch**: `DoubleAnimation` multipli con `CubicEase` 150ms — WPF standard ma potrebbero collidere se targeting nome sbagliato dopo cambio Template.
- **Gauge Path**: `RotateTransform.Angle="{Binding NoiseAngle}"` diretto. Se WPF pretende `Setter` invece del bind direct in RenderTransform, adjustment richiesto. `NoiseScore = null` → angolo 0° (safe path Geometry rimane renderizzato).
- **`SessionMode.Start()`**: `Process.GetProcessesByName` sui protected process (Discord elevated?) può dare access denied — catchato già.
- **JSON KB con 3 voci nuove al fondo**: verificato con `python json.load()` OK (133 voci).
- **`SettingsViewModel.Main` ref pattern** (come `Changes.Main`): assicurarsi che le VM instantiate dal container ricevano il main VM alla costruzione.

Nessun bug logico noto aperto oltre a questi rischi build.

## 6. Decisioni tecniche già prese (non rimetterle in discussione senza motivo)
- **UNSIGNED** (no certificato).
- **Host update = GitHub Releases** `Leongithacc/Verdict`.
- **Co-pilota = Ollama locale** di default; 3 brain cloud dietro DPAPI, opt-in via API key.
- **V7 backend = Cloudflare Workers + D1** (SQLite serverless). Client opt-in default OFF.
- **Toggle immediati** (non staged) + conferma solo sui rischiosi; OFF = undo del journal reale.
- **QR BIOS = per-marca verificato** (no per-modello inventato). EVGA/NZXT usano guida ASRock con disclaimer AMI (fedele alla regola d'oro).
- **Noise Score honest gate**: quando `NoiseBand="basso"`, la UI dice esplicitamente che i tweak background non produrranno FPS misurabili — l'opposto del "+15-30% garantito" dei competitor.
- **Gaming Session Mode NON kill/service-stop**: solo `PriorityClass=BelowNormal` sui bloater curati, restore al Ctrl+C.

## 7. Gotcha per una nuova istanza di Claude (IMPORTANTE)
- **Build**: chiudi prima i processi che lockano le DLL: `taskkill //F //IM MSBuild.exe //IM VBCSCompiler.exe //IM WPEP.exe //IM wpep-tray.exe 2>/dev/null` poi `dotnet build/test/publish ... -c Release -m:1 --disable-build-servers -v q`. Leggi SEMPRE l'output completo.
- **Non lanciare ripetutamente la tray** negli smoke-test: `wpep-tray.exe` resta in piedi e locka le DLL.
- **Regola di lavoro**: build per compile-check, `--filter` per test mirati, **suite piena solo ai checkpoint**. Ogni milestone chiude con build 0/0 + suite verde + artifact ripubblicato + commit su `main`.
- **Commit diretti su main** (repo locale). Messaggi commit terminano con la riga Co-Authored-By Claude.
- **NON push automatici**: Léon spinge a mano dal suo terminale (git credential manager richiede popup GUI). Il feedback è codificato in memoria — vedi `[[feedback-autonomous-night-mode]]` e sessioni recenti.
- **PowerShell vs CMD**: quando dai comandi shell all'utente, dichiara SEMPRE se PowerShell o CMD. CMD tratta `#` come argomento letterale, mai come commento inline. Vedi `[[feedback-shell-specifica]]`.
- **iOS remote control**: quando l'utente è in mobilità (menziona iPhone/remote/"sono fuori"), risposte 3-5 righe max, cps ≤ 3 opzioni con label ≤ 30 caratteri. Vedi `[[feedback-ios-remote-control]]`.
- **Convenzioni Léon**: italiano, diretto, NO yes-man. "domande cps" = usa `AskUserQuestion` (cliccabile), non domande testuali. NON inventare lavoro marginale: se il valore alto è esaurito, dillo.
- **Regola d'oro KB**: solo fonti verificate. Mai aggiungere una voce KB con URL/claim inventato. Verificare URL con WebFetch prima di committare voci di dubbia stabilità (siti community.amd.com sono stati riorganizzati — molti URL vecchi ridirigono a placeholder).
- **App project**: NO `System.IO` negli implicit usings → qualifica `System.IO.Path` o aggiungi using.
- **GNU tar non fa zip veri**: per lo zip usa `/c/Windows/System32/tar.exe -a -c -f x.zip dir` (bsdtar).
- **Python stdout cp1252**: rompe su unicode (→, è); scrivi su file invece di stampare.
- **`pip` si impalla** nel sandbox Bash → usa desktop-commander se serve installare pacchetti Python.
- **npm/node NON nel PATH**: usa `C:\Program Files\nodejs\npm.cmd` con PATH propagato al child process (vedi `[[feedback-node-npm-path]]`).

## 8. Comandi utili
- Pacchetto release: `bash tools/package-release.sh` → `dist/Verdict-<VER>.zip`.
- Self-test motore: `wpep selftest` (write→verify→undo su chiave usa-e-getta) — da admin prova anche bcdedit.
- Diagnostica: `wpep doctor` (riepilogo prontezza + Secure Boot + TPM 2.0 + Noise Score). Le tue prove: `wpep evidence`.
- Community: `wpep community --status` / `--enable` / `--disable`.
- Sessione gaming: `wpep session --wait 3600` (Ctrl+C ripristina i priority).
- Co-pilota: `wpep copilot "come abilito Reflex in CS2?" [--brain ollama|claude|gemini|openai --api-key <k>]`.
- Update: `wpep update-check` / `wpep version`.
- Release v1.1 automatica: bump `src/WPEP.Core/AppVersion.cs` + `VER` in `tools/package-release.sh`, poi `git tag v1.1 && git push origin v1.1` → workflow.
- Worker community (in `C:\Users\leon0\Projects\verdict-community`): `npm test`, `npm run deploy`, `scripts/smoke.ps1` per E2E live.
