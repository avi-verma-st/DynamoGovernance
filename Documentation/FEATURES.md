# Features and Functionalities

## Implemented

- Universal telemetry envelope schema `1.0` with independently versioned event payloads.
- Lifecycle events: `session.started`, `extension.ready`, and `session.ended`.
- Typed graph execution payloads for start and completion events.
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

## Runtime events pending integration

The schema and APIs exist, but Dynamo workspace subscriptions for graph runs and node changes are the next implementation task.
