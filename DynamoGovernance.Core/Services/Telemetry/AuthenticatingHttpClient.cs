using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace DynamoGovernance.Core.Services.Telemetry;

/// <summary>
/// A <see cref="IHttpClient"/> implementation for Serilog.Sinks.Http that attaches an
/// authentication header (via <see cref="ITelemetryAuthProvider"/>) to every outgoing
/// request, and reports delivery/auth failures through <see cref="DiagnosticLogger"/>
/// instead of throwing, so telemetry failures never interrupt Dynamo.
/// </summary>
public sealed class AuthenticatingHttpClient : IHttpClient
{
    private readonly ITelemetryAuthProvider _authProvider;
    private readonly DiagnosticLogger _diagnosticLogger;
    private readonly HttpClient _httpClient = new();

    public AuthenticatingHttpClient(ITelemetryAuthProvider authProvider, DiagnosticLogger diagnosticLogger)
    {
        _authProvider = authProvider;
        _diagnosticLogger = diagnosticLogger;
    }

    public void Configure(IConfiguration configuration)
    {
        // No additional configuration required from appsettings-style sources.
    }

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        Stream contentStream,
        CancellationToken cancellationToken)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };

        var authHeader = _authProvider.GetAuthHeader();
        if (authHeader is { } header)
        {
            request.Headers.TryAddWithoutValidation(header.HeaderName, header.HeaderValue);
        }

        HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden)
        {
            _diagnosticLogger.LogError(
                nameof(AuthenticatingHttpClient),
                nameof(PostAsync),
                new InvalidOperationException(
                    $"Telemetry HTTP sink authentication failed with status {(int)response.StatusCode} " +
                    $"({response.StatusCode}) while posting to '{requestUri}'."));
        }

        return response;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
