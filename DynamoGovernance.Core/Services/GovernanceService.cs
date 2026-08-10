using DynamoGovernance.Core.Models;

namespace DynamoGovernance.Core.Services;

/// <summary>
/// Main governance telemetry service - fully failsafe, never throws exceptions
/// </summary>
public class GovernanceService : IDisposable
{
    private readonly TelemetryLogger? _logger;
    private Guid _currentSessionId;
    private string _userId = "unknown";
    private string _machineId = "unknown";
    private string _hostApplication = string.Empty;
    private string _dynamoVersion = string.Empty;
    private readonly bool _isEnabled;
    private bool _sessionEnded = false;

    public GovernanceService(bool useHashedIds = true)
    {
        try
        {
            _logger = new TelemetryLogger();
            IdentityService.SetHashingEnabled(useHashedIds);

            _userId = IdentityService.GetUserId();
            _machineId = IdentityService.GetMachineId();
            _isEnabled = true;
        }
        catch
        {
            // If initialization fails, disable the service
            _isEnabled = false;
        }
    }

    /// <summary>
    /// Initializes a new session
    /// </summary>
    public void StartSession(string dynamoVersion, string hostApplication)
    {
        if (!_isEnabled) return;

        try
        {
            _currentSessionId = Guid.NewGuid();
            _dynamoVersion = dynamoVersion;
            _hostApplication = hostApplication;
            _sessionEnded = false;

            // Log session start
            var startEvent = CreateEvent();
            startEvent.Outcome = "session_started";
            _logger?.Log(startEvent);
        }
        catch
        {
            // Silently fail - never impact application startup
        }
    }

    /// <summary>
    /// Logs a graph execution event
    /// </summary>
    public async Task LogGraphExecutionAsync(Guid? graphId, string outcome, string? errorDetails = null)
    {
        if (!_isEnabled) return;

        try
        {
            var telemetryEvent = CreateEvent();
            telemetryEvent.GraphId = graphId;
            telemetryEvent.Outcome = outcome;
            telemetryEvent.ErrorDetails = errorDetails;

            if (_logger != null)
                await _logger.LogAsync(telemetryEvent);
        }
        catch
        {
            // Silently fail
        }
    }

    /// <summary>
    /// Logs a generic event asynchronously
    /// </summary>
    public async Task LogEventAsync(string outcome, Guid? graphId = null, string? errorDetails = null)
    {
        if (!_isEnabled) return;

        try
        {
            var telemetryEvent = CreateEvent();
            telemetryEvent.Outcome = outcome;
            telemetryEvent.GraphId = graphId;
            telemetryEvent.ErrorDetails = errorDetails;

            if (_logger != null)
                await _logger.LogAsync(telemetryEvent);
        }
        catch
        {
            // Silently fail
        }
    }

    /// <summary>
    /// Logs a generic event synchronously (use for lifecycle methods to avoid deadlocks)
    /// </summary>
    public void LogEvent(string outcome, Guid? graphId = null, string? errorDetails = null)
    {
        if (!_isEnabled) return;

        try
        {
            var telemetryEvent = CreateEvent();
            telemetryEvent.Outcome = outcome;
            telemetryEvent.GraphId = graphId;
            telemetryEvent.ErrorDetails = errorDetails;

            _logger?.Log(telemetryEvent);
        }
        catch
        {
            // Silently fail
        }
    }

    /// <summary>
    /// Ends the current session (idempotent - safe to call multiple times)
    /// </summary>
    public void EndSession()
    {
        if (!_isEnabled || _sessionEnded) return;

        try
        {
            _sessionEnded = true;

            var endEvent = CreateEvent();
            endEvent.Outcome = "session_ended";
            _logger?.Log(endEvent);
        }
        catch
        {
            // Silently fail
        }
    }

    private TelemetryEvent CreateEvent()
    {
        return new TelemetryEvent
        {
            SessionId = _currentSessionId,
            UserId = _userId,
            MachineId = _machineId,
            HostApplication = _hostApplication,
            DynamoVersion = _dynamoVersion
        };
    }

    public void Dispose()
    {
        try
        {
            EndSession();
            _logger?.Dispose();
        }
        catch
        {
            // Silently fail during disposal
        }
    }
}
