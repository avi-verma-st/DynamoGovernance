using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Dynamo.Extensions;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using DynamoGovernance.Core.Models;
using DynamoGovernance.Core.Services;

namespace DynamoGovernance.Extension;

public sealed class GovernanceTelemetryExtension : IExtension
{
    private const int MaximumNodeTypeSummaryItems = 50;
    private const string BuiltInNodeOrigin = "built_in";
    private const string PackageNodeOrigin = "package";
    private const string LocalCustomNodeOrigin = "local_custom_node";
    private const string UnknownNodeOrigin = "unknown";

    private static readonly ConcurrentDictionary<string, PackageLookup> PackageLookupCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _subscriptionLock = new();
    private readonly Dictionary<WorkspaceModel, WorkspaceSubscription> _subscriptions = [];
    private readonly ConcurrentDictionary<Guid, ExecutionTracking> _activeExecutions = new();
    private readonly ConcurrentDictionary<Guid, long> _executionNumbers = new();
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
        catch (Exception exception)
        {
            _governanceService?.LogExtensionError("extension_ready", exception);
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

            Action<NodeModel> nodeAdded = node => OnNodeChanged(
                TelemetryEventTypes.NodeAdded,
                workspace,
                node);
            Action<NodeModel> nodeRemoved = node => OnNodeChanged(
                TelemetryEventTypes.NodeRemoved,
                workspace,
                node);

            workspace.NodeAdded += nodeAdded;
            workspace.NodeRemoved += nodeRemoved;

            EventHandler<EventArgs>? evaluationStarted = null;
            EventHandler<EvaluationCompletedEventArgs>? evaluationCompleted = null;
            if (workspace is HomeWorkspaceModel homeWorkspace)
            {
                evaluationStarted = (_, _) => OnEvaluationStarted(homeWorkspace);
                evaluationCompleted = (_, args) => OnEvaluationCompleted(homeWorkspace, args);
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

            _activeExecutions.TryRemove(workspace.Guid, out _);
            _executionNumbers.TryRemove(workspace.Guid, out _);
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

    private void OnNodeChanged(string eventType, WorkspaceModel workspace, NodeModel node)
    {
        try
        {
            NodeInventoryItem nodeInventory = DescribeNode(node);
            _governanceService?.LogNodeChanged(
                eventType,
                new NodeChangedPayload
                {
                    Graph = CreateGraphContext(workspace),
                    Node = new NodeContext
                    {
                        NodeId = node.GUID,
                        NodeName = node.Name,
                        NodeType = nodeInventory.NodeType,
                        IsCustomNode = nodeInventory.IsCustomNode,
                        NodeOrigin = nodeInventory.NodeOrigin,
                        PackageName = nodeInventory.Package?.Name,
                        PackageVersion = nodeInventory.Package?.Version
                    }
                });
        }
        catch (Exception exception)
        {
            _governanceService?.LogExtensionError(eventType, exception);
        }
    }

    private void OnEvaluationStarted(HomeWorkspaceModel workspace)
    {
        try
        {
            DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
            long executionNumber = _executionNumbers.AddOrUpdate(
                workspace.Guid,
                1,
                static (_, current) => current + 1);
            var execution = new GraphExecutionContext
            {
                ExecutionNumber = executionNumber,
                EvaluationPerformed = false,
                Trigger = GetExecutionTrigger(workspace)
            };
            Guid startedEventId = _governanceService?.LogGraphExecutionStarted(
                new GraphExecutionStartedPayload
                {
                    Graph = CreateGraphContext(workspace),
                    Execution = execution
                },
                startedUtc) ?? Guid.Empty;

            _activeExecutions[workspace.Guid] = new ExecutionTracking(
                startedUtc,
                Stopwatch.StartNew(),
                startedEventId,
                execution);
        }
        catch (Exception exception)
        {
            _governanceService?.LogExtensionError("graph.execution.started", exception);
        }
    }

    private void OnEvaluationCompleted(
        HomeWorkspaceModel workspace,
        EvaluationCompletedEventArgs eventArgs)
    {
        try
        {
            DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
            if (!_activeExecutions.TryRemove(workspace.Guid, out ExecutionTracking? tracking))
            {
                long executionNumber = _executionNumbers.AddOrUpdate(
                    workspace.Guid,
                    1,
                    static (_, current) => current + 1);
                tracking = new ExecutionTracking(
                    completedUtc,
                    Stopwatch.StartNew(),
                    Guid.Empty,
                    new GraphExecutionContext
                    {
                        ExecutionNumber = executionNumber,
                        EvaluationPerformed = eventArgs.EvaluationTookPlace,
                        Trigger = GetExecutionTrigger(workspace)
                    });
            }

            tracking.Stopwatch.Stop();
            IReadOnlyList<ExecutionIssue> issues = CreateExecutionIssues(workspace);
            bool evaluationSucceeded = eventArgs.EvaluationSucceeded;
            Exception? evaluationError = GetEvaluationError(eventArgs, evaluationSucceeded);
            string status = GetExecutionStatus(
                eventArgs.EvaluationTookPlace,
                evaluationSucceeded,
                issues);
            var execution = new GraphExecutionContext
            {
                ExecutionNumber = tracking.Execution.ExecutionNumber,
                EvaluationRequested = tracking.Execution.EvaluationRequested,
                EvaluationPerformed = eventArgs.EvaluationTookPlace,
                Trigger = tracking.Execution.Trigger
            };

            _ = _governanceService?.LogGraphExecutionCompletedAsync(
                new GraphExecutionCompletedPayload
                {
                    Graph = CreateGraphContext(workspace),
                    Execution = execution,
                    Issues = issues,
                    IssuesSummary = new IssuesSummary
                    {
                        CapturedCount = issues.Count,
                        TotalCount = issues.Count,
                        Truncated = false
                    },
                    Exception = evaluationError is null
                        ? null
                        : new ExecutionException
                        {
                            ExceptionType = evaluationError.GetType().FullName ?? evaluationError.GetType().Name,
                            Message = evaluationError.Message,
                            StackTrace = evaluationError.StackTrace
                        }
                },
                status,
                tracking.StartedUtc,
                completedUtc,
                tracking.Stopwatch.ElapsedMilliseconds,
                tracking.StartedEventId == Guid.Empty ? null : tracking.StartedEventId,
                tracking.StartedEventId == Guid.Empty ? null : tracking.StartedEventId);
        }
        catch (Exception exception)
        {
            _governanceService?.LogExtensionError("graph.execution.completed", exception);
        }
    }

    private static GraphContext CreateGraphContext(WorkspaceModel workspace)
    {
        NodeModel[] nodes = workspace.Nodes.ToArray();
        NodeInventoryItem[] inventory = nodes.Select(DescribeNode).ToArray();
        NodeTypeSummaryItem[] nodeTypeSummary = inventory
            .GroupBy(node => new
            {
                node.NodeType,
                node.NodeKind,
                node.NodeOrigin,
                node.AssemblyName,
                node.AssemblyVersion,
                PackageName = node.Package?.Name,
                PackageVersion = node.Package?.Version
            })
            .Select(group => new NodeTypeSummaryItem
            {
                NodeType = group.Key.NodeType,
                NodeKind = group.Key.NodeKind,
                NodeOrigin = group.Key.NodeOrigin,
                AssemblyName = group.Key.AssemblyName,
                AssemblyVersion = group.Key.AssemblyVersion,
                PackageName = group.Key.PackageName,
                PackageVersion = group.Key.PackageVersion,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.NodeType, StringComparer.Ordinal)
            .ToArray();
        GraphPackageReference[] packages = inventory
            .Where(item => item.Package is not null)
            .GroupBy(item => new
            {
                item.Package!.Name,
                item.Package.Version
            })
            .Select(group => new GraphPackageReference
            {
                Name = group.Key.Name,
                Version = group.Key.Version,
                NodeCount = group.Count(),
                NodeTypeCount = group.Select(item => item.NodeType).Distinct(StringComparer.Ordinal).Count()
            })
            .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new GraphContext
        {
            GraphId = workspace.Guid,
            GraphName = string.IsNullOrWhiteSpace(workspace.FileName)
                ? null
                : Path.GetFileName(workspace.FileName),
            IsSaved = !string.IsNullOrWhiteSpace(workspace.FileName),
            RunMode = workspace is HomeWorkspaceModel homeWorkspace
                ? homeWorkspace.RunSettings.RunType.ToString().ToLowerInvariant()
                : "not_applicable",
            NodeCount = nodes.Length,
            BuiltInNodeCount = inventory.Count(item => item.NodeOrigin == BuiltInNodeOrigin),
            PackageNodeCount = inventory.Count(item => item.NodeOrigin == PackageNodeOrigin),
            LocalCustomNodeCount = inventory.Count(item => item.NodeOrigin == LocalCustomNodeOrigin),
            UnknownNodeCount = inventory.Count(item => item.NodeOrigin == UnknownNodeOrigin),
            CustomNodeCount = inventory.Count(item => item.IsCustomNode),
            Packages = packages,
            NodeTypeSummary = nodeTypeSummary.Take(MaximumNodeTypeSummaryItems).ToArray(),
            NodeTypeSummaryTruncated = nodeTypeSummary.Length > MaximumNodeTypeSummaryItems
        };
    }

    private static NodeInventoryItem DescribeNode(NodeModel node)
    {
        Type nodeType = node.GetType();
        var assemblyName = nodeType.Assembly.GetName();
        bool isCustomNode = IsCustomNode(node);
        string? sourcePath = isCustomNode
            ? GetCustomNodeDefinitionPath(node)
            : GetAssemblyLocation(nodeType);
        PackageMetadata? package = ResolvePackage(sourcePath);
        string nodeOrigin = package is not null
            ? PackageNodeOrigin
            : isCustomNode
                ? LocalCustomNodeOrigin
                : string.IsNullOrWhiteSpace(sourcePath)
                    ? UnknownNodeOrigin
                    : BuiltInNodeOrigin;

        return new NodeInventoryItem(
            nodeType.FullName ?? nodeType.Name,
            isCustomNode ? "custom_node" : "compiled_node",
            nodeOrigin,
            assemblyName.Name ?? "unknown",
            assemblyName.Version?.ToString(),
            isCustomNode,
            package);
    }

    private static string? GetAssemblyLocation(Type nodeType)
    {
        try
        {
            return nodeType.Assembly.Location;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCustomNodeDefinitionPath(NodeModel node)
    {
        try
        {
            object? definition = node.GetType().GetProperty("Definition")?.GetValue(node);
            object? workspaceModel = definition?.GetType().GetProperty("WorkspaceModel")?.GetValue(definition);
            return GetStringProperty(workspaceModel, "FileName") ??
                GetStringProperty(definition, "FileName") ??
                GetStringProperty(definition, "Path");
        }
        catch
        {
            return null;
        }
    }

    private static string? GetStringProperty(object? source, string propertyName)
    {
        return source?.GetType().GetProperty(propertyName)?.GetValue(source) as string;
    }

    private static PackageMetadata? ResolvePackage(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        try
        {
            string normalizedPath = Path.GetFullPath(sourcePath);
            return PackageLookupCache
                .GetOrAdd(normalizedPath, static path => new PackageLookup(FindPackage(path)))
                .Package;
        }
        catch
        {
            return null;
        }
    }

    private static PackageMetadata? FindPackage(string sourcePath)
    {
        try
        {
            string? directoryPath = Directory.Exists(sourcePath)
                ? sourcePath
                : Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return null;
            }

            for (var directory = new DirectoryInfo(directoryPath); directory is not null; directory = directory.Parent)
            {
                string manifestPath = Path.Combine(directory.FullName, "pkg.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                JsonElement root = manifest.RootElement;
                string name = GetManifestString(root, "name") ?? directory.Name;
                string? version = GetManifestString(root, "version");
                return new PackageMetadata(name, version);
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? GetManifestString(JsonElement manifest, string propertyName)
    {
        return manifest.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static IReadOnlyList<ExecutionIssue> CreateExecutionIssues(WorkspaceModel workspace)
    {
        return workspace.Nodes
            .Select(node => new { Node = node, Severity = GetNodeSeverity(node) })
            .Where(item => item.Severity is not null)
            .Select(item => new ExecutionIssue
            {
                Severity = item.Severity!,
                Source = "node",
                NodeId = item.Node.GUID,
                NodeName = item.Node.Name,
                NodeType = item.Node.GetType().FullName ?? item.Node.GetType().Name,
                Message = item.Node.State.ToString()
            })
            .ToArray();
    }

    private static string? GetNodeSeverity(NodeModel node)
    {
        string state = node.State.ToString();
        if (state.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return "error";
        }

        if (state.Contains("warning", StringComparison.OrdinalIgnoreCase))
        {
            return "warning";
        }

        return null;
    }

    private static string GetExecutionStatus(
        bool evaluationTookPlace,
        bool evaluationSucceeded,
        IReadOnlyList<ExecutionIssue> issues)
    {
        if (!evaluationSucceeded || issues.Any(issue => issue.Severity == "error"))
        {
            return TelemetryResultStatuses.Failed;
        }

        if (!evaluationTookPlace)
        {
            return TelemetryResultStatuses.Skipped;
        }

        return issues.Any(issue => issue.Severity == "warning")
            ? TelemetryResultStatuses.SucceededWithWarnings
            : TelemetryResultStatuses.Succeeded;
    }

    private static Exception? GetEvaluationError(
        EvaluationCompletedEventArgs eventArgs,
        bool evaluationSucceeded)
    {
        if (evaluationSucceeded)
        {
            return null;
        }

        try
        {
            return eventArgs.Error;
        }
        catch
        {
            return null;
        }
    }

    private static string GetExecutionTrigger(HomeWorkspaceModel workspace)
    {
        string runMode = workspace.RunSettings.RunType.ToString();
        return runMode.Contains("automatic", StringComparison.OrdinalIgnoreCase)
            ? "automatic_change"
            : "manual_run";
    }

    private static bool IsCustomNode(NodeModel node)
    {
        return node.GetType().FullName?.Contains(
            ".CustomNodes.Function",
            StringComparison.Ordinal) == true;
    }

    private static (string HostName, string? HostVersion) GetHostApplication()
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            string hostName = process.ProcessName;
            string? hostVersion = process.MainModule?.FileVersionInfo.ProductVersion;
            return (hostName, hostVersion);
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

    private sealed record ExecutionTracking(
        DateTimeOffset StartedUtc,
        Stopwatch Stopwatch,
        Guid StartedEventId,
        GraphExecutionContext Execution);

    private sealed record NodeInventoryItem(
        string NodeType,
        string NodeKind,
        string NodeOrigin,
        string AssemblyName,
        string? AssemblyVersion,
        bool IsCustomNode,
        PackageMetadata? Package);

    private sealed record PackageMetadata(string Name, string? Version);

    private sealed record PackageLookup(PackageMetadata? Package);
}
