# API Access Inventory

## Purpose

This document inventories the APIs and host capabilities accessed by the Dynamo Governance package. It records the configured API version, where each API is used, the data or behavior accessed, and the expected compatibility impact.

The inventory covers:

- `DynamoGovernance.Extension`, the non-visual telemetry extension.
- `DynamoGovernance.ViewExtension`, the Dynamo sidebar and menu extension.
- `DynamoGovernance.Core`, the telemetry models, event service, identity collection, serialization, and local logging implementation.
- Package manifests and external resources opened by the extension.

## Version summary

| Category | Configured or detected version | Source |
|---|---:|---|
| .NET runtime | .NET 8 | All project target frameworks |
| Dynamo Core API | `3.0.3.7597` | `DynamoVisualProgramming.Core` package reference |
| Dynamo WPF UI API | `3.0.3.7597` | `DynamoVisualProgramming.WpfUILibrary` package reference |
| Dynamo package engine baseline | `3.0.0` | `DeploymentFiles/pkg.json` |
| Core assembly | `2.0.0` | `DynamoGovernance.Core.csproj` |
| Telemetry extension assembly | `2.0.0` | `DynamoGovernance.Extension.csproj` |
| View-extension assembly | Project default | No explicit `Version` in `DynamoGovernance.ViewExtension.csproj` |
| Telemetry schema | `1.0` | `TelemetryEnvelope<TPayload>.SchemaVersion` |
| Telemetry event schema | `1.0` by default | `GovernanceService.LogEvent` |
| Windows/WPF | Version supplied by .NET 8 and the host operating system | `net8.0-windows` and `UseWPF` |
| Revit API | Not accessed | No Autodesk Revit API reference |
| Civil 3D API | Not accessed | No Autodesk Civil 3D API reference |

The Dynamo NuGet dependencies use `ExcludeAssets="runtime"`. They provide compile-time API references but are not deployed with the package. At runtime, the extension uses the Dynamo assemblies supplied by Revit, Civil 3D, or Dynamo Sandbox.

## 1. Dynamo extension lifecycle API

**Category:** Dynamo Core extension API  
**Namespace:** `Dynamo.Extensions`  
**Compile-time version:** `3.0.3.7597`  
**Project:** `DynamoGovernance.Extension`

| API access | Usage | Purpose |
|---|---|---|
| `IExtension` | Implemented by `GovernanceTelemetryExtension` | Registers a non-visual Dynamo extension. |
| `IExtension.UniqueId` | Returns the telemetry extension GUID | Supplies Dynamo's stable extension identity. |
| `IExtension.Name` | Returns the extension display name | Identifies the extension to Dynamo. |
| `IExtension.Startup(StartupParams)` | Initializes `GovernanceService` | Starts telemetry during Dynamo startup. |
| `StartupParams.DynamoVersion` | Read during startup | Records the actual host-supplied Dynamo runtime version. |
| `IExtension.Ready(ReadyParams)` | Subscribes to workspaces and events | Activates tracking after Dynamo initialization. |
| `IExtension.Shutdown()` | Ends the telemetry session | Unsubscribes and performs best-effort shutdown logging. |
| `IDisposable.Dispose()` | Releases extension resources | Removes subscriptions and disposes the logger. |
| `ReadyParams.WorkspaceModels` | Enumerated when ready | Discovers workspaces already open in Dynamo. |
| `ReadyParams.CurrentWorkspaceModel` | Read when ready | Ensures the active workspace is subscribed. |
| `ReadyParams.CurrentWorkspaceChanged` | Event subscription | Subscribes when the active workspace changes. |
| `ReadyParams.CurrentWorkspaceOpened` | Event subscription | Subscribes when a workspace opens. |
| `ReadyParams.CurrentWorkspaceRemoveStarted` | Event subscription | Unsubscribes before a workspace is removed. |
| `IWorkspaceModel` | Event parameter type | Represents a workspace supplied by Dynamo lifecycle events. |

**Compatibility sensitivity:** Medium. The standard lifecycle interfaces are stable, but the members exposed by `ReadyParams` must be verified against each supported Dynamo 3.x host runtime.

## 2. Dynamo workspace and execution runtime API

**Category:** Dynamo Core graph/runtime API  
**Namespaces:** `Dynamo.Graph.Workspaces`, `Dynamo.Models`  
**Compile-time version:** `3.0.3.7597`  
**Project:** `DynamoGovernance.Extension`

