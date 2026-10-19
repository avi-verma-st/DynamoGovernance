using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace DynamoGovernance.Core.Services;

public sealed class TelemetryLogger : IDisposable
{
    private const int QueueCapacity = 1024;
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromMilliseconds(200);
    private readonly DiagnosticLogger _diagnosticLogger = new();

    private readonly string _logDirectory;
    private readonly Channel<object> _queue;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Task _writerTask;
    private readonly object _stateLock = new();

    private bool _disposed;
    private long _droppedRecordCount;

    public TelemetryLogger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamoGovernance",
            "Logs");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _queue = Channel.CreateBounded<object>(new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _writerTask = Task.Run(ProcessQueueAsync);
    }
    public bool Log<T>(T telemetryEvent) where T : class
    {
        lock (_stateLock)
        {
            if (_disposed) return false;

            bool queued = _queue.Writer.TryWrite(telemetryEvent);
            if (!queued)
            {
                _droppedRecordCount++;
            }
            return queued;
        }
    }
    private async Task ProcessQueueAsync()
    {
        bool stoppedEarly = false;

        try
        {
            Directory.CreateDirectory(_logDirectory);

            await foreach (object telemetryEvent in _queue.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                try
                {
                    string jsonLine = JsonSerializer.Serialize(
                        telemetryEvent,
                        telemetryEvent.GetType(),
                        _jsonOptions);

                    string logFilePath = Path.Combine(
                        _logDirectory,
                        $"telemetry_{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

                    await File.AppendAllTextAsync(
                        logFilePath,
                        jsonLine + Environment.NewLine,
                        _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    IncrementDroppedRecordCount();
                    throw;
                }
                catch
                {
                    IncrementDroppedRecordCount();
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            stoppedEarly = true;
        }
        catch (Exception ex)
        {
            stoppedEarly = true;
            _queue.Writer.TryComplete(ex);
            _diagnosticLogger.LogError(
                nameof(TelemetryLogger),
                nameof(ProcessQueueAsync),
                ex);
        }
        finally
        {
            if (stoppedEarly)
            {
                CountRemainingQueuedRecordsAsDropped();
            }
        }
    }

    private void CountRemainingQueuedRecordsAsDropped()
    {
        long remainingRecordCount = 0;

        while (_queue.Reader.TryRead(out _))
        {
            remainingRecordCount++;
        }

        if (remainingRecordCount == 0)
        {
            return;
        }

        lock (_stateLock)
        {
            _droppedRecordCount += remainingRecordCount;
        }
    }

    private void IncrementDroppedRecordCount()
    {
        lock (_stateLock)
        {
            _droppedRecordCount++;
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
            _queue.Writer.TryComplete();
        }

        bool queueFlushed = false;

        try
        {
            queueFlushed = _writerTask.Wait(ShutdownFlushTimeout);

            if (!queueFlushed)
            {
                _cancellationTokenSource.Cancel();
                _writerTask.GetAwaiter().GetResult();
            }
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
            if (!_writerTask.IsCompleted)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                    _writerTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _diagnosticLogger.LogError(
                        nameof(TelemetryLogger),
                        "Stopping Queue Writer",
                        ex);
                }
            }

            long droppedRecordCount;

            lock (_stateLock)
            {
                droppedRecordCount = _droppedRecordCount;
            }

            _diagnosticLogger.LogInformation(
                nameof(TelemetryLogger),
                nameof(Dispose),
                $"Telemetry logging stopped. Queue flushed: {queueFlushed}. " +
                $"Dropped record count: {droppedRecordCount}.");

            _cancellationTokenSource.Dispose();
        }
    }
}
