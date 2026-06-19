namespace WPEP.Execution;

/// <summary>The honest result of a blind A/B, mapped from the statistical verdict by the caller.</summary>
public enum GhostOutcome { Helped, NoEffect, Hurt, Inconclusive }

public sealed record GhostReveal(GhostOutcome Outcome, string Title, string Plain, string Color);

/// <summary>Ghost Tweak (Lab feature, signature anti-placebo idea): Verdict applies a tweak WITHOUT
/// telling you which, you play and it measures, then it REVEALS whether it actually helped. A
/// double-blind on yourself — kills placebo psychologically. This is the pure core: blind selection
/// + the honest reveal text. The measurement itself is the existing bench/compare engine; the GUI
/// maps that statistical verdict onto <see cref="GhostOutcome"/>.</summary>
public static class GhostTweak
{
    /// <summary>Blindly picks one candidate id. Deterministic in <paramref name="seed"/> so it's
    /// testable; the app passes a real random seed so the user can't predict the pick.</summary>
    public static string Pick(IReadOnlyList<string> candidateIds, int seed)
    {
        if (candidateIds.Count == 0)
            throw new ArgumentException("Nessun tweak candidato per il round cieco.", nameof(candidateIds));
        // unchecked + abs-safe modulo so any seed (incl. int.MinValue) maps into range.
        long s = seed;
        int idx = (int)(((s % candidateIds.Count) + candidateIds.Count) % candidateIds.Count);
        return candidateIds[idx];
    }

    /// <summary>The reveal: name the hidden tweak and tell the honest truth about whether it moved
    /// the needle on THIS system. NoEffect is framed as "placebo for you" — the whole point.</summary>
    public static GhostReveal Reveal(string tweakName, GhostOutcome outcome, double deltaPercent)
    {
        double mag = Math.Abs(deltaPercent);
        return outcome switch
        {
            GhostOutcome.Helped => new(outcome, "Ha aiutato davvero ✓",
                $"Era «{tweakName}». Ha migliorato i frametime del {mag:F1}% in modo statisticamente reale. " +
                "Per te NON è placebo: tienilo.", "Ok"),
            GhostOutcome.Hurt => new(outcome, "Ha peggiorato ✗",
                $"Era «{tweakName}». Ha PEGGIORATO i frametime del {mag:F1}%. Verdict lo ha già annullato.", "Danger"),
            GhostOutcome.NoEffect => new(outcome, "Nessun effetto misurabile",
                $"Era «{tweakName}». Sul TUO sistema non ha cambiato niente di rilevabile — placebo per te, " +
                "anche se popolare online. Non gonfiamo il risultato.", "Neutral"),
            _ => new(GhostOutcome.Inconclusive, "Verdetto non possibile",
                $"Era «{tweakName}», ma la misura era troppo rumorosa per un verdetto onesto. " +
                "Riprova in uno scenario ripetibile (mappa benchmark, AFK).", "Warn"),
        };
    }
}
