# Architecture

## Overview

Dynamo Governance is a .NET 8 package for Dynamo 3.x with two independently loaded extensions:

- `GovernanceTelemetryExtension` observes Dynamo lifecycle, node changes, and graph evaluations.
- `GovernanceViewExtension` supplies the WPF resource sidebar.

Telemetry uses a minimal schema `2.0`. A `session.started` record stores identity and application context once. Every later record references that session with `session_id` and contains only event-specific data.

## Projects

- `DynamoGovernance.Core` — schema models, identity collection, event creation, bounded queue, JSON serialization, and local file delivery.
- `DynamoGovernance.Extension` — Dynamo `IExtension` lifecycle and workspace event subscriptions.
- `DynamoGovernance.ViewExtension` — Dynamo `IViewExtension`, menu registration, sidebar, and browser links.

## Telemetry record

Every record has:

- `schema_version`
- `event`
- `session_id`
- `occurred_utc`
- Optional `data`

No event IDs, sequence numbers, correlation objects, result envelopes, telemetry-production metadata, or repeated identity/application objects are generated.

## Session flow

1. Dynamo calls `GovernanceTelemetryExtension.Startup`.
2. `GovernanceService` creates a random `session_id`.
3. Windows identity and host/runtime context are captured.
4. One `session.started` record stores that context.
5. Later records carry the same `session_id` but do not repeat context.
6. `session.ended` is emitted once during shutdown or disposal.

## Dynamo event integration

`Ready` subscribes existing, current, and newly opened workspaces.

- `WorkspaceModel.NodeAdded` emits `node.added` with current `node_count`.
- `WorkspaceModel.NodeRemoved` emits `node.removed` with current `node_count`.
- `HomeWorkspaceModel.EvaluationStarted` emits `graph.execution.started` with current `node_count`.
- `HomeWorkspaceModel.EvaluationCompleted` emits `graph.execution.completed` with current `node_count` and `successful`.

`successful` is `true` only when Dynamo reports that evaluation took place and succeeded. No graph identity, graph name, node identity, node type, package, issue, exception, run mode, trigger, duration, or diagnostic text is collected.

Subscriptions are removed when a workspace is removed and during shutdown/disposal.

## Logger flow

1. `GovernanceService` creates a `TelemetryRecord`.
2. `TelemetryLogger.Log` uses `ChannelWriter.TryWrite`.
3. A single background worker serializes each event with `System.Text.Json`.
4. The worker appends one event per line to the daily JSONL file.

The queue holds 1,024 records. Full queues drop new records rather than blocking Dynamo. Shutdown performs a best-effort flush for 200 milliseconds. Logging failures are isolated from the host.

## View-extension flow

1. Dynamo loads `GovernanceViewExtension` from its manifest.
2. `Loaded` adds `Extensions > Dynamo Governance > Launch`.
3. The extension opens `GovernanceView` in Dynamo's extension sidebar.
4. Resource buttons open approved SharePoint destinations in the default browser.
5. Closing the sidebar does not unload the extension; the menu command reopens it.

The view extension does not emit telemetry.

## Identity profile

The session-start record currently stores plain-text `DOMAIN\\username` and Windows machine name. `identifiers_protected` remains `false`. Identity is no longer repeated on every event, but it remains linkable to all events carrying the same `session_id`.

## Schema evolution

Schema `2.0` is a breaking replacement for the previous universal envelope. Any future field collection must be documented in the schema/security review. Breaking record changes require a new major schema version.
