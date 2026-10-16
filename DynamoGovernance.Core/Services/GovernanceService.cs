using System.Runtime.InteropServices;
using DynamoGovernance.Core.Models;

namespace DynamoGovernance.Core.Services;

public sealed class GovernanceService : IDisposable
{
    private readonly object _lifecycleLock = new();
    private readonly TelemetryLogger? _logger;
    private readonly IdentityContext _identity;
    private ApplicationContext _application;
    private readonly DiagnosticLogger _diagnosticLogger = new();
    private Guid _currentSessionId;
    private bool _sessionStarted;
    private bool _sessionEnded;
    private bool _disposed;

    public GovernanceService()
    {
        _identity = new IdentityContext
        {
            UserId = GetValueOrUnknown(IdentityService.GetUserId),
            MachineId = GetValueOrUnknown(IdentityService.GetMachineId),
            IdentifiersProtected = false
        };

        _application = CreateApplicationContext("unknown", null, "unknown", "unknown");

        try
        {
            _logger = new TelemetryLogger();
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(nameof(GovernanceService), "Initiating Telemetry Logger", ex);
            _logger = null;
        }
    }

    public void StartSession(
        string dynamoVersion,
        string hostName,
        string? hostVersion,
        string extensionVersion)
    {
        lock (_lifecycleLock)
        {

            if (_logger is null || _sessionStarted || _disposed)
            {
                return;
            }

            _currentSessionId = Guid.NewGuid();
            _application = CreateApplicationContext(
                hostName,
                hostVersion,
                dynamoVersion,
                extensionVersion);
            _sessionStarted = true;
            LogEvent(
                TelemetryEventTypes.SessionStarted,
                new SessionStartedData
                {
                    Identity = _identity,
                    Application = _application
                });
        }
    }

    public void LogGraphOpened(
        GraphEventData graph,
        DateTimeOffset? occurredUtc = null)
    {
        LogEvent(TelemetryEventTypes.GraphOpened, graph, occurredUtc);
    }

    public void LogGraphClosed(
        GraphEventData graph,
        DateTimeOffset? occurredUtc = null)
    {
        LogEvent(TelemetryEventTypes.GraphClosed, graph, occurredUtc);
    }

    public void LogGraphSaved(
        GraphEventData graph,
        DateTimeOffset? occurredUtc = null)
    {
        LogEvent(TelemetryEventTypes.GraphSaved, graph, occurredUtc);
    }

    public void LogExecutionStarted(
        GraphEventData graph,
        DateTimeOffset? occurredUtc = null)
    {
        LogEvent(TelemetryEventTypes.ExecutionStarted, graph, occurredUtc);
    }

    public void LogExecutionEnded(
        GraphEventData graph,
        bool evaluationTookPlace,
        bool successful,
        DateTimeOffset? occurredUtc = null)
    {
        LogEvent(
            TelemetryEventTypes.ExecutionEnded,
            new ExecutionEndedData
            {
                Graph = graph,
                EvaluationTookPlace = evaluationTookPlace,
                Successful = successful
            },
            occurredUtc);
    }

    public void EndSession()
    {
        if (!_sessionStarted || _sessionEnded)
        {
            return;
        }
        _sessionEnded = true;
        LogEvent(TelemetryEventTypes.SessionEnded);
    }

    private void LogEvent(
        string eventType,
        DateTimeOffset? occurredUtc = null)
    {
        if (_logger is null || !_sessionStarted || _disposed)
        {
            return;
        }

        try
        {
            _logger.Log(new TelemetryRecord
            {
                Event = eventType,
                SessionId = _currentSessionId,
                OccurredUtc = occurredUtc ?? DateTimeOffset.UtcNow
            });
        }
        catch(Exception ex)
        {
            _diagnosticLogger.LogError(nameof(GovernanceService), $"Logging Telemetry Event {eventType}", ex);
        }
    }

    private void LogEvent<TPayload>(
        string eventType,
        TPayload data,
        DateTimeOffset? occurredUtc = null)
        where TPayload : class
    {
        if (_logger is null || !_sessionStarted || _sessionEnded || _disposed)
        {
            return;
        }

        try
        {
            _logger.Log(new TelemetryRecord<TPayload>
            {
                Event = eventType,
                SessionId = _currentSessionId,
                OccurredUtc = occurredUtc ?? DateTimeOffset.UtcNow,
                Data = data
            });
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(nameof(GovernanceService), $"Logging Telemetry Event {eventType}", ex);
        }
    }

    private static ApplicationContext CreateApplicationContext(
        string hostName,
        string? hostVersion,
        string dynamoVersion,
        string extensionVersion)
    {
        return new ApplicationContext
        {
            HostName = hostName,
            HostVersion = hostVersion,
            DynamoVersion = dynamoVersion,
            ExtensionVersion = extensionVersion,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            RuntimeVersion = Environment.Version.ToString()
        };
    }

    private static string GetValueOrUnknown(Func<string> valueFactory)
    {
        try
        {
            string value = valueFactory();
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
        catch
        {
            return "unknown";
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {

            if (_disposed)
            {
                return;
            }
                EndSession();
                _disposed = true;
            try
            {
                _logger?.Dispose();
            }
            catch (Exception ex)
            {
                _diagnosticLogger.LogError(nameof(GovernanceService), "Disposing Governance Service", ex);
            }
        }
    }
}
