using WPEP.Execution;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class TrustManifestTests
{
    private static TweakEntry Entry(string id, string method, string path = @"HKCU\K\V",
        EvidenceLevel evidence = EvidenceLevel.Plausible, string undo = "auto-journal",
        bool reboot = false) => new()
    {
        Id = id, Name = id.ToUpperInvariant(), Category = "gpu", Description = "d", ExpectedImpact = "i",
        EvidenceLevel = evidence, Sources = ["https://x"], Risk = RiskLevel.Low,
        Rollback = "r", ManualSteps = "m", Measurable = true,
        Apply = method == "gui-only"
            ? new ApplySpec { Method = "gui-only" }
            : new ApplySpec
            {
                Method = method, Undo = undo, RequiresReboot = reboot,
                Operations = [new ApplyOperation { Path = path, ValueAfter = "1", Kind = "dword" }],
            },
    };

    [Fact]
    public void OnlyApplicableTweaks_AppearInManifest()
    {
        var m = TrustManifest.Build([
            Entry("reg", "registry"),
            Entry("gui", "gui-only"),                                   // excluded: no write method
            Entry("plac", "registry", evidence: EvidenceLevel.Placebo), // excluded: placebo
        ]);
        Assert.Single(m);
        Assert.Equal("reg", m[0].TweakId);
    }

    [Fact]
    public void HklmOperation_IsFlaggedNeedsAdmin()
    {
        var m = TrustManifest.Build([Entry("x", "registry", @"HKLM\Soft\X")]);
        Assert.True(m[0].Operations[0].NeedsAdmin);
    }

    [Fact]
    public void AutoJournal_IsReversible()
    {
        var m = TrustManifest.Build([Entry("x", "registry")]);
        Assert.True(m[0].Operations[0].Reversible);
    }

    [Fact]
    public void NoUndo_IsNotReversible()
    {
        var m = TrustManifest.Build([Entry("x", "registry", undo: "none")]);
        Assert.False(m[0].Operations[0].Reversible);
    }

    [Fact]
    public void Summarize_CountsTweaksOpsAndReversibility()
    {
        var m = TrustManifest.Build([Entry("a", "registry"), Entry("b", "registry", @"HKLM\X\Y")]);
        var s = TrustManifest.Summarize(m);
        Assert.Contains("2 tweak", s);
        Assert.Contains("2 operazioni", s);
        Assert.Contains("reversibili", s);
        Assert.Contains("admin", s); // the HKLM op needs admin
    }

    [Fact]
    public void EmptyInput_EmptyManifest()
    {
        Assert.Empty(TrustManifest.Build([]));
    }
}
