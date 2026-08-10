# DynamoGovernance

A telemetry and governance extension for Autodesk Dynamo that tracks usage patterns and session data without impacting performance.

## ?? Quick Start

```bash
# Build the solution
dotnet build

# Deploy manifest files (one-time setup)
Copy-Item "DeploymentFiles\*" "C:\DynamoDev\packages\DynamoGovernance\" -Recurse

# Restart Dynamo/Revit
```

## ?? What It Does

- **Session Tracking**: Logs when users start/stop Dynamo
- **Privacy-First**: SHA256-hashed user/machine IDs
- **Failsafe**: Never impacts Dynamo performance or stability
- **JSONL Format**: Easy-to-parse log files
- **Zero Configuration**: Works out of the box

## ?? Log Output

```
%LocalAppData%\DynamoGovernance\Logs\
??? telemetry_2026-01-10.jsonl
```

**Example log entry:**
```json
{
  "schema_version":"1.0",
  "event_id":"cd38c4a6-...",
  "session_id":"b69ee99d-...",
  "timestamp_utc":"2026-01-10T19:15:03Z",
  "user_id":"764e68c3...",
  "machine_id":"76d2008f...",
  "host_application":"DynamoCore",
  "dynamo_version":"3.3.0.6316",
  "outcome":"session_started"
}
```

## ?? Documentation

| Document | Description |
|----------|-------------|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Software architecture and design principles |
| **[FEATURES.md](FEATURES.md)** | Complete list of features and functionality |
| **[CHANGELOG.md](CHANGELOG_NEW.md)** | Version history and changes |
| **[DEPLOYMENT.md](DeploymentFiles/README.md)** | Build, deploy, and usage instructions |

## ? Key Features

? **Automatic session tracking**  
? **Privacy-focused** (hashed IDs)  
? **Failsafe design** (never crashes)  
? **Thread-safe** logging  
? **Daily log rotation**  
? **< 10ms overhead**  
? **Zero configuration**  

## ??? Safety Guarantees

- Never throws exceptions
- Never blocks UI thread
- 5-second timeout on all operations
- Auto-disable on errors
- Silent failure mode

## ?? Requirements

- .NET 8 Runtime
- Dynamo 3.x or later
- Windows 10/11

## ?? Configuration

Toggle ID hashing in `GovernanceTelemetryExtension.cs`:
```csharp
_governanceService = new GovernanceService(useHashedIds: true);  // Hashed (default)
_governanceService = new GovernanceService(useHashedIds: false); // Plain text
```

## ?? Current Version

**v1.0.1** - Idempotent session end (no duplicate logs)

## ?? License

MIT License - See [LICENSE.txt](LICENSE.txt)

## ?? Contributing

https://github.com/avi-verma-st/DynamoGovernance