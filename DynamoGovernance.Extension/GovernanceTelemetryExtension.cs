using Dynamo.Extensions;

using DynamoGovernance.Core.Services;

namespace DynamoGovernance.Extension;

public class GovernanceTelemetryExtension : IExtension
{
    private GovernanceService? _governanceService;

    public string UniqueId => "F2BA577E-4C5C-4A37-8BFC-2A5C11FAC698";
    public string Name => "Dynamo Governance Telemetry";

    public void Startup(StartupParams sp)
    {
        // Initialize with hashed IDs (set to false for plain text)
        _governanceService = new GovernanceService(useHashedIds: true);

        string dynamoVersion = sp.DynamoVersion?.ToString() ?? "Unknown";
        string hostApplication = "DynamoCore"; // Will be set based on host context

        _governanceService.StartSession(dynamoVersion, hostApplication);
    }

    public void Ready(ReadyParams sp)
    {
        // Use synchronous logging to avoid deadlocks in lifecycle methods
        _governanceService?.LogEvent("extension_ready");

        // TODO: Hook into workspace events here
        // sp.CurrentWorkspaceModel - access current workspace
    }

    public void Shutdown()
    {
        _governanceService?.EndSession();
    }

    public void Dispose()
    {
        _governanceService?.Dispose();
    }
}
