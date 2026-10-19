# Design Automation Telemetry Schema Proposal
# Design Automation Telemetry Schema Proposal

## Document status

| Item | Value |
| --- | --- |
| Status | Proposed for Security and Risk review |
| Scope | Dynamo and Grasshopper usage telemetry |
| Audience | Design Automation, Security, Risk, Privacy, and platform owners |
| Implementation state | Dynamo telemetry partially implemented; Grasshopper telemetry not yet implemented |
| Schema version | Proposed `1.0` |

## Purpose

This document defines the telemetry information proposed for Dynamo and Grasshopper. Its purpose is to obtain Security and Risk approval before the dataset is finalized, centrally collected, or used for reporting.

The proposed dataset is intended to answer the following questions:

- Which Design Automation tools and versions are being used?
- Which Dynamo graphs and Grasshopper definitions are opened, saved, closed, and evaluated?
- How often do evaluations occur, and whether they complete successfully?
- Which third-party packages or plug-ins are used by a graph or definition?
- How many components or nodes are present, without collecting their input values or generated geometry?

## Data-classification legend

Potentially user-identifiable or sensitive fields are highlighted throughout this document.

| Marker | Classification | Meaning |
| --- | --- | --- |
| **Direct identifier** | High review priority | Directly identifies a user or managed device. |
| **Indirect identifier** | Review required | Can identify or correlate a person, device, project, or document when combined with other data. |
| **Potentially confidential** | Review required | May expose project, client, organizational, or intellectual-property information. |
| Not identifiable | Lower privacy risk | Operational metadata that does not ordinarily identify a person or project. |

A field marked as lower risk may still become sensitive when combined with other fields.

## Proposed event catalog

| Event | Dynamo trigger | Grasshopper trigger | Purpose |
| --- | --- | --- | --- |
| `session.started` | Dynamo extension starts | Grasshopper telemetry component or plug-in starts | Establish session, identity, application, and runtime context. |
| `session.ended` | Extension shutdown or disposal | Plug-in shutdown or Rhino exit | Mark a normally completed session and support duration calculations. |
| `document.opened` | A `.dyn` home workspace is opened | A `.gh` or `.ghx` document is opened | Record document usage and an initial document snapshot. |
| `document.saved` | Dynamo workspace is saved | Grasshopper document is saved | Record the latest persisted document snapshot. |
| `document.closed` | Workspace removal or extension shutdown | Grasshopper document closes | Record the final valid document snapshot. |
| `execution.started` | Dynamo evaluation starts | Grasshopper solution starts | Correlate and time an evaluation. |
| `execution.ended` | Dynamo evaluation completes or is skipped | Grasshopper solution completes, aborts, or is skipped | Record whether evaluation occurred and whether it succeeded. |

### Current Dynamo event-name mapping

The current Dynamo implementation uses `graph.opened`, `graph.saved`, and `graph.closed`. Before a shared Dynamo/Grasshopper implementation is finalized, decide whether to retain those names or migrate both platforms to the neutral `document.*` names proposed above.

## Common record envelope

Every event is proposed to use the following envelope.

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `schema_version` | string | Yes | Not identifiable | Version of the serialized telemetry contract. Supports controlled schema evolution. This is proposed and is not present in current Dynamo logs. |
| `event` | string | Yes | Not identifiable | Event type from the approved event catalog. Determines the structure of `data`. |
| `event_id` | GUID string | Yes | Indirect identifier | Unique ID for deduplication and troubleshooting. Proposed; not present in current Dynamo logs. It must be randomly generated and must not encode identity. |
| `session_id` | GUID string | Yes | Indirect identifier | Random ID correlating events from one tool session. It must not be reused across sessions or derived from a user/device identifier. |
| `occurred_utc` | ISO 8601 timestamp | Yes | Indirect identifier | UTC time at which the observed event occurred. Supports ordering and duration calculations; precise activity time can contribute to user correlation. |
| `source` | enum string | Yes | Not identifiable | Telemetry source: `dynamo` or `grasshopper`. Proposed for the shared schema. |
| `data` | object | Event-dependent | Depends on payload | Strongly typed payload for the event. Omitted for events such as `session.ended` that do not require additional data. |

## Session-start payload

`session.started.data` contains `identity` and `application` objects.

### Identity fields

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `user_id` | string | Pending approval | **Direct identifier** | Current Windows account in `DOMAIN\username` format. Intended for adoption analysis and support correlation. Collection requires explicit Security, Privacy, and Risk approval. |
| `user_id_source` | string | If `user_id` is collected | Not identifiable | Identifies how `user_id` was obtained, initially `windows_account`. Supports interpretation and future identity-source changes. |
| `machine_id` | string | Pending approval | **Direct identifier** | Current Windows machine name. Intended for device-level adoption and troubleshooting. Collection requires explicit approval. |
| `machine_id_source` | string | If `machine_id` is collected | Not identifiable | Identifies how `machine_id` was obtained, initially `machine_name`. |
| `machine_id_collected` | boolean | Yes | Not identifiable | Indicates whether device identity collection was enabled and successful. Distinguishes unavailable identity from intentionally disabled collection. |

