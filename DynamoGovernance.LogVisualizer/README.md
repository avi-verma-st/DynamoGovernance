# Dynamo Governance Log Visualizer

## Purpose

This directory contains a static, read-only dashboard for Dynamo Governance telemetry logs. `index.html` loads the adjacent `styles.css` and `app.js` files, so keep all three files together. It can be opened directly in a browser without building the .NET solution, installing dependencies, or running a local web server.

All selected files are processed in the browser. The visualizer does not upload telemetry or make network requests.

## How to use it

1. Build and deploy the Dynamo Governance extension.
2. Start Dynamo and open, save, run, or close one or more `.dyn` graphs.
3. Open `DynamoGovernance.LogVisualizer/index.html` in Microsoft Edge or Google Chrome. Do not move it away from `styles.css` and `app.js`.
4. Select **Select log folder**.
5. In the folder picker, select:

   `%LOCALAPPDATA%\DynamoGovernance\Logs`

6. Select the folder to load every `.jsonl` file it contains.
7. Select a row under **Recent sessions** to open the detailed session dialog.
8. Use **Clear** to remove the loaded data from the current browser tab.

The browser requires the user to select the folder because a local HTML file cannot silently read `%LOCALAPPDATA%`.

## Design decisions

- The dashboard consists of static `index.html`, `styles.css`, and `app.js` files.
- There is no bundling or generated frontend output; the HTML references the CSS and JavaScript files directly.
- There is no ASP.NET project, backend, database, package manager, or frontend build step.
- Telemetry files are selected with an HTML directory input using `webkitdirectory`.
- The page reads all selected files whose names end in `.jsonl`.
- Each non-empty line is parsed independently so that one malformed record does not prevent other records from loading.
- User and machine identifiers are displayed exactly as logged. They are not masked or hashed.
- Text originating in logs is inserted with `textContent`, not `innerHTML`.
- Repository and site links are enabled only for `http` or `https` URLs.
- Existing logs remain usable when optional fields are absent.
- Package details only appear in telemetry generated after package logging was added to the extension.

## Log location and format

The extension normally writes files to:

`%LOCALAPPDATA%\DynamoGovernance\Logs\telemetry_yyyy-MM-dd.jsonl`

The format is JSON Lines, also called JSONL or NDJSON:

- One complete JSON object per line
- Multiple records per file
- Files grouped by UTC date
- UTF-8 text

Do not parse an entire file as one JSON array.

## Common telemetry envelope

Every record is expected to contain these properties:

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `event` | string | Yes | Event type identifier |
| `session_id` | GUID string | Yes | Correlates records belonging to one Dynamo session |
| `occurred_utc` | ISO 8601 timestamp | Yes | UTC event time |
| `data` | object | Event-dependent | Strongly typed event payload |

Conceptual example:

```json
{"event":"session.ended","session_id":"6ef65960-e8e6-4583-ab7a-dd27f618883f","occurred_utc":"2026-09-16T17:45:12.0000000+00:00"}
```

## Supported event schema

### `session.started`

Starts a session and provides identity and application context.

```json
{
  "event": "session.started",
  "session_id": "6ef65960-e8e6-4583-ab7a-dd27f618883f",
  "occurred_utc": "2026-09-16T17:30:00.0000000+00:00",
  "data": {
    "identity": {
      "user_id": "DOMAIN\\user",
      "user_id_source": "windows_account",
      "machine_id": "COMPUTER-NAME",
      "machine_id_source": "machine_name",
      "machine_id_collected": true
    },
    "application": {
      "host_name": "Revit",
      "host_version": "2026",
      "dynamo_version": "3.0.3",
      "extension_version": "2.0.0",
      "process_id": 12345,
      "process_architecture": "x64",
      "runtime_version": "8.0.0"
    }
  }
}
```

### `session.ended`

Ends a session. This event normally has no `data` payload.

### `graph.opened`, `graph.closed`, and `graph.saved`

These events use a graph payload:

```json
{
  "graph_id": "eca26838-edc9-419d-8302-38da98608f19",
  "name": "Example.dyn",
  "file_path": "C:\\Projects\\Example.dyn",
  "file_size_bytes": 10240,
  "node_count": 24,
  "packages": []
}
```

### `execution.started`

This event correlates the execution with its graph:

```json
{
  "execution_id": "39cab54d-0042-42d7-88cf-57917c16c6e4",
  "graph_id": "eca26838-edc9-419d-8302-38da98608f19"
}
```

### `execution.ended`

This event wraps the graph payload and includes execution results:

```json
{
  "graph": {
    "graph_id": "eca26838-edc9-419d-8302-38da98608f19",
    "name": "Example.dyn",
    "file_path": "C:\\Projects\\Example.dyn",
    "file_size_bytes": 10240,
    "node_count": 24,
    "packages": []
  },
  "evaluation_took_place": true,
  "successful": true
}
```

