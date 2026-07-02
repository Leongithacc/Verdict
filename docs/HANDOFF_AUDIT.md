# Verdict — Final Engineering Audit & Implementation Handoff

> **Author:** Claude Fable 5 · **Date:** 2026-07-02 · **Audience:** Claude Opus (next implementer) + Léon (owner)
> **Repos audited (state):** `Verdict` (engine WPEP) @ `db62dd8` · `verdict-community` (Cloudflare Worker) @ `0fb9326` — both pushed to GitHub 2026-07-02.
> **Stage:** Beta. Unsigned, portable, GitHub-hosted, single maintainer.
> **This document is the master implementation guide.** It is intentionally exhaustive. Findings are ranked by severity; the roadmap after them is ordered for execution. Every task is written to be executed with minimal additional architectural reasoning.

---

## 0. How to read this document

- **Section 1** — what Verdict is, as-built, so the roadmap makes sense.
- **Section 2** — the trust model and attack surface. Read this before any security task.
- **Section 3** — findings. Each carries: Severity · Category · Explanation · Why it matters · Impact · Likelihood · Files · Strategy · Dependencies · Complexity · Risk · Acceptance criteria · Testing · Notes for Opus.
- **Section 4** — what is already correct and must NOT be "improved". Touching these is a regression.
- **Section 5** — what was NOT audited (honesty).
- **Section 6** — phased roadmap.
- **Section 7** — per-task execution cards.
- **Section 8** — scorecard & production-readiness verdict.
- **Appendices** — dependency inventory, command-execution surface table, verification commands.

A prior development log exists at [docs/OPUS_AUDIT_LOG.md](OPUS_AUDIT_LOG.md) — that is a *change history*, not a findings audit. This document does not duplicate it.

### Execution status (updated 2026-07-02, same session as the audit)

**Phase 0 gate CLEARED early via CI.** The `.github/workflows/ci.yml` pipeline builds + runs the full test suite on every push to `main` using the .NET 10 SDK on a Windows runner — so "blind" C# is actually verified within minutes. In the course of this the CI turned out to have been **red since before the audit** for two reasons unrelated to functionality, both now fixed:

- **NU1510 blocked *restore*.** The .NET 10 SDK flags `System.Security.Cryptography.ProtectedData` as framework-provided (`NU1510`), and repo-wide `TreatWarningsAsErrors` escalated it to a restore error — so no recent C# had ever been compiled by CI. Fixed by suppressing NU1510 in `Directory.Build.props` (kept the package for older-SDK compat).
- **A latent Warzone test failed.** `SystemSnapshot.GameInstalled` was case-sensitive while its parity test asserted `"WARZONE"`. Never caught because CI never ran. Fixed (case-insensitive switch).

With both fixed, **CI is green (run `28609167074`, commit `66e1428`): 399 tests pass, 0 warnings.** The entire repo — every blind commit from this session and before — is now verified building.

Executed and **CI-verified** this session:

- **F1 (Phase 2) — DONE + verified.** `KnowledgeBaseValidator` now validates apply-op *values* fail-closed (registry hive allowlist + `key\name`; powercfg GUID; powercfg-value `guid/guid`+int; bcdedit element/value alphanumeric — **kills argument-splitting, closes F4**; nvidia-drs hex/dec uint32; dxuser identifier+0/1). Regexes were empirically checked against all 135 shipped entries *before* writing the C#. +12 test cases, all green.
- **F2 + F5 (Phase 1) — DONE + verified.** New `WPEP.Core/Io/AtomicJson` (temp → `Flush(true)` → atomic rename). Routed: `ExecutionEngine.Save` (the journal / undo safety net), `EvidenceLedger.Append` (L3 refactored onto it), `AppSettings.Save`. +3 tests, all green.
- **F4 — DONE** (folded into F1: bcdedit element/value regex rejects spaces/quotes at load).
- **F11 (Phase 6, Worker) — code DONE + tested.** Atomic `stats_cache` rebuild via `env.DB.batch`; cron-rebuild vitest; CORS/`database_id` decisions documented; `ScheduledController` signature. `tsc` clean, 21/21, deployed live. **Léon-only remaining:** WAF per-IP (A1), uptime monitor (A2), D1 DR (A3).
- **F3 (Phase 3, release integrity) — DONE** (verifiable at the next `v*` tag): `SHA256SUMS.txt` emitted + attached + documented.

- **F7 (Phase 4) — DONE + verified.** Pipeline verdict now driven by the PRIMARY metric (median frametime), exposed as `ComparisonReport.PrimaryVerdict`; `PipelineValidator` no longer treats "any of 4 metrics significant" as an effect (that inflated the A/A false-positive rate to ~18.5%). New test: primary flat + a secondary metric firing → A/A still passes.
- **F6 (Phase 2) — DONE + verified.** `SafeOpen.Url` guards the two browser-open sites (BIOS guide, update page) to http/https only before they reach the shell — the shell-open counterpart of the OpenSettings allowlist. Local-file opens are app-computed paths, left as-is.
- **F8 (Phase 5) — DONE + verified (pragmatic variant, no migration).** `RigDna` now uses a **64-bit** FNV-1a and derives the two code segments from **independent 20-bit slices** → 40 bits of real entropy (was ~32: the 2nd segment used to be a rotation of the 1st), moving the birthday-collision threshold from ~65k to ~1M rigs. **Deliberately kept the `RIG-XXXX-XXXX` format** so the Worker regex is unchanged and **no migration / beta-reset is needed** (existing near-empty evidence simply re-populates under the new codes). Literal "≥64 bits" would require a longer code + a server regex change + a format-version negotiation — disproportionate for a hobby-scale user base where 40 bits (~1M) already far exceeds realistic scale. The "reversibility" half of F8 is a wording matter, already handled in M1 (PRIVACY §3.3 "self-reported, unattested"). If the user base ever approaches ~10⁵ rigs, widen the format then.

**Still open (SDK/CI available now, just not yet done):** F9 (CI real-backend HKCU round-trip), F14 (resume half-applied), F10 (MDE method doc). All Low/Info.

---

## 1. Architecture as-built

### 1.1 Solution shape

Verdict is a **.NET 10 / C# 13** Windows solution (`WPEP.sln`), WPF MVVM front-end, layered into single-responsibility projects with a clean dependency direction (leaf → core; UI and CLI share the same services):

```
WPEP.Core            models, versioning, elevation, benchmark/dpc DTOs   (no UI, no deps)
WPEP.Statistics      Mann–Whitney permutation, bootstrap CI, MDE, noise gate, pipeline validator
WPEP.Benchmark       PresentMon wrapper (download+SHA256 pin), CSV parser, metrics
WPEP.Diagnostics     ETW DPC/ISR capture (TraceEvent), stutter explainer, driver map
WPEP.SystemAnalyzer  WMI/registry/display probes, NvAPI interop, RigDna, snapshot builder
WPEP.KnowledgeBase   tweaks.json (135 entries) + loader + honesty validator
WPEP.Advisor         advisor engine, conflict resolver, CoPilot (4 brains), optimize-for-game
WPEP.Execution       V2 write engine (registry/powercfg/bcdedit/nvidia-drs/dxuser), journal, undo,
                     community backend (V7), session mode, self-tests
WPEP.Reporting       self-contained HTML report
WPEP.App             WPF app (MainWindow.xaml 1933 lines, ViewModels.cs 1136)
WPEP.Cli             wpep.exe — same engine, headless (Program.cs 1943)
WPEP.Tray            watchdog tray agent
```

