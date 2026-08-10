using DynamoGovernance.Core.Models;
using DynamoGovernance.Core.Services;

namespace DynamoGovernance.Core.Examples;

/// <summary>
/// Example usage of the GovernanceService
/// </summary>
public static class TelemetryExample
{
    public static async Task RunExampleAsync()
    {
        // Create service with hashed IDs
        using var governanceService = new GovernanceService(useHashedIds: true);

        // Start a session
        governanceService.StartSession(
            dynamoVersion: "3.6.0",
            hostApplication: "Revit"
        );

        // Log extension ready
        await governanceService.LogEventAsync("extension_ready");

        // Simulate graph execution success
        await governanceService.LogGraphExecutionAsync(
            graphId: Guid.NewGuid(),
            outcome: "success"
        );

        // Simulate an error
        await governanceService.LogEventAsync(
            outcome: "error",
            graphId: Guid.NewGuid(),
            errorDetails: "Node execution failed: NullReferenceException at MyCustomNode"
        );

        // Session ends automatically on Dispose
    }

    public static void RunExampleSync()
    {
        using var governanceService = new GovernanceService(useHashedIds: false);

        governanceService.StartSession(
            dynamoVersion: "3.6.0",
            hostApplication: "Civil3D"
        );

        // Use synchronous logging for lifecycle methods (avoids deadlocks)
        governanceService.LogEvent("workspace_opened");

        // Session ends automatically
    }

    /// <summary>
    /// Demonstrates safe async logging in event handlers
    /// </summary>
    public static async Task OnWorkspaceEventAsync()
    {
        using var governanceService = new GovernanceService(useHashedIds: true);

        governanceService.StartSession("3.6.0", "Revit");

        // Safe to use async in event handlers
        await governanceService.LogEventAsync("graph_executed", Guid.NewGuid());
    }
}
