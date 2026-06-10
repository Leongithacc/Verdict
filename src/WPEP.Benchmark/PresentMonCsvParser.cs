using WPEP.Core.Benchmark;

namespace WPEP.Benchmark;

/// <summary>
/// Parses PresentMon CSV output into frame samples. Handles both 2.x headers
/// (FrameTime, GPUBusy, FrameType) and 1.x headers (msBetweenPresents, msGPUActive).
/// When FrameType is present, only application frames are kept: mixing real and
/// AI-generated frames would corrupt the variance (spec §3B).
/// </summary>
public static class PresentMonCsvParser
{
    public sealed record ParseResult(
        IReadOnlyList<FrameSample> Samples,
        int ExcludedNonApplicationFrames);

    public static ParseResult Parse(TextReader reader)
    {
        string? headerLine = reader.ReadLine()
            ?? throw new InvalidDataException("CSV vuoto: PresentMon non ha prodotto output.");

        var columns = headerLine.Split(',');
        int frameTimeIdx = IndexOf(columns, "FrameTime", "msBetweenPresents");
        if (frameTimeIdx < 0)
            throw new InvalidDataException(
                "Colonna frametime non trovata (né 'FrameTime' né 'msBetweenPresents'). " +
                $"Header: {headerLine}");

        int gpuBusyIdx = IndexOf(columns, "GPUBusy", "msGPUActive");
        int frameTypeIdx = IndexOf(columns, "FrameType");

        var samples = new List<FrameSample>(1 << 12);
        int excluded = 0;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;
            var fields = line.Split(',');
            if (fields.Length <= frameTimeIdx)
                continue;

            if (frameTypeIdx >= 0 && frameTypeIdx < fields.Length)
            {
                var type = fields[frameTypeIdx].Trim();
                if (type is not ("" or "NotSet" or "Application"))
                {
                    excluded++;
                    continue;
                }
            }

            if (!double.TryParse(fields[frameTimeIdx],
                    System.Globalization.CultureInfo.InvariantCulture, out double frameTime))
                continue;

            double? gpuBusy = null;
            if (gpuBusyIdx >= 0 && gpuBusyIdx < fields.Length &&
                double.TryParse(fields[gpuBusyIdx],
                    System.Globalization.CultureInfo.InvariantCulture, out double g))
            {
                gpuBusy = g;
            }

            samples.Add(new FrameSample(frameTime, gpuBusy));
        }

        return new ParseResult(samples, excluded);
    }

    private static int IndexOf(string[] columns, params string[] names)
    {
        foreach (var name in names)
        {
            int idx = Array.FindIndex(columns,
                c => string.Equals(c.Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                return idx;
        }
        return -1;
    }
}
