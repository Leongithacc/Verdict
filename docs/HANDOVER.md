# Verdict — Handover completo (per riprendere a freddo)

> Scritto 2026-06-26 a fine sessione. Obiettivo: una nuova istanza di Claude (o Léon su un altro PC)
> riprende il progetto SENZA ricostruire contesto. Leggi anche `MEMORY.md` + `project_wpep.md` (memoria
> globale di Claude) e i blocchi `docs/OPUS_AUDIT_LOG.md` (#64–69) per la cronologia delle decisioni.

## 0. Come riprendere in 60 secondi
1. `cd C:\Users\leon0\Projects\WPEP`
2. Build: `dotnet build WPEP.sln -c Release -m:1 --disable-build-servers -v q` → deve dare **0/0**.
3. Test: `dotnet test WPEP.sln -c Release -m:1 --disable-build-servers -v q` → **339/339**.
4. App: `dotnet run --project src/WPEP.App` (o Win+R → `verdict` se installata).
5. CLI: build → `src/WPEP.Cli/bin/Release/net10.0-windows/win-x64/wpep.exe <verbo>` (es. `doctor`, `advise`, `evidence`).

## 1. Cos'è Verdict
App Windows di **tweaking gaming ONESTA e anti-placebo** (engine interno = "WPEP"). Tesi: *l'unico
ottimizzatore che ti dice quando smettere di ottimizzare*. Regole non negoziabili:
- **Solo tweak con FONTE verificata** in Knowledge Base (mai URL/claim inventati = "regola d'oro").
- **Niente promesse di FPS finti**: misura prima/dopo con rigore statistico.
- **Mai overlay/iniezione in-game** (anti-cheat safe): i dati frame arrivano da ETW (come Intel PresentMon).
- **Tutto reversibile**: ogni scrittura è journaled + undo + Ripristina-tutto.
- **Portatile, leave-no-trace**: una cartella, niente installer di sistema. UNSIGNED (deciso).

## 2. Architettura (soluzione .NET 10 / C# 13)
`Directory.Build.props`: ImplicitUsings, Nullable, **TreatWarningsAsErrors=true**, InvariantGlobalization=true.
**Eccezione critica**: `WPEP.App` ha `InvariantGlobalization=false` (WPF ha bisogno di culture vere) →
qualsiasi parse/format numerico nel codice condiviso che gira nel processo App usa la cultura OS. Per i
DATI esterni (es. CSV PresentMon con decimali `.`) usare SEMPRE `CultureInfo.InvariantCulture` (già fatto).

Progetti (in `src/`):
- **WPEP.Core** — tipi base, SystemSnapshot, Update (`Update/UpdateCheck.cs`), BIOS (`Bios/BiosGuide.cs`),
  `AppVersion.cs` (sorgente UNICA della versione = "1.0"). net10.0.
- **WPEP.KnowledgeBase** — `TweakEntry` + loader + `kb/tweaks.json` (**122 voci**, ogni voce con fonte).
- **WPEP.SystemAnalyzer** — scan hardware (WMI), `SnapshotBuilder`, `RigDna` (firma anonima del rig),
  `HardwareScanner.BoardManufacturer()` (marca scheda madre per il QR BIOS).
- **WPEP.Advisor** — `AdvisorEngine.Advise()` (regole deterministiche, NO ML) → Recommendation +
  Classification. `ConflictResolver`, `OptimizeForGame`. **CoPilot/** = V6 AI (vedi §5).
- **WPEP.Execution** — **il motore di scrittura V2** (`ExecutionEngine`): dry-run plan → journal →
  write → verify(rilettura) → undo drift-aware. `ApplyPolicy` (single source CanApply/NeedsAdmin),
  registry/powercfg/bcdedit/nvidia-drs/dxuser access. `Community.cs` = V7 (vedi §5).
- **WPEP.Statistics** / **WPEP.Benchmark** — Mann-Whitney + bootstrap + noise gate; PresentMon capture/parse.
- **WPEP.Diagnostics** / **WPEP.Reporting** — ETW DPC/ISR; report HTML.
- **WPEP.App** — GUI WPF MVVM. `MainWindow.xaml` (pagine come DataTemplate per VM), `ViewModels.cs`,
  `Themes/Theme.xaml` (token DynamicResource + stile `Switch`). Nav = RadioButton con handler in
  `MainWindow.xaml.cs` che settano `MainViewModel.CurrentPage`.
- **WPEP.Tray** — guardiano WinForms separato (Watchdog ogni 10 min). Progetto isolato (WPF/WinForms non si toccano).
- **WPEP.Cli** — `wpep <verbo>`. AssemblyName=`wpep`. Output RID-specifico: `bin/Release/net10.0-windows/win-x64/wpep.exe`.

## 3. Stato delle feature (V1–V7.0 FATTE)
- **V1** Fondazione · **V2** Design integrato (10 temi, token Ink, toast, traduzione IT completa) ·
  **V3** Misura (PresentMon + confronto statistico onesto) · **V4** Per-gioco (impostazioni in-game per
  CS2/Apex/OW2/TheFinals/R6 + altri) · **V5** Automazione (Watchdog tray + Sentinel + Time Machine).
- **V6 AI Co-pilot**: linguaggio naturale → cita SOLO tweak del catalogo (id inventati SCARTATI nel codice,
  non solo nel prompt). Read-only. Cervello = Ollama locale (default qwen2.5, configurabile in GUI).
  `ICoPilotBrain` swappable → ClaudeBrain cloud futuro. CLI `wpep copilot "..."`, pagina GUI "Co-pilota".
- **V6.5 UI a INTERRUTTORI**: la lista Verdict + co-pilota usano toggle ON/OFF (no più "Come fare/Applica").
  Immediato + conferma sui rischiosi; OFF = undo del journal REALE (mai un valore indovinato); admin →
  disabilitato; manuali → toggle disabilitato + info. **QR guida BIOS** per i tweak manuali-BIOS
  (xmp-expo/resizable-bar/ftpm-update/pbo): QR per-tweak → `site/bios.html` su GitHub Pages, guide
  per-marca verificate IT+EN. QR via `QRCoder` (NuGet, pure-managed).
- **V7.0 Community evidence**: privacy-first/local-first. `EvidenceLedger` (registro anonimo locale,
  firma RigDna) + `ICommunityBackend` swappable (default LocalOnly = ZERO rete). Registra: toggle ON →
  "applied"; Ghost reveal → esito misurato. CLI `wpep evidence`/`community`, card GUI "Community e prove".
- **V8 Auto-update**: `UpdateCheck` host-agnostico → GitHub Releases (host = `Leongithacc/Verdict`). CLI
  `wpep version`/`update-check`, card Impostazioni (consent-first). Check live = graceful 404 finché non c'è release.

## 4. Cosa MANCA (tutto bloccato su azioni/decisioni di Léon)
1. **Pubblicare su GitHub** (repo `Leongithacc/Verdict` PUBLIC) → accende auto-update + sync vacanza.
   Runbook: `docs/RELEASE_GITHUB.md`. Lo zip release: `bash tools/package-release.sh` → `dist/Verdict-1.0.zip`.
2. **Accendere GitHub Pages** (Source: GitHub Actions) → il QR BIOS diventa live. Runbook: `docs/BIOS_GUIDE_GITHUB_PAGES.md`.
3. **V7 remoto** ("ha aiutato il 73% dei rig simili") → serve DECISIONE: server + privacy + opt-in.
   L'interfaccia `ICommunityBackend` è pronta: basta un `RemoteBackend` dietro di essa.
4. **2ª passata Claude Design** ("per esagerare") → estetica finale degli interruttori/QR/card. Istruzioni
   già in `docs/CLAUDE_DESIGN_BRIEF.md` (sezione "AGGIORNAMENTO 2026-06-26"). Léon NON l'ha ancora fatta (token).
5. **ClaudeBrain** (co-pilota cloud) opzionale, dietro `ICoPilotBrain` — solo se Léon vuole qualità cloud.

## 5. Bug noti / debito tecnico
- **ApplyDialog singolo = codice morto**: dopo i toggle (V6.5) `ApplyDialogViewModel.Open` ha 0 chiamanti.
  Inerte (non si apre mai), da rimuovere a parte (c'è un chip/task flaggato). NON è `ApplyAllViewModel` (quella è viva).
- **Firma del modello Ollama**: default "qwen2.5"; Léon ha "qwen2.5vl:32b" → impostabile nella pagina Co-pilota.
- **GitHub Pages non ancora acceso** → il QR mostra "Nessuna release/pagina" finché Léon non attiva Pages.
- Nessun bug logico aperto: audit #68 (tutto il prodotto) + #69 (post-V6.5/V7) → codice maturo robusto.

## 6. Decisioni tecniche già prese (non rimetterle in discussione senza motivo)
- **UNSIGNED** (no certificato): self-signed non toglie l'avviso, cert a pagamento solo per distribuzione
  pubblica. Resta lo zip portatile + `Installa.cmd`.
- **Host update = GitHub Releases** `Leongithacc/Verdict` (consts in `UpdateConfig`).
- **Co-pilota = Ollama locale** di default (privacy), cloud opzionale dietro interfaccia.
- **V7 = local-first**, niente rete finché non c'è un backend configurato + opt-in.
- **Toggle immediati** (non staged) + conferma solo sui rischiosi; OFF = undo del journal reale.
- **QR BIOS = per-marca verificato** (no per-modello inventato), su GitHub Pages (raggiungibile col PC in BIOS).

## 7. Gotcha per una nuova istanza di Claude (IMPORTANTE)
- **Build**: chiudi prima i processi che lockano le DLL:
  `taskkill //F //IM MSBuild.exe //IM VBCSCompiler.exe //IM WPEP.exe //IM wpep-tray.exe 2>/dev/null`
  poi `dotnet build/test/publish ... -c Release -m:1 --disable-build-servers -v q`. Leggi SEMPRE l'output completo.
- **Non lanciare ripetutamente la tray** negli smoke-test: `wpep-tray.exe` resta in piedi e locka le DLL.
- **Regola di lavoro**: build per compile-check, `--filter` per test mirati, **suite piena solo ai checkpoint**.
  Ogni milestone chiude con build 0/0 + suite verde + artifact ripubblicato + commit su `main`.
- **Commit diretti su main** (repo locale). Messaggi commit terminano con la riga Co-Authored-By Claude.
- **Convenzioni Léon**: italiano, diretto, NO yes-man. "domande cps" = usa `AskUserQuestion` (cliccabile),
  non domande testuali. NON inventare lavoro marginale: se il valore alto è esaurito, dillo.
- **Regola d'oro KB**: solo fonti verificate. Mai aggiungere una voce KB con URL/claim inventato.
- **App project**: NO `System.IO` negli implicit usings → qualifica `System.IO.Path` o aggiungi using.
- **GNU tar non fa zip veri**: per lo zip usa `/c/Windows/System32/tar.exe -a -c -f x.zip dir` (bsdtar).
- **Python stdout cp1252**: rompe su unicode (→, è); scrivi su file invece di stampare.
- **`pip` si impalla** nel sandbox Bash → usa desktop-commander se serve installare pacchetti Python.

## 8. Comandi utili
- Pacchetto release: `bash tools/package-release.sh` → `dist/Verdict-1.0.zip` (app + Installa.cmd).
- Self-test motore: `wpep selftest` (write→verify→undo su chiave usa-e-getta) — da admin prova anche bcdedit.
- Doctor: `wpep doctor` (riepilogo prontezza). Le tue prove: `wpep evidence`. Update: `wpep update-check`.
- Versione: bump in `src/WPEP.Core/AppVersion.cs` (+ `VER` in `tools/package-release.sh`), nuovo tag release.
