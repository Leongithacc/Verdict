namespace WPEP.Statistics;

/// <summary>
/// Mann–Whitney U with a Monte Carlo permutation p-value. Frametime metrics are
/// non-normal with fat tails (spec §6), so no normal-approximation shortcut:
/// the permutation distribution is exact in the limit and honest at the tiny
/// sample sizes (N runs per side) we actually have. Deterministic via fixed seed.
/// </summary>
public static class MannWhitney
{
    public sealed record Result(double U, double PValueTwoSided);

    public static Result Test(
        IReadOnlyList<double> a, IReadOnlyList<double> b,
        int permutations = 100_000, int seed = 42)
    {
        if (a.Count == 0 || b.Count == 0)
            throw new ArgumentException("Entrambi i gruppi devono contenere almeno un valore.");

        var pooled = a.Concat(b).ToArray();
        var ranks = AverageRanks(pooled);

        double observedU = UFromRanks(ranks, a.Count);
        // Center of the U distribution under H0; distance from it is the test statistic.
        double meanU = a.Count * (double)b.Count / 2.0;
        double observedDistance = Math.Abs(observedU - meanU);

        var rng = new Random(seed);
        var indices = Enumerable.Range(0, pooled.Length).ToArray();
        int atLeastAsExtreme = 0;

        for (int p = 0; p < permutations; p++)
        {
            // Fisher–Yates partial shuffle: first a.Count indices form group A.
            for (int i = 0; i < a.Count; i++)
            {
                int j = rng.Next(i, indices.Length);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            double u = 0;
            for (int i = 0; i < a.Count; i++)
                u += ranks[indices[i]];
            u -= a.Count * (a.Count + 1) / 2.0;

            if (Math.Abs(u - meanU) >= observedDistance - 1e-12)
                atLeastAsExtreme++;
        }

        // +1 correction: the observed labelling is itself a valid permutation.
        double pValue = (atLeastAsExtreme + 1.0) / (permutations + 1.0);
        return new Result(observedU, Math.Min(1.0, pValue));
    }

    private static double UFromRanks(double[] ranks, int nA)
    {
        double rankSumA = 0;
        for (int i = 0; i < nA; i++)
            rankSumA += ranks[i];
        return rankSumA - nA * (nA + 1) / 2.0;
    }

    /// <summary>Ranks 1..n with ties assigned the average of their rank span.</summary>
    internal static double[] AverageRanks(double[] values)
    {
        var order = Enumerable.Range(0, values.Length)
            .OrderBy(i => values[i])
            .ToArray();
        var ranks = new double[values.Length];

        int pos = 0;
        while (pos < order.Length)
        {
            int tieEnd = pos;
            while (tieEnd + 1 < order.Length &&
                   values[order[tieEnd + 1]] == values[order[pos]])
                tieEnd++;

            double avgRank = (pos + tieEnd) / 2.0 + 1; // ranks are 1-based
            for (int i = pos; i <= tieEnd; i++)
                ranks[order[i]] = avgRank;
            pos = tieEnd + 1;
        }

        return ranks;
    }
}
