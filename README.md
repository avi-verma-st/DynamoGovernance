# Dynamo Governance

Dynamo Governance is a Windows package for Dynamo 3.x. It combines local graph-usage telemetry with a WPF sidebar that links users to approved design-automation resources. A browser-based dashboard is included for reviewing the generated telemetry without uploading it.

## Current capabilities

### Telemetry extension

- Starts one session when Dynamo loads the extension and ends it during shutdown or disposal.
- Observes Dynamo home workspaces (`.dyn`) and deliberately ignores custom-node workspaces (`.dyf`).
- Records `graph.opened`, `graph.saved`, and `graph.closed` lifecycle events.
- Records `execution.started` and `execution.ended` for evaluations that actually take place. The completion event includes the Dynamo success result.
- Captures a graph snapshot containing its ID, name, full file path when saved, file size, saved/dirty state, node count, and graph-scoped package dependencies.
- Enriches package dependencies with available `pkg.json` metadata, including version, description, license, repository URL, and site URL.
- Writes one compact JSON object per line to a daily local JSONL file.
- Uses a bounded 1,024-record queue and non-blocking writes. Records are dropped rather than delaying Dynamo if the queue is full or writing fails.

### Resource sidebar

- Opens the `Dynamo Governance` sidebar when the view extension loads.
- Links to the Design Automation Hub, Dynamo Training, and Dynamo Development Resources.
- Adds `Extensions > Dynamo Governance > Launch` so a closed sidebar can be reopened.
- Opens resource links in the default browser. SharePoint access depends on the user's organizational permissions.

### Local log visualizer

`DynamoGovernance.LogVisualizer` is a static browser dashboard for the JSONL logs. It processes selected files locally and does not require the .NET solution to be running. See [Log Visualizer](DynamoGovernance.LogVisualizer/README.md) for instructions.

## Data and privacy

The `session.started` record contains the Windows account (`DOMAIN\\username`), machine name, host and Dynamo versions, extension version, process ID and architecture, and runtime version. Graph records can contain graph names, full file paths, file sizes, node counts, and package metadata. Node values, node-level identities, exception text, and stack traces are not recorded.

Identifiers are stored in plain text and `identifiers_protected` is currently `false`. Logs remain on the local machine unless another process or user moves them. Complete a privacy, access, retention, and distribution review before production deployment.

## Requirements

- Windows and a Dynamo 3.x host using modern .NET
- .NET 8 SDK for building
- Dynamo package path `C:\DynamoDev\packages` for the repository's current post-build deployment targets

The projects target `net8.0`/`net8.0-windows` and compile against Dynamo `3.0.3.7597`. The assemblies do not directly reference Revit, Civil 3D, or AutoCAD APIs. Dynamo 2.x and .NET Framework hosts are not supported by these binaries.

## Build and local deployment

1. Close Dynamo and every host process that has loaded the extension assemblies.
2. From the repository root, create the package and manifest directories and copy the files that are not deployed by the build:

   ```powershell
   New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\extra" -ItemType Directory -Force
   Copy-Item "DeploymentFiles\pkg.json" "C:\DynamoDev\packages\DynamoGovernance\pkg.json" -Force
   Copy-Item "DeploymentFiles\DynamoGovernance_ExtensionDefinition.xml" "C:\DynamoDev\packages\DynamoGovernance\extra\" -Force
   ```

3. Build the solution:

   ```powershell
   dotnet build DynamoGovernance.sln
   ```

   The projects copy the three assemblies and available PDB files to `C:\DynamoDev\packages\DynamoGovernance\bin`. The view-extension project also copies `DynamoGovernance_ViewExtensionDefinition.xml` to `extra`.

4. Ensure `C:\DynamoDev\packages` is listed under Dynamo's package search paths.
5. Restart the host. Confirm that the sidebar opens and that `Extensions > Dynamo Governance > Launch` is available.
6. Open or run a `.dyn` graph, then check `%LOCALAPPDATA%\DynamoGovernance\Logs` for a telemetry file.

See [Deployment Files](DeploymentFiles/README.md) for the complete package layout and manual deployment instructions.

## Telemetry files

Logs are written to:

```text
%LOCALAPPDATA%\DynamoGovernance\Logs\telemetry_yyyy-MM-dd.jsonl
```

Files are grouped by the UTC date at write time. Each non-empty line is an independent JSON record with `event`, `session_id`, and `occurred_utc`; event-specific records also include `data`. The current format does not include a `schema_version` property.

## Solution structure

- `DynamoGovernance.Core` - telemetry models, identity/application context, event creation, queueing, serialization, and local file output.
- `DynamoGovernance.Extension` - Dynamo `IExtension` integration for sessions, graph lifecycle, execution results, and package discovery.
- `DynamoGovernance.ViewExtension` - Dynamo `IViewExtension` integration, menu command, resource sidebar, and browser links.
- `DynamoGovernance.LogVisualizer` - static HTML, CSS, and JavaScript dashboard for local JSONL logs.
- `DeploymentFiles` - package metadata, extension manifests, package layout, and deployment instructions.
- `Documentation` - implementation architecture and telemetry schema details.

## Documentation

- [Architecture and telemetry schema](Documentation/ARCHITECTURE.md)
- [Deployment files and package layout](DeploymentFiles/README.md)
- [Log visualizer](DynamoGovernance.LogVisualizer/README.md)
- [Contributing](CONTRIBUTING.md)
