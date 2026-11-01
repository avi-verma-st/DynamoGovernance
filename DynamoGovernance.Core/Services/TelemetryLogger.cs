using System.Text.Json;
using System.Text.Json.Serialization;
using DynamoGovernance.Core.Services.Telemetry;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Async;

namespace DynamoGovernance.Core.Services;

public sealed class TelemetryLogger : IDisposable
{
    private static readonly MessageTemplate TelemetryMessageTemplate =
        new MessageTemplateParser().Parse("{" + TelemetryJsonFormatter.TelemetryJsonPropertyName + "}");

    private readonly DiagnosticLogger _diagnosticLogger = new();
    private readonly TelemetryLoggerOptions _options;

    private readonly string _logDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Logger _serilogLogger;
    private readonly QueueMonitor _queueMonitor = new();
    private readonly object _stateLock = new();

    private bool _disposed;

    public TelemetryLogger() : this(new TelemetryLoggerOptions())
    {
    }

    /// <summary>
    /// Allows explicit instance-level configuration (endpoint URL, auth provider,
    /// batching) instead of relying on mutable static/global state. See
    /// <see cref="TelemetryLoggerOptions"/>.
    /// </summary>
    public TelemetryLogger(TelemetryLoggerOptions options)
    {
        _options = options;

        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamoGovernance",
            "Logs");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        Directory.CreateDirectory(_logDirectory);

        string logFilePathTemplate = Path.Combine(_logDirectory, "telemetry_.jsonl");

        SharedSelfLogManager.AddUser(selfLogMessage => _diagnosticLogger.LogInformation(
            nameof(TelemetryLogger),
            "SerilogSelfLog",
            selfLogMessage));

        var telemetryFormatter = new TelemetryJsonFormatter();

        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            //Sink 1 - Local JSONL (always enabled, independent of cloud delivery)
            .WriteTo.Async(sinkConfiguration => sinkConfiguration.File(
                telemetryFormatter,
                logFilePathTemplate,
                rollingInterval: RollingInterval.Day),
                blockWhenFull: false,
                monitor: _queueMonitor);

        //Sink 2 - Azure Function HTTP endpoint (only when URL + auth are configured)
        if (_options.IsCloudLoggingConfigured)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Http(
                requestUri: _options.AzureFunctionEndpointUrl!,
                queueLimitBytes: _options.HttpQueueLimitBytes,
                logEventsInBatchLimit: _options.LogEventsInBatchLimit,
                period: _options.BatchPeriod,
                textFormatter: telemetryFormatter,
                httpClient: new AuthenticatingHttpClient(_options.AuthProvider, _diagnosticLogger));
        }
        else
        {
            _diagnosticLogger.LogInformation(
                nameof(TelemetryLogger),
                "ConfigureCloudSink",
                "Azure Function telemetry sink disabled: endpoint URL or authentication is not configured. " +
                "Local JSONL logging remains active.");
        }

        _serilogLogger = loggerConfiguration.CreateLogger();
    }

    /// <summary>
    /// Serializes and queues a telemetry event for local JSONL logging and (if
    /// configured) batched HTTP delivery to the Azure Function. Returns
    /// <see langword="true"/> if the event was successfully accepted into the
    /// logging pipeline; this reflects queue acceptance only, not confirmed
    /// delivery to the Azure Function.
    /// </summary>
    public bool Log<T>(T telemetryEvent) where T : class
    {
        lock (_stateLock)
        {
            if (_disposed) return false;

            try
            {
                string json = JsonSerializer.Serialize(
                    telemetryEvent,
                    telemetryEvent.GetType(),
                    _jsonOptions);

                var telemetryJsonProperty = new LogEventProperty(
                    TelemetryJsonFormatter.TelemetryJsonPropertyName,
                    new ScalarValue(json));

                var logEvent = new LogEvent(
                    DateTimeOffset.UtcNow,
                    LogEventLevel.Information,
                    exception: null,
                    TelemetryMessageTemplate,
                    [telemetryJsonProperty]);

                _serilogLogger.Write(logEvent);
                return true;
            }
            catch (Exception ex)
            {
                _diagnosticLogger.LogError(
                    nameof(TelemetryLogger),
                    nameof(Log),
                    ex);
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        bool queueFlushed = false;

        // Capture queue statistics before the sinks (and their inspectors) are torn
        // down, since StopMonitoring() clears the inspector reference.
        _queueMonitor.CaptureFinalSnapshot();

        try
        {
            _serilogLogger.Dispose();
            queueFlushed = true;
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(
                nameof(TelemetryLogger),
                nameof(Dispose),
                ex);
        }
        finally
        {
            SharedSelfLogManager.RemoveUser();

            _diagnosticLogger.LogInformation(
                nameof(TelemetryLogger),
                nameof(Dispose),
                $"Telemetry logging stopped. Queue flushed: {queueFlushed}. " +
                $"Final queue count: {_queueMonitor.LastObservedCount}, " +
                $"Dropped record count: {_queueMonitor.DroppedCount}.");
        }
    }

    /// <summary>
    /// Surfaces the async sink's queue depth and dropped-event count using Serilog's
    /// built-in <see cref="IAsyncLogEventSinkMonitor"/> instrumentation, instead of
    /// hand-rolled queue bookkeeping. Thread-safe: the inspector reference and the
    /// final snapshot are guarded by a lock since Start/StopMonitoring can run
    /// concurrently with Dispose's snapshot capture.
    /// </summary>
    private sealed class QueueMonitor : IAsyncLogEventSinkMonitor
    {
        private readonly object _gate = new();
        private IAsyncLogEventSinkInspector? _inspector;
        private int _lastObservedCount;
        private long _droppedCount;

        public int LastObservedCount
        {
            get { lock (_gate) { return _lastObservedCount; } }
        }

        public long DroppedCount
        {
            get { lock (_gate) { return _droppedCount; } }
        }

        public void StartMonitoring(IAsyncLogEventSinkInspector inspector)
        {
            lock (_gate)
            {
                _inspector = inspector;
            }
        }

        public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
        {
            lock (_gate)
            {
                // Capture the final values before releasing the inspector so that
                // dropped-message/queue-depth stats remain available after shutdown.
                _lastObservedCount = inspector.Count;
                _droppedCount = inspector.DroppedMessagesCount;
                _inspector = null;
            }
        }

        /// <summary>
        /// Captures the current values from the live inspector (if still attached)
        /// without waiting for <see cref="StopMonitoring"/>, so callers can read an
        /// up-to-date snapshot immediately before disposal begins.
        /// </summary>
        public void CaptureFinalSnapshot()
        {
            lock (_gate)
            {
                if (_inspector is { } inspector)
                {
                    _lastObservedCount = inspector.Count;
                    _droppedCount = inspector.DroppedMessagesCount;
                }
            }
        }
    }
}
