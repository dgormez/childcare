# Feature Specification: Location Management

**Feature Branch**: `004-locations`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "Build location management — the physical KDV buildings that belong to an organisation (tenant). CRUD for locations within a tenant: name, address, phone, email, max licensed capacity (number of places). Settings per location: naam_locatie (official KDV name registered with Opgroeien), dossiernummer (Opgroeien location identifier, nullable), verantwoordelijke (responsible person name, for Opgroeien XML reports), flex_permission (flex opvang toestemming, default false), bo_permission (buitenschoolse opvang, default false). KBO/ondernemingsnummer lives at the organisation level, not per location. A location belongs to exactly one organisation. Multiple locations per organisation are supported from day one. The director manages all locations from the web admin. dossiernummer and verantwoordelijke are nullable and must not block location creation. All user-facing strings use i18n keys (NL/FR/EN). A location cannot be hard-deleted if it has active contracts or staff assignments — soft-delete (deactivated_at) instead."

## Clarifications

### Session 2026-07-06

- Q: Can a deactivated location be reactivated? → A: Yes — a director can reactivate a deactivated location at any time, clearing `deactivated_at` and restoring it to active-location listings.
- Q: Should this feature support full location relocation (auto-carrying over staff/contract assignments from an old location to a new one)? → A: No — full relocation continuity is deferred to features 005 (staff)/007 (contracts)/011 (scheduling), since staff are not bound to a single location and those entities don't exist yet. This feature instead adds a "duplicate location" action that clones a location's fields into a new draft location, so a director doesn't have to re-enter Opgroeien settings from scratch when a physical move happens. (A note has been added to `BACKLOG.md` flagging staff/child move-continuity for those later features.)
- Q: Can an organisation have zero active locations (e.g., all deactivated at once)? → A: Yes — no minimum-active-location guard is enforced by this feature.
- Q: How are concurrent edits to the same location by two directors resolved? → A: Last-write-wins — the most recent save simply overwrites; no optimistic-concurrency/version-token conflict detection is required.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director creates and manages locations (Priority: P1)

A director of a KDV organisation adds the physical buildings ("locations") their organisation operates out of, so that all subsequent operational data (staff, children, contracts, attendance) can be tied to a specific building.

**Why this priority**: Every downstream feature (005-staff, 006-children, 007-contracts, 009-attendance, and beyond) requires at least one location to exist. Without this, no other Phase 1 feature can be exercised end-to-end.

**Independent Test**: Can be fully tested by a director logging into the web admin, creating a location with name/address/phone/email/capacity, and seeing it appear in their organisation's location list — delivers value on its own as the foundational record other features attach to.

**Acceptance Scenarios**:

1. **Given** a director is logged into an organisation with zero locations, **When** they submit a new location with name, address, phone, email, and max capacity, **Then** the location is created, scoped to their organisation, and appears in the location list.
2. **Given** a director has one active location, **When** they create a second location, **Then** both locations exist independently and neither's data is affected by the other.
3. **Given** a director edits an existing location's name, address, phone, email, or max capacity, **When** they save the change, **Then** the location record is updated and the change is immediately reflected in the location list and detail view.
4. **Given** a director attempts to create or edit a location without a name, address, phone, email, or max capacity, **When** they submit the form, **Then** the system rejects the request with a validation error identifying the missing/invalid field.

---

### User Story 2 - Director fills in Opgroeien reporting settings (Priority: P2)

A director records the official Opgroeien-facing details for a location — its registered name, dossiernummer, the responsible person, and whether it holds flex or buitenschoolse opvang (BO) permission — as soon as that information becomes available, without those fields blocking the location's initial creation.

**Why this priority**: These fields are required for Phase 3 Opgroeien XML reporting (FO-SU-05) and for the closure-calendar and MeMoQ features that reference the responsible person and permissions, but a director frequently does not have dossiernummer yet at the time a new physical location opens (it is assigned by Opgroeien afterward).

**Independent Test**: Can be tested independently by creating a location with only the required core fields (User Story 1), confirming it saves successfully with all Opgroeien-specific fields empty/false, then later editing that same location to add dossiernummer, verantwoordelijke, and toggle flex/BO permission.

**Acceptance Scenarios**:

1. **Given** a director creates a new location, **When** they leave dossiernummer and verantwoordelijke blank, **Then** the location is still created successfully.
2. **Given** a location has no dossiernummer or verantwoordelijke set, **When** the director opens the location's settings later and fills them in, **Then** the values are saved and persist on subsequent views.
3. **Given** a new location is created, **When** no explicit value is provided for flex_permission or bo_permission, **Then** both default to false.
4. **Given** a director sets flex_permission and/or bo_permission to true, **When** they save, **Then** the updated values are reflected immediately in the location's settings view.

