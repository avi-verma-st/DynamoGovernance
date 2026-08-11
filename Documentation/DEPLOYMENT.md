# Deployment and Usage

## Build

Build the solution from Visual Studio or run:

```powershell
dotnet build
```

The post-build targets copy `DynamoGovernance.Core.dll`, `DynamoGovernance.Extension.dll`, and their debugging symbols to:

```text
C:\DynamoDev\packages\DynamoGovernance\bin
```

Close Dynamo or Revit before replacing loaded binaries, then restart the host.

## Logs

Records are written as JSONL to:

```text
%LocalAppData%\DynamoGovernance\Logs\telemetry_YYYY-MM-DD.jsonl
```

Each line is one complete schema `1.0` event. The current testing build stores the Windows account and machine name in plain text. Do not treat these test logs as anonymized data.

## Current runtime behavior

The extension emits `session.started`, `extension.ready`, `session.ended`, `graph.execution.started`, `graph.execution.completed`, `node.added`, `node.removed`, and `extension.error` events. Graph execution records are emitted for Dynamo home-workspace evaluations and include the run mode, inferred trigger, graph/node counts, duration, outcome, and bounded node issue details.

After deploying a new build, open Dynamo, add or remove a node, and run a graph. The current daily JSONL file should contain matching node and graph events with increasing `sequence_number` values.

## Safety behavior

Telemetry calls enqueue records without waiting for file I/O. When the bounded queue is full, records are dropped rather than blocking Dynamo. Shutdown performs a best-effort flush limited to 200 milliseconds.
