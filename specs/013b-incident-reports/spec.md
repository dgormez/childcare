# Feature Specification: Incident Reports

**Feature Branch**: `013b-incident-reports`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "Build a digital incident/accident report form. This is a legal
requirement under the Besluit Kwaliteit Kinderopvang — every KDV must record incidents involving
children and keep them on file for inspection."

## Clarifications

### Session 2026-07-12

- Q: `reported_by` accountability — a mandatory PIN-confirmed single identity (like medication's
  `administered_by`) directly contradicts User Story 4's requirement that offline filing always
  works, since PIN verification requires a server round-trip. How should `reported_by` be
  resolved? → A: Resolve it server-side from the room shift register (which caregivers were
  checked in at `occurred_at`), the exact mechanism feature 009 already uses for
  `child_events.recorded_by` — no client-submitted value, no PIN-confirmation step, and no offline
  contradiction (the resolution happens at sync/write time, same as 009). This also means
  `reported_by` is a `UUID[]` (zero, one, or two-plus checked-in caregivers), not the single
  nullable `UUID` the original BACKLOG schema literally specified — the same singular-to-array
  correction feature 009 already made for `recorded_by` for the identical reason (device-token
  writes carry no individual caregiver identity, only the checked-in set).
- Q: Does editing a report within the 24-hour window (Story 3/FR-007) reset its
  reviewed/unreviewed indicator back to unreviewed? → A: No. `reviewed` tracks "a director has
  opened this incident at least once," independent of subsequent edits — resetting it on every
  minor correction would create repeated re-flagging noise for something the director has already
  seen exists, working against the indicator's actual purpose (surfacing genuinely new incidents).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver files an incident report on the spot (Priority: P1)

A child scrapes a knee on the playground. The caregiver opens the child's profile on the tablet,
taps "Incident melden," and fills in a short form: what happened, injury type (selected from
tappable chips — scrape, bump, cut, fall, bite, burn, allergic reaction, other, or none), whether
first aid was given, and whether/how the parent was notified. They submit. The report is saved as
a permanent record tied to that child.

**Why this priority**: This is the legal record-keeping requirement itself — without it, nothing
else in this feature has anything to review, export, or notify about. It is the entire MVP.

**Independent Test**: Can be fully tested by a caregiver opening a child's profile, tapping
"Incident melden," completing the required fields (description, injury type), submitting, and
verifying a new incident record exists for that child with the entered details and the caregiver
as `reported_by`.

**Acceptance Scenarios**:

1. **Given** a caregiver is viewing a child's profile on the tablet, **When** they tap "Incident
   melden," **Then** a form opens pre-filled with the child's name and the current date/time as
   `occurred_at`.
2. **Given** the caregiver has entered a description and selected an injury type, **When** they
   submit, **Then** the incident report is created, linked to the child, with `reported_by`
   resolved server-side from the room shift register (feature 008a) — the caregiver(s) checked in
   at `occurred_at` — and `created_at` set to the actual submission time.
3. **Given** the caregiver leaves the description empty or does not select an injury type, **When**
   they attempt to submit, **Then** submission is blocked with a validation message naming the
   missing field(s).
4. **Given** the caregiver marks "doctor called," **When** they submit, **Then** a doctor-notes
   field becomes available (optional) for any relevant detail.
5. **Given** the caregiver selects injury type "none," **When** they also fill in first-aid,
   doctor, or parent-notification fields, **Then** submission succeeds unchanged — those fields
   are independently optional regardless of injury type (e.g. a mild allergic reaction with no
   visible injury can still involve first aid).

---

### User Story 2 - Director reviews and exports incident reports (Priority: P1)

A director opens a dedicated Incidents screen in the web admin. They see every incident filed
across all locations, newest first, with an "unreviewed" indicator on any incident they haven't
yet opened. They filter by date range, location, or a specific child, open one to see full detail,
mark it reviewed, and export it as a PDF formatted for handing to an inspector.

**Why this priority**: The record only has legal/operational value if a director can actually find,
review, and produce it — ships alongside Story 1 as the other required half of the MVP.

**Independent Test**: Can be fully tested by filing two incident reports (different children,
different locations) and verifying a director can see both in the list, filter down to one by child
or date range, and export a PDF containing all of that incident's fields.

