namespace WPEP.Statistics;

/// <summary>The summary of a reaction-time session.</summary>
public sealed record ReactionResult(
    int Count, int BestMs, int AverageMs, int MedianMs, string Grade, string GradeColor);

/// <summary>Reaction Lab (Lab feature): measures the WHOLE chain — your eyes + brain + hand + the
/// system's input-to-photon latency — by timing how fast you react to a colour change. Honest
/// framing: this is human+system combined, not a pure system number. The math is pure/testable;
/// the minigame on top just feeds it samples. Median is used for the grade so one fumble or one
/// lucky click doesn't define the score.</summary>
public static class ReactionStats
{
    public static ReactionResult Analyze(IReadOnlyList<int> samplesMs)
    {
        if (samplesMs.Count == 0)
            return new(0, 0, 0, 0, "—", "Neutral");

        var sorted = samplesMs.OrderBy(x => x).ToList();
        int best = sorted[0];
        int avg = (int)System.Math.Round(samplesMs.Average());
        int median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

        var (grade, color) = Grade(median);
        return new(samplesMs.Count, best, avg, median, grade, color);
    }

    private static (string Grade, string Color) Grade(int median) => median switch
    {
        < 180 => ("Élite", "Ok"),
        < 220 => ("Ottimo", "Ok"),
        < 260 => ("Buono", "Info"),
        < 320 => ("Nella media", "Warn"),
        _ => ("Lento — c'è margine", "Danger"),
    };
}
