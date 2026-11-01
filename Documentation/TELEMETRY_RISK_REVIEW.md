# Telemetry Risk Review Package
# Telemetry Risk Review Package

## Document status

| Item | Value |
| --- | --- |
| Status | In review preparation |
| Audience | Alex Greene, Security, Risk, Privacy, Design Automation |
| Scope | Dynamo implemented prototype + proposed shared Dynamo/Grasshopper telemetry model |
| Current implementation state | Dynamo telemetry local logging is implemented; Grasshopper telemetry and cloud collection are proposed only |
| Decision needed | Approve/adjust collection scope and controls before production and any centralized collection |

## Purpose

This package consolidates the telemetry proposal into a concise review brief so the Risk team can evaluate security, privacy, and governance implications.

The package is intended to support decisions on:

- Which fields are approved for collection.
- Which fields require minimization, pseudonymization, or removal.
- Whether centralized collection is approved.
- Which control requirements must be met before production deployment.

## Problem statement

The Design Automation team needs a governed way to understand tool adoption, document lifecycle activity, execution outcomes, and dependency usage across Dynamo and (proposed) Grasshopper workflows. Today, this visibility is limited and inconsistent.

Without a reviewed telemetry standard, the team cannot reliably support:

- Compatibility planning across host platforms and versions.
- Dependency governance for third-party packages and plug-ins.
- Operational insights into document usage and execution reliability.

Any telemetry approach must avoid collecting design content and unnecessary personal or project-sensitive information.

## Telemetry objectives

1. Measure Design Automation adoption by host, tool, and version.
2. Record document lifecycle events (`opened`, `saved`, `closed`).
3. Record execution lifecycle and completion outcomes.
4. Track document-scoped dependency usage for governance and upgrade planning.
5. Keep the schema explicit, strongly typed, and versioned.

## Explicit exclusions

The proposed dataset excludes:

- Node/component input-output values
- Geometry/model content
- Graph/definition file contents (`.dyn`, `.dyf`, `.gh`, `.ghx`)
- Python, DesignScript, C#, or other script contents
- Exception text and stack traces in business telemetry
- Keystrokes, mouse tracking, or general UI behavior surveillance
- Inventory of all installed packages/plugs-ins machine-wide

## Current prototype architecture and data flow

### Current implementation (Dynamo)

- Host raises telemetry-relevant events through the extension.
- `DynamoGovernance.Core` maps events to strongly typed records.
- Records are queued in a bounded in-memory channel (best effort).
- A background writer appends compact JSON records to daily local JSONL files.
- A local browser visualizer reads files without uploading them.

### Proposed extension of architecture (future)

- Grasshopper source integration.
- Optional approved secure transport to centralized storage.
- Central reporting with controlled, role-based access.

> Note: centralized collection is **not currently implemented or approved** in this repository.

### User workflow (illustrated)

1. **Application launch / extension load**
   - User starts a supported host (for example Revit, Civil 3D, Dynamo Sandbox, Rhino, or Rhino.Inside.Revit).
   - Telemetry extension initializes.

2. **Session start**
   - Event emitted: `session.started`.
   - Captures session envelope and startup context (for example host/tool/runtime and approved identity fields).

3. **Document open**
   - User opens a document/graph.
   - Event emitted: `document.opened` (or current Dynamo equivalent during transition).
   - Captures initial snapshot metadata (document ID, name/path policy fields, size, node/component counts, dependencies).

4. **Document save**
   - User saves the active document.
   - Event emitted: `document.saved`.
   - Captures updated snapshot metadata after save.

5. **Execution start**
   - User runs/evaluates the graph/definition.
   - Event emitted: `execution.started`.
   - Captures execution correlation ID and document correlation.

6. **Execution end**
   - Evaluation completes, is skipped, or fails.
   - Event emitted: `execution.ended`.
   - Captures completion flags (for example evaluation happened/success status) and final execution snapshot.

7. **Document close**
   - User closes the document or workspace is removed.
   - Event emitted: `document.closed`.
   - Captures final valid snapshot for lifecycle completeness.

8. **Session end**
   - User exits host or extension is unloaded.
   - Event emitted: `session.ended`.
   - Marks normal session completion.

9. **Record handling (current prototype)**
   - Each event is mapped to a schema-aligned record.
   - Record is queued in bounded in-memory channel.
   - Background writer appends compact JSON lines to local daily log files.

10. **Review and future transport**
   - Current: local review via log visualizer.
   - Future (if approved): minimized records may be securely transported to central storage and governed reporting.

## Proposed telemetry structure (aligned to review diagram)

### Event catalog

- `session.started`
- `session.ended`
- `document.opened`
- `document.saved`
- `document.closed`
- `execution.started`
- `execution.ended`

### Common envelope fields

- `schema_version`
- `event`
- `event_id`
- `session_id`
- `occurred_utc`
- `source`
- `source_version`
- `data`

