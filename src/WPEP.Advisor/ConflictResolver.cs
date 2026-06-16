using WPEP.KnowledgeBase;

namespace WPEP.Advisor;

/// <summary>Guards batch apply ("Apply all") against applying two mutually-exclusive
/// tweaks. The KB declares conflicts via <c>conflicts_with</c>; this resolves a selected
/// set down to a conflict-free one. Conflict is UNDIRECTED: A and B conflict if either
/// lists the other. When two conflict, the stronger evidence wins (lower EvidenceLevel
/// ordinal: evidence_strong &lt; plausible &lt; controversial); ties keep input order.</summary>
public static class ConflictResolver
{
    public sealed record Dropped(TweakEntry Entry, TweakEntry KeptInstead, string Reason);

    public static (IReadOnlyList<TweakEntry> Keep, IReadOnlyList<Dropped> DroppedItems)
        Resolve(IReadOnlyList<TweakEntry> selected)
    {
        var dropped = new Dictionary<string, Dropped>(StringComparer.OrdinalIgnoreCase);

        static bool Conflicts(TweakEntry a, TweakEntry b) =>
            a.ConflictsWith.Contains(b.Id, StringComparer.OrdinalIgnoreCase) ||
            b.ConflictsWith.Contains(a.Id, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < selected.Count; i++)
        {
            var a = selected[i];
            if (dropped.ContainsKey(a.Id))
                continue;
            for (int j = i + 1; j < selected.Count; j++)
            {
                var b = selected[j];
                if (dropped.ContainsKey(b.Id) || !Conflicts(a, b))
                    continue;
                // Weaker evidence (higher ordinal) loses; on a tie the later one (b) loses.
                var (winner, loser) = a.EvidenceLevel <= b.EvidenceLevel ? (a, b) : (b, a);
                dropped[loser.Id] = new Dropped(loser, winner,
                    $"in conflitto con {winner.Id} (evidenza più forte o pari) — applico solo {winner.Id}");
                if (loser.Id == a.Id)
                    break; // a itself was dropped: stop comparing it, move to next i
            }
        }

        var keep = selected.Where(e => !dropped.ContainsKey(e.Id)).ToList();
        return (keep, dropped.Values.ToList());
    }
}
