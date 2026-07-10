# Phase 1 Data Model: Waiting List Management

## WaitingListEntry

Tenant-schema entity (schema-per-tenant, no explicit tenant FK — same structural pattern as
`Location`/`Contract`/`StaffSchedule`). Represents one family's request for a place at a
specific location.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `ChildFirstName` | `string` | Required. |
| `ChildLastName` | `string` | Required. |
| `DateOfBirth` | `DateOnly` | Required. |
| `ContactName` | `string` | Required. |
| `ContactEmail` | `string?` | Optional. Used for the `offered`-transition notification (FR-008) when present. |
| `ContactPhone` | `string?` | Optional. |
| `LocationId` | `Guid` | FK → `Location.Id`. Required (FR-020) — the occupancy view and priority queue are both meaningless without it. |
| `RequestedStartDate` | `DateOnly?` | Optional. |
| `Priority` | `int` | Default: appended after all existing entries for the same `LocationId` (FR-002). Lower = higher priority. Meaningful only while `Status == Waiting` (FR-005). |
| `Status` | `WaitingListStatus` | Default `Waiting`. See State Transitions below. |
| `Notes` | `string?` | Optional free text. |
| `ChildId` | `Guid?` | FK → `Child.Id`, nullable. Set only via `LinkChildToWaitingListEntryCommand` (FR-010/FR-011), never auto-matched. |
| `RegisteredAt` | `DateTime` | UTC, set on creation. |
| `UpdatedAt` | `DateTime?` | UTC, set on any update. |

**Constraints**:
- Index on `(LocationId, Status, Priority)` — serves the common list/sort/filter query (per
  location, filtered by status, ordered by priority).
- No unique constraint on `(ChildFirstName, ChildLastName, DateOfBirth)` — duplicates are
  expected and merely flagged in the UI (FR-004), never blocked at the data layer.
- `ChildId` FK uses EF Core's default behavior (no explicit `OnDelete` override) — in practice
  `Child` rows are soft-deleted (`DeactivatedAt`), never hard-deleted, so this never triggers
  in practice, consistent with every other optional-FK-to-a-soft-deleted-entity in this
  codebase.

**Validation rules** (FluentValidation, MediatR pipeline behavior per constitution Principle
III):
- `ChildFirstName`, `ChildLastName`, `ContactName` non-empty.
- `DateOfBirth` not in the future.
- `LocationId` must reference an existing, active `Location`.
- `ContactEmail`, when present, is a valid email address (`.Cascade(CascadeMode.Stop)` per
  feature 004's shipped-notes fix, to avoid the double-error `ToDictionary` crash previously
  found and fixed in that feature).
- Status transition allow-list enforced in `TransitionWaitingListStatusCommandHandler`, not a
  DB CHECK constraint (research.md R4).

**Duplicate flagging** (FR-004): computed at read time by `ListWaitingListEntriesQuery` — for
the requested `LocationId`, groups **all** entries regardless of status (not just the entries
matching the request's `status` filter) by `(ChildFirstName, ChildLastName, DateOfBirth)`, then
marks every entry in a group of size ≥ 2 with `isDuplicate: true` in the response — only the
entries actually matching the requested status filter are returned, but each one's
`isDuplicate` flag reflects the full location roster, so a duplicate is never missed just
because its twin is hidden behind the default `waiting`-only filter. No new column; this is
presentation-layer flagging over already-fetched data, not a persisted or indexed property.

## WaitingListStatus (enum)

```csharp
public enum WaitingListStatus
{
    Waiting,
    Offered,
    Enrolled,
    Withdrawn,
}
```

New file: `backend/ChildCare.Domain/Enums/WaitingListStatus.cs`. C# identifier names are
English only per constitution Principle IV — matches the existing `ContractStatus`/
`ClosureStatus` enum precedent.

## Relationships

```text
Location (1) ──< (many) WaitingListEntry >── (0..1) Child
```

- A `WaitingListEntry` always references exactly one `Location`.
- `ChildId` is optional and set only on/after enrollment (FR-010/FR-011/FR-012) — a
  `WaitingListEntry` never implies a `Child` record exists.
- No new entity for enrollment — `Contract` creation (007) happens independently, outside
  this feature's scope; this feature only manages the `WaitingListEntry.ChildId` link.

## Derived / Query-only Concepts (not stored)

- **Occupancy projection** (FR-013/FR-014/FR-015): computed at query time by
  `GetOccupancyQuery(LocationId, DateRange)` — for each date in range: if a `KdvClosureDay`
  (`Status == Published`) exists for that `LocationId`/date, return `Closed`; otherwise return
  `Location.MaxCapacity` minus the count of `Contract` rows where `Status == Active`,
  `StartDate <= date`, (`EndDate == null || EndDate >= date`), and `ContractedDays` contains an
  entry whose `Weekday == date.DayOfWeek`. Never reads `AttendanceRecord` (research.md R1).
- **Duplicate flag** (FR-004): see above — computed in `ListWaitingListEntriesQuery`, not
  stored.

## State Transitions

```text
[Waiting] --TransitionWaitingListStatusCommand(Offered)--> [Offered]   (sends email if ContactEmail present, FR-008)
[Waiting] --TransitionWaitingListStatusCommand(Withdrawn)--> [Withdrawn]   (terminal)
[Offered] --TransitionWaitingListStatusCommand(Enrolled)--> [Enrolled]   (terminal; child-link available, FR-010/FR-011)
[Offered] --TransitionWaitingListStatusCommand(Withdrawn)--> [Withdrawn]   (terminal)
[Offered] --TransitionWaitingListStatusCommand(Waiting)--> [Waiting]   (reverts; no email sent, FR-009)
```

Any transition not listed above (including any transition where the current status is
`Enrolled` or `Withdrawn`) is rejected with `WaitingListFailure.InvalidStatusTransition`
(FR-007). `Priority` reordering (FR-005) is only accepted while `Status == Waiting`.
