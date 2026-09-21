# Deployment Files

This directory contains the metadata and manifests Dynamo uses to discover both parts of the package.

## File reference

| File | Destination | Purpose |
| --- | --- | --- |
| `pkg.json` | Package root | Declares the `Dynamo Governance` package, Dynamo engine baseline `3.0.0`, and binary contents. |
| `DynamoGovernance_ExtensionDefinition.xml` | `extra` | Loads `DynamoGovernance.Extension.GovernanceTelemetryExtension` from `bin\DynamoGovernance.Extension.dll`. |
| `DynamoGovernance_ViewExtensionDefinition.xml` | `extra` | Loads `DynamoGovernance.ViewExtension.GovernanceViewExtension` from `..\bin\DynamoGovernance.ViewExtension.dll`. |

`pkg.json` currently reports package version `1.0.0`. The core and telemetry assemblies currently report assembly version `2.0.0`; coordinate these values before publishing a release.

## Required package layout

```text
C:\DynamoDev\packages\DynamoGovernance\
|-- pkg.json
|-- bin\
|   |-- DynamoGovernance.Core.dll
|   |-- DynamoGovernance.Extension.dll
|   |-- DynamoGovernance.ViewExtension.dll
|   `-- *.pdb                         # optional for debugging
`-- extra\
    |-- DynamoGovernance_ExtensionDefinition.xml
    `-- DynamoGovernance_ViewExtensionDefinition.xml
```

The package does not contain a Dynamo node library. Both manifests are required: the standard extension records telemetry, while the view extension provides the sidebar and menu command.

## Local development deployment

Run these commands from the repository root:

```powershell
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\extra" -ItemType Directory -Force
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\bin" -ItemType Directory -Force
Copy-Item "DeploymentFiles\pkg.json" "C:\DynamoDev\packages\DynamoGovernance\pkg.json" -Force
Copy-Item "DeploymentFiles\DynamoGovernance_ExtensionDefinition.xml" "C:\DynamoDev\packages\DynamoGovernance\extra\" -Force
dotnet build "DynamoGovernance.sln"
```

The build targets are currently hard-coded to `C:\DynamoDev\packages\DynamoGovernance`:

- `DynamoGovernance.Core` copies its DLL and PDB to `bin`.
- `DynamoGovernance.Extension` copies its DLL, PDB, and the core DLL/PDB to `bin`.
- `DynamoGovernance.ViewExtension` creates `bin` and `extra`, copies its DLL/PDB to `bin`, and copies its view-extension manifest to `extra`.
- The build does **not** copy `pkg.json` or `DynamoGovernance_ExtensionDefinition.xml`; copy them explicitly as shown above.

Close Dynamo Sandbox, Revit, Civil 3D, and any other Dynamo host before rebuilding because loaded assemblies can remain locked. After building, confirm `C:\DynamoDev\packages` is configured as a Dynamo package path and restart the host.

## Fully manual deployment

If the post-build targets cannot write to `C:\DynamoDev`, build the solution and copy files manually:

1. Copy `pkg.json` to the package root.
2. Copy both XML manifests to `extra`.
3. Copy the following release outputs to `bin`:
   - `DynamoGovernance.Core\bin\Release\net8.0\DynamoGovernance.Core.dll`
   - `DynamoGovernance.Extension\bin\Release\net8.0-windows\DynamoGovernance.Extension.dll`
   - `DynamoGovernance.ViewExtension\bin\Release\net8.0-windows\DynamoGovernance.ViewExtension.dll`
4. Add the parent package directory to Dynamo's package search paths.
5. Restart the host.

For a release build, use `dotnet build DynamoGovernance.sln --configuration Release` before copying the release outputs.

## Verification

After deployment:

1. Start a compatible Dynamo 3.x host.
2. Confirm the `Dynamo Governance` sidebar opens.
3. Close it and confirm `Extensions > Dynamo Governance > Launch` reopens it.
4. Open, save, or run a `.dyn` graph.
5. Confirm `%LOCALAPPDATA%\DynamoGovernance\Logs\telemetry_yyyy-MM-dd.jsonl` exists and contains JSONL records.

If the UI loads but no telemetry appears, verify the standard extension manifest is present. If telemetry appears but the UI does not, verify the view-extension manifest and its `..\bin` assembly path.
