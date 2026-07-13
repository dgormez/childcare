# Feature Specification: Vaccine & Health Records

**Feature Branch**: `013c-vaccine-health-records`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "Track each child's vaccination schedule and health records. Belgian
KDVs are legally required to track vaccinations (Vaccinatieboekje) and are expected to flag when
boosters are due. This is also a parental trust signal. Build vaccine_records and health_records
tenant tables, a director-facing 'Gezondheid' tab on the child file, a director dashboard block
for vaccinations due within 30 days, and caregiver read-only quick-access to the health/allergy
summary from the group view."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director records a vaccination (Priority: P1)

A director opens a child's file, goes to the "Gezondheid" tab, and adds a vaccine record: which
vaccine, dose number, the date it was administered, who administered it, and (if known) the next
due date. This is the foundational capability — without it, nothing else in this feature has data
to show.

**Why this priority**: Belgian KDVs are legally required to keep a vaccination record per child.
No other user story is useful until vaccine data can be entered.

**Independent Test**: Can be fully tested by opening a child's Gezondheid tab, adding a vaccine
record with a future `next_due_date`, and confirming it appears in that child's vaccine history
list. Delivers value on its own as a compliant vaccination log, even before the dashboard alert or
caregiver quick-access exist.

**Acceptance Scenarios**:

1. **Given** a child with no vaccine records, **When** the director adds a vaccine record with
   vaccine name, administered date, and administering clinic, **Then** the record appears in the
   child's vaccine history, ordered most-recent-first.
2. **Given** a child with an existing vaccine record, **When** the director edits the record's
   `next_due_date`, **Then** the updated due date is reflected immediately in the child's history
   and in any due-soon aggregation.
3. **Given** a vaccine record with an incorrect entry, **When** the director deletes it, **Then**
   it no longer appears in the child's history (soft-deleted, preserving audit history).

---

### User Story 2 - Director records a detailed health record (Priority: P1)

A director adds a structured health record for a child — an allergy detail, a chronic condition,
a standing medication, a doctor's note, or another category — with a title, description, an
optional validity window, and an optional attachment (e.g. a scanned doctor's letter).

**Why this priority**: Equal in legal/operational importance to vaccine tracking — it is the
structured counterpart to the free-text medical fields already on the child profile (feature 006),
and caregivers depend on it for daily care decisions.

**Independent Test**: Can be fully tested by adding a health record of each `record_type` to a
child's Gezondheid tab and confirming each appears with its title, description, and validity
window, independent of any vaccine data.

**Acceptance Scenarios**:

1. **Given** a child's Gezondheid tab, **When** the director adds a health record of type
   `allergy` with a title and description, **Then** it appears in the child's health record list.
2. **Given** a health record being created, **When** the director attaches a PDF or image,
   **Then** the attachment is stored and retrievable via a signed, expiring URL — never a public
   URL.
3. **Given** an attachment upload that fails (e.g. network error), **When** the director submits
   the rest of the record, **Then** the health record still saves successfully without the
   attachment, and the director can retry adding the attachment afterward.
4. **Given** a health record with `valid_until` in the past, **When** the director views the
   Gezondheid tab, **Then** the record is visibly marked as expired/no-longer-current but remains
   in the list (never auto-hidden).

---

### User Story 3 - Director sees which children have a booster due soon (Priority: P2)

From the director's main dashboard, a "Vaccinations due soon" block lists every child across the
director's locations whose next vaccine `next_due_date` falls within 30 days (including already
overdue ones), without the director needing to open each child's file individually.

**Why this priority**: This is the proactive value-add beyond simple record-keeping — it's what
turns a compliance log into an actual reminder system. Depends on User Story 1 existing first.

**Independent Test**: Can be tested by creating vaccine records with `next_due_date` values at
various offsets (past, within 30 days, beyond 30 days, null) and confirming only the correct
subset appears in the dashboard block, sorted soonest-first.

**Acceptance Scenarios**:

1. **Given** a child with a vaccine record whose `next_due_date` is 10 days from today, **When**
   the director views the dashboard, **Then** that child appears in the "Vaccinations due soon"
   block.
2. **Given** a child with a vaccine record whose `next_due_date` was 5 days ago (overdue, never
   recorded as given), **When** the director views the dashboard, **Then** that child still
   appears in the block, visibly flagged as overdue rather than merely "due."
3. **Given** a child with a vaccine record whose `next_due_date` is 60 days away, **When** the
   director views the dashboard, **Then** that child does not appear in the block.
