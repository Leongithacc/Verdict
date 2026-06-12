using System.Text;
using WPEP.Advisor;
using WPEP.Core.SystemInfo;
using WPEP.Statistics;

namespace WPEP.Reporting;

public sealed record ReportData(
    DateTimeOffset GeneratedAtUtc,
    SystemSnapshot Snapshot,
    IReadOnlyList<Recommendation> Recommendations,
    NoiseFloorAnalyzer.NoiseReport? Noise,
    ComparisonEngine.ComparisonReport? Comparison);

/// <summary>
/// Renders the honest report (spec §3G): system snapshot, advisor verdicts
/// including what was REJECTED as placebo, measurements when available.
/// Self-contained dark/violet HTML, no external assets.
/// </summary>
public static class ReportBuilder
{
    public static string BuildHtml(ReportData data)
    {
        var s = data.Snapshot;
        var sb = new StringBuilder(1 << 16);

        sb.Append($$"""
            <!DOCTYPE html>
            <html lang="it"><head><meta charset="utf-8">
            <title>Verdict Report</title>
            <style>
              :root { --bg:#0e0e13; --panel:#16161f; --text:#d8d8e0; --dim:#8a8a96;
                      --accent:#8b5cf6; --good:#34d399; --warn:#fbbf24; --bad:#f87171; }
              body { background:var(--bg); color:var(--text); font:15px/1.55 "Segoe UI",system-ui,sans-serif;
                     max-width:960px; margin:2rem auto; padding:0 1rem; }
              h1 { color:var(--accent); font-weight:600; }
              h2 { color:var(--accent); border-bottom:1px solid #2a2a38; padding-bottom:.3rem; margin-top:2.2rem; }
              table { border-collapse:collapse; width:100%; margin:.8rem 0; }
              th,td { text-align:left; padding:.45rem .7rem; border-bottom:1px solid #23232f; vertical-align:top; }
              th { color:var(--dim); font-weight:600; }
              .dim { color:var(--dim); } .good { color:var(--good); }
              .warn { color:var(--warn); } .bad { color:var(--bad); }
              .panel { background:var(--panel); border:1px solid #23232f; border-radius:10px; padding:1rem 1.2rem; margin:1rem 0; }
              code { background:#1d1d29; padding:.1rem .4rem; border-radius:4px; }
              footer { color:var(--dim); margin-top:3rem; font-size:.85em; }
            </style></head><body>
            <h1>Verdict — Report di sistema</h1>
            <p class="dim">Generato il {{Esc(data.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm"))}} UTC ·
            read-only: questo tool non ha modificato nulla.</p>
            """);

        if (s.IsManagedDevice == true)
        {
            // PORTABILITY §3: the notice is always visible in reports from managed machines.
            sb.Append("""
                <div class="panel" style="border-color:#fbbf24"><p class="warn">
                This report was generated on what looks like a company-managed device.
                Running third-party diagnostic tools may violate the organization's IT policy.
                </p></div>
                """);
        }

        // — Snapshot —
        sb.Append("<h2>Sistema</h2><div class=\"panel\"><table>");
        Row(sb, "CPU", $"{Esc(s.CpuName)} ({s.CpuCores?.ToString() ?? "?"}C/{s.CpuThreads?.ToString() ?? "?"}T{(s.CpuIsX3D ? ", X3D" : "")})");
        Row(sb, "GPU", $"{Esc(s.GpuName)} — driver {Esc(s.GpuDriverVersion)}");
        Row(sb, "RAM", $"{s.RamTotalGb?.ToString("F0") ?? "?"} GB @ {s.RamSpeedMtps?.ToString() ?? "?"} MT/s");
        Row(sb, "Monitor", $"{s.MonitorCurrentHz?.ToString() ?? "?"} Hz attivi / max {s.MonitorMaxHz?.ToString() ?? "?"} Hz");
        Row(sb, "Power plan", Esc(s.PowerPlanName));
        Row(sb, "Rete attiva", s.ActiveNicIsWifi switch { true => "Wi-Fi", false => "cablata", null => "sconosciuta" });
        sb.Append("</table></div>");

        // — Advisor —
        sb.Append("<h2>Advisor</h2>");
        foreach (var group in data.Recommendations.GroupBy(r => r.Classification))
        {
            sb.Append($"<h3 class=\"{CssFor(group.Key)}\">{Esc(Label(group.Key))}</h3><table>");
            foreach (var r in group)
                sb.Append($"<tr><td><code>{Esc(r.Entry.Id)}</code></td><td>{Esc(r.Entry.Name)}</td><td class=\"dim\">{Esc(r.StateNote)}</td></tr>");
            sb.Append("</table>");
        }

        // — Noise floor —
        if (data.Noise is { } noise)
        {
            sb.Append($"<h2>Noise floor ({noise.Runs} run)</h2>");
            if (!noise.Reliable)
                sb.Append("<p class=\"warn\">Meno di 4 run: stima poco affidabile.</p>");
            sb.Append("<table><tr><th>Metrica</th><th>Mediana</th><th>Min</th><th>Max</th><th>Range%</th></tr>");
            foreach (var m in noise.Metrics)
                sb.Append($"<tr><td>{Esc(m.Metric)}</td><td>{m.Median:F2}</td><td>{m.Min:F2}</td><td>{m.Max:F2}</td><td>{m.RangePercent:F1}%</td></tr>");
            sb.Append("</table><p class=\"dim\">Qualsiasi \"miglioramento\" più piccolo di questi range è rumore.</p>");
        }

        // — Comparison —
        if (data.Comparison is { } cmp)
        {
            sb.Append($"<h2>Confronto baseline vs post ({cmp.BaselineRuns}+{cmp.PostRuns} run)</h2>");
            if (!cmp.Conclusive)
                sb.Append("<p class=\"warn\">Meno di 5 run per lato: risultati indicativi, non conclusioni.</p>");
            sb.Append("<table><tr><th>Metrica</th><th>Base</th><th>Post</th><th>Δ%</th><th>p</th><th>CI 95%</th><th>Verdetto</th></tr>");
            foreach (var m in cmp.Metrics)
            {
                string cls = m.Verdict switch
                {
                    Verdict.Improvement => "good",
                    Verdict.Regression => "bad",
                    Verdict.ScenarioTooNoisy => "warn",
                    _ => "dim",
                };
                string verdict = m.Verdict switch
                {
                    Verdict.Improvement => "MIGLIORAMENTO",
                    Verdict.Regression => "PEGGIORAMENTO",
                    Verdict.ScenarioTooNoisy => $"nessun verdetto: scenario troppo rumoroso (MDE {m.MdePercent:F0}%)",
                    _ => $"nessun effetto misurabile (soglia {m.MdePercent:F1}%)",
                };
                sb.Append($"<tr><td>{Esc(m.Metric)}</td><td>{m.BaselineMedian:F2}</td><td>{m.PostMedian:F2}</td>" +
                          $"<td>{m.DeltaPercent:+0.0;-0.0;0.0}%</td><td>{m.PValue:F3}</td>" +
                          $"<td>[{m.Ci.Lower:F3}, {m.Ci.Upper:F3}]</td><td class=\"{cls}\">{verdict}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("""
            <footer>
            <p><strong>Come leggere questo report.</strong> Le voci "placebo" sono mostrate apposta:
            sapere cosa NON funziona vale quanto sapere cosa funziona. Nessuna raccomandazione è
            senza fonte; nessun miglioramento è dichiarato senza statistica (Mann–Whitney +
            bootstrap, unità = run). L'input latency end-to-end non è misurabile in puro software
            e questo report non finge il contrario.</p>
            <p>Verdict · engine: WPEP (Windows Performance Engineering Platform) · V1 read-only</p>
            </footer></body></html>
            """);

        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string k, string v) =>
        sb.Append($"<tr><th>{Esc(k)}</th><td>{v}</td></tr>");

    private static string Label(Classification c) => c switch
    {
        Classification.Recommended => "Consigliato (evidenza forte)",
        Classification.Optional => "Opzionale (plausibile, da misurare)",
        Classification.OptionalWithWarning => "Opzionale con riserva (controverso)",
        Classification.Placebo => "Placebo — non lo tocchiamo",
        Classification.NotRecommended => "Sconsigliato (rischio reale)",
        Classification.AlreadyActive => "Già a posto",
        Classification.NotApplicable => "Non applicabile a questo sistema",
        _ => c.ToString(),
    };

    private static string CssFor(Classification c) => c switch
    {
        Classification.Recommended => "good",
        Classification.AlreadyActive => "good",
        Classification.NotRecommended => "bad",
        Classification.Placebo => "dim",
        _ => "warn",
    };

    internal static string Esc(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
