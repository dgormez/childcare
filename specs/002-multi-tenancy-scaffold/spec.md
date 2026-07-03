# Feature Specification: Multi-Tenancy Scaffold

**Feature Branch**: `002-multi-tenancy-scaffold`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Build the schema-per-tenant data isolation layer that all subsequent features depend on. PublicDbContext for the shared tenants registry; TenantDbContext for the current request's tenant schema; ICurrentTenantService (scoped) exposing tenant identity to handlers; TenantMiddleware resolving the tenant_id claim on every authenticated request and rejecting requests for a missing/unknown/not-ready tenant before any domain data is touched; a mechanism to roll out pending migrations to every existing tenant schema without manual per-tenant work. Must use a non-pooled Postgres connection (pgBouncer transaction mode resets search_path). The legacy single-schema AppDbContext and the Habit-tracking template code built on it are removed entirely. Tenant suspension/deletion and cross-tenant admin queries are out of scope. (See BACKLOG.md, feature 002 — Multi-Tenancy Scaffold.)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every Request Is Automatically Scoped to Its Own Organisation (Priority: P1)

An authenticated staff member, director, or parent calls any organisation-data endpoint. The system identifies which organisation (tenant) they belong to from their session, and every read or write they perform during that request only touches that organisation's own records — never another organisation's.

**Why this priority**: this is the foundational guarantee the entire product depends on. Every other feature (children, contracts, staff, invoicing, etc.) assumes this isolation already exists and enforces itself automatically, without each feature having to reinvent tenant-scoping logic.

**Independent Test**: Can be fully tested by seeding two separate organisations with their own data, issuing a valid session for a user of organisation A, and confirming every request that user makes only ever returns or affects organisation A's data, never organisation B's — even under concurrent requests from both organisations at once.

**Acceptance Scenarios**:

1. **Given** a valid, authenticated session identifying a specific ready organisation, **When** the user makes a request to any organisation-data endpoint, **Then** the system resolves their organisation and every subsequent read/write in that request is confined to that organisation's own data.
2. **Given** two authenticated users belonging to two different organisations making requests to the system at the same time, **When** both requests are processed concurrently, **Then** each is served entirely from its own organisation's data with no mixing or leakage between them.

---

### User Story 2 - Invalid or Missing Organisation Context Is Rejected Before Any Data Is Touched (Priority: P1)

A request arrives that either doesn't identify which organisation it belongs to, or identifies an organisation that doesn't exist or isn't fully set up yet. The system refuses the request immediately, before any organisation's data could be read or written.

**Why this priority**: this is the safety net for User Story 1 — isolation is only meaningful if the system fails closed on ambiguous or invalid cases rather than guessing or defaulting to some organisation's data.

**Independent Test**: Can be fully tested by sending requests that omit the organisation identifier, reference a non-existent organisation, or reference an organisation that hasn't finished being set up, and confirming each is rejected with no organisation data ever queried.

**Acceptance Scenarios**:

1. **Given** an authenticated session with no organisation identified, **When** a request is made to an organisation-data endpoint, **Then** the request is rejected before any data access occurs.
2. **Given** an authenticated session referencing an organisation identifier that does not match any known organisation, **When** a request is made, **Then** the request is rejected before any data access occurs.
3. **Given** an authenticated session referencing an organisation that exists but has not finished onboarding (its workspace is still being set up or failed to set up), **When** a request is made, **Then** the request is rejected before any data access occurs.

---

### User Story 3 - Rolling Out a Change to Every Organisation Without Manual Work (Priority: P2)

An operator needs to add new structure (e.g., a new field or table) that every organisation's workspace should have. Instead of manually applying that change to each organisation's data one at a time, the operator runs a single action that brings every existing organisation's workspace up to date.

**Why this priority**: without this, every future feature that changes the shape of tenant data would require linear, manual, error-prone per-tenant work — this doesn't block the P1 isolation guarantee itself, but every subsequent feature depends on it to ship safely at scale.

**Independent Test**: Can be fully tested by introducing a new pending structural change, running the rollout action, and confirming it is applied to every existing organisation's workspace — including ones set up before the change existed — without any organisation being touched by hand.

