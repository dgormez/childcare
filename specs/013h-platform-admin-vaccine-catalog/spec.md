# Feature Specification: Platform-Admin Vaccine Catalog Management

**Feature Branch**: `013h-platform-admin-vaccine-catalog`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "Add a platform-admin role and management UI for the shared
vaccine_types catalog feature 013g introduced (create, rename, reorder, deactivate entries).
This is the first platform-level (cross-tenant) admin capability in this codebase."

## Clarifications

### Session 2026-07-13 (resolved directly with the product owner before specifying)

- Q: Where does a platform-admin authenticate — a new tenant-less login path, a config/env-gated
  route, or something reusing existing auth? → A: Reuse the existing director login (email/
  password + Google OAuth, feature 003/007a), plus a new `IsPlatformAdmin` flag on the director's
  existing account. Granted to specific accounts via direct data change, mirroring how the
  catalog itself is maintained today (013g).
- Q: Is this a new screen in the existing Next.js web app, or a separate internal tool? → A: Same
  app, new gated route (Constitution Principle VII, monolith-first simplicity).
- Q: Does deactivating a catalog entry need an audit trail? → A: Yes — who deactivated it and
  when, consistent with 013g's existing precedent of never hard-deleting catalog rows.

## Technical Correction (from planning research, before implementation)

The original framing assumed a platform-admin has "no tenant context" to authorize against. That
assumption does not hold against this codebase's actual account model: a director's account
(`TenantUser`) lives in a per-tenant schema, not a shared/public table — there is no
tenant-independent account anywhere in this system yet, and this feature does not introduce one.
A platform-admin is simply an existing director account, in one specific tenant, with an added
flag. Their request still carries a valid `tenant_id` claim and passes through the existing
`TenantMiddleware` exactly like any other director request; it is not tenant-exempt. Only the
*data* being managed (`vaccine_types`, in `PublicDbContext`) is cross-tenant, matching the
existing precedent set by 013g's read-only `GET /api/vaccine-types` endpoint, which already
authorizes as an ordinary director-scoped request against non-tenant-scoped data. This feature
follows that same shape for its write endpoints, rather than inventing a tenant-exempt path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Platform admin adds a new catalog entry without touching the database (Priority: P1)

The platform operator needs to add a vaccine to the shared catalog (e.g. a newly recommended
immunisation) without running a manual SQL statement against production. They log in with their
existing director account — which happens to be flagged as platform-admin — see a
platform-admin-only screen, and create the entry there.

**Why this priority**: This is the core gap this feature closes — today, every catalog change is
a direct database edit. Without create, nothing else in this feature (rename, reorder,
deactivate) has anything new to act on.

**Independent Test**: Can be fully tested by logging in as a platform-admin-flagged director,
opening the catalog management screen, creating a new entry with a name and category, and
confirming it appears immediately in 013g's existing tenant-facing `GET /api/vaccine-types`
response for any tenant.

**Acceptance Scenarios**:

1. **Given** a director account flagged `IsPlatformAdmin`, **When** they log in and navigate to
   the catalog management route, **Then** they see the full list of catalog entries (active and
   inactive) with their category and display order.
2. **Given** the platform-admin is on the catalog management screen, **When** they create a new
   entry with a name and category, **Then** it is saved, appears in the management list, and is
   immediately visible via 013g's existing tenant-facing read endpoint for every tenant.
3. **Given** a director account with no `IsPlatformAdmin` flag, **When** they attempt to reach the
   catalog management route or call its underlying endpoints directly, **Then** they are denied
   (route not reachable in the UI; API returns 403).

---

### User Story 2 - Platform admin fixes a typo or reorders the catalog (Priority: P2)

The platform operator notices a misspelled vaccine name or wants the catalog to display in a more
sensible order for directors picking from it. They rename an entry or drag it to a new position.

**Why this priority**: A natural companion to creation — a catalog that can only ever grow, never
be corrected or reordered, is a small step up from the current all-manual state. Still secondary
to being able to create entries at all (User Story 1).

**Independent Test**: Can be fully tested by renaming an existing entry and confirming the new
name is what 013g's read endpoint now returns, and by reordering two entries and confirming their
display order swaps in that same read endpoint's response.

