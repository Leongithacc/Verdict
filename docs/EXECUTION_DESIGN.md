# Execution Engine — implementation design (V2)

*Drafted 2026-06-12, after machine-1 certification (A/A + known-effect passed).
Implements `docs/WPEP_docs_handoff/EXECUTION_ENGINE_V2.md`. Code does NOT ship
until the remaining gates close: second machine + public open-source repo.*

## Module layout

```
WPEP.Execution            // new project, referenced ONLY by UI/CLI entry points
├── ExecutionPlan.cs      // dry-run: resolved operations with before-values
├── Journal.cs            // undo-journal: data/journal/*.json, append-only
├── Executors/
│   ├── RegistryExecutor.cs   // read-before → write → verify-after
│   ├── PowercfgExecutor.cs   // powercfg /setactive etc.
│   └── ServiceExecutor.cs    // service start mode changes
└── RestorePoint.cs       // System Restore checkpoint before each session
```

Hard rules carried from the spec:
1. The engine executes ONLY `apply` specs from KB entries. No free-form writes.
2. `placebo` entries are not applicable, `risky` ones need per-item confirmation
   with the risk text. No "optimize all" button — selection is explicit.
3. Flow per session: build ExecutionPlan (dry-run, shows exact before→after) →
   user consents → restore point → per-tweak: journal-write(before) → write →
   verify-after-write (re-read, compare) → on mismatch STOP the session.
4. Every UI surface switches to the explicit Execution mode: terminal line goes
   from `0 writes` to `N writes · journaled`.
5. After applying, the app offers the Measure flow immediately: the claim stays
   "we prove the effect", even when we applied the change ourselves.

## Journal format (data/journal/<timestamp>.json)

```jsonc
{
  "session": "2026-06-20T18:00:00Z",
  "restore_point": true,
  "entries": [{
    "tweak_id": "disable-gamedvr-background-recording",
    "operation": { "path": "HKCU\\...\\GameDVR\\AppCaptureEnabled" },
    "value_before": "1",        // null = key did not exist (undo = delete)
    "value_after": "0",
    "verified": true,
    "applied_at": "..."
  }]
}
```

Undo = walk entries in reverse, restore `value_before` (or delete if it did not
exist), verify each restore, mark the journal entry undone. Idempotent: undoing
an already-undone entry is a no-op with a notice.

## Dry-run UI (the consent screen)

One table, no prose: tweak · operation path · current value (read live) · value
after · reboot needed? Anything unreadable → that tweak is blocked from the
session (consistent with "never write on uncertain state").

## What stays gui-only forever

In-game settings (`fn-*`), BIOS (XMP/EXPO, Resizable BAR), driver-panel options,
physical changes (ethernet). The product remains apply+guide hybrid; the "Apply"
button space shows "How to" for these — same layout, honest capability.
