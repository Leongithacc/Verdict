using WPEP.Execution;
using Xunit;

namespace WPEP.Tests;

public class ProfileStoreTests
{
    [Fact]
    public void Defaults_HasCuratedBuiltIns()
    {
        var d = ProfileStore.Defaults;
        Assert.Contains(d, p => p.Name == "Competitive" && p.BuiltIn);
        Assert.Contains(d, p => p.Name == "Streaming");
        Assert.Contains(d, p => p.Name == "Daily");
        Assert.All(d, p => Assert.NotEmpty(p.TweakIds));
    }

    [Fact]
    public void All_IncludesDefaults()
    {
        Assert.Contains(ProfileStore.All(), p => p.Name == "Competitive");
    }

    [Fact]
    public void SaveGetDelete_UserProfile_RoundTrips()
    {
        var name = "utest-" + Guid.NewGuid().ToString("N");
        try
        {
            ProfileStore.Save(new TweakProfile(name, ["a", "b"], "test"));

            var got = ProfileStore.Get(name);
            Assert.NotNull(got);
            Assert.Equal(["a", "b"], got!.TweakIds);
            Assert.False(got.BuiltIn);

            Assert.True(ProfileStore.Delete(name));
            Assert.Null(ProfileStore.Get(name));
        }
        finally
        {
            ProfileStore.Delete(name);
        }
    }
}
