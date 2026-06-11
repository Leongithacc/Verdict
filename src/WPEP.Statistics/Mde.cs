namespace WPEP.Statistics;

/// <summary>
/// Minimum Detectable Effect: the smallest relative difference this baseline's
/// noise allows us to detect. Estimated as the bootstrap CI half-width of a
/// null comparison (baseline resampled against itself), relative to the median.
/// If the MDE exceeds the noise gate threshold, the honest output is "no verdict",
/// not "no effect" (HANDOFF_R7 §1).
/// </summary>
public static class Mde
{
    /// <summary>Default noise gate: above this MDE%, no verdict is emitted.</summary>
    public const double DefaultGateThresholdPercent = 10.0;

    public static double Percent(IReadOnlyList<double> baselineValues)
    {
        double median = Bootstrap.Median([.. baselineValues]);
        if (median == 0)
            return double.PositiveInfinity;

        var nullCi = Bootstrap.DifferenceCi(baselineValues, baselineValues, Bootstrap.Median);
        double halfWidth = Math.Max(Math.Abs(nullCi.Lower), Math.Abs(nullCi.Upper));
        return halfWidth / Math.Abs(median) * 100.0;
    }
}
