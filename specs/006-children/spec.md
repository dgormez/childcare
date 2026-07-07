# Feature Specification: Child File Management

**Feature Branch**: `006-children`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "Build the child file — the central record for every child enrolled or on the waiting list at a KDV. Child profile (name, DOB, photo, gender, nationality), medical information (allergies, conditions, dietary restrictions, GP, health insurance), contacts (multiple per child, shared across siblings, roles + can_pickup), group/section assignment with date ranges, kindcode field for Phase 3, vaccine/health record tracking with due-date alerts, soft-delete on departure, locale preference on primary contact."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director creates a child's file with medical information (Priority: P1)

A director (or staff member) creates a new child record: name, date of birth, and whatever medical information is already known (allergies, conditions, dietary restrictions, GP details, health insurance number). The file exists independently of any enrolment contract, so a child on the waiting list already has a complete, usable file.

**Why this priority**: Without a child file, nothing else in the platform has anything to attach to — no contract (007), no daily events (008), no attendance (009). This is the foundation every later feature depends on.

**Independent Test**: Can be fully tested by creating a child record with core profile fields and medical information, and confirming it appears in the organisation's child list with no contract required.

**Acceptance Scenarios**:

1. **Given** a director is logged in, **When** they create a child record with first name, last name, and date of birth, **Then** the child file is created and appears in the organisation's child list, with no contract or group assignment required.
2. **Given** a director creates a child record, **When** they also enter allergy, medical condition, dietary restriction, GP, and health insurance information, **Then** all of it is saved and retrievable from the same file.
3. **Given** a child has one or more allergies or medical conditions recorded, **When** a caregiver views the child from the group view (caregiver app), **Then** the allergy/medical information is available directly alongside the profile — no separate lookup or secondary request is needed to reach it.
4. **Given** a child is on the waiting list with no enrolment contract, **When** the director views the child's file, **Then** the file displays normally, independent of contract status.

---

### User Story 2 - Director manages a child's contacts (Priority: P1)

A director adds one or more contacts to a child's file — mother, father, guardian, emergency contact, or authorised pickup — each with a name, phone, email, relationship, and whether they're allowed to pick the child up. When two children in the same family are both enrolled, the same contact is linked to both children's files rather than duplicated.

**Why this priority**: A child file with no contact information is not usable in practice — caregivers need to know who is allowed to collect a child on day one. This ties for P1 with the core profile.

**Independent Test**: Can be fully tested by adding a contact to one child, then linking that same contact to a second (sibling) child, and confirming both files show the same contact without a duplicate record being created.

**Acceptance Scenarios**:

1. **Given** a child's file exists, **When** a director adds a contact with a relationship and pickup permission, **Then** the contact appears on the child's file with those details.
2. **Given** a contact is already linked to one child, **When** a director links that same contact to a sibling's file, **Then** both children's files show the contact, and no second, duplicate contact record is created.
3. **Given** a child has multiple contacts, **When** a director designates one as the primary contact, **Then** exactly one contact is marked primary, and that contact's locale preference is what the parent app uses for that family's communications.
4. **Given** a child's primary contact changes (e.g. a custody change), **When** the director designates a new primary contact, **Then** the previous primary contact's link to the child is retained (not deleted) — it simply stops being flagged primary, so the change is derivable from the data rather than silently lost.

---

### User Story 3 - Director assigns a child to a group as they grow (Priority: P2)

A director assigns a child to a group or section within a location, with a start date. When the child moves to a new group (e.g. ages up from baby room to toddler room), the director records the new assignment; the prior assignment's end date is set automatically.

**Why this priority**: Needed for day-to-day operations (group view, ratios) but a child's file is already useful without a group during onboarding or while on the waiting list — not a launch blocker.

**Independent Test**: Can be fully tested by assigning a child to a group, then reassigning them to a different group at a later date, and confirming the history shows both assignments with non-overlapping date ranges.

**Acceptance Scenarios**:

