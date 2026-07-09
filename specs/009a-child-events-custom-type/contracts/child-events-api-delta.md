# Contract Delta: Child Events API (extends `specs/009-child-events/contracts/child-events-api.md`)

No route changes. `POST/PATCH /api/child-events` gain one new valid `eventType` (`custom`) and
one renamed value (`growth_check` replaces `measurement`) — same endpoints, same auth
(`DeviceOrDirector`/device-token-only per route, unchanged from feature 009).

## `POST /api/child-events` — `custom` example

```json
{
  "childId": "guid",
  "eventType": "custom",
  "occurredAt": "2026-07-09T10:00:00Z",
  "payload": { "label": "Sunscreen applied", "text": "Reapplied after outdoor play" },
  "visibleToParent": true
}
```

- `422 { errorKey: "errors.validation", fieldErrors: { "label": "errors.child_events.field_required" } }`
  — `label` missing, empty/whitespace, or over 100 characters (data-model.md).

## `POST /api/child-events` — `growth_check` (renamed from `measurement`)

```json
{
  "childId": "guid",
  "eventType": "growth_check",
  "occurredAt": "2026-07-09T10:00:00Z",
  "payload": { "weightKg": 9.2, "heightCm": 72.5 },
  "visibleToParent": true
}
```

Identical payload shape and validation to the old `measurement` (feature 009's data-model.md) —
only `eventType`'s wire string changed.

- `422 { errorKey: "errors.validation", fieldErrors: { "eventType": "errors.child_events.invalid_event_type" } }`
  — a request submitting the literal `eventType: "measurement"` after this feature ships (FR-008,
  no dual-write compatibility window; see research.md R2 for why this is safe only once the
  backfill has already run against every tenant).

## New CLI: `backfill-growth-check`

Mirrors `migrate-tenants`'s existing CLI contract shape and invocation pattern
(`dotnet run --project backend/ChildCare.Api -- backfill-growth-check`), checked before the web
host builds (research.md R1). Loops every `Ready` tenant, runs the schema-qualified `UPDATE`
(data-model.md's Migration section) via `ExecuteSqlRawAsync`, and prints one line per tenant
(`{slug}: N row(s) updated` / `{slug}: failed — {message}`) plus a final summary line. Exit code
`0` if every tenant succeeded, `1` if any failed — same convention as `migrate-tenants`.

**Operational requirement (research.md R2)**: this command MUST be run against every tenant
schema before deploying the application build that removes `"measurement"` recognition from
`ChildEventTypeExtensions` — documented as an explicit pre-deploy step, not run automatically at
app startup (constitution Principle VI: no auto-applying data changes against populated tenant
schemas).