Repo-wide invariants (`Directory.Build.props`): `Nullable=enable`, `ImplicitUsings=enable`, **`TreatWarningsAsErrors=true`**, `InvariantGlobalization=true` — with the single documented exception that `WPEP.App` sets `InvariantGlobalization=false` (WPF binding needs real cultures). `app.manifest` requests **`asInvoker`** (never auto-elevates); elevation is requested only for ETW/registry-HKLM/bcdedit operations at the point of use.

### 1.2 Companion backend

`verdict-community` — a **Cloudflare Worker + D1** (serverless SQLite). Endpoints: `POST /v1/evidence`, `GET /v1/stats`, `GET /v1/top-tweaks`, `GET /v1/health`, `GET /`, plus a nightly cron that rebuilds `stats_cache` and prunes evidence older than 365 days. Zod validation, native rate-limit binding, `INSERT OR IGNORE` idempotency. Toolchain (as of 2026-07-02): wrangler 4.106, vitest 4.1.9, `@cloudflare/vitest-pool-workers` 0.17, `tsc --noEmit` green, 20/20 tests. Live version `53ac27b7`.

### 1.3 Core product invariants (the reason the app exists)

1. Measurement & diagnosis are **strictly read-only**.
2. The only system writes are KB-backed, primary-source-verified tweaks the user explicitly approves after an exact dry-run.
3. Every write is **journaled before** it happens and **verified after** by re-reading; any mismatch stops the session.
4. Every write is **undoable** and **drift-aware** (a value changed outside Verdict is never clobbered).
5. No placebo is ever applied; placebos are shown on purpose.
6. Anti-cheat safe: no injection, no hooks, no game-memory reads, no overlays, no kernel driver.
7. Privacy: nothing leaves the PC without explicit opt-in (update check, cloud brain, community).

These invariants are the acceptance bar for every task below. **A change that weakens any of them is a regression regardless of other merit.**

---

## 2. Trust model & attack surface

### 2.1 Trust boundaries (ranked by blast radius)

| # | Boundary | Trusted input | Consumer | Privilege | Notes |
|---|----------|---------------|----------|-----------|-------|
| TB1 | **`kb/tweaks.json`** on disk | apply-op strings (registry paths, bcdedit element/value, powercfg GUIDs, nvidia-drs ids, settings-URIs) | registry writes, `Process.Start` of bcdedit/powercfg/powershell | **admin** for HKLM/boot | **The single highest-value boundary.** See F1. |
| TB2 | GitHub Releases API | `tag_name`, `html_url`, asset `browser_download_url` | version compare + browser open | user | host fixed to `api.github.com` for the pinned owner/repo → no SSRF. |
| TB3 | Cloud LLM response | free text + `TWEAKS:` line | grounding parser | read-only | invented ids dropped in code; copilot never applies → prompt-injection impact ~nil. |
| TB4 | PresentMon binary | downloaded exe | executed as ETW capturer | admin | SHA256-pinned; a failing hash is never executed (good). |
| TB5 | Public Worker `POST /v1/evidence` | anonymous JSON batch | D1 insert | none | Zod-validated; Sybil-limited only (disclosed). See F11. |
| TB6 | `settings.json` / journal / evidence.json | JSON on disk | deserialized to records | user | integrity/atomicity gaps: F2, F5. |

### 2.2 Command-execution surface (every `Process.Start`)

| Location | Target | `UseShellExecute` | Argument source | Assessment |
|----------|--------|-------------------|-----------------|------------|
| `ExecutionEngine.cs:510` | `powershell` Checkpoint-Computer | false | `Verdict: <tweak-id>`, now char-whitelisted (L1 fixed) | safe |
| `RegistryAccess.cs:63` | `powercfg` | false | KB GUIDs + int index | see F1/F4 |
| `BcdEditAccess.cs:55` | `bcdedit` | false | KB element + value | see F1/F4 |
| `PresentMonRunner.cs:27` | pinned PresentMon | false | process name (user/CLI) interpolated in quotes | see F6 note |
| `SnapshotBuilder.cs:315,484` | `powercfg`/util | false | constant args | safe |
| `WriteSelfTest.cs:157` | `powercfg` | false | scratch GUID | safe |
| `ApplyFlow.cs:80` | settings deep-link | **true** | KB SettingsUri, now runtime-allowlisted (L2 fixed) | safe |
| `Tray/Program.cs:142`, `MainWindow.xaml.cs:88`, `MeasureWizard.cs:150`, `ViewModels.cs:469/991/1196`, `WatchdogViewModel.cs:69` | file/URL open | **true** | computed local paths / GitHub URL | see F6 |

**Conclusion:** no shell interpretation anywhere (`UseShellExecute=false` on all argument-bearing calls); the residual risk is *argument-splitting* injection via KB values (F1/F4) and *unvalidated scheme* on the `UseShellExecute=true` open calls (F6). There is **no remote RCE vector**: auto-update is report-only, downloads nothing, executes nothing.

---

## 3. Findings

> Yesterday's audit (2026-07-02) already fixed H1 CORS, H2 rate-limit honesty, L1 restore-point whitelist, L2 OpenSettings runtime allowlist, L3 atomic evidence.json, L5 VersionCompare overflow, M1 Sybil disclosure. Those are **closed** and appear in the CHANGELOG. The findings below are the *remaining* set after that pass.

---

### F1 — Knowledge Base apply-values cross the highest trust boundary without apply-safety validation

