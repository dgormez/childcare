# Feature Specification: Vaccine Catalog & Attachments

**Feature Branch**: `013g-vaccine-catalog`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "Add a shared, admin-maintained vaccine catalog to back feature
013c's vaccineName field, plus attachment support on VaccineRecord itself so a director can
attach a photo/scan of the child's paper vaccination booklet (vaccinatieboekje) as a fallback
when a KDV's own record has fallen behind."

## Clarifications

### Session 2026-07-13

- Q: What file types and size limit should a vaccine-record attachment accept? → A: Reuse
  013c's exact HealthRecord attachment constraint (PDF/JPEG/PNG, max 10MB), since this feature
  reuses that same attachment infrastructure — no reason for a divergent limit on what is
  functionally the same kind of upload.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director picks a vaccine from a shared catalog instead of free-typing (Priority: P1)

A director adding a vaccine record for a child sees a searchable list of standard vaccines
(grouped by category — mandatory Vlaamse basisvaccinatieschema vs. recommended-but-not-free)
instead of a blank text box. Picking an entry auto-fills the vaccine name so the "due soon"
reminder (013c) has reliable, consistently-spelled data to match against.

**Why this priority**: This is the foundational gap 013c shipped with — free text with no
catalog means the due-soon reminder only works if every director spells every vaccine name
identically every time. Without this, nothing else in this feature has a catalog to draw from.

**Independent Test**: Can be fully tested by opening the vaccine record form for a child, seeing
the catalog entries grouped by category, selecting one, and confirming the vaccine name field
auto-fills and the record saves with a reference back to that catalog entry.

**Acceptance Scenarios**:

1. **Given** the vaccine catalog is seeded, **When** a director opens the "add vaccine record"
   form, **Then** they see catalog entries grouped by category (e.g. "Basisvaccinatieschema",
   "Aanbevolen, niet gratis").
2. **Given** a director is adding a vaccine record, **When** they select a catalog entry,
   **Then** the vaccine name field auto-fills with that entry's name and the saved record
   references the catalog entry.
3. **Given** a catalog entry was later deactivated by the platform operator, **When** a director
   views an existing record that referenced it, **Then** the record still displays its
   originally-saved vaccine name correctly (no error, no blank field).

---

### User Story 2 - Director's custom vaccine entry is remembered for next time (Priority: P1)

A director needs to record a vaccine that isn't in the standard catalog (a travel vaccine, a
regional program not covered by the seeded list). They type the name once; from then on, that
name appears as a selectable option for every other child in their KDV — they never have to type
it again.

**Why this priority**: Without this, typing a missing vaccine name is a dead end that has to be
repeated for every child, every time — the exact repetitive-typing problem the catalog exists to
solve. This is equally foundational to User Story 1, not a nice-to-have on top of it.

**Independent Test**: Can be fully tested by typing a vaccine name that matches nothing in the
catalog, saving the record, then opening the form for a different child in the same KDV and
confirming that name now appears as a selectable "used before" option.

**Acceptance Scenarios**:

1. **Given** a director types a vaccine name matching no catalog entry, **When** they save the
   record, **Then** the name is stored as a remembered custom entry scoped to their own KDV only.
2. **Given** a remembered custom entry already exists for a KDV, **When** any director at that
   same KDV opens the vaccine record form for any child, **Then** that entry appears in a
   separate "Other (used before)" group in the picker.
3. **Given** one director already recorded "Rabiës" as a custom entry, **When** a different
   director at the same KDV types "rabies " (different case, spacing, and accent) for a new
   record, **Then** the system treats it as the same remembered entry rather than creating a
   near-duplicate.
4. **Given** a custom entry was recorded at one KDV, **When** staff at a different KDV open the
   vaccine record form, **Then** that custom entry is never visible to them (tenant-scoped).

---

### User Story 3 - Director attaches a photo of the paper vaccination booklet (Priority: P2)

After saving a vaccine record, a director can attach a photo or scan of the child's paper
vaccinatieboekje as a fallback source of truth, since parents — the actual holders of the
authoritative record — don't reliably keep the KDV's typed data current.

**Why this priority**: Valuable and explicitly requested, but the catalog/custom-entry data
quality problem (User Stories 1-2) is the more foundational gap; a record with accurate data and
no photo is still useful, while a photo attached to an unreliable record is not.

