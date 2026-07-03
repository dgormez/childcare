# Feature Specification: Organisation Onboarding

**Feature Branch**: `001-organisation-onboarding`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Build the organisation onboarding flow for ChildCare — the entry point for a new KDV operator joining the platform. A director receives an invitation (invite-only; no public self-signup in Phase 1) and completes registration: organisation name, their name, email, password. On successful registration, the system creates the organisation's registry record (plan = trial, ready status), an isolated workspace for that organisation, that workspace's baseline structure, and the director's account inside it. The director can then log in and see an empty admin dashboard. The workspace is provisioned at registration time — not at first login — so by the time any user of the organisation ever authenticates, the workspace already exists. Onboarding is invite-only during early access, via a single-use, time-limited invitation. Regulatory identifiers the director may not yet have (Opgroeien location reference, Belgian company number) must not block registration. Out of scope: payment/subscription billing, adding locations/staff/children, open self-signup."

## Clarifications

### Session 2026-07-02

- Q: Is registration synchronous (the request waits until org + workspace + director account all exist) or asynchronous (accepted immediately, provisioned in the background)? → A: Synchronous — the registration request does not complete successfully until the organisation, workspace, and director account all exist and are ready.
- Q: Who/what generates an invitation? → A: A restricted internal capability, usable only by a platform operator, gated by a dedicated credential stored in a secrets manager (never a plain environment variable) — separate from any organisation user's authentication. This is an explicitly temporary Phase 1 measure, expected to be replaced by proper super-admin authentication/authorization once an admin UI exists (Phase 2). No self-service "invite a director" UI is in scope for this feature.
- Q: Is the invitation locked to a specific email address, or can the registrant enter any email at registration? → A: Locked — an invitation is issued for one specific email address, and registration must use that exact email. (Clarified for avoidance of doubt: the password is never part of the invitation — it is chosen fresh by the director during registration. The invitation only gates *who* may register and *which email* they must register with; it has no bearing on credentials.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director registers and gets an immediately usable organisation (Priority: P1)

A KDV operator has received an invitation to join the platform. They follow the invitation link and complete a short registration: their organisation's name, their own name, email, and a password. The moment they finish, the platform has already set up their organisation and its own isolated workspace — no waiting, no manual setup by anyone else — so the director can log straight in and see their (still empty) admin dashboard.

**Why this priority**: This is the very first experience every future customer has with the platform. If it doesn't work, or leaves the director stuck, nothing else about the product matters yet.

**Independent Test**: Issue a valid invitation, complete registration through it, and verify the director can immediately log in and reach a working admin dashboard for their own organisation — with no operator intervention between registration and login.

**Acceptance Scenarios**:

1. **Given** a director has a valid, unexpired invitation, **When** they submit organisation name, their name, the invitation's exact email address, and a password of their choosing, **Then** the organisation is registered with a "trial" plan, its isolated workspace is created and ready, and the director's account exists inside that workspace.
2. **Given** registration has just completed successfully, **When** the director logs in, **Then** they reach a working (even if empty) admin dashboard for their own organisation, with no additional setup step required of them or of an operator.
3. **Given** a director does not yet have their Opgroeien location reference or Belgian company number, **When** they register, **Then** registration completes without those fields, so they can be filled in later.
4. **Given** registration has completed, **When** the process finishes, **Then** no payment or subscription step has been required — the organisation starts on a trial plan without billing setup.

---

### User Story 2 - Invitations are the only way in, and only while valid (Priority: P1)

The platform is invite-only during early access — there is no public "sign up" page. An invitation only works once, and only within its validity window.

**Why this priority**: Without this guarantee, onboarding isn't actually invite-only — anyone who guesses or reuses a link could create organisations, undermining the controlled early-access rollout and potentially the trust model of the whole platform.

**Independent Test**: Attempt registration with an expired invitation, an already-used invitation, and a nonexistent/invalid invitation reference; verify all three are refused and that none of them creates an organisation, workspace, or account.

**Acceptance Scenarios**:

1. **Given** an invitation whose validity window has passed, **When** someone attempts to register with it, **Then** registration is refused with a clear explanation, and no organisation or workspace is created.
2. **Given** an invitation that has already been used to complete a registration, **When** someone attempts to use it again, **Then** the second attempt is refused and does not create a second organisation.
3. **Given** no invitation, or a reference that does not correspond to any real invitation, **When** someone attempts to reach the registration step, **Then** they cannot register — there is no path to create an organisation without a valid invitation.

---

### User Story 3 - Registration survives failures and races without corrupting state (Priority: P2)

Setting up a brand-new organisation involves several steps happening together (registry record, isolated workspace, baseline structure, director account). Things can go wrong partway through, or two people can click the same link at nearly the same instant. Either way, the platform must never end up with a broken, half-created organisation, or with two organisations created from one invitation.

**Why this priority**: A foundation feature that can silently corrupt itself under failure or concurrency is worse than one that simply doesn't exist yet — every later feature builds on the assumption that a "ready" organisation really is ready.

**Independent Test**: Simulate a failure partway through provisioning and confirm the organisation can be safely retried to completion; simulate two simultaneous registration attempts on the same invitation and confirm exactly one organisation results.

**Acceptance Scenarios**:

1. **Given** workspace provisioning fails partway through during registration (e.g., a transient error while setting up the workspace's baseline structure), **When** the registration is retried, **Then** the platform detects the incomplete organisation and safely completes it, rather than leaving it broken or creating a duplicate.
2. **Given** two people submit registration using the same invitation at nearly the same time, **When** both attempts are processed, **Then** exactly one organisation, one workspace, and one director account are created, and the other attempt receives a clear, safe outcome (not a duplicate organisation, not a corrupted one).

---

### Edge Cases

- What happens when a director clicks an invitation link after it has expired?
- What happens when workspace provisioning fails partway through (e.g., a database error mid-setup)? Must be detectable and recoverable, not left half-provisioned.
- What happens when two people click the same invitation link at the same time?
- What happens when someone clicks an invitation link that has already been used to successfully register?
- What happens when someone attempts to register a second organisation using an email address already associated with an existing director account (same or different organisation)?
- What happens when a director attempts to register using a different email address than the one their invitation was issued to?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST NOT provide any way to create an organisation other than by completing registration through a valid invitation — there is no public self-registration path.
- **FR-002**: System MUST support issuing a single-use, time-limited invitation bound to one specific prospective director's email address, via a restricted internal capability usable only by a platform operator — not by organisation users, directors, or the public.
- **FR-003**: System MUST reject a registration attempt whose invitation has expired, before creating any organisation, workspace, or account.
- **FR-004**: System MUST reject a registration attempt whose invitation has already been used to complete a registration, before creating any organisation, workspace, or account.
- **FR-005**: System MUST reject a registration attempt that does not reference a valid, existing invitation.
- **FR-006**: System MUST collect, at minimum, organisation name, the director's name, email (pre-filled from and locked to the invitation), and a password chosen freely by the director, to complete registration. The password is set entirely by the director at registration time and is never derived from, stored on, or otherwise linked to the invitation.
- **FR-007**: System MUST create the organisation's registry record on successful registration, with a default plan tier of "trial."
- **FR-008**: System MUST provision the organisation's isolated data workspace, including its baseline structure, synchronously within the registration process itself — the registration request MUST NOT be reported as successful until provisioning completes, and this MUST NOT be deferred to a later action such as first login or a background/asynchronous step.
- **FR-009**: System MUST create the director's user account inside the newly provisioned organisation's workspace as part of the same registration process.
- **FR-010**: System MUST mark an organisation as ready for use only once its workspace and its director's account both exist successfully.
- **FR-011**: System MUST allow the director to log in and reach a working admin view immediately after registration completes, without requiring any further operator action.
- **FR-012**: System MUST allow registration to complete without the organisation's Belgian regulatory identifiers (its Opgroeien location reference and its company registration number) — these MUST remain fillable after registration.
- **FR-013**: System MUST NOT require any payment or subscription step to complete registration.
- **FR-014**: System MUST detect an organisation whose workspace provisioning did not complete successfully and allow that registration to be safely retried to completion, without creating a duplicate or conflicting organisation record.
- **FR-015**: System MUST ensure that if the same invitation is used by two concurrent registration attempts, at most one organisation, workspace, and director account is ever created as a result.
- **FR-016**: System MUST record the organisation's plan tier as one of a known set of tiers (trial, starter, pro), defaulting every new organisation to "trial" at registration, so later plan changes do not require restructuring how the tier is stored.
- **FR-017**: System MUST gate invitation issuance behind a dedicated operator credential, separate from any organisation user's authentication, with that credential stored in a secrets manager rather than a plain environment variable or hardcoded value.
- **FR-018**: System MUST reject a registration attempt whose submitted email does not exactly match the email address the invitation was issued to.

### Key Entities

- **Invitation**: A single-use, time-limited credential tied to one specific prospective director's email address, created by a platform operator through a restricted internal capability (not a self-service UI). Has a validity window; once it has been used to complete a registration, or that window has passed, it can never be used to register again. Carries no password or credential information — only identifies who is allowed to register and with which email.
- **Organisation**: The registry record created on successful registration — organisation name, plan tier, and a readiness state that reflects whether its workspace and director account both exist.
- **Director Account**: The first user of a newly registered organisation, created inside that organisation's isolated workspace, with full administrative rights over that organisation.
- **Tenant Workspace**: The isolated operational data area belonging to one organisation, provisioned (with its baseline structure) during registration, before any user of that organisation can log in.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of organisations that exist in the system originate from a valid, single-use invitation — 0 organisations are ever created without one.
- **SC-002**: A director can go from a valid invitation to a working admin dashboard in a single, synchronous registration flow (no waiting or checking back later), with 0 manual operator steps required in between.
- **SC-003**: 100% of registration attempts using an expired, already-used, or invalid invitation are refused, with 0 organisations, workspaces, or accounts created as a side effect.
- **SC-004**: 100% of successfully registered organisations have a fully usable workspace and director account at the moment registration completes — 0% require any additional setup at first login.
- **SC-005**: When the same invitation is used by concurrent registration attempts, at most 1 organisation results in 100% of tested cases — 0 duplicate or partially-created organisations.
- **SC-006**: 100% of registrations complete successfully without the director providing regulatory identifiers they don't yet have.
- **SC-007**: 100% of registration attempts using an email address different from the one the invitation was issued to are refused, with 0 organisations created as a side effect.

## Assumptions

- Deciding *who* to invite (choosing the prospective director/organisation) remains a manual, low-volume judgment call by a platform operator during early access; this spec covers the invitation's technical creation and the registration flow it unlocks, not how an operator decides whom to invite.
- The restricted internal capability used to create invitations is a deliberately minimal, temporary measure for Phase 1 — it is expected to be superseded by proper super-admin authentication/authorization once a dedicated admin UI is built (Phase 2), not treated as the platform's long-term operator-access model.
- "Empty admin dashboard" means the director successfully reaches an authenticated view scoped to their own organisation — the dashboard's actual content and features belong to later, separate work.
- The mechanism that routes every *subsequent* authenticated request (after registration) to the correct organisation's workspace — and prevents workspace context from leaking between concurrent requests — is a separate, dependent feature (multi-tenancy scaffold). This spec covers registration up through the organisation becoming ready and the director's first login; it does not re-specify ongoing request routing.
- Payment/subscription billing infrastructure exists at the platform level but is not exercised by this feature — the trial plan is recorded, not billed or enforced here.
- The same email address may plausibly be used to register more than one organisation over time (e.g., a consultant onboarding multiple clients); registering an organisation is not blocked purely because the email has been seen before, but each invitation may still only be used once.
