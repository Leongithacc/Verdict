# Changelog

All notable changes to Verdict are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **AI co-pilot — 4 swappable brains**: Ollama (local, default), Anthropic Claude,
  Google Gemini, OpenAI GPT. Cloud API keys encrypted at rest with DPAPI.
  Selectable from Settings page and CLI `wpep copilot --brain ...`.
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
  `AppVersion.Current` and `tools/package-release.sh`.
- **Design token `OnAccent`**: extracted `#0F0F14` from 3 hardcoded sites in
  `MainWindow.xaml` into a proper theme resource. Reduces design-system debt
  for the next theme pass.
- Knowledge Base now has **130 entries** (was 122).
- Documentation: [docs/V7_REMOTE_BACKEND_DESIGN.md](docs/V7_REMOTE_BACKEND_DESIGN.md)
  (14 sections) and [docs/RELEASE_V1.1_RUNBOOK.md](docs/RELEASE_V1.1_RUNBOOK.md)
  (12 sections, includes the new automatic Actions flow).

### Changed
- `CommunityService.Record` now also fires-and-forgets `SubmitAsync` to the
  configured backend (no-op on `LocalOnlyBackend`); caller no longer needs to
  invoke the backend directly.
- `HANDOVER.md` updated: closed the `ApplyDialog` cleanup item (already done
  in commit `1bd1fe5`) and the V7 design item (now spec'd in full).

### Internal
- 9 new smoke tests in `CoPilotTests` (3 per cloud brain) — no network calls.
- DPAPI key encrypt/decrypt extracted to shared helpers in `AppSettings`
  (DRY across Claude / Gemini / OpenAI keys).

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
