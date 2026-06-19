using WPEP.Core.SystemInfo;
using WPEP.KnowledgeBase;

namespace WPEP.Advisor;

/// <summary>A one-click optimization plan for a specific game: the system tweaks worth applying
/// plus the in-game/driver settings to set by hand for THAT title.</summary>
public sealed record GameOptimization(
    string Game,
    IReadOnlyList<TweakEntry> SystemTweaks,
    IReadOnlyList<TweakEntry> InGameSettings);

/// <summary>Optimize for [game] (Lab feature): pick a title and Verdict gathers the evidence-backed
/// system tweaks plus the game-specific guidance from the KB. Pure projection over the KB — no
/// system access — so it's unit-tested and identical in GUI and CLI.</summary>
public static class OptimizeForGame
{
    /// <summary>Games that have at least one dedicated KB entry, alphabetised.</summary>
    public static IReadOnlyList<string> AvailableGames(IEnumerable<TweakEntry> entries) =>
        [.. entries.Where(e => e.Game is { Length: > 0 })
            .Select(e => e.Game!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)];

    public static GameOptimization Build(string game, IEnumerable<TweakEntry> entries,
        SystemSnapshot? snapshot = null)
    {
        var all = entries.ToList();

        // System tweaks worth applying: system-wide (no game), strong-or-plausible, never placebo/risky.
        var system = all
            .Where(e => e.Game is null &&
                        e.EvidenceLevel is EvidenceLevel.EvidenceStrong or EvidenceLevel.Plausible)
            .OrderBy(e => e.EvidenceLevel)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // When we know the rig, drop tweaks that don't apply to it (e.g. AMD-GPU tweaks on an
        // NVIDIA gamer, or laptop-only tweaks on a desktop) — that's what makes it "tailored".
        if (snapshot is not null)
        {
            var applicable = AdvisorEngine.Advise(snapshot, all)
                .Where(r => r.Classification != Classification.NotApplicable)
                .Select(r => r.Entry.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            system = system.Where(e => applicable.Contains(e.Id)).ToList();
        }

        // In-game / driver settings specific to this title.
        var inGame = all
            .Where(e => e.Game is { } g && g.Equals(game, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.EvidenceLevel)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GameOptimization(game, system, inGame);
    }
}
