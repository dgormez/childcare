# Phase 1 Data Model: Staff App (Personal Rota & Leave)

## StaffSchedule (extended — feature 012's existing entity)

Tenant-schema entity, unchanged structural pattern (no explicit tenant FK). Adds six fields to
the existing row shape; replaces `IsAbsent` with a computed property (research.md R3).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. Unchanged. |
| `StaffProfileId` | `Guid` | FK → `StaffProfile.Id`. Unchanged. |
| `LocationId` | `Guid` | FK → `Location.Id`. Unchanged. |
| `GroupId` | `Guid?` | FK → `Group.Id`, nullable. Unchanged. |
| `Date` | `DateOnly` | Unchanged. |
| `StartTime` / `EndTime` | `TimeOnly` | Unchanged. |
| `Status` | `StaffScheduleStatus` | **New.** `Scheduled` (default) / `Confirmed` / `Absent` / `Covered`. |
| `AbsenceReason` | `AbsenceReason?` | Unchanged type; now populated only when `Status == Absent` (was: when `IsAbsent == true`). |
| `IsAbsent` | *(removed as a column)* | Becomes a computed `bool IsAbsent => Status == StaffScheduleStatus.Absent` property — not mapped, preserves the existing call sites (`GetProjectedOnDutyQuery` etc.) without a rename ripple. |
| `CoverStaffId` | `Guid?` | **New.** FK → `StaffProfile.Id`, nullable. Set on the *replacement's* new row when covering an absence (FR-007); null otherwise. |
| `Notes` | `string?` | **New.** Free text, nullable, max length 2000 (matches this codebase's established free-text cap — feature 012a's `Notes` precedent). |
| `CreatedBy` | `Guid?` | **New.** FK → `Users.Id` (the director who created/changed the row); nullable because system-generated cover rows still need an originating director acting on the sick report, so this is set from the acting director's own JWT identity, never null in practice but modeled nullable for consistency with other `CreatedBy`-style columns in this codebase. |
| `IsPublished` | `bool` | **New.** Default `false`. Gates visibility to `GetMyScheduleQuery` (FR-001). |
| `PublishedAt` | `DateTime?` | **New.** UTC, set when `IsPublished` flips `true`. |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Unchanged. |

**Constraints** (unchanged from feature 012 unless noted):
- Existing unique index `(StaffProfileId, Date, StartTime)` unchanged.
- Existing overlap validator (FR-014/018, `OverlapCheck.ExistsAsync` under
  `IAdvisoryLockService`) unchanged, extended to also apply when creating the replacement's
  `Covered`-status row (a cover assignment must still not double-book the replacement).
- `AbsenceReason` required iff `Status == Absent` (was: iff `IsAbsent == true`) — same rule,
  re-keyed to the new field.
- `CoverStaffId` may only be set when `Status == Covered`, and must reference a *different*
  `StaffProfileId` than the row it's set on (a staff member cannot cover their own absence).
- `PublishedAt` MUST be non-null iff `IsPublished == true` (paired fields, validated together).

**State transitions**:

```text
Scheduled ──(staff confirms, optional)──> Confirmed
Scheduled/Confirmed ──(sick report / director marks absent)──> Absent
Absent ──(director assigns cover)──> unchanged (Absent); a NEW row is created for the
                                       replacement with Status = Covered, CoverStaffId = null
                                       (the covering row doesn't reference itself — CoverStaffId
                                       lives on the ORIGINAL absent row, pointing at the
                                       replacement's StaffProfileId, per FR-007's "who covered")
```

Correction from spec.md's Key Entities wording: `CoverStaffId` is set on the **original**
(now-`Absent`) row to record who covered it, not on the replacement's new row — this matches
the BACKLOG prompt's own field comment (`cover_staff_id UUID ... -- who covered if absent`,
attached to the absent row's own record) and avoids the replacement's row needing a
self-referential-looking field.