| API access | Data or behavior accessed | Telemetry use |
|---|---|---|
| `WorkspaceModel` | Concrete Dynamo workspace model | Subscription identity and graph context. |
| `WorkspaceModel.Guid` | Workspace GUID | `graph_id`, execution tracking key, and sequence key. |
| `WorkspaceModel.FileName` | Graph file path | Determines graph name and whether the graph is saved. Only `Path.GetFileName` is retained. |
| `WorkspaceModel.Nodes` | Current node collection | Counts nodes, summarizes types, and finds node issues. |
| `WorkspaceModel.NodeAdded` | Node-added event | Emits `node.added`. |
| `WorkspaceModel.NodeRemoved` | Node-removed event | Emits `node.removed`. |
| `HomeWorkspaceModel` | Executable home workspace | Restricts execution tracking to runnable home graphs. |
| `HomeWorkspaceModel.EvaluationStarted` | Evaluation-start event | Emits `graph.execution.started` and starts duration tracking. |
| `HomeWorkspaceModel.EvaluationCompleted` | Evaluation-completed event | Emits `graph.execution.completed`. |
| `HomeWorkspaceModel.RunSettings` | Workspace run configuration | Reads the current run mode. |
| `RunSettings.RunType` | Manual or automatic run type | Populates `run_mode` and infers `manual_run` or `automatic_change`. |
| `EvaluationCompletedEventArgs.EvaluationTookPlace` | Evaluation execution flag | Distinguishes completed evaluations from skipped runs. |
| `EvaluationCompletedEventArgs.EvaluationSucceeded` | Evaluation result flag | Determines success or failure status. |
| `EvaluationCompletedEventArgs.Error` | Evaluation exception, when available | Captures bounded exception diagnostics for failed runs. |

**Compatibility sensitivity:** Medium to high. These APIs expose Dynamo's concrete workspace and evaluation models. They require direct runtime testing because event signatures or model behavior may change between Dynamo releases even when assembly loading succeeds.

## 3. Dynamo node model API

**Category:** Dynamo Core graph API  
**Namespace:** `Dynamo.Graph.Nodes`  
**Compile-time version:** `3.0.3.7597`  
**Project:** `DynamoGovernance.Extension`

| API access | Data accessed | Telemetry use |
|---|---|---|
| `NodeModel` | Concrete node model | Represents nodes added, removed, or evaluated. |
| `NodeModel.GUID` | Node identifier | Populates `node_id`. |
| `NodeModel.Name` | Node display name | Populates `node_name`. |
| `NodeModel.State` | Current node state | Classifies errors and warnings using the state's text. |
| `NodeModel.GetType()` | Runtime CLR node type | Captures node type, assembly name, and assembly version. |
| Node type full name containing `.CustomNodes.Function` | Runtime type-name convention | Classifies a node as `custom_node`; all others are classified as `compiled_node`. |
| Runtime property inspection of `Definition` and `WorkspaceModel.FileName` | Custom-node definition path, when exposed by the Dynamo runtime | Resolves custom-node package ownership without adding a host API dependency. |
| Node assembly location | Compiled node implementation path | Resolves compiled-node package ownership from the nearest `pkg.json`. |

The extension does not read node input values, output values, connector values, Revit elements, Civil 3D objects, or graph source code.

**Compatibility sensitivity:** Medium. Core properties are commonly available, but custom-node detection depends on a Dynamo implementation type-name convention rather than a dedicated classification API.

## 4. Dynamo WPF view-extension API

**Category:** Dynamo WPF UI API  
**Namespace:** `Dynamo.Wpf.Extensions`  
**Compile-time version:** `3.0.3.7597`  
**Project:** `DynamoGovernance.ViewExtension`

| API access | Usage | Purpose |
|---|---|---|
| `IViewExtension` | Implemented by `GovernanceViewExtension` | Registers a Dynamo UI extension. |
| `IViewExtension.UniqueId` | Returns the view-extension GUID | Supplies Dynamo's stable view-extension identity. |
| `IViewExtension.Name` | Returns `Dynamo Governance` | Provides the menu and sidebar identity. |
| `IViewExtension.Startup(ViewStartupParams)` | Implemented with no current behavior | Satisfies the view-extension lifecycle. |
| `IViewExtension.Loaded(ViewLoadedParams)` | Creates menu UI and opens the sidebar | Activates the interface after Dynamo UI loading. |
| `ViewLoadedParams.AddExtensionMenuItem(MenuItem)` | Adds the extension menu entry | Creates `Extensions > Dynamo Governance > Launch`. |
| `ViewLoadedParams.AddToExtensionsSideBar(IViewExtension, UserControl)` | Adds `GovernanceView` | Opens or reopens the extension sidebar. |
| `IViewExtension.Shutdown()` | Removes local event handlers and references | Releases UI resources during shutdown. |
| `IDisposable.Dispose()` | Removes local event handlers and references | Releases UI resources during disposal. |

