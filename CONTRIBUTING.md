# Contributing to WPEP

One rule above all others:

**Every new Knowledge Base entry requires a primary source** (vendor documentation:
Microsoft Learn, NVIDIA, AMD, Epic, …). No primary source → the evidence grading
cannot be better than `controversial`. "A YouTuber said so" is not a source.
Prefer grading something `placebo` over inventing evidence.

Other non-negotiables (see `docs/WPEP_docs_handoff/` for the full design record):

- V1 is read-only. No code that writes to the user's system.
- No claim of measuring end-to-end input latency (impossible in pure software).
- No improvement claims without statistics: run as the unit of observation,
  Mann–Whitney + bootstrap CI, noise gate. "No measurable effect" is a valid
  and frequent result — the tool must be willing to say it.
- No in-game overlay, ever (anti-cheat scope decision).
- Portable leave-no-trace: nothing outside the app folder.

Build: .NET 10 SDK → `dotnet build` / `dotnet test`. All tests must pass.
