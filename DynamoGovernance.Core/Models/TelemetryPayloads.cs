using System.Text.Json.Serialization;

namespace DynamoGovernance.Core.Models;

public sealed class SessionStartedData
{
    [JsonPropertyName("identity")]
    public required IdentityContext Identity { get; init; }

    [JsonPropertyName("application")]
    public required ApplicationContext Application { get; init; }
}

public sealed class NodeCountData
{
    [JsonPropertyName("node_count")]
    public int NodeCount { get; init; }
}

public sealed class ExecutionCompletedData
{
    [JsonPropertyName("node_count")]
    public int NodeCount { get; init; }

    [JsonPropertyName("successful")]
    public bool Successful { get; init; }
}
