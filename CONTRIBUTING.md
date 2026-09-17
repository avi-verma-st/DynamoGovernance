# Contributing

## Guidelines

- Keep changes focused and consistent with the existing solution structure.
- Ensure the solution builds successfully and relevant tests pass before submitting changes.

## Telemetry Model Design

- Model telemetry classes so their object hierarchy mirrors the structure of a complete, serialized single log record.
- Prefer strongly typed nested models over loosely typed payload properties such as `object` when the set of log structures is known.
- Keep JSON property names and nullability aligned with the actual serialized telemetry schema.