# Feature Specification: Management Reporting

**Feature Branch**: `018-management-reporting`

**Created**: 2026-07-18

**Status**: Draft

**Input**: User description: "Build the director dashboard and management reports for
operational oversight of the KDV. Occupancy dashboard (today + week ahead) per group/location,
colour-coded. BKR compliance overview with live ratio per group and breach history. Monthly
attendance summary (present/absent justified-unjustified/closure days) per child/group/location,
exportable CSV/PDF. Invoice status overview (paid/outstanding/overdue, revenue collected vs
invoiced). Data-completeness monitor flagging missing/expired critical data per child and staff
member. All scoped to the director's tenant; multi-location directors see an aggregate view
filterable by location."

## Product Context

### Feature Type

Mixed — API-backend capability (reporting/aggregation endpoints, CSV/PDF export) plus
User-facing UI (director web dashboard, extending the existing `dashboard` page).

### Primary Consumer

Director (single-location and multi-location). Multi-location directors get an aggregate view
across all their locations with the ability to filter down to one.

### Workflow Boundary

**Reporting & Management** (`workflows.md`) — the first feature in this workflow, which
currently has no detail file. This feature adds `Workflows/reporting.md` documenting the
workflow in full, per the governance rule that a detail file is added once a feature actually
needs it.

Actors: Director (views every report section, exports attendance summaries). System (aggregates
from existing operational tables on read; no new write-side workflow).

Actions: Director opens the dashboard → views today's/week-ahead occupancy, live BKR compliance
+ breach history, invoice status, and the data-completeness monitor → optionally filters by
location (multi-location tenants) → generates a monthly attendance summary and exports it as
CSV or PDF → drills into a flagged item (an over-capacity group, a BKR breach, an overdue
invoice, a data-completeness flag) by navigating to the existing screen that owns that record
(group/attendance/invoice/child/staff detail — all already built by prior features).

Data Flow: read-only aggregation over existing tenant tables (`AttendanceRecord`, `Invoice`,
`Contract`, `StaffSchedule`, `RoomShift`, `Group`, `ChildGroupAssignment`, `Child`,
`ChildContact`, `VaccineRecord`, `StaffProfile`) → director web (dashboard sections) and
on-demand CSV/PDF export. No new write path; this feature introduces no new operational event.

Outputs: on-screen dashboard sections (occupancy, BKR, attendance summary, invoices, data
completeness), CSV export of the monthly attendance summary, PDF export of the monthly
attendance summary.

Cross-Platform Impact: Director web only. No caregiver tablet or parent mobile impact — this
feature reads data those surfaces already produce but adds no screen or capability to either.

### User Impact

This enables a director to see occupancy, BKR compliance, attendance, invoicing, and
data-completeness status across their location(s) in one place, resulting in faster operational
decisions and no more manually compiling that picture from separate screens or spreadsheets.

### UX Requirements

**Persona**: Director (single or multi-location), desktop web, per `platform-rules.md`'s
Director Web App section (density, reporting, multi-column layouts, tables, filtering).

**Platform**: Director web only, desktop-first, minimum supported viewport `1280px`.

**User job**: "At a glance, is my KDV running within capacity, compliant, and financially
healthy today — and where do I need to act?"

**Success criteria**:

- A director can answer the "capacity, compliance, financially healthy" question within 30
  seconds of opening the dashboard, without navigating away.
- A director can identify every currently-breaching group (BKR or over-capacity) and every
  overdue invoice directly from the dashboard, and reach that record's own detail screen in one
  click.
- A director can produce a monthly attendance summary (CSV or PDF) for a chosen period without
  leaving the dashboard.
- All dashboard strings render in the director's selected language (NL/FR/EN) with no
  untranslated text.

**Main flow**: Director opens the dashboard → sees, in order, the occupancy summary (today +
week ahead), the BKR compliance overview (live ratio + recent breach history), the monthly
attendance summary controls, the invoice status overview, and the data-completeness monitor →
a multi-location director narrows every section to one location via a shared location filter →
director exports the attendance summary for a chosen month → director clicks a flagged group,
invoice, child, or staff row to open its existing detail screen.

