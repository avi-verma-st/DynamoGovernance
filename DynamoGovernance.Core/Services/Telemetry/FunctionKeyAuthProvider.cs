namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// Authenticates outgoing telemetry requests using an Azure Function "function" auth
/// level key, sent via the <c>x-functions-key</c> header. This is the current POC
/// authentication mechanism and is intended to be replaced by
/// <c>EntraIdBearerTelemetryAuthProvider</c> (MSAL delegated token) in the future.
/// </summary>
public sealed class FunctionKeyAuthProvider : ITelemetryAuthProvider
{
    private const string FunctionKeyHeaderName = "x-functions-key";
    private readonly string? _functionKey;

    /// <summary>
    /// Creates a provider using the function key supplied explicitly, or falling
    /// back to the <c>DYNAMOGOVERNANCE_TELEMETRY_FUNCTION_KEY</c> environment
    /// variable if none is supplied. Do not hardcode or commit this value to
    /// source control.
    /// </summary>
    public FunctionKeyAuthProvider(string? functionKey = null)
    {
        DotEnvLoader.EnsureLoaded();

        _functionKey = functionKey
            ?? Environment.GetEnvironmentVariable("DYNAMOGOVERNANCE_TELEMETRY_FUNCTION_KEY");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_functionKey);

    public (string HeaderName, string HeaderValue)? GetAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_functionKey))
        {
            return null;
        }

        return (FunctionKeyHeaderName, _functionKey);
    }
}
