namespace WPEP.Execution;

/// <summary>Inputs for the Verdict Score, kept primitive so the math is unit-testable without
/// a full system snapshot. The caller (App) maps Advisor classifications + scan findings onto
/// these fields.</summary>
public sealed record ScoreInput(
    int RecommendedDone,     // evidence-backed tweaks already optimal on this PC
    int RecommendedPending,  // evidence-backed tweaks worth doing, not yet done
    int RiskyActive,         // risky tweaks currently ON (honesty: these don't earn points)
    int PlaceboActive,       // placebo tweaks the user applied (counted for the note, NOT scored)
    bool? ExpoEnabled);      // RAM EXPO/XMP: true=on, false=off (real free perf), null=unknown

/// <summary>One line of the score breakdown: a human reason and its point delta.</summary>
public sealed record ScoreReason(string Text, int Delta)
{
    /// <summary>Signed label for display; empty when the line is informational (no point change).</summary>
    public string DeltaLabel => Delta switch { 0 => "", > 0 => $"+{Delta}", _ => Delta.ToString() };
}

public sealed record ScoreResult(int Score, string Band, string BandColor,
    IReadOnlyList<ScoreReason> Breakdown, string HonestyNote);

/// <summary>The Verdict Score (0–100): a single honest number for "how optimized is this PC".
/// It rewards ONLY evidence-backed wins and refuses to move for placebos — that refusal is the
/// whole project's identity turned into a feature. Pure/deterministic so it can be tested and
/// is identical in GUI and (future) CLI.</summary>
public static class VerdictScore
{
    // Each evidence-backed tweak still worth doing is real perf left on the table.
    private const int PerPendingPenalty = 6;
    private const int PendingPenaltyCap = 54;   // never let pending tweaks alone zero the score
    private const int ExpoOffPenalty = 15;      // EXPO off is a big, free, measurable win missed
    private const int PerRiskyPenalty = 5;
    private const int RiskyPenaltyCap = 20;

    public static ScoreResult Compute(ScoreInput i)
    {
        var reasons = new List<ScoreReason>();
        int score = 100;

        int pendingPenalty = Math.Min(i.RecommendedPending * PerPendingPenalty, PendingPenaltyCap);
        if (pendingPenalty > 0)
        {
            score -= pendingPenalty;
            reasons.Add(new($"{i.RecommendedPending} ottimizzazioni utili ancora da applicare", -pendingPenalty));
        }

        if (i.ExpoEnabled == false)
        {
            score -= ExpoOffPenalty;
            reasons.Add(new("RAM EXPO/XMP spento — perfomance gratis non sfruttata", -ExpoOffPenalty));
        }

        int riskyPenalty = Math.Min(i.RiskyActive * PerRiskyPenalty, RiskyPenaltyCap);
        if (riskyPenalty > 0)
        {
            score -= riskyPenalty;
            reasons.Add(new($"{i.RiskyActive} tweak rischiosi attivi", -riskyPenalty));
        }

        if (i.RecommendedDone > 0)
            reasons.Add(new($"{i.RecommendedDone} ottimizzazioni utili già attive", 0));

        score = Math.Clamp(score, 0, 100);
        var (band, color) = Band(score);

        // The honesty note: we explicitly say we did NOT count placebos. No other tool does this.
        string note = i.PlaceboActive > 0
            ? $"Onestà: {i.PlaceboActive} tweak-placebo che hai applicato NON contano nel punteggio — non gonfiamo il numero."
            : "Onestà: il punteggio premia solo ottimizzazioni con evidenza reale, mai i placebo.";

        return new ScoreResult(score, band, color, reasons, note);
    }

    private static (string Band, string Color) Band(int score) => score switch
    {
        >= 90 => ("Eccellente", "Ok"),
        >= 75 => ("Buono", "Ok"),
        >= 55 => ("Discreto", "Warn"),
        _ => ("Da sistemare", "Danger"),
    };
}
