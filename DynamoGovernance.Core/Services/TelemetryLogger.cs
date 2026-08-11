using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace DynamoGovernance.Core.Services;

public sealed class TelemetryLogger : IDisposable
{
    private const int QueueCapacity = 1024;
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromMilliseconds(200);

    private readonly string _logDirectory;
    private readonly Channel<object> _queue;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Task _writerTask;
    private int _disposed;
    private long _droppedRecordCount;

    public TelemetryLogger(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
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

    public long DroppedRecordCount => Interlocked.Read(ref _droppedRecordCount);

    public bool Log<T>(T telemetryEvent) where T : class
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        bool queued = _queue.Writer.TryWrite(telemetryEvent);
        if (!queued)
        {
            Interlocked.Increment(ref _droppedRecordCount);
        }

        return queued;
    }

    public Task<bool> LogAsync<T>(T telemetryEvent) where T : class
    {
        return Task.FromResult(Log(telemetryEvent));
    }

    private async Task ProcessQueueAsync()
    {
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
                catch
                {
                    Interlocked.Increment(ref _droppedRecordCount);
                }
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _queue.Writer.TryComplete();
            if (!_writerTask.Wait(ShutdownFlushTimeout))
            {
                _cancellationTokenSource.Cancel();
            }
        }
        catch
        {
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }
    }
}
