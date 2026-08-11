using System.Diagnostics;
using DynamoGovernance.Core.Models;
using DynamoGovernance.Core.Services;

namespace DynamoGovernance.Core.Examples;

public static class TelemetryExample
{
    public static async Task RunExampleAsync(string logDirectory)
    {
        using var governanceService = new GovernanceService(logDirectory);
        governanceService.StartSession("3.6.0", "Revit", "2026", "1.0.0");
        governanceService.LogExtensionReady();

        Guid graphId = Guid.NewGuid();
        var graph = new GraphContext
        {
            GraphId = graphId,
            IsSaved = true,
            RunMode = "manual",
            NodeCount = 84,
            CustomNodeCount = 6
        };
        var execution = new GraphExecutionContext
        {
            ExecutionNumber = 1,
            EvaluationPerformed = true,
            Trigger = "manual_run"
        };

        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Guid startedEventId = governanceService.LogGraphExecutionStarted(
            new GraphExecutionStartedPayload
            {
                Graph = graph,
                Execution = execution
            },
            startedUtc);

        stopwatch.Stop();
        DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
        await governanceService.LogGraphExecutionCompletedAsync(
            new GraphExecutionCompletedPayload
            {
                Graph = graph,
                Execution = execution,
                IssuesSummary = new IssuesSummary()
            },
            TelemetryResultStatuses.Succeeded,
            startedUtc,
            completedUtc,
            stopwatch.ElapsedMilliseconds,
            correlationId: startedEventId,
            causationEventId: startedEventId);
    }
}
