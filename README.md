# Verdict

**The only optimizer that tells you when to stop optimizing.**

*(engine codename: WPEP — Windows Performance Engineering Platform)*

WPEP measures, diagnoses and recommends gaming-performance changes on Windows —
honestly, with statistics. For most popular "gaming tweaks" the honest answer is
*no measurable effect*, and WPEP is built to say exactly that.

Measurement and diagnosis are strictly read-only. WPEP can also **apply** the
subset of recommendations that are safely scriptable — but only the ones backed by
a primary source, only after you approve an exact dry-run, and every change is
journaled and reversible with one click. It never "optimizes everything", and it
never applies a placebo.

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
- **Knowledge Base** — 85 entries graded by evidence (strong / plausible /
  controversial / placebo / risky), each with primary sources, exact manual
  steps and rollback. The placebos are shown on purpose.
- **Apply** — for the entries that are safely scriptable (registry / power plan /
  bcdedit), Verdict can apply them one at a time or all the recommended ones at
  once, behind a single dry-run. Each write is verified by re-reading, journaled,
  and undoable per-change from the Changes page. A conflict guard prevents applying
  two mutually-exclusive tweaks. ~12 of the 85 entries are one-click; the rest stay
  manual (in-game / BIOS / driver panel) with deep-links where possible.
- **Self-test** — `verify the apply engine works on this machine` before you trust
  it: a write→verify→undo round-trip on a throwaway registry key (no real setting
  touched).
- **Validate** — the pipeline certifies itself (A/A test + known-effect test)
  before you trust any verdict.
- **Report** — a shareable dark-theme HTML report of everything above, with the
  one-click-applicable entries badged and the changes you've applied listed.

## What it will never do

- Write anything without your explicit, per-change consent shown as an exact
  before→after dry-run first (measurement and diagnosis stay fully read-only)
- Apply anything that isn't a primary-source-backed entry in the Knowledge Base —
  no "optimize everything" button, no placebos, ever
- Make a change it can't undo (every write is journaled and reversible)
- Claim to measure end-to-end input latency (impossible in pure software)
- Show you an improvement that isn't statistically real
- Inject code, hook processes, read game memory, or draw overlays — WPEP never
  touches your game. Frame data comes from Windows' own event tracing (ETW), the
  same passive channel used by Intel PresentMon. We cannot offer guarantees on
  behalf of anti-cheat vendors, but WPEP belongs to no category anti-cheat
  systems target.

## Portable by design

One folder, no installer, no background services. All data (runs, reports,
settings, journals, tools) lives next to the exe. The only system writes are the
tweaks you explicitly approve — each journaled here and reversible. Delete the
folder and WPEP was never here.

## Usage

**App:** unzip → run `WPEP.exe`. The first scan needs no administrator;
Measure and Diagnostics ask for elevation only when you use them (ETW is
admin-only by Windows design).

**CLI** (`wpep.exe`, same engine):

```
# read-only
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

# apply (the deliberate write path — dry-run unless --yes)
wpep apply <id>              show the exact before→after; writes NOTHING
wpep apply <id> --yes        apply it (verified + journaled); HKLM/boot need admin
wpep apply-all [--yes]       all recommended+applicable tweaks, one consent, each undoable
wpep changes                 list journaled sessions (applied / undone)
wpep undo <file|last>        restore previous values, verified
wpep selftest                prove the engine works here (scratch key, full cleanup)
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

.NET 10 SDK → `dotnet build` · `dotnet test` (125 tests). WPF app in
`src/WPEP.App`, CLI in `src/WPEP.Cli`, engine modules underneath — UI and CLI
share the same services. (If a parallel build crashes an MSBuild node, build
single-node: `dotnet build -m:1 --disable-build-servers`.)

License: MIT · See [CONTRIBUTING.md](CONTRIBUTING.md) — the golden rule:
**no source, no recommendation.**

## Status

| Milestone | State |
|---|---|
| R1 Diagnostics (ETW DPC/ISR) | ✅ validated live |
| R2 Benchmark (PresentMon wrapper) | ✅ validated live |
| R3 Statistics (MW + bootstrap + noise gate + MDE) | ✅ |
| R4 Knowledge Base (85 entries, primary sources) | ✅ ([research notes](docs/KB_RESEARCH.md)) |
| R5 Advisor + SystemAnalyzer (Fortnite/Valorant/CS2/Apex/OW2 detection) | ✅ |
| R6 Reporting (HTML, one-click badges, applied changes) | ✅ |
| R7 App (verdict-first UI, wizard, first-run, theming) | ✅ |
| Pipeline certification — A/A test | ✅ passed on machine 1 (MDE 0.8%, no false positive) |
| Pipeline certification — known-effect test | ✅ passed (frame-cap detected, p=0.008, all metrics) |
| V2 Execution Engine (registry / powercfg / bcdedit, dry-run + journal + undo) | ✅ engine self-test PASS on machine 1; real powercfg/bcdedit writes pending a live apply |
| Apply in GUI + CLI (single, batch, conflict guard, admin gating) | ✅ |
