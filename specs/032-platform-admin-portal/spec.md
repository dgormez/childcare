# Feature Specification: Platform-Admin Portal — Invitations & Shared Shell

**Feature Branch**: `032-platform-admin-portal`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Build a dedicated platform-admin (super-admin) portal — sits above all tenant boundaries. Director/organisation invitations (create/list/resend/revoke, reusing feature 001's invitation-token model). Platform data management shell reusable by future datasets. Auth, portal-vs-separate-tool, and first-platform-admin-provisioning questions are already resolved by feature 013h's precedent; audit trail follows 013h's established VaccineType convention."

## Clarifications

### Session 2026-07-23

- Q: How should an invitation that's been superseded (by a resend, or by creating a new invitation for the same email) be represented in the status model? → A: Superseded invitations show as "Revoked" — no 5th status; the superseded row's revoke-attribution fields are populated with whichever platform-admin performed the create/resend action that superseded it, at that same moment. The data model makes no distinction between an explicit manual revoke and a supersede-triggered one — both are simply "Revoked" rows in the list.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Platform-admin invites a prospective director (Priority: P1)

A platform-admin has been in a sales/onboarding conversation with a prospective KDV director and needs to give them a way to create their organisation's account, without anyone touching the database directly.

**Why this priority**: This is the entire reason the feature exists — feature 001 built acceptance but never creation, so today this is a manual token/DB operation for every single new customer. This is the MVP; everything else is secondary.

**Independent Test**: Can be fully tested by a platform-admin submitting an email (+ optional organisation-name note) on the Invitations screen and confirming a real invitation record is created with a working registration link — delivers value standalone even before resend/revoke exist.

**Acceptance Scenarios**:

1. **Given** a platform-admin is on the Invitations screen, **When** they submit a prospective director's email and an organisation-name note, **Then** a new invitation is created, shown in the list with status "Pending", and an email containing the registration link is sent to that address.
2. **Given** an invitation was just created for `jane@example.com`, **When** the platform-admin submits another invitation for the same email, **Then** the prior invitation's status becomes "Revoked" (superseded, unusable for registration) and the new one is the only active one for that email — mirroring the existing supersede behavior in the invitation-creation logic.
3. **Given** a platform-admin who is a director but does NOT have platform-admin authority, **When** they attempt to reach the Invitations screen or call its API directly, **Then** access is denied.

---

### User Story 2 - Platform-admin tracks and manages invitation status (Priority: P2)

A platform-admin needs to see which invitations are still waiting, which turned into real organisations, and which went stale — and act on the stale/pending ones.

**Why this priority**: Creation alone (P1) delivers the core value, but without visibility a platform-admin has no way to know if an invitation was ever used, or to clean up ones that should not still be usable — this is what makes the feature trustworthy for repeated use rather than a one-shot fix.

**Independent Test**: Can be fully tested by creating a few invitations in different states (fresh, expired, accepted) and confirming the list shows the correct status for each, independent of whether resend/revoke actions are ever used.

**Acceptance Scenarios**:

1. **Given** invitations exist in different states, **When** the platform-admin opens the Invitations screen, **Then** each row shows one of: Pending, Accepted, Expired, or Revoked, each with a distinct semantic color and icon.
2. **Given** a prospective director completed registration using their invitation link, **When** the platform-admin views the list, **Then** that invitation shows "Accepted" and no longer offers resend/revoke actions.
3. **Given** a pending invitation, **When** the platform-admin clicks "Resend", **Then** a fresh invitation (new token, new expiry) is created for the same email and email note, the old one's status becomes "Revoked" (superseded), and a new email is sent.
4. **Given** a pending invitation the platform-admin no longer wants usable (e.g. sent to the wrong address), **When** they click "Revoke", **Then** the invitation immediately stops being usable for registration and its status shows "Revoked", with a record of who revoked it and when.
5. **Given** an invitation whose expiry date has passed and was never accepted or revoked, **When** the platform-admin views the list, **Then** it shows "Expired" (derived automatically, no manual action required).

---

### User Story 3 - Platform-admin navigates a unified portal shell (Priority: P3)

A platform-admin needs one consistent place in the app to find every cross-tenant capability (today: the vaccine catalog from feature 013h; now: invitations), rather than each capability being its own disconnected page.

**Why this priority**: Lower priority than the invitation functionality itself (P1/P2 deliver the actual business value), but necessary so a second dataset doesn't force a shell rebuild — the explicit ask from BACKLOG.md's feature 032 entry — and to retire the single hardcoded nav entry 013h shipped as a placeholder.

**Independent Test**: Can be fully tested by logging in as a platform-admin and confirming the sidebar's platform-admin section lists both "Invitations" and "Vaccine Types" as sibling entries, each navigating correctly, independent of any invitation being created.

**Acceptance Scenarios**:

1. **Given** a platform-admin is logged in, **When** they look at the sidebar, **Then** they see a distinctly-separated "Platform Administration" section listing both Invitations and Vaccine Types.
2. **Given** a non-platform-admin director is logged in, **When** they look at the sidebar, **Then** no Platform Administration section appears at all.
3. **Given** a platform-admin navigates directly to a platform-admin URL by typing it, **When** the page loads, **Then** the same shared shell (heading, section nav) renders around the page's own content, consistent with every other platform-admin screen.

---

### Edge Cases

- What happens when a platform-admin tries to revoke an invitation that was already accepted? The action is unavailable (no Revoke control shown) since the invitation is no longer pending — Accepted and Revoked are mutually exclusive end states.
- What happens when a platform-admin tries to resend an invitation that already expired? Resend is still offered for Expired (not just Pending) invitations, since the whole point of resend is to give a prospective director a fresh, working link after their old one went stale.
- What happens if two platform-admins act on the same invitation at nearly the same time (e.g. one resends while another revokes)? The second write wins per normal last-write-semantics; no distributed-lock/optimistic-concurrency mechanism is introduced, consistent with how every other admin action in this codebase (e.g. vaccine-type edits) already works.
- What happens when the organisation-name note is left blank? It's optional — the invitation is still created and sent, just without a note shown in the list (the authoritative organisation name is still whatever the director enters at registration, unchanged from today).
- What happens when an email is sent to an address that was already used to complete registration previously (a different, older, now-Accepted invitation)? The system does not block sending a new invitation to a previously-registered email — this mirrors an intentional gap already in feature 001's flow (nothing today prevents inviting the same email twice over time) and is out of scope to change here.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a platform-admin to create a new director/organisation invitation by providing an email address (required) and an organisation-name note (optional, informational only).
- **FR-002**: The system MUST reuse the existing invitation-token model (signed opaque token, SHA-256 hash-only persistence, expiry) rather than introducing a second token mechanism.
- **FR-003**: The system MUST send an email to the invited address containing the registration link, at invitation creation and at resend.
- **FR-004**: The system MUST derive and display an invitation's status as exactly one of: Pending, Accepted, Expired, or Revoked — never stored as a separately-settable field that could drift from the underlying facts (token expiry, whether a resulting organisation was registered, whether a revoke action was recorded). A superseded invitation (see FR-005) is represented as Revoked — there is no separate "superseded" status.
- **FR-005**: The system MUST allow a platform-admin to resend a Pending or Expired invitation, which creates a fresh invitation (new token, new expiry) for the same email/note and marks the prior one Revoked (attributed to the platform-admin who triggered the resend, at that moment) so only the newest is usable. Creating a new invitation for an email with an existing Pending or Expired invitation follows the same supersede behavior.
- **FR-006**: The system MUST allow a platform-admin to revoke a Pending invitation, immediately and permanently preventing that invitation from completing registration. The system MUST NOT distinguish, in the data model or the list view, between this explicit action and a supersede-triggered Revoked state (FR-005) — both are the same Revoked status.
- **FR-007**: The system MUST NOT allow resend or revoke on an invitation that has already reached the Accepted state.
- **FR-008**: The system MUST record who performed a create/resend/revoke action and when, resolved from the authenticated platform-admin's session — never accepted as a client-supplied value.
- **FR-009**: The system MUST restrict every invitation-management capability (view, create, resend, revoke) to an authenticated director account with platform-admin authority, using the existing platform-admin authorization mechanism — no new authentication path.
- **FR-010**: The system MUST leave the existing invitation-acceptance/registration flow's behavior for a prospective director unchanged (a valid, unexpired, unrevoked, unused invitation link still completes registration exactly as it does today).
- **FR-011**: The system MUST present a single, shared platform-admin navigation section in the director-web app, listing every platform-admin capability (at minimum: Invitations, and the existing Vaccine Types) as sibling entries, replacing today's single hardcoded entry.
- **FR-012**: The system MUST hide the entire platform-admin navigation section from any director account without platform-admin authority.
- **FR-013**: The system MUST NOT change any existing tenant-facing behavior of platform-wide reference data (e.g. the vaccine-types read endpoint tenant screens already consume stays exactly as-is).

### Key Entities *(include if feature involves data)*

- **Invitation**: Represents an offer for a prospective director to register a new organisation. Attributes: invited email, an opaque single-use token (persisted only as a hash), expiry timestamp, an optional organisation-name note (informational, not authoritative), and revoke attribution (who revoked/superseded it and when, when applicable — the same fields serve both an explicit manual revoke and a resend/duplicate-create-triggered supersede, per the Clarifications above). Status is a derived, computed view over these facts plus whether a resulting organisation exists — not an independently-editable field. Lives in the platform's shared (cross-tenant) data, not inside any single organisation's data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A platform-admin can create and send a new organisation invitation in under 30 seconds, without any direct database or ops-tool access.
- **SC-002**: 100% of invitations created through the portal show an accurate, up-to-date status (Pending/Accepted/Expired/Revoked) reflecting real registration/expiry/revoke facts, with no manual reconciliation step.
- **SC-003**: A revoked or expired invitation can never be used to complete registration — verified as a hard guarantee, not a best-effort one.
- **SC-004**: A platform-admin reaches any platform-admin capability (Invitations or Vaccine Types) within one navigation click from anywhere in the director-web app.
- **SC-005**: A non-platform-admin director account has zero visibility into, or access to, any platform-admin capability.

## Assumptions

- The very first platform-admin account, and the mechanism for granting `IsPlatformAdmin` to any account, is entirely out of scope for this feature — feature 013h's `grant-platform-admin` CLI already handles this out-of-band, and this feature does not add an in-app way to grant that flag.
- The authentication path for a platform-admin is the existing director login (email/password, Google, or Apple) plus the existing `IsPlatformAdmin` flag and `PlatformAdminOnly` authorization policy from feature 013h — no new auth mechanism is introduced.
- This is a same-app, new-gated-route addition to the existing director-web Next.js app, not a separate internal tool — consistent with feature 013h's precedent and the project's monolith-first principle.
- The organisation-name note captured at invitation time is informational only for the platform-admin's own tracking; it does not pre-fill, constrain, or validate against the organisation name the director later chooses at registration.
- Sending invitation emails reuses this codebase's existing transactional-email sending capability (already used for daily reports, payment reminders, and other automated parent/director emails) rather than introducing a new provider or channel.
- Audit attribution (who/when) for create/resend/revoke follows the exact convention already established for platform-wide reference-data edits (feature 013h's `VaccineType` deactivation fields): a nullable acting-user id with no cross-schema foreign key, a denormalized acting-user email captured at the moment of the action, and a nullable action timestamp.
- Billing/subscription-plan management and tenant suspension/deletion tooling remain out of scope, per features 001 and 002's existing deferrals — unaffected by this feature.
- The vaccine-catalog management screen itself (013h) is not rebuilt or functionally changed by this feature — only its entry point is moved into the new shared navigation section.

## Product Context

### Feature Type

Mixed (API-backend capability + User-facing UI).

### Primary Consumer

Director (specifically one flagged `IsPlatformAdmin`) — the first feature since 013h where
the consumer is a cross-tenant platform-admin actor rather than a tenant-scoped one.

### Workflow Boundary

Platform Administration workflow (`.specify/memory/workflows.md`). This is the second
feature in that workflow (013h was the first) — it adds director/organisation invitation
management as a new "Includes" item, and adds the workflow's first detail file
(`Workflows/platform-administration.md`), since the workflow map has said "No detail file
yet" since 013h shipped. Cross-platform impact: backend + director-web only. No
caregiver-tablet or parent-mobile impact — a platform-admin is a logged-in director-web
user, full stop.

### User Impact

This enables a platform-admin to invite a prospective KDV director and have their
organisation provisioned without any manual token/DB operation, resulting in a
self-service onboarding path controlled entirely from within the app.

### UX Requirements

- **Persona**: platform-admin — an existing director account with extra cross-tenant
  authority (see the Platform Administration workflow entry).
- **Platform**: director-web only, desktop-first per `platform-rules.md` (min `1280px`,
  high-density tables, keyboard-navigable, visible focus rings).
- **User job**: send an invitation, track its status, resend or revoke it, without leaving
  the web app.
- **Success criteria**: an invitation created in-app reaches a prospective director's inbox
  and the resulting registration works identically to today's manual flow.
- **Main flow**: platform-admin nav section → Invitations screen → create (email + optional
  org-name note) → list with status badges (Pending/Accepted/Expired/Revoked, each paired
  with its own semantic color + icon per `design-system.md`'s Status Indicators rule) →
  resend/revoke row actions.
- **Loading/empty/error states**: per `design-system.md`'s Empty States convention (icon +
  one short human sentence).
- **Accessibility**: full keyboard navigation + visible focus rings per `platform-rules.md`'s
  Director Web section.
- **Offline behavior**: none required — director-web has no offline requirement anywhere in
  this codebase.

### Technical Requirements

- **API impact**: new `PlatformAdminOnly`-gated endpoints for invitation create/list/resend/
  revoke, under the same `/api/platform-admin/*` prefix convention 013h's vaccine-type
  endpoints already established.
- **Data-model impact**: a Public-schema migration extending `Invitation` with an
  organisation-name note and revoke attribution (acting-user id/email, timestamp).
- **Security considerations**: acting-user resolved server-side from JWT claims only, never
  client-supplied; revoke/resend must not introduce any new information-disclosure vector
  beyond what invitation-acceptance already generically handles.
- **Performance considerations**: not a concern at this scale — an internal operations tool,
  not a high-traffic path.
- **Testing requirements**: a policy-boundary test (platform-admin claim without director
  role is rejected, mirroring 013h's existing test), invitation lifecycle tests (create,
  resend-supersedes-prior, revoke, status-derivation for all four states), and a web
  component test for the extracted shared platform-admin shell/nav.
