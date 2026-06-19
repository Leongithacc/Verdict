using WPEP.Advisor;
using WPEP.Core.SystemInfo;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class AdvisorEngineTests
{
    private static readonly IReadOnlyList<TweakEntry> Kb =
        KnowledgeBaseLoader.Load(Path.Combine(AppContext.BaseDirectory, "kb", "tweaks.json"));

    private static SystemSnapshot Snapshot(
        bool? pointerPrecision = true,
        string powerPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
        bool? hags = false,
        bool? gameMode = true,
        bool? hvci = true,
        int? currentHz = 144,
        int? maxHz = 144,
        bool? isDesktop = true) => new()
    {
        CapturedAtUtc = DateTimeOffset.UnixEpoch,
        CpuName = "Test CPU",
        GpuName = "Test GPU",
        PointerPrecisionEnabled = pointerPrecision,
        PowerPlanGuid = powerPlanGuid,
        PowerPlanName = "Test plan",
        HagsEnabled = hags,
        GameModeEnabled = gameMode,
        HvciEnabled = hvci,
        MonitorCurrentHz = currentHz,
        MonitorMaxHz = maxHz,
        IsDesktop = isDesktop,
    };

    private static Recommendation For(IReadOnlyList<Recommendation> recs, string id) =>
        recs.Single(r => r.Entry.Id == id);

    [Fact]
    public void Advise_AmdGpuTweaks_NotApplicableOnNvidia()
    {
        // Real Léon case: AMD CPU + NVIDIA gaming GPU → AMD-GPU driver features don't apply.
        var recs = AdvisorEngine.Advise(Snapshot() with { GpuName = "NVIDIA GeForce RTX 5080" }, Kb);
        Assert.Equal(Classification.NotApplicable, For(recs, "amd-radeon-anti-lag").Classification);
        Assert.Equal(Classification.NotApplicable, For(recs, "amd-hypr-rx").Classification);
    }

    [Fact]
    public void Advise_AmdGpuTweaks_ApplicableOnRadeon()
    {
        var recs = AdvisorEngine.Advise(Snapshot() with { GpuName = "AMD Radeon RX 7900 XTX" }, Kb);
        Assert.NotEqual(Classification.NotApplicable, For(recs, "amd-radeon-anti-lag").Classification);
    }

    [Fact]
    public void Advise_LaptopTweak_NotApplicableOnDesktop()
    {
        var recs = AdvisorEngine.Advise(Snapshot(isDesktop: true), Kb);
        Assert.Equal(Classification.NotApplicable, For(recs, "laptop-dgpu-preference").Classification);
    }

    [Fact]
    public void Advise_MonitorBelowMaxRefresh_RefreshTweakIsRecommended()
    {
        var recs = AdvisorEngine.Advise(Snapshot(currentHz: 60, maxHz: 240), Kb);
        var r = For(recs, "correct-refresh-rate-and-fps-cap");

        Assert.Equal(Classification.Recommended, r.Classification);
        Assert.Contains("240", r.StateNote);
    }

    [Fact]
    public void Advise_MonitorAtMaxRefresh_RefreshTweakIsAlreadyActive()
    {
        var recs = AdvisorEngine.Advise(Snapshot(currentHz: 240, maxHz: 240), Kb);
        Assert.Equal(Classification.AlreadyActive,
            For(recs, "correct-refresh-rate-and-fps-cap").Classification);
    }

    [Fact]
    public void Advise_60HzOnlyMonitor_RefreshTweakNotApplicable()
    {
        var recs = AdvisorEngine.Advise(Snapshot(currentHz: 60, maxHz: 60), Kb);
        Assert.Equal(Classification.NotApplicable,
            For(recs, "correct-refresh-rate-and-fps-cap").Classification);
    }

    [Fact]
    public void Advise_PointerPrecisionOn_IsRecommendedToDisable()
    {
        var recs = AdvisorEngine.Advise(Snapshot(pointerPrecision: true), Kb);
        Assert.Equal(Classification.Recommended,
            For(recs, "disable-enhance-pointer-precision").Classification);
    }

    [Fact]
    public void Advise_PointerPrecisionAlreadyOff_IsAlreadyActive()
    {
        var recs = AdvisorEngine.Advise(Snapshot(pointerPrecision: false), Kb);
        Assert.Equal(Classification.AlreadyActive,
            For(recs, "disable-enhance-pointer-precision").Classification);
    }

    [Fact]
    public void Advise_PlaceboEntries_AreClassifiedPlaceboNotRecommended()
    {
        var recs = AdvisorEngine.Advise(Snapshot(), Kb);
        Assert.Equal(Classification.Placebo, For(recs, "dns-change").Classification);
        Assert.Equal(Classification.Placebo, For(recs, "timer-resolution-0.5ms").Classification);
    }

    [Fact]
    public void Advise_RiskyEntries_AreNeverRecommended()
    {
        var recs = AdvisorEngine.Advise(Snapshot(hvci: true), Kb);
        Assert.Equal(Classification.NotRecommended,
            For(recs, "memory-integrity-vbs-off").Classification);
        Assert.Equal(Classification.NotRecommended,
            For(recs, "disable-dynamic-tick").Classification);
    }

    [Fact]
    public void Advise_HvciAlreadyOff_ReportedAsAlreadyActiveWithSecurityNote()
    {
        var recs = AdvisorEngine.Advise(Snapshot(hvci: false), Kb);
        var r = For(recs, "memory-integrity-vbs-off");
        Assert.Equal(Classification.AlreadyActive, r.Classification);
        Assert.Contains("sicurezza", r.StateNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Advise_HighPerfPlanActive_PowerPlanIsAlreadyActive()
    {
        var recs = AdvisorEngine.Advise(
            Snapshot(powerPlanGuid: "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"), Kb);
        Assert.Equal(Classification.AlreadyActive,
            For(recs, "power-plan-high-performance").Classification);
    }

    [Fact]
    public void Advise_Laptop_DesktopOnlyTweaksNotApplicable()
    {
        var recs = AdvisorEngine.Advise(Snapshot(isDesktop: false), Kb);
        Assert.Equal(Classification.NotApplicable,
            For(recs, "power-plan-high-performance").Classification);
        Assert.Equal(Classification.NotApplicable,
            For(recs, "disable-core-parking").Classification);
    }

    [Fact]
    public void Advise_RamBelowRatedSpeed_XmpExpoIsRecommended()
    {
        var snapshot = Snapshot() with { RamSpeedMtps = 4800, RamRatedMtps = 6000 };
        var r = For(AdvisorEngine.Advise(snapshot, Kb), "xmp-expo-enable");
        Assert.Equal(Classification.Recommended, r.Classification);
        Assert.Contains("6000", r.StateNote);
    }

    [Fact]
    public void Advise_RamAtRatedSpeed_XmpExpoAlreadyActive()
    {
        var snapshot = Snapshot() with { RamSpeedMtps = 6000, RamRatedMtps = 6000 };
        Assert.Equal(Classification.AlreadyActive,
            For(AdvisorEngine.Advise(snapshot, Kb), "xmp-expo-enable").Classification);
    }

    [Fact]
    public void Advise_OnWifi_EthernetIsSuggested()
    {
        var snapshot = Snapshot() with { ActiveNicIsWifi = true };
        var r = For(AdvisorEngine.Advise(snapshot, Kb), "ethernet-over-wifi");
        Assert.NotEqual(Classification.AlreadyActive, r.Classification);
        Assert.NotEqual(Classification.NotApplicable, r.Classification);
    }

    [Fact]
    public void Advise_OnEthernet_WifiTweakAlreadyActive()
    {
        var snapshot = Snapshot() with { ActiveNicIsWifi = false };
        Assert.Equal(Classification.AlreadyActive,
            For(AdvisorEngine.Advise(snapshot, Kb), "ethernet-over-wifi").Classification);
    }

    [Fact]
    public void Advise_IntelCpu_AmdOnlyTweaksNotApplicable()
    {
        var snapshot = Snapshot() with { CpuName = "Intel Core i9-14900K" };
        Assert.Equal(Classification.NotApplicable,
            For(AdvisorEngine.Advise(snapshot, Kb), "amd-ftpm-bios-update").Classification);
    }

    [Fact]
    public void Advise_AmdGpu_NvidiaOnlyTweaksNotApplicable()
    {
        var snapshot = Snapshot() with { GpuName = "AMD Radeon RX 9070 XT" };
        Assert.Equal(Classification.NotApplicable,
            For(AdvisorEngine.Advise(snapshot, Kb), "enable-nvidia-reflex").Classification);
    }

    [Fact]
    public void Advise_NvidiaGpu_ReflexIsRecommended()
    {
        var snapshot = Snapshot() with { GpuName = "NVIDIA GeForce RTX 5080" };
        Assert.Equal(Classification.Recommended,
            For(AdvisorEngine.Advise(snapshot, Kb), "enable-nvidia-reflex").Classification);
    }

    [Fact]
    public void Advise_UnknownStates_NeverBecomeAlreadyActive()
    {
        // Snapshot with everything undetectable: the engine must not guess.
        var blind = new SystemSnapshot { CapturedAtUtc = DateTimeOffset.UnixEpoch };
        var recs = AdvisorEngine.Advise(blind, Kb);
        Assert.DoesNotContain(recs, r => r.Classification == Classification.AlreadyActive);
    }

    [Fact]
    public void Advise_EveryKbEntry_GetsExactlyOneRecommendation()
    {
        var recs = AdvisorEngine.Advise(Snapshot(), Kb);
        Assert.Equal(Kb.Count, recs.Count);
        Assert.Equal(Kb.Count, recs.Select(r => r.Entry.Id).Distinct().Count());
    }
}