**Acceptance Scenarios**:

1. **Given** an existing catalog entry, **When** the platform-admin renames it, **Then** the new
   name is saved and reflected in the tenant-facing read endpoint. Per 013g's existing FR-010,
   any vaccine record that already referenced this entry keeps its own originally-saved name
   text unchanged — a catalog rename never rewrites past records.
2. **Given** two or more catalog entries, **When** the platform-admin reorders them, **Then** the
   new display order is saved and reflected in the tenant-facing read endpoint's ordering.

---

### User Story 3 - Platform admin deactivates a discontinued entry, with accountability (Priority: P2)

A vaccine is discontinued or was added by mistake. The platform admin deactivates it so it no
longer appears as a selectable option for directors, without deleting it outright (013g's
existing records still reference it) or losing track of who made the change.

**Why this priority**: Matches 013g's own never-hard-delete precedent and closes the last of the
four actions (create/rename/reorder/deactivate) named in this feature's own scope. Ranked behind
User Stories 1-2 because a catalog that can be added to and corrected is already useful without
this.

**Independent Test**: Can be fully tested by deactivating an entry as one platform-admin account,
then confirming the entry no longer appears in 013g's tenant-facing read endpoint's active-only
view, still appears in the platform-admin's own management list (marked inactive), and that the
management list shows who deactivated it and when.

**Acceptance Scenarios**:

1. **Given** an active catalog entry, **When** the platform-admin deactivates it, **Then** it no
   longer appears as selectable to directors (013g's existing tenant-facing behavior for inactive
   entries, unchanged), but remains visible in the platform-admin's management list marked
   inactive.
2. **Given** a deactivated entry, **When** the platform-admin views it in the management list,
   **Then** they see which platform-admin account deactivated it and when.
3. **Given** an entry already referenced by existing vaccine records (013g FR-010), **When** it is
   deactivated, **Then** those existing records are completely unaffected — unchanged behavior,
   already covered by 013g and re-verified, not re-implemented, here.

---

### Edge Cases

- A platform-admin tries to deactivate an entry that is already inactive — the action is a no-op
  from the caller's perspective (no error), and the original deactivation audit record (who/when)
  is preserved, not overwritten by the redundant attempt.
- A platform-admin reorders entries while another platform-admin (or the same one, in a second
  tab) is also reordering — the last write wins for display order, consistent with this being
  low-frequency, single-operator administrative data with no concurrent-editing requirement.
- A non-platform-admin director's JWT already carries a valid `tenant_id` and `director` role
  (they can already reach every other director endpoint) — the platform-admin check is an
  additive requirement on top of that, never a replacement for the existing `DirectorOnly`
  policy, so revoking the flag alone (without touching role/tenant) is sufficient to fully lock
  someone out of this feature's endpoints.
- The catalog management list is empty (should not happen post-013g-seed, but not an error state
  if it occurs) — same empty-state treatment as 013g's own picker (icon + short sentence, no
  blocking error).
- A platform-admin attempts to reactivate a previously deactivated entry — supported, and clears
  the "currently inactive" state (audit fields reset to null, not retained anywhere once
  reactivation happens — see FR-008). A later re-deactivation of the same entry starts from a
  clean slate and produces a fresh audit record; the prior deactivation's who/when is not
  retrievable after reactivation clears it (no history log — Assumptions).
- Two platform-admins (or the same one in two tabs/sessions) deactivate, reactivate, or reorder
  the same entry at nearly the same time — the same last-write-wins resolution applies uniformly
  to every one of these actions, not just reorder: whichever request's database write commits
  last determines the entry's final state and, for deactivate/reactivate, its final audit fields.
  No optimistic-concurrency conflict is surfaced to either caller.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST add an `IsPlatformAdmin` flag to a director's existing account
  (`TenantUser`), defaulting to false, settable only via direct data change (no in-app UI grants
  or revokes this flag — mirrors how the flag itself, and the catalog it gates, are both
  maintained by the platform operator outside the app).
