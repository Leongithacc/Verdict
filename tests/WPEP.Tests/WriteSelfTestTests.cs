using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class WriteSelfTestTests
{
    // The verdict logic must treat SKIP as "not a failure" (no admin / no NVIDIA is fine),
    // and fail only on a real FAIL. The probes themselves do live I/O and aren't unit-run here.
    [Fact]
    public void AllOk_TrueWhenOnlyPassAndSkip()
    {
        IReadOnlyList<WriteProbe> probes =
        [
            new("registry", ProbeStatus.Pass, ""),
            new("bcdedit", ProbeStatus.Skip, "no admin"),
            new("nvidia-drs", ProbeStatus.Skip, "no nvidia"),
        ];
        Assert.True(WriteSelfTest.AllOk(probes));
    }

    [Fact]
    public void AllOk_FalseOnAnyFail()
    {
        IReadOnlyList<WriteProbe> probes =
        [
            new("registry", ProbeStatus.Pass, ""),
            new("powercfg", ProbeStatus.Fail, "boom"),
        ];
        Assert.False(WriteSelfTest.AllOk(probes));
    }
}
