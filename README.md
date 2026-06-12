# WPEP — Windows Performance Engineering Platform

**The only optimizer that tells you when to stop optimizing.**

WPEP measures, diagnoses and recommends gaming-performance changes on Windows —
honestly, with statistics, and without ever touching your system. For most popular
"gaming tweaks" the honest answer is *no measurable effect*, and WPEP is built to
say exactly that.

## What it does

- **Verdict** — scans your hardware and configuration (~30 read-only probes) and
  tells you what's worth doing, what's already optimal, and which popular tweaks
  are documented placebos. Every recommendation cites a primary source.
- **Measure** — a guided wizard: baseline runs → apply ONE change by hand → post
  runs → statistical verdict (Mann–Whitney + bootstrap CI, the run is the unit of
  observation). Includes scenario protocols (e.g. an automated Fortnite benchmark
  map) so your runs are actually comparable.
- **Noise gate** — if your scenario's run-to-run noise exceeds the minimum
  detectable effect threshold, WPEP refuses to emit a verdict instead of
  inventing one. Three outcomes: real effect · no measurable effect · no verdict.
- **Diagnostics** — kernel ETW capture of DPC/ISR latency per driver: finds the
  actual stutter culprit, or tells you there is none.
- **Knowledge Base** — 60 entries graded by evidence (strong / plausible /
  controversial / placebo / risky), each with primary sources, exact manual
  steps and rollback. The placebos are shown on purpose.
- **Validate** — the pipeline certifies itself (A/A test + known-effect test)
  before you trust any verdict.
- **Report** — a shareable dark-theme HTML report of everything above.

## What it will never do

- Write to your system (V1 is read-only by design; the user applies changes by hand)
- Claim to measure end-to-end input latency (impossible in pure software)
- Show you an improvement that isn't statistically real
- Inject code, hook processes, read game memory, or draw overlays — WPEP never
  touches your game. Frame data comes from Windows' own event tracing (ETW), the
  same passive channel used by Intel PresentMon. We cannot offer guarantees on
  behalf of anti-cheat vendors, but WPEP belongs to no category anti-cheat
  systems target.

## Portable by design

One folder, no installer, no services, no registry writes. All data (runs,
reports, settings, tools) lives next to the exe. Delete the folder and WPEP was
never here.

## Usage

**App:** unzip → run `WPEP.exe`. The first scan needs no administrator;
Measure and Diagnostics ask for elevation only when you use them (ETW is
admin-only by Windows design).

**CLI** (`wpep-cli.exe`, same engine):

```
wpep analyze                 read-only system snapshot
wpep advise                  verdicts for this PC
wpep kb [show <id>]          knowledge base with sources
wpep bench --process game.exe --seconds 60 --runs 5 --label baseline --out runs\baseline
wpep compare --baseline <dir> --post <dir> [--gate 10]
wpep noise --dir <dir>       natural run-to-run variance
wpep validate --a <dir> --b <dir> --expect none|effect
wpep report [--out file.html] [--runs dir] [--baseline a --post b]
wpep diag --seconds 30       DPC/ISR per driver (admin)
wpep tools install-presentmon   pinned 2.4.1, SHA256 verified
```

## Method, in one paragraph

Frametime distributions are non-normal with fat tails, and run-to-run variance is
real. So: N runs per configuration in a repeatable scenario, the run as the unit
of observation (never pooled frames — pooling makes any microscopic delta
"significant"), Mann–Whitney permutation test plus bootstrap CI on the difference
of medians, an environment fingerprint per run (comparison blocked if your
driver/display/power plan changed), and a noise gate: when the baseline's minimum
detectable effect exceeds the threshold, no verdict is emitted. Outlier runs are
flagged, never silently dropped.

## Build

.NET 10 SDK → `dotnet build` · `dotnet test` (94 tests). WPF app in
`src/WPEP.App`, CLI in `src/WPEP.Cli`, engine modules underneath — UI and CLI
share the same services.

License: MIT · See [CONTRIBUTING.md](CONTRIBUTING.md) — the golden rule:
**no source, no recommendation.**

## Status

| Milestone | State |
|---|---|
| R1 Diagnostics (ETW DPC/ISR) | ✅ validated live |
| R2 Benchmark (PresentMon wrapper) | ✅ validated live |
| R3 Statistics (MW + bootstrap + noise gate + MDE) | ✅ |
| R4 Knowledge Base (60 entries, primary sources) | ✅ ([research notes](docs/KB_RESEARCH.md)) |
| R5 Advisor + SystemAnalyzer | ✅ |
| R6 Reporting (HTML) | ✅ |
| R7 App (verdict-first UI, wizard, first-run, theming) | ✅ first complete iteration |
| Pipeline certification — A/A test | ✅ passed on machine 1 (MDE 0.8%, no false positive) |
| Pipeline certification — known-effect test | ✅ passed (frame-cap detected, p=0.008, all metrics) |
| V2 Execution Engine (apply + undo-journal + re-measure) | 🔒 gated until certification + open source |
