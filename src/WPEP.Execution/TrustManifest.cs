using WPEP.KnowledgeBase;

namespace WPEP.Execution;

/// <summary>One exact operation Verdict would perform — the atom of the Trust manifest.</summary>
public sealed record ChangeOperation(
    string Method, string Target, string NewValue, string Kind,
    bool NeedsAdmin, bool Reversible, bool RequiresReboot);

/// <summary>Everything one tweak would touch, listed precisely.</summary>
public sealed record TrustEntry(string TweakId, string TweakName, IReadOnlyList<ChangeOperation> Operations);

/// <summary>Trust mode (Lab feature): a security-review-style manifest of EXACTLY what Verdict
/// would change — every registry path, value, method, whether it needs admin, whether it's
/// reversible. Built statically from the KB apply specs: NO system reads, NO writes. For the
/// paranoid who want to see the diff before trusting the tool. On-brand: total transparency.</summary>
public static class TrustManifest
{
    /// <summary>Builds the manifest for the given tweaks. Only applicable ones (real write method,
    /// non-placebo) contribute — gui-only/placebo entries have nothing to disclose here.</summary>
    public static IReadOnlyList<TrustEntry> Build(IEnumerable<TweakEntry> tweaks)
    {
        var entries = new List<TrustEntry>();
        foreach (var e in tweaks)
        {
            if (!ApplyPolicy.CanApply(e) || e.Apply is not { } spec)
                continue;

            bool needsAdmin = ApplyPolicy.NeedsAdmin(e);
            bool reversible = !string.IsNullOrWhiteSpace(spec.Undo) && spec.Undo != "none";

            var ops = spec.Operations.Select(o => new ChangeOperation(
                Method: spec.Method,
                Target: o.Path,
                NewValue: o.ValueAfter ?? "(impostato)",
                Kind: o.Kind,
                NeedsAdmin: needsAdmin,
                Reversible: reversible,
                RequiresReboot: spec.RequiresReboot)).ToList();

            // Some methods (e.g. powercfg plan switch) carry the action in the method, not in a
            // path-based operation; surface a single line so nothing is ever silently hidden.
            if (ops.Count == 0)
                ops.Add(new ChangeOperation(spec.Method, spec.Method, "(azione di sistema)", "n/d",
                    needsAdmin, reversible, spec.RequiresReboot));

            entries.Add(new TrustEntry(e.Id, e.Name, ops));
        }
        return entries;
    }

    /// <summary>Plain summary line, e.g. "12 tweak · 18 operazioni · tutte reversibili".</summary>
    public static string Summarize(IReadOnlyList<TrustEntry> manifest)
    {
        int ops = manifest.Sum(t => t.Operations.Count);
        bool allReversible = manifest.All(t => t.Operations.All(o => o.Reversible));
        int adminOps = manifest.Sum(t => t.Operations.Count(o => o.NeedsAdmin));
        return $"{manifest.Count} tweak · {ops} operazioni" +
               (allReversible ? " · tutte reversibili" : " · alcune NON reversibili") +
               (adminOps > 0 ? $" · {adminOps} richiedono admin" : "");
    }
}