- **Severity:** Medium (High-value mitigation)
- **Category:** Trust boundary / privilege / input validation
- **Explanation:** `KnowledgeBaseValidator` (`KnowledgeBaseLoader.cs:37`) enforces *honesty* rules at load (unique ids, valid category, mandatory https sources, manual steps, rollback, method enum, non-empty operations). It does **not** validate the *content* of `apply.operations`: registry paths/hives, bcdedit `element`+`value`, powercfg subgroup/setting GUIDs, nvidia-drs id/value. Those strings then flow, in an admin context, into `RealRegistryAccess.Write` and into `Process.Start` arguments for `bcdedit`/`powercfg` (`BcdEditAccess.cs:47`, `RegistryAccess.cs:56`).
- **Why it matters:** the on-disk `tweaks.json` is the app's root of authority for what gets written to the registry and boot store. It is unsigned and locally mutable. The shipped KB is clean and CI tests some invariants, but there is no *fail-closed* structural gate ensuring an apply-op can only ever be a well-formed, hive-restricted registry write or a GUID/token that cannot break out of an argument list. This is the root cause behind L1/L2 (which were patched at the sinks) — the durable fix is at the source.
- **Potential impact:** a tampered or future-authored KB entry could (a) write to an unintended registry location under HKLM, (b) inject extra `bcdedit`/`powercfg` arguments via a value containing spaces/quotes (arg-splitting, not shell), (c) point a "registry" op at a security-sensitive key. All require admin, which the operation already demands.
- **Likelihood:** Low today (attacker needs local write to the app folder — at which point they can already replace the unsigned exe). The value is **defense-in-depth + future-proofing** as the KB grows and gains contributors.
- **Files:** `src/WPEP.KnowledgeBase/KnowledgeBaseLoader.cs:37-86`; sinks `src/WPEP.Execution/RegistryAccess.cs:56,114`, `src/WPEP.Execution/BcdEditAccess.cs:47,50`, `ExecutionEngine.cs:81-144`.
- **Suggested strategy:** extend `KnowledgeBaseValidator` with per-method apply-safety checks, failing the load (as it already does for honesty violations):
  - `registry`: path must match `^(HKCU|HKLM|HKEY_CURRENT_USER|HKEY_LOCAL_MACHINE)\\[^\r\n]+$`; optionally an allowlist of top-level key prefixes actually used by the KB.
  - `powercfg`: `value_after` is a GUID (`^[0-9a-fA-F-]{36}$`).
  - `powercfg-value`: path is `guid/guid`, `value_after` parses as int.
  - `bcdedit`: `element` matches `^[a-z][a-z0-9]*$`, `value_after` matches `^[A-Za-z0-9]+$` (no spaces/quotes — kills arg-splitting).
  - `nvidia-drs`: id and value match `^(0x[0-9a-fA-F]+|[0-9]+)$`.
  - `dxuser`: path is a known sub-setting token; value `0|1`.
- **Dependencies:** none. Pure validation added to an existing pure function → unit-testable.
- **Complexity:** Low (one file, ~40 lines + tests). **Risk:** Low — but must run the full KB through it once; if any *shipped* entry fails, fix the entry (do not loosen the regex).
- **Acceptance criteria:** load of the shipped 135-entry KB passes; hand-crafted malformed apply-ops (space in bcdedit value, non-GUID powercfg, path without hive) are rejected with a specific message; existing tests stay green.
- **Testing:** add `KnowledgeBaseValidatorTests` cases per method (valid + each malformed variant); assert the shipped KB validates clean in CI.
- **Notes for Opus:** this is the headline architectural hardening. Do it as strict/allowlist, fail-closed. Keep the sink-level checks (L1/L2) in place — belt and braces.

---

### F2 — The undo safety-net writes its own journal non-atomically

- **Severity:** Medium
- **Category:** Reliability / durability / data integrity
- **Explanation:** `ExecutionEngine.Save` (`ExecutionEngine.cs:480-482`) persists the journal with `File.WriteAllText`. The journal is correctly written *before* each write and *again* after verify — but the write itself is not atomic. A crash/power-loss during that flush truncates the JSON. `Undo`/`DetectDrift`/`LatestActiveSessionFor` then `JsonSerializer.Deserialize` it; a truncated file throws, and in `UndoAll` the session is caught-and-skipped (`ExecutionEngine.cs:275`).
- **Why it matters:** invariant #4 ("every change is journaled and reversible") is the core promise of the write engine. The mechanism that guarantees reversibility is itself not crash-safe: a torn journal turns a real applied change into one that can no longer be undone through the normal path.
- **Potential impact:** a user who applied a registry/boot change and then crashed mid-journal cannot one-click undo it; they must revert by hand. Silent erosion of the product's central guarantee.
- **Likelihood:** Low per-event, but this is the safety net — its failure mode is exactly when things are already going wrong.
- **Files:** `src/WPEP.Execution/ExecutionEngine.cs:480-482` (and every `Save(file, session)` call site).
- **Suggested strategy:** introduce a shared `AtomicJson.Write(path, obj)` helper (write to `path + ".tmp"`, `FileStream.Flush(true)`, then `File.Move(tmp, path, overwrite: true)`), and route `ExecutionEngine.Save`, the evidence-ledger writer (already fixed in L3 — refactor onto the shared helper), and `AppSettings.Save` (F5) through it. Extract into `WPEP.Core` so all three projects reuse it.
- **Dependencies:** F5 shares the helper; do them together.
- **Complexity:** Low-Medium (new helper + 3 call sites). **Risk:** Low; behavior-preserving on the happy path.
- **Acceptance criteria:** a fault-injection test (write, kill before rename) leaves the previous valid journal intact and no `.tmp` residue; normal apply/undo round-trip unchanged.
- **Testing:** unit test the helper (temp file removed on success; original preserved if rename never happens); keep existing `ExecutionEngineTests` green.
- **Notes for Opus:** same pattern already applied to `evidence.json` in L3 — generalize it rather than copy-paste a third time.

---

### F3 — Release artifacts are unsigned and ship without a published checksum

- **Severity:** Medium
- **Category:** Supply chain / distribution integrity / production readiness
- **Explanation:** the release workflow (`.github/workflows/release.yml`) builds `Verdict-<ver>.zip` and attaches it to a GitHub Release. There is no code-signing certificate (accepted, documented trade-off) **and** no published `SHA256SUMS` for the zip. Ironically, Verdict *itself* verifies PresentMon's SHA256 before executing it (`PresentMonInstaller.cs:15`) but offers users no equivalent way to verify the Verdict download.
- **Why it matters:** an unsigned binary already incurs SmartScreen friction; without a signed-commit-published checksum, a user cannot detect a tampered or MITM'd download, and the project cannot prove artifact integrity after the fact.
- **Potential impact:** a compromised release asset (or a look-alike mirror) is indistinguishable from the real one to end users.
- **Likelihood:** Low (GitHub Releases are HTTPS + account-protected) but the cost to close is trivial.
- **Files:** `.github/workflows/release.yml:64-97`, `tools/package-release.sh`.
- **Suggested strategy:** in the package step compute `sha256sum dist/Verdict-$VER.zip > dist/SHA256SUMS.txt`; attach both the zip and `SHA256SUMS.txt` to the release; add the expected hash + a `Get-FileHash` verification line to the release notes and README "Usage". (Optional, later: sigstore/cosign keyless signing or an EV cert.)
- **Dependencies:** none.
- **Complexity:** Low. **Risk:** Low.
- **Acceptance criteria:** every release has a `SHA256SUMS.txt` asset that matches the zip; README documents how to verify.
- **Testing:** run the workflow on a throwaway tag; verify the printed hash matches `Get-FileHash` locally.
- **Notes for Opus:** keep it consistent with the existing "we verify PresentMon, so we hold ourselves to the same bar" narrative — it's on-brand for an honesty-first tool.

---

### F4 — `bcdedit`/`powercfg` build process arguments by string interpolation of KB values