**Compatibility sensitivity:** Medium. The extension directly uses Dynamo's WPF integration API and must run only in Dynamo hosts that provide the expected WPF UI library.

## 5. WPF desktop UI API

**Category:** Microsoft WPF desktop API  
**Namespaces:** `System.Windows`, `System.Windows.Controls`  
**Runtime version:** .NET 8 for Windows  
**Project:** `DynamoGovernance.ViewExtension`

| API access | Purpose |
|---|---|
| `UserControl` | Hosts the extension's sidebar content. |
| `MenuItem` | Creates the persistent launch menu and handles `Click`. |
| `StackPanel` | Lays out headings, descriptions, and resource buttons. |
| `ScrollViewer` | Makes sidebar content vertically scrollable. |
| `TextBlock` | Displays headings and descriptions. |
| `Button` | Opens resource links. |
| `Separator` | Separates primary and secondary resource areas. |
| `Thickness` | Configures margins and padding. |
| `FontWeights` | Configures text emphasis. |
| `HorizontalAlignment` and `ScrollBarVisibility` | Configures layout behavior. |
| `RoutedEventArgs` | Handles menu-item and button click events. |
| `MessageBox.Show` | Displays an error if a resource cannot be opened. |

The UI is created programmatically; the package does not access XAML loading, Dynamo view models, the Dynamo command-execution API, or host-specific Revit/Civil 3D windows.

**Compatibility sensitivity:** Low within .NET 8 Dynamo 3.x hosts. This assembly cannot load in Dynamo 2.x/.NET Framework hosts without a separate build.

## 6. .NET process and host inspection API

**Category:** .NET runtime and operating-system integration  
**Namespaces:** `System.Diagnostics`, `System.Runtime.InteropServices`, `System`  
**Runtime version:** .NET 8  
**Projects:** `DynamoGovernance.Extension`, `DynamoGovernance.Core`

| API access | Data accessed | Telemetry use |
|---|---|---|
| `Process.GetCurrentProcess()` | Current host process | Determines whether the extension is running in Revit, Civil 3D, Sandbox, or another process. |
| `Process.ProcessName` | Executable process name | Populates `host_name`. |
| `Process.MainModule.FileVersionInfo.ProductVersion` | Host executable product version | Populates `host_version`. |
| `Environment.ProcessId` | Current process ID | Populates `process_id`. |
| `RuntimeInformation.ProcessArchitecture` | Current process architecture | Populates `process_architecture`. |
| `Environment.Version` | Loaded .NET runtime version | Populates `runtime_version`. |
| `Stopwatch` | Monotonic elapsed time | Measures session duration, graph execution duration, and telemetry record creation time. |
| Assembly metadata APIs | Extension and node assembly names/versions | Records extension version and summarizes node implementations. |

`Process.MainModule` access may be restricted by operating-system policy. The implementation catches failures and substitutes `unknown` values.

**Compatibility sensitivity:** Low. Values depend on the actual host executable and runtime rather than a fixed Revit or Civil 3D API contract.

## 7. Windows identity and machine API

**Category:** .NET environment/Windows identity context  
**Namespace:** `System`  
**Runtime version:** .NET 8  
**Project:** `DynamoGovernance.Core`

| API access | Data accessed | Telemetry field |
|---|---|---|
| `Environment.UserDomainName` | Windows account domain | Part of `user_id`. |
| `Environment.UserName` | Windows account name | Part of `user_id`. |
| `Environment.MachineName` | Windows device name | `machine_id`. |

The current identity format is `domain\user`. These values are written in plain text, and `identifiers_protected` is currently `false`.

**Compatibility and privacy sensitivity:** High for privacy; low for API compatibility. This is local environment information, not Autodesk identity, Microsoft Entra ID, or Dynamo account data.

## 8. .NET filesystem API

**Category:** .NET filesystem API  
**Namespace:** `System.IO`  
**Runtime version:** .NET 8  
**Projects:** `DynamoGovernance.Core`, `DynamoGovernance.Extension`