**Acceptance Scenarios**:

1. **Given** at least one incident report exists, **When** a director opens the Incidents screen,
   **Then** they see a table of all incidents across their organisation's locations, sorted newest
   first, each row showing child name, location, occurred-at time, injury type, and a
   reviewed/unreviewed state.
2. **Given** the director applies a date-range filter, a location filter, or a child filter (any
   combination), **When** the filters are applied, **Then** only matching incidents are shown.
3. **Given** the director opens an incident's detail view, **When** the view loads, **Then** the
   incident is marked reviewed (its unreviewed indicator clears) and every field is shown, including
   `follow_up` notes.
4. **Given** the director is viewing an incident's detail, **When** they click "Export PDF," **Then**
   a PDF downloads containing every field, the KDV's name/address/Opgroeien identifier, and a
   signature line for the reporting caregiver.
5. **Given** a child has since been deactivated, **When** a director applies that child as the
   Incidents screen's child filter, **Then** all of that child's incident reports still appear,
   unaffected by the child's deactivated status.

---

### User Story 3 - Director adds a follow-up note without altering the original record (Priority: P2)

A day after an incident, the director learns the child needed a follow-up doctor visit. They open
the incident and add a follow-up note. The original description, injury type, and every other
originally-submitted field remain exactly as the caregiver entered them.

**Why this priority**: Legal-document integrity is a hard constraint of this feature, but it's
secondary to the record existing and being reviewable at all (Stories 1–2). Still required for the
feature to satisfy its own immutability guarantee in practice, not just in theory.

**Independent Test**: Can be fully tested by waiting past (or simulating past) the 24-hour window
on an existing incident, attempting to edit its description via the API, verifying rejection, then
adding a follow-up note via the dedicated follow-up field and verifying it saves while the original
description is untouched.

**Acceptance Scenarios**:

1. **Given** an incident report is more than 24 hours old, **When** a director attempts to change
   its description, injury type, first-aid, doctor, or parent-notification fields, **Then** the
   system rejects the change.
2. **Given** an incident report is more than 24 hours old, **When** a director adds or updates a
   `follow_up` note, **Then** the note saves successfully and no other field changes.
3. **Given** an incident report is less than 24 hours old, **When** the reporting caregiver or a
   director edits any field, **Then** the edit is accepted and `updated_at` is set.

---

### User Story 4 - Incident filed offline is captured and synced without loss (Priority: P2)

A caregiver's tablet has no network connection when a minor fall happens. They file the incident
report exactly as they would online; it appears immediately in the local event timeline marked
"pending sync." When connectivity returns, it syncs automatically and becomes visible to the
director.

**Why this priority**: Safety-critical records must not depend on network availability — the same
guarantee feature 009 (child events) already makes for routine daily logs. Secondary to the record
existing at all (Story 1), but required before this feature can be trusted in real KDV conditions.

**Independent Test**: Can be fully tested by disabling network on the caregiver app, filing an
incident report, confirming it appears locally with a pending-sync indicator, then re-enabling
network and confirming it syncs and becomes visible in the director's Incidents screen.

**Acceptance Scenarios**:

1. **Given** the caregiver app has no network connection, **When** a caregiver submits an incident
   report, **Then** it is queued locally, shown immediately in the child's timeline with a
   pending-sync indicator, and no error is shown.
2. **Given** a queued incident report exists, **When** network connectivity returns, **Then** the
   sync engine replays the queued report to the server in submission order, and the pending-sync
   indicator clears once confirmed.
3. **Given** an incident occurred while offline and is filed after the fact once back online, the
   caregiver backdates `occurred_at` to the actual time, **When** they submit, **Then** the report
   is accepted with `occurred_at` earlier than `created_at`, and this discrepancy is visible to the
   director in the detail view (not hidden).

---

### Edge Cases

