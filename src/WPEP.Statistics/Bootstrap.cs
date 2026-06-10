namespace WPEP.Statistics;

/// <summary>
/// Percentile bootstrap for the difference of a statistic between two groups
/// (post - baseline). Deterministic via fixed seed.
/// </summary>
public static class Bootstrap
{
    public sealed record Interval(double PointEstimate, double Lower, double Upper)
    {
        public bool IncludesZero => Lower <= 0 && Upper >= 0;
    }

    public static Interval DifferenceCi(
        IReadOnlyList<double> baseline, IReadOnlyList<double> post,
        Func<double[], double> statistic,
        double confidence = 0.95, int iterations = 10_000, int seed = 42)
    {
        if (baseline.Count == 0 || post.Count == 0)
            throw new ArgumentException("Entrambi i gruppi devono contenere almeno un valore.");

        double point = statistic([.. post]) - statistic([.. baseline]);

        var rng = new Random(seed);
        var deltas = new double[iterations];
        var sampleA = new double[baseline.Count];
        var sampleB = new double[post.Count];

        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < sampleA.Length; j++)
                sampleA[j] = baseline[rng.Next(baseline.Count)];
            for (int j = 0; j < sampleB.Length; j++)
                sampleB[j] = post[rng.Next(post.Count)];
            deltas[i] = statistic(sampleB) - statistic(sampleA);
        }

        Array.Sort(deltas);
        double alpha = (1 - confidence) / 2;
        return new Interval(
            point,
            Quantile(deltas, alpha),
            Quantile(deltas, 1 - alpha));
    }

    public static double Median(double[] values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double Quantile(double[] sorted, double q)
    {
        double rank = q * (sorted.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi)
            return sorted[lo];
        return sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }
}
