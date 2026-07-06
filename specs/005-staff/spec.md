# Feature Specification: Staff Management

**Feature Branch**: `005-staff`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "Build staff member management for KDV caregivers and directors. Staff member profiles (name, email, phone, qualification level, profile photo), Director|Staff role assignment, multi-location eligibility assignment, director-provisioned accounts (email/password only, invite-only), qualification level feeds BKR ratio computation in feature 009, soft-delete on departure."

## Clarifications

### Session 2026-07-06

- Q: Should the existing Director account (created during organisation onboarding, feature 001) be extended with an optional Staff Profile (qualification, phone, photo), so a director who personally covers shifts can appear in caregiver rosters and BKR counts? → A: Yes — Staff Profile attaches to any tenant user regardless of role (Director or Staff). Qualification level is required when a profile is created for a `Staff`-role account, but optional for a `Director`-role account (a director who never covers the floor has no need to set one).
- Q: Can a staff member edit their own profile (phone, photo), or is all profile maintenance director-only? → A: Director-only for Phase 1 — consistent with the "director-provisioned" framing already established for staff accounts; self-service profile editing is out of scope here and may be revisited in a later phase.

### Session 2026-07-06 (requirements-quality review)

A `/speckit-checklist` pass surfaced 26 completeness/clarity questions against the draft above (see `checklists/requirements-quality.md`). Every one was resolved by editing this spec rather than left open — the resolutions are folded into the sections below (User Scenarios, Edge Cases, Functional Requirements, Success Criteria, Assumptions). Two resolutions are substantive enough to call out here:

- **User Story 3's title/description previously said a director could change a staff member's role between Staff and Director.** That capability was never reflected in the Functional Requirements (FR-009 only ever listed phone/qualification/photo/eligible-locations) and directly conflicts with this spec's own resolution to the "director who also covers shifts" problem (an optional Staff Profile attached to the existing Director account, not a role flip — see the first Clarification above and FR-002). The title/description below have been corrected to remove role-changing; no role-change capability exists in this feature.
- **Deactivating a staff member's login-blocking behavior (FR-010) only applies when the affected account's role is `Staff`.** A `Director` account that has an attached (optional) Staff Profile must never lose the ability to log in by deactivating that profile — deactivating a director's own Staff Profile only removes them from caregiver rosters/BKR counts, per the new edge case under User Story 4.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director creates a staff profile (Priority: P1)

A director adds a new caregiver to the organisation: name, email, phone, and qualification level (qualified caregiver, auxiliary, or student/volunteer). The system creates the account and the new staff member receives an invitation to set their own password and log in to the caregiver app.

**Why this priority**: Without a way to create staff accounts, no caregiver can use the platform at all — this is the foundation every other story and every later feature (scheduling, attendance, BKR) depends on.

**Independent Test**: Can be fully tested by a director submitting a new staff profile with a qualification level, confirming the account appears in the staff list in `Staff` status, and confirming the invited person can set a password and log in.

**Acceptance Scenarios**:

1. **Given** a director is logged in, **When** they submit a new staff profile with first name, last name, email, phone, and qualification level, **Then** a staff account is created with an invitation sent to the provided email, and the profile appears in the organisation's staff list.
2. **Given** a staff invitation has been sent, **When** the invited person opens the invitation link and sets a password, **Then** they can log in to the caregiver app with email and password (no social login).
3. **Given** a director submits a staff profile without a qualification level, **When** the form is validated, **Then** the system rejects the submission — qualification level is required for every staff profile because feature 009 (attendance) depends on it for BKR computation.
4. **Given** a director is a staff member themself (covers a group when short-staffed), **When** they view the staff list, **Then** their own account (already created during organisation onboarding, feature 001) can be included in caregiver rosters and BKR counts without needing a second account or a second role value.
5. **Given** a staff invitation was sent but never used, **When** the director requests it be resent, **Then** a new invitation supersedes the old one (the old link stops working) and the invitee can complete setup using the new link.

---

### User Story 2 - Director assigns which locations a staff member may work at (Priority: P2)

