# Data Model: Child Event Timeline

## ChildEvent (new entity, tenant schema)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. Client-generated on create (mobile) so offline-created events can be shown immediately and reconciled by id on sync, per feature 008's existing offline-write convention. |
| `ChildId` | `Guid` | FK to `Child`. |
| `LocationId` | `Guid` | The location the event was recorded at — sourced from the recording device's claims (same `LocationId` `IShiftAttributionService.ResolveRecordedByAsync` already needs), not from the child's current group assignment. This is the location FR-006's edit-window check compares against the *editing request's own device* `LocationId` claim (research.md R4) — there is no per-caregiver identity to check on a device-token-authenticated request. |
| `GroupId` | `Guid` | The group the event was recorded for — sourced the same way as `LocationId`, needed by `IShiftAttributionService`. |
| `EventType` | `string` (enum-backed) | One of: `sleep`, `temperature`, `medication`, `feeding_bottle`, `feeding_solid`, `diaper`, `mood`, `activity`, `note`, `weight`, `measurement`. Stored as a `ChildEventType` enum, mapped to its string name (matches existing enum-as-string convention, e.g. `ContractStatus`). |
| `OccurredAt` | `DateTime` (UTC) | When the event happened (not when it was saved). |
| `EndedAt` | `DateTime?` (UTC) | Sleep only; null = in progress. Ignored/must be null for all other event types (FR-002 validation). |
| `Payload` | `string`, column type `jsonb` | Serialized JSON matching `EventType`'s shape (see Payload Shapes below). Validated by `ChildEventPayloadValidator`, not by the database. |
| `VisibleToParent` | `bool` | Default `true`; `false` = staff-internal note, excluded from any parent-facing read (FR-005/FR-018). |
| `RecordedBy` | `Guid[]` (jsonb array) | 0, 1, or 2+ `StaffProfileId`s — resolved via `IShiftAttributionService` at write time (research.md R2). Empty array if nobody was checked in yet (spec edge case). |
| `AdministeredBy` | `Guid?` | Medication/temperature only. Set via the reused `confirm-administrator` flow (research.md R2); null if skipped or recorded offline, director-fillable later. |
| `RecordedByDeviceId` | `Guid` | The device (tablet) that submitted the event — for audit, mirrors `RoomShift.DevicePairingId`'s pattern of tracking which device acted. |
| `DeletedAt` | `DateTime?` | Soft-delete marker (FR-008); null = active. All read queries filter `DeletedAt IS NULL`. |
| `CreatedAt` | `DateTime` (UTC) | Set once. |
| `UpdatedAt` | `DateTime` (UTC) | Bumped on every edit. |