---

### User Story 3 - Director deactivates and reactivates a location (Priority: P3)

A director closes down a physical location (e.g., a building closes permanently) by deactivating it, preserving its historical record rather than deleting it outright — and can reactivate it later if the closure turns out to be temporary or was a mistake.

**Why this priority**: Lower priority than creation and settings because deactivation is a less frequent operation, and its full guard logic (blocking deactivation when active contracts/staff exist) only becomes fully meaningful once features 005 (staff) and 007 (contracts) exist — but the soft-delete/reactivate mechanism itself must exist from day one so those later features have something to extend.

**Independent Test**: Can be tested independently by creating a location with no dependent staff/contract data (the only case possible before features 005/007 ship), deactivating it, confirming it no longer appears in active-location lists, then reactivating it and confirming it reappears unchanged.

**Acceptance Scenarios**:

1. **Given** a location has no active staff assignments or contracts, **When** a director deactivates it, **Then** the location's `deactivated_at` timestamp is set, it is excluded from active-location lists, and it is not hard-deleted from the database.
2. **Given** a location has already been deactivated, **When** any user requests the organisation's list of active locations, **Then** that location does not appear.
3. **Given** a deactivated location, **When** a director or system process looks up its historical record (e.g., for audit or reporting purposes), **Then** the record is still retrievable.
4. **Given** a deactivated location, **When** a director reactivates it, **Then** its `deactivated_at` timestamp is cleared and it reappears in active-location lists with all its prior settings intact.

---

### User Story 4 - Director duplicates an existing location (Priority: P4)

A director opening a new physical location — including one replacing a closing building — creates it by duplicating an existing location's settings (address-style fields, capacity, Opgroeien reporting fields) into a new draft location, then edits only what differs, rather than re-entering everything from scratch.

**Why this priority**: Lowest priority because it is a convenience layered on top of User Stories 1 and 2, not a blocking capability — a director can always create a location field-by-field without it. It matters most in the location-replacement scenario (old building closes, new one opens) so the director isn't forced to re-enter Opgroeien settings they already had on file.

**Independent Test**: Can be tested independently by taking an existing location, invoking "duplicate," confirming a new location is created pre-filled with the source location's field values, then editing and saving the new location without affecting the original.

**Acceptance Scenarios**:

1. **Given** an existing location, **When** a director duplicates it, **Then** a new location is created in the same organisation with the same field values (name, address, phone, email, max capacity, naam_locatie, dossiernummer, verantwoordelijke, flex_permission, bo_permission), independently editable from the source.
2. **Given** a newly duplicated location, **When** the director edits its name and address before saving, **Then** only the new location's values change — the original source location is unaffected.
3. **Given** a duplicated location, **When** it is viewed later, **Then** it carries no reference or link back to the source location — it is a fully independent record.

---

### Edge Cases

