# Deployment Files

Copy these files to `C:\DynamoDev\packages\DynamoGovernance\` for extension discovery.

## Files

### pkg.json
Package metadata file for Dynamo package manager.

### DynamoGovernance_ExtensionDefinition.xml
Extension manifest that tells Dynamo how to load `GovernanceTelemetryExtension`.

### DynamoGovernance_ViewExtensionDefinition.xml
View-extension manifest that tells Dynamo how to load `GovernanceViewExtension`. Because the manifest is deployed under `extra`, its assembly path is `..\bin\DynamoGovernance.ViewExtension.dll`.

## Directory Structure

After copying, your deployment should look like:
```
C:\DynamoDev\packages\DynamoGovernance\
??? pkg.json
??? extra\
?   ??? DynamoGovernance_ExtensionDefinition.xml
?   ??? DynamoGovernance_ViewExtensionDefinition.xml
??? bin\
    ??? DynamoGovernance.Extension.dll (auto-copied by build)
    ??? DynamoGovernance.ViewExtension.dll (auto-copied by build)
    ??? DynamoGovernance.Core.dll (auto-copied by build)
    ??? *.pdb files (auto-copied by build)
```

## Manual Setup

```powershell
# Create directory structure
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\extra" -ItemType Directory -Force
New-Item -Path "C:\DynamoDev\packages\DynamoGovernance\bin" -ItemType Directory -Force

# Copy manifest files
Copy-Item "DeploymentFiles\pkg.json" "C:\DynamoDev\packages\DynamoGovernance\"
Copy-Item "DeploymentFiles\DynamoGovernance_ExtensionDefinition.xml" "C:\DynamoDev\packages\DynamoGovernance\extra\"

# Build solution (DLLs and the view-extension manifest are deployed automatically)
dotnet build
```

Close Dynamo and Revit before building because loaded extension DLLs are locked until the host exits. Ensure `C:\DynamoDev\packages` is configured as a Dynamo package path, and restart the host after deployment so Dynamo discovers both manifests.
