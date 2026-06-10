using System.Text.Json;
using WPEP.Core.Benchmark;

namespace WPEP.Benchmark;

/// <summary>Loads benchmark runs saved by the bench command.</summary>
public static class BenchmarkRunStore
{
    public static IReadOnlyList<BenchmarkRun> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Cartella non trovata: {directory}");

        var runs = new List<BenchmarkRun>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order())
        {
            var run = JsonSerializer.Deserialize<BenchmarkRun>(File.ReadAllText(file))
                ?? throw new InvalidDataException($"Run non leggibile: {file}");
            runs.Add(run);
        }
        return runs;
    }
}
