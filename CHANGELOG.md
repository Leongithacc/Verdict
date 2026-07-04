# Changelog

All notable changes to Verdict are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **2 nuove voci Knowledge Base (Placebo Museum)** dal vetting della ricerca
  Perplexity 2026-07, entrambe con fonte primaria Microsoft verificata:
  `tcp-chimney-offload-disable` (feature deprecata da Windows Server 2016 e già
  off di default → niente da applicare) e `tcp-autotuning-disable` (placebo per
  il gaming UDP; disattivarlo danneggia il throughput TCP). KB ora a **137 voci**
  (16 placebo). Trail in [`docs/KB_RESEARCH.md`](docs/KB_RESEARCH.md).
- **[`docs/VS_PLATINUM.md`](docs/VS_PLATINUM.md)** — analisi competitor onesta di
  Platinum+Optimizer, nella serie VS_HONE / VS_RAPTECHPC; riga aggiunta a
  [`docs/COMPARISON.md`](docs/COMPARISON.md).

## [1.1] — 2026-07-02

### Added
- **AI co-pilot — 4 swappable brains**: Ollama (local, default), Anthropic Claude,
  Google Gemini, OpenAI GPT. Cloud API keys encrypted at rest with DPAPI.
  Selectable from Settings page and CLI `wpep copilot --brain ...`.
- **Changes page flags half-applied sessions** (audit F14): a tweak interrupted
  mid-apply (crash / power loss) now shows an "Interrotto" badge with an undo hint,
  mirroring the CLI `wpep changes` marking. Closes the audit's last open UI item.
- **5 new BIOS-guided tweaks** with verified per-vendor steps (ASUS / MSI /
  Gigabyte / ASRock × IT + EN): Secure Boot, TPM 2.0, Above 4G Decoding, CSM
  disable, Virtualization (VT-x / AMD-V). Required by Vanguard (Valorant) and
  Windows 11.
- **3 new KB entries** with primary sources: Intel XTU undervolt (Intel equivalent
  of PBO Curve Optimizer), Windows Update active-hours extension, Focus Assist
  auto in fullscreen games.
- **"Pronto per Vanguard" card** on the Verdict page: runtime detection of
  Secure Boot (registry) + TPM 2.0 (WMI `Win32_Tpm`) with one-click BIOS guide
  shortcuts when something needs enabling. Mirrored in CLI `wpep doctor` output.
- **Community evidence — V7 remote backend** (privacy-first, opt-in default OFF):
  - Cloudflare Worker + D1 backend, deployed live
  - `RemoteBackend.cs` client class behind `ICommunityBackend`
  - Settings checkbox to enable/disable sharing
  - CLI subcommands `wpep community --enable/--disable/--status`
  - Public leaderboard page at `/community.html` (top tweaks by sample size)
  - Full design: [docs/V7_REMOTE_BACKEND_DESIGN.md](docs/V7_REMOTE_BACKEND_DESIGN.md)
- **GitHub Actions release workflow**: pushing a `v*` tag triggers an automatic
  build + test + zip + GitHub release. Sanity-checks that the tag version matches
  `AppVersion.Current` and `tools/package-release.sh`. Now looks for release notes
  at `docs/RELEASE_NOTES_v<version>.md` first (per-release), then legacy
  `RELEASE_NOTES.md` at root, then falls back to auto-generated commit log.
- **Design token `OnAccent`**: extracted `#0F0F14` from 3 hardcoded sites in
  `MainWindow.xaml` into a proper theme resource. Reduces design-system debt
  for the next theme pass.
- **3 additional KB entries** with primary sources (2026-07-01):
  - `warzone-reflex-on`: Call of Duty Warzone is in the official NVIDIA Reflex list. `SystemSnapshot` gains `WarzoneInstalled` (Steam app 1962663); `wpep doctor` now lists Warzone alongside the other titles; `NetworkDuel.GamePublisher` gains the `warzone → callofduty.com` route anchor, so `wpep network warzone` works out of the box (kept in sync with the existing `GamePublisher_CoversEveryKbGameSlug` parity test).
  - `ultimate-performance-plan-enable`: unlocks the hidden Ultimate Performance power scheme via `powercfg -duplicatescheme` (desktop only, conflicts with `power-plan-high-performance`).
  - `delivery-optimization-p2p-disable`: turns off Windows Update peer-to-peer upload — a common source of jitter during gaming on asymmetric links. MS Learn documented.
- Knowledge Base now has **133 entries** (was 122).
- Documentation: [docs/V7_REMOTE_BACKEND_DESIGN.md](docs/V7_REMOTE_BACKEND_DESIGN.md)
  (14 sections) and [docs/RELEASE_V1.1_RUNBOOK.md](docs/RELEASE_V1.1_RUNBOOK.md)
  (12 sections, includes the new automatic Actions flow).
