# Deployment and Usage

## Prerequisites

- Dynamo 3.x running on .NET 8.
- Visual Studio 2022 or the .NET 8 SDK.
- `C:\DynamoDev\packages` configured in Dynamo under `Settings > Manage Node and Package Paths`.
- `DeploymentFiles/pkg.json` copied to `C:\DynamoDev\packages\DynamoGovernance` during initial setup.

## Build

Close Dynamo and every host process that embeds it, including Revit, before building. Loaded extension assemblies remain locked for the lifetime of the host process and cannot be replaced in place.

Build the solution from Visual Studio or run:

```powershell
dotnet build
```

The post-build targets copy these assemblies and available debugging symbols to `C:\DynamoDev\packages\DynamoGovernance\bin`:

- `DynamoGovernance.Core.dll`
- `DynamoGovernance.Extension.dll`
- `DynamoGovernance.ViewExtension.dll`

The view-extension project also copies `DynamoGovernance_ViewExtensionDefinition.xml` to `C:\DynamoDev\packages\DynamoGovernance\extra`.

## Initial package setup

Run the following commands from the repository root:

```powershell
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\extra" -ItemType Directory -Force
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\bin" -ItemType Directory -Force

Copy-Item "DeploymentFiles\pkg.json" "C:\DynamoDev\packages\DynamoGovernance\" -Force
Copy-Item "DeploymentFiles\DynamoGovernance_ExtensionDefinition.xml" "C:\DynamoDev\packages\DynamoGovernance\extra\" -Force

dotnet build
```

After a successful build, the deployed package should contain:

```text
C:\DynamoDev\packages\DynamoGovernance\
??? pkg.json
??? bin\
?   ??? DynamoGovernance.Core.dll
?   ??? DynamoGovernance.Extension.dll
?   ??? DynamoGovernance.ViewExtension.dll
?   ??? *.pdb
??? extra\
    ??? DynamoGovernance_ExtensionDefinition.xml
    ??? DynamoGovernance_ViewExtensionDefinition.xml
```

## Extension discovery

The package contains two independently loaded extensions:

- `DynamoGovernance_ExtensionDefinition.xml` loads `GovernanceTelemetryExtension`, which implements Dynamo's `IExtension` lifecycle.
- `DynamoGovernance_ViewExtensionDefinition.xml` loads `GovernanceViewExtension`, which implements Dynamo's `IViewExtension` lifecycle.

The view-extension manifest is stored in `extra`, so its assembly path must be relative to that directory:

```xml
<AssemblyPath>..\bin\DynamoGovernance.ViewExtension.dll</AssemblyPath>
```

Using `bin\DynamoGovernance.ViewExtension.dll` would incorrectly make Dynamo search under `extra\bin`.

## Using the view extension

1. Close Dynamo or Revit before deploying a new build.
2. Build the solution and confirm the files above were deployed.
3. Start Revit and open Dynamo, or start Dynamo Sandbox.
4. Open `Extensions > Dynamo Governance` if the sidebar is not already visible.
5. Click `Test View Extension`.
6. Confirm that the `The Dynamo Governance view extension is working.` message appears.

View extensions are discovered only during startup. Rebuilding while Dynamo is open does not reload the extension; restart the host after each deployment.

## Troubleshooting

### The view extension is absent from the Extensions menu

Verify all of the following:

- `C:\DynamoDev\packages` is listed in Dynamo's package paths.
- `pkg.json` exists at the package root.
- `DynamoGovernance_ViewExtensionDefinition.xml` exists under `extra`.
- `DynamoGovernance.ViewExtension.dll` exists under `bin`.
- The manifest assembly path is `..\bin\DynamoGovernance.ViewExtension.dll`.
- Dynamo or Revit was fully restarted after deployment.

For Dynamo for Revit 3.3, inspect the latest log under:

```text
%AppData%\Dynamo\Dynamo Revit\3.3\Logs
```

Successful discovery produces a log entry similar to:

```text
Dynamo Governance (id: 83105D82-E9EF-48B6-9C51-F4027939C59A) view extension is added
```

An `extra\bin\DynamoGovernance.ViewExtension.dll` error indicates the manifest is missing the leading `..\` segment.

### The build cannot copy the DLL

An `MSB3021` or `MSB3027` error stating that Autodesk Revit is using `DynamoGovernance.ViewExtension.dll` means the assembly is locked. Close Dynamo and Revit completely, then rebuild. Do not terminate the host automatically if it has unsaved work.

## Logs

Records are written as JSONL to:

```text
%LocalAppData%\DynamoGovernance\Logs\telemetry_YYYY-MM-DD.jsonl
```

Each line is one complete schema `1.0` event. The current testing build stores the Windows account and machine name in plain text. Do not treat these test logs as anonymized data.

## Current runtime behavior

The telemetry extension emits `session.started`, `extension.ready`, `session.ended`, `graph.execution.started`, `graph.execution.completed`, `node.added`, `node.removed`, and `extension.error` events. Graph execution records are emitted for Dynamo home-workspace evaluations and include the run mode, inferred trigger, graph/node counts, duration, outcome, and bounded node issue details.

The view extension adds a basic WPF sidebar with a title and test button. It currently verifies UI discovery and interaction only; it does not display telemetry or emit an event for button clicks.

After deploying a new build, open Dynamo, add or remove a node, and run a graph. The current daily JSONL file should contain matching node and graph events with increasing `sequence_number` values.

## Safety behavior

Telemetry calls enqueue records without waiting for file I/O. When the bounded queue is full, records are dropped rather than blocking Dynamo. Shutdown performs a best-effort flush limited to 200 milliseconds.
