# Data Model: Staff HR Dossier & Time Registration

All new entities live in the tenant schema (`ChildCare.Domain/Entities`), consumed via
`ITenantDbContext` (Principle I — no explicit tenant FK column, same structural pattern as every
existing tenant table).

## StaffTimeEntryFunction (new enum)

`Kinderbegeleider`, `Logistiek`, `Verantwoordelijke` — wire strings match the backlog's snake/kebab
source values (`kinderbegeleider`, `logistiek`, `verantwoordelijke`) via a
`StaffTimeEntryFunctionExtensions.TryParseWireString`/`ToWireString` pair, mirroring every other
multi-word-safe enum wire mapping in this codebase (e.g. `ChildEventTypeExtensions`, feature 009).

## StaffTimeEntry (new entity, new table `staff_time_entries`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `StaffProfileId` | `Guid` | FK → `staff_profiles` |
| `LocationId` | `Guid` | FK → `locations` |
| `GroupId` | `Guid?` | FK → `groups`, nullable |
| `ClockedInAt` | `DateTime` | UTC, required |
| `ClockedOutAt` | `DateTime?` | UTC, null while the shift is open |
| `Function` | `StaffTimeEntryFunction` | required — the function this entry was worked under (FR-004) |
| `Notes` | `string?` | free text, director-only edit surface |
| `UnlockedAt` | `DateTime?` | non-null = an active director unlock override (R4); doubles as an audit timestamp |
| `UnlockedBy` | `Guid?` | director's `TenantUserId` — set together with `UnlockedAt` (FR-007a); cleared together on re-lock |
| `CreatedAt` | `DateTime` | UTC |
| `UpdatedAt` | `DateTime` | UTC |

**Derived state** (not persisted): `IsOpen = ClockedOutAt is null`; `IsLocked = UnlockedAt is
null && DateTime.UtcNow - ClockedInAt > TimeSpan.FromDays(7)` (R4 — fixed 7-day constant per
Clarifications, no per-tenant setting).

**Invariants**:
- At most one open (`ClockedOutAt is null`) entry per `StaffProfileId` at any time (FR-003) —
  enforced in `ClockInCommandHandler`, not a DB constraint (mirrors how single-open-record
  invariants are enforced elsewhere in this codebase, e.g. attendance check-in).
- `LocationId` MUST be one the acting staff member has a `StaffLocationEligibility` grant for
  (FR-001a) — enforced in `ClockInCommandHandler`, mirrors feature 012's schedule-write check.
- `Function` (on clock-in and on any later correction) MUST be one of the acting staff member's
  own `StaffProfile.TimeEntryFunctions` (FR-005a/FR-008) — enforced in `ClockInCommandHandler`
  and `UpdateStaffTimeEntryCommandHandler`, never trusted from the request alone.
- A locked entry (`IsLocked == true`) rejects any field mutation except through
  `UnlockTimeEntryCommand` (FR-006/FR-007).
- A correction that would overlap another entry for the same `StaffProfileId` triggers a warning,
  not a hard block (FR-009 — Edge Cases: "the correction UI warns the director before saving").

**Indexes**: `(LocationId, ClockedInAt)` — supports the subsidy report's period/location
aggregation (R6); `(StaffProfileId, ClockedOutAt)` — supports "find my currently open entry" on
every clock-in/out request.

## StaffDocument (new entity, new table `staff_documents`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `StaffProfileId` | `Guid` | FK → `staff_profiles` |
| `DocumentType` | `StaffDocumentType` | enum: `EmploymentContract`, `Amendment`, `Qualification`, `Training`, `Other` |
| `Title` | `string` | required |
| `ObjectPath` | `string` | GCS object path only, never a URL (R3 — same idiom as every other document/photo port) |
| `ValidFrom` | `DateOnly?` | nullable |
| `ValidUntil` | `DateOnly?` | nullable — set for contracts with an end date (FR-011); drives contract-expiry alerts (FR-014) when `DocumentType == EmploymentContract` |
| `CreatedBy` | `Guid` | director's `TenantUserId` who uploaded it (FR-012a) |
| `CreatedAt` | `DateTime` | UTC |
| `DeletedAt` | `DateTime?` | soft-delete (FR-012a audit trail) — mirrors this codebase's dominant `DeactivatedAt`-style soft-delete idiom (`StaffProfile`, `Location`) rather than a hard delete, so "who deleted what, when" survives the action; the underlying GCS object is still hard-deleted via `IStaffDocumentStorage.DeleteAsync` |
| `DeletedBy` | `Guid?` | director's `TenantUserId` who deleted it |

**Indexes**: `(DocumentType, ValidUntil)` — supports the contract-expiry query's `WHERE
DocumentType = EmploymentContract AND ValidUntil <= today + 60d` filter without a full scan (both
filtered to `DeletedAt IS NULL`).

## StaffProfile (extended)

New field: `TimeEntryFunctions: List<StaffTimeEntryFunction>` (default empty) — the function(s) a
director has configured this staff member to clock in under (FR-010). EF value-conversion to a
Postgres `text[]`, mirroring `ContractedDays: List<DayOfWeek>`'s existing conversion (feature 027,
same file). Empty list means the staff member cannot clock in yet (a real, expected pre-configuration
state — a director must set at least one function before a new staff member's first shift).

## Report shape (not persisted — computed on read, R5/R6)

`GetStaffHoursReportQuery(LocationId, DateOnly From, DateOnly To)` returns, per function:
`TotalStaffHours` (Σ closed `StaffTimeEntry` durations for that location/period/function),
`TotalChildHours` (Σ closed `AttendanceRecord` durations for that location/period — the same
figure for every function row, since child-hours aren't function-scoped), and `Ratio =
TotalChildHours / TotalStaffHours` (null/undefined when `TotalStaffHours == 0`, not a divide-by-
zero error — FR-016 Acceptance Scenario 2).

`ExportStaffHoursReportQuery` returns one CSV row per closed `StaffTimeEntry` in the period/
location (`StaffName`, `Date`, `Function`, `ClockedInAt`, `ClockedOutAt`, `DurationHours`) — the
same underlying query result the on-screen report uses, reused rather than re-derived (R6),
per FR-020's "raw hours... for a payroll system" framing.
