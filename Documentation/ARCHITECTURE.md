# Architecture

## Overview

DynamoGovernance is a .NET 8 Dynamo package containing a telemetry extension and a WPF view extension. The telemetry extension writes versioned events to local JSONL files. The first official schema is `1.0`; every event uses a stable envelope and a strongly typed payload selected by `event_type`.

## Projects

- `DynamoGovernance.Extension`: Dynamo `IExtension` lifecycle integration and host-process discovery.
- `DynamoGovernance.Core`: schema models, identity collection, event construction, diagnostic limits, and JSONL delivery.
- `DynamoGovernance.ViewExtension`: Dynamo `IViewExtension` lifecycle integration and a basic WPF sidebar.

## View-extension flow

1. Dynamo discovers `DynamoGovernance_ViewExtensionDefinition.xml` in the package's `extra` directory.
2. The manifest resolves `..\bin\DynamoGovernance.ViewExtension.dll` relative to that directory.
3. Dynamo creates `GovernanceViewExtension` and calls `Startup()`.
4. When the Dynamo UI is ready, Dynamo calls `Loaded()`.
5. `Loaded()` registers an `Extensions > Dynamo Governance > Launch` menu command.
6. `Loaded()` creates `GovernanceView` and passes it to `ViewLoadedParams.AddToExtensionsSideBar()` for the initial display.
7. If the sidebar is closed, selecting `Launch` creates a new `GovernanceView` and adds it to the sidebar again.
8. The WPF view displays a scrollable set of descriptive governance-resource buttons.
9. A resource click uses the Windows shell to open the target in the user's default browser.

`GovernanceResources` contains three direct destinations. `HubHome` supplies the primary Design Automation Hub button. The `Resources` collection supplies `Dynamo Training` as its first entry and `Dynamo Development Resources` as its second entry. All three destinations use their supplied canonical SharePoint URLs.

The menu remains available after the sidebar closes because closing the view does not unload `GovernanceViewExtension`. The menu click handler is detached during shutdown and disposal. The telemetry `IExtension` and UI `IViewExtension` have separate manifests and lifecycles. The view currently has no dependency on `GovernanceService` and does not emit telemetry when a resource is opened.

## Package discovery

The development package is rooted at `C:\DynamoDev\packages\DynamoGovernance`. Dynamo must have `C:\DynamoDev\packages` configured as a package path. Telemetry extension paths are resolved by the package loader, while the view-extension assembly path is resolved relative to the manifest in `extra`; consequently, its manifest uses `..\bin\DynamoGovernance.ViewExtension.dll`.

## Event model

`TelemetryEnvelope<TPayload>` contains the universal fields: `schema_version`, `event_type`, `event_version`, `event_id`, `session_id`, `sequence_number`, `correlation`, `timing`, `identity`, `application`, `result`, `payload`, and `telemetry`.

Implemented payloads are `SessionStartedPayload`, `ExtensionReadyPayload`, `SessionEndedPayload`, `GraphExecutionStartedPayload`, `GraphExecutionCompletedPayload`, `NodeChangedPayload`, and `ExtensionErrorPayload`.

## Event flow

1. `GovernanceTelemetryExtension.Startup` creates `GovernanceService` and starts a session.
2. `GovernanceService` captures common identity and application context.
3. Each event receives an ID, session sequence, correlation, timing, result, and typed payload.
4. `TelemetryLogger.Log` attempts to enqueue the event in a bounded channel.
5. A background worker serializes queued records and appends one JSON object per line.
6. `Shutdown` records session duration; `Dispose` performs a short best-effort flush.

## Dynamo event integration

`GovernanceTelemetryExtension.Ready` subscribes existing and newly opened workspaces. Every workspace supplies `NodeAdded` and `NodeRemoved`; `HomeWorkspaceModel` additionally supplies `EvaluationStarted` and `EvaluationCompleted`. Subscriptions are removed when a workspace is removed and again during shutdown or disposal.

Execution state is maintained per workspace. A monotonic stopwatch, execution number, start event ID, and graph context connect `graph.execution.started` with `graph.execution.completed`. Completion records include evaluation status, bounded node warning/error summaries, and an evaluation exception when Dynamo provides one.

Graph context records the file name for saved workspaces without including the containing directory. It also includes up to 50 node-type summary records grouped by runtime type, node kind, source assembly, and assembly version. This provides package-like provenance without depending on Dynamo Package Manager internals. `node_type_summary_truncated` indicates when additional types were omitted.

## Runtime isolation

- Event calls use non-blocking `Channel.TryWrite`.
- The queue is bounded to 1,024 records.
- Records are dropped under pressure instead of blocking Dynamo.
- Serialization and file I/O run on one background worker.
- Logging failures are isolated from the host.
- Shutdown flushing is limited to 200 milliseconds.
- Session completion is idempotent.

## Identity profile

The testing profile stores unprotected values: `DOMAIN\\username` from `windows_account` and the Windows machine name from `machine_name`. Records set `identifiers_protected` to `false` and `privacy_profile` to `testing_plaintext`. This must be reviewed before deployment.

## Correlation and ordering

- `sequence_number` increments atomically within a session.
- `session_id` groups every event produced during one Dynamo session.
- `correlation_id` groups events belonging to one operation or causal workflow, not the entire session.
- Standalone events use their own `event_id` as `correlation_id`.
- `graph.execution.started` uses its event ID as the execution correlation ID; the matching `graph.execution.completed` record reuses that value.
- `causation_event_id` identifies the event that caused another event.
- Envelope and payload versions evolve independently.

## Diagnostic boundaries

- Maximum 25 issues.
- Maximum 1,024 characters per issue message.
- Maximum 2,048 characters per exception message.
- Maximum 8,192 characters per stack trace.
- `issues_summary` reports captured, total, and truncated state.

## Schema evolution

Additive optional fields and new event types may remain in schema `1.x`. Renaming, removing, moving, or changing an envelope field requires schema `2.0`. Payload-only breaking changes increment the corresponding `event_version`.
