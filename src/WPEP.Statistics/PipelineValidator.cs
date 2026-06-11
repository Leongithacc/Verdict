using WPEP.Core.Benchmark;

namespace WPEP.Statistics;

/// <summary>
/// HANDOFF_R7 §2: the pipeline certifies itself before judging tweaks.
/// A/A test (expect: none): two run groups with NOTHING changed — any detected
/// effect is a false positive. Known-effect test (expect: effect): a guaranteed
/// large change — failing to detect it means the pipeline or scenario is unusable.
/// </summary>
public static class PipelineValidator
{
    public enum Expectation { None, Effect }

    public sealed record Result(
        bool Passed,
        string Summary,
        ComparisonEngine.ComparisonReport Report);

    public static Result Run(
        IReadOnlyList<BenchmarkRun> groupA, IReadOnlyList<BenchmarkRun> groupB,
        Expectation expectation,
        double gateThresholdPercent = Mde.DefaultGateThresholdPercent)
    {
        var report = ComparisonEngine.Compare(groupA, groupB, gateThresholdPercent);
        var primary = report.Metrics[0]; // median frametime

        if (report.GateTriggered)
        {
            return new Result(false,
                $"FAIL — scenario troppo rumoroso (MDE {primary.MdePercent:F1}% > gate {gateThresholdPercent:F0}%). " +
                "Questo scenario non può certificare nulla: rendilo più ripetibile e ripeti.",
                report);
        }

        bool effectDetected = report.Metrics.Any(
            m => m.Verdict is Verdict.Improvement or Verdict.Regression);

        return expectation switch
        {
            Expectation.None when !effectDetected => new Result(true,
                $"PASS — A/A test: nessun effetto dichiarato tra due gruppi identici " +
                $"(MDE {primary.MdePercent:F1}%). La pipeline non inventa effetti su questo scenario.",
                report),

            Expectation.None => new Result(false,
                "FAIL — A/A test: la pipeline ha dichiarato un effetto tra due gruppi " +
                "raccolti senza cambiare nulla. Falso positivo: run insufficienti, " +
                "scenario instabile, o condizione esterna cambiata (driver, temperature, patch).",
                report),

            Expectation.Effect when effectDetected => new Result(true,
                $"PASS — known-effect test: effetto rilevato " +
                $"({primary.DeltaPercent:+0.0;-0.0}% sulla mediana, p={primary.PValue:F3}). " +
                "La pipeline vede gli effetti reali su questo scenario.",
                report),

            _ => new Result(false,
                "FAIL — known-effect test: una modifica a effetto garantito NON è stata " +
                "rilevata. Pipeline o scenario inutilizzabili per giudicare tweak più piccoli: " +
                "più run, scenario più ripetibile, o effetto di prova più grande.",
                report),
        };
    }
}
