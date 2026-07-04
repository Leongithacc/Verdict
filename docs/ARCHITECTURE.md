# Architecture

Reference architetturale di Verdict / WPEP. Aggiornato 2026-07-01 (v1.1).

Per il rundown storico decisionale vedi [HANDOVER.md](HANDOVER.md).
Per la roadmap versionata vedi [ROADMAP.md](ROADMAP.md).

## Overview a un livello

Verdict è un'applicazione client Windows (WPF MVVM) + un backend serverless
opzionale (Cloudflare Worker + D1). Il client è **stateful sul disco locale**
(journal + settings) e **read-only sul sistema** salvo espliciti consensi. Il
backend è **stateless per richiesta** e memorizza solo record anonimi aggregabili.

```
┌────────────────────────────────────────────────────────────┐
│  User (Windows 10/11)                                      │
│                                                            │
│  ┌──────────────┐   ┌───────────┐   ┌──────────────────┐  │
│  │  WPEP.App    │   │ WPEP.Cli  │   │  WPEP.Tray       │  │
│  │  (WPF GUI)   │   │ (wpep.exe)│   │  (Watchdog)      │  │
│  └──────┬───────┘   └─────┬─────┘   └────────┬─────────┘  │
│         │                 │                  │            │
│         └─────────┬───────┴──────────────────┘            │
│                   ▼                                       │
│         ┌───────────────────────────┐                     │
│         │  WPEP.Advisor (core)      │                     │
│         │  - AdvisorEngine          │                     │
│         │  - ConflictResolver       │                     │
│         │  - OptimizeForGame        │                     │
│         │  - MacroCategory          │                     │
│         │  - CoPilot/ (4 brains)    │                     │
│         └────────┬──────────────────┘                     │
│                  │                                        │
│      ┌───────────┼───────────────┐                        │
│      ▼           ▼               ▼                        │
│  ┌─────────┐ ┌──────────┐  ┌────────────────┐            │
│  │Analyzer │ │Execution │  │KnowledgeBase   │            │
│  │(WMI,ETW,│ │(V2 engine│  │(137 entries,   │            │
│  │Registry,│ │ registry │  │ tweaks.json)   │            │
│  │Steam,   │ │ powercfg │  └────────────────┘            │
│  │Epic,    │ │ bcdedit  │                                 │
│  │Riot)    │ │ nvidia)  │                                 │
│  └─────────┘ └──────────┘                                 │
│                                                            │
│         ┌──────────┐  ┌──────────┐   ┌───────────┐        │
│         │Benchmark │  │Statistics│   │Diagnostics│        │
│         │(PresentM │  │(Mann-Whit│   │(ETW DPC/  │        │
│         │ ETW)     │  │ bootstrap│   │  ISR)     │        │
│         └──────────┘  └──────────┘   └───────────┘        │
└────────────────────────────────────────────────────────────┘
                    │
                    │ HTTPS opt-in (Settings checkbox default OFF)
                    │
                    ▼
┌────────────────────────────────────────────────────────────┐
│  Cloud (Cloudflare)                                        │
│                                                            │
│    ┌──────────────────────────┐                            │
│    │  verdict-community       │                            │
│    │  Worker (TypeScript)     │                            │
│    │  ─ POST /v1/evidence     │                            │
│    │  ─ GET  /v1/stats        │                            │
│    │  ─ GET  /v1/top-tweaks   │                            │
│    └────────┬─────────────────┘                            │
│             │                                              │
│             ▼                                              │
│    ┌────────────────┐          ┌──────────────────┐        │
│    │  D1 (SQLite)   │  cron    │  Cron scheduled  │        │
│    │  ─ evidence    │◀──03:00  │  rebuild stats   │        │
│    │  ─ stats_cache │  UTC     │  + 365d retention│        │
│    └────────────────┘          └──────────────────┘        │
└────────────────────────────────────────────────────────────┘
```

## Progetti .NET (solution `WPEP.sln`)

Il codice C# è in `src/`. Ogni progetto ha responsabilità netta e testabile in
isolamento.

### Layer domain (nessuna dipendenza da UI)

