# Feature Specification: Platform-Admin Portal — Invitations, Registration & Organisation Directory

**Feature Branch**: `032-platform-admin-portal`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Build a dedicated platform-admin (super-admin) portal — sits above all tenant boundaries. Director/organisation invitations (create/list/resend/revoke). A public self-service registration page (missing entirely today). An organisation directory (every onboarded organisation, read-only). A shared platform-admin shell reusable by future datasets. Auth, portal-vs-separate-tool, first-platform-admin-provisioning, and audit-trail questions are already resolved by feature 013h's precedent."

## Clarifications

### Session 2026-07-23 (initial)

- Q: How should an invitation that's been superseded (by a resend, or by creating a new invitation for the same email) be represented in the status model? → A: Superseded invitations show as "Revoked" — no 5th status; the superseded row's revoke-attribution fields are populated with whichever platform-admin performed the create/resend action that superseded it, at that same moment. The data model makes no distinction between an explicit manual revoke and a supersede-triggered one — both are simply "Revoked" rows in the list.

### Session 2026-07-23 (scope expansion, before planning)

While researching feature 013h's precedent for `/speckit-plan`, two facts came to light that changed this feature's scope before any code was written — resolved directly with the product owner via `AskUserQuestion` rather than assumed, per this pipeline's standing rule for genuinely novel scope questions:

- Q: There is no web page anywhere for a prospective director to complete registration — only feature 001's backend endpoint exists. Should 032 also build that page? → A: Yes — without it, an invitation created by this feature has nowhere to go.
- Q: Should the platform-admin's "overview" show every organisation on the platform, or only invitation-originated ones? → A: Every organisation (in practice the same set today, since feature 001 has no other creation path).
- Q: Does "approval/accepted states" mean a new manual-approval gate before an organisation activates? → A: No — visibility only. Registration still activates an organisation immediately, exactly as feature 001 already built it.
- Q: Should this land as one feature or be split across multiple BACKLOG items? → A: One feature (032).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Platform-admin invites a prospective director (Priority: P1)

A platform-admin has been in a sales/onboarding conversation with a prospective KDV director and needs to give them a way to create their organisation's account, without anyone touching the database directly.

**Why this priority**: This is the entry point to the entire flow — without it, nothing downstream (registration, the directory) has a starting point. Feature 001 built acceptance but never creation, so today this is a manual token/DB operation for every single new customer.

**Independent Test**: Can be fully tested by a platform-admin submitting an email (+ optional organisation-name note) on the Invitations screen and confirming a real invitation record is created — delivers value standalone even before the registration page or directory exist.

**Acceptance Scenarios**:

1. **Given** a platform-admin is on the Invitations screen, **When** they submit a prospective director's email and an organisation-name note, **Then** a new invitation is created, shown in the list with status "Pending", and an email containing the registration link is sent to that address.
2. **Given** an invitation was just created for `jane@example.com`, **When** the platform-admin submits another invitation for the same email, **Then** the prior invitation's status becomes "Revoked" (superseded, unusable for registration) and the new one is the only active one for that email.
3. **Given** a platform-admin who is a director but does NOT have platform-admin authority, **When** they attempt to reach the Invitations screen or call its API directly, **Then** access is denied.

---

### User Story 2 - Prospective director completes registration (Priority: P1)

A prospective director receives an invitation email, clicks the link, and needs a page where they can actually create their organisation's account — today this page does not exist anywhere, so the link goes nowhere.

**Why this priority**: Co-equal with User Story 1 — an invitation with no working destination delivers zero real value. This is the other half of "self-service onboarding," the feature's entire reason for existing.

