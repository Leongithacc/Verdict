using WPEP.Execution;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class ApplyPolicyTests
{
    private static TweakEntry Entry(string method, string path = @"HKCU\K\V",
        EvidenceLevel evidence = EvidenceLevel.Plausible) => new()
    {
        Id = "e", Name = "e", Category = "gpu", Description = "d", ExpectedImpact = "i",
        EvidenceLevel = evidence, Sources = ["https://x"], Risk = RiskLevel.Low,
        Rollback = "r", ManualSteps = "m", Measurable = true,
        Apply = method == "gui-only"
            ? new ApplySpec { Method = "gui-only" }
            : new ApplySpec
            {
                Method = method,
                Operations = [new ApplyOperation { Path = path, ValueAfter = "1", Kind = "dword" }],
            },
    };

    // ---- CanApply ----
    [Theory]
    [InlineData("registry", EvidenceLevel.Plausible, true)]
    [InlineData("powercfg", EvidenceLevel.Plausible, true)]
    [InlineData("bcdedit", EvidenceLevel.Risky, true)]
    [InlineData("gui-only", EvidenceLevel.EvidenceStrong, false)]
    [InlineData("registry", EvidenceLevel.Placebo, false)] // placebos are never applied
    public void CanApply_RespectsMethodAndPlacebo(string method, EvidenceLevel ev, bool expected)
    {
        Assert.Equal(expected, ApplyPolicy.CanApply(Entry(method, evidence: ev)));
    }

    // ---- NeedsAdmin ----
    [Fact]
    public void NeedsAdmin_HklmRegistry_True() =>
        Assert.True(ApplyPolicy.NeedsAdmin(Entry("registry", @"HKLM\Soft\X")));

    [Fact]
    public void NeedsAdmin_HkcuRegistry_False() =>
        Assert.False(ApplyPolicy.NeedsAdmin(Entry("registry", @"HKCU\Soft\X")));

    [Fact]
    public void NeedsAdmin_Bcdedit_AlwaysTrue() =>
        Assert.True(ApplyPolicy.NeedsAdmin(Entry("bcdedit", "disabledynamictick")));

    // ---- DecideAction ----
    [Fact]
    public void Decide_NotApplicable_Wins()
    {
        Assert.Equal(ApplyAction.NotApplicable,
            ApplyPolicy.DecideAction(canApply: false, isAlreadyApplied: true,
                needsAdmin: true, elevated: false, confirmYes: true));
    }

    [Fact]
    public void Decide_AlreadyApplied_BeatsAdminGate()
    {
        // No elevation, needs admin, no confirm — but already applied wins (no write needed).
        Assert.Equal(ApplyAction.AlreadyApplied,
            ApplyPolicy.DecideAction(true, isAlreadyApplied: true,
                needsAdmin: true, elevated: false, confirmYes: false));
    }

    [Fact]
    public void Decide_NoConfirm_IsDryRun_EvenWhenAdminNeeded()
    {
        Assert.Equal(ApplyAction.DryRun,
            ApplyPolicy.DecideAction(true, false, needsAdmin: true, elevated: false, confirmYes: false));
    }

    [Fact]
    public void Decide_ConfirmButUnelevatedAdmin_IsNeedsAdmin()
    {
        Assert.Equal(ApplyAction.NeedsAdmin,
            ApplyPolicy.DecideAction(true, false, needsAdmin: true, elevated: false, confirmYes: true));
    }

    [Theory]
    [InlineData(true, true)]   // needs admin but elevated
    [InlineData(false, false)] // no admin needed
    public void Decide_ConfirmAndAllowed_IsExecute(bool needsAdmin, bool elevated)
    {
        Assert.Equal(ApplyAction.Execute,
            ApplyPolicy.DecideAction(true, false, needsAdmin, elevated, confirmYes: true));
    }
}
