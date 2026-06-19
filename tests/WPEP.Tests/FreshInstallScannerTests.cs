using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class FreshInstallScannerTests
{
    private static StartupItem Item(string name, string cmd) =>
        new(name, cmd, "HKCU\\...\\Run", FreshInstallScanner.IsMicrosoft(cmd, name));

    [Theory]
    [InlineData(@"C:\Windows\System32\SecurityHealthSystray.exe", true)]
    [InlineData(@"C:\Program Files\Microsoft\Edge\msedge.exe", true)]
    [InlineData(@"C:\Program Files\Steam\steam.exe", false)]
    [InlineData(@"C:\Users\leon\AppData\Local\Discord\Update.exe", false)]
    public void IsMicrosoft_ClassifiesByPath(string cmd, bool expected) =>
        Assert.Equal(expected, FreshInstallScanner.IsMicrosoft(cmd, "x"));

    [Fact]
    public void CleanStartup_Scores100()
    {
        var r = FreshInstallScanner.Analyze([
            Item("SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe"),
        ]);
        Assert.Equal(100, r.Score);
        Assert.Equal(0, r.ThirdPartyCount);
        Assert.Equal("Pulito", r.Band);
    }

    [Fact]
    public void ThirdPartyEntries_LowerTheScore()
    {
        var r = FreshInstallScanner.Analyze([
            Item("Steam", @"C:\Program Files\Steam\steam.exe"),
            Item("Discord", @"C:\Users\x\AppData\Local\Discord\app.exe"),
            Item("Edge", @"C:\Program Files\Microsoft\Edge\msedge.exe"),
        ]);
        Assert.Equal(2, r.ThirdPartyCount);
        Assert.Equal(1, r.MicrosoftCount);
        Assert.Equal(100 - 2 * 6, r.Score);
    }

    [Fact]
    public void Score_IsClamped()
    {
        var many = Enumerable.Range(0, 50)
            .Select(i => Item($"app{i}", $@"C:\Apps\app{i}.exe")).ToList();
        Assert.InRange(FreshInstallScanner.Analyze(many).Score, 0, 100);
    }

    [Fact]
    public void ThirdParty_ListExcludesMicrosoft()
    {
        var r = FreshInstallScanner.Analyze([
            Item("Steam", @"C:\Program Files\Steam\steam.exe"),
            Item("Edge", @"C:\Program Files\Microsoft\Edge\msedge.exe"),
        ]);
        Assert.Single(r.ThirdParty);
        Assert.Equal("Steam", r.ThirdParty[0].Name);
    }
}
