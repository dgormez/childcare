# Feature Specification: ID-Verified Registration

**Feature Branch**: `022-id-verified-registration`

**Created**: 2026-07-20

**Status**: Draft

**Input**: User description: "Streamlined child and parent registration with a director
'identity verified' audit trail. Replaces the original eID card-reader approach: most KDV
children are babies/toddlers who don't have an eID chip, making a card reader largely useless
in this context. Opgroeien and GDPR require the KDV to have verified the identity of each child
and guardian; the director does this in person at drop-in or enrolment and the system records
that it happened and what was shown — no hardware required. Adds verification fields to the
child and contact records, a director-web 'Identiteit bevestigen' section, an optional encrypted
National Register Number field on the child record, and a dashboard badge counting unverified
dossiers."

## Clarifications

### Session 2026-07-20

- Q: User Story 3's acceptance scenarios promise a prior verification "remains retrievable" —
  does that require a visible history panel in the "Identiteit bevestigen" section, or is
  database-level retention (no in-app way to view it) sufficient? → A: Show inline history in the
  same section — current verification first, prior entries in an expandable list. A history a
  director can't actually see doesn't serve the trust/audit purpose the feature exists for, and
  no separate audit-log screen is needed for it.
- Q: Should unverified status be visible per-child (e.g., a badge on the child list row in
  director-web), or is the aggregate count on the admin home sufficient on its own? → A: Add a
  small "Niet geverifieerd" badge on the child list row, matching the existing badge pattern
  already established for other short per-item states (e.g., the allergy indicator on a child
  card, `design-system.md`'s Status Indicators). A count alone tells the director work remains
  but not which child — the badge makes it actionable without leaving the list.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director confirms a child's identity (Priority: P1)

A director meets a child's family in person (at enrolment or a later drop-in) and visually
confirms the child's identity document. The director opens the child's file in the web admin,
selects which document type was shown, optionally adds a short note, and confirms. The system
records who verified it and when.

**Why this priority**: This is the legally required core of the feature — Opgroeien and GDPR
require the KDV to have verified and recorded each child's identity. Without this, the feature
delivers no compliance value.

**Independent Test**: Can be fully tested by opening an unverified child's file, completing the
"Identiteit bevestigen" section, and confirming the record now shows a verified state with the
selected document type, timestamp, and verifying director.

**Acceptance Scenarios**:

1. **Given** a child's file with no identity verification recorded, **When** the director selects
   a document type and confirms, **Then** the record shows `id_verified_at` (current time),
   `id_verified_by` (the confirming director), and the selected `id_document_type`.
2. **Given** a child's file with no identity verification recorded, **When** the director attempts
   to confirm without selecting a document type, **Then** the system blocks the action with a
   validation message and no verification is recorded.
3. **Given** a child's file that already shows a verified identity, **When** any director views
   the file, **Then** the verification section shows the recorded document type, note, verifying
   director, and timestamp as read-only information (not an editable form).
4. **Given** a child was enrolled several months ago with documents only posted afterward,
   **When** the director confirms identity today, **Then** the recorded timestamp is today (the
   moment verification actually happened), not the enrolment date.

---

### User Story 2 - Director confirms a parent/guardian contact's identity (Priority: P1)

The same in-person verification applies to the child's parents/guardians. The director opens a
contact's record and records which identity document was shown, using the same
"Identiteit bevestigen" pattern as the child file.

**Why this priority**: Equally required by the same Opgroeien/GDPR obligation — verifying only
the child and not the accompanying guardian would leave the audit trail incomplete. Independent
of User Story 1 since it operates on a different entity (contact, not child).

**Independent Test**: Can be fully tested by opening an unverified contact's record, completing
the "Identiteit bevestigen" section, and confirming the record now shows a verified state.

**Acceptance Scenarios**:

1. **Given** a contact record with no identity verification recorded, **When** the director
   selects a document type and confirms, **Then** the record shows the verification timestamp,
   verifying director, and selected document type.
2. **Given** a contact linked to more than one child (siblings), **When** the director verifies
   that contact's identity once, **Then** the verification applies to the contact record itself
   and does not need to be repeated per linked child.

---

### User Story 3 - Director corrects or updates a verification record (Priority: P2)

Identity documents change over time — most notably a child turning 12 and receiving a Belgian
eID card. A director needs to update a previously recorded verification without losing the
history of what was originally recorded.

**Why this priority**: Necessary for the audit trail to stay accurate over the life of a
long-running enrolment, but only matters once User Story 1/2 already has a verified record to
correct — a natural second-priority increment.

**Independent Test**: Can be fully tested by updating an already-verified child's document type
and confirming both the current record and the visible change history reflect the update.

**Acceptance Scenarios**:

1. **Given** a child verified with `id_document_type = birth_certificate`, **When** the director
   updates it to `eid` (e.g., the child turned 12) and confirms, **Then** the current record shows
   `eid` with a new `id_verified_at`/`id_verified_by`, and the prior verification (document type,
   verifier, timestamp) remains visible in an expandable history within the same
   "Identiteit bevestigen" section.
2. **Given** an already-verified child or contact record, **When** any director updates the
   verification (document type or note), **Then** the change is attributed to that director and
   timestamped, distinct from the original verification's attribution, and the current
   verification (shown first) reflects the update while the prior entry moves into the history
   list.

---

### User Story 4 - Director sees which dossiers still need verification (Priority: P2)

From the admin home, a director sees a count of enrolled children who have not yet had their
identity verified, so outstanding compliance work is visible without checking every child file
individually.

**Why this priority**: Turns the per-record capability from User Stories 1–3 into an operational
tool the director actually uses day-to-day; not required for the audit trail itself to be legally
valid, so it can ship after the recording capability exists.

**Independent Test**: Can be fully tested by viewing the admin home with a mix of verified and
unverified enrolled children and confirming the badge count matches the unverified ones exactly.

**Acceptance Scenarios**:

1. **Given** 3 actively enrolled children without `id_verified_at` and 5 with it set, **When** the
   director views the admin home, **Then** the "Niet-geverifieerde dossiers" badge shows 3.
2. **Given** an unverified child's enrolment is later deactivated/departs, **When** the director
   views the admin home, **Then** that child no longer counts toward the badge.
3. **Given** the director verifies the last remaining unverified child, **When** the admin home is
   next viewed, **Then** the badge no longer appears (or shows zero).
4. **Given** a child list view in director-web, **When** a child has no recorded identity
   verification, **Then** that child's row shows a small "Niet geverifieerd" badge, so the
   director can identify which specific child needs action directly from the list, not only the
   aggregate count.

---

### User Story 5 - Director records a child's National Register Number (Priority: P3)

A director with access to a child's Belgian National Register Number (rijksregisternummer) can
record it on the child's file for future fiscal reporting use, without it ever being exposed in
plain text again after saving.

**Why this priority**: Optional, forward-looking data capture (explicitly noted as needed for a
Phase 3 fiscal feature) — lowest priority since nothing in this feature's own scope consumes it
yet.

**Independent Test**: Can be fully tested by entering an NRN on a child's file, saving, reloading
the page, and confirming only the last 4 digits are ever shown again.

**Acceptance Scenarios**:

1. **Given** a child's file with no NRN recorded, **When** the director enters a validly-formatted
   NRN and saves, **Then** the file subsequently displays only the last 4 digits (e.g.,
   `•••••••93.71`).
2. **Given** a child's file with an NRN already recorded, **When** the director views the file,
   **Then** the full NRN is never rendered in plain text, and no application log at any level
   contains the plain-text value.
3. **Given** an NRN input that doesn't match the expected Belgian national register number format,
   **When** the director attempts to save, **Then** the system blocks the save with a validation
   message.

---

### Edge Cases

- Director verifies a child's identity months after enrolment (documents posted later): allowed;
  the recorded timestamp is when verification actually happened, not the enrolment date (User
  Story 1, Acceptance Scenario 4).
- A child turns 12 and receives an eID: director updates the document type from
  `birth_certificate` to `eid` without losing the original verification in the change history
  (User Story 3).
- A contact is linked to multiple children (siblings): verifying the contact once covers all
  linked children, since verification lives on the contact record, not the link (User Story 2,
  Acceptance Scenario 2).
- Attempting to confirm verification without a document type selected: blocked, since
  `id_verified_at` and `id_document_type` together form the audit trail and neither is meaningful
  alone.
- A previously-verified record is corrected (wrong document type recorded initially): allowed via
  the same update action as User Story 3; the correction itself becomes part of the traceable
  history, satisfying the anti-tampering intent without needing a role this codebase doesn't have
  (see Assumptions).
- An enrolled child's identity is verified, then the child later departs/is deactivated: the
  historical verification record is retained (it's a legal audit fact), but the child no longer
  contributes to the "unverified dossiers" count since it no longer represents outstanding work.
- NRN entered with an invalid format (wrong length, non-numeric): rejected at save with a
  validation message; no partial/invalid value is ever persisted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a director to record an identity verification on a child's file,
  consisting of a document type (one of: birth certificate, Belgian Kids-ID, Belgian eID,
  passport, other), an optional free-text note, the verifying director, and the verification
  timestamp.
- **FR-002**: System MUST allow a director to record the same shape of identity verification on a
  contact (parent/guardian) record, independent of which children that contact is linked to.
- **FR-003**: System MUST require a document type to be selected before a verification can be
  confirmed; the note is optional. A verification with no document type MUST NOT be recorded.
- **FR-004**: The verification timestamp MUST always be the moment the director confirms the
  action (server time), never a user-editable/backdated value — this holds even when verification
  happens well after enrolment (retroactive verification is allowed in *when* it happens, not in
  *what timestamp* gets recorded).
- **FR-005**: System MUST allow a director to update an already-verified child's or contact's
  document type and/or note (e.g., a child ages into eID eligibility, or an initial entry needs
  correcting). Each update MUST re-set the verifying director and timestamp to the director and
  moment performing the update.
- **FR-006**: System MUST retain a durable, attributable history of every identity-verification
  change (initial verification and every subsequent update) for a child or contact — who made the
  change, what changed, and when — so that a correction is always traceable rather than silently
  overwriting the prior record. This satisfies the compliance need for tamper-evidence without
  introducing an access-control tier this codebase does not otherwise have (see Assumptions).
- **FR-006a**: The verification history MUST be visible in the app, not only persisted — the
  "Identiteit bevestigen" section MUST show the current verification first and prior entries in
  an expandable list, so a director can actually review what changed.
- **FR-007**: System MUST expose a count, visible from the director's admin home, of actively
  enrolled children that do not yet have an identity verification recorded.
- **FR-007a**: System MUST show a per-child indicator (badge) in the director-web child list for
  any actively enrolled child without a recorded identity verification, so an unverified child can
  be identified directly from the list, not only via the aggregate count.
- **FR-008**: The unverified-dossier count MUST exclude children whose enrolment is inactive/
  departed, and MUST update to reflect newly-completed verifications without requiring a page
  reload workaround (i.e., it reflects current state on each view).
- **FR-009**: System MUST allow a director to optionally record a child's National Register Number
  (NRN / rijksregisternummer).
- **FR-010**: System MUST validate that an entered NRN matches the expected Belgian national
  register number format before it can be saved; an invalid format MUST be rejected with a clear
  validation message and MUST NOT be persisted.
- **FR-011**: System MUST encrypt the NRN at rest and MUST NOT include its plain-text value in any
  application log at any level.
- **FR-012**: System MUST NOT render the NRN in plain text in any UI surface once it has been
  saved; only the last 4 digits MAY be displayed, in any subsequent view of the record.
- **FR-013**: All user-facing strings introduced by this feature (labels, validation messages, the
  dashboard badge text) MUST be available in NL, FR, and EN via the existing i18n mechanism.
- **FR-014**: Recording or updating an identity verification, and recording/updating an NRN, MUST
  remain restricted to the Director role, consistent with this codebase's existing child/contact
  write-access model.

### Key Entities

- **Child (extended)**: gains an identity-verification state (document type, note, verifying
  director, verified-at timestamp) and an optional encrypted National Register Number.
- **Contact (extended)**: gains the same identity-verification state as Child — document type,
  note, verifying director, verified-at timestamp. Represents a parent/guardian, independent of
  which children they're linked to.
- **Identity Verification History Entry (new)**: one durable record per verification event (create
  or correction) on a Child or Contact — captures who performed it, what document type/note were
  recorded, and when. Exists to satisfy the anti-tampering/audit requirement (FR-006) without
  restricting who may perform a correction.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can record an identity verification for a child or contact in a single
  form submission (document type selection + confirm), with no more than one optional field
  (note) in between.
- **SC-002**: 100% of identity-verification changes — both the first verification and any later
  correction — are attributable to a specific director and timestamp in a retrievable history.
- **SC-003**: The "unverified dossiers" count shown on the admin home always matches the actual
  count of actively enrolled children without a recorded verification, with no manual refresh or
  recomputation step required by the director.
- **SC-004**: A National Register Number, once saved, is never observable in plain text through
  any part of the product afterward — only its last 4 digits are ever shown again.
- **SC-005**: Directors can complete both a child's and a guardian's identity verification without
  any additional hardware (no card reader, no external device).

## Assumptions

- **No organisation-owner role exists in this codebase** (only Director, Staff, and Parent
  account roles — plus a cross-tenant `IsPlatformAdmin` flag unrelated to any single organisation).
  The originating backlog note called for verification fields to be "editable only by org owner
  to prevent retroactive tampering," but that role doesn't exist anywhere in the system, and the
  same backlog note's own edge case ("a child turns 12... **director** can update
  `id_document_type`") already assumes plain director access, not an owner tier. Resolved by
  satisfying the anti-tampering intent through **traceable history** (FR-006) rather than
  restricting *who* may correct a record: any director may create or update a verification, but
  every such action is permanently attributed and timestamped, so silent retroactive tampering
  isn't possible even though editing itself isn't gated behind a role this codebase doesn't have.
  If a genuine org-owner tier is wanted later, it's a separate cross-cutting access-control
  feature, not something to invent narrowly for this one field set.
- The "unverified dossiers" dashboard badge counts children only (per the backlog description),
  not contacts — a director can identify unverified contacts by visiting each child's file, which
  already surfaces the linked contacts.
- Document type is a free choice by the director (birth certificate / Kids-ID / eID / passport /
  other) with no system-enforced age restriction (e.g., not blocking `eid` for a child under 12)
  — the backlog note's "(12+ years)" annotation is informational precedent, not a validation rule,
  since a hard age gate wasn't requested and would add complexity with no stated need.
- Belgian NRN format validation checks structural shape (11 digits in the standard
  YY.MM.DD-XXX.CC grouping); it does not perform the full checksum/modulo-97 validity check against
  Belgium's national register algorithm, since correctness there isn't required for this feature's
  purpose (recording what a director was shown) and the field's actual regulatory use is deferred
  to the Phase 3 fiscal feature (015/Belcotax 281.86) that will consume it.
- Encryption at rest for the NRN reuses this codebase's existing ASP.NET Core Data Protection
  encryption pattern (already used for third-party OAuth token storage) rather than introducing a
  new encryption mechanism.
- This feature is director-web only; no caregiver-tablet or parent-mobile surface is affected, per
  the backlog note's scope ("web admin child file and contact view").
- eID card-reader hardware integration and automatic NRN lookup against Opgroeien systems are
  explicitly out of scope (Phase 3 at earliest), per the backlog note.
