# Features and Functionalities

## Implemented

- Universal telemetry envelope schema `1.0` with independently versioned event payloads.
- Lifecycle events: `session.started`, `extension.ready`, and `session.ended`.
- Typed graph execution payloads for start and completion events.
- Live `graph.execution.started` and `graph.execution.completed` logging from Dynamo evaluation events.
- Live `node.added` and `node.removed` logging for existing, changed, and newly opened workspaces.
- Per-workspace execution numbering, timing, correlation, saved graph name, run mode, trigger, node count, and custom-node count.
- Bounded graph-level node-type summaries with runtime type, node kind, source assembly, assembly version, and usage count.
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
- Dynamo `IViewExtension` integration with a `Dynamo Governance` WPF sidebar.
- Scrollable governance-resource navigation within the Dynamo sidebar.
- Direct Design Automation Hub homepage access with a concise description of its purpose.
- A `Resources` section containing `Dynamo Training` followed by `Dynamo Development Resources`.
- Direct access to the canonical Dynamo Training learning-resources list.
- Direct access to the canonical Dynamo Development Resources document-library folder.
- Default-browser launching with a user-visible error message when a resource cannot be opened.
- Persistent `Extensions > Dynamo Governance > Launch` command for reopening a closed sidebar.
- Separate manifests for telemetry-extension and view-extension discovery.
- Automatic deployment of core, telemetry-extension, and view-extension binaries to `C:\DynamoDev\packages\DynamoGovernance\bin` after build.
- Automatic deployment of the view-extension manifest to `C:\DynamoDev\packages\DynamoGovernance\extra`.

## Runtime coverage

Telemetry currently covers lifecycle events, graph evaluation start/completion, and node additions/removals. The view extension provides governance-resource navigation but does not yet display telemetry or log UI interactions. Workspace save/open/close events, package usage, connector changes, and detailed node execution timing are not yet logged.