- A caregiver files a report for a past incident discovered later (e.g. a bruise noticed at pickup
  that nobody saw happen): `occurred_at` can be backdated; the system does not hide the gap between
  `occurred_at` and `created_at` — both are always shown together in the incident's detail view
  (the list view's single "occurred" column is not required to duplicate both timestamps).
- Two caregivers file separate reports for what turns out to be the same incident: no automatic
  merge. A director uses the `follow_up` note on one (or both) to cross-reference the duplicate;
  the two records remain independent, immutable legal documents.
- A child has no parent contact with a registered notification channel: the caregiver records
  `parent_notified_how` as `phone` or `in_person` (out-of-band notification), and the system does
  not attempt any automated parent contact for this feature.
- A child is later deactivated/soft-deleted (feature 006): all of that child's incident reports
  remain fully intact and reachable from the Incidents screen's child filter — never
  cascade-deleted or hidden.
- A caregiver selects injury type "none" (e.g. a behavioral incident or near-miss with no physical
  injury): the report is still valid and required fields are still enforced identically.
- The 24-hour immutability window elapses while a report is only partially filled in offline and not
  yet synced: the clock is measured from the server's recorded `created_at` (the moment it's
  durably stored), not from the device's local clock at data-entry time — a long offline queue delay
  does not shorten the caregiver's effective edit window.
- A director filters the cross-KDV inspection view by a date range spanning thousands of incidents
  across multiple years: the view remains responsive (see Technical Requirements on indexing).
- A director edits/adds a follow-up note, or a caregiver corrects a field within the 24-hour window,
  on a report already marked reviewed: the reviewed state is unaffected by any edit (see
  Clarifications) — it does not revert to unreviewed.
- A caregiver device and a director both submit an edit to the same report within the 24-hour
  window at nearly the same time: the system applies simple last-write-wins semantics (each save
  overwrites the full set of included fields as a unit) — consistent with this codebase's existing
  precedent for low-frequency administrative edits with no cross-actor race history (e.g. feature
  013f's location-settings save); no optimistic-concurrency conflict is surfaced to either actor.
- The caregiver tablet submits a report while online but the request fails for a reason other than
  validation (e.g. a `5xx` server error): this is a normal error, shown to the caregiver with a
  retry option — it is not silently written to the offline queue, since the device knows it has
  connectivity; only an actual network-unreachable condition triggers offline queuing (FR-014).
- A director filters the Incidents screen by a location that has since been deactivated (feature
  004): the location remains selectable in the filter and its historical incident reports remain
  fully reachable — deactivation affects new operational assignment, not historical record access.
- A director exports a PDF for a report with every optional field left blank (only `description`
  and `injury_type` provided): the PDF renders successfully with blank/omitted sections for the
  unset fields, never a rendering error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a caregiver to create an incident report for a child, capturing:
  `occurred_at`, `location_detail`, `description` (required), `injury_type` (required, one of
  `none`/`scrape`/`bump`/`cut`/`fall`/`bite`/`burn`/`allergic_reaction`/`other`), `first_aid_given`,
  `doctor_called`, `doctor_notes`, `parent_notified`, `parent_notified_at`, `parent_notified_how`
  (`phone`/`app`/`in_person`), `witnesses`, and `follow_up`.
- **FR-002**: The system MUST reject creation of an incident report missing `description` or
  `injury_type`, with a validation error identifying the missing field(s).
- **FR-003**: The system MUST default a new report's `occurred_at` to the current time and allow the
  caregiver to change it to an earlier time (backdating), while always retaining the actual
  `created_at` timestamp separately.
- **FR-004**: The system MUST resolve `reported_by` server-side as the set of caregivers checked in
  (feature 008a room shift register) at `occurred_at`, the same mechanism feature 009 uses for
  `child_events.recorded_by` — never a client-submitted value. `reported_by` MUST be stored as zero,
  one, or more caregiver identities (an array), and MUST be empty (not a blocking error) when no one
  was checked in — filing the report must never be blocked by shift-register state.
- **FR-004a**: Unlike the optional medication `administered_by` PIN confirmation, no explicit
  select-then-PIN step is required to file an incident report — accountability here is "who was on
  shift when this happened" (mirroring `recorded_by`), not a single confirmed identity, and requiring
  one would make offline filing (Story 4) impossible since PIN verification needs a server
  round-trip.
- **FR-005**: The system MUST prevent any modification to `description`, `injury_type`,
  `location_detail`, `first_aid_given`, `doctor_called`, `doctor_notes`, `parent_notified`,
  `parent_notified_at`, `parent_notified_how`, or `witnesses` once more than 24 hours have elapsed
  since the report's `created_at`, enforced server-side regardless of client input.
- **FR-006**: The system MUST allow `follow_up` to be added or updated at any time, including after
  the 24-hour immutability window, without affecting any other field or resetting the immutability
  clock.
- **FR-007**: Within the 24-hour window, the system MUST allow any caregiver request from an
  authorized device at the report's location, or any director in the organisation (not scoped to
  the report's location, consistent with FR-009's cross-location director visibility), to edit any
  field of the report. Because the caregiver tablet has no individual-identity check on writes
  (FR-004a), "the reporting caregiver may edit" is enforced as "any caregiver-authenticated request
  within the window," not a match against the specific individual(s) recorded in `reported_by` —
  mirrors feature 009's existing same-day/director edit precedent for child events, which applies
  the same device-scoped (not author-scoped) rule.
