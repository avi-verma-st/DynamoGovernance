using System.Diagnostics;
using System.Runtime.InteropServices;
using DynamoGovernance.Core.Models;

namespace DynamoGovernance.Core.Services;

public sealed class GovernanceService : IDisposable
{
    private const int MaximumIssues = 25;
    private const int MaximumIssueMessageLength = 1024;
    private const int MaximumExceptionMessageLength = 2048;
    private const int MaximumStackTraceLength = 8192;

    private readonly TelemetryLogger? _logger;
    private readonly IdentityContext _identity;
    private readonly Stopwatch _sessionStopwatch = new();
    private ApplicationContext _application;
    private Guid _currentSessionId;
    private long _sequenceNumber;
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

    public Guid CurrentSessionId => _currentSessionId;

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

        try
        {
            _currentSessionId = Guid.NewGuid();
            _sequenceNumber = 0;
            Volatile.Write(ref _sessionEnded, 0);
            _application = CreateApplicationContext(
                hostName,
                hostVersion,
                dynamoVersion,
                extensionVersion);
            _sessionStopwatch.Restart();

            LogEvent(
                TelemetryEventTypes.SessionStarted,
                new SessionStartedPayload(),
                new EventResult { Status = TelemetryResultStatuses.Succeeded });
        }
        catch
        {
        }
    }

    public void LogExtensionReady()
    {
        LogEvent(
            TelemetryEventTypes.ExtensionReady,
            new ExtensionReadyPayload(),
            new EventResult { Status = TelemetryResultStatuses.Succeeded });
    }

    public Guid LogGraphExecutionStarted(
        GraphExecutionStartedPayload payload,
        DateTimeOffset startedUtc,
        Guid? correlationId = null,
        Guid? causationEventId = null)
    {
        return LogEvent(
            TelemetryEventTypes.GraphExecutionStarted,
            payload,
            new EventResult { Status = TelemetryResultStatuses.Succeeded },
            new EventTiming
            {
                OccurredUtc = startedUtc,
                StartedUtc = startedUtc
            },
            correlationId,
            causationEventId);
    }

    public Task<Guid> LogGraphExecutionCompletedAsync(
        GraphExecutionCompletedPayload payload,
        string status,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        long durationMs,
        Guid? correlationId = null,
        Guid? causationEventId = null)
    {
        GraphExecutionCompletedPayload boundedPayload = BoundDiagnostics(payload);
        Guid eventId = LogEvent(
            TelemetryEventTypes.GraphExecutionCompleted,
            boundedPayload,
            new EventResult
            {
                Status = status,
                WarningCount = boundedPayload.Issues.Count(issue => issue.Severity == "warning"),
                ErrorCount = boundedPayload.Issues.Count(issue => issue.Severity == "error")
            },
            new EventTiming
            {
                OccurredUtc = completedUtc,
                StartedUtc = startedUtc,
                CompletedUtc = completedUtc,
                DurationMs = Math.Max(0, durationMs)
            },
            correlationId,
            causationEventId);

        return Task.FromResult(eventId);
    }

    public Guid LogExtensionError(string operation, Exception exception)
    {
        return LogEvent(
            TelemetryEventTypes.ExtensionError,
            new ExtensionErrorPayload
            {
                Operation = operation,
                Exception = CreateBoundedException(exception)
            },
            new EventResult
            {
                Status = TelemetryResultStatuses.Failed,
                ErrorCount = 1
            });
    }

    public void EndSession(string shutdownReason = "dynamo_shutdown")
    {
        if (Volatile.Read(ref _sessionStarted) == 0 || Interlocked.Exchange(ref _sessionEnded, 1) != 0)
        {
            return;
        }

        try
        {
            _sessionStopwatch.Stop();
            long durationMs = _sessionStopwatch.ElapsedMilliseconds;
            DateTimeOffset completedUtc = DateTimeOffset.UtcNow;

            LogEvent(
                TelemetryEventTypes.SessionEnded,
                new SessionEndedPayload
                {
                    ShutdownReason = shutdownReason,
                    SessionDurationMs = durationMs
                },
                new EventResult { Status = TelemetryResultStatuses.Succeeded },
                new EventTiming
                {
                    OccurredUtc = completedUtc,
                    CompletedUtc = completedUtc,
                    DurationMs = durationMs
                });
        }
        catch
        {
        }
    }

    public Guid LogEvent<TPayload>(
        string eventType,
        TPayload payload,
        EventResult result,
        EventTiming? timing = null,
        Guid? correlationId = null,
        Guid? causationEventId = null,
        string eventVersion = "1.0")
        where TPayload : class
    {
        if (_logger is null || Volatile.Read(ref _sessionStarted) == 0 || Volatile.Read(ref _disposed) != 0)
        {
            return Guid.Empty;
        }

        try
        {
            Stopwatch creationStopwatch = Stopwatch.StartNew();
            Guid eventId = Guid.NewGuid();
            DateTimeOffset recordCreatedUtc = DateTimeOffset.UtcNow;
            var metadata = new TelemetryMetadata
            {
                RecordCreatedUtc = recordCreatedUtc
            };

            var envelope = new TelemetryEnvelope<TPayload>
            {
                EventType = eventType,
                EventVersion = eventVersion,
                EventId = eventId,
                SessionId = _currentSessionId,
                SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
                Correlation = new EventCorrelation
                {
                    CorrelationId = correlationId ?? eventId,
                    CausationEventId = causationEventId
                },
                Timing = timing ?? new EventTiming { OccurredUtc = recordCreatedUtc },
                Identity = _identity,
                Application = _application,
                Result = result,
                Payload = payload,
                Telemetry = metadata
            };

            creationStopwatch.Stop();
            metadata.RecordCreationDurationMs = creationStopwatch.Elapsed.TotalMilliseconds;
            _logger.Log(envelope);
            return eventId;
        }
        catch
        {
            return Guid.Empty;
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

    private static GraphExecutionCompletedPayload BoundDiagnostics(GraphExecutionCompletedPayload payload)
    {
        int totalIssueCount = Math.Max(payload.IssuesSummary.TotalCount, payload.Issues.Count);
        IReadOnlyList<ExecutionIssue> issues = payload.Issues
            .Take(MaximumIssues)
            .Select(issue => new ExecutionIssue
            {
                Severity = issue.Severity,
                Source = issue.Source,
                NodeId = issue.NodeId,
                NodeName = issue.NodeName,
                NodeType = issue.NodeType,
                Message = Truncate(issue.Message, MaximumIssueMessageLength)
            })
            .ToArray();

        ExecutionException? exception = payload.Exception is null
            ? null
            : new ExecutionException
            {
                ExceptionType = payload.Exception.ExceptionType,
                Message = Truncate(payload.Exception.Message, MaximumExceptionMessageLength),
                StackTrace = Truncate(payload.Exception.StackTrace, MaximumStackTraceLength),
                StackTraceTruncated = payload.Exception.StackTraceTruncated ||
                    payload.Exception.StackTrace?.Length > MaximumStackTraceLength
            };

        return new GraphExecutionCompletedPayload
        {
            Graph = payload.Graph,
            Execution = payload.Execution,
            Issues = issues,
            IssuesSummary = new IssuesSummary
            {
                CapturedCount = issues.Count,
                TotalCount = totalIssueCount,
                Truncated = payload.IssuesSummary.Truncated || totalIssueCount > issues.Count
            },
            Exception = exception
        };
    }

    private static ExecutionException CreateBoundedException(Exception exception)
    {
        return new ExecutionException
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = Truncate(exception.Message, MaximumExceptionMessageLength),
            StackTrace = Truncate(exception.StackTrace, MaximumStackTraceLength),
            StackTraceTruncated = exception.StackTrace?.Length > MaximumStackTraceLength
        };
    }

    private static string? Truncate(string? value, int maximumLength)
    {
        return value?.Length > maximumLength ? value[..maximumLength] : value;
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