- **FR-002**: System MUST include the platform-admin status in the director's access token when
  set (as a claim present with an exact expected value; a director's flag being off produces a
  token with the claim absent entirely — never present-but-false or present-with-any-other-value),
  so API endpoints can authorize against it without an extra lookup per request. Any claim value
  other than the exact expected one, or the claim's absence, MUST be treated identically to "not a
  platform admin" — there is no partial-credit or malformed-claim state that grants access.
  Because the flag is baked into the token at issuance/refresh, revoking `IsPlatformAdmin` takes
  effect the next time that director's token is refreshed or expires and they re-authenticate —
  not instantly for an already-issued, still-valid token. This mirrors how every other role/claim
  change already behaves in this codebase (e.g. a role change is likewise not retroactively
  applied to a live token) and is an accepted, explicit trade-off given this codebase's existing
  short-lived access token / refresh cycle (constitution's Auth stack constraint) — not a gap.
- **FR-003**: System MUST expose a platform-admin-only management route in the existing director
  web app, reachable only when the authenticated director's token carries the platform-admin
  flag; the route (and its nav entry, if any) MUST NOT be visible or reachable for a director
  without the flag.
- **FR-004**: System MUST allow a platform-admin to create a new catalog entry (name, category,
  display order), reusing 013g's existing category values. The catalog is fully tenant-agnostic:
  a platform-admin's own `tenant_id` (the one tenant they happen to be a director of) has no
  bearing whatsoever on which catalog entries they can view or act on — every platform-admin sees
  and can modify the exact same single, shared catalog, regardless of which tenant their director
  account belongs to.
- **FR-005**: System MUST allow a platform-admin to rename an existing catalog entry's name and/or
  category.
- **FR-006**: System MUST allow a platform-admin to reorder catalog entries (change display
  order).
- **FR-007**: System MUST allow a platform-admin to deactivate an active catalog entry and to
  reactivate a previously deactivated one.
- **FR-008**: System MUST record which platform-admin account deactivated a catalog entry and
  when, captured server-side from the acting platform-admin's own authenticated identity (never a
  client-supplied value of any kind), and MUST display this in the management view for that
  entry. Reactivating clears these fields to empty (not retained anywhere, no history log — see
  Assumptions); a later re-deactivation of the same entry MUST then produce a fresh audit record
  from scratch, never an edit of a previously-cleared one. At every point in time, an entry's
  audit fields being populated and its `IsActive` being false are a single, always-consistent
  pair: an active entry MUST always have empty audit fields, and an inactive entry MUST always
  have them populated — the system MUST NOT allow these to disagree.
- **FR-009**: System MUST NOT allow a director without the platform-admin flag to create, rename,
  reorder, deactivate, or reactivate any catalog entry, via the UI or by calling the underlying
  endpoints directly (API MUST return 403 for every one of these five actions individually — this
  requirement applies per-endpoint, not just at the feature level, so each new endpoint this
  feature adds must independently enforce it). The platform-admin check MUST always require
  `DirectorOnly`-equivalent authentication as a strict prerequisite — a token that carries the
  platform-admin claim but not the `director` role MUST still be rejected; this is an additive
  check layered on top of, never a substitute for or bypass of, existing director role
  authorization.
- **FR-010**: System MUST NOT change 013g's existing tenant-facing `GET /api/vaccine-types`
  endpoint's behavior, response shape, or authorization in any way — this feature only adds new
  write capability behind the platform-admin check; the existing read path for ordinary directors
  is untouched. This MUST be verified by an automated regression test asserting that endpoint's
  contract is unaffected (not merely a documentation assertion — see tasks.md T017).
- **FR-011**: System MUST reflect every create/rename/reorder/deactivate/reactivate action
  immediately in 013g's existing tenant-facing read endpoint for every tenant (no propagation
  delay, no per-tenant cache to invalidate). Each such action MUST be atomic — an entry's fields
  and its audit fields (when applicable) MUST update together in a single transaction; the system
  MUST NOT ever expose a partially-applied action (e.g. a name change saved without its
  corresponding timestamp update) to any reader.
- **FR-012**: System MUST present the catalog management list showing every entry (active and
  inactive), its category, display order, and — for inactive entries — who deactivated it and
  when.
- **FR-013**: All platform-admin-facing strings introduced by this feature MUST be available in
  NL, FR, and EN, consistent with every other director-facing surface in this codebase.