| API access | Purpose |
|---|---|
| `Environment.GetFolderPath(LocalApplicationData)` | Selects the current user's local application-data directory. |
| `Path.Combine` | Builds the log directory and daily log-file path. |
| `Path.GetFileName` | Removes directories from Dynamo graph paths before telemetry storage. |
| `Path.GetFullPath` and `Path.GetDirectoryName` | Normalizes node source paths for package lookup. |
| `DirectoryInfo.Parent` | Walks from a node assembly or definition toward its package root. |
| `File.Exists` and `File.ReadAllText` | Finds and reads the nearest package `pkg.json`. |
| `Directory.CreateDirectory` | Creates `%LocalAppData%\DynamoGovernance\Logs`. |
| `File.AppendAllTextAsync` | Appends one JSON object per line to the daily JSONL file. |
| `Environment.NewLine` | Delimits JSONL records. |
| `DateTime.UtcNow` | Selects `telemetry_yyyy-MM-dd.jsonl`. |

The extension writes local telemetry only. It does not upload files, inspect arbitrary directories, or modify Dynamo graph files.

**Compatibility sensitivity:** Low. File creation can still fail because of permissions, profile restrictions, storage exhaustion, antivirus controls, or file locking; failures are caught and counted as dropped records.

## 9. .NET JSON serialization API

**Category:** .NET serialization API  
**Namespaces:** `System.Text.Json`, `System.Text.Json.Serialization`  
**Runtime version:** .NET 8  
**Projects:** `DynamoGovernance.Core`, `DynamoGovernance.Extension`

| API access | Purpose |
|---|---|
| `JsonSerializer.Serialize` | Converts telemetry envelopes to compact JSON. |
| `JsonSerializerOptions.WriteIndented` | Keeps each event on one JSONL line. |
| `JsonSerializerOptions.DefaultIgnoreCondition` | Omits properties whose values are `null`. |
| `JsonPropertyNameAttribute` | Defines stable snake-case telemetry property names. |
| `JsonDocument.Parse` | Reads package name and version from a package `pkg.json`. |

Serialization uses only .NET-provided APIs and does not require a third-party JSON package.

**Compatibility sensitivity:** Low on .NET 8. The externally observable contract is the telemetry schema, currently version `1.0`.

## 10. .NET concurrency and shutdown API

**Category:** .NET threading and task API  
**Namespaces:** `System.Threading`, `System.Threading.Channels`, `System.Threading.Tasks`, `System.Collections.Concurrent`  
**Runtime version:** .NET 8  
**Projects:** `DynamoGovernance.Core`, `DynamoGovernance.Extension`

| API access | Purpose |
|---|---|
| `Channel.CreateBounded<object>` | Creates the telemetry queue with capacity `1024`. |
| `ChannelWriter.TryWrite` | Enqueues without blocking Dynamo's event thread. |
| `ChannelReader.ReadAllAsync` | Processes queued records on the writer task. |
| `Task.Run` | Starts the background JSONL writer. |
| `CancellationTokenSource` | Cancels the writer if shutdown flushing exceeds its limit. |
| `Task.Wait(TimeSpan)` | Allows a best-effort shutdown flush for up to 200 milliseconds. |
| `Interlocked` and `Volatile` | Protect counters and lifecycle state. |
| `ConcurrentDictionary<Guid, ...>` | Tracks active graph executions and per-workspace execution numbers. |
| `ConcurrentDictionary<string, ...>` | Caches package metadata lookups by normalized node source path. |
| `lock` | Protects workspace subscription registration and removal. |

The bounded channel is configured with `BoundedChannelFullMode.Wait`, but producers use `TryWrite`; therefore a full queue causes records to be dropped rather than blocking Dynamo.

**Compatibility sensitivity:** Low. Shutdown timing and host process termination can affect how many queued events are flushed.

## 11. Windows shell and default-browser API

**Category:** Operating-system shell integration  
**Namespace:** `System.Diagnostics`  
**Runtime version:** .NET 8 plus the installed Windows shell  
**Project:** `DynamoGovernance.ViewExtension`

| API access | Purpose |
|---|---|
| `Process.Start(ProcessStartInfo)` | Opens a selected governance resource. |
| `ProcessStartInfo.FileName` | Supplies the HTTPS resource URL. |
| `ProcessStartInfo.UseShellExecute = true` | Delegates the URL to the user's default browser. |

This is not a direct HTTP API call. Authentication, cookies, proxy handling, TLS, conditional access, and page rendering are handled by the default browser and organizational services.

