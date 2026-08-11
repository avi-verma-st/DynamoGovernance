# Changelog

## [2.0.0] - Unreleased - Universal Event Schema Milestone

### Major changes

- Replaced the experimental flat telemetry record with the first official universal envelope schema `1.0`.
- Added strongly typed lifecycle, graph execution, and extension error payloads.
- Added event type and payload versioning, sequence numbers, correlation IDs, and causation IDs.
- Replaced direct file writes with a bounded background JSONL queue so telemetry does not block Dynamo callbacks.
- Added host, process, runtime, result, timing, and telemetry-production metadata.
- Added bounded issue and exception diagnostics.
- Switched testing identity collection to plain Windows account and machine-name values with `testing_plaintext` metadata.
- Advanced core and extension assembly versions to `2.0.0`.
- Added live Dynamo subscriptions for workspace changes, workspace removal, graph evaluation start/completion, and node additions/removals.
- Added `node.added` and `node.removed` payloads with graph and node context.
- Added per-workspace execution numbering, monotonic duration measurement, start/completion correlation, result classification, and bounded node issue capture.
- Added deterministic subscription cleanup during workspace removal, shutdown, and disposal.

### Fixed

- Preserved idempotent session completion.
- Ensured `Dispose` can enqueue the final session event before stopping the writer.
- Prevented successful graph evaluations from producing `extension.error` records when Dynamo's empty `EvaluationCompletedEventArgs.Error` option throws from its getter.

## [1.0.1] - 2026-01-10

- Prevented duplicate `session_ended` records.

## [1.0.0] - 2026-01-10

- Added the initial experimental JSONL lifecycle logger.
