# Feature Specification: Child Profile UI

**Feature Branch**: `006a-child-profile-ui`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "Build the general-details tab of the child file. Feature 006
modeled a child's core profile and medical info end to end in the domain/API layer, but no
screen anywhere (web or mobile) lets a director create or edit a child record. Add a 'Profiel'
tab alongside the existing 'Gezondheid' tab (013c), a create-child form, a new
PediatricianName/PediatricianPhone contact pair distinct from the existing GP (huisarts) field,
and extend the caregiver mobile summary to show both GP and pediatrician contact."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web and
caregiver tablet).

### Primary Consumer

Director (primary — creates and edits the full child profile). Caregiver (secondary —
read-only medical/contact summary).

### Workflow Boundary

This feature belongs to the **Child Lifecycle** workflow (`Workflows/child-lifecycle.md`),
specifically the "child profile" item under its ongoing-lifecycle section, which today points
only to feature 006's domain model with no UI documented. This feature adds the first UI surface
for that workflow item. It also touches Health & Safety workflow content (allergies, medical
conditions, GP/pediatrician contacts), but the UI surface itself belongs to Child Lifecycle,
consistent with feature 013c's precedent of extending the same `/children/[id]` screen.

Actors: Director (creates a child record, edits any core-profile or medical-contact field);
Caregiver (views a child's medical/contact summary, read-only).

Actions: Create a child profile; edit a child profile's general details and medical contacts;
view a child's medical/contact summary from the caregiver tablet.

Data Flow: Director web submits a create or edit form to the existing child create/update API
(feature 006), extended with the new pediatrician fields. The child record is read back by both
the director web profile tab and the caregiver tablet's existing medical/dietary summary
screen, which is extended to also show GP and pediatrician contact.

Outputs: A persisted child record, creatable and editable through the web UI for the first time;
a director-facing "Profiel" tab; an extended caregiver-facing medical/contact summary.

Cross-platform Impact: Director web (new create screen, new "Profiel" tab, first tabbed
structure on the child-detail screen) and caregiver tablet (extend an existing read-only
screen). No parent-app or backend-only-only impact beyond the new field and its migration.

### User Impact

This enables a director to create and maintain a child's full core profile — including a
pediatrician contact distinct from the family GP — entirely through the web UI, replacing the
current state where a child record can only be created or fully populated by a direct API call,
test fixture, or seed script.

### UX Requirements

Persona: Director (desktop, `web/`, per `platform-rules.md`'s Director Web section — dense,
keyboard-navigable, 1280px+ desktop-first) for create/edit. Caregiver (tablet, `mobile/`, per
`platform-rules.md`'s Caregiver Tablet section — glanceable, read-only, no typing) for the
summary extension.

Platform: Web (primary — new screens) and mobile (secondary — extend one existing screen).

User job: "I need to register a new child in the system with their core profile and medical
contacts" (director). "I need to see a child's GP and pediatrician contact during a medical
situation" (caregiver).

Success criteria: A director can create a child from zero via the web UI without any other
tool. A director can edit any core-profile or medical-contact field from the child's "Profiel"
tab. A caregiver can see both GP and pediatrician contact on the existing child summary screen.

Main flow (director): `/children` list → "New child" action → create form (name, date of
birth required; gender, nationality, and every medical/contact field optional, per FR-003's
precedent from feature 006 that medical info never blocks child creation) → save → redirected to
`/children/[id]`'s "Profiel" tab → edit any field → save.

Main flow (caregiver): existing child screen → medical/dietary summary section → GP and
pediatrician contact both shown (today only GP is modeled, and even that isn't yet displayed on
this screen).

Loading/empty/error states: Per `design-system.md` — inline `danger`-colored validation on
invalid fields, no blocking modal errors, standard loading skeleton per existing web
conventions.

Accessibility: Web forms fully keyboard-navigable with visible focus rings, per
`platform-rules.md`'s Director Web section. No new interactive element is added on the caregiver
tablet (this is a read-only summary extension), so no new touch-target consideration applies.

Offline behavior: Director web create/edit assumes network access for all writes — not
required to work offline, consistent with every other director-web feature to date (007a, 011,
012, 012a). The caregiver summary extension follows the existing offline-cache-fallback pattern
already used on this screen (feature 013c's cache-fallback service) — no new offline-queue write
path, since this is a read-only addition.

### Technical Requirements

API impact: Extend the existing child create/update endpoints (feature 006, `DirectorOnly`) to
accept and return two new optional fields, `PediatricianName` and `PediatricianPhone`.

Data-model impact: `PediatricianName` (nullable string) and `PediatricianPhone` (nullable
string) added to the child record, mirroring the existing `GpName`/`GpPhone` pair. Requires a
migration — generate the SQL script and run it manually; no auto-migrate in production (per
this repo's conventions).

Security considerations: Create/edit restricted to the director role, consistent with feature
006's existing authorization. Caregiver access remains read-only, scoped exactly the way the
existing medical/dietary summary is scoped today — no new access rule is introduced.

Performance considerations: None beyond existing patterns — single-record form/detail
operations, not a list or bulk operation.

Testing requirements: Backend tests (happy path plus key negative flows) for the extended
create/update handlers. Web component tests for the create form and the new "Profiel" tab.
Mobile test for the extended summary's read path, including its offline-cache-fallback branch.

## Clarifications

### Session 2026-07-13

- Q: Feature 006 already ships a working `Child` domain entity, create/update commands, and
  `DirectorOnly` endpoints — this feature's own BACKLOG framing describes it as "build the
  general-details tab," which reads as a UI-only feature. Does this feature rebuild any of that
  backend capability, or only extend it? → A: Only extend it. The create/update commands,
  entity, and authorization already exist and are reused as-is; this feature adds exactly one
  new field pair (`PediatricianName`/`PediatricianPhone`) to that existing surface and builds
  the UI that has never existed for any of it, web or mobile.
- Q: The existing 012a waiting-list-to-enrollment conversion flow already calls the create-child
  command, but only populates first name, last name, and date of birth — every other field
  (gender, nationality, allergies, medical conditions, GP, insurance number, kindcode) is passed
  as null. Is fixing that gap in scope for this feature? → A: No. That gap predates this feature
  and is a property of the 012a conversion flow, not the create/edit capability this feature is
  building. A director can already reach the newly created child via this feature's "Profiel"
  tab and fill in the missing fields after conversion — no change to the conversion flow itself
  is needed or in scope.
- Q: The child-detail screen currently has no tab structure at all — feature 013c shipped a
  single "Gezondheid" section, not an actual tab component. Does this feature build the first
  tabbed-navigation shell for that screen? → A: Yes. This feature introduces the tab structure
  itself (with "Profiel" and "Gezondheid" as the first two tabs), since it's the first feature
  to need more than one section on this screen — 013c's own shipped-notes flagged this screen as
  the one future per-child-tab work should extend, and this is that extension.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director creates a new child profile (Priority: P1)

A director opens the child list, starts a new child record, enters the child's name and date of
birth, optionally fills in gender, nationality, a profile photo, allergy/medical/dietary
information, GP and pediatrician contacts, health insurance number, and kindcode, then saves.
This is the foundational capability — without it, no child record can be created through the UI
at all, and every other story in this feature operates on a record that doesn't yet exist.

**Why this priority**: Today there is no UI path to create a child record outside of a direct
API call, seed script, or the waiting-list conversion flow (which leaves every field but name
and date of birth null). Nothing else in this feature is reachable without this capability.

**Independent Test**: Can be fully tested by a director creating a child record with only the
required fields (name, date of birth) and confirming it saves and appears in the child list,
independent of editing or the mobile summary.

**Acceptance Scenarios**:

1. **Given** a director on the child list screen, **When** they start a new child record and
   enter only first name, last name, and date of birth, **Then** the record is created
   successfully and appears in the child list.
2. **Given** a director creating a new child record, **When** they also fill in gender,
   nationality, allergy information, medical conditions, dietary restrictions, GP name/phone,
   pediatrician name/phone, health insurance number, and kindcode, **Then** all fields are saved
   and visible on the resulting child record.
3. **Given** a director creating a new child record, **When** they omit every optional field,
   **Then** the record still saves successfully — no medical or contact field blocks creation.
4. **Given** a director creating a new child record, **When** they omit a required field (first
   name, last name, or date of birth), **Then** the system shows inline validation and does not
   save the record.
5. **Given** a successful child creation, **When** the save completes, **Then** the director is
   taken directly to that child's "Profiel" tab.

---

### User Story 2 - Director edits a child's general profile and medical contacts (Priority: P1)

A director opens an existing child's file, goes to the "Profiel" tab, and edits any core-profile
or medical-contact field — including adding or changing the pediatrician contact independently
of the GP contact.

**Why this priority**: Equally foundational to creation — a child's details change over time
(new pediatrician, updated allergy information, a photo added after enrollment), and without
edit capability the create form would only ever be useful once per child.

**Independent Test**: Can be fully tested by opening an existing child's "Profiel" tab, changing
several fields, saving, and confirming the changes persist on reload — independent of the
creation flow or the mobile summary.

**Acceptance Scenarios**:

1. **Given** a director on a child's "Profiel" tab, **When** they edit the pediatrician name and
   phone and save, **Then** the updated values persist and are shown on reload, and the existing
   GP name/phone are unaffected.
2. **Given** a child record with a GP contact but no pediatrician contact, **When** the director
   views the "Profiel" tab, **Then** the pediatrician fields are shown as empty (not blocked,
   not defaulted from the GP fields).
3. **Given** a child record with a pediatrician contact but no GP contact, **When** the director
   views the "Profiel" tab, **Then** the GP fields are shown as empty and saving succeeds without
   requiring a GP value.
4. **Given** a director editing a child's pediatrician contact, **When** they clear both fields
   and save, **Then** the pediatrician contact is removed (set to empty) without affecting any
   other field.
5. **Given** a director on a child's "Profiel" tab, **When** they attempt to clear a required
   field (first name, last name, or date of birth), **Then** the system shows inline validation
   and does not save.

---

### User Story 3 - Caregiver views a child's GP and pediatrician contact (Priority: P2)

A caregiver on the tablet opens a child's screen during a medical situation and sees both the
GP and pediatrician contact alongside the existing allergy, medical condition, and dietary
information.

**Why this priority**: Valuable and directly motivated by this feature's origin (a caregiver
needs the pediatrician contact on-site), but depends on User Story 2 having a way to enter that
data first, and the caregiver tablet is a read-only consumer, not the primary data-entry
surface.

**Independent Test**: Can be fully tested by viewing the caregiver child screen for a child with
both GP and pediatrician contacts set, and separately for a child with neither set, confirming
the summary reflects each case correctly — independent of the director web screens.

**Acceptance Scenarios**:

1. **Given** a child with both a GP and a pediatrician contact set, **When** a caregiver views
   that child's screen, **Then** both contacts are shown, visually and linguistically distinct
   from each other.
2. **Given** a child with a pediatrician contact but no GP contact (or vice versa), **When** a
   caregiver views that child's screen, **Then** only the populated contact is shown — no
   placeholder or error for the missing one.
3. **Given** a child with neither contact set, **When** a caregiver views that child's screen,
   **Then** the existing allergy/medical/dietary summary is shown unchanged, with no GP/
   pediatrician section rendered as broken or empty-with-error.
4. **Given** the caregiver tablet is offline and a cached summary exists for a previously viewed
   child, **When** a caregiver reopens that child's screen, **Then** the cached GP/pediatrician
   contact (if previously loaded) is shown via the existing offline-cache-fallback behavior.

---

### Edge Cases

- A child has a pediatrician contact but no GP contact, or vice versa — both fields are
  independently optional; no "at least one" validation exists (US2, scenarios 2–3).
- A family switches pediatricians — the director overwrites the field in place; no history is
  kept, matching the existing GP field's behavior today.
- A child created through the 012a waiting-list conversion flow has every field but name and
  date of birth null — this feature's "Profiel" tab is how a director fills in the rest after
  conversion; the conversion flow itself is unchanged (see Clarifications).
- A director creates a child record, then immediately deactivates it (existing feature 006
  capability) — the "Profiel" tab remains viewable and editable; feature 006 places no guard on
  editing a deactivated child's fields today, and this feature does not add one.
- Two directors edit the same child's "Profiel" tab concurrently — the system does not detect
  or reject the conflict; the later save fully overwrites the fields from the earlier one
  (last-write-wins), matching the existing full-record-replace semantics of feature 006's update
  operation and, per Constitution/precedent, an acceptable simplification for a low-frequency
  administrative action with no legal/compliance stakes (see feature 012a's identical precedent
  for waiting-list priority reordering).
- A director attempts to save a profile-photo-only change with every other field left as-is —
  the save succeeds and no other field is altered (full-form save, not a photo-only endpoint).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to create a new child record from the web UI, specifying
  first name, last name, and date of birth as required fields.
- **FR-002**: Gender, nationality, profile photo, allergy description and severity, medical
  conditions, dietary restrictions, GP name, GP phone, pediatrician name, pediatrician phone,
  health insurance number, and kindcode MUST all be optional at creation — none of them may
  block a child record from being created or saved.
- **FR-003**: On successful creation, the system MUST take the director directly to the new
  child's "Profiel" tab.
- **FR-004**: Directors MUST be able to view and edit every field listed in FR-002, plus first
  name, last name, and date of birth, from a "Profiel" tab on the existing child-detail screen.
- **FR-005**: The child-detail screen MUST present "Profiel" and the existing "Gezondheid"
  (feature 013c) content as separate, navigable tabs on the same screen.
- **FR-006**: The system MUST support a `PediatricianName` and a `PediatricianPhone` field on
  the child record, both optional, independent of the existing `GpName`/`GpPhone` fields — a
  child MAY have one, both, or neither pair populated, with no cross-field validation between
  them.
- **FR-007**: Editing or clearing the pediatrician contact MUST NOT affect the GP contact, and
  vice versa.
- **FR-008**: All user-facing labels distinguishing the two medical contacts MUST use distinct
  i18n keys per locale (NL: "Huisarts" for GP vs. "Kinderarts"/"Pediater" for pediatrician; FR:
  the existing GP label vs. "Pédiatre"; EN: "GP"/"General practitioner" vs. "Pediatrician") — no
  hardcoded strings.
- **FR-009**: Create and edit operations on a child record MUST remain restricted to the
  director role, matching feature 006's existing authorization on this data.
- **FR-010**: The caregiver-facing child summary screen MUST display both GP and pediatrician
  contact (name and phone) when populated, alongside the existing allergy, medical condition,
  and dietary information, without requiring either field to be present.
- **FR-011**: The caregiver-facing summary MUST remain read-only — no create or edit affordance
  for any child-profile field is exposed on the caregiver tablet.
- **FR-012**: When the caregiver-facing summary is unavailable due to a network failure, the
  system MUST fall back to the existing cached-summary behavior (feature 013c's
  offline-cache-fallback pattern), applied to the extended field set.
- **FR-013**: Required-field validation (first name, last name, date of birth) MUST be enforced
  on both create and edit, with inline, non-blocking-modal error feedback.
- **FR-014**: The child list screen MUST expose a "New child" action that opens the create flow
  described in FR-001–FR-003.

### Key Entities

- **Child** (extended, feature 006): Adds `PediatricianName` (nullable string) and
  `PediatricianPhone` (nullable string), mirroring the existing `GpName`/`GpPhone` pair. All
  other existing fields (name, date of birth, gender, nationality, profile photo, allergy
  description/severity, medical conditions, dietary restrictions, GP contact, health insurance
  number, kindcode, active/deactivated status) are unchanged by this feature — only their UI
  reachability changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can create a child record with only the required fields in under 1
  minute via the web UI.
- **SC-002**: A director can create a child record populating every optional field (including
  both GP and pediatrician contacts) in a single form submission, with zero fields requiring a
  separate save step.
- **SC-003**: 100% of child records with a pediatrician contact set show it on the caregiver
  tablet summary, visually distinct from the GP contact.
- **SC-004**: 100% of attempts to save a child record missing a required field (first name, last
  name, date of birth) are rejected with inline validation before any write occurs.
- **SC-005**: A director can navigate between the "Profiel" and "Gezondheid" tabs on a child's
  detail screen without a full page reload.

## Assumptions

- The child create/update commands, the underlying `Child` entity, and the `DirectorOnly`
  authorization policy already exist (feature 006) and are extended, not rebuilt — this feature
  adds one new field pair and the UI that has never existed for any of it (see Clarifications).
- The 012a waiting-list-to-enrollment conversion flow's existing behavior (populating only name
  and date of birth on child creation) is unchanged — a director completes the remaining fields
  afterward via this feature's "Profiel" tab (see Clarifications and Edge Cases).
- Pediatrician name/phone are ordinary contact fields, not GDPR special-category data on their
  own, but inherit the existing access scoping of the section they sit within (director
  create/edit, caregiver read-only) rather than a new, separate access rule.
- No field history/audit trail is introduced for the pediatrician (or any other) field — an edit
  overwrites in place, matching the existing GP field's behavior.
- The "Profiel" tab is the first tabbed-navigation structure on the child-detail screen; this
  feature builds that shell (see Clarifications), scoped to exactly two tabs — "Profiel" and the
  existing "Gezondheid" content. Additional tabs (contacts, contracts, groups) remain out of
  scope, per the BACKLOG item's own scope boundary.
- A profile photo upload uses the existing photo-upload mechanism from feature 006
  (`RequestChildPhotoUploadUrlCommand`) rather than a new upload path invented by this feature.