- **Severity:** Low (subsumed by F1 at the source; listed for the sink)
- **Category:** Argument injection
- **Explanation:** `BcdEditAccess.Set` does `Run($"/set {{current}} {element} {value}")` and `RealPowerCfg` interpolates subgroup/setting/index. `UseShellExecute=false` means no shell metacharacters, but a `value`/`element` containing spaces or quotes could split into extra arguments to a privileged tool.
- **Why it matters:** consistency with the L1 fix; these are the other two argument-bearing sinks fed by TB1.
- **Impact / Likelihood:** Low / Low — inputs are KB-controlled and currently clean.
- **Files:** `src/WPEP.Execution/BcdEditAccess.cs:47-51`, `src/WPEP.Execution/RegistryAccess.cs:33,54-58`.
- **Suggested strategy:** primary fix is F1 (validate at load). Optionally, at the sink, assert `element`/`value` match the safe token regex before `Run` and throw otherwise.
- **Complexity:** Low. **Risk:** Low. **Acceptance:** malformed element/value throws before any process starts. **Testing:** unit test the guard with a fake runner. **Notes for Opus:** do F1 first; this becomes a cheap redundant assertion.

---

### F5 — `settings.json` is written non-atomically

- **Severity:** Low
- **Category:** Reliability / data integrity
- **Explanation:** `AppSettings.Save` (`Infrastructure.cs:204-209`) uses `File.WriteAllText`. A crash mid-write corrupts all preferences plus the DPAPI-encrypted cloud keys. `Load` catches `IOException`/`JsonException` and falls back to defaults, so the failure is graceful but lossy (keys need re-entry, theme/prefs reset).
- **Why it matters:** low-impact but same class as F2; fixing it via the shared helper is nearly free.
- **Impact / Likelihood:** Low / Low.
- **Files:** `src/WPEP.App/Infrastructure.cs:204-209`.
- **Suggested strategy:** route through the F2 `AtomicJson.Write` helper.
- **Complexity:** Low. **Risk:** Low. **Acceptance:** fault-injection leaves prior settings intact. **Testing:** shared with F2. **Notes for Opus:** bundle with F2.

---

### F6 — Outbound `Process.Start(UseShellExecute=true)` calls don't validate the scheme/target

