using System.Text.Json.Serialization;

namespace DynamoGovernance.Core.Models;

public sealed class SessionStartedData
{
    [JsonPropertyName("identity")]
    public required IdentityContext Identity { get; init; }

    [JsonPropertyName("application")]
    public required ApplicationContext Application { get; init; }
}

public sealed class GraphEventData
{
    [JsonPropertyName("graph_id")]
    public Guid GraphId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    [JsonPropertyName("file_size_bytes")]
    public long? FileSizeBytes { get; init; }

    [JsonPropertyName("is_saved")]
    public bool IsSaved { get; init; }

    [JsonPropertyName("has_unsaved_changes")]
    public bool HasUnsavedChanges { get; init; }

    [JsonPropertyName("node_count")]
    public int NodeCount { get; init; }

    [JsonPropertyName("packages")]
    public IReadOnlyList<PackageReferenceData> Packages { get; init; } = [];
}

public sealed class PackageReferenceData
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("node_count")]
    public int NodeCount { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("license")]
    public string? License { get; init; }

    [JsonPropertyName("repository_url")]
    public string? RepositoryUrl { get; init; }

    [JsonPropertyName("site_url")]
    public string? SiteUrl { get; init; }
}

public sealed class ExecutionEndedData
{
    [JsonPropertyName("graph")]
    public required GraphEventData Graph { get; init; }

    [JsonPropertyName("evaluation_took_place")]
    public bool EvaluationTookPlace { get; init; }

    [JsonPropertyName("successful")]
    public bool Successful { get; init; }
}
