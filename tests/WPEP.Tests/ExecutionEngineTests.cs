using WPEP.Execution;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

/// <summary>In-memory registry: the engine must be provably correct without
/// ever touching the real one in tests.</summary>
file sealed class FakeRegistry : IRegistryAccess
{
    public readonly Dictionary<string, (string Kind, string Value)> Data =
        new(StringComparer.OrdinalIgnoreCase);
    public bool FailWrites;

    public RegistryValue Read(string path) =>
        Data.TryGetValue(path, out var v)
            ? new RegistryValue(true, v.Kind, v.Value)
            : new RegistryValue(false, "dword", null);

    public void Write(string path, string kind, string value)
    {
        if (!FailWrites)
            Data[path] = (kind, value);
    }

    public void Delete(string path) => Data.Remove(path);
}

/// <summary>In-memory power scheme: never touches the real active plan.</summary>
file sealed class FakePowerCfg : IPowerCfg
{
    public string Active = "381b4222-f694-41f0-9685-ff5bb260df2e"; // Balanced
    public readonly Dictionary<string, int> Values = new();
    public string GetActiveScheme() => Active;
    public void SetActiveScheme(string guid) => Active = guid.ToLowerInvariant();
    public int QuerySettingIndex(string subgroup, string setting) =>
        Values.TryGetValue($"{subgroup}/{setting}", out var v) ? v : 0;
    public void SetSettingIndex(string subgroup, string setting, int index) =>
        Values[$"{subgroup}/{setting}"] = index;
}

public class ExecutionEngineTests : IDisposable
{
    private readonly string _journalDir =
        Path.Combine(Path.GetTempPath(), $"wpep-journal-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_journalDir))
            Directory.Delete(_journalDir, recursive: true);
    }

    private static TweakEntry Entry(string id = "test-tweak", ApplySpec? apply = null,
        EvidenceLevel evidence = EvidenceLevel.Plausible) => new()
    {
        Id = id,
        Name = "Test",
        Category = "background",
        Description = "d",
        ExpectedImpact = "i",
        EvidenceLevel = evidence,
        Sources = ["https://x"],
        Risk = RiskLevel.Low,
        Rollback = "r",
        ManualSteps = "m",
        Measurable = true,
        Apply = apply ?? new ApplySpec
        {
            Method = "registry",
            Operations =
            [
                new ApplyOperation { Path = @"HKCU\Test\Key\ValueA", ValueAfter = "0" },
                new ApplyOperation { Path = @"HKCU\Test\Key\ValueB", ValueAfter = "1" },
            ],
        },
    };

    [Fact]
    public void BuildPlan_CapturesLiveBeforeValues()
    {
        var registry = new FakeRegistry();
        registry.Data[@"HKCU\Test\Key\ValueA"] = ("dword", "1"); // exists
        var engine = new ExecutionEngine(registry, _journalDir);

        var plan = engine.BuildPlan(Entry());

        Assert.Equal("1", plan.Operations[0].Before);
        Assert.False(plan.Operations[1].ExistedBefore); // ValueB not set
    }

