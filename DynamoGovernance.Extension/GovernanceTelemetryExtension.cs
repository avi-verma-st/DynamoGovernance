using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Dynamo.Extensions;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.Logging;

using DynamoGovernance.Core.Models;
using DynamoGovernance.Core.Services;

namespace DynamoGovernance.Extension;

public sealed class GovernanceTelemetryExtension : IExtension
{
    private readonly object _subscriptionLock = new();

    private readonly Dictionary<HomeWorkspaceModel, WorkspaceSubscription>
        _subscriptions = [];

    private GovernanceService? _governanceService;
    private ReadyParams? _readyParams;
    private readonly DiagnosticLogger _diagnosticLogger = new();

    public string UniqueId =>
        "F2BA577E-4C5C-4A37-8BFC-2A5C11FAC698";

    public string Name =>
        "Dynamo Governance Telemetry";
    // STARTUP
    public void Startup(StartupParams sp)
    {
        try
        {
            _governanceService = new GovernanceService();

            (string hostName, string? hostVersion) =
                Utils.DynamoUtils.GetHostApplication();

            string dynamoVersion =
                sp.DynamoVersion?.ToString() ?? "unknown";

            string extensionVersion =
                typeof(GovernanceTelemetryExtension)
                    .Assembly
                    .GetName()
                    .Version?
                    .ToString(3) ?? "unknown";

            _governanceService.StartSession(
                dynamoVersion,
                hostName,
                hostVersion,
                extensionVersion);
        }
        catch (Exception ex)
        {
            _governanceService = null;
            _diagnosticLogger.LogError(nameof(GovernanceTelemetryExtension), "Failed to start governance telemetry session.", ex);

        }
    }
    // READY
    public void Ready(ReadyParams sp)
    {
        try
        {
            _readyParams = sp;
            sp.CurrentWorkspaceOpened +=
                OnCurrentWorkspaceOpened;

            sp.CurrentWorkspaceRemoveStarted +=
                OnCurrentWorkspaceRemoveStarted;
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(nameof(GovernanceTelemetryExtension), "Failed to attach opening and closing event listeners.", ex);
        }
    }
    // SHUTDOWN / DISPOSE
    public void Shutdown()
    {
        UnsubscribeAll();

        _governanceService?.EndSession();
    }
    public void Dispose()
    {
        UnsubscribeAll();

        _governanceService?.Dispose();

        _governanceService = null;
    }

    // WORKSPACE EVENTS
    private void OnCurrentWorkspaceOpened(
        IWorkspaceModel workspace)
    {
        // Ignore CustomNodeWorkspaceModel (.dyf).
        if (workspace is not HomeWorkspaceModel homeWorkspace)
        {
            return;
        }
        if (homeWorkspace.Guid == Guid.Empty)
        {
            return;
        }
        SubscribeWorkspace(homeWorkspace);
    }
    private void OnCurrentWorkspaceRemoveStarted(
        IWorkspaceModel workspace)
    {
        if (workspace is not HomeWorkspaceModel homeWorkspace)
        {
            return;
        }
        lock (_subscriptionLock)
        {
            if (!_subscriptions.TryGetValue(
                    homeWorkspace,
                    out WorkspaceSubscription? subscription))
            {
                return;
            }
            LogGraphClosed(homeWorkspace);
            UnsubscribeWorkspace(homeWorkspace);
        }
    }
    // WORKSPACE SUBSCRIPTIONS
    private void SubscribeWorkspace(
        HomeWorkspaceModel workspace)
    {
        WorkspaceSubscription subscription =
            new(
                CreateGraphEventData(workspace));


        EventHandler<EventArgs> evaluationStarted =
            (_, _) =>
                CaptureExecutionStarted(subscription);


        EventHandler<EvaluationCompletedEventArgs>
            evaluationCompleted =
                (_, args) =>
                    LogExecutionCompleted(
                        workspace,
                        subscription,
                        args);

        Action saved =
            () => LogGraphSaved(
                workspace,
                subscription);

        subscription.Saved = saved;
        subscription.EvaluationStarted = evaluationStarted;
        subscription.EvaluationCompleted = evaluationCompleted;

        lock (_subscriptionLock)
        {
            if (_subscriptions.ContainsKey(workspace))
            {
                return;
            }

            _subscriptions.Add(
                workspace,
                subscription);

            workspace.EvaluationStarted +=
                evaluationStarted;

            workspace.EvaluationCompleted +=
                evaluationCompleted;

            workspace.Saved += saved;

            _governanceService?.LogGraphOpened(
                subscription.Snapshot);
        }
    }
    private void UnsubscribeWorkspace(
        HomeWorkspaceModel workspace)
    {
        lock (_subscriptionLock)
        {
            if (!_subscriptions.Remove(
                    workspace,
                    out WorkspaceSubscription? subscription))
            {
                return;
            }

            workspace.EvaluationStarted -=
                subscription.EvaluationStarted;

            workspace.EvaluationCompleted -=
                subscription.EvaluationCompleted;

            workspace.Saved -= subscription.Saved;
        }
    }
    private void UnsubscribeAll()
    {
        try
        {
            ReadyParams? readyParams =
                _readyParams;

            _readyParams = null;


            if (readyParams is not null)
            {

                readyParams.CurrentWorkspaceOpened -=
                    OnCurrentWorkspaceOpened;

                readyParams.CurrentWorkspaceRemoveStarted -=
                    OnCurrentWorkspaceRemoveStarted;
            }


            HomeWorkspaceModel[] workspaces;

            lock (_subscriptionLock)
            {
                workspaces =
                    _subscriptions.Keys.ToArray();
            }


            foreach (
                HomeWorkspaceModel workspace
                in workspaces)
            {
                LogGraphClosed(workspace);
                UnsubscribeWorkspace(workspace);
            }
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(nameof(GovernanceTelemetryExtension), "Failed to unsubscribe from all workspace events.", ex);
        }
    }
    // GRAPH LIFECYCLE
    private void LogGraphSaved(
        HomeWorkspaceModel workspace,
        WorkspaceSubscription subscription)
    {
        lock (_subscriptionLock)
        {
            GraphEventData snapshot = CreateGraphEventData(
                workspace);
            subscription.Snapshot = snapshot;
            _governanceService?.LogGraphSaved(snapshot);
        }
    }

