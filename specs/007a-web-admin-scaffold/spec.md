# Feature Specification: Web Admin Scaffold

**Feature Branch**: `007a-web-admin-scaffold`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Bootstrap the director web admin app — mirrors what feature 008 did
for the caregiver Expo app, but for Next.js. Remove the Habits walking skeleton, wire director
authentication, establish the navigation shell, and ship one real screen (staff list, since
features 005/008a both need it) so future features stop having to defer their web UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director logs in and reaches a real working app (Priority: P1)

A director opens the web admin app in a browser, signs in with their existing email/password
(or Google account), and lands inside a navigation shell that shows their organisation name and
their own name — a real application, not the old Habits template.

**Why this priority**: Without a working login and shell, nothing else in this feature (or any
future director-web feature) can be reached. This is the foundation everything else sits on.

**Independent Test**: Can be fully tested by opening the app, signing in with a known director
account, and confirming the sidebar shell renders with the organisation and director name —
independent of whether any content screen exists yet.

**Acceptance Scenarios**:

1. **Given** a director with valid email/password credentials for their organisation, **When**
   they submit the login form, **Then** they are signed in and see the navigation shell with
   their organisation name and their own name displayed.
2. **Given** a director with a valid Google account linked to their organisation, **When** they
   sign in with Google, **Then** they are signed in and see the same navigation shell.
3. **Given** a director enters an incorrect password, **When** they submit the login form,
   **Then** they see a clear, human-readable error message and remain on the login screen.
4. **Given** a signed-in director closes and reopens their browser before their session expires,
   **When** they return to the app, **Then** they are still signed in and do not need to log in
   again.
5. **Given** a signed-in director explicitly logs out, **When** they next open the app, **Then**
   they are returned to the login screen.

---

### User Story 2 - Director finds and manages a staff member (Priority: P2)

A director opens the Staff screen, searches or filters for a specific caregiver, and resets that
caregiver's PIN or deactivates/reactivates their account — all without leaving the browser.

**Why this priority**: This is the first real, business-value-producing screen the web admin
ships. Features 005 (staff) and 008a (PIN/kiosk) already built the backend for this; directors
currently have no way to use it at all.

**Independent Test**: Can be fully tested by signing in as a director, navigating to Staff,
searching for a known staff member, and performing a PIN reset and a deactivate/reactivate
action, then confirming the change is reflected in the list.

**Acceptance Scenarios**:

1. **Given** a director on the Staff screen with multiple staff members listed, **When** they
   type a name into the search field, **Then** the table filters to matching staff members only.
2. **Given** a director viewing the Staff table, **When** they select the "reset PIN" action for
   a caregiver, **Then** they can set a new 4-digit PIN and receive confirmation that it was
   saved.
3. **Given** a director viewing the Staff table, **When** they select "deactivate" for an active
   staff member and confirm the action, **Then** that staff member's status updates to
   deactivated in the table.
4. **Given** a deactivated staff member, **When** the director selects "reactivate" and confirms,
   **Then** that staff member's status updates back to active in the table.
5. **Given** a tenant with no staff members yet, **When** the director opens the Staff screen,
   **Then** they see an empty state (icon + one sentence) instead of a blank table.
6. **Given** the staff API request fails (e.g. network or server error), **When** the Staff
   screen attempts to load, **Then** the director sees a clear inline error state with the
   option to retry, not a raw error or blank screen.

---

### User Story 3 - Director manages paired devices (Priority: P3)

A director opens the Devices screen to see which tablets are paired to which location/group, and
revokes a device that has been lost or is no longer in use.

**Why this priority**: Device management is a real, already-built backend capability (feature
008a) with no UI today, but it is used less frequently than day-to-day staff management, so it
is ordered after Staff.

**Independent Test**: Can be fully tested by signing in as a director, navigating to Devices,
confirming paired tablets are listed with their location/group/pairing details, and revoking one
device, then confirming it no longer appears as active.

**Acceptance Scenarios**:

1. **Given** a tenant with paired tablets, **When** the director opens the Devices screen,
   **Then** they see each device's location, group, who paired it, and when.
2. **Given** a director viewing the Devices list, **When** they select "revoke" for a device and
   confirm the action, **Then** that device is marked revoked and is visually distinguished in
   (not removed from) the devices list.