A director marks which of the organisation's locations a given staff member is eligible to work at. A staff member can be eligible for one or several locations within the same organisation.

**Why this priority**: Multi-location organisations need this before they can build a weekly rota (feature 011) — but a single-location KDV can operate on User Story 1 alone, so this is independently valuable but not blocking for the MVP.

**Independent Test**: Can be fully tested by assigning a staff member to two locations and confirming both appear on their profile, independent of any actual day-by-day schedule (which is feature 011).

**Acceptance Scenarios**:

1. **Given** an organisation with two active locations, **When** a director assigns a staff member as eligible for both, **Then** both locations appear on the staff member's profile.
2. **Given** a staff member is eligible for a location, **When** the director removes that eligibility, **Then** the location no longer appears on the profile, and no other data (past shifts, past events) is affected.
3. **Given** a staff member has zero eligible locations assigned, **When** their profile is viewed, **Then** the system shows an empty eligibility list without error — assigning eligibility is not required before profile creation.

---

### User Story 3 - Director updates a staff member's profile (Priority: P2)

A director corrects or updates a staff member's details — phone number, qualification level, profile photo. Changing which role (Staff/Director) an account holds is out of scope for this feature (see Clarifications) — the only way a Director gains caregiver-facing presence is the optional Staff Profile attachment from User Story 1.

**Why this priority**: Profiles need to stay accurate over time (promotions, corrected phone numbers, requalification), but this is routine maintenance rather than a launch blocker.

**Independent Test**: Can be fully tested by editing an existing staff member's qualification level or phone number and confirming the change is reflected immediately, with no impact on their historical records.

**Acceptance Scenarios**:

1. **Given** an existing staff profile, **When** a director updates the qualification level, **Then** the new qualification takes effect immediately for any future BKR computation (feature 009), without altering past attendance history.
2. **Given** an existing staff profile, **When** a director uploads a new profile photo, **Then** the profile displays the new photo via a signed URL — no publicly-accessible image link is ever created.

---

### User Story 4 - Director deactivates a staff member who has left (Priority: P3)

A staff member leaves the organisation. The director deactivates their profile rather than deleting it, preserving their historical record (past shift history, authored events) while removing them from active rosters and preventing further login.

**Why this priority**: Necessary for accurate long-term records and legal/audit requirements, but an organisation can operate for weeks without ever needing to offboard someone — lowest priority of the four.

**Independent Test**: Can be fully tested by deactivating a staff member and confirming they disappear from active staff lists and can no longer log in, while their historical authorship (e.g. on child events, once feature 008 exists) remains intact and attributable.

**Acceptance Scenarios**:

1. **Given** an active staff member with no future-dated commitments, **When** a director deactivates them, **Then** the account is soft-deleted (marked inactive, not removed), can no longer log in, and disappears from active staff/rota pickers.
2. **Given** a deactivated staff member, **When** a director reactivates them, **Then** the account becomes active again and can log in.
3. **Given** a staff member who authored historical records before being deactivated, **When** those records are viewed later, **Then** the staff member's name still appears as the author — deactivation never hides authorship of past work.

---

### Edge Cases

