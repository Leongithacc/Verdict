namespace WPEP.Execution;

/// <summary>How far the user is willing to go. A single knob that maps to which tweaks Verdict
/// puts "in scope". Ordered so the int value == the highest risk tier it accepts.</summary>
public enum RiskTolerance
{
    Safe = 0,        // zero-risk tweaks only
    Balanced = 1,    // + low risk (the normal recommended set)
    Aggressive = 2,  // + medium risk (controversial)
    Extreme = 3,     // + high risk (everything, with warnings) — never placebos
}

/// <summary>Human description of a tolerance level for the UI.</summary>
public sealed record RiskProfile(RiskTolerance Level, string Name, string Tagline, string Color);

/// <summary>The Risk Slider (Lab feature): one knob safe ↔ extreme that decides which tweaks are
/// in scope, by their inherent risk tier. Pure/deterministic and KB-agnostic — the caller maps a
/// tweak's KB <c>RiskLevel</c> ordinal (None=0..High=3) onto <see cref="Includes"/>. Placebos are
/// NEVER in scope at any level: the slider widens how RISKY you'll go, never how USELESS.</summary>
public static class RiskSlider
{
    /// <summary>Is a tweak in scope at this tolerance? <paramref name="riskTier"/> is the KB risk
    /// ordinal (0=none,1=low,2=medium,3=high). Placebos are always excluded (honesty rule).</summary>
    public static bool Includes(RiskTolerance tolerance, int riskTier, bool isPlacebo)
    {
        if (isPlacebo) return false;
        return riskTier <= (int)tolerance;
    }

    public static RiskProfile Describe(RiskTolerance t) => t switch
    {
        RiskTolerance.Safe => new(t, "Sicuro",
            "Solo tweak a rischio nullo. Niente di reversibile-male, niente sorprese.", "Ok"),
        RiskTolerance.Balanced => new(t, "Bilanciato",
            "Il set consigliato: rischio basso, evidenza solida. La scelta giusta per quasi tutti.", "Ok"),
        RiskTolerance.Aggressive => new(t, "Aggressivo",
            "Aggiunge i tweak controversi a rischio medio. Più margine, più attenzione.", "Warn"),
        _ => new(t, "Estremo",
            "Tutto tranne i placebo, inclusi i tweak rischiosi. Solo se sai cosa fai.", "Danger"),
    };

    public static IReadOnlyList<RiskProfile> AllProfiles { get; } =
        [.. Enum.GetValues<RiskTolerance>().Select(Describe)];
}