**Indexes**:
- `(ChildId, OccurredAt DESC)` — primary timeline access pattern (spec.md Key Constraints; also
  serves R6's cursor pagination).
- `(ChildId, EventType, OccurredAt DESC)` — per-type filtering (e.g. "this child's sleep history").

**Validation rules** (`ChildEventPayloadValidator`, FluentValidation, one rule set per
`EventType`):

| EventType | Payload fields | Required | Notes |
|---|---|---|---|
| `sleep` | `quality: "good"\|"okay"\|"restless"`, `durationMinutes: int?` | `quality` only on completion | `durationMinutes` computed server-side as `EndedAt - OccurredAt` when ended; stored in payload for query efficiency per spec.md. |
| `temperature` | `celsius: decimal` | yes | Range 30.0–42.0 (FR-002a). Triggers `ITemperatureAlertService` when `celsius > 38.0` (FR-010), once per event with no de-duplication (FR-011b). |
| `medication` | `name: "perdolan"\|"nurofen"\|"antibiotics"\|"other"`, `doseDescription: string`, `reason: string`, `nextDoseNotBefore: DateTime?` | `name`, `doseDescription`, `reason` | `nextDoseNotBefore` informational only (2026-07-08 clarification: no enforcement). |
| `feeding_bottle` | `ml: int` | yes | |
| `feeding_solid` | `description: string` | yes | |
| `diaper` | `type: "wet"\|"dirty"\|"both"`, `notes: string?` | `type` | |
| `mood` | `value: "great"\|"good"\|"okay"\|"difficult"` | yes | |
| `activity` | `description: string` | yes | |
| `note` | `text: string` | yes | |
| `weight` | `kg: decimal` | yes | Range 0–30 (FR-002a). Legally required field to exist (Belgian KDVs) — no recording-cadence enforcement in this feature. |
| `measurement` | `weightKg: decimal?` (0–30), `heightCm: decimal?` (30–120), `headCm: decimal?` (25–60) | at least one of the three non-null | "Any subset is valid" per spec.md means any *non-empty* subset (FR-002) — a payload with all three null/absent is rejected. |

A payload containing fields outside its `EventType`'s set, missing a required field, or with a
numeric field outside its FR-002a range, fails validation (FR-002/FR-002a) the same way every
other command in this codebase does: the global `ValidationBehavior` MediatR pipeline throws
`FluentValidation.ValidationException`, caught by `Program.cs`'s exception handler and returned
as `422 { errorKey: "errors.validation", fieldErrors: { <field>: <message> } }` — not a bespoke
`invalid_payload` shape invented for this feature.

A `POST` retried with an `id` that already exists returns the existing record rather than
creating a duplicate or erroring (FR-013a) — the create path is idempotent by client-generated id.

## Daily Summary (computed, not stored)

Produced by `GetDailySummaryQuery(childId, date)` — aggregates `ChildEvent` rows for that
child/calendar day (the `Europe/Brussels`-anchored day, FR-018a) where `DeletedAt IS NULL AND
VisibleToParent = true`. This filter applies uniformly to every field below, including the
latest-value fields (FR-018) — a staff-internal or deleted event is never eligible to become a
"latest" value either:

| Field | Derivation |
|---|---|
| `napsCount` | count of completed `sleep` events |
| `bottlesCount` | count of `feeding_bottle` events |
| `diaperChangesCount` | count of `diaper` events |
| `latestMood` | most recent (eligible) `mood` event's `value`, or null |
| `latestTemperatureCelsius` | most recent (eligible) `temperature` event's `celsius`, or null |
| `medicationAdministered` | `true` if any eligible `medication` event exists that day — independent of whether it has a confirmed `AdministeredBy` (FR-017) |

Returns all-zero/null fields (never an error) when no events exist for that child/date
(spec.md User Story 4, Acceptance Scenario 3).

## State / Lifecycle

```
[created] --(same-day edit, any caregiver at location OR director any time)--> [edited]
[created|edited] --(soft-delete, same authorization rule)--> [deleted] (excluded from all reads)

sleep only:
[in progress: EndedAt = null] --(end recorded)--> [completed: EndedAt set, durationMinutes computed]
```

No other event type has a lifecycle beyond create → optional edit → optional soft-delete.

## Relationships

- `ChildEvent.ChildId` → `Child.Id` (existing entity, feature 006).
- `ChildEvent.RecordedBy[]` / `AdministeredBy` → `StaffProfile.Id` (existing entity, feature 005),
  resolved via `IShiftAttributionService` / the reused `confirm-administrator` flow — no new FK
  constraint is added beyond the existing `RoomShift`/`StaffProfile` relationship those already
  enforce.
- `ChildEvent.RecordedByDeviceId` → `DevicePairing.Id` (existing entity, feature 008a).
- `ChildEvent.LocationId` → `Location.Id` (existing entity, feature 004) — the location FR-006's
  edit-window check authorizes against.
- `ChildEvent.GroupId` → `Group.Id` (existing entity, feature 006).

**Correction made during implementation**: `Contact` gains one new nullable column,
`PushToken` (`string?`), rather than adding zero columns as originally stated. Without it, the
temperature-alert recipient query (research.md R5) would have literally nothing to check —
`ChildContact.CanPickup = true` narrows *which* contacts are eligible, but nothing on `Contact`
itself represented "has a registered device" at all, making FR-010/FR-011 unimplementable and
untestable as written. No feature registers a value into this column yet (per the accepted
push-token-registration gap, spec.md Assumptions) — it exists now so the alert *query* is real
and testable (a test can seed a `PushToken` directly), the same way `Contract.TariefCode`
(feature 007) was added as a Phase-3 placeholder ahead of anything populating it.