1. **Given** a child has no group assignment, **When** a director assigns them to a group with a start date, **Then** the child appears in that group from that date.
2. **Given** a child is currently assigned to a group, **When** a director assigns them to a different group with a new start date, **Then** the prior assignment's end date is set to the day before the new one starts, and the child now appears in the new group.
3. **Given** a child's group-assignment history, **When** it is viewed later, **Then** every prior assignment remains visible with its own date range — it is a history, not a single overwritten value.

---

### User Story 4 - Caregiver/director records vaccine and health-record entries (Priority: P2)

A caregiver or director records a vaccine administered to a child, including the date and (if applicable) when the next dose is due. The system surfaces an alert when a recorded vaccine's next-due date has arrived or passed.

**Why this priority**: Legally and medically important, but a child can be enrolled and cared for before their vaccine history is fully digitised — not a launch blocker.

**Independent Test**: Can be fully tested by recording a vaccine with a future next-due date, confirming no alert shows yet, then recording one with a next-due date in the past and confirming it does.

**Acceptance Scenarios**:

1. **Given** a child's file, **When** a director or caregiver records a vaccine (name, date administered, next due date), **Then** it appears in the child's vaccine history.
2. **Given** a recorded vaccine's next-due date is today or in the past, **When** the child's file or an overview list is viewed, **Then** the vaccine is flagged as due.
3. **Given** a recorded vaccine has no next-due date (a one-time vaccine), **When** the file is viewed, **Then** no due alert is ever shown for it.

---

### User Story 5 - Director deactivates a child who has left (Priority: P3)

A child leaves the KDV permanently. The director deactivates the child's file rather than deleting it, preserving the full history (medical records, contacts, group history, vaccine history) while removing the child from active rosters.

**Why this priority**: Necessary for accurate historical records, but an organisation can operate normally for a long time before its first child ever leaves — lowest priority of the five.

**Independent Test**: Can be fully tested by deactivating a child and confirming they disappear from the active child list while their file (medical info, contacts, history) remains fully retrievable.

**Acceptance Scenarios**:

1. **Given** an active child with no active enrolment contract, **When** a director deactivates the file, **Then** the child is marked inactive, disappears from active child/group lists, and all historical data on the file remains intact and viewable.
2. **Given** a deactivated child, **When** a director reactivates them (e.g. they re-enrol later), **Then** the file becomes active again with its full prior history intact.

---

### Edge Cases

