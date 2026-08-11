# Features and Functionalities

## Implemented

- Universal telemetry envelope schema `1.0` with independently versioned event payloads.
- Lifecycle events: `session.started`, `extension.ready`, and `session.ended`.
- Typed graph execution payloads for start and completion events.
- Live `graph.execution.started` and `graph.execution.completed` logging from Dynamo evaluation events.
- Live `node.added` and `node.removed` logging for existing, changed, and newly opened workspaces.
- Per-workspace execution numbering, timing, correlation, run mode, trigger, node count, and custom-node count.
- Graph result classification as succeeded, succeeded with warnings, failed, or skipped.
- Node-state warning and error summaries with bounded diagnostic records.
- Typed extension error payloads.
- Session-scoped event sequencing, correlation IDs, and causation IDs.
- UTC event timing and monotonic session/execution duration support.
- Host process, host version, Dynamo version, extension version, process, architecture, and runtime metadata.
- Plain Windows account and machine-name identity collection for testing.
- Non-blocking bounded telemetry queue with background JSON serialization and JSONL file writes.
- Daily files at `%LocalAppData%\DynamoGovernance\Logs\telemetry_YYYY-MM-DD.jsonl`.
- Idempotent session shutdown.
- Bounded graph diagnostics: 25 issues, 1,024-character issue messages, 2,048-character exception messages, and 8,192-character stack traces.
- Automatic deployment of core and extension binaries to `C:\DynamoDev\packages\DynamoGovernance\bin` after build.

## Runtime coverage

The extension currently covers lifecycle events, graph evaluation start/completion, and node additions/removals. Workspace save/open/close events, package usage, connector changes, and detailed node execution timing are not yet logged.