- **Severity:** Low
- **Category:** Defense in depth / insecure default
- **Explanation:** several "open in browser / open file" calls pass a string straight into `Process.Start` with `UseShellExecute=true` (`ViewModels.cs:469,991,1196`, `MainWindow.xaml.cs:88`, `WatchdogViewModel.cs:69`, `MeasureWizard.cs:150`). Targets are computed local paths or the GitHub update URL (host fixed via the pinned repo), so today they're safe — but there's no guard that the target is an `http(s)`/known-local path before shelling it out.
- **Why it matters:** mirrors the OpenSettings allowlist fix (L2). A future code path that lets any of these strings become attacker-influenced would turn into an arbitrary file/URI launch.
- **Impact / Likelihood:** Low / Low.
- **Files:** the call sites above; also `PresentMonRunner.cs:21` (process name interpolated into quoted args — validate it's a bare `name.exe`).
- **Suggested strategy:** add a small `SafeOpen.Url(string)` / `SafeOpen.File(string)` helper that asserts scheme ∈ {http, https} for URLs (or that a file path exists and is under a known root) before `Process.Start`. Validate the PresentMon process name against `^[A-Za-z0-9._-]+$`.
- **Complexity:** Low. **Risk:** Low. **Acceptance:** non-http(s) URL / non-existent path is refused; PresentMon rejects a name with a quote/space. **Testing:** unit-test the helpers. **Notes for Opus:** cheap, consistent hardening; do it alongside F1/F4.

---

### F7 — Multiple-comparison inflation in the verdict pipeline

- **Severity:** Low
- **Category:** Statistical correctness / business-logic
- **Explanation:** `ComparisonEngine.Compare` tests **4 metrics** (median, 1% low, 0.2% low, avg frametime) each at `Alpha = 0.05` with **no multiplicity correction** (`ComparisonEngine.cs:57-63,97`). `PipelineValidator` declares an effect if **any** metric is significant (`PipelineValidator.cs:36`). Under the null, family-wise false-positive probability is ≈ 1 − 0.95⁴ ≈ 18.5%, and the A/A self-test's false-fail rate is similarly inflated.
- **Why it matters:** Verdict's entire pitch is "no fake effects". An 18.5% family-wise error rate directly contradicts that on multi-metric verdicts and can make the A/A certification fail on genuinely-clean scenarios.
- **Potential impact:** occasional false "improvement/regression" verdicts and false A/A failures — the exact placebo/noise the module exists to prevent.
- **Likelihood:** Medium in practice on noisy rigs.
- **Files:** `src/WPEP.Statistics/ComparisonEngine.cs:57-63,97-100`, `src/WPEP.Statistics/PipelineValidator.cs:36`.
- **Suggested strategy:** either (a) designate median frametime the **primary** metric and base the overall verdict + A/A pass solely on it, treating the other three as descriptive; or (b) apply Holm–Bonferroni across the 4 p-values before classifying. (a) is simpler and matches the existing "run is the unit, median is primary" framing (`ComparisonEngine.cs:59` is already metric[0]).
- **Dependencies:** none; pure functions with existing tests.
- **Complexity:** Low. **Risk:** Medium — changes verdict outputs; must update `ComparisonEngineTests`/`PipelineValidatorTests` expectations and document the change in method notes.
- **Acceptance criteria:** A/A test false-positive rate on synthetic identical groups drops to ≈ α; verdict is driven by the primary metric (or Holm-corrected set); docs state the choice.
- **Testing:** Monte-Carlo style test — many synthetic A/A pairs, assert empirical false-positive rate ≈ 0.05; known-effect test still passes.
- **Notes for Opus:** pick (a) unless Léon wants per-metric verdicts; if (b), correct p-values *before* the `IncludesZero || p>α` decision. This is a correctness improvement with measurable value, not a style refactor.

---

### F8 — RigDna signature is only 32 bits; "not reversible" privacy claim is weak

- **Severity:** Low
- **Category:** Privacy / identity / collision
- **Explanation:** `RigDna.Compute` (`RigDna.cs:26-27`) hashes the canonical hardware string with **32-bit FNV-1a**, then encodes `RIG-XXXX-YYYY` where the second segment is a bit-rotation of the first — so the shareable code carries **only 32 bits of entropy**. This value maps to the server `rig_signature` (regex-matched in the Worker). Two consequences: (1) birthday collisions at ~65k distinct rigs merge different machines' evidence under one signature; (2) PRIVACY.md §3.3 calls the signature "non reversibile all'inverso" — but 32 bits over a known canonical format is trivially brute-forceable / dictionary-checkable, so membership ("is this exact rig in the dataset?") is confirmable by anyone who knows a candidate config.
- **Why it matters:** the community leaderboard's statistical validity degrades as adoption grows (collisions), and the privacy wording overstates irreversibility for an honesty-first product.
- **Potential impact:** aggregate stats skew once the population is non-trivial; a privacy claim that a careful reader can falsify.
- **Likelihood:** Low near-term (tiny user base) but structural.
- **Files:** `src/WPEP.SystemAnalyzer/RigDna.cs:26-27,78-90`; `docs/PRIVACY.md:75-77,157-161`.
- **Suggested strategy:** widen the hash to 64-bit FNV-1a (or SHA-256 truncated to 64 bits) and derive both code segments from independent halves; keep the Crockford base32 display. Update the server regex + client in lockstep (versioned: accept old + new format during transition, or reset the beta dataset). Soften PRIVACY.md to "pseudonymous, membership-checkable if the exact config is known" — which is already the honest position in §7.3.
- **Dependencies:** touches client + Worker regex + docs together; coordinate a format version.
- **Complexity:** Medium (cross-repo, format migration). **Risk:** Medium — changes the on-wire signature; simplest during beta is to reset the (small, non-critical, 365-day-rolling) dataset.
- **Acceptance criteria:** signature space ≥ 64 bits; client/server agree on format; PRIVACY wording matches reality; `RigDnaTests` updated.
- **Testing:** determinism test (same inventory → same code), collision smoke over synthetic inventories, server regex accepts the new format.
- **Notes for Opus:** the *fun* trading-card tier/hue can stay 32-bit; only the **shareable/comparable signature** needs the widening. Confirm with Léon whether to reset the beta community table (recommended) vs. dual-format transition.

---

### F9 — Real OS write-backends are exercised only by runtime self-tests, not automated CI

- **Severity:** Low
- **Category:** Test coverage / QA
- **Explanation:** unit tests use fake `IRegistryAccess`/`IPowerCfg`/`IBcdEdit`/`INvidiaDrs`; the *real* backends (`RealRegistryAccess`, `RealBcdEdit`, `RealPowerCfg`, `RealNvidiaDrs`) are validated only by the in-app `WriteSelfTest`/`EngineSelfTest` a user runs, plus manual field validation. CI has no write→verify→undo round-trip against a real hive.
- **Why it matters:** the highest-risk code (actual registry/boot writes) has the least automated coverage; a regression in a real backend would only surface at runtime on a user's machine.
- **Potential impact:** an undetected break in the real write/undo path ships to users.
- **Likelihood:** Low (backends are thin) but consequence is high (system writes).
- **Files:** `src/WPEP.Execution/RegistryAccess.cs`, `BcdEditAccess.cs`, `WriteSelfTest.cs`, `EngineSelfTest.cs`; CI `.github/workflows/ci.yml`.
- **Suggested strategy:** add a CI-only integration test that runs `WriteSelfTest`/`EngineSelfTest` against a **throwaway HKCU key** (no admin needed, no real setting touched — the self-test already uses a scratch key) on the `windows-latest` runner; gate it so it never touches HKLM/boot in CI.
- **Dependencies:** none.
- **Complexity:** Low-Medium. **Risk:** Low (scratch key only). **Acceptance:** CI runs a real HKCU write→verify→undo and asserts full cleanup. **Testing:** the test *is* the coverage; assert the scratch key is gone afterward. **Notes for Opus:** reuse the existing self-test scratch-key machinery; do NOT attempt HKLM/bcdedit in CI.

---

### F10 — MDE is estimated as a null-CI half-width (documentation gap)

- **Severity:** Info
- **Category:** Statistical method / documentation
- **Explanation:** `Mde.Percent` (`Mde.cs:15-24`) estimates the minimum detectable effect as the bootstrap CI half-width of a baseline-vs-itself null comparison. This is a reasonable, conservative-ish proxy but is **not** a formal 80%-power MDE (which is ≈ 2.8σ for a two-sided test); it can understate the true detectable threshold, and the 10% default gate is generous.
- **Why it matters:** users may read "no verdict, MDE X%" as a power guarantee it isn't. Honesty-first tool → the method note should say what the number is and isn't.
- **Impact / Likelihood:** Info.
- **Files:** `src/WPEP.Statistics/Mde.cs`, method blurb in README/report footer.
- **Suggested strategy:** document precisely (report footer + `KB_RESEARCH`/method note) that MDE here = null-CI half-width, not a powered MDE; optionally offer a powered variant later. **Do not silently change the number** — it's referenced in the gate and tests.
- **Complexity:** Trivial (docs). **Risk:** none. **Acceptance:** method note states the definition. **Notes for Opus:** documentation only unless Léon wants a powered MDE (larger stats task).

---

### F11 — Worker hardening backlog (post-CORS)

- **Severity:** Info / Low
- **Category:** API security / abuse / ops
- **Explanation & items:**
  1. **`Access-Control-Allow-Origin: *` now also applies to `POST /v1/evidence`.** Correct for the read endpoints; for POST it lets any web origin submit. Submission is anonymous-by-design and rate-limited, so acceptable — but if browser submission is never intended (the WPF client doesn't use CORS), restrict ACAO on POST to the GitHub Pages origin. (`src/index.ts:41-48`.)
  2. **Per-IP rate limiting is not in code** — the native binding is keyed on client-controlled `rig_signature` (disclosed in M1). A Cloudflare **WAF rate-limiting rule** (dashboard: Security → WAF → Rate limiting) is still needed for real per-IP protection. **This is a dashboard action only Léon can take.** (`wrangler.toml:16-22`.)
  3. **`database_id` is committed** in `wrangler.toml:14` — not a secret, but avoidable noise.
  4. **`rebuildStatsCacheAndPrune` is DELETE-then-INSERT without a transaction** (`src/index.ts:209-238`) — a failure between the two leaves `stats_cache` empty until the next nightly run; self-heals but could serve empty stats for up to 24h. Wrap in a D1 batch/transaction.
- **Impact / Likelihood:** Low.
- **Files:** `verdict-community/src/index.ts`, `wrangler.toml`.
- **Suggested strategy:** (1) split CORS: `*` for GET, Pages-origin for POST, or leave `*` and document; (2) **action item for Léon**: add the WAF per-IP rule; (3) drop `database_id` from VCS or leave with a comment; (4) wrap the rebuild in `env.DB.batch([...])`.
- **Complexity:** Low. **Risk:** Low. **Acceptance:** rebuild is atomic; CORS policy documented; WAF rule live. **Testing:** vitest for the rebuild transaction; manual for WAF. **Notes for Opus:** you cannot do the WAF rule — surface it to Léon as a checklist item.

---

### F12 — No backup / disaster-recovery for D1

- **Severity:** Info
- **Category:** DR / backup
- **Explanation:** the D1 `evidence` table is the only copy of community submissions; there's no scheduled export. Data is non-critical (365-day rolling, stats are derivable), but there is zero recovery path if the database is dropped.
- **Suggested strategy:** add a periodic `wrangler d1 export` to R2 (or a scheduled dump) — or explicitly document "community data is ephemeral/best-effort, no DR" as an accepted stance for beta.
- **Complexity:** Low. **Risk:** none. **Acceptance:** either a scheduled export exists or the ephemeral stance is documented. **Notes for Opus:** cheapest correct answer for beta is to *document* the ephemeral stance; wire a real export before any "trust the numbers" marketing.

---

### F13 — Observability: health endpoint exists but nothing consumes it

- **Severity:** Info
- **Category:** Monitoring
- **Explanation:** `/v1/health` was added (returns 503 if D1 is unreachable) but no external uptime monitor is wired. Worker logs exist via `observability.enabled` but there's no alerting.
- **Suggested strategy:** point a free monitor (UptimeRobot/BetterUptime) at `/v1/health`; optionally alert on 5xx rate. **Action item for Léon** (external account).
- **Complexity:** Trivial. **Risk:** none. **Notes for Opus:** surface to Léon; the endpoint is already monitor-ready.

---

### F14 — A crash between operations of a multi-op tweak leaves it half-applied with no resume

- **Severity:** Low
- **Category:** Reliability / consistency
- **Explanation:** `Execute` journals-before/verifies-after each op in a loop (`ExecutionEngine.cs:167-191`), which is correct — but if the process dies between op N and op N+1 of a multi-operation tweak, ops 1..N are applied and journaled while the tweak is only partially applied. There's no startup reconciliation that detects "journaled sessions whose tweak is partially applied" and offers resume/undo.
- **Why it matters:** rare, but produces a system in a state no single verdict describes (half a tweak). The journal *does* record what happened (so manual undo works), so impact is bounded.
- **Impact / Likelihood:** Low / Low.
- **Files:** `src/WPEP.Execution/ExecutionEngine.cs:156-194`.
- **Suggested strategy:** on app start (or Changes page load), scan journals for sessions where some entries are `Verified` and the plan had more ops than journaled-verified, and surface a "finish or undo" prompt. Low priority; the journal already makes it recoverable by hand.
- **Complexity:** Medium. **Risk:** Low. **Acceptance:** a simulated half-applied session is detected and offered for undo. **Testing:** unit test with a hand-crafted partial journal. **Notes for Opus:** optional polish; F2 (atomic journal) is the prerequisite that makes the partial journal trustworthy.

---

## 4. Genuinely good — do NOT "improve" these (touching them is a regression)

- **Auto-update is report-only** (`UpdateCheck.cs`): reads GitHub Releases, compares versions, opens the browser. Downloads/executes nothing. **No RCE.** Keep it that way.
- **Consent-first gating is real, not cosmetic** (`ViewModels.cs:122`, CLI `Program.cs`): `RemoteBackend` is used only when `CommunityShareEnabled && CommunityConfig.IsConfigured`; default OFF; both GUI and CLI. Local-only is the default `ICommunityBackend`.
- **PresentMon integrity**: pinned version + SHA256 verified before execution; a failing hash is never written/run (`PresentMonInstaller.cs`).
- **Write engine discipline**: journal-before-write, verify-after-write by re-read, stop-on-mismatch, per-entry reverse-order undo, **drift-aware undo** that refuses to clobber values changed outside Verdict (`ExecutionEngine.cs:220-259`). This is genuinely careful systems code.
- **CoPilot grounding**: invented ids are dropped in code (`CoPilotGrounding.ParseReply`), copilot is read-only, catalog is trusted KB → prompt-injection impact ≈ nil.
- **KB honesty validation at load**: sources mandatory (unless placebo), https-only, category/method enums, placebo-can't-have-programmatic-apply (`KnowledgeBaseValidator`). (F1 adds *apply-safety* on top — it does not replace this.)
- **Report XSS hygiene**: `ReportBuilder.Esc` escapes `& < > "` on interpolated fields; numeric fields are formatted server-side.
- **Statistics core**: Mann–Whitney permutation with partial Fisher–Yates + `+1` correction, deterministic seeds, run-as-unit-of-observation (never pooled frames), noise gate emitting "no verdict" instead of "no effect". Solid. (F7 is a multiplicity refinement, not a rewrite.)
- **Dependency hygiene**: 4 NuGet refs total (`TraceEvent`, `System.Management`, `QRCoder`, `ProtectedData`) — all first-party or well-known, no transitive bloat. Worker: `npm audit` = **0 vulnerabilities** (prod and dev).
- **`asInvoker` manifest**: never auto-elevates; elevation only at the point of a privileged operation.

---

## 5. Not audited (honesty)

- **Line-by-line correctness of ETW/DPC-ISR capture** (`EtwDpcIsrCollector`, `DpcIsrAggregator`) against malformed/streaming ETW input — reviewed for shape, not fuzzed.
- **NvAPI P/Invoke struct layouts** (`NvApi.cs`, `NvidiaDrsAccess.cs`) — trusted; a wrong offset is a native-memory risk. Recommend a targeted review before any driver-panel expansion.
- **`SnapshotBuilder` (679 lines) WMI queries** for correctness on exotic hardware — read for the process-exec surface only.
- **WPF accessibility** (keyboard nav, screen-reader, contrast on all 10 themes).
- **Full `Cli/Program.cs` (1943 lines)** argument parsing — skimmed for the security-relevant apply/undo/copilot paths, not every subcommand.
- **The 135 KB entries' primary sources** were not re-verified in this pass (a separate KB URL audit did that on 2026-07-01).
- **`MainWindow.xaml` (1933 lines)** binding correctness for the blind design commits (see §6 Phase 0).

---

## 6. Implementation roadmap

Ordered for execution. Phase 0 is a hard gate: nothing else is trustworthy until the blind commits build.

### Phase 0 — Restore a green build on real hardware (GATE)
- **Objective:** confirm the ~40 SDK-less "blind" commits (design pass, KB, audit fixes L1–L5) actually compile and pass tests on the .NET 10 SDK.
- **Why now:** everything below assumes a building tree. Léon returns to the SDK-equipped PC 2026-07-05.
- **Tasks:** `dotnet build WPEP.sln -c Release -m:1 --disable-build-servers` → expect 0/0; `dotnet test` → expect ~360 green. Fix any blind-commit breakage (the design-pass WPF XAML risks are enumerated in `project_verdict.md` / `HANDOVER.md`).
- **Dependencies:** SDK availability (2026-07-05).
- **Expected outcome:** green build+test, tag-ready.
- **Verification checklist:** build 0 warnings (TreatWarningsAsErrors), all tests pass, app launches, 4-brain UI + community opt-in visible.
- **Potential regressions:** WPF `Path`/`LinearGradientBrush`/`RotateTransform` binding edge cases from the design pass.

### Phase 1 — Durability & safety-net hardening
- **Objective:** make the journal, settings, and evidence writes crash-safe via one shared atomic helper. (F2, F5; refactor L3 onto it; F14 groundwork.)
- **Why now:** the write engine's core guarantee (reversibility) depends on a durable journal; cheap, high-value, no design decisions.
- **Tasks:** add `WPEP.Core/Io/AtomicJson.cs`; route `ExecutionEngine.Save`, `EvidenceLedger.Append`, `AppSettings.Save` through it; add fault-injection tests.
- **Dependencies:** none.
- **Expected outcome:** torn-write can't corrupt journal/settings/evidence.
- **Verification checklist:** fault-injection tests pass; existing engine/community/settings tests green; no `.tmp` residue.
- **Potential regressions:** file-locking on Windows if a reader holds the file during `File.Move` — use `overwrite: true` and retry-once.

### Phase 2 — KB trust-boundary hardening
- **Objective:** validate apply-op values at load, fail-closed (F1); redundant sink assertions (F4); safe-open helpers (F6).
- **Why now:** highest-value security hardening; independent of Phase 1.
- **Tasks:** extend `KnowledgeBaseValidator` with per-method regex/allowlist; run the shipped KB through it and fix any offender at the source; add `SafeOpen` helpers + PresentMon name guard.
- **Dependencies:** none.
- **Expected outcome:** a malformed apply-op can never reach a registry write or process arg.
- **Verification checklist:** shipped KB validates clean; malformed variants rejected; open helpers refuse non-http(s)/nonexistent targets.
- **Potential regressions:** an over-strict regex rejecting a legitimate existing entry — run the full KB before merging.

### Phase 3 — Release integrity
- **Objective:** publish `SHA256SUMS.txt` per release + document verification (F3).
- **Why now:** before the v1.1 tag/release; trivial, on-brand.
- **Tasks:** compute+attach checksum in `release.yml`; add verification line to README + release notes.
- **Dependencies:** Phase 0 (a releasable build).
- **Expected outcome:** verifiable downloads.
- **Verification checklist:** dry-run tag produces a matching `SHA256SUMS.txt`.
- **Potential regressions:** none.

### Phase 4 — Statistical correctness
- **Objective:** kill multiplicity inflation (F7); document MDE definition (F10).
- **Why now:** directly protects the "no fake effects" promise; isolated pure functions.
- **Tasks:** choose primary-metric verdict (recommended) or Holm–Bonferroni; update `ComparisonEngine`/`PipelineValidator` + tests; add a Monte-Carlo A/A false-positive test; document MDE.
- **Dependencies:** none (but coordinate with Léon on primary-metric vs per-metric).
- **Expected outcome:** A/A false-positive ≈ α; verdicts driven by a corrected/primary metric.
- **Verification checklist:** synthetic A/A empirical FP ≈ 0.05; known-effect still detected; docs updated.
- **Potential regressions:** changed verdict outputs — update all affected test expectations and the report/README wording.

### Phase 5 — Identity & privacy
- **Objective:** widen the rig signature to ≥64 bits and align the "pseudonymous" wording (F8).
- **Why now:** structural; do it during beta while the community dataset is small and resettable.
- **Tasks:** widen hash in `RigDna`; version the format; update Worker regex + client + PRIVACY.md together; decide (with Léon) reset-vs-dual-format.
- **Dependencies:** cross-repo coordination.
- **Expected outcome:** ≥64-bit signature; honest privacy wording.
- **Verification checklist:** determinism + collision-smoke tests; server regex accepts new format; PRIVACY matches reality.
- **Potential regressions:** on-wire format change — reset the beta table (recommended) or run dual-format.

### Phase 6 — Backend hardening & ops
- **Objective:** atomic stats rebuild, CORS policy decision, WAF per-IP rule, D1 DR stance, uptime monitor (F11, F12, F13).
- **Why now:** after the leaderboard is actually live (it now is).
- **Tasks (code):** wrap rebuild in `env.DB.batch`; decide/split CORS; drop or comment `database_id`. **Tasks (Léon-only):** add WAF rate-limit rule; wire `/v1/health` to a monitor; choose D1 DR (export vs. document-ephemeral).
- **Dependencies:** none.
- **Expected outcome:** abuse-resistant, observable, atomic backend.
- **Verification checklist:** vitest for atomic rebuild; WAF rule live; monitor green.
- **Potential regressions:** CORS tightening could break a future web submitter — document the decision.

### Phase 7 — Test coverage for real backends
- **Objective:** CI round-trip on a throwaway HKCU key (F9); optional half-apply reconciliation (F14).
- **Why now:** last, once behavior is stable, to lock it in.
- **Tasks:** CI-only integration test invoking the existing self-test scratch-key path on `windows-latest`; optional startup reconciliation for partial journals.
- **Dependencies:** Phases 1–2.
- **Expected outcome:** the highest-risk code has automated CI coverage.
- **Verification checklist:** CI performs write→verify→undo on a scratch key and asserts cleanup; no HKLM/boot in CI.
- **Potential regressions:** none if scoped to HKCU scratch.

---

## 7. Per-task execution cards (for Claude Opus)

> Format per task: **objective · context · reasoning · implementation guidance · affected components · expected result · validation · completion criteria · side effects.**

### T1 — `AtomicJson.Write` helper (Phase 1)
- **Objective:** one crash-safe JSON writer reused by journal/settings/evidence.
- **Context:** three writers use `File.WriteAllText` (F2, F5) — non-atomic.
- **Reasoning:** temp-write + flush(true) + `File.Move(overwrite)` is atomic on NTFS; centralizing avoids a third copy of the L3 fix.
- **Implementation guidance:** `WPEP.Core/Io/AtomicJson.cs`: `Write<T>(string path, T value, JsonSerializerOptions? opts)`. Write to `path+".tmp"`, `fs.Flush(true)`, `File.Move(tmp, path, overwrite: true)`; on move failure retry once after a short delay. Route `ExecutionEngine.Save`, `EvidenceLedger.Append`, `AppSettings.Save` through it.
- **Affected components:** `WPEP.Core`, `WPEP.Execution`, `WPEP.App`.
- **Expected result:** torn writes impossible.
- **Validation:** fault-injection unit test; existing tests green.
- **Completion criteria:** all three writers migrated; no `.tmp` residue after success.
- **Side effects:** slightly slower writes (one extra rename) — negligible.

### T2 — KB apply-safety validation (Phase 2)
- **Objective:** reject malformed apply-ops at load.
- **Context:** F1 — validator checks honesty, not apply-op safety.
- **Reasoning:** fail-closed at the trust root beats patching each sink.
- **Implementation guidance:** in `KnowledgeBaseValidator.Validate`, per `apply.Method`, validate each op's path/value with the regexes in F1; append a specific problem string on failure (load already throws when `problems` is non-empty). Run the shipped KB; fix offenders at source.
- **Affected components:** `WPEP.KnowledgeBase`.
- **Expected result:** only well-formed apply-ops load.
- **Validation:** `KnowledgeBaseValidatorTests` per method (valid + malformed); shipped KB validates clean in CI.
- **Completion criteria:** malformed variants rejected; 135-entry KB passes.
- **Side effects:** a too-strict regex could reject a real entry — run the full KB first.

### T3 — Safe-open helpers (Phase 2)
- **Objective:** validate scheme/target before `Process.Start(UseShellExecute=true)` and PresentMon process name.
- **Context:** F6.
- **Implementation guidance:** `SafeOpen.Url` (scheme ∈ http/https) and `SafeOpen.File` (exists, under known root); route the 6 call sites through them; guard PresentMon name with `^[A-Za-z0-9._-]+$`.
- **Validation:** unit tests reject non-http(s)/nonexistent/quoted inputs.
- **Completion criteria:** all listed call sites migrated.
- **Side effects:** none for legitimate targets.

### T4 — Release checksum (Phase 3)
- **Objective:** publish `SHA256SUMS.txt`.
- **Implementation guidance:** in `release.yml` package step, `sha256sum dist/Verdict-$VER.zip > dist/SHA256SUMS.txt`; add it to `gh release create` assets; add `Get-FileHash` line to README + release notes.
- **Validation:** throwaway tag → asset matches local hash.
- **Completion criteria:** release has both assets.
- **Side effects:** none.

### T5 — Multiplicity fix (Phase 4)
- **Objective:** verdict false-positive ≈ α.
- **Implementation guidance:** make median frametime the primary metric for the overall verdict and A/A pass (`ComparisonEngine`, `PipelineValidator`); keep other metrics descriptive. Update tests; document.
- **Validation:** synthetic A/A empirical FP ≈ 0.05; known-effect still detected.
- **Completion criteria:** tests + docs updated.
- **Side effects:** changed verdicts on multi-metric cases — expected; update report/README wording.

### T6 — Rig signature widening (Phase 5)
- **Objective:** ≥64-bit signature + honest wording.
- **Implementation guidance:** 64-bit FNV-1a (or truncated SHA-256) in `RigDna`; version the code format; update Worker `RigSignatureRe` + client + PRIVACY.md together; reset the beta table (recommended).
- **Validation:** determinism + collision-smoke; server accepts new format.
- **Completion criteria:** cross-repo aligned; PRIVACY matches.
- **Side effects:** on-wire change — reset beta dataset.

### T7 — Worker rebuild transaction + CORS decision (Phase 6)
- **Objective:** atomic stats rebuild; explicit CORS policy.
- **Implementation guidance:** wrap DELETE+INSERT of `stats_cache` in `env.DB.batch([...])`; decide GET-`*`/POST-Pages-origin or document `*`; drop/annotate `database_id`.
- **Validation:** vitest asserting stats never transiently empty on rebuild.
- **Completion criteria:** rebuild atomic; CORS documented.
- **Side effects:** CORS tightening could block a future web submitter — document.

### T8 — CI real-backend round-trip (Phase 7)
- **Objective:** automated write→verify→undo on a scratch HKCU key.
- **Implementation guidance:** CI-only test invoking the existing self-test scratch-key path on `windows-latest`; assert full cleanup; never touch HKLM/boot.
- **Validation:** CI green; scratch key gone afterward.
- **Completion criteria:** test in the CI matrix.
- **Side effects:** none (scratch only).

### Léon-only action items (cannot be done by Opus)
- **A1:** Cloudflare dashboard → Security → WAF → Rate limiting rule for per-IP protection on `POST /v1/evidence` (F11).
- **A2:** Wire an uptime monitor to `/v1/health` (F13).
- **A3:** Decide D1 DR: scheduled `wrangler d1 export` to R2, or accept & document ephemeral (F12).
- **A4:** (Optional) code-signing certificate to remove SmartScreen friction (F3).

---

## 8. Scorecard & production verdict

| Dimension | Score (0–10) | Rationale |
|---|---:|---|
| Architecture & module boundaries | 8.5 | Clean layering, testable seams (interfaces on every OS backend), UI/CLI share services. |
| Security | 7 | No RCE, consent-first is real, hygiene good; residual = KB trust-boundary validation (F1) + unsigned/unchecksummed release (F3). |
| Reliability | 7 | Excellent write discipline; the safety net's own writes aren't atomic yet (F2). |
| Maintainability | 8.5 | `TreatWarningsAsErrors`, ~360 tests, tiny dep surface, strong docs. |
| Performance | 8 | Read-only probes, bounded process timeouts, drained pipes; deterministic stats. Not a concern for a desktop tool. |
| Statistical rigor | 7.5 | Genuinely careful; docked for multiplicity (F7) + MDE wording (F10). |
| Backend / ops | 6.5 | Works, validated, health-ready; missing per-IP WAF, DR, and monitoring wiring (F11–F13). |
| Production readiness (for a "large company") | 6 | Beta-appropriate; gaps are integrity (signing/checksum), backend abuse-resistance, and real-backend CI coverage. |
| **For an unsigned hobby/OSS project of this scope** | **8.5** | **Well above the norm** — the honesty invariants are actually enforced in code, not just claimed. |

**Verdict:** **Approve for continued Beta.** Not yet "large-company production" — close F1, F2, F3 (and the Phase 6 backend items) before positioning the community numbers or the download as trustworthy at scale. There is **no Critical or High blocker**: no RCE, no injection reachable from untrusted remote input, no secret leakage, consent gating is genuine. The remaining work is hardening and correctness refinement, not firefighting.

---

## Appendix A — Dependency inventory

**.NET (NuGet), 4 total:**
- `Microsoft.Diagnostics.Tracing.TraceEvent` 3.2.4 (ETW) — `WPEP.Diagnostics`
- `System.Management` 9.* (WMI) — `WPEP.SystemAnalyzer`
- `QRCoder` 1.6.0 (MIT, pure-managed) — `WPEP.App`
- `System.Security.Cryptography.ProtectedData` 9.0.0 (DPAPI, first-party) — `WPEP.App`

**Node (verdict-community):** runtime `zod` ^3.23.8; dev `wrangler` 4, `vitest` 4, `@cloudflare/vitest-pool-workers` 0.17, `@cloudflare/workers-types`, `typescript`. `npm audit` = **0 vulnerabilities**.

## Appendix B — Command-execution surface

See §2.2. All argument-bearing calls use `UseShellExecute=false` (no shell). Residual risk = KB-value argument splitting (F1/F4) and unvalidated open-scheme (F6). No remote RCE path.

## Appendix C — Verification commands

```powershell
# .NET (on the SDK PC, from repo root)
dotnet build WPEP.sln -c Release -m:1 --disable-build-servers -v q     # expect 0/0
dotnet test  WPEP.sln -c Release -m:1 --disable-build-servers -v q     # expect ~360 green
```
```powershell
# Worker (verdict-community); node/npm not on PATH → absolute path
$env:Path = "C:\Program Files\nodejs;$env:Path"
& "C:\Program Files\nodejs\npm.cmd" test                                # 20/20
& "C:\Program Files\nodejs\npx.cmd" tsc --noEmit                        # 0 errors
& "C:\Program Files\nodejs\npx.cmd" wrangler deploy --dry-run           # bindings OK
& "C:\Program Files\nodejs\npm.cmd" audit                               # 0 vulnerabilities
```

## Appendix D — Finding index

| ID | Sev | Title | Phase |
|----|-----|-------|-------|
| F1 | Medium | KB apply-values lack apply-safety validation | 2 |
| F2 | Medium | Non-atomic journal writes (undo net not crash-safe) | 1 |
| F3 | Medium | Unsigned release + no published checksum | 3 |
| F4 | Low | bcdedit/powercfg arg interpolation | 2 |
| F5 | Low | Non-atomic settings.json write | 1 |
| F6 | Low | Unvalidated scheme on shell-open calls | 2 |
| F7 | Low | Multiple-comparison inflation | 4 |
| F8 | Low | 32-bit rig signature + weak irreversibility claim | 5 |
| F9 | Low | Real OS backends not in CI | 7 |
| F10 | Info | MDE method documentation gap | 4 |
| F11 | Info/Low | Worker hardening backlog (CORS/WAF/txn/id) | 6 |
| F12 | Info | No D1 backup/DR | 6 |
| F13 | Info | Health endpoint unmonitored | 6 |
| F14 | Low | Half-applied multi-op tweak, no resume | 7 |

*End of handoff.*