    private void LogGraphClosed(
        HomeWorkspaceModel workspace)
    {
        lock (_subscriptionLock)
        {
            if (!_subscriptions.TryGetValue(
                    workspace,
                    out WorkspaceSubscription? subscription))
            {
                return;
            }

            GraphEventData liveSnapshot = CreateGraphEventData(
                workspace);

            if (HasGraphContent(liveSnapshot))
            {
                subscription.Snapshot = liveSnapshot;
            }

            _governanceService?.LogGraphClosed(
                subscription.Snapshot);
        }
    }

    private static bool HasGraphContent(
        GraphEventData snapshot)
    {
        return !string.IsNullOrWhiteSpace(snapshot.Name) ||
            !string.IsNullOrWhiteSpace(snapshot.FilePath) ||
            snapshot.NodeCount > 0 ||
            snapshot.Packages.Count > 0;
    }

    private static GraphEventData CreateGraphEventData(
        HomeWorkspaceModel workspace)
    {
        string? filePath = workspace.FileName;
        return new GraphEventData
        {
            GraphId = workspace.Guid,
            Name = workspace.Name,
            FilePath = filePath,
            FileSizeBytes = Utils.FileUtils.GetFileSizeBytes(filePath),
            NodeCount = workspace.Nodes.Count(),
            Packages = Utils.DynamoUtils.GetPackages(workspace)
        };
    }
    // GRAPH EXECUTION
    private void CaptureExecutionStarted(
        WorkspaceSubscription subscription)
    {
        try
        {
            Guid executionId = Guid.NewGuid();
            DateTimeOffset occurredUtc = DateTimeOffset.UtcNow;
            bool executionAlreadyInProgress;

            lock (subscription)
            {
                executionAlreadyInProgress = subscription.ExecutionId.HasValue;

                if (!executionAlreadyInProgress)
                {
                    subscription.ExecutionId = executionId;
                }
            }

            if (executionAlreadyInProgress)
            {
                _diagnosticLogger.LogInformation(
                    nameof(GovernanceTelemetryExtension),
                    nameof(CaptureExecutionStarted),
                    $"Ignored overlapping execution start for graph {subscription.GraphId}.");
                return;
            }

            _governanceService?.LogExecutionStarted(
                executionId,
                subscription.GraphId,
                occurredUtc);
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(
                nameof(GovernanceTelemetryExtension),
                "Failed to log graph execution started event.",
                ex);
        }
    }
    private void LogExecutionCompleted(
        HomeWorkspaceModel workspace,
        WorkspaceSubscription subscription,
        EvaluationCompletedEventArgs eventArgs)
    {
        Guid? executionId;

        lock (subscription)
        {
            executionId = subscription.ExecutionId;
            subscription.ExecutionId = null;
        }

        if (!executionId.HasValue)
        {
            _diagnosticLogger.LogInformation(
                nameof(GovernanceTelemetryExtension),
                nameof(LogExecutionCompleted),
                $"Ignored execution completion without a matching start for graph {subscription.GraphId}.");
            return;
        }

        try
        {
            lock (_subscriptionLock)
            {
                GraphEventData snapshot = eventArgs.EvaluationTookPlace
                    ? CreateGraphEventData(workspace)
                    : subscription.Snapshot;

                subscription.Snapshot = snapshot;
                _governanceService?.LogExecutionEnded(
                    executionId.Value,
                    snapshot,
                    eventArgs.EvaluationTookPlace,
                    eventArgs.EvaluationTookPlace && eventArgs.EvaluationSucceeded);
            }
        }
        catch (Exception ex)
        {
            _diagnosticLogger.LogError(
                nameof(GovernanceTelemetryExtension),
                "Failed to log graph execution completed event.",
                ex);
        }
    }
    // INTERNAL RECORDS
    private sealed class WorkspaceSubscription
    {
        public WorkspaceSubscription(
            GraphEventData snapshot)
        {
            GraphId = snapshot.GraphId;
            Snapshot = snapshot;
        }
        public Guid? ExecutionId { get; set; }

        public Guid GraphId { get; set; }

        public GraphEventData Snapshot { get; set; }
        public Action Saved { get; set; } = null!;

        public EventHandler<EventArgs> EvaluationStarted { get; set; } = null!;

        public EventHandler<EvaluationCompletedEventArgs>
            EvaluationCompleted
        { get; set; } = null!;
    }
}