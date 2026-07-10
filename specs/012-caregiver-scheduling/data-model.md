# Phase 1 Data Model: Caregiver Scheduling (Weekly Staff Rota)

## StaffSchedule

Tenant-schema entity (schema-per-tenant, no explicit tenant FK — same structural pattern as
`Location`/`StaffProfile`/`Group`). Represents one planned shift for one staff member.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `StaffProfileId` | `Guid` | FK → `StaffProfile.Id`. |
| `LocationId` | `Guid` | FK → `Location.Id`. |
| `GroupId` | `Guid?` | FK → `Group.Id`, nullable (unassigned/floater — spec.md edge case). |
| `Date` | `DateOnly` | The calendar day of the shift (Belgium local, `Europe/Brussels` — spec.md Assumptions). |
| `StartTime` | `TimeOnly` | Shift start. |
| `EndTime` | `TimeOnly` | Shift end. Must be after `StartTime` (validated). |
| `IsAbsent` | `bool` | Default `false`. When `true`, excluded from `GetProjectedOnDutyQuery` (FR-006). |
| `AbsenceReason` | `AbsenceReason?` | Nullable; required when `IsAbsent = true` (validated), null otherwise. |
| `CreatedAt` | `DateTime` | UTC. |
| `UpdatedAt` | `DateTime` | UTC. |

**Constraints**:
- Unique index on `(StaffProfileId, Date, StartTime)` — matches BACKLOG.md's original
  `UNIQUE(staff_id, date, start_time)`, prevents exact-duplicate shift entries; overlap
  *between different* start/end ranges is a validator concern (FR-003), not a DB constraint,
  since overlap is a range comparison the unique index can't express.
- FK behavior matches the existing `RoomShift` precedent (feature 008a) exactly: EF Core's
  default cascade delete on `StaffProfileId`/`LocationId` (no explicit `OnDelete` override,
  same as every sibling entity in this codebase), `ClientSetNull`-equivalent on the optional
  `GroupId`. In practice this never triggers — `StaffProfile`/`Location` are always
  soft-deleted (`DeactivatedAt`), never hard-deleted (constitution's soft-delete convention),
  so `StaffSchedule` history is preserved for the same reason `RoomShift` history always is.
  Deactivation itself (not deletion) is what R5 handles — a deactivated staff member's future
  entries remain visible, just excluded from the projected on-duty count.

**Validation rules** (FluentValidation, MediatR pipeline behavior per constitution Principle
III):
- `EndTime` > `StartTime`.
- `Date` must not be in the past for create/update/delete (FR-004) — checked against
  `BelgianCalendarDay.Today()` (the existing helper used by feature 009/010, not a raw UTC
  `DateTime.Today`).
- `AbsenceReason` required iff `IsAbsent == true`.
- Overlap check (FR-003): no other `StaffSchedule` row for the same `StaffProfileId` on the
  same `Date` with an overlapping `[StartTime, EndTime)` range, regardless of `LocationId` —
  same-location and cross-location overlaps are both rejected. Enforced inside
  `IAdvisoryLockService.RunExclusiveAsync(staffProfileId, ...)` (research.md R2) around the
  read-then-write, not just a stateless FluentValidation rule (which cannot safely check
  "does an overlapping row already exist" under concurrency).

**Immutability**: Once `Date` has passed (relative to `BelgianCalendarDay.Today()`), the row
is read-only — `UpdateStaffScheduleCommand`/`DeleteStaffScheduleCommand`/`MarkAbsenceCommand`
all reject with `StaffScheduleFailure.PastDate` for such rows (FR-004).

## AbsenceReason (enum)

```csharp
public enum AbsenceReason
{
    Sick,
    Leave,
    Holiday,
}
```

New file: `backend/ChildCare.Domain/Enums/AbsenceReason.cs`. C# identifier names are English
only per constitution Principle IV — matches the existing `QualificationLevel`/`ClosureType`
enum precedent.

## Relationships

```text
StaffProfile (1) ──< (many) StaffSchedule >── (1) Location
                                    │
                                    └──< (0..1) Group
```

- A `StaffSchedule` always references exactly one `StaffProfile` and one `Location`.
- `GroupId` is optional (floater staff, per spec.md edge case — must not block the projected
  on-duty count).
- No new entity is needed for the rota-copy operation — `CopyWeekCommand` reads source-week
  `StaffSchedule` rows and writes new ones with shifted `Date` values; it produces no
  separate audit/log entity in this feature (not required by any FR).

## Derived / Query-only Concepts (not stored)

- **Projected on-duty count** (FR-007): computed at query time by
  `GetProjectedOnDutyQuery(LocationId, Date, TimeOnly)` — joins `StaffSchedule` →
  `StaffProfile`, filtering `IsAbsent == false`, `StaffProfile.DeactivatedAt == null`,
  `StaffProfile.QualificationLevel != QualificationLevel.StudentVolunteer`, and
  `StartTime <= time < EndTime`. Mirrors `GetBkrRatioQuery`'s qualification-exclusion rule
  for consistency of meaning, but reads `StaffSchedule` instead of `RoomShifts` and is never
  consulted by `GetBkrRatioQuery` itself (research.md R1).
- **Caregiver's own schedule** (FR-012): `GetMyScheduleQuery(StaffProfileId)` — all
  `StaffSchedule` rows for that staff member from today forward (no historical read needed
  for this feature's stated use case — "see where and when I'm working this week").

## State Transitions

`StaffSchedule` has no formal status field / state machine. Its only state-like behavior is:

```text
[Date in future] --edit/delete allowed--> [Date in future]
[Date in future] --date passes (time)--> [Date in past] (immutable from here on)
[Date in future, IsAbsent=false] --MarkAbsenceCommand--> [Date in future, IsAbsent=true, AbsenceReason set]
[Date in future, IsAbsent=true] --MarkAbsenceCommand(false)--> [Date in future, IsAbsent=false, AbsenceReason=null]
```

Once a row transitions to "past", no command may act on it further (FR-004) — including
un-marking an absence recorded before the date passed.
