namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// Instance-level configuration for <see cref="TelemetryLogger"/>. Replaces the
/// previous mutable static properties (<c>TelemetryLogger.AzureFunctionEndpointUrl</c>,
/// <c>FunctionKeyAuthProvider.FunctionKey</c>) so configuration is explicit and testable,
/// while still defaulting from environment variables for the existing POC setup.
/// Cloud logging remains entirely optional: if the endpoint URL is not configured (or
/// authentication is not configured), only local JSONL logging is enabled.
/// </summary>
public sealed class TelemetryLoggerOptions
{
    static TelemetryLoggerOptions()
    {
        // Best-effort: loads DYNAMOGOVERNANCE_TELEMETRY_* values from a ".env" file
        // (if present) into the process environment so local/POC testing doesn't
        // require real OS environment variables. Explicit environment variables
        // always take precedence and are never overwritten.
        DotEnvLoader.EnsureLoaded();
    }

    /// <summary>
    /// The URL of the Azure Function endpoint that telemetry events are forwarded to
    /// (e.g. <c>https://your-function.azurewebsites.net/api/telemetry</c>). The function
    /// key used to authenticate against it is configured separately via
    /// <see cref="AuthProvider"/> and must never be appended to this URL as a query
    /// string parameter.
    /// </summary>
    public string? AzureFunctionEndpointUrl { get; init; } =
        Environment.GetEnvironmentVariable("DYNAMOGOVERNANCE_TELEMETRY_FUNCTION_URL");

    /// <summary>
    /// Supplies the authentication header for outgoing telemetry requests. Defaults to
    /// <see cref="FunctionKeyAuthProvider"/> reading the
    /// <c>DYNAMOGOVERNANCE_TELEMETRY_FUNCTION_KEY</c> environment variable.
    /// </summary>
    public ITelemetryAuthProvider AuthProvider { get; init; } = new FunctionKeyAuthProvider();

    /// <summary>
    /// Maximum number of telemetry events sent per HTTP batch.
    /// </summary>
    public int LogEventsInBatchLimit { get; init; } = 50;

    /// <summary>
    /// Maximum time to wait before flushing a partial batch to the Azure Function.
    /// </summary>
    public TimeSpan BatchPeriod { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Upper bound, in bytes, on the in-memory HTTP delivery queue. Once exceeded,
    /// new events are dropped rather than blocking the caller or growing unbounded.
    /// </summary>
    public long HttpQueueLimitBytes { get; init; } = 50 * 1024 * 1024; // 50 MB

    public bool IsCloudLoggingConfigured =>
        !string.IsNullOrWhiteSpace(AzureFunctionEndpointUrl) && AuthProvider.IsConfigured;
}