The schema does not propose collecting email address, employee number, Autodesk account ID, Rhino license ID, IP address, or geographic location.

### Application and runtime fields

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `host_name` | string | Yes | Not identifiable | Host product, such as `DynamoSandbox`, `Revit`, `Civil 3D`, `Rhino`, or `Rhino.Inside.Revit`. Supports host-specific adoption and compatibility analysis. |
| `host_version` | string or null | No | Not identifiable | Host product version. Supports compatibility and upgrade analysis. |
| `tool_name` | enum string | Yes | Not identifiable | Design Automation tool: `Dynamo` or `Grasshopper`. Proposed neutral replacement for a Dynamo-only field name. |
| `tool_version` | string | Yes | Not identifiable | Dynamo or Grasshopper version. Supports compatibility and adoption reporting. Current Dynamo logs call this `dynamo_version`. |
| `integration_version` | string | Yes | Not identifiable | Version of the telemetry extension or plug-in. Supports rollout and defect analysis. Current Dynamo logs call this `extension_version`. |
| `process_id` | integer | Yes | Indirect identifier | Operating-system process ID. Useful only for local diagnostics and distinguishing simultaneous sessions. It is short-lived but can correlate with local process logs. |
| `process_architecture` | enum string | Yes | Not identifiable | Process architecture, such as `x64` or `arm64`. Supports compatibility analysis. |
| `runtime_version` | string | Yes | Not identifiable | .NET runtime version used by the host. Supports compatibility and troubleshooting. |

## Document snapshot payload

A document snapshot is used by `document.opened`, `document.saved`, and `document.closed`, and is nested in `execution.ended`.

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `document_id` | GUID string | Yes | **Indirect identifier** | In-memory document/workspace identifier used to correlate lifecycle and execution events. It must not be derived from a path or user identity. Current Dynamo logs call this `graph_id`. |
| `name` | string | Yes | **Potentially confidential** | Dynamo graph or Grasshopper definition name. Supports reporting by document and troubleshooting. Names may contain client, project, or user information. |
| `file_path` | string or null | No | **Directly user-identifiable and potentially confidential** | Full local or network path when available. It can expose usernames, client names, project numbers, server names, and folder structures. This field requires explicit approval; a filename-only or path-token alternative should be considered. |
| `file_size_bytes` | integer or null | No | Indirect identifier | Current on-disk file size. Supports complexity and change analysis without reading file content. |
| `component_count` | integer | Yes | Not identifiable | Number of Dynamo nodes or Grasshopper components in the document. Current Dynamo logs call this `node_count`. |
| `dependencies` | array | Yes | Potentially confidential | Packages or plug-ins used by components in this document. Supports dependency governance and upgrade planning. Current Dynamo logs call this `packages`. |

No component IDs, component names/types, parameter values, code blocks, scripts, geometry, previews, screenshots, file contents, or model contents are proposed.

### Dynamo mapping

| Shared field | Current Dynamo source |
| --- | --- |
| `document_id` | Workspace GUID / current `graph_id` |
| `name` | Home workspace name |
| `file_path` | Workspace file path |
| `file_size_bytes` | On-disk `.dyn` file size |
| `component_count` | Workspace node count / current `node_count` |
| `dependencies` | Graph-scoped Dynamo package dependencies / current `packages` |

### Grasshopper mapping

| Shared field | Proposed Grasshopper source |
| --- | --- |
| `document_id` | Random telemetry ID associated with the active `GH_Document`, or a safe native document ID if available |
| `name` | Grasshopper document display name |
| `file_path` | `.gh` or `.ghx` path when saved |
| `file_size_bytes` | On-disk `.gh` or `.ghx` file size |
| `component_count` | Count of document objects/components, excluding UI-only objects if agreed |
| `dependencies` | Third-party Grasshopper assembly/plug-in dependencies actually used by document objects |

Grasshopper API feasibility and exact mapping must be validated before implementation.

## Dependency fields

Each item in `dependencies` contains the following proposed fields.

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | Potentially confidential | Package or plug-in name. Supports dependency inventories and governance. A proprietary internal package name may disclose organizational information. |
| `version` | string or null | No | Not identifiable | Installed dependency version. Supports compatibility and upgrade planning. |
| `component_count` | integer | Yes | Not identifiable | Number of document nodes/components associated with the dependency. Current Dynamo logs call this `node_count`. |
| `description` | string or null | No | Potentially confidential | Manifest description when available. Supports human-readable dependency review. Consider excluding it if name/version are sufficient. |
| `license` | string or null | No | Not identifiable | Declared dependency license. Supports license-governance review. |
| `repository_url` | URI string or null | No | Potentially confidential | Declared source repository. Internal URLs may disclose organizational systems or project names. |
| `site_url` | URI string or null | No | Potentially confidential | Declared package site. Internal URLs may disclose organizational systems. |