- An organisation starts with one location and adds a second later — staff and children created before the second location existed must be unaffected by the new location's creation (no default reassignment, no data migration required).
- A director attempts to deactivate a location that a later feature (staff or contracts) has marked as having active dependents — the system must reject the deactivation and clearly indicate why, once that dependent-checking capability exists.
- A director submits a location with an invalid email or phone format — the system must reject with a field-specific, localized validation error rather than a generic failure.
- A director submits a max capacity of zero or a negative number — the system must reject this as invalid, since a location must be able to host at least one child to be meaningful.
- Two directors of the same organisation edit the same location concurrently — the system applies last-write-wins; the most recent save overwrites without conflict detection.
- A director deactivates an organisation's only remaining active location — this is permitted; the organisation may have zero active locations at any point in time.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a director to create a location within their own organisation with: name, address, phone, email, and max licensed capacity (a positive integer).
- **FR-002**: System MUST allow a director to view, update, and list all locations belonging to their own organisation only — never another organisation's locations.
- **FR-003**: System MUST allow a director to record, view, and update per-location Opgroeien reporting settings: naam_locatie (official registered name), dossiernummer, verantwoordelijke, flex_permission, and bo_permission.
- **FR-004**: System MUST NOT require dossiernummer or verantwoordelijke to be present at location creation time; both MUST be nullable/empty at creation and fillable later without restriction.
- **FR-005**: System MUST default flex_permission and bo_permission to false when not explicitly provided.
- **FR-006**: System MUST support multiple locations per organisation from the first location onward, with no artificial limit tied to this feature.
- **FR-007**: System MUST scope every location to exactly one organisation (tenant) and MUST make it structurally impossible for a location to be read, updated, or listed under a different organisation's context.
- **FR-008**: System MUST support deactivating a location via a soft-delete marker (`deactivated_at`) rather than permanently removing its record; deactivated locations MUST be excluded from active-location listings but remain retrievable for historical/audit purposes. System MUST allow a director to reactivate a previously deactivated location at any time, clearing `deactivated_at` and restoring it to active-location listings with all prior settings intact.
- **FR-009**: System MUST NOT permanently delete (hard-delete) a location record under any circumstance reachable through this feature.
- **FR-010**: System MUST reject location creation or update requests missing required fields (name, address, phone, email, max capacity) or containing invalid values (e.g., non-positive max capacity, malformed email), returning a field-specific, localized error.
- **FR-011**: System MUST expose every location management operation (create, view, edit, deactivate, reactivate, duplicate) only to users holding the Director role for that organisation.
- **FR-012**: System MUST NOT block a location deactivation on active contracts or staff assignments in this feature, since no such dependent entities exist yet; the deactivation check MUST be designed so that features 005 (staff) and 007 (contracts) can register their own "has active dependents" condition before their respective entities ship.
- **FR-013**: All user-facing strings (labels, validation messages, error messages) exposed by this feature MUST be resolved through i18n keys covering Dutch, French, and English — no hardcoded text.
- **FR-014**: KBO/ondernemingsnummer MUST remain an organisation-level field and MUST NOT be duplicated or stored per location.
- **FR-015**: System MUST allow a director to create a new location by duplicating an existing location's fields (name, address, phone, email, max capacity, and all Opgroeien settings) into a new, independently editable location record. Duplication MUST NOT create any persisted link between source and copy, and MUST NOT carry over staff or contract assignments, since no such entities exist yet.
- **FR-016**: System MUST NOT enforce a minimum number of active locations per organisation; an organisation MAY have zero active locations at any point in time.
- **FR-017**: System MUST resolve concurrent edits to the same location using last-write-wins semantics; optimistic-concurrency/version-conflict detection is not required.

### Key Entities

- **Location**: A physical KDV building belonging to exactly one Organisation. Attributes: name, address, phone, email, max licensed capacity, naam_locatie, dossiernummer (nullable), verantwoordelijke (nullable), flex_permission (boolean, default false), bo_permission (boolean, default false), deactivated_at (nullable timestamp marking soft-delete).
- **Organisation**: The tenant that owns one or more Locations (already modeled by feature 001); holds KBO/ondernemingsnummer at this level, not per location.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can create a new location, providing only the required core fields, in under 2 minutes. (UI-dependent — this feature is backend-only per plan.md; verified once the web admin screens consuming this API ship. This backend feature's own tasks cannot measure human task-completion time on their own.)
- **SC-002**: 100% of locations created without dossiernummer or verantwoordelijke can later be edited to add those values with no data loss or re-entry of other fields.
- **SC-003**: An organisation can operate with 2 or more simultaneously active locations with zero cross-contamination of data between them (each location's settings and status are independently readable and editable).
- **SC-004**: 100% of attempts to view or modify a location belonging to a different organisation are rejected.
- **SC-005**: 100% of location deactivations preserve the underlying record — no deactivated location is ever unrecoverable via historical/audit access, and 100% of reactivations restore the location with all prior settings intact.
- **SC-006**: A director can create a new location by duplicating an existing one, editing only the fields that differ, in under 1 minute. (UI-dependent — same caveat as SC-001: verified once the web admin screens consuming this API ship.)

## Assumptions

- No subscription/plan-tier limit on the number of locations per organisation is enforced by this feature — plan-tier enforcement (trial/starter/pro) was explicitly deferred as billing/subscription scope by feature 001-organisation-onboarding and remains out of scope here.
- Since features 005 (staff) and 007 (contracts) do not exist yet, "prevent deactivation while active contracts or staff assignments exist" cannot be fully implemented in this feature. This feature builds the soft-delete mechanism and an extensible dependent-check point; the actual blocking behavior against staff/contract data is completed when those features ship.
- Location name uniqueness within an organisation is not enforced — an organisation may have two locations with the same display name (e.g., during a rename transition) without the system treating this as an error.
- Phone and email fields follow standard international format validation (valid email syntax, phone number with optional country code); no Belgium-specific format is mandated at this stage.
- Group/section management within a location, and the physical access control hardware integration (Paxton), are out of scope, as documented in the original feature backlog for this feature.
