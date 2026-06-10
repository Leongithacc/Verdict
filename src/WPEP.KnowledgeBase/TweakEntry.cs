using System.Text.Json.Serialization;

namespace WPEP.KnowledgeBase;

[JsonConverter(typeof(JsonStringEnumConverter<EvidenceLevel>))]
public enum EvidenceLevel
{
    [JsonStringEnumMemberName("evidence_strong")] EvidenceStrong,
    [JsonStringEnumMemberName("plausible")] Plausible,
    [JsonStringEnumMemberName("controversial")] Controversial,
    [JsonStringEnumMemberName("placebo")] Placebo,
    [JsonStringEnumMemberName("risky")] Risky,
}

[JsonConverter(typeof(JsonStringEnumConverter<RiskLevel>))]
public enum RiskLevel
{
    [JsonStringEnumMemberName("none")] None,
    [JsonStringEnumMemberName("low")] Low,
    [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("high")] High,
}

/// <summary>One knowledge base entry, schema per spec §5.</summary>
public sealed record TweakEntry
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("category")] public required string Category { get; init; }
    [JsonPropertyName("description")] public required string Description { get; init; }
    [JsonPropertyName("hardware_prerequisites")] public IReadOnlyList<string> HardwarePrerequisites { get; init; } = [];
    [JsonPropertyName("expected_impact")] public required string ExpectedImpact { get; init; }
    [JsonPropertyName("evidence_level")] public required EvidenceLevel EvidenceLevel { get; init; }
    [JsonPropertyName("sources")] public IReadOnlyList<string> Sources { get; init; } = [];
    [JsonPropertyName("risk")] public required RiskLevel Risk { get; init; }
    [JsonPropertyName("risk_notes")] public string RiskNotes { get; init; } = "";
    [JsonPropertyName("rollback")] public required string Rollback { get; init; }
    [JsonPropertyName("manual_steps")] public required string ManualSteps { get; init; }
    [JsonPropertyName("conflicts_with")] public IReadOnlyList<string> ConflictsWith { get; init; } = [];
    [JsonPropertyName("measurable")] public required bool Measurable { get; init; }
}
