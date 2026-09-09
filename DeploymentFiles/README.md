# Deployment Files

These files allow Dynamo to discover the telemetry extension and view extension.

## Files

- `pkg.json` — package metadata with Dynamo engine baseline `3.0.0`.
- `DynamoGovernance_ExtensionDefinition.xml` — loads `DynamoGovernance.Extension.GovernanceTelemetryExtension`.
- `DynamoGovernance_ViewExtensionDefinition.xml` — loads `DynamoGovernance.ViewExtension.GovernanceViewExtension` from `..\bin` because the manifest is placed under `extra`.

## Expected package structure

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

## Initial setup

```powershell
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\extra" -ItemType Directory -Force
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\bin" -ItemType Directory -Force
Copy-Item "DeploymentFiles\pkg.json" "C:\DynamoDev\packages\DynamoGovernance\" -Force
Copy-Item "DeploymentFiles\DynamoGovernance_ExtensionDefinition.xml" "C:\DynamoDev\packages\DynamoGovernance\extra\" -Force
dotnet build
```

The build copies the assemblies and view-extension manifest. Close Dynamo, Revit, Civil 3D, or Sandbox before rebuilding because loaded assemblies remain locked. Restart the host after deployment.
