# Feature Specification: Staff HR Dossier & Time Registration

**Feature Branch**: `028-staff-hr-dossier`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Give directors a digital HR file per staff member — employment
contracts, training records, qualification documents — and let staff clock in/out so the KDV has
accurate hours worked for payroll and for the medewerkersbeleid subsidy application (Opgroeien
2025 subsidy that rewards KDVs meeting the new BKR ratios early, verified by hour counts per
caregiver per function)."

## Product Context

### Feature Type

Mixed — API-backend capability (time entries, documents, reporting) plus user-facing UI on two
surfaces: director-web (HR dossier, contract-expiry dashboard block, subsidy report) and
staff-mobile (clock in/out, extending feature 027's existing personal-phone app).

### Primary Consumer

Director (manages HR dossiers, unlocks/corrects time entries, downloads the subsidy report) and
Staff (clocks in/out via staff-mobile).

### Workflow Boundary

No existing workflow in `workflows.md` covers staff employment records or worked-hours tracking —
**Classroom Operations** only covers room/ratio "Staffing" (008a's room-shift register), not HR
documents or time registration. This feature adds a new **Staff Management** workflow to
`workflows.md` (see that file's own diff, made as part of this spec per its governance rules):
covering the HR dossier (contracts/training/qualification documents), time registration (clock
in/out with a per-entry function), contract-expiry alerts, and the medewerkersbeleid subsidy
report.

- **Actors**: Director (manages dossier, unlocks/corrects time entries, downloads the subsidy
  report); Staff (clocks in/out via staff-mobile).
- **Actions**: upload/manage HR documents; clock in/out with function selection; lock/unlock time
  entries; generate the subsidy report.
- **Data flow**: staff-mobile clock action → `staff_time_entries` (tenant schema) → subsidy
  report aggregation. Director-web document upload → GCS signed URL → `staff_documents` (tenant
  schema) → contract-expiry dashboard block.
- **Outputs**: an HR dossier per staff member, a contract-expiry alert list, a downloadable
  subsidy report and an hours CSV export.
- **Cross-platform impact**: staff-mobile (clock in/out, extending feature 027) and director-web
  (HR dossier screens, dashboard block, subsidy report) are affected. Caregiver tablet and parent
  mobile are not.

### User Impact

This enables directors to maintain a compliant digital HR file per staff member and track
accurate worked hours per function, resulting in audit-ready records for payroll handoff and the
medewerkersbeleid subsidy application, while letting staff clock in/out from their personal phone
instead of a paper register.

### UX Requirements

- **Persona / platform**: Director on director-web (desktop, high-density tables per
  `platform-rules.md`); Staff on staff-mobile (personal phone, one-tap actions, matching feature
  027's existing screens).
- **User job**: a director needs to see at a glance which staff contracts are expiring and
  produce a subsidy-ready hours report; staff need a fast, unambiguous way to start/end a shift.
- **Success criteria**: a director can find any staff member's HR documents and generate the
  subsidy report in a couple of clicks; staff clock in/out in one tap, with a function picker
  appearing only when it's actually ambiguous (see FR-005).
- **Main flow**: staff opens staff-mobile → taps "Begin dienst" → (function picker only if
  ambiguous) → later taps "Einde dienst". Director opens Staff → [staff member] → a new "Dossier"
  tab → uploads/views documents. Director dashboard shows a "Personeel — verlopende contracten"
  block. Director opens the new "Personeelsuren" report page (a top-level sidebar entry — this
  codebase has no "Rapporten" parent nav; feature 018's reports live inline on the dashboard,
  and every other report-like screen is its own flat sidebar item), selects period/location, and
  downloads the medewerkersbeleid report.
- **Loading/empty/error states**: empty HR dossier ("Nog geen documenten"); empty subsidy-report
  period (no time entries in range — shown as zero hours, not an error); clock-in disabled while
  offline, mirroring feature 027's `report-sick` precedent (see Assumptions).
- **Accessibility**: 48pt minimum touch targets on staff-mobile clock buttons; keyboard/focus-ring
  navigation on director-web tables, per `platform-rules.md`.
- **Offline behavior**: staff-mobile has no offline queue anywhere yet (confirmed against feature
  027's actual implementation, not assumed) — clock in/out follows the same online-only pattern as
  027's sick-report action rather than introducing new offline infrastructure.

### Technical Requirements

- **API impact**: endpoints for time-entry clock in/out, list/correct entries, lock/unlock; staff
  document CRUD via GCS signed URLs; a contract-expiry query; a subsidy-report query/CSV export.
- **Data-model impact**: two new tenant-schema tables, `staff_time_entries` and `staff_documents`
  (see Key Entities), each with FKs into existing tenant tables. Any new migration must extend the
  recurring `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` schema-revert-helper
  pattern this pipeline has hit on every migration-adding feature since 012a.
- **Security**: HR documents and dossier management are director-only (sensitive employment
  data). Staff can only clock in/out for themselves — identity resolved server-side from the JWT
  `NameIdentifier` claim, the exact mechanism feature 027's `GetStaffMeQuery` already uses, never
  a client-supplied staff ID. Staff documents use a GCS signed-URL port mirroring
  `IHealthAttachmentStorage`'s shape (content-type + category prefix), consistent with every other
  document/photo port in this codebase.
- **Performance**: the subsidy report aggregates time entries by function/location/period —
  `staff_time_entries` needs an index covering `(location_id, clocked_in_at)` at minimum.
- **Testing**: happy-path clock in/out; immutability after the lock period and director unlock;
  the 60-day contract-expiry boundary; subsidy-report hour aggregation by function; CSV export.

## Clarifications

### Session 2026-07-23

- Q: Is the time-entry lock period director-configurable via a settings UI, or a fixed
  system-wide constant? → A: A fixed system-wide constant (7 days) — no other requirement in this
  feature needs a settings screen, and the director-facing intervention that actually matters
  (FR-007's per-entry unlock) is unaffected either way.
- Q: Does the medewerkersbeleid report evaluate pass/fail against Opgroeien's BKR ratio
  thresholds (1:5 baby, 1:7 mixed, 1:8 toddler), or just display the computed ratios? → A:
  Display computed ratios only, with no pass/fail evaluation. Formally evaluating ratios against
  the versioned, effective-dated ruleset is feature 041's job (BKR 2027 ruleset, not yet built);
  hardcoding thresholds here would fork that logic and drift once 041 ships.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Staff clocks in and out (Priority: P1)

A staff member starts their shift by opening staff-mobile and tapping "Begin dienst"; at the end
of the shift they tap "Einde dienst". The system records accurate worked hours without any manual
time entry.

**Why this priority**: Without this, there is no hours data at all — every other part of this
feature (dossier aside) depends on time entries existing.

**Independent Test**: Can be fully tested by clocking in, then clocking out, on staff-mobile and
confirming a `staff_time_entries` row exists with the correct timestamps and function — delivers
value standalone even before the subsidy report or dossier exist.

**Acceptance Scenarios**:

1. **Given** a staff member with exactly one configured function and no open time entry, **When**
   they tap "Begin dienst", **Then** a new time entry is created with `clocked_in_at` set to now,
   `function` set to their one configured function, and no function picker is shown.
2. **Given** a staff member configured with more than one function, **When** they tap "Begin
   dienst", **Then** a function picker appears and the entry is created only after they select one.
3. **Given** a staff member with an open (not yet clocked out) time entry, **When** they open
   staff-mobile, **Then** they see "Einde dienst" instead of "Begin dienst", and tapping it sets
   `clocked_out_at` to now on that same entry.
4. **Given** a staff member with an open time entry, **When** they attempt to tap "Begin dienst"
   again (e.g. double-tap, or reopening the app), **Then** the system does not create a second
   open entry — it treats the action as already clocked in.
5. **Given** a staff member is offline, **When** they open the clock in/out screen, **Then** the
   action is disabled with a message explaining connectivity is required (mirrors feature 027's
   sick-report screen).

---

### User Story 2 - Director corrects a missed clock-out (Priority: P2)

A staff member forgot to tap "Einde dienst". The director notices the open entry and fills in the
correct `clocked_out_at` from the web admin.

**Why this priority**: A real, expected edge case (explicitly named in the source backlog item)
that the hours data must handle gracefully — without it, the subsidy report and payroll export
would silently carry incomplete/wrong data indefinitely.

**Independent Test**: Can be tested by creating an open time entry, then using the director-web
correction UI to set `clocked_out_at`, and confirming the entry reflects the correction.

**Acceptance Scenarios**:

1. **Given** a time entry with `clocked_out_at = null` and within the lock period, **When** the
   director opens the staff member's time entries and fills in a clock-out time, **Then** the
   entry is updated and the staff member's mobile view no longer shows them as clocked in.
2. **Given** a time entry older than the 7-day lock period, **When** the director attempts to
   edit it, **Then** the edit is rejected until the director explicitly unlocks that entry.
3. **Given** a locked time entry, **When** the director unlocks it and corrects it, **Then** the
   entry remains editable afterward until the director explicitly re-locks it (unlocking does not
   silently re-lock itself).

---

### User Story 3 - Director maintains a staff member's HR dossier (Priority: P2)

A director uploads a staff member's employment contract, a training certificate, and a
qualification document to that person's HR dossier, and can find them again later.

**Why this priority**: The digital-record-keeping half of the feature's value — independent of
time registration, and independently demoable (a director can start uploading documents on day
one even before any staff member has clocked in).

**Independent Test**: Can be tested by uploading a document of each `document_type` to a staff
member's dossier and confirming each is listed, downloadable, and shows its validity dates.

**Acceptance Scenarios**:

1. **Given** a staff member's dossier, **When** the director uploads a document with a title,
   type, and validity dates, **Then** it appears in that staff member's document list with a
   working signed download link.
2. **Given** an employment-contract document with a `valid_until` date, **When** that date is
   within 60 days of today, **Then** the staff member appears in the director dashboard's
   "Personeel — verlopende contracten" block.
3. **Given** a document with no `valid_until` (e.g. most qualification/training records),
   **When** viewing the dossier, **Then** it never appears in the contract-expiry block.
4. **Given** a staff member with no documents yet, **When** the director opens their dossier,
   **Then** an empty state is shown, not an error.

---

### User Story 4 - Director generates the medewerkersbeleid subsidy report (Priority: P1)

A director needs to apply for the 2025 medewerkersbeleid subsidy, which requires proof that the
location met the required child-hours-to-staff-hours ratio by function. The director selects a
location and period and downloads a report showing exactly that.

**Why this priority**: This is the concrete, named regulatory/financial payoff the feature exists
for — the subsidy is real money, and the report is the actual deliverable a director hands to
Opgroeien. Ranked P1 alongside clock in/out because the report is worthless without accurate time
entries, and time entries are worthless (for this purpose) without the report.

**Independent Test**: Can be tested by seeding known attendance records and time entries for a
location/period, generating the report, and confirming the computed ratios match a manual
calculation.

**Acceptance Scenarios**:

1. **Given** a location and a date range with both attendance and time-entry data, **When** the
   director generates the report, **Then** it shows total child-hours, total staff-hours per
   function, and the resulting ratio per function for that location and period.
2. **Given** a date range with no time entries for a location, **When** the director generates the
   report, **Then** it shows zero staff-hours (not an error), and the report is still downloadable.
3. **Given** a generated report, **When** the director downloads it, **Then** they can also export
   the same period's raw hours as a CSV (per staff member) for a payroll system.
4. **Given** an open (not yet clocked-out) time entry inside the selected period, **When** the
   report is generated, **Then** that entry's hours are excluded from the total (an open entry has
   no known duration) and the report does not silently guess an end time.

### Edge Cases

- Staff member forgets to clock out → `clocked_out_at` stays null until a director fills it in
  retroactively (User Story 2); the report excludes open entries rather than guessing (User Story
  4, Scenario 4).
- Staff member works across two groups/locations in one day (e.g. morning in babies, afternoon in
  toddlers) → two separate time entries, each with its own `group_id`/`location_id`.
- Director attempts to correct a time entry older than the lock period without unlocking it first
  → rejected with a clear message naming the lock period.
- A staff document's `valid_until` is in the past (an already-expired contract that was never
  renewed) → still appears in the contract-expiry block (does not require expiry to be strictly in
  the future — an already-lapsed contract is more urgent, not less).
- Two time entries for the same staff member overlap in time (e.g. a correction creates an
  overlap) → the correction UI warns the director before saving; overlapping entries are not
  silently allowed to double-count hours in the subsidy report.
- A staff member is deactivated (offboarded) while they still have an open time entry → the open
  entry is surfaced as needing correction, same as any other open entry; deactivation does not
  auto-close it (an inaccurate auto-close would corrupt the hours record).

## Requirements *(mandatory)*

### Functional Requirements

**Time registration**

- **FR-001**: Staff MUST be able to clock in from staff-mobile, creating a time entry with
  `clocked_in_at` set to the current time and identity resolved server-side from their
  authenticated session (never a client-supplied staff ID).
- **FR-001a**: The system MUST reject a clock-in for a location the staff member has no
  `StaffLocationEligibility` grant for, the same eligibility check every other staff-write path
  in this codebase already enforces (e.g. feature 009's device-location match, feature 012's
  schedule-write check) — a subsidy-hours record at an unauthorized location is exactly the kind
  of integrity gap this feature exists to prevent, not create.
- **FR-002**: Staff MUST be able to clock out from staff-mobile, setting `clocked_out_at` on their
  currently open time entry.
- **FR-003**: The system MUST prevent a staff member from having more than one open (not yet
  clocked out) time entry at a time.
- **FR-004**: Each time entry MUST record a `function` value (one of `kinderbegeleider`,
  `logistiek`, `verantwoordelijke`), a `location_id`, and an optional `group_id`.
- **FR-004a**: When a `group_id` is supplied, the system MUST reject it if that group does not
  belong to the supplied `location_id` — an unvalidated mismatch would misattribute hours to the
  wrong location/group in the subsidy report (FR-017/FR-018), the same integrity concern FR-001a/
  FR-005a address for location/function.
- **FR-005**: When a staff member has more than one configured function, the system MUST prompt
  them to select the function for that clock-in; when they have exactly one, the system MUST skip
  the prompt and use it automatically.
- **FR-005a**: The system MUST reject a clock-in `function` that is not one of the staff member's
  own configured functions (FR-010), regardless of what a client sends — the picker in FR-005 is
  a UX convenience, not the actual integrity boundary; the server enforces it independently, since
  a mis-attributed function directly skews the subsidy report's per-function hour totals (FR-018).
- **FR-006**: Time entries MUST become immutable (uneditable, including their `clocked_out_at`)
  once older than a fixed, system-wide lock period of 7 days (not a director-facing setting — see
  Clarifications).
- **FR-007**: A director MUST be able to unlock an individual locked time entry to correct it.
- **FR-007a**: Every unlock and re-lock action MUST be attributable to the director who performed
  it, with a timestamp — unlocking is the one path that bypasses the immutability control the
  subsidy report's data integrity otherwise relies on, so it must itself be traceable.
- **FR-008**: A director MUST be able to fill in or correct `clocked_out_at` (and other editable
  fields) on any unlocked time entry, including one with a null `clocked_out_at`. A corrected
  `function` value is subject to the same constraint as FR-005a (must be one of the staff
  member's configured functions).
- **FR-009**: The system MUST warn (not silently allow) a director-made correction that would
  create two overlapping time entries for the same staff member.
- **FR-010**: Directors MUST be able to configure, per staff member, which function(s) they may
  clock in under (at least one is required).

**HR dossier**

- **FR-011**: Directors MUST be able to upload a document to a staff member's dossier with a
  title, a `document_type` (`employment_contract`, `amendment`, `qualification`, `training`,
  `other`), and optional `valid_from`/`valid_until` dates.
- **FR-012**: Directors MUST be able to view and download every document in a staff member's
  dossier via a signed URL.
- **FR-012a**: Every document upload and deletion MUST be attributable to the director who
  performed it, with a timestamp — the same traceability requirement as FR-007a, for the same
  reason (sensitive employment records, not just operational data).
- **FR-013**: Access to the HR dossier and all its management actions (upload, view, delete) is
  director-only — staff MUST NOT have dossier access from staff-mobile.
- **FR-014**: The director dashboard MUST show a "Personeel — verlopende contracten" block listing
  every staff member with a document of type `employment_contract` whose `valid_until` is within
  60 days of today (inclusive of already-past dates).
- **FR-015**: The contract-expiry block MUST link each listed staff member to their dossier.

**Medewerkersbeleid subsidy report**

- **FR-016**: Access to the medewerkersbeleid report (on-screen and CSV export) is director-only,
  consistent with FR-013's dossier access scope — staff MUST NOT be able to generate or download
  it. Directors MUST be able to generate the report, scoped to a selected location and date
  range, showing total child-hours, total staff-hours per function, and the resulting
  child-hours-to-staff-hours ratio per function. The report displays computed ratios only — it
  does not evaluate them against Opgroeien's BKR pass/fail thresholds (see Clarifications; that
  evaluation belongs to feature 041's versioned ruleset).
- **FR-017**: Child-hours for the report period MUST be computed from attendance records (feature
  010) as the sum of each present child's checked-in duration at that location within the period.
- **FR-018**: Staff-hours per function for the report period MUST be computed from time entries at
  that location within the period, summing `clocked_out_at - clocked_in_at`, grouped by `function`.
- **FR-019**: A time entry with no `clocked_out_at` (still open) MUST be excluded from the report's
  hour totals rather than estimated.
- **FR-020**: Directors MUST be able to export the same period's hours as a CSV, one row per
  closed time entry (staff member, date, function, duration), suitable for handoff to a payroll
  system to apply its own pay-rate logic.

### Key Entities *(include if feature involves data)*

- **StaffTimeEntry**: One clock-in/clock-out record for a staff member at a location (and
  optionally a group), tagged with the function worked. Belongs to a `StaffProfile`, a `Location`,
  optionally a `Group`. Becomes immutable after the fixed 7-day lock period unless a director
  explicitly unlocks it.
- **StaffDocument**: One HR document (contract, amendment, qualification, training, or other) on a
  staff member's dossier, stored via a GCS signed URL, with optional validity dates. Belongs to a
  `StaffProfile`.
- **Staff function configuration**: The set of function(s) a given staff member may clock in
  under, configured by a director on the staff member's profile — determines whether the
  clock-in function picker is shown (FR-005/FR-010).
- **Time-entry lock period**: A fixed, system-wide 7-day period after which a time entry becomes
  immutable pending an explicit director unlock (FR-006/FR-007) — not a per-tenant setting.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A staff member can clock in or clock out in a single tap, with no typing required,
  in the common case (one configured function).
- **SC-002**: A director can locate any staff member's HR documents in one navigation step from
  the staff list (clicking their row) — the detail screen opens directly on the Dossier tab,
  since documents are the more frequently needed of the two tabs (research.md R9).
- **SC-003**: A director can generate the medewerkersbeleid subsidy report for a chosen
  location/period in under 30 seconds of interaction (select location, select period, generate).
- **SC-004**: 100% of time entries older than the lock period are immutable without an explicit
  director unlock action — verified by a regression test, not manual inspection.
- **SC-005**: The subsidy report's computed staff-hours for a given period exactly matches the sum
  of that period's closed time entries' durations for the selected location, with zero
  discrepancy — verified by a regression test comparing the report's output against directly
  queried time-entry data.

## Assumptions

- **Function configuration is per staff member, on their profile** (a new `StaffTimeEntryFunctions`
  concept), distinct from the existing `QualificationLevel` enum (training level) — confirmed
  during research that no existing field maps to the medewerkersbeleid function categories, so
  this is new, director-managed data, not a reuse of `QualificationLevel`.
- **The time-entry lock period is a fixed 7-day system-wide constant**, not a per-tenant or
  per-location setting (see Clarifications) — nothing in the source backlog item suggests
  payroll/subsidy record-locking needs to vary by organisation or location.
- **Clock in/out requires connectivity** — staff-mobile has no offline queue anywhere yet (feature
  027's sick-report screen is the precedent: disabled while offline, no queued retry), so this
  feature follows the same pattern rather than building new offline infrastructure.
- **"Child-hours" for the subsidy report is computed from feature 010's attendance records** — the
  sum of each present child's checked-in duration at the given location within the period. This is
  the only existing source of child presence duration in the codebase.
- **The CSV hours export is per time entry** (staff member, date, function, duration), giving a
  payroll system the raw data rather than a pre-aggregated total, so it can apply its own pay-rate
  logic — consistent with the backlog item's explicit "out of scope: payroll calculation."
- **Staff-mobile document access is out of scope** — the HR dossier is a director-web-only
  surface; staff do not view their own documents from staff-mobile in this feature (not requested,
  and dossiers may contain sensitive employment data best gated by the existing director-only
  screens).
- **Clock-in location selection mirrors FR-005's function-picker pattern** (found underspecified
  during implementation — the "one tap" flow needs a location, which nothing in the original UX
  Requirements addressed): when the staff member has exactly one `StaffLocationEligibility` grant
  (`GET /api/staff/me`'s existing `eligibleLocationIds`), it's used automatically with no picker;
  when they have more than one, a location picker appears before the action, same ambiguity rule
  FR-005 already establishes for function selection. No `group_id` is captured from this one-tap
  flow — `StaffTimeEntry.GroupId` stays null for staff-mobile-originated entries in this feature,
  consistent with the flow's "quick action" intent; a director can still set it during a
  correction (FR-008) if needed for a specific report.
- **The three medewerkersbeleid function categories are taken as given by the backlog input, not
  independently verified against an official Opgroeien document** — unlike features 015/019/
  033–041 (which the pipeline's standing process explicitly requires citing
  `docs/integrations/opgroeien/` for), 028 is not on that verified-regulatory-contract list. If
  the exact category set or naming turns out to be wrong, correcting it is a data/enum change,
  not a re-architecture — flagged here rather than silently treated as verified fact.
- **Time entries and HR documents are retained indefinitely after a staff member is
  deactivated/offboarded** — this feature does not implement any deletion or archival policy for
  either. Belgian employment law requires retaining staff records for a period after employment
  ends (`workflows.md`'s Government Reporting workflow already notes "5y staff data, clock starts
  at end of employment" as a fact for feature 038's future retention lifecycle to implement); this
  feature deliberately does not build that lifecycle itself, consistent with feature 031's
  precedent of separating "data is stored" (this feature, and 005/009b/013b before it) from "data
  is governed/retained/purged" (a dedicated feature — 038, not yet built). A staff member's open
  time entry at deactivation is still surfaced for director correction per the Edge Cases section
  below; deactivation itself neither deletes nor locks it early.
