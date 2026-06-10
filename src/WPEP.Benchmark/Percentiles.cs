namespace WPEP.Benchmark;

public static class Percentiles
{
    /// <summary>
    /// Percentile with linear interpolation between closest ranks
    /// (same convention as numpy's default). <paramref name="p"/> is 0..100.
    /// </summary>
    public static double Compute(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0)
            throw new ArgumentException("Serie vuota: nessun percentile calcolabile.", nameof(values));
        ArgumentOutOfRangeException.ThrowIfLessThan(p, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(p, 100);

        var sorted = values.ToArray();
        Array.Sort(sorted);

        double rank = p / 100.0 * (sorted.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi)
            return sorted[lo];
        return sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }
}