3. **Given** a tenant with no paired devices yet, **When** the director opens the Devices
   screen, **Then** they see an empty state (icon + one sentence) instead of a blank table.

---

### Edge Cases

- What happens when a director's session/refresh token expires while they are actively using the
  app? The app must detect this and redirect to the login screen with a clear message, not show
  a broken or infinitely-loading screen.
- What happens if a director attempts to reset a PIN to a value that collides with another
  caregiver's PIN at the same location (feature 008a's uniqueness-per-location rule)? The error
  returned by the existing API must be surfaced as a clear, localized inline message on the form,
  not a generic failure.
- What happens if two directors (or two browser tabs) deactivate/reactivate or revoke the same
  entity concurrently? The UI must reflect whatever the server's final state is on next load/
  refresh, without crashing or showing stale contradictory state indefinitely.
- What happens when a director opens the app on a narrow browser window? The sidebar/table layout
  must remain usable (e.g. collapsible sidebar), even though director web is optimized for
  desktop, not designed mobile-first.
- What happens when the director's account has no organisation name set, or other optional
  profile data is missing? The shell must render sensible fallback text, not blank or broken UI.
- What happens if a director navigates directly to a not-yet-built section's URL (bypassing the
  disabled nav entry)? The system MUST show a not-yet-available message within the shell, not a
  broken route or a raw 404.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a director to sign in using email and password against the
  existing authentication backend (feature 003).
- **FR-002**: The system MUST allow a director to sign in using Google OAuth against the existing
  authentication backend (feature 003).
- **FR-003**: The system MUST persist a director's session (JWT + refresh token) across browser
  restarts until explicit logout or token/session expiry, using storage appropriate for a web
  context (not readable by client-side JavaScript).
- **FR-004**: The system MUST allow a director to explicitly log out, clearing their session and
  returning them to the login screen.
- **FR-005**: The system MUST present a persistent navigation shell (sidebar) after login,
  displaying the signed-in director's organisation name and their own name.