**Immutability**: Past-date rule unchanged from feature 012 (FR-004's precedent) — a same-day
sick report is the one exception already implied by "today or tomorrow" (spec.md UX
Requirements), handled by `ReportSickCommand` operating on the current day explicitly rather
than going through the general past-date-rejecting update path.

## StaffScheduleStatus (enum, new)

```csharp
public enum StaffScheduleStatus
{
    Scheduled,
    Confirmed,
    Absent,
    Covered,
}
```

New file: `backend/ChildCare.Domain/Enums/StaffScheduleStatus.cs`. English-only identifiers per
constitution Principle IV.

## StaffLeaveRequest (new entity)

Tenant-schema entity, same structural pattern as `StaffSchedule`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `StaffProfileId` | `Guid` | FK → `StaffProfile.Id` — corrected from the BACKLOG sketch's `staff_members`/`users` reference (research.md R1's naming correction applies here too). |
| `Type` | `StaffLeaveRequestType` | `Sick` / `Annual` / `Other`. |
| `DateFrom` / `DateTo` | `DateOnly` | Inclusive range; `DateTo >= DateFrom` (validated). Must not be entirely in the past at submission time (mirrors `StaffSchedule`'s past-date convention). |
| `Notes` | `string?` | Free text, nullable, max length 2000. |
| `Status` | `StaffLeaveRequestStatus` | `Pending` (default) / `Approved` / `Rejected`. |
| `DecidedBy` | `Guid?` | FK → `Users.Id`, null while `Pending`. |
| `DecidedAt` | `DateTime?` | UTC, null while `Pending`. Paired with `DecidedBy` — both null or both set. |
| `CreatedAt` | `DateTime` | UTC. |

**Constraints**:
- FK `StaffProfileId` → `StaffProfile.Id`; default EF cascade behavior, matching `StaffSchedule`.
- `DecidedBy`/`DecidedAt` both null or both non-null (validated together).
- No unique index — a staff member may have multiple leave requests over time, including
  overlapping ranges across separate submissions (the director's approval decision is the
  actual conflict check, not a DB constraint — approving two overlapping requests is a director
  judgment call this feature doesn't need to block).

**State transitions**: `Pending → Approved` or `Pending → Rejected`, terminal — no
re-opening a decided request (a new request is submitted instead, matching how every other
approval-queue pattern in this codebase works, e.g. feature 013a's day-reservation decisions).

**On `Approved`** (`DecideLeaveRequestCommand`, FR-011): for every `StaffSchedule` row whose
`StaffProfileId` matches and whose `Date` falls within `[DateFrom, DateTo]` **and whose
`Status` is not already `Covered`** (FR-011a — a covered absence already has an arranged
replacement; overwriting it would silently discard `CoverStaffId`), set `Status = Absent`,
`AbsenceReason` per `Type` (research.md R3's mapping: `Sick → Sick`, `Annual → Leave`,
`Other → Leave`), leaving `Notes`/`CoverStaffId` alone. Dates in the range with no existing
`StaffSchedule` row, and dates whose row is already `Covered`, are both left untouched
(FR-011/FR-011a) — no row is created for a day the staff member wasn't scheduled at all, and no
existing cover arrangement is silently reverted.

## StaffLeaveRequestType / StaffLeaveRequestStatus (enums, new)

```csharp
public enum StaffLeaveRequestType
{
    Sick,
    Annual,
    Other,
}

public enum StaffLeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
}
```

New files: `backend/ChildCare.Domain/Enums/StaffLeaveRequestType.cs`,
`.../StaffLeaveRequestStatus.cs`.

## StaffProfile.ContractedDays (extended — feature 005's existing entity)

| Field | Type | Notes |
|---|---|---|
| `ContractedDays` | `List<DayOfWeek>` | **New.** Which weekdays this staff member normally works. Empty list = no contracted-days restriction (every day is schedulable — the safe default for existing staff profiles created before this feature, so a migration backfill of `[]` is non-breaking, per research.md R2). Stored as native `text[]` via an EF Core value converter + `ValueComparer`, mirroring `MealPreference.DietaryType`. |

**Validation**: No duplicate `DayOfWeek` values within one profile's list (validated, not a DB
constraint — same class of rule as `Contract.ContractedDay`'s "each weekday at most once").

## Relationships

```text
StaffProfile (1) ──< (many) StaffSchedule >── (1) Location
                │                   │
                │                   └──< (0..1) Group
                │                   └──> (0..1) StaffProfile   [CoverStaffId — who covered]
                │
                └──< (many) StaffLeaveRequest
```

- `StaffSchedule.CoverStaffId` is a self-referencing-via-`StaffProfile` optional FK, not a
  self-referencing FK on `StaffSchedule` itself — it names *which staff member* covered, not
  which schedule row.
- `StaffLeaveRequest` has no relationship to `StaffSchedule` directly; the link is behavioral
  (an approval writes into matching `StaffSchedule` rows by date range), not a foreign key,
  since a leave request can span dates with no existing schedule row at all.

## Migration notes

- One EF Core migration: adds `Status`, `CoverStaffId`, `Notes`, `CreatedBy`, `IsPublished`,
  `PublishedAt` to `staff_schedules`; drops the `IsAbsent` column after a data-backfill step
  (`UPDATE staff_schedules SET status = CASE WHEN is_absent THEN 'absent' ELSE 'scheduled' END`
  in the migration's `Up()`, before dropping the column — safe because no production tenant
  data exists yet, research.md R3); adds `ContractedDays text[]` to `staff_profiles` (backfilled
  to `{}`); creates `staff_leave_requests`.
- Per constitution Principle VI, the migration ships as reviewed EF Core migration code; it does
  **not** auto-apply to any existing tenant schema — a SQL script is generated and run manually
  per deployed tenant, same as every prior tenant-schema-altering feature.
- Extends the recurring `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests`
  revert-helper pattern (every migration-adding feature since 012a has needed this) — this
  migration both alters an existing table (`staff_schedules`) and adds a new one
  (`staff_leave_requests`); the revert helper needs both the new-table drop and the
  altered-column reversions.
