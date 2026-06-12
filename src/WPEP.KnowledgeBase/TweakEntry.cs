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

    /// <summary>Null = system-wide. Set (e.g. "fortnite") for per-game entries:
    /// shown in their own section, excluded from the system Verdict counts.</summary>
    [JsonPropertyName("game")] public string? Game { get; init; }

    /// <summary>V2 Execution Engine spec (EXECUTION_ENGINE_V2 §3): how to apply
    /// this tweak programmatically. Null in V1 — the field exists now so the
    /// schema never needs a migration. Entries that can only be applied by hand
    /// (in-game settings, BIOS) stay gui-only forever.</summary>
    [JsonPropertyName("apply")] public ApplySpec? Apply { get; init; }
}

public sealed record ApplySpec
{
    [JsonPropertyName("method")] public required string Method { get; init; } // registry|powercfg|bcdedit|service|gui-only
    [JsonPropertyName("operations")] public IReadOnlyList<ApplyOperation> Operations { get; init; } = [];
    [JsonPropertyName("requires_reboot")] public bool RequiresReboot { get; init; }
    [JsonPropertyName("undo")] public string Undo { get; init; } = "auto-journal";
    [JsonPropertyName("gui_only_reason")] public string? GuiOnlyReason { get; init; }
}

public sealed record ApplyOperation
{
    [JsonPropertyName("path")] public required string Path { get; init; }
    [JsonPropertyName("value_after")] public string? ValueAfter { get; init; }
    [JsonPropertyName("verify")] public string? Verify { get; init; }
}
