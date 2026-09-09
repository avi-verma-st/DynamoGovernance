# Dynamo Governance

Dynamo Governance is a .NET 8 package for Dynamo 3.x. It contains a lightweight local telemetry extension and a WPF resource-navigation extension.

## Current capabilities

- Starts one telemetry session when Dynamo loads the extension.
- Writes Windows identity and application/runtime context once in `session.started`.
- Logs `extension.ready`, `session.ended`, and `extension.error` without additional data.
- Logs `node.added`, `node.removed`, and `graph.execution.started` with the current graph node count.
- Logs `graph.execution.completed` with the current node count and a `successful` boolean.
- Writes schema `2.0` JSONL records asynchronously to a local daily file.
- Uses a bounded, non-blocking queue so telemetry does not delay Dynamo workflows.
- Adds a `Dynamo Governance` WPF sidebar with links to the Design Automation Hub, Dynamo Training, and Dynamo Development Resources.
- Adds `Extensions > Dynamo Governance > Launch` so the sidebar can be reopened.

The telemetry does not collect graph names, graph paths, node identities, node types, package usage, node values, issue details, exception text, or stack traces.

## Solution structure

- `DynamoGovernance.Core` — minimal telemetry schema, session identity, event creation, and local JSONL logging.
- `DynamoGovernance.Extension` — Dynamo lifecycle, workspace, node-count, and execution-success integration.
- `DynamoGovernance.ViewExtension` — Dynamo `IViewExtension` integration and resource sidebar.
- `DeploymentFiles` — Dynamo package metadata and extension manifests.
- `Documentation` — architecture, schema, security, feature, and deployment guidance.

## Build and run

Close Dynamo and any host that embeds it, then run:

```powershell
dotnet build
```

Build output is copied to `C:\DynamoDev\packages\DynamoGovernance\bin`.

For initial setup, copy `DeploymentFiles/pkg.json` to the package root and `DeploymentFiles/DynamoGovernance_ExtensionDefinition.xml` to the package `extra` directory. Ensure `C:\DynamoDev\packages` is configured as a Dynamo package path and restart the host.

## Local telemetry

Logs are written to `%LocalAppData%\DynamoGovernance\Logs\telemetry_YYYY-MM-DD.jsonl`.

Each line is one schema `2.0` event. Identity appears only in `session.started`; the `session_id` connects later events to that session header.

> **Privacy notice:** The testing profile still stores the Windows account and machine name in plain text in the session-start record. Production deployment requires security/privacy review and a retention decision.

## Documentation

- [Architecture](Documentation/ARCHITECTURE.md)
- [Features](Documentation/FEATURES.md)
- [Deployment and usage](Documentation/DEPLOYMENT.md)
- [Telemetry data sources](Documentation/TELEMETRY_DATA_SOURCES.md)
- [Telemetry schema and security review](Documentation/TELEMETRY_SCHEMA_SECURITY_REVIEW.md)
- [API inventory](Documentation/API-INVENTORY.md)
- [Changelog](Documentation/CHANGELOG.md)

## Compatibility

The current projects target .NET 8 and compile against Dynamo `3.0.3.7597`. The extension does not reference Revit, Civil 3D, or AutoCAD APIs. Dynamo 2.x/.NET Framework hosts are not supported by the current binaries.