- **FR-005a**: The backend MUST expose the signed-in director's name and their organisation's
  name to the client — neither is currently returned by any existing endpoint (the auth
  response returns only email/role; no endpoint returns the tenant's display name). This feature
  adds the minimal exposure needed (the existing `TenantUser.Name` field on the auth response,
  and the existing `Tenant.Name` value via a small read-only endpoint), with no new business
  logic.
- **FR-005b**: While the organisation name and director name are still loading, the navigation
  shell MUST show a neutral loading state (e.g. skeleton text) rather than blank space or a
  flash of empty/placeholder text.
- **FR-006**: The navigation shell MUST include entries for sections not yet built in this
  feature (e.g. Locations, Contracts) as inert/placeholder items, without breaking navigation to
  the sections that are built (Staff, Devices).
- **FR-007**: The system MUST display a list of the organisation's staff members, showing at
  minimum: name, role, assigned location(s), and active/deactivated status.
- **FR-008**: The system MUST allow a director to search and/or filter the staff list (e.g. by
  name) without a full page reload.
- **FR-009**: The system MUST allow a director to set or reset a caregiver's 4-digit PIN from the
  staff list, using the existing PIN-management backend (feature 008a).
- **FR-010**: The system MUST allow a director to deactivate an active staff member and to
  reactivate a deactivated staff member, using the existing staff backend (feature 005), with an
  explicit confirmation step before the action is applied.
- **FR-011**: The system MUST display an empty state (not a blank table) when a tenant has no
  staff members.
- **FR-012**: The system MUST display a clear, human-readable inline error state (not a raw error
  or stack trace) when the staff list, PIN reset, or deactivate/reactivate actions fail, and MUST
  offer a way to retry.
- **FR-013**: The system MUST display a list of the organisation's paired devices, showing at
  minimum: location, group, who paired the device, and when it was paired.
- **FR-013a**: The backend MUST expose a read-only endpoint listing the tenant's paired devices
  (location, group, paired-by, paired-at, revoked status), scoped to the tenant schema and
  restricted to directors, mirroring the authorization pattern of existing list endpoints.
- **FR-014**: The system MUST allow a director to revoke a paired device, using the existing
  device-management backend (feature 008a), with an explicit confirmation step before the action
  is applied.
- **FR-015**: The system MUST display an empty state (not a blank table) when a tenant has no
  paired devices.
- **FR-016**: The system MUST display a clear, human-readable inline error state when the devices
  list or revoke action fails, and MUST offer a way to retry.
- **FR-017**: All user-facing strings MUST be presented via localization keys with Dutch, French,
  and English translations available from initial release — no hardcoded UI text.
- **FR-018**: The system MUST NOT expose any screen, data, or action belonging to a different
  organisation (tenant) than the signed-in director's own — all data displayed MUST come from
  authorization-scoped existing endpoints.
- **FR-019**: The system MUST remove all Habits-template screens, navigation entries, and code
  references from the web application as part of this feature.
- **FR-020**: The system MUST NOT introduce any new database table, authorization rule, or
  business logic — this feature is a UI consumer of existing, already-shipped backend capability
  (features 003, 005, 008a). The exceptions are FR-013a (a read-only device-listing endpoint,
  since 008a built pairing/revocation but no list) and FR-005a (exposing the already-stored
  director name and organisation name, neither of which any existing endpoint returns) — both
  are minimal, read-only additions with no new domain behavior, following existing endpoint
  conventions.

### Key Entities

This feature introduces no new data entities. It is a UI consumer of entities already defined by
prior features:

- **Director / TenantUser** (feature 003/005): the signed-in user; source of organisation name,
  director name, and role-based authorization.
- **Staff** (feature 005): rendered in the Staff list — name, role, assigned location(s), active
  status, PIN state (write-only from this UI).
- **Device** (feature 008a): rendered in the Devices list — location, group, paired-by, paired-at,
  revoked status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can go from opening the app to seeing their organisation's staff list in
  under 10 seconds on a typical broadband connection.
- **SC-002**: A director can locate a specific staff member among at least 50 staff records using
  search/filter in under 5 seconds.
- **SC-003**: A director can complete a PIN reset or a deactivate/reactivate action, including the
  confirmation step, in 3 clicks or fewer from the staff list.
- **SC-004**: A director can complete a device revocation, including the confirmation step, in 3
  clicks or fewer from the devices list.
- **SC-005**: 100% of user-facing text on the screens built in this feature is available in
  Dutch, French, and English at ship time.
- **SC-006**: A director whose session has expired is redirected to the login screen with a clear
  message on their very next action, rather than encountering a broken or unresponsive screen.
- **SC-007**: No screen built in this feature ever displays another organisation's data, verified
  by testing with at least two distinct tenant accounts.

## Assumptions

- The backend endpoints for authentication (feature 003), staff CRUD/deactivation (feature 005),
  and PIN management/device pairing/revocation (feature 008a) are complete, stable, and require no
  changes to support this feature, confirmed by BACKLOG.md's shipped-notes for those features —
  with one exception (FR-013a): feature 008a never built a read endpoint to list paired devices,
  only pair/revoke/exit-room-mode, so this feature adds that one minimal read-only listing
  endpoint rather than deferring the Devices screen entirely.
- "Director" is the only role that uses the web admin app in this feature's scope; no other role
  (staff/parent) is expected to sign into this application.
- Director web usage assumes an active internet connection; no offline-first behavior is required
  (unlike the caregiver app's offline-queue infrastructure from feature 008). A network failure
  simply shows a retryable error state.
- Refresh-token storage uses an httpOnly cookie via the existing `app/api/set-refresh-token` route
  already scaffolded in `web/`, consistent with not storing tokens in client-readable storage.
- Staff and device list sizes are expected to be tens to low hundreds of records per tenant in
  Phase 1; client-side search/filter is sufficient and server-side pagination is not required yet.
- "Placeholder" navigation entries for sections not yet built (Locations, Contracts, etc.) are
  visually present but non-functional (e.g., disabled or clearly marked "coming soon") rather than
  omitted entirely, so the navigation structure does not need to be redesigned when those sections
  are added later.
- Locale selection/detection mechanism (browser-detected vs. an explicit switcher) is out of
  this feature's scope to design new UI for — this feature only guarantees translated content
  exists (FR-017) for whatever mechanism the localization library is configured with at
  implementation time; adding a visible locale switcher is a future feature's decision if needed.
- No new workflow document is required: this feature is infrastructure plus UI over
  already-documented business behavior (staffing under Classroom Operations), not a new business
  workflow itself.