**Acceptance Scenarios**:

1. **Given** several existing organisations and one pending structural change, **When** the rollout action runs, **Then** every existing organisation's workspace has that change applied.
2. **Given** an organisation whose workspace already has the change applied, **When** the rollout action is run again, **Then** nothing happens to that organisation and no error occurs.

---

### Edge Cases

- Two requests from two different organisations arrive on the system at the exact same time — each must be resolved and served independently, with no chance of one request's organisation context leaking into the other's.
- An organisation's workspace was being set up when a structural rollout happened mid-way — the rollout must not corrupt or conflict with an in-progress setup, and must not leave the organisation in a state where a later rollout can't cleanly finish it.
- The system reuses an underlying connection for efficiency — a connection previously used to serve one organisation's request must never carry over any trace of that organisation's context into a later, unrelated request that reuses the same connection.
- An authenticated session references an organisation identifier that isn't even validly formed (garbled/malformed) — this must be treated the same as "unknown organisation," not cause an unhandled error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST maintain a shared registry (already established) of organisations, containing only organisation-level metadata — no organisation's operational/domain data lives in that shared registry.
- **FR-002**: System MUST maintain each organisation's operational/domain data in a location isolated from every other organisation's.
- **FR-003**: System MUST determine which organisation a request belongs to before any organisation-data read or write occurs for that request.
- **FR-004**: System MUST make the current request's organisation identity available to whatever code handles that request, for the lifetime of that request only.
- **FR-005**: The current request's organisation identity MUST never be shared or reused across two different requests, even concurrently.
- **FR-006**: System MUST reject a request that does not identify an organisation, before any organisation data is accessed.
- **FR-007**: System MUST reject a request that identifies an organisation which does not exist, before any organisation data is accessed.
- **FR-008**: System MUST reject a request that identifies an organisation which exists but has not finished (or has failed) onboarding, before any organisation data is accessed.
- **FR-008a**: System MUST reject a request the same way (FR-006/007/008) when organisation resolution cannot be completed at all (e.g., an infrastructure failure while checking) as when the organisation is definitively unknown — the caller MUST NOT be able to distinguish "doesn't exist" from "couldn't be checked" from the response alone. Server-side, the system MUST still log the actual distinguishing cause of each rejection, so a developer can tell these cases apart during debugging even though callers cannot.
- **FR-009**: System MUST make it structurally impossible for a request resolved to one organisation to read or write a different organisation's data, regardless of what a caller supplies in the request itself.
- **FR-010**: System MUST provide a way for an operator to apply a pending structural change to every existing organisation's workspace without manually repeating the action per organisation.
- **FR-011**: The rollout mechanism in FR-010 MUST be safe to run again on an organisation that already has the change applied — it MUST NOT fail or duplicate the change.
- **FR-012**: System MUST NOT use a database connection mode that resets an organisation's data-isolation context between statements within the same request.
- **FR-013**: System MUST remove the unrelated template features (habit tracking, per-user subscription billing, push-notification token storage) and their pre-existing single-shared-schema storage entirely — none are superseded, all are deleted outright as leftover template code with no place in the organisation-scoped domain model.
- **FR-014**: System MUST relocate the existing global user-account table (currently backing the pre-existing login/registration/password-reset/email-verification endpoints) out of the shared single-schema model and into the per-organisation data model, so each organisation has its own copy rather than one table shared across every organisation.
- **FR-015**: System MUST resolve and enforce organisation context, by default, for every authenticated request — except for a small, explicitly named set of endpoints that do not operate on organisation domain data (specifically: super-admin invitation issuance, organisation registration, and the health check endpoint). A new endpoint MUST be tenant-scoped by default; it can only skip organisation-context resolution by being deliberately and visibly added to this exemption list — resolution is never something an endpoint has to opt into.

### Key Entities

