using WPEP.Advisor;
using WPEP.Core.SystemInfo;
using WPEP.KnowledgeBase;
using WPEP.Reporting;
using WPEP.Statistics;
using Xunit;

namespace WPEP.Tests;

public class ReportBuilderTests
{
    private static ReportData Data(
        NoiseFloorAnalyzer.NoiseReport? noise = null,
        ComparisonEngine.ComparisonReport? comparison = null) => new(
        GeneratedAtUtc: DateTimeOffset.UnixEpoch,
        Snapshot: new SystemSnapshot
        {
            CapturedAtUtc = DateTimeOffset.UnixEpoch,
            CpuName = "Test CPU <script>alert(1)</script>",
            GpuName = "Test GPU",
            PowerPlanName = "Bilanciato",
        },
        Recommendations:
        [
            new Recommendation(new TweakEntry
            {
                Id = "test-tweak",
                Name = "Test & Tweak",
                Category = "gpu",
                Description = "d",
                ExpectedImpact = "i",
                EvidenceLevel = EvidenceLevel.Placebo,
                Risk = RiskLevel.None,
                Rollback = "r",
                ManualSteps = "m",
                Measurable = false,
            }, Classification.Placebo, "nota di stato"),
        ],
        Noise: noise,
        Comparison: comparison);

    [Fact]
    public void BuildHtml_ContainsCoreSections()
    {
        var html = ReportBuilder.BuildHtml(Data());

        Assert.Contains("WPEP", html);
        Assert.Contains("Sistema", html);
        Assert.Contains("Advisor", html);
        Assert.Contains("test-tweak", html);
        Assert.Contains("Placebo", html);
        Assert.Contains("read-only", html);
    }

    [Fact]
    public void BuildHtml_EscapesHtmlInSnapshotAndEntries()
    {
        var html = ReportBuilder.BuildHtml(Data());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Test &amp; Tweak", html);
    }

    [Fact]
    public void BuildHtml_WithoutOptionalSections_OmitsThem()
    {
        var html = ReportBuilder.BuildHtml(Data());
        Assert.DoesNotContain("Noise floor", html);
        Assert.DoesNotContain("Confronto baseline", html);
    }

    [Fact]
    public void BuildHtml_PlaceboEntry_NotMarkedOneClick()
    {
        // Data() uses a placebo entry: it must never carry the one-click badge.
        var html = ReportBuilder.BuildHtml(Data());
        Assert.DoesNotContain("one-click", html);
    }

    [Fact]
    public void BuildHtml_MarksOneClickApplicable_AndRendersAppliedChanges()
    {
        var applicable = new Recommendation(new TweakEntry
        {
            Id = "hags",
            Name = "HAGS",
            Category = "gpu",
            Description = "d",
            ExpectedImpact = "i",
            EvidenceLevel = EvidenceLevel.Controversial,
            Risk = RiskLevel.Low,
            Rollback = "r",
            ManualSteps = "m",
            Measurable = true,
            Apply = new ApplySpec
            {
                Method = "registry",
                Operations = [new ApplyOperation
                    { Path = @"HKLM\X\Y", ValueAfter = "2", Kind = "dword" }],
            },
        }, Classification.OptionalWithWarning, "stato");

        var data = new ReportData(
            DateTimeOffset.UnixEpoch,
            new SystemSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UnixEpoch,
                CpuName = "c", GpuName = "g", PowerPlanName = "p",
            },
            [applicable], Noise: null, Comparison: null,
            AppliedChanges: [@"game-dvr · HKCU\...\AppCaptureEnabled: 1 → 0 [applicato]"]);

        var html = ReportBuilder.BuildHtml(data);

        Assert.Contains("one-click", html);                  // badge present
        Assert.Contains("Changes applied by Verdict", html); // section present
        Assert.Contains("AppCaptureEnabled", html);          // the journaled change is shown
    }

    [Fact]
    public void BuildHtml_WithComparison_RendersVerdicts()
    {
        var comparison = new ComparisonEngine.ComparisonReport(5, 5, Conclusive: true,
            GateThresholdPercent: 10,
        [
            new ComparisonEngine.MetricComparison(
                "Median frametime (ms)", 10, 9, -1, -10,
                PValue: 0.008, new Bootstrap.Interval(-1, -1.4, -0.6),
                MdePercent: 2.5, Verdict.Improvement),
        ]);

        var html = ReportBuilder.BuildHtml(Data(comparison: comparison));

        Assert.Contains("Confronto baseline", html);
        Assert.Contains("MIGLIORAMENTO", html);
        Assert.Contains("0.008", html);
    }
}
