# Data Model: Day Reservations

## `DayReservation` (tenant schema table `day_reservations`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `Guid.NewGuid()` default. |
| `ChildId` | `Guid` | FK → `Children`. Not null. |
| `Type` | `DayReservationType` (enum: `Absence`, `Extra`, `Exchange`) | Not null. Stored as string via EF conversion, matching `ContractStatus`-style enum precedent. |
| `RequestedDate` | `DateOnly` | Not null. The date being requested (absence date, extra date, or exchange target date). |
| `ExchangeForDate` | `DateOnly?` | Only set when `Type = Exchange` — the contracted day being given up. Null for `Absence`/`Extra`. |
| `Reason` | `string?` | Parent's free-text reason. Optional. |
| `AbsenceJustified` | `bool?` | Only meaningful for `Type = Absence`, set by the director at approval time (FR-008). Null until decided; null forever for non-absence types. |
| `Status` | `DayReservationStatus` (enum: `Pending`, `Approved`, `Rejected`, `Cancelled`) | Not null, defaults to `Pending`. |
| `RequestedBy` | `Guid` | FK → tenant `Users` (the parent `TenantUser`). Not null. |
| `DecidedBy` | `Guid?` | FK → tenant `Users` (the director `TenantUser`). Null until decided. |
| `DecidedAt` | `DateTime?` | UTC. Null until decided. |
| `DirectorNotes` | `string?` | Optional note, shown to parent on rejection (FR-013). |
| `CreatedAt` | `DateTime` | UTC, set on insert. |
| `UpdatedAt` | `DateTime` | UTC, updated on every state transition. |

### Validation rules (enforced in command validators/handlers, constitution Principle II)

- `RequestedDate` for `Type = Absence`: MUST NOT be more than 1 day in the past (FR-002).
- `Type = Exchange`: `ExchangeForDate` MUST be set and MUST match an active `Contract`'s
  `ContractedDay.Weekday` for the child at the relevant location (FR-003); `RequestedDate` MUST
  NOT be a published closure day for that location (FR-004).
- `ChildId` MUST resolve to a child linked to the requesting parent's `Contact` record, checked
  via `ICurrentParentContactResolver` (FR-005).
- Status transitions: `Pending → Approved`, `Pending → Rejected`, `Pending → Cancelled` only.
  Any other source status is rejected (FR-015). Concurrent decisions on the same row are
  serialized by an optimistic-concurrency check (`UpdatedAt`-based) or a `WHERE Status =
  'pending'` guarded update — the second concurrent writer's affected-row-count is 0 and the
  handler returns a conflict result (FR-016).
- Approving `Type = Absence`: requires `AbsenceJustified` to be supplied by the director (FR-008);
  `LocationId` for the resulting `AttendanceRecord` is resolved from the child's active `Contract`
  whose `ContractedDay` matches `RequestedDate`'s weekday (research.md R7) — approval fails with
  `NoContractedLocation` if none matches; the underlying `AttendanceRecord` write also reuses
  `IClosureCalendarReader`'s guard — if the date has since become a closure day, the approval
  itself fails with a clear error (FR-011).

### Relationships

- `DayReservation.ChildId` → `Children.Id` (many day reservations per child over time).
- `DayReservation.RequestedBy` / `DecidedBy` → tenant `Users.Id` (no navigation property needed
  beyond the FK, consistent with `AttendanceRecord.RecordedBy`-style precedent of not always
  needing a full navigation).
- On `Absence` approval: writes one `AttendanceRecord` (feature 010, existing entity) — a
  produced side effect, not a stored relationship on `DayReservation` itself (no FK back to
  `AttendanceRecord`, mirroring how `MarkAbsentCommand` doesn't need one either).

### Indexes

- `(Status, CreatedAt DESC)` — the director queue's primary read pattern (FR-006: newest-first,
  filtered to pending).
- `(ChildId, CreatedAt DESC)` — the parent's own-request-history read pattern (FR-019).

## No changes to existing entities

`AttendanceRecord`, `Contract`, `KdvClosureDay` are read (and, for `AttendanceRecord`, written to
via the existing `MarkAbsentCommand` path) but not schema-modified by this feature.
