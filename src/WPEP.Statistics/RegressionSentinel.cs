namespace WPEP.Statistics;

public enum SentinelStatus { NoBaseline, Stable, Improved, Regressed, Inconclusive }

/// <summary>The Sentinel's read on a re-benchmark vs the known-good baseline.</summary>
public sealed record SentinelResult(SentinelStatus Status, string Headline, double DeltaPercent, string Color);

/// <summary>Regression Sentinel (Lab feature): you bank a known-good benchmark, the Sentinel
/// re-benches later and tells you if performance got WORSE (a Windows Update broke something, a
/// driver regressed). Nobody warns you when you regress — this does. Pure interpretation over the
/// existing statistical comparison: frametime is lower-is-better, so a positive median delta is a
/// regression. The measurement itself is the bench/compare engine; this is the verdict layer.</summary>
public static class RegressionSentinel
{
    public static SentinelResult Evaluate(ComparisonEngine.ComparisonReport? report)
    {
        if (report is null || report.Metrics.Count == 0)
            return new(SentinelStatus.NoBaseline,
                "Nessuna baseline: banca un benchmark con il sistema in forma per attivare la sorveglianza.", 0, "Neutral");

        if (report.GateTriggered)
            return new(SentinelStatus.Inconclusive,
                "Misura troppo rumorosa per un verdetto. Riprova in uno scenario ripetibile.", 0, "Warn");

        var primary = report.Metrics[0]; // median frametime — lower is better
        double delta = primary.DeltaPercent;
        return primary.Verdict switch
        {
            Verdict.Regression => new(SentinelStatus.Regressed,
                $"⚠ Regressione: i frametime sono PEGGIORATI del {delta:F1}% rispetto alla baseline. " +
                "Qualcosa è cambiato (Windows Update? driver?). Indaga.", delta, "Danger"),
            Verdict.Improvement => new(SentinelStatus.Improved,
                $"Migliorato del {System.Math.Abs(delta):F1}% rispetto alla baseline. Aggiorna la baseline se è stabile.",
                delta, "Ok"),
            _ => new(SentinelStatus.Stable,
                "Stabile: nessuna regressione misurabile rispetto alla baseline. Tutto sotto controllo.", delta, "Ok"),
        };
    }
}
