using System.Text.Json.Serialization;

namespace DynamoGovernance.Core.Models;

public sealed class SessionStartedPayload
{
    [JsonPropertyName("startup_reason")]
    public string StartupReason { get; init; } = "extension_startup";
}

public sealed class ExtensionReadyPayload
{
    [JsonPropertyName("initialization_completed")]
    public bool InitializationCompleted { get; init; } = true;
}

public sealed class SessionEndedPayload
{
    [JsonPropertyName("shutdown_reason")]
    public string ShutdownReason { get; init; } = "dynamo_shutdown";

    [JsonPropertyName("session_duration_ms")]
    public required long SessionDurationMs { get; init; }
}

public sealed class GraphExecutionStartedPayload
{
    [JsonPropertyName("graph")]
    public required GraphContext Graph { get; init; }

    [JsonPropertyName("execution")]
    public required GraphExecutionContext Execution { get; init; }
}

public sealed class GraphExecutionCompletedPayload
{
    [JsonPropertyName("graph")]
    public required GraphContext Graph { get; init; }

    [JsonPropertyName("execution")]
    public required GraphExecutionContext Execution { get; init; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<ExecutionIssue> Issues { get; init; } = [];

    [JsonPropertyName("issues_summary")]
    public required IssuesSummary IssuesSummary { get; init; }

    [JsonPropertyName("exception")]
    public ExecutionException? Exception { get; init; }
}

public sealed class GraphContext
{
    [JsonPropertyName("graph_id")]
    public required Guid GraphId { get; init; }

    [JsonPropertyName("graph_id_source")]
    public string GraphIdSource { get; init; } = "workspace_guid";

    [JsonPropertyName("is_saved")]
    public bool IsSaved { get; init; }

    [JsonPropertyName("run_mode")]
    public required string RunMode { get; init; }

    [JsonPropertyName("node_count")]
    public int NodeCount { get; init; }

    [JsonPropertyName("custom_node_count")]
    public int CustomNodeCount { get; init; }
}

public sealed class GraphExecutionContext
{
    [JsonPropertyName("execution_number")]
    public required long ExecutionNumber { get; init; }

    [JsonPropertyName("evaluation_requested")]
    public bool EvaluationRequested { get; init; } = true;

    [JsonPropertyName("evaluation_performed")]
    public bool EvaluationPerformed { get; init; }

    [JsonPropertyName("trigger")]
    public required string Trigger { get; init; }
}

public sealed class ExecutionIssue
{
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("node_id")]
    public Guid? NodeId { get; init; }

    [JsonPropertyName("node_name")]
    public string? NodeName { get; init; }

    [JsonPropertyName("node_type")]
    public string? NodeType { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed class IssuesSummary
{
    [JsonPropertyName("captured_count")]
    public int CapturedCount { get; init; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }
}

public sealed class ExecutionException
{
    [JsonPropertyName("exception_type")]
    public required string ExceptionType { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("stack_trace")]
    public string? StackTrace { get; init; }

    [JsonPropertyName("stack_trace_truncated")]
    public bool StackTraceTruncated { get; init; }
}

public sealed class ExtensionErrorPayload
{
    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    [JsonPropertyName("exception")]
    public required ExecutionException Exception { get; init; }
}
