# Data Model: Child Events — Custom Type & Growth Check Rename

No new entity and no schema (DDL) change — this feature extends `ChildEvent`
(`specs/009-child-events/data-model.md`) in place: the `event_type` column stays a `text`
column with the same `ChildEventTypeExtensions`-driven value converter; only the set of valid
values and one value's payload shape change.

## `ChildEventType` (updated enum)

| Value (wire string) | Change |
|---|---|
| `sleep`, `temperature`, `medication`, `feeding_bottle`, `feeding_solid`, `diaper`, `mood`, `activity`, `note`, `weight` | Unchanged. |
| `measurement` | **Removed.** No longer a valid value for any new create/update request (FR-008). Existing rows are backfilled to `growth_check` before this code ships (research.md R1/R2) — no row should hold this value once this feature is live. |
| `growth_check` | **New name for the renamed type.** Same payload shape, same validation rules `measurement` had — see below. |
| `custom` | **New type.** Payload: `{ label: string, text?: string }`. |

## Validation rules (additions/changes to `ChildEventPayloadValidator`'s table, feature 009's data-model.md)

| EventType | Payload fields | Required | Notes |
|---|---|---|---|
| `growth_check` | `weightKg: decimal?` (0–30), `heightCm: decimal?` (30–120), `headCm: decimal?` (25–60) | at least one of the three non-null | Byte-for-byte the same rule `measurement` had (FR-009) — rename only, no behavior change. |
| `custom` | `label: string` (1–100 chars), `text: string?` | `label` | `label` rejected if empty/whitespace-only or over 100 characters (FR-001/FR-002), the same validation-error pattern as every other required field in this validator. `text`, when present, has no length/shape constraint beyond the existing free-text handling `note.text`/`activity.description` already use (FR-003). No autocomplete/history lookup — plain free text (clarify session 2026-07-09). |

A `custom` payload containing fields outside `{ label, text }`, or missing `label`, fails
validation the same way every other event type's malformed payload does (feature 009's FR-002 /
`422 { errorKey: "errors.validation", fieldErrors }` pattern) — no bespoke error shape.

## Rendering rule (new, cross-cutting)

- Any timeline/daily-summary view rendering a `custom` event MUST show `payload.label` as the
  event's headline (in place of the generic type-name label every other type uses) and
  `payload.text` (if present) as secondary detail text (FR-004).
- `growth_check` MUST display under its new name (i18n key updated) everywhere `measurement` was
  previously shown — no other rendering change.

## Migration (data backfill, not DDL — research.md R1/R2)

One-time, per-tenant-schema raw SQL, run as an explicit pre-deploy operator step via the new
`backfill-growth-check` CLI subcommand:

```sql
UPDATE "<schema_name>".child_events SET "EventType" = 'growth_check' WHERE "EventType" = 'measurement';
```

No column added, dropped, or retyped. No index change (the existing `(ChildId, EventType,
OccurredAt DESC)` index from feature 009 covers `growth_check`/`custom` exactly as it did every
other type — a value change, not a shape change).

## Relationships

Unchanged from feature 009 — `custom` and `growth_check` are just two more values of the same
`ChildEvent.EventType` field; no new FK, no new referenced entity.
