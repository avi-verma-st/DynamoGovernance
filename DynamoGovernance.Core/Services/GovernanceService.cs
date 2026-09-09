using System.Runtime.InteropServices;
using DynamoGovernance.Core.Models;

namespace DynamoGovernance.Core.Services;

public sealed class GovernanceService : IDisposable
{
    private readonly TelemetryLogger? _logger;
    private readonly IdentityContext _identity;
    private ApplicationContext _application;
    private Guid _currentSessionId;
    private int _sessionStarted;
    private int _sessionEnded;
    private int _disposed;

    public GovernanceService(string? logDirectory = null)
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
            _logger = new TelemetryLogger(logDirectory);
        }
        catch
        {
            _logger = null;
        }
    }

    public void StartSession(
        string dynamoVersion,
        string hostName,
        string? hostVersion,
        string extensionVersion)
    {
        if (_logger is null || Interlocked.CompareExchange(ref _sessionStarted, 1, 0) != 0)
        {
            return;
        }

        _currentSessionId = Guid.NewGuid();
        Volatile.Write(ref _sessionEnded, 0);
        _application = CreateApplicationContext(
            hostName,
            hostVersion,
            dynamoVersion,
            extensionVersion);

        LogEvent(
            TelemetryEventTypes.SessionStarted,
            new SessionStartedData
            {
                Identity = _identity,
                Application = _application
            });
    }

    public void LogExtensionReady()
    {
        LogEvent(TelemetryEventTypes.ExtensionReady);
    }

    public void LogGraphExecutionStarted(int nodeCount, DateTimeOffset occurredUtc)
    {
        LogEvent(
            TelemetryEventTypes.GraphExecutionStarted,
            new NodeCountData { NodeCount = nodeCount },
            occurredUtc);
    }

    public void LogGraphExecutionCompleted(
        int nodeCount,
        bool successful,
        DateTimeOffset occurredUtc)
    {
        LogEvent(
            TelemetryEventTypes.GraphExecutionCompleted,
            new ExecutionCompletedData
            {
                NodeCount = nodeCount,
                Successful = successful
            },
            occurredUtc);
    }

    public void LogNodeChanged(string eventType, int nodeCount)
    {
        LogEvent(eventType, new NodeCountData { NodeCount = nodeCount });
    }

    public void LogExtensionError()
    {
        LogEvent(TelemetryEventTypes.ExtensionError);
    }

    public void EndSession()
    {
        if (Volatile.Read(ref _sessionStarted) == 0 || Interlocked.Exchange(ref _sessionEnded, 1) != 0)
        {
            return;
        }

        LogEvent(TelemetryEventTypes.SessionEnded);
    }

    private void LogEvent(
        string eventType,
        object? data = null,
        DateTimeOffset? occurredUtc = null)
    {
        if (_logger is null || Volatile.Read(ref _sessionStarted) == 0 || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _logger.Log(new TelemetryRecord
            {
                Event = eventType,
                SessionId = _currentSessionId,
                OccurredUtc = occurredUtc ?? DateTimeOffset.UtcNow,
                Data = data
            });
        }
        catch
        {
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
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            EndSession();
            Interlocked.Exchange(ref _disposed, 1);
            _logger?.Dispose();
        }
        catch
        {
        }
    }
}
