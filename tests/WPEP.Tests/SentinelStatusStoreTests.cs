using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class SentinelStatusStoreTests
{
    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"verdict-sentinel-{Guid.NewGuid():N}.json");
        try
        {
            var snap = new SentinelSnapshot("Regressed", "frametime +12% vs baseline", "2026-06-23T10:00:00Z");
            SentinelStatusStore.Save(snap, path);
            var loaded = SentinelStatusStore.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal("Regressed", loaded!.Status);
            Assert.Equal("frametime +12% vs baseline", loaded.Headline);
            Assert.Equal("2026-06-23T10:00:00Z", loaded.CapturedAtIso);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"verdict-sentinel-missing-{Guid.NewGuid():N}.json");
        Assert.Null(SentinelStatusStore.Load(path));
    }
}
