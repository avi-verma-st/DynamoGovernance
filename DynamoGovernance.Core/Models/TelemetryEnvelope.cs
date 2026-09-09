using System.Text.Json.Serialization;

namespace DynamoGovernance.Core.Models;

public sealed class TelemetryRecord
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "2.0";

    [JsonPropertyName("event")]
    public required string Event { get; init; }

    [JsonPropertyName("session_id")]
    public required Guid SessionId { get; init; }

    [JsonPropertyName("occurred_utc")]
    public required DateTimeOffset OccurredUtc { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

public sealed class IdentityContext
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("user_id_source")]
    public string UserIdSource { get; init; } = "windows_account";

    [JsonPropertyName("machine_id")]
    public required string MachineId { get; init; }

    [JsonPropertyName("machine_id_source")]
    public string MachineIdSource { get; init; } = "machine_name";

    [JsonPropertyName("machine_id_collected")]
    public bool MachineIdCollected { get; init; } = true;

    [JsonPropertyName("identifiers_protected")]
    public bool IdentifiersProtected { get; init; }
}

public sealed class ApplicationContext
{
    [JsonPropertyName("host_name")]
    public required string HostName { get; init; }

    [JsonPropertyName("host_version")]
    public string? HostVersion { get; init; }

    [JsonPropertyName("dynamo_version")]
    public required string DynamoVersion { get; init; }

    [JsonPropertyName("extension_version")]
    public required string ExtensionVersion { get; init; }

    [JsonPropertyName("process_id")]
    public int ProcessId { get; init; } = Environment.ProcessId;

    [JsonPropertyName("process_architecture")]
    public required string ProcessArchitecture { get; init; }

    [JsonPropertyName("runtime_version")]
    public required string RuntimeVersion { get; init; }
}

