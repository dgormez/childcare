# Research: Management Reporting

## R1 — Occupancy: reuse feature 012a's forward projection, add a today-actual layer

**Decision**: The week-ahead location occupancy projection reuses `GetOccupancyQuery`
(`backend/ChildCare.Application/WaitingList/GetOccupancyQuery.cs`) unmodified — it already
computes, per date, `Location.MaxCapacity - occupiedCount` from active contracts' `ContractedDays`
against `KdvClosureDay`. Today's *actual* occupancy (both per-group and per-location) is a new,
separate query reading `AttendanceRecord` (status = Present, no check-out) joined to
`ChildGroupAssignment` (active on today's date) for the per-group breakdown.

**Rationale**: `GetOccupancyQuery`'s own doc comment already states why attendance can't be used
for future dates ("feature 010's data is same-day/historical and doesn't exist for future
dates") — so today needs attendance-based actuals and the week ahead needs the existing
contract-based projection; these are two different data sources by necessity, not an
inconsistency to resolve.

**Alternatives considered**: Projecting "today" from contracts too, for a single consistent
data source — rejected because it would show a child as "occupying a spot" even after they
checked out or were marked absent, which is less accurate than the real-time attendance data
this feature already has for today specifically.

## R2 — Per-group BKR: extend `GetBkrRatioQuery`'s pattern to group scope

**Decision**: Add a new query (`GetGroupBkrRatioQuery` or a `LocationId, GroupId` overload/
sibling of the existing query) that mirrors `GetBkrRatioQuery`
(`backend/ChildCare.Application/Attendance/GetBkrRatioQuery.cs`) exactly, but filters
`AttendanceRecord` present-children by `ChildGroupAssignment.GroupId` and `RoomShift` staff by
`RoomShift.GroupId` (already a first-class column on `RoomShift`), instead of by `LocationId`
alone. The nap-time/threshold/status logic (FR-007a–e in feature 010's own spec) is reused as-is.

**Rationale**: `RoomShift` already carries `GroupId` (feature 008a); `ChildGroupAssignment`
already resolves a child's current group. No new data is needed, only a narrower `WHERE` on
data this feature already reads elsewhere.

**Alternatives considered**: Computing group ratios purely by re-aggregating the existing
location-level query's raw data in the read-model layer instead of a new query — rejected as it
would need to re-fetch or duplicate the same present/staff joins anyway; a dedicated query
handler is no more code and stays consistent with this codebase's one-query-per-read-shape
convention (`GetBkrRatioQuery`, `GetOccupancyQuery`, etc. are all single-purpose).

## R3 — BKR breach history: on-demand reconstruction, no new event table

**Decision**: For a requested date range (default: last 30 days, per spec.md's Clarifications),
reconstruct breach windows by replaying `AttendanceRecord.CheckInAt`/`CheckOutAt` and
`RoomShift.CheckedInAt`/`CheckedOutAt` timestamps within that range, per location/group: walk
the sorted list of state-change timestamps, recomputing present-count and qualified-staff-count
after each event (same threshold logic as `GetBkrRatioQuery`), and emit a breach window whenever
the ratio crosses into/out of a breaching state.

**Rationale**: BACKLOG.md's explicit constraint rules out a separate reporting schema or data
warehouse. The number of check-in/check-out events per location per day is bounded by that
day's headcount and staff count (small), so replaying a 30-day default window is a bounded,
cheap computation — not the kind of historical-scale aggregation the "no data warehouse"
constraint is guarding against.

**Alternatives considered**: A persisted `BkrBreachEvent` log, written whenever the live query
detects a breach — rejected for this feature's scope because it requires a background
process/trigger to write entries even when nobody is looking at the report (this codebase has no
background-job infrastructure yet, per `Workflows/billing.md`'s own precedent), and because the
on-demand approach satisfies the spec's actual requirement without it. If profiling in
implementation shows the replay is too slow for realistic ranges, a lightweight persisted log is
a reasonable follow-up feature, not a blocker here (documented in spec.md's Assumptions).

## R4 — `Group.Capacity`: new nullable column, no new table

**Decision**: Add `public int? Capacity { get; set; }` to `Group.cs`, nullable, no default
(existing groups get `null` until a director sets one). One new EF Core migration in
`Migrations/Tenant/`.

**Rationale**: `Group.cs`'s existing comment ("Minimal — no capacity, no BKR configuration") was
a decision made before any feature needed per-group capacity; this feature is that need. Nullable
avoids forcing every existing tenant's groups to retroactively get a capacity value before this
ships (Edge Cases: a group with no `Capacity` set shows headcount only, no ratio/colour).

**Alternatives considered**: Deriving an implicit per-group capacity from `Location.MaxCapacity`
divided evenly across groups — rejected; group sizes are not generally equal in real KDVs (a baby
room legitimately holds fewer children than a toddler room), so an even split would misrepresent
real capacity and produce false amber/red signals.

## R5 — Monthly attendance summary: aggregate query + CSV/PDF export

**Decision**: One MediatR query aggregates `AttendanceRecord` status counts
(Present/Absent-with-`AbsenceJustified`/Closure) grouped by `ChildId` for a requested
`(LocationId?, PeriodMonth)`, joined to `ChildGroupAssignment` for the group rollup. The same
aggregated result feeds three outputs: the on-screen JSON response, a CSV writer (new — no CSV
export exists anywhere in this codebase yet), and a QuestPDF renderer following
`QuestPdfInvoiceGenerator`'s on-demand, unstored pattern (not `FiscalAttestation`'s stored-record
pattern, since this report isn't a permanent per-child legal record — it's a point-in-time
export).

**Rationale**: Building the aggregation once and rendering it three ways avoids duplicating the
grouping/rollup logic per export format, and matches this codebase's established pattern of
computing a shared result model once and handing it to format-specific renderers
(`MilestonePortfolioBuilder` from feature 016 is the direct precedent for "one query builds the
model, multiple consumers render it").

**Alternatives considered**: A separate query per export format — rejected as pure duplication
with no benefit; the underlying data and grouping are identical regardless of output format.

## R6 — Invoice status overview: reuse `InvoiceStatus`/`DueDate` overdue convention

**Decision**: A new query buckets the current month's `Invoice` rows by
`Status == Paid` / `Status == Sent && DueDate >= today` / `Status == Sent && DueDate < today`
(overdue) — the exact convention `Invoice.cs`'s own comment documents
("'Overdue' is not a stored value — it's computed as `Status == Sent && DueDate < today`").
Revenue collected sums `TotalCents` for `Paid` invoices; total invoiced sums `TotalCents` across
all three buckets.

**Rationale**: Reusing the existing, already-battle-tested overdue convention from feature 014
avoids introducing a second definition of "overdue" that could drift from the one the invoice
list itself uses.

**Alternatives considered**: None — this is a direct, uncontested reuse of existing logic.

## R7 — Data-completeness monitor: four checks, all against existing fields

**Decision**: Four independent checks, each producing a flat list item:
1. A `Child` with zero `ChildContact` rows where `CanPickup == true`.
2. A `Child` with a `VaccineRecord` whose `NextDueDate < today` and no newer record for the
   same vaccine (`VaccineTypeId`/`CustomVaccineEntryId`) with a later `AdministeredOn`.
3. A `StaffProfile` linked to a `TenantUser` with `Role == Staff` where `QualificationLevel`
   is null.
4. A `StaffProfile` with `PinHash == null` (blocks kiosk check-in per feature 008a).

**Rationale**: Every check reads a field that already exists and already has a clear, correct
meaning — no new schema, no speculative "critical data" definition invented for this feature.
Staff document/dossier gaps (contracts, training records) are explicitly out of scope because no
such data model exists yet (feature 028, not yet built) — flagging a field that doesn't exist
would be fabrication, not a real check (spec.md's Assumptions documents this deferral).

**Alternatives considered**: Flagging children missing optional medical fields (allergies, GP
contact) as "incomplete" — rejected; those fields are legitimately optional (many children have
no allergies, no GP on file yet at intake), so their absence isn't a data-quality gap the way a
missing pickup authorization or an overdue vaccine is.

## R8 — CSV export format

**Decision**: RFC 4180-style CSV, UTF-8 with BOM (so Excel on Windows — the realistic
consumer for a Belgian KDV director sharing a report — renders NL/FR accented characters
correctly), comma-delimited, generated server-side and streamed as `text/csv` with a
`Content-Disposition: attachment` header, mirroring how the PDF endpoints already stream
`application/pdf` on demand rather than persisting a file.

**Rationale**: This is the first CSV export in the codebase, so there's no existing convention
to match — UTF-8 BOM + comma delimiter is the standard choice for Excel compatibility in a
Belgian/European context, and streaming (not storing) matches every existing on-demand export in
this codebase.

**Alternatives considered**: Semicolon-delimited CSV (common in some European Excel locales that
treat comma as the decimal separator) — deferred; comma-delimited is the more broadly compatible
default, and this can be revisited if a real user reports an issue.
