using System.Text.Json;
using WPEP.Core.Io;
using Xunit;

namespace WPEP.Tests;

public class AtomicJsonTests
{
    private sealed record Sample(string Name, int Value);

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"wpep-atomic-{Guid.NewGuid():N}.json");

    [Fact]
    public void Write_then_read_round_trips()
    {
        var path = TempPath();
        try
        {
            AtomicJson.Write(path, new Sample("a", 1));
            var back = JsonSerializer.Deserialize<Sample>(File.ReadAllText(path));
            Assert.Equal(new Sample("a", 1), back);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Write_overwrites_existing_and_leaves_no_tmp()
    {
        var path = TempPath();
        try
        {
            AtomicJson.Write(path, new Sample("first", 1));
            AtomicJson.Write(path, new Sample("second", 2));
            var back = JsonSerializer.Deserialize<Sample>(File.ReadAllText(path));
            Assert.Equal(new Sample("second", 2), back);
            Assert.False(File.Exists(path + ".tmp"), "il file temp non deve restare sul disco");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Write_creates_missing_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"wpep-atomic-dir-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "nested", "settings.json");
        try
        {
            AtomicJson.Write(path, new Sample("x", 9));
            Assert.True(File.Exists(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
