# Dynamo and Grasshopper Telemetry Schema Comparison

## Purpose and scope

This document compares the current Dynamo telemetry fields described in [TELEMETRY_SCHEMA.md](TELEMETRY_SCHEMA.md) with the actual Grasshopper telemetry in `Grasshopper20261016_001.log`. It identifies equivalent information, differences in structure, and additional information collected by each tool.

The Grasshopper side is based exclusively on the supplied log file, not on the proposed Grasshopper mappings or examples in the schema documentation. This is a comparison, not a recommendation to migrate either implementation to a shared schema.

The Dynamo documentation combines current implementation details with proposed changes. This comparison uses current Dynamo names where the documentation identifies them and lists proposed additions separately. Dynamo implementation behavior has not been independently verified against source code or a Dynamo log for this comparison.

## Evidence

| Source | Role |
| --- | --- |
| [TELEMETRY_SCHEMA.md](TELEMETRY_SCHEMA.md) | Documented Dynamo fields, current-name mappings, and implementation-status notes. |
| `Grasshopper20261016_001.log` | Actual Grasshopper text log with embedded JSON usage events. |

The Grasshopper log was supplied from the separate `10202026` workspace folder. It is not copied into this repository. Observations below describe that particular log, not all possible Grasshopper telemetry.

## Summary

| Area | Dynamo | Grasshopper |
| --- | --- | --- |
| Application context | Host name/version, Dynamo version, extension version, process and runtime details. | Telemetry plug-in version; no explicit host/tool version or runtime/process fields in the supplied usage payload. |
| Document metadata | Graph ID, name, full file path, file size, and node count. | Document ID, title, extension, total components, and unique component types. |
| Execution | Separate start/end events, execution ID, and evaluation/result flags. | `FirstSolve` and `Solve` records with solution sequence and explicit duration. |
| Dependencies | Graph-scoped package aggregates with version and governance metadata. | Detailed component statistics with assembly and plug-in attribution. |
| Identity | User/machine identity with source and collection metadata, associated with session start. | Raw and hashed user/machine identities repeated on usage events. |
| Event identity | No current per-event ID according to the documentation. | `EventId` on each supplied usage event. |
| Record structure | Common envelope with event-specific `data`. | Combined JSON usage payload following a text timestamp/severity/message prefix. |

## Equivalent information

These mappings indicate similar purposes, not guaranteed identical semantics or counting rules.

| Current Dynamo field | Grasshopper usage field | Difference |
| --- | --- | --- |
| `event` | `EventName` | Different property names and event vocabulary. |
| `session_id` | `SessionId` | Both correlate events to a session. |
| `occurred_utc` | `TimestampUtc` | Both record event time in UTC. The Grasshopper text prefix also includes a logging timestamp. |
| `user_id` | `UserName` | Dynamo documents `DOMAIN\username`; Grasshopper's payload contains a short username without a domain. |
| `machine_id` | `MachineName` | Both identify the machine. |
| `extension_version` | `PluginVersion` | Version of the telemetry integration, not the design tool. |
| `graph_id` | `DocumentId` | Graph/document correlation identifier. Identical ID-generation and persistence behavior is not established. |
| `name` | `DocumentTitle` | Graph/document name or title. |
| `node_count` | `TotalComponents` | Similar complexity totals; Grasshopper statistics include UI objects such as groups and scribbles. Counts are not automatically comparable. |
| `packages[].name` | `ComponentStats[].PluginName` or `AssemblyName` | Dynamo reports package aggregates; Grasshopper attributes component-statistic entries to providers. |
| `packages[].node_count` | Sum of `ComponentStats[].Count` by provider | A comparable provider total requires aggregation and an agreed provider key. It is not a direct field rename. |

## Additional information in Dynamo

The following fields are described for Dynamo but are absent from the supplied Grasshopper log.

| Area | Dynamo fields | Additional information |
| --- | --- | --- |
| Host application | `host_name`, `host_version` | Identifies the hosting product and version. The Grasshopper log has no explicit host name or version. |
| Design tool version | `dynamo_version` | Explicit design-tool version. Grasshopper `PluginVersion` is the integration version, not a separately captured Grasshopper tool version. |
| Process/runtime | `process_id`, `process_architecture`, `runtime_version` | Process identification, architecture, and .NET runtime context. |
| Identity provenance | `user_id_source`, `machine_id_source`, `machine_id_collected` | Identity-source and machine-collection metadata. A single collection boolean does not by itself distinguish every reason an identifier is unavailable. |
| Saved file | `file_path`, `file_size_bytes` | Full saved-file path and on-disk file size. Grasshopper title and extension do not provide either. |
| Execution correlation | `execution_id` | Dedicated identifier connecting an execution start and end. Grasshopper has a sequence number but no dedicated execution ID in this log. |
| Execution outcome | `evaluation_took_place`, `successful` | Explicit evaluation and completion-result flags. A Grasshopper solve event or non-null duration alone does not establish success. |
| Package governance | Package `version`, `description`, `license`, `repository_url`, `site_url` | Dependency version, description, license, and declared repository/site information. Values may be unavailable or null. |

## Additional information in Grasshopper

These fields appear in the supplied usage payload but have no current Dynamo counterpart described in the documentation.