- **`WPEP.Core`** — tipi base condivisi tra tutti gli altri progetti.
  - `System/SystemSnapshot.cs` — read-only photograph del sistema (record C#).
  - `Bios/BiosGuide.cs` — mapping tweak-id → guida BIOS per vendor.
  - `Update/UpdateCheck.cs` — check GitHub Releases per auto-update.
  - `AppVersion.cs` — sorgente UNICA della versione (bump qui per release).

- **`WPEP.KnowledgeBase`** — il catalogo tweak.
  - `TweakEntry.cs` — schema record (id, name, category, sources, evidence_level, apply, ecc.).
  - `KnowledgeBaseLoader.cs` — deserializza `kb/tweaks.json`.
  - `KnowledgeBaseValidator.cs` — enforce regola d'oro (fonte primaria per non-placebo).
  - `PlaceboMuseum.cs` — proiezione dei tweak con `evidence_level: placebo`.

- **`WPEP.SystemAnalyzer`** — probes read-only del sistema.
  - `SnapshotBuilder.cs` — assembla `SystemSnapshot` con `Probe(...)` graceful.
  - `HardwareScanner.cs` — CPU / GPU / RAM / mobo brand via WMI.
  - `NetworkDuel.cs` — game-aware route ping/jitter.
  - `RigDna.cs` — firma anonima FNV-1a del rig (per opt-in community).

- **`WPEP.Advisor`** — brain deterministico che decide "cosa consigliare".
  - `AdvisorEngine.cs` — regole (no ML) → Recommendation + Classification.
  - `ConflictResolver.cs` — mutual-exclusion tra tweak.
  - `OptimizeForGame.cs` — piano per gioco specifico.
  - `MacroCategory.cs` — mapping 7 categorie tecniche → 4 bucket UX.
  - `CoPilot/OllamaBrain.cs` — brain locale default.
  - `CoPilot/ClaudeBrain.cs` — brain Anthropic opzionale.
  - `CoPilot/GeminiBrain.cs` — brain Google opzionale.
  - `CoPilot/OpenAiBrain.cs` — brain OpenAI opzionale.
  - `CoPilot/CoPilotGrounding.cs` — costruisce catalog + parse reply
    (droppa id inventati **nel codice**, non solo nel prompt).

- **`WPEP.Execution`** — il motore di scrittura V2.
  - `ExecutionEngine.cs` — dry-run → journal → write → verify(rilettura) → undo.
  - `ApplyPolicy.cs` — CanApply + NeedsAdmin.
  - `Community.cs` — `ICommunityBackend` con `LocalOnlyBackend` (default) e `RemoteBackend`.
  - `SessionMode.cs` — Gaming Session Mode (lower PriorityClass, ripristina a stop).
  - Sotto-cartelle per method-specifici: `registry/`, `powercfg/`, `bcdedit/`, `nvidia/`, `dxuser/`.

- **`WPEP.Statistics`** — Mann-Whitney U + bootstrap CI + noise gate + MDE.
- **`WPEP.Benchmark`** — capture + parse PresentMon ETW → frame stats.
- **`WPEP.Diagnostics`** — ETW DPC/ISR aggregator per driver.
- **`WPEP.Reporting`** — HTML report generator.

### Layer presentazione

- **`WPEP.App`** — GUI WPF MVVM.
  - `MainWindow.xaml` — pagine come `DataTemplate` per VM specifici.
  - `ViewModels.cs` — `MainViewModel`, `VerdictViewModel`, `CoPilotViewModel`,
    `SettingsViewModel`, `ChangesViewModel`, ecc.
  - `Themes/Theme.xaml` — token semantici (`Text`, `TextMuted`, `Accent`, ...) +
    stili premium (`Switch`, `Card`, `PrimaryButton`, `MissileButton`, icone Path).
  - `Infrastructure.cs` — `AppSettings` + preset temi + DPAPI helpers.
  - **Nota critica**: unico progetto con `InvariantGlobalization=false` (WPF).

- **`WPEP.Tray`** — WinForms tray watchdog isolato dal processo GUI.
  - `WatchdogMonitor.cs`, `SentinelStatusStore.cs`, `TrayAutostart.cs`.

- **`WPEP.Cli`** — CLI `wpep.exe`, stesso motore della GUI.
  - Nessuna logica duplicata: la CLI chiama i servizi core direttamente.

## Backend `verdict-community` (repo separato)

Cloudflare Worker in `verdict-community/` (repo GitHub distinto):

```
src/
├── index.ts       # Worker single-file, 3 endpoint + 1 scheduled handler
├── index.test.ts  # vitest + @cloudflare/vitest-pool-workers + D1 in-memory
schema.sql         # 2 tabelle: evidence (raw) + stats_cache (aggregato)
wrangler.toml      # D1 binding + cron "0 3 * * *" + rate limit nativo
scripts/
├── smoke.sh       # E2E test (Bash)
└── smoke.ps1      # E2E test (PowerShell)
```

**Contract**:
- `POST /v1/evidence` — batch di `EvidenceRecord`, idempotente per
  `(rig_signature, tweak_id, captured_at)`.
- `GET /v1/stats?tweak_id=X&rig_tier=Y` — statistiche aggregate,
  soglia minima 10 sample.
- `GET /v1/top-tweaks?limit=N` — leaderboard per sample_size (per la vetrina).
- `GET /` — health check.
- Scheduled cron nightly 03:00 UTC — rebuild `stats_cache` + prune 365d.

**Privacy**: nessun IP, User-Agent, geo. Solo `rig_signature` (hash 8-char) +
`rig_tier` + `tweak_id` + `outcome` + `delta_percent` + `captured_at`.

## Struttura test

```
tests/WPEP.Tests/  (43 file al 2026-07-01)
├── AdvisorEngineTests.cs
├── ApplyOrchestrationTests.cs
├── ApplyPolicyTests.cs
├── BiosGuideTests.cs
├── BootstrapTests.cs
├── CommunityTests.cs
├── ComparisonEngineTests.cs
├── CoPilotTests.cs          ─ smoke 4-brain (Ollama/Claude/Gemini/OpenAI)
├── ExecutionEngineTests.cs
├── GhostTweakTests.cs
├── KnowledgeBaseTests.cs    ─ integrity (game/category/URL allowlist)
├── MannWhitneyTests.cs
├── NetworkDuelTests.cs      ─ parity GamePublisher ↔ KB game slugs
├── NoiseFloorAnalyzerTests.cs
├── NoiseGateTests.cs
├── OptimizeForGameTests.cs
├── PresentMonCsvParserTests.cs
├── RigDnaTests.cs
├── SessionModeTests.cs      ─ sanity list + MacroCategory
├── SystemSnapshotTests.cs   ─ NoiseBand thresholds + GameInstalled parity
├── UpdateCheckTests.cs
└── ...
```

**Test infrastructure**:
- xUnit 2.x
- Repo-wide: `TreatWarningsAsErrors=true` in `Directory.Build.props`
- ~360 test at build time, tutti offline (no network, no processes reali)

## Vincoli architetturali (invariants)

1. **`WPEP.Core` non dipende da niente eccetto BCL**. Nessun WPF, nessun WMI.
2. **`WPEP.App` è l'unico con `InvariantGlobalization=false`**. Codice condiviso
   che gira nel processo App usa la culture OS per formattare; per dati esterni
   (CSV PresentMon) usa sempre `CultureInfo.InvariantCulture` esplicito.
3. **Nessun tweak KB senza fonte primaria** (regola d'oro codificata in
   `KnowledgeBaseValidator`).
4. **Nessuna scrittura nel sistema senza consenso per-cambio** (verificato in
   `ApplyPolicy` + confermato in `ExecutionEngine` con dry-run visibile).
5. **Zero overlay in-game / injection / kernel driver** — usiamo solo ETW
   passivo (`WPEP.Benchmark`, `WPEP.Diagnostics`).
6. **Portable**: nessun servizio Windows, no scheduled task senza consenso, no
   scritture fuori da `%LOCALAPPDATA%\Verdict\` (o cartella dell'exe se portable).

## Note per contributor

Vedi [CONTRIBUTING.md](../CONTRIBUTING.md) per il flow PR. La regola d'oro KB
è non-negoziabile: PR con tweak senza fonte primaria vengono rifiutati anche se
il tweak in sé è "corretto".