**Independent Test**: Can be fully tested by taking any valid, unexpired, unrevoked invitation (created directly via existing infrastructure, independent of User Story 1's own UI) and completing the registration form — delivers value standalone as long as some invitation exists.

**Acceptance Scenarios**:

1. **Given** a prospective director opens a valid, unexpired invitation link, **When** the page loads, **Then** they see a registration form asking for organisation name, director name, email (pre-filled from the invitation, not editable), and password.
2. **Given** a prospective director fills in the form correctly, **When** they submit, **Then** their organisation is created and immediately usable — they can log in right away, exactly as feature 001's existing registration flow already behaves. No approval step, no waiting period.
3. **Given** a prospective director opens an expired or revoked invitation link, **When** the page loads, **Then** they see a clear message that the link is no longer valid, with no way to guess whether it was expired, revoked, or never existed (matching the existing generic not-found handling on the acceptance endpoint).
4. **Given** a prospective director already completed registration using this link once, **When** they (or anyone else) open the same link again, **Then** they see the same "no longer valid" message — the link cannot be used twice.

---

### User Story 3 - Platform-admin tracks and manages invitation status (Priority: P2)

A platform-admin needs to see which invitations are still waiting, which turned into real organisations, and which went stale — and act on the stale/pending ones.

**Why this priority**: Creation and completion (P1s) deliver the core value end-to-end, but without visibility a platform-admin has no way to know if an invitation was ever used, or to clean up ones that should not still be usable.

**Independent Test**: Can be fully tested by creating a few invitations in different states (fresh, expired, accepted) and confirming the list shows the correct status for each, independent of whether resend/revoke actions are ever used.

**Acceptance Scenarios**:

1. **Given** invitations exist in different states, **When** the platform-admin opens the Invitations screen, **Then** each row shows one of: Pending, Accepted, Expired, or Revoked, each with a distinct semantic color and icon.
2. **Given** a prospective director completed registration using their invitation link, **When** the platform-admin views the list, **Then** that invitation shows "Accepted" and no longer offers resend/revoke actions.
3. **Given** a pending invitation, **When** the platform-admin clicks "Resend", **Then** a fresh invitation (new token, new expiry) is created for the same email and email note, the old one's status becomes "Revoked" (superseded), and a new email is sent.
4. **Given** a pending invitation the platform-admin no longer wants usable (e.g. sent to the wrong address), **When** they click "Revoke", **Then** the invitation immediately stops being usable for registration and its status shows "Revoked", with a record of who revoked it and when.
5. **Given** an invitation whose expiry date has passed and was never accepted or revoked, **When** the platform-admin views the list, **Then** it shows "Expired" (derived automatically, no manual action required).

---

### User Story 4 - Platform-admin views the organisation directory (Priority: P2)

A platform-admin needs to see, in one place, every KDV organisation already on the platform — today answering "how many customers do we have" or "when did this org sign up" requires a direct database query.

**Why this priority**: Equal weight to invitation-status tracking (P2) — both are visibility features that make the portal trustworthy for repeated use, distinct from the P1 create/complete flows that deliver the transactional value.

**Independent Test**: Can be fully tested by viewing the directory against organisations that already exist from prior features' test data, independent of any new invitation being created during this test.

**Acceptance Scenarios**:

1. **Given** organisations exist on the platform, **When** the platform-admin opens the Organisations screen, **Then** they see every organisation's name, plan, provisioning status, KBO number (if set), creation date, and the email address that registered it.
2. **Given** the platform-admin is viewing the directory, **When** they look for a way to suspend, deactivate, or edit an organisation, **Then** no such action exists — the directory is read-only visibility, not an administrative control surface.
3. **Given** an organisation's provisioning failed (a rare operational fault, not an admin action), **When** the platform-admin views the directory, **Then** that organisation's status is visibly distinct from a normally-onboarded one.

---

### User Story 5 - Platform-admin navigates a unified portal shell (Priority: P3)

A platform-admin needs one consistent place in the app to find every cross-tenant capability (today: the vaccine catalog from feature 013h; now: invitations and the organisation directory), rather than each capability being its own disconnected page.

**Why this priority**: Lower priority than the functional capabilities themselves (P1/P2 deliver the actual business value), but necessary so a future dataset doesn't force a shell rebuild, and to retire the single hardcoded nav entry 013h shipped as a placeholder.

**Independent Test**: Can be fully tested by logging in as a platform-admin and confirming the sidebar's platform-admin section lists "Invitations", "Organisations", and "Vaccine Types" as sibling entries, each navigating correctly.

**Acceptance Scenarios**:

1. **Given** a platform-admin is logged in, **When** they look at the sidebar, **Then** they see a distinctly-separated "Platform Administration" section listing Invitations, Organisations, and Vaccine Types.
2. **Given** a non-platform-admin director is logged in, **When** they look at the sidebar, **Then** no Platform Administration section appears at all.
3. **Given** a platform-admin navigates directly to a platform-admin URL by typing it, **When** the page loads, **Then** the same shared shell (heading, section nav) renders around the page's own content, consistent with every other platform-admin screen.

---

### Edge Cases

- What happens when a platform-admin tries to revoke an invitation that was already accepted? The action is unavailable (no Revoke control shown) since the invitation is no longer pending — Accepted and Revoked are mutually exclusive end states.
- What happens when a platform-admin tries to resend an invitation that already expired? Resend is still offered for Expired (not just Pending) invitations, since the whole point of resend is to give a prospective director a fresh, working link after their old one went stale.
- What happens if two platform-admins act on the same invitation at nearly the same time (e.g. one resends while another revokes)? The second write wins per normal last-write-semantics; no distributed-lock/optimistic-concurrency mechanism is introduced, consistent with how every other admin action in this codebase already works.
- What happens when the organisation-name note is left blank at invitation time? It's optional — the invitation is still created and sent, just without a note shown in the list (the authoritative organisation name is still whatever the director enters at registration).
- What happens when an email is sent to an address that was already used to complete registration previously (a different, older, now-Accepted invitation)? The system does not block sending a new invitation to a previously-registered email — an intentional, pre-existing gap in feature 001's flow, out of scope to change here.
- What happens if a prospective director's chosen organisation name collides with an existing one? Unchanged from feature 001's existing behavior on the registration endpoint — this feature's new page surfaces whatever validation/error feature 001 already returns, it does not introduce new collision logic.
- What happens when a prospective director abandons the registration form partway through and returns later using the same link? As long as the invitation hasn't expired or been revoked in the meantime, the link still works — nothing is consumed by merely loading the page, only by successful submission.
- What happens to the organisation directory when an invitation is still Pending (no organisation created yet)? It does not appear in the directory at all — the directory only ever lists actual organisations (which only exist after successful registration); pending/expired/revoked invitations remain visible only on the Invitations screen.

## Requirements *(mandatory)*

### Functional Requirements

#### Invitations

- **FR-001**: The system MUST allow a platform-admin to create a new director/organisation invitation by providing an email address (required), an organisation-name note (optional, informational only), and the language to send the invitation email in (Dutch, French, or English — defaulting to Dutch, this platform's primary market).
- **FR-002**: The system MUST reuse the existing invitation-token model (signed opaque token, SHA-256 hash-only persistence, expiry) rather than introducing a second token mechanism.
- **FR-003**: The system MUST send an email to the invited address containing the registration link, at invitation creation and at resend.
- **FR-004**: The system MUST derive and display an invitation's status as exactly one of: Pending, Accepted, Expired, or Revoked — never stored as a separately-settable field that could drift from the underlying facts. A superseded invitation (see FR-005) is represented as Revoked — there is no separate "superseded" status.
- **FR-005**: The system MUST allow a platform-admin to resend a Pending or Expired invitation, which creates a fresh invitation (new token, new expiry) for the same email/note and marks the prior one Revoked (attributed to the platform-admin who triggered the resend, at that moment) so only the newest is usable. Creating a new invitation for an email with an existing Pending or Expired invitation follows the same supersede behavior.
- **FR-006**: The system MUST allow a platform-admin to revoke a Pending invitation, immediately and permanently preventing that invitation from completing registration. The system MUST NOT distinguish, in the data model or the list view, between this explicit action and a supersede-triggered Revoked state (FR-005) — both are the same Revoked status.
- **FR-007**: The system MUST NOT allow resend or revoke on an invitation that has already reached the Accepted state.
- **FR-008**: The system MUST record who performed a create/resend/revoke action and when, resolved from the authenticated platform-admin's session — never accepted as a client-supplied value.

#### Registration

- **FR-009**: The system MUST provide a public, unauthenticated web page where a prospective director can complete registration using a valid invitation link — the invited email pre-filled and not editable, plus organisation name, director name, and password as input.
- **FR-010**: The system MUST leave the existing invitation-acceptance/registration behavior unchanged (a valid, unexpired, unrevoked, unused invitation completes registration exactly as feature 001 already built it — immediate activation, no approval step, no waiting period).
- **FR-011**: The system MUST show a clear, non-specific "this invitation link is no longer valid" message for an expired, revoked, already-used, or never-existent invitation token — never revealing which of those reasons applies (preserves the existing generic not-found handling).

#### Organisation directory

- **FR-012**: The system MUST show a platform-admin a directory of every organisation on the platform: name, plan, provisioning status, KBO number (when set), creation date, and the email address that registered it.
- **FR-013**: The system MUST NOT provide any action to suspend, deactivate, edit, or delete an organisation from this directory — it is read-only visibility only.

#### Access & shell

- **FR-014**: The system MUST restrict every capability in this feature (invitation view/create/resend/revoke, organisation directory) to an authenticated director account with platform-admin authority, using the existing platform-admin authorization mechanism — no new authentication path. The registration page (FR-009) is the sole exception, being necessarily unauthenticated.
- **FR-015**: The system MUST present a single, shared platform-admin navigation section in the director-web app, listing every platform-admin capability (at minimum: Invitations, Organisations, and the existing Vaccine Types) as sibling entries, replacing today's single hardcoded entry.
- **FR-016**: The system MUST hide the entire platform-admin navigation section from any director account without platform-admin authority.
- **FR-017**: The system MUST NOT change any existing tenant-facing behavior of platform-wide reference data (e.g. the vaccine-types read endpoint tenant screens already consume stays exactly as-is).

### Key Entities *(include if feature involves data)*

- **Invitation**: Represents an offer for a prospective director to register a new organisation. Attributes: invited email, an opaque single-use token (persisted only as a hash), expiry timestamp, an optional organisation-name note (informational, not authoritative), and revoke attribution (who revoked/superseded it and when, when applicable — the same fields serve both an explicit manual revoke and a resend/duplicate-create-triggered supersede). Status is a derived, computed view over these facts plus whether a resulting organisation exists. Lives in the platform's shared (cross-tenant) data.
- **Organisation** (existing entity, read-only in this feature): The record created when a prospective director's registration completes. This feature adds no new attributes to it — the directory (FR-012) surfaces fields that already exist (name, plan, provisioning status, KBO number, creation date) plus the registering email, traced back through the Invitation that produced it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A platform-admin can create and send a new organisation invitation in under 30 seconds, without any direct database or ops-tool access.
- **SC-002**: A prospective director can go from clicking an invitation email to a working, logged-in-ready organisation account without any assistance from a platform-admin or developer.
- **SC-003**: 100% of invitations show an accurate, up-to-date status (Pending/Accepted/Expired/Revoked) reflecting real registration/expiry/revoke facts, with no manual reconciliation step.
- **SC-004**: A revoked or expired invitation, or one already used once, can never be used to complete registration a second time — a hard guarantee, not a best-effort one.
- **SC-005**: A platform-admin can answer "how many organisations exist and when did each sign up" by looking at one screen, with no database access.
- **SC-006**: A platform-admin reaches any platform-admin capability (Invitations, Organisations, or Vaccine Types) within one navigation click from anywhere in the director-web app.
- **SC-007**: A non-platform-admin director account has zero visibility into, or access to, any platform-admin capability.

## Assumptions

- The very first platform-admin account, and the mechanism for granting `IsPlatformAdmin` to any account, is entirely out of scope for this feature — feature 013h's `grant-platform-admin` CLI already handles this out-of-band.
- The authentication path for a platform-admin is the existing director login (email/password, Google, or Apple) plus the existing `IsPlatformAdmin` flag and `PlatformAdminOnly` authorization policy from feature 013h — no new auth mechanism is introduced.
- This is a same-app, new-gated-route addition to the existing director-web Next.js app, not a separate internal tool — consistent with feature 013h's precedent and the project's monolith-first principle.
- The organisation-name note captured at invitation time is informational only for the platform-admin's own tracking; it does not pre-fill, constrain, or validate against the organisation name the director later chooses at registration.
- Sending invitation emails reuses this codebase's existing transactional-email sending capability rather than introducing a new provider or channel.
- Audit attribution (who/when) for create/resend/revoke follows the exact convention already established for platform-wide reference-data edits (feature 013h's `VaccineType` deactivation fields): a nullable acting-user id with no cross-schema foreign key, a denormalized acting-user email captured at the moment of the action, and a nullable action timestamp.
- The registration page's content is fully localized (Dutch/French/English), consistent with Principle IV (non-negotiable) — it does not repeat the older "accepted English-only gap" feature 020 documented for pre-020 transactional flows, since that gap was explicitly scoped to flows that already existed before 020, not new ones. It uses the same pattern as the existing public enrollment page (feature 023): its own nested locale-message loading and an in-page language toggle, since the prospective director has no stored locale preference yet (defaulting to Dutch). The invitation email itself is likewise sent in a real language, not hardcoded English — the platform-admin picks it at invitation-creation time (FR-001), defaulting to Dutch.
- The organisation directory (FR-012) is read directly from existing, already-persisted data (the organisation record plus the invitation that produced it) — no new data is captured or computed, and no per-tenant-schema query is required, since every field the directory shows already lives in the platform's shared, cross-tenant data.
- "Provisioning status" shown in the directory reflects whether an organisation's technical setup completed successfully — it is not, and this feature does not turn it into, an admin-controlled active/suspended toggle. Tenant suspension remains fully out of scope (feature 002's existing deferral).
- Billing/subscription-plan management remains out of scope, per feature 001's existing deferral — unaffected by this feature.
- The vaccine-catalog management screen itself (013h) is not rebuilt or functionally changed by this feature — only its entry point is moved into the new shared navigation section.

## Product Context

### Feature Type

Mixed (API-backend capability + User-facing UI).

### Primary Consumer

Two distinct actors: the **platform-admin** (an existing director account flagged
`IsPlatformAdmin`) for invitation management, the organisation directory, and the portal
shell; and a **prospective director** (no account yet) for the registration page — the only
part of this feature reachable without authentication.

### Workflow Boundary

Platform Administration workflow (`.specify/memory/workflows.md`). This is the second feature
in that workflow (013h was the first) — it adds director/organisation invitation management,
the self-service registration completion step, and the organisation directory as new
"Includes" items, and adds the workflow's first detail file
(`Workflows/platform-administration.md`), since the workflow map has said "No detail file yet"
since 013h shipped. Cross-platform impact: backend + director-web only. No caregiver-tablet or
parent-mobile impact.

### User Impact

This enables a platform-admin to invite a prospective KDV director, have that director
complete registration entirely on their own, and see every organisation already on the
platform — closing the loop on self-service onboarding end-to-end, with no manual token/DB
operation and no ops-assisted registration step.

### UX Requirements

- **Persona**: platform-admin (existing director account with extra cross-tenant authority)
  for the portal itself; a prospective director (no account, no locale preference yet) for the
  standalone registration page.
- **Platform**: director-web only, desktop-first per `platform-rules.md` (min `1280px`,
  high-density tables, keyboard-navigable, visible focus rings) for the portal; the
  registration page is public-facing but still director-web (desktop-first), not a mobile app.
- **User job (platform-admin)**: send an invitation, track its status, resend or revoke it,
  and browse the organisation directory, without leaving the web app.
- **User job (prospective director)**: land on a working registration form from the emailed
  link and complete it in one sitting.
- **Success criteria**: an invitation created in-app reaches a prospective director's inbox,
  and the resulting registration works end-to-end with no assistance.
- **Main flow (platform-admin)**: platform-admin nav section → Invitations screen → create
  (email + optional org-name note) → list with status badges (Pending/Accepted/Expired/
  Revoked, each paired with its own semantic color + icon per `design-system.md`'s Status
  Indicators rule) → resend/revoke row actions. Separately: Organisations screen → read-only
  table of every organisation.
- **Main flow (prospective director)**: email link → registration page (email pre-filled) →
  fill organisation name/director name/password → submit → immediately usable account.
- **Loading/empty/error states**: per `design-system.md`'s Empty States convention (icon + one
  short human sentence), on every screen including the registration page's invalid-link state.
- **Accessibility**: full keyboard navigation + visible focus rings per `platform-rules.md`'s
  Director Web section, on both the portal and the public registration page.
- **Offline behavior**: none required anywhere in this feature.

### Technical Requirements

- **API impact**: new `PlatformAdminOnly`-gated endpoints for invitation create/list/resend/
  revoke and the organisation directory read, under the `/api/platform-admin/*` prefix
  convention 013h's vaccine-type endpoints already established. No new backend endpoint for
  registration itself — feature 001's `POST /api/organisations/register` is reused as-is.
- **Data-model impact**: a Public-schema migration extending `Invitation` with an
  organisation-name note and revoke attribution (acting-user id/email, timestamp). No changes
  to the `Tenant` entity — the directory reads existing fields.
- **Security considerations**: acting-user resolved server-side from JWT claims only, never
  client-supplied; the registration page is necessarily unauthenticated (mirrors feature 001's
  existing `RequireTenantExempt` pattern) and must not leak invitation validity/state beyond
  the existing generic not-found handling.
- **Performance considerations**: not a concern at this scale — an internal operations tool
  plus a low-volume public form, not a high-traffic path.
- **Testing requirements**: a policy-boundary test (platform-admin claim without director role
  is rejected), invitation lifecycle tests (create, resend-supersedes-prior, revoke, status-
  derivation for all four states), a registration-page component test (valid/expired/revoked/
  reused token states), an organisation-directory read test, and a web component test for the
  extracted shared platform-admin shell/nav.
