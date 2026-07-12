using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using WPEP.Benchmark;
using WPEP.Statistics;

namespace WPEP.App;

/// <summary>Una riga della pagina Storico: una sessione di misura (cartella wizard) con il suo
/// esito. Il verdetto compare solo se la sessione ha sia baseline sia post.</summary>
public sealed record HistorySessionRow(
    string SessionId, string When, string ProcessName,
    string Verdict, string VerdictColor, string Detail, bool CanVerdict);

/// <summary>Pagina "Storico": le misure Ghost/wizard non sono più usa-e-getta. Legge le run
/// persistite su disco (via <see cref="RunHistory"/>), mostra ogni sessione col suo verdetto
/// before/after (riusa <see cref="ComparisonEngine"/>, stessa statistica del wizard) e un trend
/// della mediana frametime nel tempo per il processo più misurato. Sola lettura.</summary>
public sealed class HistoryViewModel : ViewModelBase
{
    // Fixed drawing box for the sparkline; a Viewbox in XAML scales it. Data→VM-space here so the
    // Polyline binding stays pure MVVM (no code-behind measuring).
    private const double BoxW = 260, BoxH = 48;

    public ObservableCollection<HistorySessionRow> Sessions { get; } = [];
    public bool IsEmpty => Sessions.Count == 0;

    public string RunsFolder => Path.Combine(AppContext.BaseDirectory, "runs");

    private PointCollection _trendPoints = [];
    public PointCollection TrendPoints { get => _trendPoints; private set => Set(ref _trendPoints, value); }
    private string _trendProcess = "";
    public string TrendProcess { get => _trendProcess; private set => Set(ref _trendProcess, value); }
    public bool HasTrend => TrendPoints.Count >= 2;

    /// <summary>Apre la cartella delle run in Esplora file. Path app-computed (BaseDirectory\runs),
    /// mai influenzato da input esterni. Best-effort: non deve mai far crashare l'app.</summary>
    public RelayCommand OpenRunsFolderCommand => new(() =>
    {
        try
        {
            Directory.CreateDirectory(RunsFolder);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(RunsFolder) { UseShellExecute = true });
        }
        catch { /* aprire una cartella non deve mai rompere l'app */ }
    });

    public void Refresh()
    {
        Sessions.Clear();
        var sessions = RunHistory.Load(RunsFolder);
        foreach (var s in sessions)
            Sessions.Add(BuildRow(s));
        BuildTrend(sessions);
        Raise(nameof(IsEmpty));
        Raise(nameof(HasTrend));
    }

    private static HistorySessionRow BuildRow(RunSession s)
    {
        string when = s.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (!s.CanVerdict)
            return new HistorySessionRow(s.SessionId, when, s.ProcessName,
                "Solo baseline", "Neutral", $"mediana {s.BaselineMedianMs:F1} ms · nessun post", false);

        var report = ComparisonEngine.Compare(s.Baseline, s.Post);
        double deltaPct = Math.Abs(report.Metrics[0].DeltaPercent);
        string verdict = report.PrimaryVerdict switch
        {
            WPEP.Statistics.Verdict.Improvement => $"Migliorato {deltaPct:F1}%",
            WPEP.Statistics.Verdict.Regression => $"Peggiorato {deltaPct:F1}%",
            WPEP.Statistics.Verdict.ScenarioTooNoisy => "Troppo rumoroso — nessun verdetto",
            _ => "Nessun effetto misurabile",
        };
        string color = report.PrimaryVerdict switch
        {
            WPEP.Statistics.Verdict.Improvement => "Ok",
            WPEP.Statistics.Verdict.Regression => "Danger",
            WPEP.Statistics.Verdict.ScenarioTooNoisy => "Warn",
            _ => "Neutral",
        };
        string detail = $"mediana {s.BaselineMedianMs:F1} → {s.PostMedianMs:F1} ms"
            + (report.Conclusive ? "" : $" · non conclusivo (<{ComparisonEngine.MinRunsForConclusion} run)");
        return new HistorySessionRow(s.SessionId, when, s.ProcessName, verdict, color, detail, true);
    }

    /// <summary>Trend della mediana frametime nel tempo per il processo con più sessioni (≥2 punti).
    /// Ogni punto = mediana della fase post se c'è, altrimenti baseline. Normalizzato in un box fisso.</summary>
    private void BuildTrend(IReadOnlyList<RunSession> sessions)
    {
        var group = sessions
            .GroupBy(s => s.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (group is null)
        {
            TrendProcess = "";
            TrendPoints = [];
            Raise(nameof(HasTrend));
            return;
        }

        var ordered = group.OrderBy(s => s.CapturedAt).ToList();
        var values = ordered.Select(s => s.HasPost ? s.PostMedianMs : s.BaselineMedianMs).ToList();
        double min = values.Min(), max = values.Max();
        double range = max - min;

        var pts = new PointCollection();
        for (int i = 0; i < values.Count; i++)
        {
            double x = values.Count == 1 ? 0 : i / (double)(values.Count - 1) * BoxW;
            // Y cresce verso il basso col frametime: mediana più bassa (= meglio) sta più in alto.
            double y = range <= 0 ? BoxH / 2 : (values[i] - min) / range * BoxH;
            pts.Add(new System.Windows.Point(x, y));
        }
        TrendProcess = group.Key;
        TrendPoints = pts;
        Raise(nameof(HasTrend));
    }
}