- **EVGA + NZXT** added to the BIOS guide vendor picker. Both use AMI Aptio
  BIOS similar to ASRock, so the page shows the ASRock steps with an honest
  disclaimer banner instead of inventing per-model specifics
  (fedele alla regola d'oro KB).
- **CLI `wpep doctor`** now prints Secure Boot + TPM 2.0 readiness alongside
  the existing fields — same data the GUI "Pronto per Vanguard" card uses.
- **README polish**: vetrina pubblica aggiornata con tutti i 4 brain AI,
  card Vanguard, vetrina community, link a runbook/workflow, status table
  estesa a V6 / V7 / V8.
- **CONTRIBUTING.md rewritten end-to-end**: ora riflette V2 Execution Engine
  (era ancora fermo a "V1 is read-only" dell'inizio 2026), la regola d'oro
  KB con esempi di fonti primarie valide, checklist step-by-step per
  aggiungere un tweak KB / un gioco (con tutti i 6 punti di parity) / un
  co-pilot brain. Include workflow PR + build/test locali.
- **PR template esteso** (`.github/PULL_REQUEST_TEMPLATE.md`): 2 checkbox nuovi
  che rimandano a CONTRIBUTING.md — se il PR aggiunge un gioco (6 punti di sync)
  o un co-pilot brain (ICoPilotBrain + config + ViewModel + settings + CLI + doc).
- **`.github/` templates**: `CHANGELOG.md`, ISSUE_TEMPLATE/(bug_report +
  feature_request), PULL_REQUEST_TEMPLATE per ready-to-contributor.
- **Hone competitor integration** (`docs/VS_HONE.md`): 4 feature derivate
  dall'analisi Perplexity di [Hone](https://gethone.co), l'app di gaming
  tweaks su Epic Games. Prese solo le meccaniche coerenti coi principi di
  Verdict, scartata la retorica del "+15-30% FPS universale":
  - **System Noise Score** (0–100): quanto è rumoroso il sistema per il
    gaming. Onestà attiva contro il placebo — su PC puliti i tweak
    background non produrranno FPS misurabili, e Verdict lo dice.
  - **Macro-categorie UI**: raggruppamento alternativo a 4 bucket
    (FPS/Latenza, Network/Ping, Stabilità/QoL, Sfondo) attivabile con un
    toggle nella pagina Verdict. La KB non cambia.
  - **Gaming Session Mode**: `wpep session` (o CTA nella card Rumore)
    abbassa temporaneamente la `ProcessPriorityClass` dei bloater noti
    (Discord, OneDrive, Dropbox, Google Drive, Spotify, updater
    Edge/Chrome/Steam/Epic). Reversibile al Ctrl+C. Anti-cheat safe per
    design: non uccide processi, non stoppa servizi, non tocca il gioco.
  - Doc pubblico `docs/VS_HONE.md`: posizionamento onesto vs Hone,
    inclusa la sezione "quando SCEGLIERE Hone invece di Verdict".
- **RapTechPC competitor research** (`docs/VS_RAPTECHPC.md`): ricerca 2026-07-01
  su RapTechPC's Free Optimizer Tool. Conclusione: nessuna documentazione
  tecnica pubblica verificabile, tutte le feature ipotizzate sono già
  interamente coperte da Verdict. Zero azioni concrete, doc archiviato
  per completezza con protocollo di riapertura se il repo diventerà pubblico.

### Changed
- `CommunityService.Record` now also fires-and-forgets `SubmitAsync` to the
  configured backend (no-op on `LocalOnlyBackend`); caller no longer needs to
  invoke the backend directly.
- `HANDOVER.md` updated: closed the `ApplyDialog` cleanup item (already done
  in commit `1bd1fe5`) and the V7 design item (now spec'd in full).
- `docs/V7_REMOTE_BACKEND_DESIGN.md` header refreshed to reflect that the design
  is **implemented and LIVE** (not "to implement") — Cloudflare Worker deployed,
  client `RemoteBackend` in `Community.cs`, CLI + Settings opt-in wired.
- `docs/CLAUDE_DESIGN_BRIEF.md` 2026-07-01 addendum refreshed: the 5 components
  of the 2nd design pass ("Rumore" card, Vanguard card, bucket toggle, Missile
  Button, 4-brain selector) are now marked as **implemented** (via the 7-commit
  design spike) instead of "must be handled" — closes the loop between brief
  and reality.

### Community / OSS polish (2026-07-01)
- **`SECURITY.md`** — responsible disclosure policy: modello di minaccia in 2 righe, superficie non-negoziabile (no kernel driver, no injection, no telemetry default), come segnalare, tempi risposta realistici.
- **`CODE_OF_CONDUCT.md`** — 3 regole corte: sii diretto, rispetta la regola d'oro, non tollerato.
- **`docs/FAQ.md`** — nuove risposte a domande frequenti (anti-cheat, brain cloud, community, "perché unsigned", "perché WPF", ecc.).
- **`docs/ROADMAP.md`** — banner "STATO 2026-07-01: V1–V8 completate", sezione **Post-v1.1** con target v1.2 (loc EN, nuovi giochi, PDF export, SessionMode espansione) e visione v2.0.
- **`.github/workflows/ci.yml`** (nuovo) — CI su push/PR: `dotnet restore + build + test` su `windows-latest`, 0 warning/0 failing atteso.
- **`SessionMode.KnownNoiseProcesses` esteso**: aggiunti `slack`, `Zoom`, `Teams`, `ms-teams`, `WhatsApp`, `Telegram` (comuni bloater da lavoro/messaging). Coerente col nuovo elenco documentato in `docs/VS_HONE.md` §3.3 e nella card bullet Session Mode del README.

### verdict-community (repo separato, LIVE su GitHub)
- Repo pubblico creato su [Leongithacc/verdict-community](https://github.com/Leongithacc/verdict-community) 2026-07-01 con MIT LICENSE.
- **CI workflow** (`.github/workflows/ci.yml`): `tsc --noEmit + npm test + wrangler dry-run` su ogni PR.
- **CONTRIBUTING.md dedicato**: privacy-first come regola d'oro non-negoziabile, setup dev locale, come aggiungere endpoint / migrazione DB.
- **Issue + PR templates**: bug report con endpoint/body, feature request con obbligo di dichiarare impatto privacy.
- **README badges**: CI status, License MIT, endpoint live.

### Internal
- 9 new smoke tests in `CoPilotTests` (3 per cloud brain) — no network calls.
- 2 new smoke tests in `CoPilotTests` for `OllamaBrain` (default model `qwen2.5`, custom model wins), symmetrizing coverage across all 4 brains.
- 3 new integrity tests in `KnowledgeBaseTests` — safety nets against silent typos:
  - every KB entry's `game` field must be in the 8-key allowlist (fortnite / valorant / cs2 / apex / overwatch2 / thefinals / r6siege / warzone)
  - every `category` in the 7-key allowlist (power / gpu / scheduler / input / network / background / security)
  - every `source` must be an absolute `http(s)://` URL that passes `Uri.TryCreate` (catches malformed URLs like `www.foo.com` without a scheme that would silently fail on browser open).

### Round 3 (2026-07-01, autonomous session)
- **KB URL audit**: verified 30+ potentially unstable URLs via WebFetch. Fixed 5 dead/redirected URLs:
  - `windows-game-mode` and `disable-gamedvr-background-recording` now point to the new Xbox support hub (Microsoft moved gaming support pages to xbox.com).
  - `windows-update-active-hours-extend` → new MS support URL (old one 404'd).
  - `focus-assist-fullscreen-game` → new MS support URL for the renamed "Do Not Disturb" feature (old Focus Assist URL 404'd).
  - `win11-variable-refresh-rate` → new DirectX devblog URL for OS Variable Refresh Rate.
- **2 new KB entries** with primary MS Learn sources:
  - `storage-sense-disable` — prevents automatic cleanup from running during a gaming session (I/O spike prevention). Documented setting.
  - `nic-power-management-off` — Device Manager tab / `Disable-NetAdapterPowerManagement` PowerShell cmdlet, for stable ping and no reconnect hitches on power state transitions.
- Knowledge Base now has **135 entries** (was 133).
- **Worker `/v1/health`** endpoint (retro-compat, safe): returns `{status, service, version, db, timestamp}` after a read-only DB probe. Returns 503 if D1 is unreachable — suitable for external uptime monitors (BetterUptime, UptimeRobot). No PII exposed.
- **vitest test** for `/v1/health` (asserts status + db=ready + Cache-Control no-store).
- **`docs/COMPARISON.md`** — matrice onesta Verdict vs Hone / RapTechPC / Wemod / Iolo / IObit / RTSS / MSI Afterburner / Windows Game Mode. Nessuno è "cattivo": documenta quando scegliere quale, e cosa Verdict esplicitamente NON è.
- **`docs/ARCHITECTURE.md`** — reference architetturale con ASCII diagram client/backend, moduli, vincoli invarianti (`WPEP.Core` no-UI, `InvariantGlobalization=false` solo App, regola d'oro codificata, ecc.).
- **`docs/BLOG_POST_DRAFT.md`** — draft blog post per HN "Show HN" / DEV.to / r/pcgaming / Medium al lancio v1.1. 600 parole + note per canale.
- **`SUPPORT.md`** — canali di supporto ordinati per tipo di richiesta.
- **`.editorconfig`** — cross-editor consistency per file .cs/.md/.ts/.xaml/.ps1/.sh/.yml.
- **Site EN localization**: `site/index.en.html` e `site/community.en.html` completi con language switcher IT ↔ EN bidirezionale.
- **`.gitignore`**: aggiunta esclusione `SCELTE_APERTE_*.md` e `NOTE_PERSONALI_*.md` per file di lavoro personali.
- 5 new sanity tests in `SessionModeTests` for the curated `KnownNoiseProcesses` list: no `.exe` suffix, no case-insensitive duplicates, no whitespace-only or untrimmed entries, coverage of the families documented in `docs/VS_HONE.md` §3.3, and `OriginalState` record value equality. No `Process.GetProcessesByName` calls — safe in CI.
- 6 new tests in `SystemSnapshotTests` covering `NoiseBand` thresholds (boundary tests at 25/26/55/56), `GameInstalled` switch parity for all 8 known game keys (fortnite, valorant, cs2, apex, overwatch2, thefinals, r6siege, warzone), null-safety, and `NoiseFactors` default (empty, not null).
- DPAPI key encrypt/decrypt extracted to shared helpers in `AppSettings`
  (DRY across Claude / Gemini / OpenAI keys).

### Security / audit fixes (2026-07-02)

Findings from the aggressive internal audit of 2026-07-02, all fixed same day:

- **`TryCreateRestorePoint` hardened** (`ExecutionEngine.cs`): the description is
  now reduced to a character whitelist before being interpolated into the
  PowerShell `-Command` string. The previous `'` → `''` escape did not cover `"`,
  which could break out of the argument quoting (not exploitable today — the only
  caller passes `Verdict: <tweak-id>` with ids restricted to `[a-z0-9-]` — but
  fragile against future callers).
- **`EvidenceLedger.Append` made atomic** (`Community.cs`): writes to a temp file
  then renames over `evidence.json`. A crash mid-write previously left a truncated
  JSON, which `Load()` silently turned into an empty history. Capped at 5000
  records (the file is fully rewritten per append). New test: corrupt-file
  recovery + no leftover `.tmp`.
- **`OpenSettings` runtime allowlist** (`ApplyFlow.cs`): the deep-link prefix
  allowlist that CI enforces on the shipped KB is now also enforced at runtime
  (defence-in-depth against a locally tampered KB reaching
  `Process.Start(UseShellExecute: true)`).
- **`VersionCompare` overflow fix** (`UpdateCheck.cs`): numeric components are
  parsed as `long`, and components too large even for `long` saturate instead of
  collapsing to 0 (which could have hidden a real update). 3 new test rows.
- **Honest Sybil-limit disclosure (audit M1)**: the community leaderboard numbers
  are self-reported by anonymous clients and unattested — now stated explicitly
  in `docs/PRIVACY.md` §3.3, `docs/V7_REMOTE_BACKEND_DESIGN.md` §9 (which
  previously overstated the implemented defences) and on both community pages.
- (verdict-community repo) **CORS fixed on all responses** — the leaderboard
  fetch from github.io was blocked by the browser; **rate-limit comments now
  match reality** (60 req/min per rig, client-controlled key; per-IP defence
  requires a Cloudflare WAF rule); **the vitest suite is actually runnable** for
  the first time (setup used `readFileSync` inside workerd — CI had been red
  since creation); toolchain upgraded to wrangler 4 + vitest 4 +
  vitest-pool-workers 0.17 with `tsc --noEmit` finally green.

## [1.0] — 2026-06-26

First public release. See the
[v1.0 release notes](https://github.com/Leongithacc/Verdict/releases/tag/v1.0)
and [docs/HANDOVER.md](docs/HANDOVER.md) for the full scope of what shipped:
hardware analyzer, advisor, V2 execution engine (registry + powercfg + bcdedit),
statistics engine (Mann–Whitney + bootstrap + noise gate), PresentMon-based
benchmark, ETW DPC/ISR diagnostics, knowledge base (122 entries), HTML
reporting, V5 automation (tray watchdog + sentinel + time machine), V6 AI
co-pilot (Ollama only, single brain), V7.0 local-only community ledger,
V8 GitHub-Releases auto-update, BIOS guide QR per vendor, 10 themes,
full Italian localization.

[Unreleased]: https://github.com/Leongithacc/Verdict/compare/v1.0...HEAD
[1.0]: https://github.com/Leongithacc/Verdict/releases/tag/v1.0
