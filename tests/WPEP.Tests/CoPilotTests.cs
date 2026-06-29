using WPEP.Advisor;
using WPEP.Advisor.CoPilot;
using WPEP.KnowledgeBase;
using Xunit;

namespace WPEP.Tests;

public class CoPilotTests
{
    private static TweakEntry Entry(string id, EvidenceLevel ev, string? game) => new()
    {
        Id = id,
        Name = $"Nome {id}",
        Category = "test",
        Description = "d",
        ExpectedImpact = "meno input lag",
        EvidenceLevel = ev,
        Risk = RiskLevel.None,
        Rollback = "r",
        ManualSteps = "m",
        Measurable = true,
        Game = game,
    };

    private static Recommendation Rec(string id, Classification c,
        EvidenceLevel ev = EvidenceLevel.EvidenceStrong, string? game = null)
        => new(Entry(id, ev, game), c, "stato");

    private sealed class FakeBrain(string canned) : ICoPilotBrain
    {
        public string Name => "fake";
        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> CompleteAsync(string s, string u, CancellationToken ct = default)
            => Task.FromResult(canned);
    }

    private sealed class ThrowingBrain : ICoPilotBrain
    {
        public string Name => "throwing";
        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> CompleteAsync(string s, string u, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void BuildCatalog_lists_ids_forbids_inventing_and_marks_placebo()
    {
        var s = CoPilotGrounding.BuildCatalog(
            [Rec("a", Classification.Recommended), Rec("b", Classification.Placebo, EvidenceLevel.Placebo)]);
        Assert.Contains("[a]", s);
        Assert.Contains("[b]", s);
        Assert.Contains("usa SOLO questi id", s);
        Assert.Contains("PLACEBO", s);
    }

    [Fact]
    public void BuildCatalog_excludes_not_applicable_entries()
    {
        var s = CoPilotGrounding.BuildCatalog(
            [Rec("a", Classification.Recommended), Rec("z", Classification.NotApplicable)]);
        Assert.Contains("[a]", s);
        Assert.DoesNotContain("[z]", s);
    }

    [Fact]
    public void ParseReply_keeps_only_real_ids_and_strips_the_tweaks_line()
    {
        Recommendation[] cat = [Rec("a", Classification.Recommended), Rec("b", Classification.Optional)];
        var reply = CoPilotGrounding.ParseReply("Ecco cosa farei.\nTWEAKS: a, inventato-xyz, b", cat);
        Assert.Equal("Ecco cosa farei.", reply.Answer);
        Assert.DoesNotContain("TWEAKS", reply.Answer);
        Assert.Collection(reply.Suggestions,
            x => Assert.Equal("a", x.TweakId),
            x => Assert.Equal("b", x.TweakId));   // 'inventato-xyz' scartato
    }

    [Fact]
    public void ParseReply_dedups_case_insensitive()
    {
        var reply = CoPilotGrounding.ParseReply("ok\nTWEAKS: a, A, a", [Rec("a", Classification.Recommended)]);
        Assert.Single(reply.Suggestions);
    }

    [Fact]
    public async Task Service_returns_grounded_reply_dropping_hallucinations()
    {
        var svc = new CoPilotService(new FakeBrain("Spiegazione.\nTWEAKS: a, hallucinated"));
        var reply = await svc.AskAsync("rendi fluido", [Rec("a", Classification.Recommended)]);
        Assert.Null(reply.Error);
        Assert.Single(reply.Suggestions);
        Assert.Equal("a", reply.Suggestions[0].TweakId);
    }

    [Fact]
    public async Task Service_never_throws_a_brain_error_becomes_a_message()
    {
        var svc = new CoPilotService(new ThrowingBrain());
        var reply = await svc.AskAsync("x", [Rec("a", Classification.Recommended)]);
        Assert.NotNull(reply.Error);
        Assert.Empty(reply.Suggestions);
    }

    // ── ClaudeBrain smoke ────────────────────────────────────────────────
    // Nessuna chiamata di rete: tutto deterministico off-line.

    [Fact]
    public void ClaudeBrain_default_model_is_sonnet_4_6()
    {
        var b = new ClaudeBrain("sk-test");
        Assert.Contains("claude-sonnet-4-6", b.Name);
        Assert.Contains("cloud", b.Name);
    }

    [Fact]
    public void ClaudeBrain_explicit_model_wins_over_default()
    {
        var b = new ClaudeBrain("sk-test", "claude-opus-4-8");
        Assert.Contains("claude-opus-4-8", b.Name);
        Assert.DoesNotContain("sonnet", b.Name);
    }

    [Fact]
    public async Task ClaudeBrain_unavailable_when_apikey_empty()
    {
        // Empty key ⇒ short-circuit a false senza toccare la rete.
        Assert.False(await new ClaudeBrain("").IsAvailableAsync());
        Assert.False(await new ClaudeBrain("   ").IsAvailableAsync());
    }

    // ── GeminiBrain smoke (offline) ──────────────────────────────────────

    [Fact]
    public void GeminiBrain_default_model_is_2_5_pro()
    {
        var b = new GeminiBrain("k-test");
        Assert.Contains("gemini-2.5-pro", b.Name);
        Assert.Contains("cloud", b.Name);
    }

    [Fact]
    public void GeminiBrain_explicit_model_wins()
    {
        var b = new GeminiBrain("k-test", "gemini-2.5-flash");
        Assert.Contains("gemini-2.5-flash", b.Name);
        Assert.DoesNotContain("pro", b.Name);
    }

    [Fact]
    public async Task GeminiBrain_unavailable_when_apikey_empty()
    {
        Assert.False(await new GeminiBrain("").IsAvailableAsync());
        Assert.False(await new GeminiBrain(null).IsAvailableAsync());
    }

    // ── OpenAiBrain smoke (offline) ──────────────────────────────────────

    [Fact]
    public void OpenAiBrain_default_model_is_gpt5()
    {
        var b = new OpenAiBrain("sk-test");
        Assert.Contains("gpt-5", b.Name);
        Assert.Contains("cloud", b.Name);
    }

    [Fact]
    public void OpenAiBrain_explicit_model_wins()
    {
        var b = new OpenAiBrain("sk-test", "gpt-4o-2024-08-06");
        Assert.Contains("gpt-4o", b.Name);
    }

    [Fact]
    public async Task OpenAiBrain_unavailable_when_apikey_empty()
    {
        Assert.False(await new OpenAiBrain("").IsAvailableAsync());
        Assert.False(await new OpenAiBrain(null).IsAvailableAsync());
    }
}
