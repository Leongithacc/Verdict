using WPEP.Core.Benchmark;

namespace WPEP.Benchmark;

/// <summary>One measurement session on disk: a wizard run folder with a baseline phase and an
/// optional post phase. <see cref="CanVerdict"/> is true only when both phases exist — the Storico
/// page shows a before/after verdict only then.</summary>
public sealed record RunSession(
    string SessionId,
    DateTimeOffset CapturedAt,
    string ProcessName,
    IReadOnlyList<BenchmarkRun> Baseline,
    IReadOnlyList<BenchmarkRun> Post)
{
    public bool HasPost => Post.Count > 0;
    public bool CanVerdict => Baseline.Count > 0 && Post.Count > 0;

    /// <summary>Median-of-medians frametime (ms) for the baseline phase; 0 if empty.</summary>
    public double BaselineMedianMs => MedianOfMedians(Baseline);
    /// <summary>Median-of-medians frametime (ms) for the post phase; 0 if empty.</summary>
    public double PostMedianMs => MedianOfMedians(Post);

    private static double MedianOfMedians(IReadOnlyList<BenchmarkRun> runs)
    {
        if (runs.Count == 0) return 0;
        var medians = runs.Select(r => r.Metrics.MedianFrameTimeMs).OrderBy(x => x).ToList();
        return medians[medians.Count / 2];
    }
}

/// <summary>Reads the measurement history off disk: the wizard writes each session under
/// <c>&lt;root&gt;/&lt;session&gt;/{baseline,post}/*.json</c> (see MeasureWizard). Pure I/O + projection over
/// <see cref="BenchmarkRun"/>, so it's unit-tested and works on both legacy (tag-less) and new runs.
/// Best-effort: a missing root or a single corrupt session is skipped, never thrown.</summary>
public static class RunHistory
{
    /// <summary>All sessions under <paramref name="runsRoot"/>, newest first. Empty if the root is
    /// absent. Sessions with no readable runs (empty or all-corrupt) are skipped.</summary>
    public static IReadOnlyList<RunSession> Load(string runsRoot)
    {
        if (!Directory.Exists(runsRoot)) return [];

        var sessions = new List<RunSession>();
        foreach (var dir in Directory.EnumerateDirectories(runsRoot))
        {
            var baseline = LoadPhase(Path.Combine(dir, "baseline"));
            var post = LoadPhase(Path.Combine(dir, "post"));
            if (baseline.Count == 0 && post.Count == 0) continue;

            var all = baseline.Concat(post).ToList();
            sessions.Add(new RunSession(
                Path.GetFileName(dir),
                all.Min(r => r.CapturedAtUtc),
                all[0].ProcessName,
                baseline,
                post));
        }
        return sessions.OrderByDescending(s => s.CapturedAt).ToList();
    }

    private static IReadOnlyList<BenchmarkRun> LoadPhase(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        try { return BenchmarkRunStore.LoadDirectory(dir); }
        catch { return []; } // best-effort: una fase corrotta non deve rompere tutta la storia
    }
}
