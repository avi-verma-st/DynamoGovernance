# Architecture

## Overview

DynamoGovernance is a .NET 8 Dynamo extension that writes versioned telemetry events to local JSONL files. The first official schema is `1.0`. Every event uses a stable envelope and a strongly typed payload selected by `event_type`.

## Projects

- `DynamoGovernance.Extension`: Dynamo `IExtension` lifecycle integration and host-process discovery.
- `DynamoGovernance.Core`: schema models, identity collection, event construction, diagnostic limits, and JSONL delivery.
- `DynamoGovernance.ViewExtension`: reserved for a future user interface.

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
