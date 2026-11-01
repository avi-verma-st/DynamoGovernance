namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// Supplies the authentication header to attach to outgoing telemetry HTTP requests.
/// Implementations are swappable: the current POC uses an Azure Function key
/// (<see cref="FunctionKeyAuthProvider"/>), while a future implementation can acquire
/// a Microsoft Entra ID delegated access token via MSAL and return it as a Bearer token.
/// </summary>
public interface ITelemetryAuthProvider
{
    /// <summary>
    /// Returns <see langword="true"/> if authentication is configured and the sink
    /// should be enabled; <see langword="false"/> if cloud logging should be
    /// disabled gracefully (e.g. missing key/endpoint).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Gets the HTTP header name/value pair to attach to the outgoing request,
    /// or <see langword="null"/> if no header should be attached.
    /// </summary>
    (string HeaderName, string HeaderValue)? GetAuthHeader();
}
