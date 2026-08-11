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

The extension currently emits lifecycle events. Graph and node payload APIs are present, while live Dynamo event subscriptions are the next implementation task.

## Safety behavior

Telemetry calls enqueue records without waiting for file I/O. When the bounded queue is full, records are dropped rather than blocking Dynamo. Shutdown performs a best-effort flush limited to 200 milliseconds.