**Independent Test**: Can be fully tested by saving a vaccine record, then separately uploading an
attachment to it, and confirming the attachment is retrievable via a signed URL without needing
to re-save the record.

**Acceptance Scenarios**:

1. **Given** a saved vaccine record, **When** a director uploads a photo attachment, **Then** the
   attachment is stored and retrievable via a signed URL, without modifying the record's other
   fields.
2. **Given** a director is uploading an attachment, **When** the upload fails (network error,
   unsupported file), **Then** the vaccine record itself remains saved and unaffected, and the
   director can retry the upload separately.

---

### Edge Cases

- A catalog entry is renamed by the platform operator after several vaccine records already
  reference it. Existing records keep their originally-saved vaccine name (denormalised at
  creation time) — the rename only affects what a director sees when picking that entry going
  forward, never past records.
- A director picks a catalog entry, then edits the auto-filled name before saving. The record
  saves with the edited text but keeps its reference to the originally-picked catalog entry (see
  FR-004) — editing text does not silently detach the reference.
- The catalog is empty or fails to load (should not happen post-seed). The picker degrades to a
  plain free-text field rather than blocking record creation entirely.
- Attachment upload fails after the record was already saved — the record is never rolled back
  or blocked (see User Story 3, Scenario 2). Because the attachment slot is reserved at the time
  the upload is requested (so a retry overwrites the same slot instead of accumulating orphaned
  files), a record can transiently point at an attachment slot with no file in it yet if the
  client-side upload itself never completes — this is not an error state; the record continues to
  behave exactly as "no attachment" until a file actually lands in that slot.
- A KDV has zero remembered custom entries yet (new tenant, or one that has only ever used
  catalog entries) — the "Other (used before)" group simply doesn't appear, rather than showing
  empty.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a shared, platform-wide vaccine catalog (name, category,
  display order, active/inactive status) that is identical across every KDV tenant — never
  tenant-specific data.
- **FR-002**: System MUST seed the catalog with the Vlaamse Departement Zorg
  basisvaccinatieschema for children 0-14 (DTP-Polio-Hib-HepB combination, Pneumococcal, MMR,
  Meningococcal ACWY, HPV) plus the recommended-but-not-free set relevant to daycare-age children
  (RSV immunisation for infants, Meningococcal B, Hepatitis A, chickenpox). Adult-only vaccines
  are out of scope.
- **FR-003**: System MUST let a director select a catalog entry when recording a vaccine, which
  auto-fills the record's vaccine name.
- **FR-004**: System MUST allow a director to edit the auto-filled vaccine name text after
  selecting a catalog entry, and MUST preserve — never silently clear — the record's reference
  to that catalog entry even when the saved text differs from the entry's current name. A
  record's reference to a catalog entry and its reference to a remembered custom entry (FR-006)
  are mutually exclusive: a given vaccine record resolves to at most one of the two, never both,
  and picking one always replaces (never adds to) the other if a prior selection existed.
- **FR-005**: System MUST allow a director to type a vaccine name that matches no catalog entry
  and still save the record; such a record carries no catalog reference (see FR-006 for what it
  carries instead).
- **FR-006**: System MUST remember a typed-but-uncatalogued vaccine name, scoped to the
  director's own KDV, and offer it as a selectable option for every subsequent vaccine record at
  that same KDV — a director never re-types the same missing vaccine name twice. Every vaccine
  record saved this way carries a reference to that remembered entry (mutually exclusive with a
  catalog reference, per FR-004).
- **FR-007**: System MUST treat remembered custom entries as duplicates within the same KDV, and
  merge them into a single entry, whenever two typed names differ only by letter case, leading/
  trailing/repeated whitespace, or accent marks (e.g. "Rabiës", "rabies ", and "RABIES" all
  resolve to the same remembered entry) — this normalization applies consistently regardless of
  how many directors at that KDV type a variant, including near-simultaneous submissions, so the
  picker never accumulates near-duplicates under any timing.
- **FR-008**: System MUST NOT share a KDV's remembered custom entries with any other KDV.
- **FR-009**: System MUST NOT allow any director — regardless of role or permission level, and
  with no exception carved out anywhere in this feature — to create, rename, reorder, or
  deactivate an entry in the shared platform-wide catalog. This is a hard requirement of this
  feature, not a temporarily-deferred capability: no director-facing code path introduced by this
  feature may write to the catalog under any circumstance (see Assumptions for how the catalog is
  maintained instead).