    [Fact]
    public void PowerCfg_Apply_SwitchesSchemeAndJournals()
    {
        var power = new FakePowerCfg();
        var engine = new ExecutionEngine(new FakeRegistry(), _journalDir, power);
        var entry = Entry(apply: new ApplySpec
        {
            Method = "powercfg",
            Operations = [new ApplyOperation
                { Path = "active-scheme", ValueAfter = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", Kind = "powercfg" }],
        });

        var plan = engine.BuildPlan(entry);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", plan.Operations[0].Before);

        var file = engine.Execute(plan);
        Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", power.Active);

        engine.Undo(file);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", power.Active); // restored
    }

    [Fact]
    public void BuildPlan_PlaceboEntry_Refuses()
    {
        var engine = new ExecutionEngine(new FakeRegistry(), _journalDir);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            engine.BuildPlan(Entry(evidence: EvidenceLevel.Placebo)));
        Assert.Contains("placebo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPlan_GuiOnly_Refuses()
    {
        var engine = new ExecutionEngine(new FakeRegistry(), _journalDir);
        var entry = Entry(apply: new ApplySpec
        { Method = "gui-only", GuiOnlyReason = "BIOS" });
        Assert.Throws<InvalidOperationException>(() => engine.BuildPlan(entry));
    }

    [Fact]
    public void Execute_WritesVerifiesAndJournals()
    {
        var registry = new FakeRegistry();
        registry.Data[@"HKCU\Test\Key\ValueA"] = ("dword", "1");
        var engine = new ExecutionEngine(registry, _journalDir);

        var file = engine.Execute(engine.BuildPlan(Entry()));

        Assert.Equal(("dword", "0"), registry.Data[@"HKCU\Test\Key\ValueA"]);
        Assert.Equal(("dword", "1"), registry.Data[@"HKCU\Test\Key\ValueB"]);
        Assert.True(File.Exists(file));
        Assert.Contains("\"Verified\": true", File.ReadAllText(file));
    }

    [Fact]
    public void Execute_VerifyFailure_StopsImmediately()
    {
        var registry = new FakeRegistry { FailWrites = true };
        var engine = new ExecutionEngine(registry, _journalDir);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            engine.Execute(engine.BuildPlan(Entry())));

        Assert.Contains("VERIFY", ex.Message);
        // The journal still recorded the attempt: nothing is lost.
        Assert.Single(ExecutionEngine.ListSessions(_journalDir));
    }

    [Fact]
    public void PowerCfgValue_Apply_SetsIndexAndUndoRestores()
    {
        var power = new FakePowerCfg();
        power.Values["sub/setting"] = 0; // current: parking allowed
        var engine = new ExecutionEngine(new FakeRegistry(), _journalDir, power);
        var entry = Entry(apply: new ApplySpec
        {
            Method = "powercfg-value",
            Operations = [new ApplyOperation
                { Path = "sub/setting", ValueAfter = "100", Kind = "powercfg-value" }],
        });

        var plan = engine.BuildPlan(entry);
        Assert.Equal("0", plan.Operations[0].Before);

        var file = engine.Execute(plan);
        Assert.Equal(100, power.Values["sub/setting"]); // applied

        engine.Undo(file);
        Assert.Equal(0, power.Values["sub/setting"]); // restored
    }

    [Fact]
    public void Undo_RestoresPreviousValuesAndDeletesWhatDidNotExist()
    {
        var registry = new FakeRegistry();
        registry.Data[@"HKCU\Test\Key\ValueA"] = ("dword", "1"); // existed
        var engine = new ExecutionEngine(registry, _journalDir);
        var file = engine.Execute(engine.BuildPlan(Entry()));

        int restored = engine.Undo(file);

        Assert.Equal(2, restored);
        Assert.Equal(("dword", "1"), registry.Data[@"HKCU\Test\Key\ValueA"]); // restored
        Assert.False(registry.Data.ContainsKey(@"HKCU\Test\Key\ValueB"));     // deleted
    }

    [Fact]
    public void Undo_Twice_IsIdempotent()
    {
        var registry = new FakeRegistry();
        var engine = new ExecutionEngine(registry, _journalDir);
        var file = engine.Execute(engine.BuildPlan(Entry()));

        engine.Undo(file);
        int second = engine.Undo(file);

        Assert.Equal(0, second);
    }

    [Fact]
    public void ShippedKb_AllProgrammaticRegistryEntries_ProducePlans()
    {
        var registry = new FakeRegistry();
        var engine = new ExecutionEngine(registry, _journalDir);
        var kb = KnowledgeBaseLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "kb", "tweaks.json"));

        var programmatic = kb.Where(e =>
            e.Apply is { Method: "registry" } && e.EvidenceLevel != EvidenceLevel.Placebo);

        foreach (var entry in programmatic)
        {
            var plan = engine.BuildPlan(entry);
            Assert.NotEmpty(plan.Operations);
            Assert.All(plan.Operations, o => Assert.Matches("^(dword|string)$", o.Kind));
        }
    }
}
