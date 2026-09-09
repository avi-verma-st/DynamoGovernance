using System.Diagnostics;
using Dynamo.Extensions;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using DynamoGovernance.Core.Models;
using DynamoGovernance.Core.Services;

namespace DynamoGovernance.Extension;

public sealed class GovernanceTelemetryExtension : IExtension
{
    private readonly object _subscriptionLock = new();
    private readonly Dictionary<WorkspaceModel, WorkspaceSubscription> _subscriptions = [];
    private GovernanceService? _governanceService;
    private ReadyParams? _readyParams;

    public string UniqueId => "F2BA577E-4C5C-4A37-8BFC-2A5C11FAC698";
    public string Name => "Dynamo Governance Telemetry";

    public void Startup(StartupParams sp)
    {
        try
        {
            _governanceService = new GovernanceService();
            (string hostName, string? hostVersion) = GetHostApplication();
            string dynamoVersion = sp.DynamoVersion?.ToString() ?? "unknown";
            string extensionVersion = typeof(GovernanceTelemetryExtension)
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
        catch
        {
            _governanceService = null;
        }
    }

    public void Ready(ReadyParams sp)
    {
        try
        {
            _readyParams = sp;
            sp.CurrentWorkspaceChanged += OnCurrentWorkspaceChanged;
            sp.CurrentWorkspaceOpened += OnCurrentWorkspaceOpened;
            sp.CurrentWorkspaceRemoveStarted += OnCurrentWorkspaceRemoveStarted;

            foreach (WorkspaceModel workspace in sp.WorkspaceModels.OfType<WorkspaceModel>())
            {
                SubscribeWorkspace(workspace);
            }

            if (sp.CurrentWorkspaceModel is WorkspaceModel currentWorkspace)
            {
                SubscribeWorkspace(currentWorkspace);
            }

            _governanceService?.LogExtensionReady();
        }
        catch
        {
            _governanceService?.LogExtensionError();
        }
    }

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

    private void OnCurrentWorkspaceChanged(IWorkspaceModel workspace)
    {
        if (workspace is WorkspaceModel workspaceModel)
        {
            SubscribeWorkspace(workspaceModel);
        }
    }

    private void OnCurrentWorkspaceOpened(IWorkspaceModel workspace)
    {
        if (workspace is WorkspaceModel workspaceModel)
        {
            SubscribeWorkspace(workspaceModel);
        }
    }

    private void OnCurrentWorkspaceRemoveStarted(IWorkspaceModel workspace)
    {
        if (workspace is WorkspaceModel workspaceModel)
        {
            UnsubscribeWorkspace(workspaceModel);
        }
    }

    private void SubscribeWorkspace(WorkspaceModel workspace)
    {
        lock (_subscriptionLock)
        {
            if (_subscriptions.ContainsKey(workspace))
            {
                return;
            }

            Action<NodeModel> nodeAdded = _ => LogNodeCount(TelemetryEventTypes.NodeAdded, workspace);
            Action<NodeModel> nodeRemoved = _ => LogNodeCount(TelemetryEventTypes.NodeRemoved, workspace);
            workspace.NodeAdded += nodeAdded;
            workspace.NodeRemoved += nodeRemoved;

            EventHandler<EventArgs>? evaluationStarted = null;
            EventHandler<EvaluationCompletedEventArgs>? evaluationCompleted = null;
            if (workspace is HomeWorkspaceModel homeWorkspace)
            {
                evaluationStarted = (_, _) => LogExecutionStarted(homeWorkspace);
                evaluationCompleted = (_, args) => LogExecutionCompleted(homeWorkspace, args);
                homeWorkspace.EvaluationStarted += evaluationStarted;
                homeWorkspace.EvaluationCompleted += evaluationCompleted;
            }

            _subscriptions.Add(
                workspace,
                new WorkspaceSubscription(
                    nodeAdded,
                    nodeRemoved,
                    evaluationStarted,
                    evaluationCompleted));
        }
    }

    private void UnsubscribeWorkspace(WorkspaceModel workspace)
    {
        lock (_subscriptionLock)
        {
            if (!_subscriptions.Remove(workspace, out WorkspaceSubscription? subscription))
            {
                return;
            }

            workspace.NodeAdded -= subscription.NodeAdded;
            workspace.NodeRemoved -= subscription.NodeRemoved;

            if (workspace is HomeWorkspaceModel homeWorkspace)
            {
                if (subscription.EvaluationStarted is not null)
                {
                    homeWorkspace.EvaluationStarted -= subscription.EvaluationStarted;
                }

                if (subscription.EvaluationCompleted is not null)
                {
                    homeWorkspace.EvaluationCompleted -= subscription.EvaluationCompleted;
                }
            }
        }
    }

    private void UnsubscribeAll()
    {
        try
        {
            ReadyParams? readyParams = _readyParams;
            _readyParams = null;
            if (readyParams is not null)
            {
                readyParams.CurrentWorkspaceChanged -= OnCurrentWorkspaceChanged;
                readyParams.CurrentWorkspaceOpened -= OnCurrentWorkspaceOpened;
                readyParams.CurrentWorkspaceRemoveStarted -= OnCurrentWorkspaceRemoveStarted;
            }

            WorkspaceModel[] workspaces;
            lock (_subscriptionLock)
            {
                workspaces = _subscriptions.Keys.ToArray();
            }

            foreach (WorkspaceModel workspace in workspaces)
            {
                UnsubscribeWorkspace(workspace);
            }
        }
        catch
        {
        }
    }

    private void LogNodeCount(string eventType, WorkspaceModel workspace)
    {
        try
        {
            _governanceService?.LogNodeChanged(eventType, workspace.Nodes.Count());
        }
        catch
        {
            _governanceService?.LogExtensionError();
        }
    }

    private void LogExecutionStarted(HomeWorkspaceModel workspace)
    {
        try
        {
            _governanceService?.LogGraphExecutionStarted(
                workspace.Nodes.Count(),
                DateTimeOffset.UtcNow);
        }
        catch
        {
            _governanceService?.LogExtensionError();
        }
    }

    private void LogExecutionCompleted(
        HomeWorkspaceModel workspace,
        EvaluationCompletedEventArgs eventArgs)
    {
        try
        {
            bool successful = eventArgs.EvaluationTookPlace && eventArgs.EvaluationSucceeded;
            _governanceService?.LogGraphExecutionCompleted(
                workspace.Nodes.Count(),
                successful,
                DateTimeOffset.UtcNow);
        }
        catch
        {
            _governanceService?.LogExtensionError();
        }
    }

    private static (string HostName, string? HostVersion) GetHostApplication()
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            return (
                process.ProcessName,
                process.MainModule?.FileVersionInfo.ProductVersion);
        }
        catch
        {
            return ("unknown", null);
        }
    }

    private sealed record WorkspaceSubscription(
        Action<NodeModel> NodeAdded,
        Action<NodeModel> NodeRemoved,
        EventHandler<EventArgs>? EvaluationStarted,
        EventHandler<EvaluationCompletedEventArgs>? EvaluationCompleted);
}