- Two children in the same KDV are siblings and share the same parents as contacts: the data model links one contact record to multiple children rather than duplicating it (User Story 2).
- A child's primary contact changes (e.g. custody change): the previous primary contact's link is retained, just no longer flagged primary — not deleted or silently overwritten (User Story 2, AC4).
- A child's primary contact is unlinked entirely while other contacts remain: the most-recently-linked remaining contact is automatically promoted to primary, so the child is never left with contacts but no primary designation (FR-007).
- A child is on the waiting list (no contract yet) but already has a file: the file exists and is fully usable independently of any contract (User Story 1, AC4).
- A child has zero contacts recorded (e.g. file created in a hurry, contacts added later): the file must still be viewable and functional, showing an empty contacts list rather than an error.
- A child has zero group assignments (e.g. not yet placed): the file must still be viewable, showing no current group rather than an error.
- A director attempts to create a group against a location that has since been deactivated (feature 004): the creation is rejected — a group cannot be newly created against an inactive location, though existing groups/assignments referencing a location that is deactivated *after* the group was created are unaffected (FR-008).
- A director attempts to record a new group assignment with a start date earlier than the child's currently open assignment: rejected — assignments must be entered in chronological order (FR-008a).
- A director attempts to deactivate a child who has an active enrolment contract (feature 007, once it exists): this feature provides the extension point for that guard but registers no guard itself, since no feature yet creates contracts — mirrors the pattern established in features 004/005.
- A child's kindcode is not set (private KDV, Phase 1): the field exists and accepts a value later without requiring one now.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors and staff MUST be able to create a child file consisting of: first name (required, ≤100 characters), last name (required, ≤100 characters), date of birth (required, must not be a future date), profile photo (optional, signed URL only), gender (optional), and nationality (optional).
- **FR-002**: A child file MUST exist and be fully usable independently of any enrolment contract — a child on the waiting list has a complete file from the moment it's created.
- **FR-003**: Directors and staff MUST be able to record and update medical information on a child's file: allergies (free text up to 2000 characters, plus a severity level), medical conditions (free text up to 2000 characters), dietary restrictions (free text up to 2000 characters), GP name and phone, and health insurance number. All medical fields are optional at creation — a file is never blocked on medical information being unavailable yet.
- **FR-004**: The data model MUST make a child's allergy/medical-condition information directly retrievable alongside their core profile in a single request — no separate lookup or nested resource is required to reach it, so a caregiver-facing client can surface it immediately from a group view without a secondary round-trip.
- **FR-005**: The system MUST support linking multiple contacts to a child file, each with a name, phone, email, relationship (mother, father, guardian, emergency contact, or authorised pickup), and a boolean indicating whether that contact is authorised to pick the child up.
- **FR-006**: A single contact record MUST be linkable to more than one child file (siblings sharing the same parents) without duplicating the contact's underlying data — updating the shared contact's details updates it for every linked child.
- **FR-007**: Whenever a child has at least one contact, exactly one of them MUST be designated primary — the first contact added to a child with none is automatically primary, so a director is never required to make a separate "set primary" decision unless they want to change it later. The primary contact's locale preference (NL/FR/EN) is what the parent-facing product uses for that child's family. Changing the primary-contact designation MUST NOT hard-delete or overwrite the prior contact link — the previous primary contact's link to the child remains intact (just no longer flagged primary), so which contact was primary at any past point stays derivable from the data. If the primary contact's link is removed (unlinked) while at least one other contact remains linked, the system MUST automatically promote the most-recently-linked remaining contact to primary, so the "exactly one primary whenever ≥1 contact exists" invariant is never silently violated.
- **FR-008**: Directors MUST be able to create a minimal group/section record (a name, scoped to one of the organisation's locations) for children to be assigned to — this feature does not build full group administration (capacity limits, BKR configuration), only what's needed to name a group and assign children to it (see Assumptions). The referenced location MUST be active at creation time; a group cannot be created against an already-deactivated location.
- **FR-008a**: The system MUST support assigning a child to a group/section with a start date; assigning a new group automatically ends the prior assignment (the day before the new one starts). Group assignments MUST be entered in chronological order — a new assignment's start date must not be earlier than the currently open assignment's start date. The full history of group assignments MUST remain visible, never overwritten.
- **FR-009**: A child file MUST support a nullable `kindcode` field (Opgroeien child identifier, format YYMMDD-NNN) — not required for private KDVs in Phase 1, but the field must exist now so Phase 3 IKT reporting doesn't require a schema change. The format is stored as entered, not validated in Phase 1 — format validation is a Phase 3 IKT concern.
- **FR-010**: The system MUST support recording vaccine/health-record entries per child: vaccine name, date administered (must not be a future date), and an optional next-due date.
- **FR-011**: The system MUST flag a vaccine as due when its recorded next-due date is today or in the past; a vaccine with no next-due date is never flagged.
- **FR-012**: The system MUST support deactivating (soft-delete) a child file rather than permanently deleting it. A deactivated child does not appear in active child/group listings, but every historical record (medical info, contacts, group history, vaccine history) remains intact and retrievable.
- **FR-013**: The system MUST provide an extension point for other features to block child deactivation when the child has an active dependent (e.g., an active enrolment contract, once feature 007 exists). This feature registers no such guard itself, mirroring the pattern established in features 004 (locations) and 005 (staff).
- **FR-014**: Directors and staff MUST be able to reactivate a previously deactivated child file, restoring it to active listings with its full history intact.
- **FR-015**: Profile photos MUST be stored and served exclusively via signed URLs — no public, unsigned image links are ever exposed, reusing the signed-URL mechanism established in feature 005 (staff profile photos).
- **FR-016**: All user-facing strings (labels, validation messages) MUST use i18n keys (NL/FR/EN); no hardcoded text.
- **FR-017**: Child, medical, contact, group-assignment, and vaccine data MUST be scoped to the tenant schema — no cross-tenant child data is ever accessible.

### Key Entities

- **Child**: The central record for an enrolled or waitlisted child — name, date of birth, photo (signed URL, optional), gender (optional), nationality (optional), medical fields (allergies, conditions, dietary restrictions, GP, health insurance), `kindcode` (nullable), and active/deactivated status. Exists independently of any contract.
- **Contact**: A person who may be linked to one or more children — name, phone, email, locale preference. Modelled separately from Child so the same person can be linked to multiple children (siblings) without duplication.
- **Child Contact Link**: A many-to-many association between a Child and a Contact, carrying the relationship type (mother, father, guardian, emergency contact, authorised pickup), the can-pickup flag, and whether this is the child's current primary contact.
- **Group Assignment**: A dated record linking a Child to a group/section within a location, with a start date and an end date (nullable — open-ended until superseded by a new assignment). A minimal Group/Section reference (name, location) is introduced by this feature since no earlier feature created one — see Assumptions.
- **Vaccination Record**: A dated entry per Child recording a vaccine name, the date it was administered, and an optional next-due date used for due alerts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can create a new child file with core profile information in under 2 minutes.
- **SC-002**: 100% of children on the waiting list (no contract) have a fully viewable, fully functional file — contract status never blocks file access.
- **SC-003**: 100% of the time, a child's allergy/medical information is retrievable in the same request as their core profile — never requiring a separate lookup.
- **SC-004**: A sibling relationship never results in a duplicated contact record — the same contact's data is always consistent across every child it's linked to.
- **SC-005**: Deactivating a child takes effect such that they disappear from active listings on the very next read, with zero loss of historical medical, contact, group, or vaccine data.

## Assumptions

- No earlier feature has introduced a "Group" or "Section" concept (004/locations manages physical buildings only; group/section management was explicitly deferred by 004's Out of Scope to "a dedicated groups feature or as part of attendance"). This feature introduces the minimal Group/Section reference (name, scoped to a location) needed to support dated child assignments — full group administration (capacity limits, BKR configuration per group) is out of scope here and may be revisited by a dedicated feature if needed.
- Contacts are data records only in this feature — no parent-facing login account (TenantUser) is provisioned here. Feature 003's shipped notes flagged that account-provisioning UX for parents is left to whichever feature needs it first (likely 012, parent communication); this feature only models the contact's data (name, phone, email, locale), not authentication.
- Profile photo storage reuses the `IProfilePhotoStorage` signed-URL port established in feature 005 (staff); the exact object-path convention for child photos (as opposed to staff photos) is an implementation detail decided at plan time, not a new mechanism.
- Following features 004/005's precedent, a child cannot be deleted only while they have an *active* dependent introduced by a later feature (an active enrolment contract, once feature 007 exists); since that feature doesn't exist yet, this constraint currently has no effect beyond the extension point described in FR-013.
- Vaccine due-date alerting in this feature is a computed flag (next-due date ≤ today), surfaced wherever a vaccine record is displayed — no separate notification/push mechanism is built here (that's a natural fit for feature 012, parent communication, or a future staff-facing alert list).
- Severity levels for allergies are a small fixed set (e.g. mild/moderate/severe) consistent with standard medical-record practice; the exact set is a plan-time detail.
- Linking a contact to a child is always an explicit director action (searching the tenant's existing contact list and choosing one, per research.md R6) — there is no automatic name/phone matching that could accidentally link two non-sibling families' contacts together. Accidental cross-family linking is therefore not a risk this spec needs to guard against separately.
- No hard limit is placed on how many contacts a single child can have, or how many groups a single location can have; Phase 1 volume is expected to be small (a handful of each), so no pagination is required on either listing (mirrors feature 005's equivalent assumption).
- GDPR data export/deletion per child is explicitly out of scope for this feature (stated in the original feature description's Out of Scope) — deactivation retains all data indefinitely, consistent with every other feature's soft-delete-preserves-history pattern. A future feature may add export/purge capability without requiring a schema change here.