- A staff member works at location A on Monday and location B on Tuesday: this feature only stores which locations they are *eligible* for; the day-by-day assignment is feature 011's concern and is out of scope here.
- A director is also a staff member covering a group: no second account or dual-role value is created — the existing single Director account already satisfies both "director" and "can work a shift" access, since Director already has the broadest access level (see FR-002 and Assumptions).
- A director deactivates their own attached Staff Profile (e.g., they stop personally covering shifts): only the Staff Profile is deactivated — they disappear from caregiver rosters/BKR counts, but their Director account is entirely unaffected and they can still log in and administer the organisation. This is distinct from deactivating a `Staff`-role account, where login is blocked (FR-010).
- A staff member's only eligible location is later deactivated (feature 004): the staff profile is unaffected — the `StaffLocationEligibility` row is not cleaned up or removed, it simply now points at an inactive location, with no dependents to block anything, since staff are explicitly not bound to a single location.
- A director removes a staff member's only remaining eligible location (as opposed to one of several): this results in the same empty eligibility list described in User Story 2's third acceptance scenario — not an error.
- Two directors attempt to invite the same email address as a staff member at the same time: because a new `Staff`-role account is created at invitation time (not only at acceptance — see Assumptions), this is the same race as any duplicate-email creation attempt (FR-008) — the database's email-uniqueness constraint is the sole arbiter of which request wins; the losing request receives the standard "already exists" error, mirroring how feature 001 resolves the equivalent concurrent-registration race.
- A director creates a second staff profile for an email that already has a pending (not yet accepted) invitation: this fails the same way as any duplicate email (FR-008), since the account row already exists from the first invitation, not only the invitation record itself.
- A staff invitation link is not used before it expires, or the director simply wants to re-send it (e.g., the email never arrived): the director can request the invitation be resent (FR-006a). Resending supersedes the prior invitation — the old token stops working — rather than creating a second, independent invitation for the same profile.
- A staff invitation email fails to send (e.g., a transient SMTP error): the staff profile and invitation record are still created; the send failure is logged server-side and does not fail the director's create-profile request. The director can use resend (FR-006a) once the underlying issue is resolved.
- An invitee opens their invitation link, successfully sets a password, and then opens the same link again (e.g., a duplicate email client prefetch, or the invitee mistakenly clicking twice): the second attempt is rejected the same way an expired token is (FR-006b) — the invitation is single-use even though its expiry timestamp hasn't passed yet.
- A staff member (or anyone) attempts to log in using the credentials of a `Staff`-role account whose invitation has not yet been accepted (no password has ever been set): login fails with the platform's standard invalid-credentials response — the same response an unknown email or wrong password produces, so no detail about the account's onboarding state is ever revealed (mirrors feature 003's existing non-enumeration posture for login failures).
- A staff member is deactivated while they still have data attached to future dependents (scheduled shifts once feature 011 exists, active group assignments once relevant) — this feature builds the extension point for that guard (see FR-011) but registers no guards itself, since neither dependent exists yet.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to create a staff profile consisting of: first name (required, ≤100 characters), last name (required, ≤100 characters), email (required, valid email format, ≤254 characters), phone (required, ≤30 characters, permissive international format — mirrors feature 004's phone convention), qualification level (conditionally required — see FR-003), and an optional profile photo. A Staff Profile MAY also be attached to an existing Director account (e.g., the organisation's founding director created in feature 001), so a director who personally covers shifts can appear in caregiver rosters and BKR counts without a second account.
- **FR-002**: Every staff profile MUST have exactly one role value, reusing the existing `Director`/`Staff`/`Parent` role already established in feature 003 — this feature does not introduce a second, per-location role, and does not introduce any capability to change an account's role after creation. A person acting as both director and caregiver is represented by a single `Director` account, since Director access is a superset of Staff access under the existing `StaffOrDirector` authorization policy.
- **FR-003**: Qualification level MUST be one of: qualified caregiver, auxiliary, or student/volunteer. It is required whenever a profile is created for a `Staff`-role account — it is not optional there, because feature 009's BKR ratio computation depends on it (only qualified caregivers and auxiliaries count; students/volunteers never count). It is optional for a `Director`-role account, since not every director personally covers shifts; a director who wants to appear in caregiver rosters/BKR counts sets one, others may leave it unset. The target role for this rule is either the role of the new account being created, or — when attaching to an existing account — that account's existing role.
- **FR-004**: The system MUST support assigning a staff member as eligible to work at zero, one, or multiple locations within the same organisation. Eligibility is independent of any specific date or schedule (that is feature 011). No upper limit is imposed on the number of eligible locations (an organisation's total location count is expected to remain small in Phase 1 — see Assumptions).
- **FR-005**: Staff accounts MUST be created only by a director (invite-only) — there is no self-registration path for staff, matching the existing invite-only pattern used for organisation onboarding (feature 001).
- **FR-006**: When a director creates a new staff account (not the director-opt-in path), the system MUST send an invitation to the provided email allowing the recipient to set their own password before they can log in. The invitation MUST expire, matching the existing invitation-expiry behavior used elsewhere in the system. A failure to send the invitation email MUST NOT fail the staff profile's creation — the failure is logged server-side (constitution Principle VI) and the director can resend (FR-006a).
- **FR-006a**: Directors MUST be able to resend a staff invitation to the same email address. Resending supersedes (invalidates) any still-pending prior invitation for that profile rather than creating a second, independently-valid one — mirroring feature 001's existing "resending supersedes the prior invitation" behavior.
- **FR-006b**: Accepting a staff invitation MUST be single-use: once a password has been successfully set via a given invitation, any further attempt to use that same invitation (even before its expiry timestamp) MUST be rejected the same way an expired or unknown token is rejected — no detail distinguishing "already used" from "expired"/"unknown" is ever revealed to the caller.
- **FR-007**: A staff member's authentication MUST use email + password only — no Google or Apple sign-in, consistent with the existing caregiver-app auth strategy (feature 003).
- **FR-008**: The system MUST reject creating a second staff or director account with an email address already in use within the same organisation. This applies from the moment a new `Staff`-role account is created (at invitation time, not only at acceptance — see Assumptions), so two concurrent attempts to invite the same email race on the same uniqueness check as any other duplicate-account attempt.
- **FR-009**: Directors MUST be able to update an existing staff member's phone, qualification level, profile photo, and eligible-locations list at any time. Profile editing is director-only in Phase 1 — a staff member cannot edit their own profile; self-service editing is out of scope here. Changing an account's role (Staff ↔ Director) is out of scope for this feature (see FR-002, Clarifications).
- **FR-010**: The system MUST support deactivating (soft-delete) a staff member rather than permanently deleting their record. Deactivating a `Staff`-role account blocks login (on their very next login attempt — an already-issued access token is not proactively revoked, consistent with the platform's short-lived-JWT design, but all of that account's active refresh tokens are invalidated immediately, mirroring the existing password-reset session-invalidation behavior, so no new access token can be silently obtained) and removes them from active staff/roster listings; all historical records they authored or were referenced by remain intact and continue to display their name. Deactivating a `Director`-role account's optional Staff Profile MUST NOT block that account's login or admin access — it only removes them from caregiver rosters/BKR counts (see Edge Cases).
- **FR-011**: The system MUST provide an extension point for other features to block staff deactivation when the staff member has an active dependent (e.g., a future-dated shift or group assignment). This feature registers no such guards itself, since no feature yet creates shifts or group assignments — a future feature that introduces such a dependent is responsible for registering its own guard, mirroring the pattern already established for location deactivation (feature 004).
- **FR-012**: Directors MUST be able to reactivate a previously deactivated staff member, restoring their ability to log in (for a `Staff`-role account) and appear in active listings.
- **FR-013**: Profile photos MUST be stored and served exclusively via signed URLs — no public, unsigned image links are ever exposed. Re-uploading a photo replaces the same profile's photo at its existing storage location rather than accumulating orphaned prior uploads (an implementation detail — see Assumptions).
- **FR-014**: All user-facing labels and validation/error messages MUST use i18n keys (NL/FR/EN); no hardcoded text. Invitation email *body* content is explicitly excluded from this requirement for this feature — it follows the existing, already-shipped English-only transactional email pattern (features 001/003), consistent with feature 019 owning the templating/i18n rework for all transactional email project-wide; this feature does not introduce a new instance of hardcoded UI or validation text.
- **FR-015**: Staff data MUST be scoped to the tenant schema — a director can only see, invite, edit, or deactivate staff within their own organisation.

### Key Entities

- **Staff Profile**: Represents a caregiver or director's operational profile — first name, last name, phone, qualification level (required for `Staff`-role accounts, optional for `Director`-role accounts), profile photo (signed URL, optional), and active/deactivated status. Linked one-to-one with the existing tenant user account (email, password, role) introduced in feature 003 — attachable to either a `Staff` or a `Director` account.
- **Location Eligibility**: A many-to-many association between a staff profile and the organisation's locations (feature 004), recording which locations a staff member may be assigned to work at. Carries no date or schedule information — that belongs to feature 011.
- **Staff Invitation**: A time-limited, single-use invitation tied to an email address and an organisation, allowing an invited staff member to set their own password on first access. Single-use is enforced even before the expiry timestamp elapses (FR-006b). Distinct from the organisation-onboarding invitation (feature 001), which creates a new organisation rather than a new staff member within an existing one.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can go from opening the create-staff form to seeing confirmation that the profile was created and its invitation sent, in under 2 minutes.
- **SC-002**: 100% of `Staff`-role profiles have a non-empty qualification level — the system never allows one to be created or saved without it, since downstream BKR computation (feature 009) requires it on every caregiver profile.
- **SC-003**: A staff member can go from receiving their invitation email to successfully logging in without switching devices and without any second email exchange (e.g., a password reset) — the one invitation email is sufficient on its own.
- **SC-004**: Deactivating a `Staff`-role staff member takes effect such that their very next login attempt fails and they disappear from active listings on the very next read — with zero loss of their historical authored records, and with all of their active refresh tokens invalidated so no new access token can be silently obtained after deactivation.
- **SC-005**: An organisation with multiple locations can assign any staff member to any subset of its locations, with changes reflected on the very next read of that staff member's profile or eligible-locations list — no caching layer exists to delay it.

## Assumptions

- The existing global `Role` field on the tenant user account (Director/Staff/Parent, established in feature 003) is reused as-is; this feature does not add a per-location role and does not add any way to change an account's role after creation. A director who also covers caregiver shifts is represented by their single existing Director account gaining an optional Staff Profile (see Clarifications) — no second account is created for the same person. This directly resolves the "director is also a staff member" edge case using feature 003's existing authorization model (`StaffOrDirector` policy) rather than inventing a new one, per feature 003's explicit guidance that later features should not invent their own role-comparison logic. This feature depends on feature 003's `DirectorOnly` policy already existing and being sufficient to protect every staff-management endpoint — no new authorization policy is introduced here.
- Staff account provisioning reuses the same invitation-token primitives introduced for organisation onboarding (feature 001) — a time-limited, single-use, signed token emailed to the invitee — scoped here to an existing tenant/organisation instead of creating a new one. This follows the direction flagged in feature 003's shipped notes.
- The mechanics of generating and storing a signed GCS URL for profile photos (e.g., a reusable signed-upload-URL utility, and the exact validity duration of a signed URL) are implementation details decided at plan time; this spec only requires that a profile photo, when present, is always accessed via a time-limited signed URL and never a public one. Re-uploading a photo is expected to reuse the same storage location per profile, so no orphaned prior uploads accumulate — an implementation detail, not a separate cleanup requirement. This is likely the first feature to need a signed-URL utility; a later feature (e.g., feature 006 children) may find it reusable, though that feature's actual needs aren't known yet.
- Deactivating a staff member's eligibility for a specific location does not, by itself, block that location from being deactivated (feature 004) — staff are explicitly not bound to a single location, so location-eligibility is not treated as a "hard dependent" the way active contracts are expected to be. Eligibility rows for a since-deactivated location are left as-is, not cleaned up.
- A staff member cannot be deleted only while they have an *active* dependent introduced by a later feature (future scheduled shifts from feature 011, active group assignments once modelled); since neither feature exists yet, this constraint currently has no effect beyond the extension point described in FR-011.
- Qualification level values (qualified caregiver, auxiliary, student/volunteer) are fixed to the three named in the feature description; no additional custom qualification levels are supported in Phase 1.
- A deactivated staff member's profile photo is retained, not deleted, alongside the rest of their historical profile data — consistent with FR-010's "nothing is hard-deleted."
- No hard limit is placed on how many staff profiles or pending invitations a single organisation can have; Phase 1 volume is expected to be dozens per organisation at most, so no pagination is required on the staff list (mirrors plan.md's Scale/Scope).
