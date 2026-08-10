# Deployment Files

Copy these files to `C:\DynamoDev\packages\DynamoGovernance\` for extension discovery.

## Files

### pkg.json
Package metadata file for Dynamo package manager.

### DynamoGovernance_ExtensionDefinition.xml
Extension manifest that tells Dynamo how to load the extension.

## Directory Structure

After copying, your deployment should look like:
```
C:\DynamoDev\packages\DynamoGovernance\
??? pkg.json
??? extra\
?   ??? DynamoGovernance_ExtensionDefinition.xml
??? bin\
    ??? DynamoGovernance.Extension.dll (auto-copied by build)
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

# Build solution (DLLs will auto-copy to bin folder)
dotnet build
```