The dependency list must be document-scoped. It must not enumerate every package or plug-in installed on the machine.

## Execution payloads

### `execution.started.data`

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `execution_id` | GUID string | Yes | Indirect identifier | Random ID pairing one start event with one end event. Supports duration and completion analysis. |
| `document_id` | GUID string | Yes | Indirect identifier | Associates the execution with its document snapshot. Current Dynamo logs call this `graph_id`. |

### `execution.ended.data`

| Field | Type | Required | Classification | Description and purpose |
| --- | --- | --- | --- | --- |
| `execution_id` | GUID string | Yes | Indirect identifier | Matches the corresponding `execution.started` event. |
| `document` | object | Yes | Depends on nested fields | Latest valid document snapshot. Current Dynamo logs call this `graph`. |
| `evaluation_took_place` | boolean | Yes | Not identifiable | Distinguishes a real evaluation from a skipped, canceled, or no-op evaluation. |
| `successful` | boolean | Yes | Not identifiable | Indicates successful completion. It must be `false` when no evaluation took place unless the field is changed to nullable. |
| `duration_ms` | integer or null | No | Indirect identifier | Proposed explicit duration. It can alternatively be derived from start/end timestamps. Including it simplifies reporting but duplicates derivable information. |

Detailed exception messages, stack traces, node warnings, node errors, and failing input values are not proposed because they can expose paths, code, model data, or other confidential information.

## Example Dynamo payloads

### Session started

```json
{
  "schema_version": "1.0",
  "event": "session.started",
  "event_id": "00fcbb4b-206c-411a-810d-f10a32b69e9f",
  "session_id": "5fff4d50-7442-498b-9324-3c48dc893c2c",
  "occurred_utc": "2026-10-19T11:28:04.3546923Z",
  "source": "dynamo",
  "data": {
    "identity": {
      "user_id": "CORP\\user",
      "user_id_source": "windows_account",
      "machine_id": "WORKSTATION-001",
      "machine_id_source": "machine_name",
      "machine_id_collected": true
    },
    "application": {
      "host_name": "Revit",
      "host_version": "2026",
      "tool_name": "Dynamo",
      "tool_version": "3.3.0",
      "integration_version": "2.0.0",
      "process_id": 51480,
      "process_architecture": "x64",
      "runtime_version": "8.0.12"
    }
  }
}
```

### Execution ended

```json
{
  "schema_version": "1.0",
  "event": "execution.ended",
  "event_id": "6b5fa31f-358f-413b-98ca-ff60cc3747a0",
  "session_id": "5fff4d50-7442-498b-9324-3c48dc893c2c",
  "occurred_utc": "2026-10-19T11:28:17.2756063Z",
  "source": "dynamo",
  "data": {
    "execution_id": "c568bae9-c4e9-4b7b-ae5b-b20d65c41041",
    "document": {
      "document_id": "0fd195ec-f9ec-42a5-b228-4efca62a71b8",
      "name": "Example.dyn",
      "file_path": "C:\\Projects\\ClientProject\\Example.dyn",
      "file_size_bytes": 7685,
      "component_count": 3,
      "dependencies": [
        {
          "name": "Example Package",
          "version": "2026.1.0",
          "component_count": 1,
          "description": null,
          "license": null,
          "repository_url": null,
          "site_url": null
        }
      ]
    },
    "evaluation_took_place": true,
    "successful": true,
    "duration_ms": 118
  }
}
```

## Example Grasshopper payload

```json
{
  "schema_version": "1.0",
  "event": "document.opened",
  "event_id": "c5566255-e481-41d8-b3ff-4c324bb1838d",
  "session_id": "ef728247-021f-49c0-b889-99325521dc84",
  "occurred_utc": "2026-10-19T14:12:00Z",
  "source": "grasshopper",
  "data": {
    "document_id": "cb907668-994d-4816-b440-73397665fac3",
    "name": "FacadeStudy.gh",
    "file_path": "C:\\Projects\\ClientProject\\FacadeStudy.gh",
    "file_size_bytes": 124830,
    "component_count": 146,
    "dependencies": [
      {
        "name": "Example Grasshopper Plug-in",
        "version": "1.4.2",
        "component_count": 18,
        "description": null,
        "license": null,
        "repository_url": null,
        "site_url": null
      }
    ]
  }
}
```

## Explicit exclusions

The following information is not proposed for collection:

