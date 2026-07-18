# Data Model: Management Reporting

This feature is read-only over existing tenant tables, with exactly one schema change.

## Schema change

### `Group` (extended)

| Field | Type | Notes |
|---|---|---|
| `Capacity` | `int?` | **New.** Number of children this group is designed to hold. Null for existing groups until a director sets one — occupancy shows headcount only (no colour-coding) until then. |

No other entity changes. Migration: one new file in `Migrations/Tenant/` adding the nullable
`Capacity` column to the tenant-schema `groups` table.

## Read models (not persisted — response shapes only)

### `OccupancyGroupSummary`

| Field | Type | Source |
|---|---|---|
| `GroupId` | `Guid` | `Group.Id` |
| `GroupName` | `string` | `Group.Name` |
| `LocationId` | `Guid` | `Group.LocationId` |
| `PresentCount` | `int` | `AttendanceRecord` (today, Present, no check-out) joined to `ChildGroupAssignment` (active today) |
| `Capacity` | `int?` | `Group.Capacity` |
| `Status` | `"green" \| "amber" \| "red" \| null` | null when `Capacity` is null; else green (under), amber (at), red (over) |

### `OccupancyLocationSummary`

| Field | Type | Source |
|---|---|---|
| `LocationId` | `Guid` | `Location.Id` |
| `LocationName` | `string` | `Location.Name` |
| `PresentCount` | `int` | `AttendanceRecord` (today, Present, no check-out) |
| `Capacity` | `int` | `Location.MaxCapacity` |
| `Status` | `"green" \| "amber" \| "red"` | same thresholds as group |
| `WeekAhead` | `OccupancyDayResponse[]` | reuses `GetOccupancyQuery`'s existing shape (feature 012a) — `Date`, `FreeCapacity`, `Closed` |

### `BkrGroupRatio`

| Field | Type | Source |
|---|---|---|
| `GroupId` | `Guid` | `Group.Id` |
| `LocationId` | `Guid` | `Group.LocationId` |
| `PresentCount` | `int` | same present-children join as occupancy |
| `QualifiedStaffCount` | `int` | `RoomShift` (open, this `GroupId`) joined to `StaffProfile`, excluding `StudentVolunteer` — mirrors `GetBkrRatioQuery` |
| `IsNapTime` | `bool` | mirrors `GetBkrRatioQuery`'s nap-time inference, scoped to this group's present children |
| `Threshold` | `int` | mirrors `GetBkrRatioQuery`'s per-caregiver-cap × staff-count computation |
| `Status` | `"green" \| "amber" \| "red"` | mirrors `GetBkrRatioQuery` |

### `BkrBreachWindow`

| Field | Type | Source |
|---|---|---|
| `GroupId` | `Guid` | reconstructed |
| `LocationId` | `Guid` | reconstructed |
| `StartedAt` | `DateTime` | first timestamp where the ratio entered a breaching (red) state |
| `EndedAt` | `DateTime?` | timestamp where it left the breaching state; null if still ongoing |

### `AttendanceSummaryRow`

| Field | Type | Source |
|---|---|---|
| `ChildId` | `Guid` | `Child.Id` |
| `ChildName` | `string` | `Child.FirstName`/`LastName` |
| `GroupId` | `Guid?` | `ChildGroupAssignment` active during the period (see Edge Cases below for a mid-period change) |
| `LocationId` | `Guid` | `AttendanceRecord.LocationId` |
| `PresentDays` | `int` | count of `AttendanceRecord` where `Status == Present` in the period |
| `AbsentJustifiedDays` | `int` | `Status == Absent && AbsenceJustified == true` |
| `AbsentUnjustifiedDays` | `int` | `Status == Absent && AbsenceJustified == false` |
| `ClosureDays` | `int` | `Status == Closure` |

Rolled up per group and per location by summing this row set — no separate rollup entity, computed
in the response builder.

### `InvoiceStatusOverview`

| Field | Type | Source |
|---|---|---|
| `PaidCount` / `PaidTotalCents` | `int` / `int` | `Invoice.Status == Paid`, current `PeriodMonth` |
| `OutstandingCount` / `OutstandingTotalCents` | `int` / `int` | `Status == Sent && DueDate >= today` |
| `OverdueCount` / `OverdueTotalCents` | `int` / `int` | `Status == Sent && DueDate < today` |
| `TotalInvoicedCents` | `int` | sum across all three buckets |
| `OverdueInvoices` | `OverdueInvoiceRow[]` | `InvoiceId`, `ChildName`, `DueDate`, `DaysOverdue`, `TotalCents` |

### `DataCompletenessFlag`

| Field | Type | Source |
|---|---|---|
| `Type` | `"missing_pickup_contact" \| "overdue_vaccine" \| "missing_qualification" \| "missing_pin"` | see research.md R7 |
| `SubjectType` | `"child" \| "staff"` | |
| `SubjectId` | `Guid` | `Child.Id` or `StaffProfile.Id` |
| `SubjectName` | `string` | for display |
| `Detail` | `string?` | e.g. the overdue vaccine's name/due date |

## Edge case: mid-period group/location change

`AttendanceSummaryRow` attributes each day's `LocationId` from that day's own `AttendanceRecord`
(which already carries the location the child actually attended that day), never from the
child's current contract — so a mid-month location change naturally splits across two rows (one
per `LocationId`) rather than mis-attributing days. The same principle applies to `GroupId`: it's
resolved per day from whichever `ChildGroupAssignment` was active on that specific date, not the
child's group today.
