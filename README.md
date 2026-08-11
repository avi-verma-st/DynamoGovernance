# Dynamo Governance

Dynamo Governance is a .NET 8 extension for Dynamo 3.x that captures local usage and graph-execution telemetry. It provides a foundation for understanding Dynamo activity and reliability without requiring enterprise infrastructure or affecting graph execution.

## Current capabilities

- Captures extension startup, readiness, and shutdown events.
- Tracks graph execution starts, completions, outcomes, and duration.
- Records node additions and removals.
- Includes host, Dynamo, extension, process, and runtime information.
- Captures bounded warning, error, and exception details.
- Writes versioned JSONL records asynchronously to local daily log files.
- Isolates logging failures so they do not interrupt Dynamo workflows.

## Solution structure

- `DynamoGovernance.Core` — telemetry schema, identity collection, event creation, and local JSONL logging.
- `DynamoGovernance.Extension` — Dynamo lifecycle and workspace event integration.
- `DynamoGovernance.ViewExtension` — reserved for future user-interface features.
- `DeploymentFiles` — Dynamo package metadata and extension manifest.
- `Documentation` — architecture, features, and deployment guidance.

## Build and run

Build the solution in Visual Studio 2022 or run:

dotnet build

The build deploys the extension binaries to:

C:\DynamoDev\packages\DynamoGovernance\bin


Copy the package and manifest files from `DeploymentFiles` as described in `Documentation/DEPLOYMENT.md`, then restart Dynamo or its host application.

## Local telemetry

Logs are created automatically at:

%LocalAppData%\DynamoGovernance\Logs\telemetry_YYYY-MM-DD.jsonl


Each line contains one complete telemetry event. Logging uses a background queue to minimize impact on Dynamo, and failures are safely ignored rather than interrupting graph execution.

> **Privacy notice:** The current testing profile stores the Windows account and machine name in plain text. Review and protect identifiers before production deployment.

## Documentation

- [Architecture](Documentation/ARCHITECTURE.md)
- [Features](Documentation/FEATURES.md)
- [Deployment and usage](Documentation/DEPLOYMENT.md)

## Status

The initial extension framework and local telemetry pipeline are implemented and working with Dynamo 3.x. Future work includes privacy protection, retention rules, broader event coverage, and enterprise telemetry integration.