4. **Given** no children have any upcoming or overdue vaccine due dates, **When** the director
   views the dashboard, **Then** the block shows a calm empty state, not an error or blank gap.

---

### User Story 4 - Caregiver glances at a child's health summary from the group view (Priority: P1)

A caregiver, mid-shift on the tablet, taps a child from the group view and sees that child's
allergy/health summary — including active health records and any due-soon vaccine flags — in one
tap, without navigating into a full child file (which does not exist as a caregiver-facing screen).
This extends the existing medical quick-access sheet built in feature 008.

**Why this priority**: This is the safety-critical, daily-operational reason the data model exists
at all — a caregiver needs to know about an allergy or standing medication before making a care
decision, in seconds, not minutes.

**Independent Test**: Can be tested by giving a child an active health record and a due-soon
vaccine flag, then confirming a caregiver assigned to that child's location sees both in the
quick-access sheet, read-only, with no edit affordance.

**Acceptance Scenarios**:

1. **Given** a child with an active `allergy`-type health record, **When** a caregiver taps that
   child from the group view, **Then** the health/allergy summary sheet shows the record's title
   and description.
2. **Given** a child with no health records or vaccine flags, **When** a caregiver taps that
   child, **Then** the summary sheet shows a calm empty state ("No known health information"),
   not a blank screen.
3. **Given** a caregiver not assigned/eligible for a child's location, **When** they attempt to
   view that child's summary, **Then** access is denied, consistent with feature 008's existing
   location-eligibility scoping.
4. **Given** the tablet is offline, **When** a caregiver taps a child whose summary was already
   loaded during today's session, **Then** the cached summary is still shown.

---

### Edge Cases

- A vaccine's `next_due_date` passes without the vaccination ever being recorded as given. The
  dashboard keeps showing it as overdue indefinitely — it is never auto-dismissed or auto-expired.
- A health record's `valid_until` date passes. The record is not deleted or hidden; it is shown as
  expired so the historical record is preserved.
- A child's contract ends and they leave the organisation (transfer to a different KDV). Their
  vaccine and health records remain in the system for the legal retention period; they are not
  exported or transferred to the new KDV automatically by this feature.
- A director attempts to bulk-export child data or send a data summary email (existing features).
  Vaccine and health record data is excluded from that export/summary by default, since it is
  higher-sensitivity medical data — it can only be included if the director takes an explicit,
  separate action to select it.
- Two directors edit the same vaccine record at nearly the same time. The later write wins (same
  behavior as every other tenant-schema record in this codebase); no record locking is introduced.
- A health record is created with only a title and no attachment. This is valid — attachments are
  always optional.
