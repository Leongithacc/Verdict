using WPEP.Advisor;
using WPEP.Execution;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

/// <summary>End-to-end of the apply-all orchestration the CLI and GUI perform:
/// ConflictResolver → BuildPlan → ExecuteAll → Undo. The pieces are unit-tested
/// individually; this pins their interaction.</summary>
file sealed class OrchFakeReg : IRegistryAccess
{
    public readonly Dictionary<string, (string Kind, string Value)> Data =
        new(StringComparer.OrdinalIgnoreCase);
    public RegistryValue Read(string p) =>
        Data.TryGetValue(p, out var v) ? new RegistryValue(true, v.Kind, v.Value)
                                       : new RegistryValue(false, "dword", null);
    public void Write(string p, string k, string v) => Data[p] = (k, v);
    public void Delete(string p) => Data.Remove(p);
}

public class ApplyOrchestrationTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"wpep-orch-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static TweakEntry Entry(string id, string path, string after,
        EvidenceLevel ev = EvidenceLevel.Plausible, params string[] conflictsWith) => new()
    {
        Id = id, Name = id, Category = "gpu", Description = "d", ExpectedImpact = "i",
        EvidenceLevel = ev, Sources = ["https://x"], Risk = RiskLevel.Low,
        Rollback = "r", ManualSteps = "m", Measurable = true, ConflictsWith = conflictsWith,
        Apply = new ApplySpec
        {
            Method = "registry",
            Operations = [new ApplyOperation { Path = path, ValueAfter = after, Kind = "dword" }],
        },
    };

    [Fact]
    public void ApplyAll_ThenUndoAll_RoundTripsToOriginalState()
    {
        var reg = new OrchFakeReg();
        reg.Data[@"HKCU\A\V"] = ("dword", "1");
        reg.Data[@"HKCU\B\V"] = ("dword", "1");
        var engine = new ExecutionEngine(reg, _dir);

        var (kept, conflicts) = ConflictResolver.Resolve(
            [Entry("t1", @"HKCU\A\V", "0"), Entry("t2", @"HKCU\B\V", "0")]);
        Assert.Empty(conflicts);

        var plans = kept.Select(engine.BuildPlan).ToList();
        var (applied, stopped) = engine.ExecuteAll(plans);

        Assert.Equal(2, applied);
        Assert.Null(stopped);
        Assert.Equal("0", reg.Data[@"HKCU\A\V"].Value);
        Assert.Equal("0", reg.Data[@"HKCU\B\V"].Value);

        foreach (var f in ExecutionEngine.ListSessions(_dir))
            engine.Undo(f);

        Assert.Equal("1", reg.Data[@"HKCU\A\V"].Value); // back to original
        Assert.Equal("1", reg.Data[@"HKCU\B\V"].Value);
    }

    [Fact]
    public void ApplyAll_AppliesOnlyTheKeptSideOfAConflict()
    {
        var reg = new OrchFakeReg();
        reg.Data[@"HKCU\A\V"] = ("dword", "1");
        reg.Data[@"HKCU\B\V"] = ("dword", "1");
        var engine = new ExecutionEngine(reg, _dir);

        // strong conflicts with weak — only strong should survive and apply.
        var (kept, conflicts) = ConflictResolver.Resolve(
        [
            Entry("strong", @"HKCU\A\V", "0", EvidenceLevel.EvidenceStrong, "weak"),
            Entry("weak", @"HKCU\B\V", "0", EvidenceLevel.Controversial),
        ]);

        Assert.Single(kept);
        Assert.Equal("strong", kept[0].Id);
        Assert.Single(conflicts);

        var (applied, _) = engine.ExecuteAll(kept.Select(engine.BuildPlan).ToList());

        Assert.Equal(1, applied);
        Assert.Equal("0", reg.Data[@"HKCU\A\V"].Value); // strong applied
        Assert.Equal("1", reg.Data[@"HKCU\B\V"].Value); // weak never touched
    }
}
