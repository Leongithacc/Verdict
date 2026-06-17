using WPEP.KnowledgeBase;

namespace WPEP.Execution;

/// <summary>What an apply request should do, once the plan's live state is known.
/// The terminal decision of the apply flow, factored out so it is unit-testable and
/// identical across CLI and UI.</summary>
public enum ApplyAction
{
    /// <summary>Not a programmatically-applicable entry (gui-only or placebo).</summary>
    NotApplicable,
    /// <summary>Every operation is already at its target — nothing to write.</summary>
    AlreadyApplied,
    /// <summary>A real write is needed but the process isn't elevated (HKLM/boot).</summary>
    NeedsAdmin,
    /// <summary>Caller hasn't confirmed: show the dry-run, write nothing.</summary>
    DryRun,
    /// <summary>Go ahead and execute.</summary>
    Execute,
}

/// <summary>Single source of truth for "can this be applied, and does it need admin".
/// Mirrored nowhere else — CLI and UI both call here so the rules can't drift apart.</summary>
public static class ApplyPolicy
{
    /// <summary>A KB entry Verdict can write programmatically: a supported method and
    /// never a placebo (applying a placebo is refused by design).</summary>
    public static bool CanApply(TweakEntry e) =>
        e.Apply is { Method: "registry" or "powercfg" or "powercfg-value" or "bcdedit" } &&
        e.EvidenceLevel != EvidenceLevel.Placebo;

    /// <summary>bcdedit always writes the boot store; HKLM registry writes need admin too.</summary>
    public static bool NeedsAdmin(TweakEntry e) =>
        e.Apply?.Method == "bcdedit" ||
        (e.Apply?.Operations.Any(o =>
            o.Path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
            o.Path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) ?? false);

    /// <summary>The terminal decision. Precedence: not-applicable → already-applied →
    /// (no confirm) dry-run → needs-admin → execute. "Already applied" wins over the
    /// admin gate on purpose: reporting "nothing to do" never requires elevation.</summary>
    public static ApplyAction DecideAction(
        bool canApply, bool isAlreadyApplied, bool needsAdmin, bool elevated, bool confirmYes)
    {
        if (!canApply) return ApplyAction.NotApplicable;
        if (isAlreadyApplied) return ApplyAction.AlreadyApplied;
        if (!confirmYes) return ApplyAction.DryRun;
        if (needsAdmin && !elevated) return ApplyAction.NeedsAdmin;
        return ApplyAction.Execute;
    }
}