### Payload sections (event-dependent)

#### Identity data

- `user_id`
- `machine_id`

#### Application/runtime data

- `host_name`
- `host_version`
- `extension_version`

#### Document snapshot

- `document_id`
- `name`
- `file_path`
- `file_size_bytes`
- `node_count`
- `unique_node_count`
- `dependencies`

#### Dependencies

- `name`
- `version`
- `description`
- `license`
- `repository_url`
- `site_url`

## Prototype hosting approach

### Current approved prototype

- Telemetry is generated in-process in the Dynamo extension.
- Logs are written locally to:
  - `%LOCALAPPDATA%\DynamoGovernance\Logs\telemetry_yyyy-MM-dd.jsonl`
- Local visualizer processes selected files on the user machine.
- No automatic cloud upload is performed.

### Future hosting option (for separate approval)

A cloud database can be considered only after approval of:

- Data minimization and sensitive-field policy
- Identity and access model
- Encryption in transit and at rest
- Storage region and sovereignty constraints
- Retention and deletion policy
- Audit logging and incident response controls

## Known risks

| Risk area | Risk summary | Impact | Mitigation proposal | Decision needed |
| --- | --- | --- | --- | --- |
| Direct identifiers | `user_id` and `machine_id` may directly identify individuals/devices | Privacy and compliance exposure | Replace with approved pseudonymous or managed identifiers, or remove | Are raw values allowed? |
| Confidential paths and names | `file_path` and `name` may expose client/project/user details | Confidentiality exposure | Use filename-only, tokenized path, or omit | Are full paths and names allowed? |
| Dependency metadata sensitivity | Internal package names/URLs may expose internal systems | Confidentiality exposure | Restrict dependency fields to minimum required | Which dependency fields are approved? |
| Session/activity linkage | Session correlation and precise timestamps can support behavioral profiling | Privacy and governance concern | Retention limits, restricted access, aggregation controls | What retention and access controls are required? |
| Local file exposure | Plain-text local JSON files can be copied/read by other local actors | Data leakage risk | Endpoint hardening, file ACL checks, optional local protection | Are endpoint controls required before deployment? |
| Future central collection | Upload/storage may expand threat and compliance scope | Security and legal risk | Secure architecture and formal approval gate | Is centralized collection approved at all? |

## Assumptions

- Telemetry is best effort and must never block or crash host applications.
- UTC timestamps are used.
- Correlation IDs are random and do not encode identity.
- Snapshot data reflects metadata only, not document contents.
- Unsaved documents may have null path/size fields.
- Grasshopper mappings are conceptual until implementation validation.

## Open questions for Alex Greene and Risk team

1. Are raw `user_id` and `machine_id` approved, or must they be pseudonymized/removed?
2. Is collecting full `file_path` approved?
3. Should `name` be collected as-is, tokenized, or omitted?
4. Which dependency fields are approved (`name/version` only or full metadata)?
5. Is user/device-level reporting required at all?
6. What retention period applies to local and future centralized data?
7. Who can access raw telemetry vs aggregated reports?
8. What encryption and key-management controls are required?
9. What data residency/geographic restrictions apply?
10. What deletion and subject-request process is required?
11. Is centralized collection approved, deferred, or rejected?
12. Is a shared Dynamo/Grasshopper schema approved as one contract?

## Review readiness checklist

- [x] Problem statement documented
- [x] Telemetry objectives defined
- [x] Proposed data model summarized
- [x] Architecture and data flow included
- [x] Prototype hosting approach described
- [x] Known risks, assumptions, and open questions documented
- [ ] Final package reviewed with Alex Greene and Risk team

## Decision log (to complete during review)

| Decision area | Proposed baseline | Final decision | Owner | Date |
| --- | --- | --- | --- | --- |
| User identity handling | Pseudonymize or remove raw user IDs | Pending | Risk | TBD |
| Device identity handling | Pseudonymize or replace machine ID | Pending | Risk | TBD |
| Document path treatment | Avoid full paths by default | Pending | Risk | TBD |
| Dependency metadata scope | Minimize to operationally necessary fields | Pending | Risk | TBD |
| Retention | Define and enforce retention limits | Pending | Risk/Platform | TBD |
| Access model | Least privilege + role-based access | Pending | Security | TBD |
| Centralized collection | Separate approval gate | Pending | Security/Risk | TBD |

## References

- `Documentation/TELEMETRY_SCHEMA.md`
- `Documentation/ARCHITECTURE.md`
- `README.md`
- `DynamoGovernance.Core/Models/TelemetryEnvelope.cs`
- `DynamoGovernance.Core/Models/TelemetryPayloads.cs`
- `DynamoGovernance.Core/Services/GovernanceService.cs`
- `DynamoGovernance.Core/Services/TelemetryLogger.cs`
- `DynamoGovernance.Extension/GovernanceTelemetryExtension.cs`
