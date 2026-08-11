namespace DynamoGovernance.Core.Models;

public static class TelemetryEventTypes
{
    public const string SessionStarted = "session.started";
    public const string ExtensionReady = "extension.ready";
    public const string SessionEnded = "session.ended";
    public const string GraphExecutionStarted = "graph.execution.started";
    public const string GraphExecutionCompleted = "graph.execution.completed";
    public const string NodeAdded = "node.added";
    public const string NodeRemoved = "node.removed";
    public const string ExtensionError = "extension.error";
}

public static class TelemetryResultStatuses
{
    public const string Succeeded = "succeeded";
    public const string SucceededWithWarnings = "succeeded_with_warnings";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Skipped = "skipped";
    public const string Unknown = "unknown";
}
