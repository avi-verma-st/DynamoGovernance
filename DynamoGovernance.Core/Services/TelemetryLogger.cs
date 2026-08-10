using System.Text.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamoGovernance.Core.Models;

namespace DynamoGovernance.Core.Services;

/// <summary>
/// Simple JSONL logger for telemetry events - fully non-blocking and failsafe
/// </summary>
public class TelemetryLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _enableLogging = true;

    public TelemetryLogger(string? logDirectory = null)
    {
        try
        {
            string directory = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DynamoGovernance",
                "Logs");

            Directory.CreateDirectory(directory);

            // Single file per day
            string fileName = $"telemetry_{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
            _logFilePath = Path.Combine(directory, fileName);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }
        catch
        {
            // If initialization fails, disable logging to prevent issues
            _enableLogging = false;
        }
    }

    /// <summary>
    /// Logs a telemetry event asynchronously - fire and forget, never blocks
    /// </summary>
    public async Task LogAsync(TelemetryEvent telemetryEvent)
    {
        if (!_enableLogging) return;

        try
        {
            // Use timeout to prevent indefinite waiting
            if (!await _writeLock.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                return; // Skip logging if can't acquire lock quickly
            }

            try
            {
                string jsonLine = JsonSerializer.Serialize(telemetryEvent, _jsonOptions);
                await File.AppendAllLinesAsync(_logFilePath, new[] { jsonLine });
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
            // Silently fail - logging should never crash the app
        }
    }

    /// <summary>
    /// Logs a telemetry event synchronously with timeout protection
    /// </summary>
    public void Log(TelemetryEvent telemetryEvent)
    {
        if (!_enableLogging) return;

        try
        {
            // Use timeout to prevent blocking
            if (!_writeLock.Wait(TimeSpan.FromSeconds(5)))
            {
                return; // Skip logging if can't acquire lock quickly
            }

            try
            {
                string jsonLine = JsonSerializer.Serialize(telemetryEvent, _jsonOptions);
                File.AppendAllLines(_logFilePath, new[] { jsonLine });
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
            // Silently fail - logging should never crash the app
        }
    }

    public void Dispose()
    {
        try
        {
            _writeLock?.Dispose();
        }
        catch
        {
            // Swallow disposal errors
        }
    }
}