| Grasshopper fields | Additional information |
| --- | --- |
| `EventId` | Per-event identifier, separate from session/document IDs. |
| `UserNameHash`, `MachineNameHash` | Hashed forms alongside raw identifiers. The log does not establish the hashing algorithm, keying, or protection strategy. |
| `DocumentExtension` | Explicit document extension without requiring a full file path. |
| `SolutionSequence` | Solution sequence number. Reset behavior is not established by the schema alone. |
| `SolutionDurationMs` | Explicit solution duration. Dynamo duration can instead be derived from paired execution timestamps. |
| `UniqueComponentTypes` | Reported distinct-component-type count; exact counting rules are not established. |
| `ComponentStats[].ComponentGuid` | Component-type identifier, not necessarily an individual placed-component instance ID. |
| `ComponentStats[].ComponentName`, `ComponentNickName` | Component names and labels, including customized labels. |
| `ComponentStats[].AssemblyName`, `PluginName`, `PluginGuid`, `Count` | Provider attribution and counts at component-statistic granularity rather than package-aggregate granularity. |
| `DroppedEventCount` | Reported dropped-event count. Its accounting window and reset behavior require implementation details. |

Component statistics can contain multiple entries for one component GUID with different nicknames. The number of `ComponentStats` entries should not be assumed to equal `UniqueComponentTypes`.

`$type` in the text log is serialization metadata, not an additional business telemetry metric.

## Event coverage

| Activity | Documented Dynamo event | Observed Grasshopper event |
| --- | --- | --- |
| Session begins | `session.started` | Not observed in the supplied log. |
| Session ends | `session.ended` | Not observed in the supplied log. |
| Graph/document opens | `graph.opened` | `Opened`. |
| Graph/document saves | `graph.saved` | Not observed in the supplied log. |
| Graph/document closes | `graph.closed` | Not observed in the supplied log. |
| Evaluation begins | `execution.started` | No separate start event observed. |
| Evaluation result | `execution.ended` | `FirstSolve` and `Solve` carry solution duration/sequence, but no explicit evaluation or success flags. |

Absence from this sample does not prove an event is unsupported by the Grasshopper plug-in. `FirstSolve` is an explicit Grasshopper event distinction with no separate current Dynamo event name documented here. Its precise trigger requires the plug-in implementation to confirm.

## Payload organization

Dynamo documents a common envelope containing `event`, `session_id`, `occurred_utc`, and event-specific `data`. Identity and application context belong to `session.started`. Graph lifecycle events carry snapshots; `execution.ended` nests a snapshot under `graph` alongside execution information.

The supplied Grasshopper usage payload combines event/session identifiers, identity, plug-in version, document metadata, solution fields, and component statistics. These fields repeat on usage events rather than appearing in separate session-context payloads.

Dynamo uses snake_case names. Grasshopper uses PascalCase names. The difference is not only casing: payload nesting, event boundaries, execution correlation, and dependency granularity also differ.

## Grasshopper log format

The supplied log contains three records: `Opened`, `FirstSolve`, and `Solve`. Each line has a text timestamp, severity (`[INF]`), and message prefix (`Grasshopper usage event`), followed by the JSON usage payload.

The prefix records local time with a `-07:00` offset; `TimestampUtc` inside the JSON records UTC event time. For example, `08:18:08 -07:00` and `15:18:08 UTC` describe the same instant. These are separate timestamp fields and may differ slightly in precision or capture time.

The JSON object directly contains fields such as `EventName`, `SessionId`, `DocumentId`, and `ComponentStats`; there is no nested `data` envelope. `$type` identifies serialized payload/component-statistic types.

## Current versus proposed Dynamo fields

The following must not be counted as current Dynamo advantages over Grasshopper:

| Proposed field/name | Current documented state |
| --- | --- |
| `schema_version` | Proposed; not currently emitted. |
| `event_id` | Proposed; not currently emitted. Grasshopper already supplies `EventId`. |
| `source` | Proposed; not currently emitted. |
| `tool_name` | Proposed neutral field; not currently emitted. |
| `tool_version` | Proposed neutral name for current `dynamo_version`. |
| `integration_version` | Proposed neutral name for current `extension_version`. |
| `document_id` | Proposed neutral name for current `graph_id`. |
| `component_count` | Proposed neutral name for current `node_count`. |
| `dependencies` | Proposed neutral name for current `packages`. |
| `document` in execution results | Proposed neutral name for current `graph`. |
| `document.opened`, `document.saved`, `document.closed` | Proposed neutral event names; current Dynamo names are `graph.*`. |
| `duration_ms` | Proposed explicit field; current duration is derivable from paired timestamps. |

## Privacy-relevant differences

- Both schemas contain raw user/machine identifiers and graph/document names.
- Dynamo additionally describes full file paths, which can reveal user, client, project, server, and folder information.
- Grasshopper additionally records component names and customized nicknames, which can disclose project terminology or design intent even without input/output values.
- Grasshopper hashes do not anonymize records that retain the corresponding raw `UserName` and `MachineName` identifiers in the same payload.
- Dynamo package descriptions and repository/site URLs can expose internal organizational information.

These are differences in collected information, not approval findings. Collection and retention decisions remain subject to the applicable review.

## Conclusion

Dynamo provides richer application/runtime context, saved-file metadata, execution correlation and outcome information, and package-governance metadata. Grasshopper provides finer component-level statistics, event IDs, identity hashes, explicit solution duration/sequence, and dropped-event reporting.

Grasshopper observations are limited to the three records in the supplied log. Missing fields and unobserved events should not be interpreted as proof of what the plug-in supports outside this sample.