**Compatibility sensitivity:** Low for Dynamo; dependent on Windows URL associations and security policy.

## 12. External SharePoint resources

**Category:** External web resources  
**Service:** Microsoft SharePoint Online  
**API version:** Not applicable; browser navigation only  
**Project:** `DynamoGovernance.ViewExtension`

The extension opens these HTTPS resources:

| Resource | Access behavior |
|---|---|
| Design Automation Hub home | Opens the SharePoint site home page in the default browser. |
| Dynamo Training | Opens the configured SharePoint learning-resources list. |
| Dynamo Development Resources | Opens the configured SharePoint document-library folder. |

The extension does not call Microsoft Graph, the SharePoint REST API, Azure APIs, or an authentication SDK. It stores no SharePoint access token and does not inspect browser responses.

**Compatibility sensitivity:** External configuration risk. Links, permissions, tenant policy, or SharePoint content can change independently of the extension.

## 13. Manifest and package discovery API

**Category:** Dynamo package and extension discovery contract  
**Engine baseline:** Dynamo `3.0.0`  
**Files:** `DeploymentFiles/pkg.json` and the two extension-definition XML files

| Contract | Current use |
|---|---|
| Dynamo `pkg.json` | Declares a binary Dynamo package named `Dynamo Governance`. |
| `engine = dynamo` | Limits the package engine to Dynamo. |
| `engine_version = 3.0.0` | Declares the package's minimum Dynamo engine baseline. |
| `ExtensionDefinition` | Loads `DynamoGovernance.Extension.GovernanceTelemetryExtension`. |
| `ViewExtensionDefinition` | Loads `DynamoGovernance.ViewExtension.GovernanceViewExtension`. |
| `AssemblyPath` | Resolves the deployed assemblies under the package's `bin` directory. |

The manifests do not declare a Revit-only or Civil 3D-only dependency. Runtime compatibility is determined by Dynamo version, .NET runtime, UI availability, and the host's package-loading behavior.

## 14. Internal package APIs

**Category:** Dynamo Governance internal API  
**Current core/telemetry assembly version:** `2.0.0`

| Internal API | Purpose |
|---|---|
| `GovernanceService` | Owns session state and creates telemetry events. |
| `TelemetryLogger` | Queues, serializes, and writes JSONL records. |
| `IdentityService` | Reads the Windows user and machine identity. |
| `TelemetryEnvelope<TPayload>` | Defines the versioned event envelope. |
| Telemetry payload models | Define session, graph, node, issue, exception, result, identity, and application data. |
| `GovernanceResources` | Defines browser resource metadata and URLs. |
| `GovernanceView` | Constructs the sidebar UI and launches resources. |

These are package-owned APIs rather than Autodesk or Microsoft service APIs.

## 15. APIs not currently accessed

The package does **not** currently access:

- Autodesk Revit API assemblies or Revit documents, elements, parameters, transactions, or user identity.
- Autodesk Civil 3D or AutoCAD API assemblies, drawings, database objects, transactions, or user identity.
- Dynamo node input values, output values, preview geometry, connectors, or graph JSON contents.
- Dynamo command execution or graph modification APIs.
- Dynamo authentication, package-manager, analytics, or user-account APIs.
- Microsoft Graph, SharePoint REST, Azure, Application Insights, or another telemetry ingestion endpoint.
- Registry, Windows Management Instrumentation, credential stores, or environment variables other than the explicit `Environment` properties listed above.
- Network sockets or direct HTTP clients.

## 16. Recommended compatibility verification

For each supported Dynamo runtime, verify:

1. Both manifests discover and load their extension types.
2. `StartupParams.DynamoVersion` returns the expected host runtime version.
3. Existing, newly opened, changed, and removed workspaces subscribe and unsubscribe correctly.
4. Node addition and removal produce one matching event each.
5. Manual and automatic evaluations raise start and completion events.
6. Evaluation success, warning, error, and skipped states are classified correctly.
7. The extension menu and sidebar can be opened, closed, and reopened.
8. Browser links launch through the Windows shell.
9. JSONL files are created and remain valid one-event-per-line JSON.
10. Host shutdown does not block and performs the expected best-effort flush.

The minimum compile-time baseline is Dynamo `3.0.3.7597` on .NET 8. Revit 2024 and Civil 3D 2024 Dynamo 2.x runtimes are outside the current binary target and require a separate .NET Framework-compatible build if they must be supported.
