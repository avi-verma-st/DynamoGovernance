# Dynamo Governance

Dynamo Governance is a .NET 8 package for Dynamo 3.x that captures local usage and graph-execution telemetry and provides a WPF resource-navigation extension. It provides a foundation for understanding Dynamo activity and reliability without requiring enterprise infrastructure or affecting graph execution.

## Current capabilities

- Captures extension startup, readiness, and shutdown events.
- Tracks graph execution starts, completions, outcomes, and duration.
- Records node additions and removals.
- Includes host, Dynamo, extension, process, and runtime information.
- Captures bounded warning, error, and exception details.
- Writes versioned JSONL records asynchronously to local daily log files.
- Isolates logging failures so they do not interrupt Dynamo workflows.
- Adds a `Dynamo Governance` sidebar with compact, descriptive resource navigation.
- Provides direct access to the Design Automation Hub, Dynamo Training, and Dynamo Development Resources.

## Solution structure

- `DynamoGovernance.Core` — telemetry schema, identity collection, event creation, and local JSONL logging.
- `DynamoGovernance.Extension` — Dynamo lifecycle and workspace event integration.
- `DynamoGovernance.ViewExtension` — Dynamo `IViewExtension` integration and the governance-resource sidebar.
- `DeploymentFiles` — Dynamo package metadata, extension manifest, and view-extension manifest.
- `Documentation` — architecture, features, and deployment guidance.

## Build and run

Close Dynamo or Revit, then build the solution in Visual Studio 2022 or run:

```powershell
dotnet build
```

The build deploys the telemetry and view-extension binaries to:

```text
C:\DynamoDev\packages\DynamoGovernance\bin
```

Copy `DeploymentFiles/pkg.json` and the telemetry manifest during initial setup. The view-extension project automatically copies `DynamoGovernance_ViewExtensionDefinition.xml` into the package's `extra` directory. Ensure `C:\DynamoDev\packages` is configured as a Dynamo package path, then restart Dynamo or its host application.

## View extension

During startup, Dynamo reads `DynamoGovernance_ViewExtensionDefinition.xml`, loads `DynamoGovernance.ViewExtension.dll`, and creates `GovernanceViewExtension`. Its `Loaded()` method adds `GovernanceView` to the extensions sidebar. Open `Extensions > Dynamo Governance` if the panel is not already visible. The panel opens the Design Automation Hub, Dynamo Training, and Dynamo Development Resources in the user's default browser. SharePoint uses the user's existing organizational authentication session.

View extensions are loaded once per host session. Close all Dynamo and Revit processes before replacing the DLL, then restart the host after deployment.

## Local telemetry

Logs are created automatically at:

%LocalAppData%\DynamoGovernance\Logs\telemetry_YYYY-MM-DD.jsonl


Each line contains one complete telemetry event. Logging uses a background queue to minimize impact on Dynamo, and failures are safely ignored rather than interrupting graph execution.

> **Privacy notice:** The current testing profile stores the Windows account and machine name in plain text. Review and protect identifiers before production deployment.

## Telemetry reference

See [Telemetry data sources and collection timing](Documentation/TELEMETRY_DATA_SOURCES.md) for details about where each logged value comes from and when it is captured.

## Documentation

- [Architecture](Documentation/ARCHITECTURE.md)
- [Features](Documentation/FEATURES.md)
- [Deployment and usage](Documentation/DEPLOYMENT.md)

## Status

The initial extension framework and local telemetry pipeline are implemented and working with Dynamo 3.x. Future work includes privacy protection, retention rules, broader event coverage, and enterprise telemetry integration.