- A caregiver views a child's summary while offline, but that child was never loaded this session
  (e.g. a new caregiver's first tap of the day, offline from launch). No cached data exists; the
  summary shows a "cannot load — check connection" state rather than a false empty state.
- A director adds a vaccine or health record for a child who has already left the organisation
  (deactivated/soft-deleted). This is allowed — recordkeeping is sometimes completed after
  departure (e.g. finishing paperwork) — the system does not block writes based on the child's
  active/deactivated state, only on the record's own soft-delete state.
- A director records the same vaccine and dose number for a child twice (accidental or
  intentional duplicate). No deduplication is enforced — this mirrors the general absence of
  speculative uniqueness validation elsewhere in this codebase; a director notices and deletes the
  incorrect one via the existing edit/delete capability.
- A director edits a vaccine or health record that another director has, moments earlier,
  soft-deleted. The edit fails with the same not-found response a genuinely nonexistent record
  would produce (a soft-deleted record is not editable) — no special "someone else just deleted
  this" message is required.
- The vaccine/health-record data migration described in this feature's technical design
  (superseding an existing, unused legacy table) runs as a single database migration; a failure
  partway through rolls back automatically along with the whole migration, leaving the prior
  schema state intact — not a multi-step process that could leave data half-migrated.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a director to create a vaccine record for a child, capturing
  at minimum: vaccine name, administered date, and the recording user; dose number, administering
  clinic, next due date, and notes are optional.
- **FR-002**: The system MUST allow a director to edit or delete (soft-delete) an existing vaccine
  record for a child.
- **FR-003**: The system MUST display a child's vaccine records in reverse-chronological order
  (most recently administered first) on that child's Gezondheid tab.
- **FR-004**: The system MUST allow a director to create a health record for a child, capturing a
  record type (allergy, chronic condition, standing medication, doctor's note, or other), a title,
  and a description; validity window (`valid_from`/`valid_until`) and an attachment are optional.
- **FR-005**: The system MUST allow a director to edit or delete (soft-delete) an existing health
  record for a child.
- **FR-006**: The system MUST allow an attachment to be uploaded with a health record, limited to
  PDF, JPEG, or PNG files up to 10MB, stored so that it is retrievable only via a signed,
  time-limited URL — never a publicly accessible URL. The upload control MUST communicate the
  allowed file types/size limit and any rejection reason to the director, not silently fail.
- **FR-007**: The system MUST allow a health record to be saved successfully even if its
  attachment upload fails, and MUST allow the attachment to be added or retried afterward.
- **FR-008**: The system MUST display a child's health records on that child's Gezondheid tab,
  visually distinguishing expired records (past `valid_until`) from current ones without hiding
  either.
- **FR-009**: The system MUST compute, for each child, whether any vaccine record has a
  `next_due_date` within 30 days from today (inclusive of dates already in the past/overdue). A
  `next_due_date` equal to today is classified as due-soon, not yet overdue; a `next_due_date`
  strictly before today is overdue.
- **FR-010**: The system MUST surface all children meeting the FR-009 condition, across every
  location the director manages, in a single dashboard block, sorted by due date (soonest or most
  overdue first), showing every matching child in one scrollable list with no pagination — the
  scale this system operates at (tens to low hundreds of children per tenant) does not warrant it.
- **FR-011**: The dashboard block MUST visually distinguish an overdue vaccine (due date already
  passed) from one that is merely upcoming (due today or within 30 days), using both a color and a
  non-color indicator (icon per the due-soon/overdue distinction). The same overdue-vs-upcoming
  distinction and icon pairing MUST be reused, not reinvented, on the caregiver summary sheet
  (FR-013) for consistency across both surfaces.
- **FR-012**: The system MUST continue showing an overdue vaccine on the dashboard indefinitely
  until a director either records the vaccination as given or explicitly updates/clears the due
  date — it must never auto-dismiss.
- **FR-013**: The system MUST allow a caregiver to view a read-only summary of a child's active
  health records and every due-soon/overdue vaccine flag for that child (not collapsed to a single
  most-urgent one — unlike the dashboard's cross-child collapsing in FR-010, a caregiver is already
  looking at one specific child, so showing all of that child's flags is more useful than hiding
  any) directly within the existing medical quick-access sheet (feature 008) — this feature extends
  that sheet's content, it does not add a further tap or a second sheet.
- **FR-014**: The system MUST NOT allow a caregiver to create, edit, or delete a vaccine record or
  health record — caregiver access to this data is read-only in every context.
- **FR-015**: The system MUST enforce the same location-eligibility check for a caregiver's access
  to a child's vaccine/health summary that feature 008's existing medical quick-access already
  enforces — a caregiver ineligible for a child's location cannot view that child's summary.
- **FR-016**: The system MUST exclude vaccine and health record data — including any health-record
  attachment — from any bulk data export or automated email summary by default; such data may
  only be included if a director takes an explicit, separate action selecting it for that specific
  export/summary. No such export/summary feature exists in this codebase yet (see Assumptions);
  this requirement constrains whichever future feature builds one — the exact form of "explicit,
  separate action" (e.g. an unchecked-by-default checkbox, a distinct named permission) is that
  future feature's own decision to specify, not this one's, since no UI exists yet to specify it
  against.
- **FR-017**: The system MUST retain a child's vaccine and health records after that child's
  contract ends or they leave the organisation, for the applicable legal retention period, and
  MUST NOT automatically transfer or export them to any other organisation.
- **FR-018**: All user-facing text for this feature MUST be provided via i18n keys in Dutch,
  French, and English — no hardcoded strings.
- **FR-019**: When a child has no vaccine records or no health records, the respective section on
  the Gezondheid tab and the caregiver summary sheet MUST show a calm, human-readable empty state
  rather than a blank area.
- **FR-020**: The health-record attachment upload control MUST be operable via keyboard alone and
  MUST announce upload progress, success, and failure to assistive technology (e.g. via an
  `aria-live` region) — not only via a visual-only spinner or color change.

### Key Entities

- **VaccineRecord**: One vaccination event for one child — which vaccine, which dose, when it was
  administered, by/where, when the next dose is expected, and free-text notes. Belongs to exactly
  one child. Many per child over time.
- **HealthRecord**: One structured medical record for a child, distinct from the free-text medical
  fields on the child profile (feature 006) — a categorized, titled, described entry with an
  optional validity window and an optional attachment. Belongs to exactly one child. Many per
  child over time. `doctor_note` is specifically a record originating from a clinical visit
  (letter, diagnosis, referral); `other` is for anything relevant that does not fit `allergy`,
  `chronic_condition`, `medication_standing`, or `doctor_note` (e.g. a non-medical caregiving
  instruction a parent has asked to be on file) — the distinction is source/origin, not severity.
- **Child** (existing, feature 006): The subject of both entities above; unchanged by this
  feature except for gaining these two new associated record types.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can add a new vaccine record or health record for a child in under one
  minute, measured from tapping the "Add" action on the Gezondheid tab to the record appearing in
  the list.
- **SC-002**: A director can identify every child with a vaccination due or overdue within the
  next 30 days without opening a single individual child file.
- **SC-003**: A caregiver can reach a child's health/allergy summary, including any due-soon
  vaccine flag, in one tap from the group view.
- **SC-004**: 100% of vaccine and health record data is absent from any bulk export or automated
  email summary unless a director has explicitly opted that data into a specific export. Verified
  today by a regression test proving no existing serialization/export path includes this data
  (see Assumptions' "Known limitation" note — no such export feature exists yet to test
  end-to-end); the criterion becomes fully end-to-end-testable once one does.
- **SC-005**: A child that leaves the organisation retains 100% of their historical vaccine and
  health records, visible to a director, for the legal retention period.

## Assumptions

- "Legal retention period" for medical records follows the same retention policy as the rest of
  the child's file (feature 006) — this feature does not introduce a separate retention timer or
  automatic deletion job; that remains a policy-level decision outside this feature's scope.
- The Gezondheid tab is a new tab on the child file; per BACKLOG.md's note under feature 007a, the
  web admin's `/children` area does not yet have a per-child detail screen — this feature's
  director-web work is scoped to building only the minimal child-detail screen shell needed to
  host the Gezondheid tab (a name header + the tab itself, nothing else). Any other tab (profile,
  contracts, contacts) is explicitly out of scope and must not be added speculatively — a full
  child-file screen remains a separate, later feature's job.
- The caregiver quick-access sheet's current capacity/layout (feature 008) is assumed able to
  accommodate this feature's additional content without a redesign; this is validated during
  implementation (not re-verified here), and if the sheet is found too cramped, the fix is a
  layout adjustment within the same sheet, not a second sheet or extra tap (per FR-013).
- "Director's locations" for the due-soon dashboard block means every location the signed-in
  director has access to, consistent with how other director-web aggregate views in this codebase
  are scoped.
- Vaccine name and administering clinic are free-text fields in this feature (no fixed vaccine
  catalog/dropdown) — matching the BACKLOG prompt's example list format ("DTP", "MMR", "Hep B",
  etc.) as illustrative, not an enumerated constraint.
- The caregiver quick-access summary extends feature 008's existing medical quick-access sheet
  (allergy/medical-notes) rather than replacing it or building a second, parallel sheet.
- No push notification or in-app alert is sent to a director when a vaccine becomes due or
  overdue — the dashboard block is the sole surfacing mechanism in this feature, consistent with
  the BACKLOG prompt's explicit "Out of scope: automated parent reminder... Phase 2" and the
  absence of any director-push-notification channel elsewhere in this codebase (see
  `Workflows/health-safety.md`'s note on 013b).
- No new parent-facing UI is introduced by this feature — parents do not see vaccine or health
  record data through this feature (consistent with the BACKLOG prompt's scope).
- The attachment upload flow has no separate "confirm attachment received" API step that could
  itself fail after a successful storage upload (a partial-success state) — the record's
  attachment location is established at the moment an upload URL is issued, and the next read
  simply reflects whatever exists at that location (nothing, if the client-side upload never
  completed). This avoids a distinct partial-success failure class rather than needing to handle
  one.
- **Known limitation, by design**: no bulk data export or automated email-summary feature exists
  anywhere in this codebase yet (features 013/020 do not implement one). FR-016's exclusion rule
  has nothing to guard against in production today — it is a forward-looking constraint so that
  whichever future feature builds a bulk export/summary inherits an explicit opt-in requirement
  for this data rather than defaulting to "include everything," mirroring feature 009's temperature
  push-alert precedent (built ahead of its eventual consumer, not yet reachable end-to-end).
