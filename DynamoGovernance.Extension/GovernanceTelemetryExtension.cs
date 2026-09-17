using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Dynamo.Extensions;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;

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

    public string UniqueId =>
        "F2BA577E-4C5C-4A37-8BFC-2A5C11FAC698";

    public string Name =>
        "Dynamo Governance Telemetry";


    // ============================================================
    // STARTUP
    // ============================================================

    public void Startup(StartupParams sp)
    {
        try
        {
            _governanceService = new GovernanceService();

            (string hostName, string? hostVersion) =
                GetHostApplication();

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
        catch
        {
            _governanceService = null;
        }
    }


    // ============================================================
    // READY
    // ============================================================

    public void Ready(ReadyParams sp)
    {
        try
        {
            _readyParams = sp;

            sp.CurrentWorkspaceChanged +=
                OnCurrentWorkspaceChanged;

            sp.CurrentWorkspaceOpened +=
                OnCurrentWorkspaceOpened;

            sp.CurrentWorkspaceRemoveStarted +=
                OnCurrentWorkspaceRemoveStarted;


            // Only subscribe to HOME workspaces (.dyn).
            //
            // Do NOT subscribe CustomNodeWorkspaceModel (.dyf)
            // because we do not want telemetry generated while
            // someone is editing a custom node.
            foreach (
                HomeWorkspaceModel workspace
                in sp.WorkspaceModels.OfType<HomeWorkspaceModel>())
            {
                SubscribeWorkspace(workspace);
            }


            // Subscribe current workspace only when it is
            // an actual .dyn graph.
            if (sp.CurrentWorkspaceModel
                is HomeWorkspaceModel currentWorkspace)
            {
                SubscribeWorkspace(currentWorkspace);
            }

        }
        catch
        {
        }
    }


    // ============================================================
    // SHUTDOWN / DISPOSE
    // ============================================================

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


    // ============================================================
    // WORKSPACE EVENTS
    // ============================================================

    private void OnCurrentWorkspaceChanged(
        IWorkspaceModel workspace)
    {
        // Only subscribe actual Dynamo graphs (.dyn).
        if (workspace is not HomeWorkspaceModel homeWorkspace)
        {
            return;
        }

        SubscribeWorkspace(homeWorkspace);
    }


    private void OnCurrentWorkspaceOpened(
        IWorkspaceModel workspace)
    {
        // Ignore CustomNodeWorkspaceModel (.dyf).
        if (workspace is not HomeWorkspaceModel homeWorkspace)
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

        LogGraphClosed(homeWorkspace);
        UnsubscribeWorkspace(homeWorkspace);
    }


    // ============================================================
    // WORKSPACE SUBSCRIPTIONS
    // ============================================================

    private void SubscribeWorkspace(
        HomeWorkspaceModel workspace)
    {
        lock (_subscriptionLock)
        {
            if (_subscriptions.TryGetValue(
                    workspace,
                    out WorkspaceSubscription? existingSubscription))
            {
                EnsureCurrentDocument(
                    workspace,
                    existingSubscription);
                return;
            }


            Guid graphId = workspace.Guid == Guid.Empty
                ? Guid.NewGuid()
                : workspace.Guid;

            WorkspaceSubscription subscription =
                new(
                    workspace.Guid,
                    CreateGraphEventData(workspace, graphId));


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


            workspace.EvaluationStarted +=
                evaluationStarted;

            workspace.EvaluationCompleted +=
                evaluationCompleted;

            workspace.Saved += saved;


            _subscriptions.Add(
                workspace,
                subscription);

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
                readyParams.CurrentWorkspaceChanged -=
                    OnCurrentWorkspaceChanged;

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
        catch
        {
            // Do not allow cleanup exceptions to crash Dynamo.
        }
    }


    // ============================================================
    // GRAPH LIFECYCLE
    // ============================================================

    private void LogGraphSaved(
        HomeWorkspaceModel workspace,
        WorkspaceSubscription subscription)
    {
        lock (_subscriptionLock)
        {
            GraphEventData snapshot = CreateGraphEventData(
                workspace,
                subscription.GraphId);

            subscription.SourceGraphId = workspace.Guid;
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
                workspace,
                subscription.GraphId);

            if (!IsDifferentDocument(
                    subscription,
                    liveSnapshot,
                    workspace.Guid))
            {
                subscription.Snapshot = liveSnapshot;
            }

            _governanceService?.LogGraphClosed(
                subscription.Snapshot);
        }
    }


    private static GraphEventData CreateGraphEventData(
        HomeWorkspaceModel workspace,
        Guid graphId)
    {
        string? filePath =
            string.IsNullOrWhiteSpace(workspace.FileName)
                ? null
                : workspace.FileName;

        return new GraphEventData
        {
            GraphId = graphId,
            Name = string.IsNullOrWhiteSpace(workspace.Name)
                ? "Untitled"
                : workspace.Name,
            FilePath = filePath,
            FileSizeBytes = GetFileSizeBytes(filePath),
            IsSaved = filePath is not null,
            HasUnsavedChanges = workspace.HasUnsavedChanges,
            NodeCount = workspace.Nodes.Count(),
            Packages = GetPackages(workspace)
        };
    }


    private void EnsureCurrentDocument(
        HomeWorkspaceModel workspace,
        WorkspaceSubscription subscription)
    {
        GraphEventData liveSnapshot = CreateGraphEventData(
            workspace,
            subscription.GraphId);

        if (!IsDifferentDocument(
                subscription,
                liveSnapshot,
                workspace.Guid))
        {
            subscription.Snapshot = liveSnapshot;
            return;
        }

        _governanceService?.LogGraphClosed(
            subscription.Snapshot);

        Guid graphId = workspace.Guid;
        if (graphId == Guid.Empty || graphId == subscription.GraphId)
        {
            graphId = Guid.NewGuid();
        }

        subscription.SourceGraphId = workspace.Guid;
        subscription.GraphId = graphId;
        subscription.Snapshot = CreateGraphEventData(
            workspace,
            graphId);

        _governanceService?.LogGraphOpened(
            subscription.Snapshot);
    }


    private static bool IsDifferentDocument(
        WorkspaceSubscription subscription,
        GraphEventData liveSnapshot,
        Guid sourceGraphId)
    {
        if (sourceGraphId != subscription.SourceGraphId)
        {
            return true;
        }

        string? previousPath = subscription.Snapshot.FilePath;
        string? currentPath = liveSnapshot.FilePath;

        return previousPath is not null &&
            !string.Equals(
                previousPath,
                currentPath,
                StringComparison.OrdinalIgnoreCase);
    }


    private static long? GetFileSizeBytes(
        string? filePath)
    {
        if (filePath is null)
        {
            return null;
        }

        try
        {
            FileInfo file = new(filePath);
            return file.Exists ? file.Length : null;
        }
        catch
        {
            return null;
        }
    }


    private static PackageReferenceData[] GetPackages(
        HomeWorkspaceModel workspace)
    {
        try
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            Type workspaceType = workspace.GetType().BaseType
                ?? workspace.GetType();

            object? dependencies = workspaceType
                .GetMethod(
                    "ComputeNodeLibraryDependencies",
                    flags)?
                .Invoke(workspace, null);

            dependencies ??= workspaceType
                .GetProperty(
                    "NodeLibraryDependencies",
                    flags)?
                .GetValue(workspace);

            if (dependencies is not System.Collections.IEnumerable items)
            {
                return [];
            }

            List<PackageReferenceData> packages = [];

            foreach (object dependency in items)
            {
                Type dependencyType = dependency.GetType();
                string? referenceType = dependencyType
                    .GetProperty("ReferenceType")?
                    .GetValue(dependency)?
                    .ToString();

                if (!string.IsNullOrWhiteSpace(referenceType) &&
                    !string.Equals(
                        referenceType,
                        "Package",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(referenceType) &&
                    !dependencyType.Name.Contains(
                        "PackageDependency",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? name = dependencyType
                    .GetProperty("Name")?
                    .GetValue(dependency)?
                    .ToString();

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string? version = dependencyType
                    .GetProperty("Version")?
                    .GetValue(dependency)?
                    .ToString();

                string? dependencyPath = dependencyType
                    .GetProperty(
                        "Path",
                        flags)?
                    .GetValue(dependency)?
                    .ToString();

                PackageManifest manifest =
                    ReadPackageManifest(dependencyPath);

                packages.Add(new PackageReferenceData
                {
                    Name = name,
                    Version = version ?? manifest.Version,
                    NodeCount = GetDependentNodeCount(
                        dependency,
                        flags),
                    Description = manifest.Description,
                    License = manifest.License,
                    RepositoryUrl = manifest.RepositoryUrl,
                    SiteUrl = manifest.SiteUrl
                });
            }

            return packages
                .GroupBy(package => new
                {
                    package.Name,
                    package.Version
                })
                .Select(group =>
                {
                    PackageReferenceData package = group.First();

                    return new PackageReferenceData
                    {
                        Name = package.Name,
                        Version = package.Version,
                        NodeCount = group.Sum(item => item.NodeCount),
                        Description = package.Description,
                        License = package.License,
                        RepositoryUrl = package.RepositoryUrl,
                        SiteUrl = package.SiteUrl
                    };
                })
                .OrderBy(package => package.Name)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }


    private static int GetDependentNodeCount(
        object dependency,
        System.Reflection.BindingFlags flags)
    {
        object? nodes = dependency
            .GetType()
            .GetProperty(
                "Nodes",
                flags)?
            .GetValue(dependency);

        if (nodes is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        if (nodes is not System.Collections.IEnumerable items)
        {
            return 0;
        }

        int count = 0;

        foreach (object _ in items)
        {
            count++;
        }

        return count;
    }


    private static PackageManifest ReadPackageManifest(
        string? dependencyPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dependencyPath))
            {
                return new PackageManifest();
            }

            DirectoryInfo? directory = File.Exists(dependencyPath)
                ? new FileInfo(dependencyPath).Directory
                : new DirectoryInfo(dependencyPath);

            for (int level = 0;
                directory is not null && level < 8;
                level++, directory = directory.Parent)
            {
                string manifestPath = Path.Combine(
                    directory.FullName,
                    "pkg.json");

                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                using System.Text.Json.JsonDocument document =
                    System.Text.Json.JsonDocument.Parse(
                        File.ReadAllText(manifestPath));

                System.Text.Json.JsonElement root =
                    document.RootElement;

                return new PackageManifest(
                    GetJsonString(root, "version"),
                    GetJsonString(root, "description"),
                    GetJsonString(root, "license"),
                    GetJsonString(root, "repository_url"),
                    GetJsonString(root, "site_url"));
            }
        }
        catch
        {
        }

        return new PackageManifest();
    }


    private static string? GetJsonString(
        System.Text.Json.JsonElement root,
        string propertyName)
    {
        return root.TryGetProperty(
                propertyName,
                out System.Text.Json.JsonElement value)
            && value.ValueKind ==
                System.Text.Json.JsonValueKind.String
                ? value.GetString()
                : null;
    }


    // ============================================================
    // GRAPH EXECUTION
    // ============================================================

    private static void CaptureExecutionStarted(
        WorkspaceSubscription subscription)
    {
        lock (subscription)
        {
            subscription.ExecutionStartedUtc =
                DateTimeOffset.UtcNow;
        }
    }


    private void LogExecutionCompleted(
        HomeWorkspaceModel workspace,
        WorkspaceSubscription subscription,
        EvaluationCompletedEventArgs eventArgs)
    {
        DateTimeOffset? executionStartedUtc;

        lock (subscription)
        {
            executionStartedUtc =
                subscription.ExecutionStartedUtc;
            subscription.ExecutionStartedUtc = null;
        }

        if (!eventArgs.EvaluationTookPlace)
        {
            return;
        }

        try
        {
            lock (_subscriptionLock)
            {
                EnsureCurrentDocument(
                    workspace,
                    subscription);

                GraphEventData snapshot = CreateGraphEventData(
                    workspace,
                    subscription.GraphId);

                subscription.Snapshot = snapshot;

                _governanceService?.LogExecutionStarted(
                    snapshot,
                    executionStartedUtc);

                _governanceService?.LogExecutionEnded(
                    snapshot,
                    true,
                    eventArgs.EvaluationSucceeded);
            }
        }
        catch
        {
        }
    }


    // ============================================================
    // HOST INFORMATION
    // ============================================================

    private static (
        string HostName,
        string? HostVersion)
        GetHostApplication()
    {
        try
        {
            using Process process =
                Process.GetCurrentProcess();


            return (
                process.ProcessName,
                process
                    .MainModule?
                    .FileVersionInfo
                    .ProductVersion);
        }
        catch
        {
            return (
                "unknown",
                null);
        }
    }


    // ============================================================
    // INTERNAL RECORDS
    // ============================================================

    private sealed class WorkspaceSubscription
    {
        public WorkspaceSubscription(
            Guid sourceGraphId,
            GraphEventData snapshot)
        {
            SourceGraphId = sourceGraphId;
            GraphId = snapshot.GraphId;
            Snapshot = snapshot;
        }

        public Guid SourceGraphId { get; set; }

        public Guid GraphId { get; set; }

        public GraphEventData Snapshot { get; set; }

        public DateTimeOffset? ExecutionStartedUtc { get; set; }

        public Action Saved { get; set; } = null!;

        public EventHandler<EventArgs> EvaluationStarted { get; set; } = null!;

        public EventHandler<EvaluationCompletedEventArgs>
            EvaluationCompleted { get; set; } = null!;
    }

    private sealed record PackageManifest(
        string? Version = null,
        string? Description = null,
        string? License = null,
        string? RepositoryUrl = null,
        string? SiteUrl = null);
}