- **FR-010**: System MUST preserve a vaccine record's originally-saved vaccine name verbatim even
  after the catalog entry it referenced is later deactivated or renamed, and MUST continue to
  return that record from every existing read path (list, detail, due-soon aggregation) with its
  full original field set intact — never omitting the record, blanking its `vaccineName`, or
  raising an error because the referenced catalog entry is no longer active.
- **FR-011**: System MUST allow a director to attach one photo/scan file to a saved vaccine
  record, restricted to PDF, JPEG, or PNG content types and a maximum of 10MB (mirroring 013c's
  HealthRecord attachment constraint), retrievable only via a signed, time-limited URL
  (mirroring the existing health-record attachment pattern).
- **FR-012**: System MUST NOT block or roll back a vaccine record's save if a subsequent
  attachment upload for that record fails, and the record MUST remain fully readable and
  editable with all its other fields intact regardless of attachment outcome; the director can
  retry the upload independently. A record whose attachment slot was reserved but never actually
  received a file MUST be treated identically to a record with no attachment at all when
  displayed (see Edge Cases) — never as an error state.
- **FR-013**: System MUST present the vaccine-name picker to a director as catalog entries
  grouped by category, plus the KDV's own remembered custom entries in a separate group, plus the
  ability to type a new name not in either.
- **FR-014**: Caregivers' existing read-only access to a child's vaccine history (013c) MUST be
  unaffected by this feature — they continue to see the record's vaccine name as plain text, with
  no picker or attachment-upload capability.
- **FR-015**: All director-facing strings introduced by this feature MUST be available in NL,
  FR, and EN.

### Key Entities *(include if feature involves data)*

- **Vaccine Catalog Entry**: A platform-wide, non-tenant-specific reference record representing
  one standard vaccine (name, category, display order, active/inactive). Shared identically
  across every KDV. Never edited by a director.
- **Remembered Custom Vaccine Entry**: A KDV-scoped record of a vaccine name a director has
  typed that didn't match the catalog — created automatically the first time it's used, then
  offered as a picker option for that same KDV going forward. Distinct from the platform-wide
  catalog; never shared across KDVs.
- **Vaccine Record** *(extends 013c)*: Gains an optional reference to the catalog entry (or
  remembered custom entry) the director picked, and an optional attachment (photo/scan of the
  paper vaccinatieboekje). Its existing vaccine-name text field remains the source of truth
  regardless of which reference, if any, it carries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can record a standard vaccine by selecting it from a list in under 10
  seconds, without typing the vaccine name at all.
- **SC-002**: A director never has to type the same non-catalog vaccine name more than once for
  their KDV — the second and every subsequent occurrence is selectable.
- **SC-003**: 100% of vaccine records created via catalog selection use a consistently-spelled
  vaccine name, eliminating spelling-driven mismatches in the "due soon" reminder (013c) for
  catalog-backed vaccines.
- **SC-004**: A director can attach a photo of the paper vaccination booklet to any existing
  vaccine record without needing to re-enter any of that record's other fields.
- **SC-005**: An attachment upload failure never results in a lost or unsaved vaccine record.

## Assumptions

- The shared vaccine catalog is treated as reference data maintained outside any director-facing
  UI in this feature — seeded directly (matching this codebase's existing seed-data pattern), with
  updates made by the platform operator through direct data changes rather than an in-app screen.
  A future, separate backlog item will address an actual platform-admin management surface/role
  if the operator needs one; introducing a first-of-its-kind platform-admin role is explicitly out
  of scope here (confirmed directly with the product owner — see BACKLOG.md's 013g shipped-note
  once implemented).
- Remembered custom vaccine entries live in the tenant schema (KDV-scoped), architecturally
  distinct from the shared, non-tenant catalog — the two are never merged into one table.
- Attachment storage reuses the existing health-record attachment infrastructure (013c) with a
  distinct object-path prefix; no new storage bucket or infrastructure change is required.
- Auto-calculating a vaccine's next due date from the catalog's standard dosing interval is out
  of scope — this feature only adds the picker, the remembered custom entries, and the
  attachment.
- Extracting structured data (OCR) from an uploaded vaccination-booklet photo is out of scope —
  the attachment is a human-readable fallback only.
- Adult (staff) vaccination tracking is out of scope — vaccine records are child-only.
- A platform-admin role/management UI for editing the canonical catalog is out of scope for this
  feature and should be logged as a new BACKLOG.md item during this feature's work.