- Dynamo node or Grasshopper component input/output values
- Geometry, model elements, previews, screenshots, or thumbnails
- `.dyn`, `.dyf`, `.gh`, or `.ghx` file contents
- Python, DesignScript, C#, VB, or other script contents
- Node/component GUIDs or complete component-type inventories
- Revit, Civil 3D, AutoCAD, or Rhino model contents
- Autodesk account IDs, Rhino account/license IDs, email addresses, employee IDs, IP addresses, or location
- Keystrokes, mouse activity, or general UI interaction tracking
- Exception text, stack traces, or arbitrary diagnostic messages in the business telemetry stream
- A list of every package or plug-in installed on the machine

Any future proposal to collect one of these fields requires a separate schema and Security/Risk review.

## Data minimization recommendations

The following decisions are recommended before production implementation:

1. Prefer a pseudonymous approved user identifier over raw `DOMAIN\username` if user-level analysis is required.
2. Prefer an approved device identifier or one-way protected value over the raw machine name if device-level analysis is required.
3. Replace `file_path` with one of the following unless full paths are explicitly approved:
   - filename only;
   - a separately collected project identifier;
   - a one-way path token using an enterprise-managed strategy; or
   - no path field.
4. Keep dependency collection document-scoped and consider limiting it to `name`, `version`, and `component_count`.
5. Do not collect error text or stack traces in the central telemetry dataset.
6. Add `schema_version` before relying on the dataset as a versioned contract.
7. Define retention, access, encryption, and deletion controls before telemetry leaves the local machine.

## Assumptions

- Telemetry is best effort and must not block or crash Dynamo, Grasshopper, Rhino, or their host applications.
- Event timestamps are recorded in UTC.
- Session, event, execution, and document correlation IDs are random and contain no encoded identity.
- A document snapshot reflects metadata at the event time; it does not contain document contents.
- Unsaved documents may have no `file_path` or `file_size_bytes`.
- Close events reuse the last valid snapshot if the host clears document state during teardown.
- A skipped or canceled evaluation is not counted as a failed completed evaluation.
- Dynamo custom-node authoring documents (`.dyf`) are currently outside scope unless separately approved.
- Grasshopper implementation details are conceptual until the relevant Rhino/Grasshopper APIs and event ordering are validated.
- Current Dynamo logs are local JSONL files. Central transport, storage, and reporting are outside the current implementation and require approval.
- Legacy Dynamo field names may require a migration or compatibility layer if the neutral shared schema is adopted.

## Open questions for Security and Risk

1. Is raw `DOMAIN\username` approved, or must it be removed, pseudonymized, or protected before storage?
2. Is raw machine name approved, and is device-level reporting required?
3. Is full `file_path` permitted given that it can reveal usernames, clients, projects, server names, and folder structures?
4. Should document names be collected, tokenized, or omitted?
5. Are internal package/plug-in names and repository URLs considered confidential information?
6. Is `process_id` useful enough to retain, or should it remain only in local diagnostics?
7. What is the approved legal/business basis and user-notification requirement for collection?
8. Must users or business units be able to opt out?
9. What retention period applies to local files and any future central store?
10. Who can access raw telemetry, and which roles can access user- or project-level reports?
11. What encryption is required in transit and at rest if logs are centrally collected?
12. In which geographic region may centrally collected telemetry be stored and processed?
13. What deletion process applies to user, machine, project, or document telemetry?
14. Should Security approve one shared schema or separate Dynamo and Grasshopper schemas?
15. Should `successful` be nullable when `evaluation_took_place` is `false`?
16. Should `duration_ms` be stored explicitly or derived from paired timestamps?
17. Should full dependency metadata be retained, or only name, version, and usage count?
18. Are unsaved documents in scope, and should their names be recorded?
19. What constitutes a Grasshopper dependency: loaded plug-in, document-used assembly, or component provider?
20. Is centralized collection approved at all, or should the initial implementation remain local-only?

## Approval checklist

Before implementation or centralized collection, record decisions for the following:

- [ ] Approved event list
- [ ] Approved identity fields
- [ ] Approved document-name and path treatment
- [ ] Approved dependency metadata
- [ ] Approved Grasshopper field mappings
- [ ] Approved retention period
- [ ] Approved access model
- [ ] Approved encryption requirements
- [ ] Approved storage region
- [ ] Approved user notice and opt-out requirements
- [ ] Approved schema version and change-control owner
- [ ] Approved transport and central-storage architecture

## Relationship to the current Dynamo implementation

The existing Dynamo implementation already emits most session, graph, execution, application, identity, and dependency fields described here, using Dynamo-specific property names. The following shared-schema fields are proposals and are not currently emitted: `schema_version`, `event_id`, `source`, `tool_name`, `document_id`, `component_count`, `dependencies`, and `duration_ms`. Their current equivalents or derivation are documented above.

This document is a review proposal, not authorization to collect or centrally transmit the highlighted fields.