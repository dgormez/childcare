# Feature Specification: Authentication & Role-Based Authorization

**Feature Branch**: `003-auth`

**Created**: 2026-07-04

**Status**: Draft

**Input**: User description: "Build the authentication layer for all three products (web admin, caregiver app, parent app) on top of the multi-tenancy scaffold. A working auth skeleton already exists (AuthEndpoints.cs, AuthService.cs) — this spec describes what to keep, what to change, and what to add now that schema-per-tenant (feature 002) is in place."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Email/password sign-in resolves the correct organisation (Priority: P1)

A director, caregiver, or parent opens their app and signs in with the email and password tied to their account. The system identifies which organisation (tenant) that account belongs to, verifies the password against that tenant's record, and returns a session (access token + refresh token) scoped to that organisation. No part of this flow depends on a single hard-coded "default" organisation standing in for the real one.

**Why this priority**: Every other product capability sits behind this. Today the system fakes tenant resolution by always picking the earliest-created organisation — a placeholder ("research.md R7") that must be replaced before any second organisation can safely onboard a user. This is the single riskiest gap blocking real usage.

**Independent Test**: Create two organisations, each with a user sharing a distinguishable email domain. Sign in as a user in organisation B and confirm the returned session is scoped to organisation B's schema, not organisation A's.

**Acceptance Scenarios**:

1. **Given** an organisation with a registered, verified user, **When** that user submits their organisation identifier, email, and correct password, **Then** the system returns an access token whose `tenant_id` claim matches that organisation and a refresh token scoped to that same organisation.
2. **Given** two different organisations each containing a user with the same email address, **When** either user signs in supplying their own organisation's identifier, **Then** the system authenticates against that specific organisation and account only, never the other organisation's account of the same email.
3. **Given** a user submits an incorrect password (organisation correctly identified), **When** they attempt sign-in, **Then** the system returns a generic authentication failure without revealing whether the email exists in that organisation.
4. **Given** an organisation whose provisioning status is not "ready", **When** a user tied to that organisation attempts sign-in, **Then** the system rejects the request.
5. **Given** no organisation identifier is supplied with a sign-in request, **When** the request is submitted, **Then** the system rejects it rather than guessing an organisation from the email.

---

### User Story 2 - Social sign-in for web admin and parent app (Priority: P2)

A director signs into the web admin using their Google account. A parent signs into the parent app using their Google account or Apple ID. In both cases the system verifies the identity token with the provider before trusting it, then links it to that person's existing account (found by email, within the organisation identified on the request) — it never creates a new account this way, since every account is created solely through an invitation-based provisioning flow (FR-009).

**Why this priority**: Google/Apple sign-in is a decided requirement for two of the three products and directly affects first-run conversion, but it is additive on top of the core email/password flow in User Story 1.

**Independent Test**: With a valid Google test account, call the Google sign-in endpoint with a real ID token and confirm a session is issued; repeat with a tampered/expired token and confirm rejection. Same for Apple with a JWKS-signed identity token.

**Acceptance Scenarios**:

1. **Given** a Google ID token that passes Google's tokeninfo validation and whose audience is on the allowed-clients list, **and** an existing account in the specified organisation matches the token's email, **When** a director signs in via the web admin, **Then** the system links the Google identity to that existing account (if not already linked) and issues a session.
2. **Given** a Google or Apple token that is valid but whose email matches no existing account in the specified organisation, **When** sign-in is attempted, **Then** the system refuses to create a new account and returns the same generic authentication failure as an unknown password-based account.
3. **Given** an Apple identity token that passes JWKS signature/issuer/audience validation and matches an existing parent account, **When** that parent signs in via the parent app for the first time with Apple, **Then** the system requires and stores the email supplied at that first sign-in (Apple only sends it once) to complete the match, and issues a session.
4. **Given** a Google or Apple token that fails server-side validation, **When** sign-in is attempted, **Then** the system rejects the request without issuing any session, regardless of what the client claims about the user's identity.

---

### User Story 3 - Role-based access control across the three products (Priority: P3)

An endpoint that only a director should reach (e.g. inviting staff) is protected so that a caregiver's or parent's valid, otherwise-authenticated session is refused access. An endpoint open to both directors and caregivers accepts either but refuses a parent's session. This is enforced centrally via named authorization policies, not by scattered `if (role == ...)` checks inside endpoint handlers.

**Why this priority**: Every Phase 1 feature built after this one (locations, staff, children, contracts, attendance, etc.) depends on `DirectorOnly` / `StaffOrDirector` / `ParentOnly` policies already existing. Without this, each subsequent feature would invent its own ad hoc role check.

**Independent Test**: Create one account per role. Call a `DirectorOnly`-protected test endpoint with each account's token and confirm only the director's succeeds (403 for the other two). Repeat for `StaffOrDirector` and `ParentOnly`.