- **FR-014**: System MUST NOT read or write any tenant-schema domain data (e.g. children, staff,
  contracts, attendance) anywhere in this feature — every endpoint this feature adds touches only
  `TenantUser.IsPlatformAdmin` (read-only, via the JWT claim already resolved at authentication
  time) and the public-schema `VaccineType` table. This is the explicit boundary that keeps this
  feature's cross-tenant capability scoped to genuinely tenant-agnostic reference data only.

### Key Entities *(include if feature involves data)*

- **Platform Admin**: Not a new entity — an existing `TenantUser` (director account, feature
  003/005) with its new `IsPlatformAdmin` flag set to true. Still belongs to exactly one tenant
  like any other director account; the flag only grants extra capability over cross-tenant
  reference data, not a new kind of account.
- **Vaccine Catalog Entry** *(extends 013g)*: Gains deactivation audit fields (which
  platform-admin account, and when) alongside its existing name/category/display-order/
  active-inactive fields. Still platform-wide, non-tenant-specific, identical across every KDV.
  The audit fields record the deactivating admin's identity as it was *at the moment of
  deactivation* (denormalized, not a live reference) — so the audit record stays accurate and
  readable even if that admin's account is later renamed or loses its own platform-admin flag;
  it is a historical record of who acted, not a live pointer to their current account state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A platform admin can add a new catalog entry without any direct database access,
  start to finish, in under one minute.
- **SC-002**: A director without the platform-admin flag cannot reach, see, or act on the catalog
  management route or its endpoints under any circumstance.
- **SC-003**: 100% of catalog deactivations are attributable to a specific platform-admin account
  and timestamp.
- **SC-004**: A catalog change made by a platform admin is visible to every tenant's existing
  vaccine-record picker (013g) with no manual propagation step.

## Assumptions

- A platform-admin is an existing director account with an added flag, not a new kind of
  tenant-independent account — see Technical Correction above. This keeps the feature additive on
  top of the existing auth model (director login, `DirectorOnly`-style policies) rather than
  requiring a new authentication scheme.
- The platform-admin management route lives in the existing director-web sidebar shell (007a),
  shown conditionally when the flag is present, rather than a separate layout or app — per
  Constitution Principle VII and the resolved clarification above.
- Granting the `IsPlatformAdmin` flag to a specific account (including the platform operator's own
  account, to make this feature usable immediately after merge) is an out-of-band data change, not
  an in-app action — consistent with FR-001 and with how 013g's catalog itself is maintained.
  Implementation MUST provide a repeatable, auditable way to perform this grant (mirroring this
  codebase's existing per-tenant maintenance-command pattern, e.g. feature 009a's
  `backfill-growth-check`) rather than a one-off manual SQL statement with no record of having run
  it. "Auditable" here means: the command's own console output records, per run, which tenant(s)
  it matched and acted on (or found no match in) — the same per-tenant result-line-plus-summary
  shape `migrate-tenants`/`backfill-growth-check` already produce — so a run's effect is visible
  and reviewable from its output, not a separate persisted audit log (that would be new
  infrastructure this narrow, low-frequency operation doesn't warrant). The grant command is
  intentionally email-matched, not tenant-scoped: if the same email happens to exist as a director
  account in more than one tenant, every matching account is granted the flag. This is accepted
  as intentional rather than a gap — the platform operator supplies the email deliberately, and
  granting broadly on an intentionally-chosen, presumably-rare email collision is a lower risk
  than silently granting only one of several matches and leaving the operator unsure which.
- Create/rename/reorder are not given their own audit trail beyond standard row timestamps —
  only deactivation gets an explicit who/when audit field, matching the specific accountability
  concern raised in this feature's own scoping (the resolved clarification above), not a general
  audit-log system.
- Out of scope (confirmed in this feature's own prompt): any change to 013g's existing
  tenant-facing read behavior; OCR or auto-population of the catalog from an external
  vaccine-schedule data source; a general-purpose platform-admin capability beyond the vaccine
  catalog (future platform-level admin needs get their own backlog item and reuse the
  `IsPlatformAdmin` flag/policy this feature introduces, per the new Platform Administration
  workflow this feature adds to `workflows.md`).
