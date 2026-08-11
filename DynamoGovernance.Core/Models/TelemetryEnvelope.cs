using System.Text.Json.Serialization;

namespace DynamoGovernance.Core.Models;

public sealed class TelemetryEnvelope<TPayload>
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    [JsonPropertyName("event_version")]
    public string EventVersion { get; init; } = "1.0";

    [JsonPropertyName("event_id")]
    public Guid EventId { get; init; } = Guid.NewGuid();

    [JsonPropertyName("session_id")]
    public required Guid SessionId { get; init; }

    [JsonPropertyName("sequence_number")]
    public required long SequenceNumber { get; init; }

    [JsonPropertyName("correlation")]
    public required EventCorrelation Correlation { get; init; }

    [JsonPropertyName("timing")]
    public required EventTiming Timing { get; init; }

    [JsonPropertyName("identity")]
    public required IdentityContext Identity { get; init; }

    [JsonPropertyName("application")]
    public required ApplicationContext Application { get; init; }

    [JsonPropertyName("result")]
    public required EventResult Result { get; init; }

    [JsonPropertyName("payload")]
    public required TPayload Payload { get; init; }

    [JsonPropertyName("telemetry")]
    public required TelemetryMetadata Telemetry { get; init; }
}

public sealed class EventCorrelation
{
    [JsonPropertyName("correlation_id")]
    public required Guid CorrelationId { get; init; }

    [JsonPropertyName("causation_event_id")]
    public Guid? CausationEventId { get; init; }
}

public sealed class EventTiming
{
    [JsonPropertyName("occurred_utc")]
    public required DateTimeOffset OccurredUtc { get; init; }

    [JsonPropertyName("started_utc")]
    public DateTimeOffset? StartedUtc { get; init; }

    [JsonPropertyName("completed_utc")]
    public DateTimeOffset? CompletedUtc { get; init; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; init; }
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

public sealed class EventResult
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("warning_count")]
    public int WarningCount { get; init; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; init; }
}

public sealed class TelemetryMetadata
{
    [JsonPropertyName("record_created_utc")]
    public required DateTimeOffset RecordCreatedUtc { get; init; }

    [JsonPropertyName("record_creation_duration_ms")]
    public double RecordCreationDurationMs { get; set; }

    [JsonPropertyName("producer")]
    public string Producer { get; init; } = "DynamoGovernance";

    [JsonPropertyName("delivery")]
    public string Delivery { get; init; } = "local_jsonl";

    [JsonPropertyName("privacy_profile")]
    public string PrivacyProfile { get; init; } = "testing_plaintext";
}
