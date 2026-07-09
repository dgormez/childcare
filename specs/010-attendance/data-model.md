# Data Model: Daily Attendance Registration

## AttendanceRecord (new entity, tenant schema)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. Client-generated on create (mobile), same offline-write convention as `ChildEvent`. |
| `ChildId` | `Guid` | FK to `Child`. |
| `LocationId` | `Guid` | The location this attendance day belongs to — sourced from the recording device's claims, same as `ChildEvent.LocationId`. |
| `Date` | `DateOnly` | The `Europe/Brussels`-anchored calendar day (reuses `BelgianCalendarDay`, feature 009) this record is for. |
| `Status` | `string` (enum-backed) | One of: `present`, `absent`, `closure`. Stored as an `AttendanceStatus` enum, mapped to its string name. |
| `CheckInAt` | `DateTime?` (UTC) | Set on check-in; null until then. |
| `CheckOutAt` | `DateTime?` (UTC) | Set on check-out; null while still present. |
| `PlannedDurationMinutes` | `int?` | Derived once at check-in from the child's active `Contract` at this `LocationId` for this weekday (research.md R6); `null` when no matching `ContractedDay` exists (an "extra day"). |
| `AbsenceJustified` | `bool?` | Null unless `Status = absent`. `true` = pre-approved (respijtdag), `false` = unjustified. |
| `AbsenceReason` | `string?` | Free text, only meaningful when `Status = absent`. |
| `RecordedBy` | `Guid[]` (`uuid[]` column) | 0, 1, or 2+ `StaffProfileId`s checked in at the location at the time of the action — resolved via `IShiftAttributionService` (research.md R1), same mapping pattern as `ChildEvent.RecordedBy`. For a director-initiated correction, this is the director's own `TenantUser.Id` wrapped in a single-element array, not a roster lookup. |
| `CreatedAt` | `DateTime` (UTC) | Set once. |
| `UpdatedAt` | `DateTime` (UTC) | Bumped on every correction. |

**Indexes**:
- Unique `(ChildId, LocationId, Date)` — enforces "at most one record per child per location per
  day" (spec.md FR-003); the source of the 409-on-duplicate conflict behavior (research.md R4).
- `(LocationId, Date, Status)` — the BKR present-count query's access pattern (research.md R2).

**Validation rules** (enforced on both initial writes and corrections, FR-011a):

- `Status = present` requires `CheckInAt` set.
- `Status = absent` requires `AbsenceJustified` set (non-null); `CheckInAt`/`CheckOutAt` must be
  null.
- A check-in against an existing `absent` record transitions it to `present` (FR-001a) rather
  than conflicting; a check-in against an existing `present` record is a 409 conflict (FR-012).
- A check-out requires an existing `present`-status record with `CheckInAt` set and `CheckOutAt`
  still null; otherwise not-found (FR-002a) — never silently overwrites an existing `CheckOutAt`.
- Only `Status = present` records with `CheckOutAt = null` count toward the BKR present count
  (FR-007d) — `absent`/`closure` records are always excluded.
- `Status = closure` is only ever set by a future feature 011 mechanism, never by a caregiver/
  director write in this feature (FR-015) — this feature's commands reject attempting to set it
  directly.
- A check-in/absence-mark request against a `child_id`/`location_id`/`date` combination that
  already has a record is rejected with `409` (FR-012) rather than silently overwritten, *unless*
  it's a director correction (which explicitly targets an existing record's `Id`, not a new
  create).

## BKR Ratio (computed, not stored)

Produced by `GetBkrRatioQuery(locationId)` — see research.md R2 for the full derivation. Response
shape:

| Field | Derivation |
|---|---|
| `presentCount` | Children with `Status = present` and `CheckOutAt = null` at `locationId`, today. |
| `qualifiedStaffCount` | Open `RoomShift`s at `locationId` whose `StaffProfile.QualificationLevel != StudentVolunteer`. |
| `isNapTime` | `true` when `nappingCount × 2 ≥ presentCount` (open `sleep`-type `ChildEvent` right now, at least half rounding up — spec.md FR-007c). |
| `threshold` | `8` (solo, non-nap), `14` (solo, nap), `9 × qualifiedStaffCount` (2+, non-nap), `14 × qualifiedStaffCount` (2+, nap). `0` staff → treated as breached regardless of `presentCount > 0` (FR-007b). A location with no `RoomShift` history at all and one with a history but nobody currently checked in are both `qualifiedStaffCount = 0` — indistinguishable and handled identically. |
| `status` | `green` (`presentCount < threshold`), `amber` (`presentCount == threshold`, at capacity but not breached), `red` (`presentCount > threshold`, or zero qualified staff with ≥1 present child) — spec.md FR-007e, a precise server-computed value, not a UI-layer judgment call. |

Absent/closure-status children are never counted in `presentCount` (FR-007d) — only `present`
records with `checkOutAt = null` qualify. The response is recomputed on every read and MUST be
re-fetched by the client at least every 15 seconds, in addition to an immediate local
recomputation after any check-in/check-out/absence action taken on the same device (FR-008a).

Never persisted — recomputed on every read, consistent with feature 009's daily summary being
computed at query time.

## State / Lifecycle

```
[no record] --(check-in)--> [present, CheckInAt set]
[present] --(check-out)--> [present, CheckOutAt set]  (status stays "present"; check-out is a field update, not a status change)
[no record] --(mark absent)--> [absent, AbsenceJustified set]
[any] --(director correction, any day | caregiver correction, same-day + own location)--> [corrected]

Not set by this feature (reserved for feature 011):
[no record] --(closure-calendar bulk job)--> [closure]  — blocks any manual check-in against it
```

## Relationships

- `AttendanceRecord.ChildId` → `Child.Id` (existing entity, feature 006).
- `AttendanceRecord.LocationId` → `Location.Id` (existing entity, feature 004).
- `AttendanceRecord.RecordedBy[]` → `StaffProfile.Id` (existing entity, feature 005) / director
  `TenantUser.Id` — resolved via `IShiftAttributionService`, no new FK constraint beyond what
  `RoomShift`/`StaffProfile` already enforce.
- `PlannedDurationMinutes` derivation reads `Contract.ContractedDays` (existing entity, feature
  007) for the child's single active contract at this `LocationId` — read-only, no FK added to
  `AttendanceRecord` itself (the contract may later change or end without needing to update
  already-recorded attendance history).
- BKR computation reads `RoomShift` (feature 008a) and `ChildEvent` (feature 009) — read-only,
  no FK.
