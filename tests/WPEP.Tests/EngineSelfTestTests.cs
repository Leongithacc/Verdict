using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

/// <summary>In-memory registry for the self-test orchestration (never the real one).</summary>
file sealed class SelfTestFakeReg : IRegistryAccess
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

public class EngineSelfTestTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"verdict-selftest-utest-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Run_OnHealthyRegistry_PassesAndLeavesNoResidue()
    {
        var reg = new SelfTestFakeReg();

        var result = EngineSelfTest.Run(reg, _dir);

        Assert.True(result.Passed);
        Assert.Equal(3, result.Steps.Count);
        Assert.All(result.Steps, s => Assert.True(s.Ok));
        Assert.False(reg.Read(EngineSelfTest.ScratchPath).Exists); // scratch value removed
    }

    [Fact]
    public void Run_WhenWritesDoNotStick_Fails()
    {
        var reg = new SelfTestFakeReg { FailWrites = true };

        var result = EngineSelfTest.Run(reg, _dir);

        Assert.False(result.Passed);
    }
}
