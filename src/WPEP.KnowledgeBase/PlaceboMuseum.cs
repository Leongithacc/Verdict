namespace WPEP.KnowledgeBase;

/// <summary>One debunked myth-tweak on display: what people claim it does vs the honest truth.</summary>
public sealed record PlaceboExhibit(
    string Id, string Name, string Category, string Myth, string Truth, IReadOnlyList<string> Sources);

/// <summary>Placebo Museum (Lab feature): a gallery of the popular tweaks that DON'T actually work,
/// each with the evidence. "Non ci sono cascato." Turns the project's honesty engine into shareable
/// content. Pure projection over the KB — just the placebo-graded entries, nicely framed.</summary>
public static class PlaceboMuseum
{
    public static IReadOnlyList<PlaceboExhibit> Build(IEnumerable<TweakEntry> entries) =>
        [.. entries
            .Where(e => e.EvidenceLevel == EvidenceLevel.Placebo)
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new PlaceboExhibit(
                Id: e.Id,
                Name: e.Name,
                Category: e.Category,
                // What people believe it does vs what actually happens.
                Myth: string.IsNullOrWhiteSpace(e.ExpectedImpact) ? "Promette più FPS / meno lag." : e.ExpectedImpact,
                Truth: e.Description,
                Sources: e.Sources))];

    public static int Count(IEnumerable<TweakEntry> entries) =>
        entries.Count(e => e.EvidenceLevel == EvidenceLevel.Placebo);
}