- **FR-008**: The system MUST never cascade-delete or hide an incident report when its linked child
  is deactivated/soft-deleted (`Child.DeactivatedAt` set) — the report remains fully retrievable.
- **FR-009**: The system MUST provide a director-facing view listing all incident reports across
  every location in the organisation, filterable by date range, location, and child, sorted newest
  first by default (secondary sort by id for entries sharing the same `occurred_at`, so ordering is
  stable across paginated pages). The list MUST be paginated, defaulting to 25 reports per page.
- **FR-010**: The system MUST track, per incident report, whether a director has opened/reviewed it,
  and MUST visually distinguish unreviewed incidents in the list view described in FR-009 using both
  an icon and a color (never color alone, per design-system.md's badge/banner accessibility rule) —
  this is the mechanism by which a director becomes aware of a newly filed incident (see Assumptions
  for why this substitutes for a push notification).
- **FR-011**: Opening an incident report's detail view MUST mark it reviewed if it was not already.
- **FR-012**: The system MUST generate a PDF export of a single incident report containing every
  field on the record, the location's name, address, and Opgroeien identifier (see Assumptions), and
  a signature line for the reporting caregiver. The PDF MUST render successfully with any unset
  optional field simply omitted or shown blank, never as a rendering error.
- **FR-013**: The system MUST enforce tenant isolation on all incident-report data and endpoints,
  consistent with every other tenant-schema entity in this codebase.
- **FR-014**: The caregiver app MUST support filing an incident report while offline, queuing it
  locally and displaying it immediately (optimistic) with a pending-sync indicator, using the same
  offline-queue/sync-engine mechanism feature 008 built (entity_type = `incident_report`). A
  same-device online submission that fails for a non-validation reason (e.g. a `5xx` response) MUST
  be treated as a normal, retryable error, not silently written to the offline queue.
- **FR-015**: On reconnect, the sync engine MUST replay queued incident reports in creation order,
  and the pending-sync indicator MUST clear once the server confirms persistence.
- **FR-016**: All user-facing strings introduced by this feature — caregiver tablet, director web,
  and the PDF export's own labels (locale-selected via the same `?locale=nl|fr|en` convention as
  feature 007's contract PDF) — MUST use i18n keys covering NL/FR/EN.
- **FR-017**: The system MUST index incident report queries by location and `occurred_at` (at
  minimum) so the cross-KDV inspection view remains responsive against a multi-year accumulation of
  records (thousands of reports per location over several years is the scale this must hold up
  against, not just the small dataset a single KDV accumulates in its first year).
- **FR-018**: The caregiver tablet MUST be able to view any incident report for a child currently
  assigned to that device's location/group — not restricted to reports the device itself filed —
  mirroring feature 008's existing medical-quick-access precedent of location/group-scoped access
  rather than per-report authorship scoping.
- **FR-019**: Every incident report MUST capture the location it was filed at (`location_id`,
  resolved from the filing device's paired location, feature 008a) — this is the field FR-009's
  location filter and FR-017's index operate on, and is additive to the original BACKLOG schema
  (see data-model.md).

### Key Entities

- **Incident Report**: A legal, largely-immutable record of a single safety-relevant event
  involving one child. Belongs to exactly one child (never reassigned, never cascade-deleted) and
  one location (FR-019, the filter/index dimension for FR-009/FR-017). Carries what happened, an
  injury classification, first-aid/doctor/parent-notification details, the caregiver(s) checked in
  when it happened (`reported_by`, zero or more — resolved server-side, not client-submitted), a
  reviewed state (director-facing, sticky across edits), and a follow-up note that remains editable
  after the rest of the record locks.
- **Child** (existing, feature 006): the subject of the incident. Unchanged by this feature except
  as the target of a new one-to-many relationship.
- **Location** (existing, feature 004): supplies the name/address/Opgroeien-identifier fields the
  PDF export needs, and is the filter dimension for the cross-KDV inspection view.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can file a minor, common-case incident report (description + injury-type
  selection only, no optional fields) in under one minute on the tablet, measured from tapping
  "Incident melden" to a confirmed submission.
- **SC-002**: 100% of incident reports remain retrievable and unaltered (aside from `follow_up`)
  more than 24 hours after filing, verified by attempted edits being rejected.
- **SC-003**: 100% of incident reports filed while offline are eventually synced and visible to a
  director with no data loss, once connectivity returns.
- **SC-004**: Given a report already findable via a known filter (child, location, or date), a
  director can go from opening the Incidents screen to the PDF download beginning in under 30
  seconds.
- **SC-005**: 100% of incident reports remain linked to and retrievable for a child even after that
  child is deactivated.

## Assumptions

- **Premise correction — director push notification**: the original brief calls for a "push
  notification to director when a caregiver files an incident report." No director push channel
  exists anywhere in this codebase today — `TenantUser` has no push-token field, and the only
  existing notification mechanism (`Notification` entity, `/api/parent/notifications`) is
  `ParentOnly` (feature 013). Building a first-ever director push channel is out of proportion for
  this feature (the same conclusion feature 013f reached for an analogous "notify director" ask).
  This spec substitutes an in-app unreviewed/reviewed indicator on the Incidents screen (FR-010,
  FR-011) as the mechanism by which a director learns of a new incident — directors are expected to
  check this screen as part of daily operations, the same way they check the existing "Verzoeken"
  queue. `IExpoPushSender` (the generic Expo-push abstraction underneath feature 009's temperature
  alerts) is the port a future director-push feature would reuse, if one is ever built.
- **Premise correction — "in the child file"**: the brief describes incident history living "in the
  child file," but no per-child detail screen exists yet in `web/` (`/children` is still a
  placeholder per feature 007a). Building the full child file is a separate, larger scope than this
  feature needs. This spec instead ships a dedicated Incidents screen (FR-009) with a child filter,
  satisfying "see this child's incident history" without building the child file. Whichever future
  feature builds the real child file should add an incident-history tab there that queries the same
  endpoint this feature exposes.
- **`erkenningsnummer` reconciliation**: feature 004 shipped `Location.Dossiernummer` (nullable
  Opgroeien location identifier) — no field literally named `erkenningsnummer` exists. This spec
  treats `Dossiernummer` as the identifier the PDF export prints; if a location hasn't filled it in
  yet, the PDF prints it blank rather than blocking export.
- **`director_notes`/merge mechanism**: the brief's edge case describes a director "merges [duplicate
  reports] manually via director_notes" — no such field exists in the original schema (`witnesses`
  and `follow_up` are the closest fits). This spec treats `follow_up` as the field a director uses to
  cross-reference a duplicate; no separate `director_notes` field or auto-merge mechanism is built.
- **Reporting-caregiver identity (FR-004/FR-004a)**: resolved per the Clarifications session above
  — `reported_by` mirrors feature 009's `recorded_by` (server-side shift-register resolution, stored
  as an array), not a PIN-confirmed single identity, to avoid contradicting Story 4's offline-filing
  requirement.
- **Signature line**: the PDF's signature line (per the brief) is for the reporting caregiver, not
  the parent — parent e-signature/digital acknowledgment is explicitly out of scope (Phase 2, per
  the original brief).
- Only directors can access the cross-KDV Incidents screen and PDF export (consistent with every
  other cross-location reporting/admin screen in this codebase being `DirectorOnly`); caregivers can
  create reports and see them in a child's own timeline but do not get a cross-KDV view.
- Zorginspectie API integration and parent digital acknowledgment remain out of scope, per the
  original brief.