- **Organisation record** (existing, shared registry): the durable identity of an organisation — id, slug, workspace location, plan, and onboarding status. Already established by organisation onboarding (feature 001); this feature does not change its shape.
- **Current-request organisation context**: not a stored entity, but a transient, request-scoped fact — which organisation a specific in-flight request belongs to, and where that organisation's data lives. Exists only for the duration of one request.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of requests to organisation-data endpoints are served using only the requesting user's own organisation's data — zero cross-organisation data exposure in any test scenario, including concurrent requests from different organisations.
- **SC-002**: 100% of requests with a missing, unknown, or not-yet-ready organisation reference are rejected before any organisation data is touched.
- **SC-003**: Rolling out a new structural change to all existing organisations requires a single operator action, regardless of how many organisations exist.
- **SC-004**: Re-running the rollout action against organisations that already have a change applied produces no errors and no duplicate effects.
- **SC-005**: The rollout mechanism from SC-003/SC-004 completes within a single operator-monitored action (no batching or background-job infrastructure needed) across the full Phase 1 range of dozens to low hundreds of organisations — not thousands.

## Assumptions

- The organisation registry (public shared schema) and the organisation-onboarding flow that populates it already exist (delivered by feature 001) and are not changed by this feature beyond what's needed to read them.
- "Not finished onboarding" and "finished onboarding" map to the existing onboarding-status values already recorded on the organisation record (feature 001); this feature does not introduce new statuses.
- This feature builds the mechanism by which future features store and access per-organisation data — it does not itself introduce new operational/domain entities (e.g., children, contracts, staff); those arrive with their own features and simply plug into the mechanism this feature provides.
- Determining "which organisation a request belongs to" relies on organisation identity already being present in the user's authenticated session, established by the existing/forthcoming authentication flow — how that identity gets into the session is not this feature's concern.
- Tenant suspension and deletion, and any cross-organisation administrative queries, are out of scope for this feature (per BACKLOG.md).
- Per-user subscription billing and push-notification token storage (removed by FR-013) were generic template features with no equivalent anywhere in the organisation-scoped domain model; they were never part of a tested, working flow. Organisation-level billing is a real future need (per BACKLOG.md's invoicing feature) but is out of scope here — it will be designed properly, scoped to the organisation, not an individual user, when that feature is built.
- Relocating the user-account table (FR-014) moves *where the data lives*, not *how a not-yet-authenticated login request figures out which organisation to look in*. Resolving that (e.g., an email-to-organisation lookup, and embedding organisation identity into a newly-issued session at login) is explicitly out of scope here and is delivered by the dedicated Auth feature (003, per BACKLOG.md). Until then, the relocated login/registration/password-reset/email-verification endpoints may not be fully end-to-end functional across multiple organisations — an accepted, temporary state this feature is not responsible for resolving, not a regression it must prevent.

## Clarifications

### Session 2026-07-03

- Q: The pre-existing single-shared-schema data model being removed also backs the existing authentication endpoints (login, registration, password reset, email verification) for parent/caregiver/admin accounts. What happens to those endpoints in this feature? → A: Relocate the existing user-account table into the per-organisation data model now, as bare infrastructure (FR-014), so the endpoints keep compiling against the new model — but the deeper tenant-aware login resolution (email→organisation lookup, embedding organisation identity in the session at login) remains out of scope, deferred to feature 003 (Auth).
- Q: Should organisation-context resolution be a mandatory blanket gate on every authenticated request, or opt-in per endpoint/route group? → A: Deny-by-default (FR-015) — mandatory for every authenticated request except a small, explicitly named exemption list (super-admin invitation issuance, organisation registration, health check); new endpoints are tenant-scoped by default and must be deliberately added to the exemption list to skip it, never the reverse.
- Q: Should a lookup failure while resolving an organisation (e.g. a transient infrastructure error) be surfaced differently to the caller than a definitive "unknown organisation" rejection? → A: No — identical rejection response either way (FR-008a), so callers can never distinguish "doesn't exist" from "couldn't be checked." Server-side logging still records the true cause, so this stays debuggable for developers without weakening the fail-closed guarantee for callers.
- Q: What scale should the rollout mechanism (and tenant resolution generally) be designed for during Phase 1? → A: Dozens to low hundreds of organisations (SC-005) — consistent with PROJECT-BRIEF.md's pre-revenue, Neon-free-tier, private-KDVs-only Phase 1 posture, not thousands.
