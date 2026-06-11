using WPEP.Core.Benchmark;

namespace WPEP.Statistics;

/// <summary>
/// EDGE_CASES F10: a comparison between runs captured under different
/// environments (GPU driver, display mode, power plan) is invalid by
/// construction. Better no verdict than a verdict on apples vs oranges.
/// </summary>
public static class EnvironmentValidator
{
    public sealed record Result(bool Valid, string? BlockReason, string? Warning);

    public static Result Validate(
        IReadOnlyList<BenchmarkRun> baseline, IReadOnlyList<BenchmarkRun> post)
    {
        var all = baseline.Concat(post).ToArray();
        var environments = all.Select(r => r.Environment).Where(e => e is not null).Cast<RunEnvironment>().ToArray();

        string? warning = null;
        if (environments.Length < all.Length)
            warning = environments.Length == 0
                ? "Nessuna run ha l'ambiente registrato (run pre-F10): impossibile verificare la coerenza."
                : $"{all.Length - environments.Length} run senza ambiente registrato: coerenza verificata solo sulle altre.";

        var distinct = environments.Distinct().ToArray();
        if (distinct.Length > 1)
        {
            return new Result(false,
                "Ambiente cambiato tra le run — confronto non valido.\n" +
                string.Join("\n", distinct.Select(e => $"  - {e.Describe()}")) +
                "\nRifai baseline e post nello stesso ambiente (stesso driver, risoluzione, refresh, piano energia).",
                warning);
        }

        return new Result(true, null, warning);
    }
}