**Acceptance Scenarios**:

1. **Given** an authenticated user with role Director, **When** they call an endpoint guarded by the `DirectorOnly` policy, **Then** the request succeeds.
2. **Given** an authenticated user with role Staff or Parent, **When** they call an endpoint guarded by the `DirectorOnly` policy, **Then** the request is refused with a 403, not a 401 (they are authenticated, just not authorized).
3. **Given** an authenticated user with role Parent, **When** they call an endpoint guarded by the `StaffOrDirector` policy, **Then** the request is refused.
4. **Given** any authenticated user regardless of role, **When** their JWT is valid but does not carry a recognized role claim, **Then** every role-guarded policy refuses the request (fail closed).

---

### User Story 4 - Per-device session management continues to work under real tenant resolution (Priority: P4)

A caregiver logs into the caregiver app on a tablet and, separately, into a personal phone browser session for testing. Logging out from the tablet does not sign them out of the phone session. A stolen refresh token cannot be replayed indefinitely — once used, it is rotated. A password reset invalidates every existing session for that account across all devices.

**Why this priority**: This behavior already exists in the current skeleton; the requirement here is to preserve it unchanged as tenant resolution is rebuilt underneath it, not to add new capability.

**Independent Test**: Sign in twice with the same account from two simulated devices, capturing two distinct refresh tokens. Log out using the first token and confirm the second still refreshes successfully. Trigger a password reset and confirm both refresh tokens are subsequently rejected.

**Acceptance Scenarios**:

1. **Given** a user signed in on two devices, **When** one device logs out, **Then** the other device's session remains valid.
2. **Given** a valid refresh token, **When** it is used to obtain a new access token, **Then** the old refresh token is invalidated and a new one is issued in its place.
3. **Given** a completed password reset, **When** any previously issued refresh token for that account is used afterward, **Then** it is rejected.

---

### Edge Cases

- A parent has a child enrolled at two different KDVs (two different organisations) and uses the same email for both. Each is a fully separate account; the client always supplies which organisation to sign into (see User Story 1, Scenarios 2 and 5), so signing in must never mix data or sessions between them.
- A staff member is transferred from one location to another within the same organisation: their account and role are unaffected; only their location assignment (handled by a later feature) changes.
- An expired or already-used password-reset / email-verification token must be rejected the same way regardless of *why* it's invalid (expired vs. never existed), so a caller cannot distinguish account existence by response shape.
- A Google or Apple account is linked to an email that already has a password-based account: sign-in via that provider links to the existing account rather than creating a duplicate.
- A Google or Apple sign-in is attempted with a valid, provider-verified token whose email has no matching account in the specified organisation: no account is created; the attempt is refused the same way an unrecognized password-based login would be (FR-009 applies identically regardless of which sign-in method is used).
- An organisation is suspended or its provisioning status changes to something other than "ready" after users already hold valid, unexpired JWTs: the next refresh (or any request requiring re-resolution of the tenant) must be rejected once the middleware re-checks tenant status; a still-valid unexpired access token is a known, accepted exposure window bounded by its 15-minute lifetime.

## Requirements *(mandatory)*

### Functional Requirements

**Kept from the existing skeleton (behavior must not regress):**

- **FR-001**: System MUST issue short-lived JWT access tokens (15-minute expiry) and rotate per-device refresh tokens (30-day expiry, one active token per device) on every successful authentication.
- **FR-002**: System MUST rate-limit authentication endpoints using the existing tiers (strict for login/register-class endpoints, generous for OAuth, dedicated for refresh).
- **FR-003**: System MUST validate Google ID tokens server-side via Google's tokeninfo endpoint, checking email verification and an allow-listed audience, before ever issuing a session.
- **FR-004**: System MUST validate Apple identity tokens server-side against Apple's published JWKS (issuer, audience, signature, expiry) before ever issuing a session.
- **FR-005**: System MUST support independent per-device logout that revokes only the calling device's refresh token.
- **FR-006**: System MUST support email verification and password-reset flows, and a password reset MUST invalidate all of that account's existing refresh tokens across all devices.
- **FR-007**: System MUST apply the existing security headers to all responses, unchanged.

**Changed:**

- **FR-008**: System MUST resolve which organisation (tenant) an email/password or OAuth sign-in belongs to from the organisation identifier supplied with the request (FR-016), replacing the current placeholder that always resolves to a single hard-coded "default" organisation regardless of who is signing in.
- **FR-009**: System MUST NOT create a new account as a side effect of any authentication attempt, by any method (password, Google, or Apple); every account is created solely as a direct consequence of an existing organisation-, staff-, or contact-provisioning flow (director via organisation onboarding; staff and parent accounts via their respective future features). The current generic `/api/auth/register` endpoint MUST be removed, and Google/Apple sign-in MUST be changed from its current behavior of auto-creating an account when none matches to instead refusing the attempt in that case.
- **FR-010**: All account/session write operations previously implemented as direct service-class calls (register, login, OAuth sign-in, refresh, logout, password reset, email verification) MUST be re-implemented as MediatR commands with FluentValidation pipeline behavior, per the project's CQRS/thin-endpoint requirement — endpoint handlers MUST contain no business logic.
- **FR-011**: Every issued JWT access token MUST carry the user's role in addition to the existing user-id, email, and `tenant_id` claims.

