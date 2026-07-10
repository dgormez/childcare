# Feature Specification: Caregiver Scheduling (Weekly Staff Rota)

**Feature Branch**: `012-caregiver-scheduling`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Build the weekly staff rota — who works where, on which days and hours. Weekly rota builder in web admin, rota copy, absence marking, BKR integration, multi-location support. Caregiver-facing schedule visibility is backend-only in this feature (own-shifts read API only) — the kiosk tablet UI is deferred to feature 027 (Staff App), per an explicit scope decision: feature 008a replaced personal caregiver login with a shared kiosk tablet with no personal session, and BACKLOG.md already scopes 'own schedule view' as feature 027's job."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web).

### Primary Consumer

Director (builds/manages the rota). Caregiver is a secondary consumer of the read-only own-shifts API only — no caregiver-facing UI ships in this feature (see Assumptions).

### Workflow Boundary

This feature belongs to the Classroom Operations workflow (workflow map's "Includes: Staffing"). `Workflows/classroom-operations.md` is extended with a new "Flow — weekly staff rota (feature 012)" section alongside the existing 008a room-shift-register flow, since the two are distinct but related: the rota is the *planned* schedule, the room shift register is the *actual* presence log.

Actors: Director (builds/edits the rota, copies weeks, marks absences); Caregiver (their own schedule is readable via API only, no UI consumer yet); System (the rota builder's own projected on-duty count, a planning-only signal — feature 010's live BKR ratio is a separate, already-correct computation sourced from real-time check-in presence and is not touched by this feature; see Assumptions).

Actions: Director assigns staff to location/group/date/time; director copies a week's rota forward, with closure-day entries excluded from the copy; director marks a staff member absent with a reason; system serves a caregiver's own schedule via a personal-account-scoped read endpoint.

Data Flow: Director web writes `staff_schedules`. This feature reads `staff_schedules` (joined with absence flag and feature 005's staff qualification) to compute a projected on-duty count for the rota builder's own planning display. Feature 010's BKR computation is unaffected — it reads `RoomShifts` (feature 008a's real-time check-in log), not `staff_schedules`. A future consumer (feature 027) reads a caregiver's own schedule via the API this feature exposes.

Outputs: A weekly rota per location/group, an absence-adjusted projected on-duty count (planning-only), a rota-copy operation, and an own-schedule read API.

Cross-platform Impact: Director web gains the full rota builder (create/edit/copy/absence-mark). Caregiver tablet (008a's kiosk) is explicitly not involved — no personal session exists there to host a personal schedule view. Backend gains the `staff_schedules` table, overlap/absence validation, the rota-copy operation, and the projected on-duty count endpoint (planning-only — does not touch feature 010's live BKR path). Parent mobile is not involved.

### User Impact

This enables a director to build and maintain the weekly staff rota across locations and groups, resulting in an auditable planned schedule and an accurate forward-looking staffing picture (via the projected on-duty count) for planning purposes — independent of, and without altering, feature 010's live BKR ratio.

### UX Requirements

Persona: A director managing multi-location staff scheduling week by week.

Platform: Director web, desktop-first at 1280px and above, high-density per `platform-rules.md`'s Director Web section — this is exactly the "Staff scheduling and room assignment" example that document names.

User job: Assign every staff member to a location/group/shift for the upcoming week, correct mistakes before the shift date arrives, copy last week forward instead of rebuilding, and mark absences.

Success criteria: A full week's rota is built in a few minutes via copy + targeted edits, not built from scratch each week; overlap mistakes are caught before saving, not discovered later.

Main flow: Week-grid view (staff × days) → click a cell → assign location/group/hours or mark absent → save. Alternative: copy prior week → review/correct flagged conflicts (overlaps, closure-day exclusions) → done.

Loading/empty/error states: Per `design-system.md` — an empty week shows an icon + one sentence ("No shifts scheduled yet for this week"); overlap/validation errors point to the specific conflicting cell; rota-copy reports which entries were skipped (closure days, existing conflicts) rather than failing silently.

Accessibility: Keyboard-reachable grid and actions with a visible focus ring, per `platform-rules.md`'s director-web keyboard-navigation requirement — no touch-target floor applies (desktop, mouse/keyboard).

Offline behavior: Not required — director web assumes network access for all writes, consistent with every other director-web feature to date (007a, 011).

### Technical Requirements

API impact: Director-only (`DirectorOnly` policy) endpoints for CRUD on `staff_schedules`, listing, the projected on-duty count, and the rota-copy endpoint (FR-015); and a personal-account-scoped, `StaffOrDirector` read endpoint for a caregiver's own shifts (FR-012) — served via the caregiver's personal account context, not a device-token route, since device-token-authenticated tablet routes carry no individual caregiver identity (feature 009's research.md R4 precedent).

Data-model impact: New `staff_schedules` table, tenant-scoped (schema-per-tenant, no explicit tenant FK, per the established 004/005 pattern), with `UNIQUE(staff_id, date, start_time)`.

Security considerations: All writes are `DirectorOnly`. The staff-member-overlap validator must be safe under concurrent writes — reuse feature 007's `IAdvisoryLockService` pattern (keyed on staff member) rather than a client-side-only pre-check.

Performance considerations: Rota copy is a bulk-insert operation. Index `staff_schedules` on `(staff_id, date)` and `(location_id, date)` to serve both the rota-builder queries and this feature's own projected on-duty lookups efficiently.

Testing requirements: Happy-flow rota build and copy; overlap-rejection (including a concurrency test); absence marking and its effect on the projected on-duty count; closure-day exclusion during copy; past-date immutability; caregiver own-shifts read scoping (returns only the requesting caregiver's entries); a regression test confirming feature 010's `GetBkrRatioQuery` output is unchanged by this feature's data (proves the two stay decoupled).

## Clarifications

### Session 2026-07-10

- Q: When a rota copy targets a week that already has some schedule entries, should the
  system skip conflicting slots (and report which were skipped) or block the whole copy
  operation? → A: Skip conflicting slots and report them — consistent with FR-009's
  closure-day-exclusion behavior, avoids silent overwrite without forcing an all-or-nothing
  retry for an otherwise-clean copy.
- Q: When a staff member is deactivated (feature 005) while holding future-dated schedule
  entries, what happens to those entries? → A: The entries are not deleted, but are
  automatically excluded from the projected on-duty count (treated the same as if marked
  absent) from the moment of deactivation. The director sees them in the rota (so they know
  to reassign the shift) but they never silently count toward that projection.
- Q: BACKLOG.md's original prompt states "the live BKR count in feature 010 uses
  staff_schedules to know who is on duty right now" — is that accurate against what feature
  010 actually shipped? → A: No. `GetBkrRatioQuery` (feature 010) computes on-duty qualified
  staff from `RoomShifts` (feature 008a's real-time PIN check-in/out log), not from any
  schedule — and this is the *more* accurate signal (a scheduled-but-absent-without-updating-
  the-rota caregiver was already correctly excluded, since they never check in). This
  feature does not wire `staff_schedules` into feature 010's live computation; FR-006/FR-007
  are revised to describe a separate, planning-only "projected on-duty count" derived from
  the rota, distinct from and non-authoritative over the live, presence-based BKR ratio.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director builds the weekly rota (Priority: P1)

A director opens the rota builder for an upcoming week and assigns staff members to
locations, groups, and shift hours for each day. This is the foundational capability —
without it, nothing else in this feature has data to operate on.

**Why this priority**: This is the entire point of the feature. No rota exists without it,
and every other story (copy, absence, own-schedule read) depends on schedule entries existing.

**Independent Test**: Can be fully tested by a director creating schedule entries for
several staff members across a week and verifying each entry persists with the correct
staff/location/group/date/time, independent of copy or absence functionality.

**Acceptance Scenarios**:

1. **Given** a director viewing an empty week for a location, **When** they assign a staff
   member to a location, group, date, and time range, **Then** a schedule entry is created
   and visible in the rota.
2. **Given** a staff member already has a shift on a given date, **When** the director adds
   a second, non-overlapping shift for that staff member at a different location on the same
   date, **Then** both entries are saved (multi-location-per-week is supported).
3. **Given** a staff member already scheduled at Location A from 08:00–12:00, **When** the
   director tries to schedule the same staff member at Location B from 11:00–15:00 the same
   date, **Then** the system rejects the overlapping shift with a clear error.
4. **Given** a schedule entry for a future date, **When** the director edits the location,
   group, or time, **Then** the change is saved and reflected immediately.
5. **Given** a schedule entry whose date has already passed, **When** the director attempts
   to edit or delete it, **Then** the system rejects the change — past schedules are
   immutable historical records.
6. **Given** a staff member added to the system mid-week, **When** the director opens the
   current week's rota, **Then** that staff member is available to schedule immediately.

---

### User Story 2 - Director copies a week's rota forward (Priority: P2)

A director copies the current week's schedule to the following week rather than rebuilding
it from scratch, since most KDVs run the same rota week after week.

**Why this priority**: A major time-saver called out explicitly in scope, but the feature
is usable without it (director can still build each week manually) — so it ranks below the
core builder.

**Independent Test**: Can be fully tested by copying a populated week to an empty target
week and verifying every source entry is replicated with the target week's dates, without
needing absence-marking or the own-schedule read endpoint to be present.

**Acceptance Scenarios**:

1. **Given** a fully-scheduled current week, **When** the director copies it to next week,
   **Then** every schedule entry (staff, location, group, time range) is replicated onto the
   corresponding day of the target week.
2. **Given** the target week contains a closure day (an existing closure-calendar entry for
   that location/date), **When** the copy runs, **Then** schedule entries that would fall on
   that closure day are excluded from the copy and the director is shown which day(s) were
   skipped and why.
3. **Given** the target week already has some schedule entries, **When** the director copies
   into it, **Then** the system skips slots that would conflict with an existing entry,
   completes the copy for all non-conflicting slots, and reports which slots were skipped and
   why — never silently overwriting an existing entry.
4. **Given** a target week that is not after the source week (e.g. the same week, or an
   earlier week), **When** the director attempts the copy, **Then** the system rejects the
   request (FR-016) — copy is a forward-planning operation, not a way to retroactively alter
   history.

---

### User Story 3 - Director marks a staff member absent (Priority: P2)

A director marks a staff member absent for a given day (sick, leave, or holiday), which
removes them from the rota builder's own projected on-duty count (a planning signal —
feature 010's live BKR ratio is unaffected, see FR-007).

**Why this priority**: Keeps the rota's projected staffing picture accurate for planning
purposes — a qualified caregiver who is known to be absent should not appear as "covered" in
the director's forward view — but it is a targeted edit on top of an existing schedule entry,
not a prerequisite for the rota to exist. (Feature 010's live BKR ratio is unaffected either
way — see FR-007.)

**Independent Test**: Can be fully tested by marking an existing schedule entry absent and
verifying it is flagged as such and excluded from on-duty queries, independent of the copy
feature.

**Acceptance Scenarios**:

1. **Given** a staff member with a scheduled shift today, **When** the director marks them
   absent with a reason (sick/leave/holiday), **Then** the shift is flagged `is_absent =
   true` and no longer counts toward the projected on-duty count for that date/time.
2. **Given** a staff member marked absent, **When** the rota builder's projected on-duty
   count is computed for that location and time, **Then** that staff member is excluded from
   it. (Feature 010's live BKR ratio, sourced from actual check-in presence, is unaffected —
   it already excludes anyone who hasn't physically checked in.)
3. **Given** a staff member marked absent in error, **When** the director un-marks the
   absence (for a future-dated entry), **Then** the shift reverts to counting normally.

---

### User Story 4 - Caregiver's own schedule is readable via API (Priority: P3)

The system exposes a read endpoint returning a caregiver's own upcoming shifts, scoped to
their personal account — ready for a future consumer (feature 027, Staff App) to display it.
No caregiver-facing UI ships in this feature; the kiosk tablet (008a) has no personal
session to host a personal schedule view, and BACKLOG.md already scopes "own schedule view"
as feature 027's job.

**Why this priority**: Required by the original feature intent ("visible to caregivers") but
deliberately narrowed to an API contract only for this feature — see the Assumptions
section for the full scope decision. Lowest priority because no product surface consumes it
yet; it exists so 027 doesn't have to build both the read path and the UI from scratch.

**Independent Test**: Can be fully tested by calling the endpoint as an authenticated staff
account and verifying it returns only that account's own schedule entries, scoped correctly
and excluding other staff members' shifts — independent of any UI.

**Acceptance Scenarios**:

1. **Given** an authenticated staff account with scheduled shifts this week, **When** they
   request their own schedule, **Then** only their own entries are returned (not other staff
   members' shifts, even at the same location).
2. **Given** an authenticated staff account with no scheduled shifts, **When** they request
   their own schedule, **Then** an empty result is returned, not an error.

---

### Edge Cases

- A director enters a shift for the wrong location. They can correct it via edit up until
  the shift's date arrives (US1, scenario 4); once the date has passed, it is immutable
  (US1, scenario 5).
- Rota copy targets a week containing a closure day for that location — those entries are
  excluded and flagged, not silently created against a day the KDV is closed (US2, scenario
  2).
- Two directors (or the same director in two tabs) attempt to save overlapping schedule
  entries for the same staff member concurrently — the overlap check must be safe under
  concurrent writes, not just a client-side pre-check (see IAdvisoryLockService precedent
  from feature 007).
- A staff member is deactivated (feature 005) while they have future-dated schedule entries
  — those entries are not deleted, but are automatically excluded from the projected on-duty
  count (treated the same as an absence), remaining visible to the director for manual
  reassignment.
- A schedule entry's group is left unassigned (`group_id = null`) — this is valid (e.g. a
  floater covering a location without a fixed group) and must not block projected on-duty
  eligibility.
- A staff member is scheduled for today but never checks in (e.g. calls in sick without the
  director updating the rota first). Feature 010's live BKR ratio already excludes them,
  since it counts real check-ins, not scheduled shifts — this feature's projected count is a
  separate planning signal and is not consulted by the live ratio.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to create a schedule entry for a staff member
  specifying location, group (optional), date, start time, and end time.
- **FR-002**: Directors MUST be able to edit or delete a schedule entry for a future-dated
  (not-yet-occurred) shift.
- **FR-003**: The system MUST reject a schedule entry that would cause a staff member to have
  two overlapping time ranges on the same date, regardless of whether the two entries are at
  the same location or different locations — a staff member cannot be in two places (or two
  groups) at once.
- **FR-004**: The system MUST reject creation, edit, or deletion of a schedule entry whose
  date has already passed — past schedules are immutable historical records.
- **FR-005**: Directors MUST be able to mark a staff member's schedule entry as absent for a
  given day, with a reason (sick, leave, or holiday).
- **FR-006**: A schedule entry marked absent MUST be excluded from any *planned/projected*
  on-duty staff count derived from `staff_schedules` (see FR-007 — this is distinct from
  feature 010's live BKR ratio, which is sourced from actual check-in presence and is
  unaffected by this feature).
- **FR-007**: The system MUST expose a projected on-duty qualified-staff count for a given
  location/date/time, derived from `staff_schedules` joined with absence and feature 005's
  staff qualification (excluding non-qualifying staff, same rule as feature 010's `Student
  Volunteer` exclusion) — for director planning purposes in the rota builder (e.g., "is this
  day adequately staffed on paper"). Feature 010's live BKR ratio (`GetBkrRatioQuery`) is
  computed from actual `RoomShifts` check-in/out data (feature 008a) and is NOT modified by
  this feature — a scheduled-but-not-checked-in staff member never counted toward live BKR
  before this feature and still does not after it; an absent-marked entry simply confirms
  what real presence data already shows. This feature's projected count is planning-only and
  informational, never a substitute for the live, presence-based ratio.
- **FR-008**: Directors MUST be able to copy an entire week's schedule to another week,
  replicating staff/location/group/time-range per day.
- **FR-009**: When a rota copy target week contains a closure-calendar entry (feature 011)
  for a given location/date, the system MUST exclude schedule entries that would fall on
  that closure day from the copy and report which entries were skipped.
- **FR-009a**: When a rota copy target slot already has an existing schedule entry, the
  system MUST skip that slot (not overwrite it), complete the copy for all other
  non-conflicting slots, and report which slots were skipped and why.
- **FR-009b**: When a staff member is deactivated (feature 005) while holding future-dated
  schedule entries, those entries MUST NOT be deleted, but MUST be automatically excluded
  from the projected on-duty count (FR-007) from the moment of deactivation, remaining
  visible to the director for manual reassignment.
- **FR-010**: A staff member MUST be able to appear at different locations on different days
  within the same week.
- **FR-011**: A staff member added to the system mid-week MUST be immediately available to
  schedule for the remainder of that week.
- **FR-012**: The system MUST expose a read endpoint returning a caregiver's own schedule
  entries, scoped to their personal authenticated account, returning no other staff member's
  entries.
- **FR-013**: All user-facing strings in the rota builder MUST use i18n keys (NL/FR/EN) —
  no hardcoded text.
- **FR-014**: All write operations on schedule entries MUST be restricted to directors
  (DirectorOnly authorization policy).
- **FR-015**: All read operations on schedule entries and the projected on-duty count
  (everything except FR-012's own-schedule endpoint) MUST be restricted to directors
  (DirectorOnly authorization policy) — only FR-012's endpoint uses the more permissive
  StaffOrDirector policy, since it is scoped to the caller's own data.
- **FR-016**: The system MUST reject a rota-copy request whose target week is not strictly
  after the source week, or whose target week has already fully or partially passed —
  copying is a forward-planning operation, not a way to retroactively alter historical
  schedule data (which FR-004 already makes immutable).
- **FR-017**: The system MUST reject creating or updating a schedule entry that would assign
  a staff member to a location they are not eligible for (`StaffLocationEligibility`, feature
  005) — added via `/speckit-converge` (finding F1): a rota entry a director could plan but
  that would fail at actual check-in time (`VerifyPinCommand`/`CheckInCommand` already reject
  an ineligible caregiver, `RoomShiftFailure.NotEligible`) is a data-integrity gap, not a
  reasonable simplification. The director-web rota builder also only offers scheduling
  eligible staff for the currently selected location, so this is never surfaced as a
  late/confusing save-time error in the normal flow.

### Key Entities

- **StaffSchedule**: A single shift assignment — staff member, location, optional group,
  date, start time, end time, absence flag, absence reason (when absent). Belongs to the
  tenant schema (schema-per-tenant, no explicit tenant column, per the established pattern
  from features 004/005). Immutable once its date has passed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can build a full week's rota for 10+ staff members across 2
  locations, using copy-then-correct rather than building from scratch, in under 5 minutes.
- **SC-002**: 100% of attempted schedule entries that would double-book a staff member at
  overlapping times (whether at the same location or different locations) are rejected
  before being saved.
- **SC-003**: 100% of staff marked absent on a given date/time are excluded from that
  location's projected on-duty staff count (the rota builder's planning indicator — feature
  010's live, presence-based BKR ratio is unmodified and unaffected by this feature).
- **SC-004**: 100% of attempts to edit or delete a schedule entry after its date has passed
  are rejected.
- **SC-005**: Rota copy into a week containing a closure day excludes 100% of entries that
  would fall on that closure day, with zero silent data loss on non-conflicting entries.

## Assumptions

- **Caregiver-facing schedule UI is out of scope for this feature** — resolved via an
  explicit scope decision (not a default guess) during specification: feature 008a replaced
  personal caregiver login with a shared kiosk tablet that has no personal session to host a
  personal "my schedule" view, and BACKLOG.md already scopes feature 027 (Staff App) as
  delivering exactly this "own schedule view" on a separate, personally-authenticated app.
  This feature ships the data model, the director-facing rota builder, and a read API for a
  caregiver's own schedule (FR-012) so feature 027 can consume it directly.
- Feature 010's live BKR ratio (`GetBkrRatioQuery`) is sourced from `RoomShifts` (real-time
  check-in presence, feature 008a) and is neither read from nor written to by this feature —
  confirmed against the actual shipped implementation, not assumed (see Clarifications). This
  feature's `staff_schedules`-derived "projected on-duty count" is a separate, planning-only
  signal for the rota builder; it is informational and never overrides or feeds the live
  ratio.
- Overlap and absence rules apply per staff member across the whole tenant (all locations),
  consistent with feature 007's split-location day-overlap validator precedent.
- Time values are stored and interpreted in the KDV's local time (Belgium, `Europe/Brussels`)
  consistent with the project's single-country Phase 1 scope — no per-tenant timezone field
  exists yet (same simplification documented in feature 009's shipped-notes).
- A "week" is Monday through Sunday, matching the ISO-8601 week definition already implicit
  in feature 007a/011's calendar UI conventions — a rota-copy request identifies both source
  and target week by their Monday date.