**Loading/empty/error states**: Each section loads independently (one section's slow query
never blocks another) and shows its own loading skeleton. A location with zero children present
on a given day (closure or holiday) shows `0 / capacity` cleanly, not an error or a blank
section. A tenant with no overdue invoices, no BKR breaches, or no data-completeness flags shows
a short, calm empty state ("No overdue invoices" + icon), not an empty table or a `0` with no
label. A failed export shows a retryable inline error, never a raw stack trace (per
`CLAUDE.md`'s error-handling rule) — the full error is logged server-side.

**Accessibility**: Occupancy/BKR status is never conveyed by color alone — every colour-coded
state (green/amber/red) pairs with an icon, per `design-system.md`'s Status Indicators section
and its "never convey a semantic state by color alone" rule. Every interactive element (filter,
export button, drill-in row) is keyboard-reachable with a visible focus ring, per
`platform-rules.md`'s Director Web App section.

**Offline behavior**: Not applicable — director web is not an offline-first surface.

### Technical Requirements

**API impact**: New read-only, tenant-scoped, location-filterable endpoints: occupancy summary
(today, actual; week-ahead, projected), BKR compliance overview (per-group live ratio, extending
the existing location-scoped `GetBkrRatioQuery` pattern to group scope) plus breach history for
a requested date range, monthly attendance summary (aggregated, with CSV and PDF export),
invoice status overview, and the data-completeness monitor. All read from existing tables; no
new write-side command.

**Data-model impact**: `Group` gains a `Capacity` field (int, nullable, defaulting to
unset/null) — required to colour-code per-group occupancy against a capacity the way
`Location.MaxCapacity` already does at the location level; `Group` previously had no capacity
field by an earlier, narrower decision (see Assumptions for why this feature supersedes that).
No other new tables or columns. BKR breach history is reconstructed on demand from existing
timestamped state changes (`AttendanceRecord.CheckInAt`/`CheckOutAt`,
`RoomShift.CheckedInAt`/`CheckedOutAt`) for the requested date range rather than a new persisted
event log, per the "no separate reporting schema" constraint (see Assumptions).

**Security considerations**: Every endpoint is Director-only (existing `DirectorOnly` policy)
and tenant-scoped via the existing `TenantDbContext`; a multi-location director's location
filter only ever narrows within their own tenant's locations, never crosses tenants.

**Performance considerations**: Add indexes needed for efficient date-range aggregation
(`AttendanceRecord` by `LocationId, Date`; `Invoice` by `LocationId, PeriodMonth` and by
`Status, DueDate` for the overdue list) if not already present. Each dashboard section queries
independently so one expensive aggregation (e.g. a long BKR-breach-history range) does not block
the rest of the dashboard from rendering.

**Testing requirements**: Happy path per section (occupancy shows correct present/capacity
counts; BKR overview shows correct live ratio per group; attendance summary aggregates correctly
and exports match on-screen totals; invoice overview correctly buckets paid/outstanding/overdue;
data-completeness monitor correctly flags each defined condition). Key negative/edge flows: a
closed/holiday location shows `0/capacity` cleanly; a dashboard viewed at midnight during a
shift transition uses the unambiguous calendar date (`BelgianCalendarDay`), not a rolling 24h
window; a child's attendance history spanning multiple contracts for the same period aggregates
correctly across the contract boundary; a director from tenant A cannot see tenant B's data via
any of these endpoints.

## Clarifications

No `[NEEDS CLARIFICATION]` markers were needed. The BACKLOG.md prompt block plus existing
precedent (012a's contract-based forward occupancy projection, 010's location-scoped BKR ratio
computation, 014's `InvoiceStatus`/overdue-by-`DueDate` convention, 013c's vaccine
`NextDueDate`) supplied reasonable defaults for every real ambiguity. See Assumptions below for
the handful of scope calls those defaults required.

### Session 2026-07-18

- Q: What's the default date range for the BKR breach-history view when the director hasn't
  picked one? → A: last 30 days, matching the recency window a director would actually
  investigate (a staffing gap noticed this week, not a multi-year audit); the director can widen
  it explicitly. Self-resolved per this project's standing rule of picking the recommended
  default rather than blocking a scheduled run on an unanswered question — no comparable
  precedent existed to reuse directly, but the choice is low-impact (a UI default, not an
  architectural one) and easily changed later.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director checks today's occupancy and BKR compliance at a glance (Priority: P1)

A director opens the dashboard first thing and needs to know, without digging, whether every
group and location is within capacity and within the legally required BKR ratio right now.

**Why this priority**: This is the single highest-value, highest-frequency use of the
dashboard — the "is everything OK today" check a director runs daily, often multiple times.
Without it, the rest of the feature has no anchor.

**Independent Test**: Can be fully tested by seeding a tenant with two locations, several groups
with attendance both under and over capacity, and staffing both within and breaching BKR — then
confirming the dashboard shows correct colour-coded status per group/location and an accurate
live BKR ratio per group, with a multi-location director able to filter to one location.

**Acceptance Scenarios**:

1. **Given** a group with 8 children present against a capacity of 10, **When** the director
   opens the dashboard, **Then** that group shows green with "8 / 10".
2. **Given** a group with children present at or above its capacity, **When** the director opens
   the dashboard, **Then** that group shows amber (at capacity) or red (over capacity), always
   paired with an icon, not colour alone.
3. **Given** a group where the present-children-to-qualified-staff ratio exceeds the legal BKR
   threshold right now, **When** the director opens the dashboard, **Then** that group's BKR
   status shows red with the live ratio numbers (present count, qualified staff count).
4. **Given** a multi-location director, **When** they select one location in the shared filter,
   **Then** every dashboard section narrows to that location only.
5. **Given** a location fully closed today (KDV closure day), **When** the director opens the
   dashboard, **Then** occupancy shows `0 / capacity` cleanly for that location, not an error.

---

### User Story 2 - Director reviews BKR breach history (Priority: P2)

A director needs to see when, over a recent period, any group breached the BKR ratio, so they
can investigate staffing gaps and demonstrate compliance oversight.

**Why this priority**: Builds directly on User Story 1's live ratio; valuable but secondary to
the today-focused check — a director reaches for this when investigating a specific concern, not
on every visit.

**Independent Test**: Can be fully tested by seeding historical attendance/staffing data with a
known breach window (a period where present count exceeded the qualified-staff-based threshold)
and confirming the breach history correctly reports that window's start, end, and group.

**Acceptance Scenarios**:

1. **Given** a group that breached its BKR threshold for a period yesterday, **When** the
   director views that group's breach history for a date range including yesterday, **Then** the
   breach appears with its start time, end time, and location/group.
2. **Given** a group with no breaches in the selected date range, **When** the director views its
   breach history, **Then** the section shows a calm "no breaches in this period" empty state.

---

### User Story 3 - Director generates and exports a monthly attendance summary (Priority: P1)

A director needs a monthly attendance summary — present/absent (justified/unjustified)/closure
day totals per child, group, and location — to review at the end of the month and share
externally (e.g. with an accountant or inspector) as CSV or PDF.

**Why this priority**: This is an explicit, recurring, externally-facing deliverable (unlike the
live dashboard sections, which are for internal glancing) — high value, matches existing export
patterns (014's invoice PDFs, 013d's printable meal list).

**Independent Test**: Can be fully tested by seeding a month of attendance records spanning a
mid-month contract change for one child, generating the summary for that month, and confirming
the on-screen totals, the CSV export, and the PDF export all agree and correctly attribute every
day across the contract boundary.

**Acceptance Scenarios**:

1. **Given** a month with a mix of present, justified-absent, unjustified-absent, and closure
   days for a child, **When** the director generates the summary for that month, **Then** the
   totals per category are correct for that child, and roll up correctly to that child's group
   and location.
2. **Given** a child whose contract changed location mid-month, **When** the director generates
   the summary spanning that change, **Then** each day is attributed to the location the child
   was actually contracted/present at on that day, with no day double-counted or dropped.
3. **Given** a generated summary, **When** the director exports it as CSV, **Then** the file
   contains the same totals shown on screen. **When** exported as PDF, **Then** the PDF is
   formatted for formal/external sharing (per `CLAUDE.md`'s QuestPDF convention) and shows the
   same totals.

---

### User Story 4 - Director reviews invoice status overview (Priority: P2)

A director needs a current-month snapshot of paid/outstanding/overdue invoices and total revenue
collected vs invoiced, to track financial health without opening the full invoice list.

**Why this priority**: Valuable operational summary, but the director already has a full invoice
list (014) to fall back on — this is a faster glanceable summary layered on top of it, not a
first-time capability.

**Independent Test**: Can be fully tested by seeding invoices in Draft/Sent/Paid states for the
current month, including some Sent past their due date, and confirming the overview correctly
buckets and totals them.

**Acceptance Scenarios**:

1. **Given** the current month's invoices in a mix of paid, outstanding (sent, not yet due), and
   overdue (sent, past due, unpaid) states, **When** the director views the overview, **Then**
   each bucket's count and the total revenue collected vs total invoiced are correct.
2. **Given** an overdue invoice, **When** the director views the overdue list, **Then** it shows
   how many days overdue, and clicking it opens that invoice's existing detail screen.
3. **Given** no overdue invoices exist, **When** the director views the overview, **Then** it
   shows a calm empty state, not an empty table.

---

### User Story 5 - Director reviews the data-completeness monitor (Priority: P3)

A director needs a single list of children and staff members with missing or overdue critical
data (no authorised pickup contact, an overdue vaccine, a staff member missing a qualification
level or check-in PIN), so gaps get fixed before they become a problem.

**Why this priority**: High director value relative to its build cost, per the BACKLOG's own
framing, but it's a periodic housekeeping check rather than a daily-glance need — lowest
priority of the five sections.

**Independent Test**: Can be fully tested by seeding a child with no `CanPickup` contact, a child
with an overdue vaccine (`NextDueDate` in the past with no newer record), a staff member missing
a qualification level, and a staff member with no check-in PIN set — then confirming all four
are flagged with a clear reason and a link to fix them.

**Acceptance Scenarios**:

1. **Given** a child with no contact marked `CanPickup`, **When** the director views the monitor,
   **Then** that child is flagged "no authorised pickup contact."
2. **Given** a child with a vaccine whose `NextDueDate` has passed with no newer record for that
   vaccine, **When** the director views the monitor, **Then** that child is flagged with the
   overdue vaccine.
3. **Given** a staff member whose role requires a qualification level but has none set,
   **When** the director views the monitor, **Then** that staff member is flagged.
4. **Given** a staff member with no check-in PIN set, **When** the director views the monitor,
   **Then** that staff member is flagged (this silently blocks kiosk check-in per feature 008a).
5. **Given** a tenant with none of the above gaps, **When** the director views the monitor,
   **Then** it shows a calm "nothing to flag" empty state.

---

### Edge Cases

- A location has no children present on a given day (closure or holiday): the dashboard shows
  `0 / capacity` cleanly, not an error (per BACKLOG constraint).
- A director views the dashboard at midnight during a shift transition: "today" is the
  unambiguous `BelgianCalendarDay`, never a rolling 24-hour window (per BACKLOG constraint,
  matching the convention already established in features 009/010).
- Historical attendance spans multiple contracts for the same child within the requested period:
  the attendance summary aggregates every day correctly regardless of which contract was active
  on that day.
- A group has no `Capacity` set (existing groups predate this feature): the occupancy dashboard
  shows the present headcount without a capacity ratio or colour-coding for that group, rather
  than dividing by zero or hiding the group.
- A qualified-staff count of zero with children present: BKR status is red (matches the existing
  `GetBkrRatioQuery` rule — see Assumptions).
- A very long BKR breach-history date range: the query still returns correctly, though see
  Assumptions on why performance validation of the on-demand reconstruction approach is a plan
  concern, not a scope concern.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST show, for the current calendar day, each group's present headcount
  against its capacity (when set), colour-coded green (under capacity)/amber (at capacity)/red
  (over capacity), for every location the viewing director has access to.
- **FR-002**: System MUST show, for the current calendar day, each location's total present
  headcount against `Location.MaxCapacity`, colour-coded the same way as FR-001.
- **FR-003**: System MUST show a week-ahead occupancy projection per location, based on active
  contracts' contracted days (not actual attendance, which does not yet exist for future dates),
  reusing the existing forward-occupancy projection approach (feature 012a).
- **FR-004**: System MUST show, per group, the live BKR ratio (present children ÷ on-duty
  qualified staff) right now, extending the existing location-scoped live-ratio computation
  (feature 010) to group scope.
- **FR-005**: System MUST show, for a director-selected date range, the history of BKR breaches
  (periods where the ratio exceeded the legal threshold) per group, including each breach's
  start time, end time, and group/location. When the director has not picked a range, the
  default is the last 30 days.
- **FR-006**: System MUST generate a monthly attendance summary for a director-selected month,
  showing total present days, total absent days (split justified/unjustified), and total
  closure days, per child, rolled up per group and per location.
- **FR-007**: System MUST allow the director to export the monthly attendance summary as CSV.
- **FR-008**: System MUST allow the director to export the monthly attendance summary as PDF,
  formatted for formal/external sharing.
- **FR-009**: System MUST show, for the current month, invoice counts and totals bucketed as
  paid / outstanding (sent, not yet due) / overdue (sent, past due date, unpaid), plus total
  revenue collected vs total invoiced.
- **FR-010**: System MUST show a list of overdue invoices with each invoice's number of days
  overdue, linking to that invoice's existing detail screen.
- **FR-011**: System MUST show a data-completeness monitor flagging: a child with no contact
  marked as authorised to pick up; a child with an overdue vaccine record (`NextDueDate` passed,
  no newer record); a staff member whose role requires a qualification level but has none set;
  a staff member with no check-in PIN configured.
- **FR-012**: System MUST scope every report and the data-completeness monitor to the viewing
  director's own tenant, with no cross-tenant data ever visible.
- **FR-013**: System MUST let a multi-location director filter every dashboard section down to
  a single location; the default (no filter applied) MUST show the aggregate across all their
  locations.
- **FR-014**: System MUST render every user-facing string in this feature via i18n keys, with
  NL/FR/EN translations provided.
- **FR-015**: System MUST show a clean `0 / capacity` state (not an error) for a location or
  group with zero children present, including on a closure/holiday day.
- **FR-016**: System MUST anchor "today" to the unambiguous Belgian calendar day, not a rolling
  24-hour window, so the dashboard's meaning doesn't shift at midnight mid-shift.
- **FR-017**: System MUST show a distinct, human-readable empty state (not a blank or
  zero-valued table) for each section when there is nothing to report (no breaches, no overdue
  invoices, no data-completeness flags).
- **FR-018**: System MUST convey every colour-coded status (occupancy, BKR) with a paired icon,
  never colour alone.
- **FR-019**: System MUST log the full error server-side and show a human-readable message
  (never a raw stack trace) if any report query or export fails.

### Key Entities

- **Group (extended)**: gains an optional `Capacity` — the number of children that group is
  designed to hold, used to colour-code that group's occupancy. Existing groups have no value
  until a director sets one.
- **Occupancy Summary**: a read-model (not a stored entity) combining today's actual present
  headcount (from `AttendanceRecord`) and the week-ahead projected headcount (from active
  `Contract`s), per group and per location, against each one's capacity.
- **BKR Compliance Overview**: a read-model combining the live present-vs-qualified-staff ratio
  per group (from `AttendanceRecord`/`RoomShift`/`StaffProfile`) and, for a requested range, the
  reconstructed history of breach windows.
- **Monthly Attendance Summary**: a read-model aggregating `AttendanceRecord` status counts
  (present/absent-justified/absent-unjustified/closure) per child, rolled up per group (via
  `ChildGroupAssignment`) and per location, for a director-chosen month; exportable as CSV/PDF.
- **Invoice Status Overview**: a read-model bucketing the current month's `Invoice` rows by
  paid/outstanding/overdue (per the existing `InvoiceStatus`/`DueDate` convention) with revenue
  totals.
- **Data-Completeness Flag**: a read-model item — one per detected gap (missing pickup contact,
  overdue vaccine, missing staff qualification, missing staff PIN) — with enough context to link
  to the affected child's or staff member's existing detail screen.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can determine, within 30 seconds of opening the dashboard, whether
  every group/location is within occupancy and BKR compliance right now.
- **SC-002**: A director can generate and export (CSV or PDF) a monthly attendance summary for
  any past month in under 1 minute, with exported totals matching on-screen totals exactly.
- **SC-003**: A director can identify every overdue invoice and its days-overdue count without
  leaving the dashboard.
- **SC-004**: A director can identify every data-completeness gap (missing pickup contact,
  overdue vaccine, missing staff qualification, missing staff PIN) in one place, without
  visiting each child's or staff member's profile individually.
- **SC-005**: A multi-location director can narrow the entire dashboard to one location, and back
  to the full aggregate, without a page reload.
- **SC-006**: Zero untranslated strings appear on the dashboard in any of the three supported
  languages (NL/FR/EN).

## Assumptions

- **`Group.Capacity` is a new field.** `Group.cs` currently has no capacity by an earlier,
  narrower decision ("Minimal — no capacity, no BKR configuration"). This feature supersedes
  that decision because BACKLOG.md explicitly asks for colour-coded per-group occupancy against
  capacity, and no reasonable colour-coding exists without one. The field is optional/nullable so
  existing groups are unaffected until a director sets a value (Edge Cases).
- **BKR breach history is reconstructed on demand, not stored in a new event log.** The BACKLOG
  constraint explicitly rules out a separate reporting schema or data warehouse. Breach windows
  are derived by replaying existing timestamped state changes
  (`AttendanceRecord`/`RoomShift` check-in/out events) for the requested range rather than
  persisting a dedicated breach-event table. `plan.md` should confirm this holds up
  performance-wise for the ranges directors actually request (a rolling recent window, not
  multi-year history); if it doesn't, introducing a small persisted breach log is a reasonable
  follow-up, not a blocker for this feature's scope.
- **Week-ahead occupancy projection is location-level only**, reusing feature 012a's existing
  contract-based projection (`GetOccupancyQuery`) as-is. Per-group week-ahead projection is not
  built in this feature — contracts aren't group-scoped, and the BACKLOG's "today + week ahead"
  framing reads as a location-level capability with group-level detail scoped to today (the one
  day actual attendance and staffing data exists for).
- **Staff document gaps (contracts, training records, certification expiry) are out of scope.**
  The BACKLOG prompt's data-completeness example lists "staff document gaps," but no staff
  document/dossier data model exists yet (that's feature 028, staff HR dossier, not yet built).
  The monitor flags what the current schema actually supports: missing qualification level and
  missing check-in PIN. Full staff document completeness is deferred to build on top of 028,
  mirroring how 013g/013h split the vaccine catalog into infrastructure-now/content-later.
- **"Expired medical info" maps to overdue vaccines specifically**, via `VaccineRecord.NextDueDate`
  (feature 013c) — the only critical child medical field with an actual due/expiry date in the
  current schema. Other medical fields (allergies, GP/pediatrician contact, medical conditions)
  are free-text and optional by design (many children legitimately have none), so their absence
  is not treated as a completeness gap.
- **Occupancy colour thresholds**: green when present count is under capacity, amber when present
  count exactly equals capacity, red when it exceeds capacity — matching the semantic meaning of
  "amber = near/at capacity, red = over" from the BACKLOG prompt and the existing BKR
  green/amber/red convention (feature 010) for visual consistency across the dashboard.
- **This feature extends the existing `web/app/(app)/dashboard/page.tsx`** rather than creating a
  new route — that page's own comment already anticipates this ("A future feature adding more
  widgets extends this page, not a new one").
- Revenue forecasting, in/outflow trend reports, staff payroll-hour reports, IKT subsidy
  tracking, and the Opgroeien monthly XML report are out of scope, per BACKLOG.md.
