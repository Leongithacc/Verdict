# Contributing to Verdict / WPEP

## The one rule above all others

**Every new Knowledge Base entry requires a primary source** — vendor documentation:
Microsoft Learn, NVIDIA (nvidia.com, developer.nvidia.com), AMD (www.amd.com),
Intel (intel.com), Epic (epicgames.com), Riot, Steam (store.steampowered.com),
etc. Community blogs, YouTube videos, Reddit threads, and forum posts are **not**
primary sources.

No primary source → the evidence grading cannot be better than `controversial`.
Prefer grading something `placebo` (with a source that debunks the myth) over
inventing evidence for it.

This is the "regola d'oro" — it's what separates Verdict from every marketing-first
optimizer competitor. See [docs/VS_HONE.md](docs/VS_HONE.md) and
[docs/VS_RAPTECHPC.md](docs/VS_RAPTECHPC.md) for the philosophy in context.

## Non-negotiables

- **Statistics for any performance claim.** Run as the unit of observation,
  Mann–Whitney + bootstrap CI, noise gate. "No measurable effect" is a valid and
  frequent result — Verdict must be willing to say it.
- **No in-game overlay, no injection, no kernel driver, ever.** Frame data comes
  from Windows ETW (the same passive channel Intel PresentMon uses). This is what
  keeps Verdict anti-cheat compatible.
- **Portable leave-no-trace.** Nothing outside the app folder. No installer, no
  service, no scheduled task without consent. All settings live next to the exe.
- **Every write is journaled and reversible.** The V2 Execution Engine (registry
  / powercfg / bcdedit / nvidia-drs) shows an exact before → after dry-run, asks
  for consent per-change, re-reads to verify, and stores enough state to undo.
  No "optimize everything" button, ever.
- **Impossible claims are refused, not softened.** End-to-end input latency in
  pure software is impossible → we don't claim it.

## Adding a KB entry — checklist

1. Find a primary vendor source. If you can't, stop.
2. Fill every required field in `src/WPEP.KnowledgeBase/kb/tweaks.json`:
   `id`, `name`, `category` (one of: `power` / `gpu` / `scheduler` / `input` /
   `network` / `background` / `security`), `description`, `expected_impact`,
   `evidence_level`, `sources` (≥ 1 URL), `risk`, `manual_steps`, `rollback`.
   For game-specific entries, `game` must be one of the 8 allowlist keys
   (fortnite / valorant / cs2 / apex / overwatch2 / thefinals / r6siege / warzone).
3. If the tweak can be applied programmatically, add an `apply` block with
   `method` (registry / powercfg / bcdedit / nvidia-drs / dxuser / gui-only)
   and the operations. `gui-only` requires a `gui_only_reason`.
4. If the tweak is BIOS-only, add its id to the `Guided` HashSet in
   `src/WPEP.Core/Bios/BiosGuide.cs` and to the `T` object in `site/bios.html`
   for each supported motherboard vendor (IT + EN).
5. Run `dotnet test` — the KB integrity tests will catch a dropped field or an
   unknown category/game slug before it hits `main`.

## Adding a game

You need to touch **four** places, kept in sync by the parity tests:

1. `src/WPEP.Core/System/SystemSnapshot.cs` — add a `XxxInstalled` property and
   a case in the `GameInstalled` switch.
2. `src/WPEP.SystemAnalyzer/SnapshotBuilder.cs` — add a `ReadXxxInstalled()`
   method (Steam app / Battle.net path / Epic manifest, whatever applies) and
   the `Probe(...)` line.
3. `src/WPEP.SystemAnalyzer/NetworkDuel.cs` — add a `GamePublisher["xxx"]` entry
   with the publisher's public CDN host (the `GamePublisher_CoversEveryKbGameSlug`
   test will otherwise fail).
4. `src/WPEP.Cli/Program.cs` — add the `("xxx", "Display Name")` tuple to the
   `games` array in `wpep doctor`.
5. Update the allowlist in `tests/WPEP.Tests/KnowledgeBaseTests.cs`
   (`ShippedKb_EveryGameFieldIsInAllowlist`).
6. Add at least one KB entry with `"game": "xxx"`.

## Adding a co-pilot brain

Verdict has 4 brains behind `ICoPilotBrain` (`src/WPEP.Advisor/CoPilot/`). Add a
new one by:

1. Implementing `ICoPilotBrain` (see `ClaudeBrain.cs` / `GeminiBrain.cs` /
   `OpenAiBrain.cs` for the pattern).
2. Adding a `DefaultXxxModel` constant in `CoPilotConfig` (defined at the top
   of `OllamaBrain.cs`).
3. Wiring the brain in `CoPilotViewModel.BuildService`.
4. Extending `AppSettings` (encrypted API key via DPAPI helpers) + adding
   the CLI subcommand in `Program.cs`.
5. Documenting the brain in `docs/BRAINS.md` (when to use, model options, env var).

## Workflow

- Fork → branch → PR against `main`. Use the PR template in
  `.github/PULL_REQUEST_TEMPLATE.md`.
- CI runs `dotnet build -c Release` + `dotnet test`. Both must be 0 warnings /
  0 failures (`TreatWarningsAsErrors=true` is repo-wide).
- For KB-only changes, expect the reviewer to click through every source URL.
  If a URL 404s or isn't a primary source, the PR is rejected regardless of
  how correct the underlying tweak is.

## Build & test locally

```
.NET 10 SDK required.
dotnet build WPEP.sln -c Release -m:1 --disable-build-servers -v q
dotnet test  WPEP.sln -c Release -m:1 --disable-build-servers -v q
```

Historical design records: see `docs/WPEP_docs_handoff/` for the V1 architecture
choices from early 2026 (still useful for context, superseded on some details
by the V2+ features documented in the top-level [docs/HANDOVER.md](docs/HANDOVER.md)).