**Added:**

- **FR-012**: Every user account MUST have exactly one role: Director, Staff, or Parent.
- **FR-013**: System MUST provide named authorization policies — `DirectorOnly`, `StaffOrDirector`, `ParentOnly` — usable on any endpoint via declarative policy attachment, with no hardcoded role-comparison logic inside endpoint handlers.
- **FR-014**: A request whose JWT is valid but lacks a recognized role claim, or targets a policy the role does not satisfy, MUST be refused (403) rather than granted by default.
- **FR-015**: System MUST reject sign-in, refresh, and OAuth requests for an organisation whose provisioning status is not "ready".
- **FR-016**: Every request that identifies an account by email rather than by an already-issued token — sign-in (email/password, Google, or Apple) and "forgot password" — MUST include a client-supplied organisation identifier (e.g. an org slug the app already knows or the user has selected); the system MUST authenticate/act only within that organisation and MUST NOT attempt to resolve an account from email alone when no organisation identifier is supplied. Requests carrying an already-issued, effectively-unguessable token (refresh, password-reset, email-verification) MAY instead recover the organisation from that token's own context, since the token itself was only ever handed to one specific organisation's account holder.
- **FR-017**: System MUST reject a sign-in attempt via a method not permitted for that account's role (web admin: password + Google; caregiver app: password only; parent app: password + Google + Apple), enforced server-side regardless of which client made the request — not merely relied upon each app's UI to omit the option.

### Key Entities

- **User Account**: One per person per organisation. Holds email, password hash (nullable for pure-OAuth accounts), optional linked Google/Apple identity, verification state, and exactly one role (Director, Staff, Parent). Already exists as `TenantUser`; this feature adds the role.
- **Organisation Identifier**: The existing tenant `slug` (public schema), supplied by the client on every sign-in request to route it to the correct tenant schema before any account lookup happens — no cross-tenant email index is needed once the organisation is known upfront.
- **Refresh Token**: One active token per device per user account, rotated on use, revocable individually (logout) or in bulk (password reset).
- **Authorization Policy**: A named, declarative rule (`DirectorOnly`, `StaffOrDirector`, `ParentOnly`) mapping a user's role claim to endpoint access, independent of any specific feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can sign in with an organisation identifier, email, and password and receive a usable session in under 2 seconds under normal load (defined as up to 50 concurrent authentication requests, consistent with this feature's Phase 1 scale of dozens to low hundreds of organisations), with the correct organisation authenticated against 100% of the time, including for accounts sharing an email across different organisations.
- **SC-002**: 100% of endpoints added by every subsequent feature can declare their access requirement using one of the three named policies, with zero endpoint handlers containing an inline role comparison.
- **SC-003**: Zero valid sessions are issued for tokens that fail Google/Apple server-side validation, verified by negative testing (tampered, expired, wrong-audience tokens all rejected).
- **SC-004**: A logged-out device's refresh token can never be used again to obtain a new session; a used refresh token can never be replayed to obtain a second new session.
- **SC-005**: No authentication failure response (bad password, unknown email, expired reset/verification token) reveals, from response alone, whether a given email is registered anywhere in the system.

## Assumptions

- Staff accounts (feature 005) and parent accounts (features 006/012) will call into the same account-provisioning primitives this feature establishes (role assignment, tenant-scoped `TenantUser` creation); this feature does not itself build staff- or parent-invitation UI, only the underlying auth/authorization mechanics they'll rely on.
- Existing `TenantUser` rows created by organisation onboarding (feature 001) are all directors; the migration that adds the role column backfills them as Director.
- The current generic `/api/auth/register` endpoint is dead code from the pre-tenancy walking skeleton (nothing in the organisation-onboarding flow calls it) and is safe to remove outright rather than adapt.
- "Fail closed" for authorization means a missing or unrecognized role claim is treated as "no access," never as an implicit allow.

## Clarifications

### Session 2026-07-04

- Q: When the same email has separate accounts in two different organisations, how should login determine which organisation to authenticate against? → A: The client supplies the organisation identifier (e.g. slug) alongside email/password on every sign-in request; the server authenticates only within that organisation and never resolves an account from email alone.
- Q: Should the backend enforce which sign-in method (password/Google/Apple) is valid per role, or leave that entirely to each client's UI? → A: The backend enforces it server-side, independent of which client makes the call.
