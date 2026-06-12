# Pre-publication checklist (before the public GitHub repo)

Scan run 2026-06-12: source code, KB and tests contain NO personal data.
Personal references exist ONLY in `docs/WPEP_docs_handoff/` (design-session
records naming the owner, his school laptop, etc.).

Before publishing:
- [ ] Decide the public name (owner's call) and rename solution/namespaces if needed
- [ ] Create the public repo from a CLEAN export: everything EXCEPT
      `docs/WPEP_docs_handoff/` (or publish sanitized copies — the design
      decisions are worth sharing, the personal context is not)
- [ ] Re-run the scan on the export: `git ls-files | xargs grep -ilE "leon|schindler|bxtool"`
- [ ] Verify no `runs/`, `reports/`, `data/` or personal snapshots are tracked
- [ ] README screenshots: generate on a neutral profile (no personal usernames visible)
- [ ] Release zip + SHA256 published alongside; SmartScreen expectations
      documented ("Why does Windows warn about WPEP?")
- [ ] LICENSE (MIT) + CONTRIBUTING.md present — already done