## Package schema

The `packages` collection belongs to a graph payload. It represents packages required by nodes in that graph, not every package installed in Dynamo.

```json
{
  "name": "Example Package",
  "version": "1.2.3",
  "node_count": 4,
  "description": "Example Dynamo nodes",
  "license": "MIT",
  "repository_url": "https://example.com/repository",
  "site_url": "https://example.com"
}
```

| Property | Type | Required | Source |
| --- | --- | --- | --- |
| `name` | string | Yes | Dynamo graph dependency metadata |
| `version` | string or null | No | Dynamo dependency metadata, with `pkg.json` fallback |
| `node_count` | integer | No | Number of graph nodes associated with the dependency |
| `description` | string or null | No | Matched package `pkg.json` |
| `license` | string or null | No | Matched package `pkg.json` |
| `repository_url` | string or null | No | Matched package `pkg.json` |
| `site_url` | string or null | No | Matched package `pkg.json` |

Package discovery is deliberately graph-scoped:

1. The extension asks the current `HomeWorkspaceModel` for node-library dependencies.
2. Only dependencies identified as packages are retained.
3. The dependency's node collection supplies `node_count`.
4. The code searches upward from that matched dependency path for `pkg.json`.
5. The manifest enriches the already-matched dependency with descriptive fields.

The implementation does not enumerate all installed packages. Built-in nodes, local definitions, and unrelated installed packages should not be reported as package usage.

Dynamo exposes the relevant workspace dependency members as non-public in the referenced API version. `GovernanceTelemetryExtension.GetPackages` therefore uses guarded reflection. Any API or manifest failure returns an empty package list rather than interrupting Dynamo.

## Dashboard behavior

The overview includes:

- Completed or started session count
- Unique users and machines
- Graph-open count
- Completed evaluation count and success percentage
- Skipped-evaluation count, kept separate from failures
- Unique graph package count
- Last telemetry activity
- Fourteen-day session and execution chart
- Most frequently opened graphs
- Most frequently observed graph packages
- Invalid-record, incomplete-session, and skipped-evaluation diagnostics

The session dialog includes:

- Full user and machine identifiers
- Host, Dynamo, and extension versions
- Session duration
- Graph, completed-evaluation, and skipped-evaluation totals
- Package name, version, dependent-node count, description, license, and project link
- Graph name, complete path, node count, and inferred saved status

A session is considered incomplete if it is missing either `session.started` or `session.ended`. An active Dynamo session will normally appear incomplete until Dynamo closes and writes `session.ended`.

An `execution.ended` record with `evaluation_took_place` set to `false` is shown as a skipped evaluation. It is excluded from execution totals and the success-rate denominator. Empty graph snapshots from older teardown logs do not replace the last meaningful graph name, path, node count, or package list; current logs preserve the last valid graph snapshot before closure.

## Compatibility and extension guidance

When changing telemetry:

1. Keep the common envelope fields stable.
2. Add new payload properties as optional where possible.
3. Keep JSON property names in snake case.
4. Update this README and the relevant visualizer source files together.
5. Ensure old records without new fields still render.
6. Never use `innerHTML` with telemetry values.
7. Count malformed lines but continue processing the file.
8. Preserve local-only processing unless the product requirements explicitly change.

The parser currently recognizes the event names listed above. Unknown event names are retained in a session's event timeline if their envelope is valid, but they do not affect existing aggregate metrics.

## Validation

After modifying `index.html`, `styles.css`, or `app.js`:

1. Run a JavaScript syntax check on `app.js` (for example, `node --check app.js` when Node.js is available).
2. Open the page in Edge or Chrome.
3. Confirm the empty dashboard renders before files are selected.
4. Load a folder containing valid and malformed JSONL lines.
5. Confirm metrics continue to render when malformed lines exist.
6. Confirm a session row opens the dialog by mouse and keyboard.
7. Confirm raw user and machine identifiers display correctly.
8. Confirm package information appears only for graph dependencies.
9. Confirm logs created before package enrichment still render.

After changing package telemetry, build the solution and restart Dynamo before generating test logs. Old log records are not rewritten.

## Related code

- `../DynamoGovernance.Core/Models/TelemetryEnvelope.cs` defines the telemetry envelope, identity, and application models.
- `../DynamoGovernance.Core/Models/TelemetryPayloads.cs` defines graph, execution, and package payload models.
- `../DynamoGovernance.Core/Models/TelemetryConstants.cs` defines event names.
- `../DynamoGovernance.Core/Services/TelemetryLogger.cs` writes JSONL files.
- `../DynamoGovernance.Core/Services/GovernanceService.cs` creates telemetry records.
- `../DynamoGovernance.Extension/GovernanceTelemetryExtension.cs` observes Dynamo and gathers graph/package data.
