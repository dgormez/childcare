# ChildCare — Feature Backlog

> Ordered list of features to implement. Each feature = one Git branch + one Spec Kit cycle.
> Update this file as dependencies become clearer during implementation.
> Reference gap-analysis.md for full feature details.
> To process the next feature in a fresh session, see `.specify/memory/process-next-feature.md`.

---

## Implementation Order

### Foundation (must be done in sequence — each depends on the previous)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 001 | `001-organisation-onboarding` | Organisation registration + workspace provisioning | — | ✅ Done |
| 002 | `002-multi-tenancy-scaffold` | TenantMiddleware, TenantDbContext, ICurrentTenantService, schema switching | 001 | ✅ Done |
| 003 | `003-auth` | Login, per-device refresh tokens, Google/Apple OAuth (parent app) | 001, 002 | ✅ Done |
| 004 | `004-locations` | Location management within an organisation | 001, 002 | ✅ Done |
| 005 | `005-staff` | Caregiver + director profiles, role assignment, multi-location assignment | 004 | ✅ Done |
| 006 | `006-children` | Child profiles, medical notes, authorised pickups | 002 | ✅ Done |
| 007 | `007-contracts` | Enrolment contracts, contracted days, split-location validator | 005, 006 | ✅ Done |

### Phase 1 — Core Operations (can be parallelised once foundation is in place)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 007a | `007a-web-admin-scaffold` | Next.js app cleanup, director auth (email/password + Google OAuth), nav shell, first real screen (staff list + PIN management) | 003, 005 | 🔲 Not started |
| 008 | `008-caregiver-app-scaffold` | Expo app structure, caregiver auth, API client, offline sync infrastructure | 003, 006 | ✅ Done |
| 008a | `008a-caregiver-kiosk-mode` | Room tablet kiosk mode, PIN per caregiver, session management | 008 | ✅ Done |
| 009 | `009-child-events` | Daily tracking (sleep, feeding, diaper, mood, weight, etc.) | 006, 008a | 🔲 Not started |
| 010 | `010-attendance` | Daily attendance register, BKR ratio enforcement | 007, 008a | 🔲 Not started |
| 011 | `011-closure-calendar` | KDV holiday/closure schedule, parent notification | 004 | 🔲 Not started |
| 012 | `012-caregiver-scheduling` | Shift planning, multi-location day assignment | 005, 010 | 🔲 Not started |
| 013 | `013-parent-communication` | Messaging, daily reports to parents | 006, 009 | 🔲 Not started |
| 014 | `014-invoicing` | Monthly invoice generation (QuestPDF), payment tracking | 007, 011 | 🔲 Not started |

### Phase 2 (after Phase 1 is stable)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 015 | `015-fiscal-attestations` | Annual tax certificates (QuestPDF) | 014 | 🔲 Not started |
| 016 | `016-developmental-milestones` | Child development tracking | 006 | 🔲 Not started |
| 017 | `017-memoq` | MeMoQ pedagogical quality self-evaluation (6 dimensions) | 004, 005 | 🔲 Not started |
| 018 | `018-management-reporting` | KPIs, occupancy, financial summaries | 010, 014 | 🔲 Not started |
| 020 | `020-email-communications` | Bulk parent emails by location/section, emailed daily reports | 004, 006, 009, 011, 013 | 🔲 Not started |

### Phase 3 (post-revenue)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 019 | `019-ikt-compliance` | IKT subsidy integration, Opgroeien API | All Phase 1 | 🔲 Not started |

---

## Dependency Graph (Foundation)

```
001-organisation-onboarding
        ↓
002-multi-tenancy-scaffold
        ↓
003-auth ──────────────────┐
004-locations              │
        ↓                  │
005-staff                  │
        ↓                  ↓
006-children ──→ 007-contracts
```

---

## Spec Kit Inputs

> For each feature, run in Claude Code:
> `/speckit-specify @PROJECT-BRIEF.md @BACKLOG.md`
> Then paste the prompt below for that feature.

---

### 001 — Organisation Onboarding

```
Build the organisation onboarding flow for ChildCare — the entry point
for a new KDV operator joining the platform.

What to build:
- A director receives an invitation (invite-only; no public self-signup in Phase 1).
- They complete registration: organisation name, their name, email, password.
- On successful registration, the system creates:
    1. A record in the shared tenants table (public schema): org name, slug,
       plan = 'trial', provisioning_status = 'ready'.
    2. An isolated PostgreSQL schema for this organisation (schema-per-tenant).
    3. Baseline EF Core migrations applied to that new schema.
    4. The director's user account inside the tenant schema.
- The director is then able to log in and see an empty admin dashboard.

Key constraints:
- Workspace (PostgreSQL schema) is provisioned at registration time — NOT at first login.
  By the time any user ever authenticates, the schema must already exist.
- Onboarding is invite-only during early access. The invitation is a signed
  token (e.g. JWT or UUID stored in DB) with an expiry.
- Plan tiers: trial | starter | pro. Default = trial.
- The following fields are nullable at onboarding and filled in later via Settings:
    dossiernummer (Opgroeien location ID), KBO/ondernemingsnummer (Belgian company number).
  Onboarding must NOT block on these fields — directors may not have them yet.
- All user-facing strings must use i18n keys (NL/FR/EN). No hardcoded labels.

Edge cases:
- What if the invitation token has expired when the director clicks the link?
- What if schema provisioning fails partway through (e.g. DB error mid-migration)?
  The system must be able to detect and recover — not leave a half-provisioned org.
- What if two people click the same invitation link concurrently?

Out of scope:
- Payment / subscription billing (Stripe is wired up in infra but not triggered here).
- Adding locations, staff, or children — that comes in features 004–006.
- Open self-signup — always invite-only in Phase 1.
```

**Shipped 2026-07-02** — `specs/001-organisation-onboarding/` (spec → plan → tasks → implementation, 56/56 tasks, 12 new integration tests + all 29 pre-existing tests passing). Scope deltas from the plan above, worth knowing before starting feature 002:

- Introduced the full 5-project solution (`ChildCare.Domain`/`Application`/`Infrastructure`/`Contracts` + `ChildCare.sln`) — this was always planned for "eventually," but 001 is what actually did it, since it needed MediatR/FluentValidation from day one.
- Invitation rejection (not-found/expired/already-used) all return the same generic `404 errors.invitation.not_found` — a deliberate security decision (don't let a caller enumerate token state), made during implementation, not in the original spec.
- Discovered and fixed a genuine EF Core limitation: migrations bake their schema name in as a literal string at scaffold time, so "replay the same migration per dynamic schema" doesn't work as originally planned. Fixed via runtime script-generation + placeholder substitution — see `research.md` R15.
- **Flag for 002 and beyond**: MediatR 14.2.0 (pulled in by this feature) requires a paid license for production use (dev/test/CI are unaffected) — see `research.md` R18. Needs a decision (buy a license, or migrate to a free alternative) before any MediatR-dependent feature ships to production.
- `TenantMiddleware`/`ICurrentTenantService` are intentionally NOT built here — 001 exposes zero tenant-data-read endpoints, so there's nothing for them to protect yet. This is now a codified constitution carve-out (v1.1.0), not just an assumption.

---

### 002 — Multi-Tenancy Scaffold

```
Build the schema-per-tenant data isolation layer that all subsequent
features depend on.

What to build:
- PublicDbContext: connects to the shared public schema. Contains only the
  tenants table (id, slug, schema_name, plan, provisioning_status, created_at).
  No domain data lives here.
- TenantDbContext: connects to the current tenant's schema. All domain entities
  (children, contracts, staff, events, etc.) are registered here. EF Core sets
  the PostgreSQL search_path to the tenant's schema on every connection.
- ICurrentTenantService (scoped): exposes TenantId, SchemaName, TenantSlug to
  all downstream handlers and repositories. Set once per request by middleware.
- TenantMiddleware: runs on every authenticated request. Reads the tenant_id
  claim from the validated JWT, looks up the tenant in PublicDbContext, sets
  ICurrentTenantService, and rejects the request (401/403) if the tenant is
  not found or not in 'ready' status.
- EF Core migration strategy: a mechanism to apply pending migrations to every
  existing tenant schema — as a CLI command or startup utility — without
  manual per-tenant work.

Key constraints:
- Use Neon direct (non-pooled) connection string. pgBouncer transaction mode
  resets search_path between statements, breaking schema isolation. This is
  a hard constraint — the spec must reflect it.
- No cross-tenant data access is ever possible through TenantDbContext.
  search_path ensures PostgreSQL only sees the current tenant's tables.
- ICurrentTenantService must be scoped (per-request), never singleton.
  A singleton would leak tenant context between concurrent requests.
- Requests with no tenant_id claim, or a tenant_id that resolves to no known
  tenant, must be rejected before any domain data is touched (fail closed).
- The existing AppDbContext (single-schema, has Users + Habits) is removed
  entirely and replaced by PublicDbContext + TenantDbContext.
- The HabitEndpoints.cs and all Habits domain code is deleted — it was a
  template leftover and has no place in ChildCare.

Edge cases:
- Two concurrent requests from different tenants on the same server instance:
  each must get its own scoped TenantDbContext with its own search_path.
- A request arrives for a tenant whose schema was provisioned but migrations
  are partially applied (e.g. a deployment happened mid-migration run).
- Connection reuse by the .NET connection pool: search_path set on one
  connection must not bleed into a subsequent request that reuses that connection.

Out of scope:
- Tenant suspension / deletion (not in Phase 1).
- Cross-tenant admin queries (super-admin tooling is a later concern).
```

---

### 003 — Auth

```
Build the authentication layer for all three products (web admin,
caregiver app, parent app) on top of the multi-tenancy scaffold.

Context: a working auth skeleton already exists in the repo
(AuthEndpoints.cs, AuthService.cs). This spec describes what to keep,
what to change, and what to add now that schema-per-tenant is in place.

What to keep (already solid):
- JWT access tokens (15-minute expiry) + per-device refresh token rotation
  (30-day expiry, one token per device, stored in tenant schema).
- Rate limiting: auth-strict (login/register), auth-oauth, auth-refresh policies.
- Google tokeninfo validation (for Android/web Google Sign-In).
- Apple JWKS validation (for iOS Apple Sign-In).
- Security headers middleware.

What changes:
- Users table moves from the old AppDbContext into TenantDbContext (tenant schema).
  Each tenant schema has its own users table — no shared user pool.
- The JWT must include a tenant_id claim so TenantMiddleware can resolve the schema.
- Login flow: receive email+password → look up user by email in the tenant schema
  (tenant must be identified before the user can be found). Resolution strategy:
  a thin public-schema index (email → tenant_id) avoids a full cross-schema scan.
- Email verification and password reset flows must work within the tenant context.

Auth strategy per product (already decided):
- Web admin: email/password + Google OAuth.
- Caregiver app: email/password only (employer-provisioned accounts — no social login).
- Parent app: email/password + Google OAuth + Apple Sign-In (App Store requirement).

Key constraints:
- No hardcoded role checks in endpoint handlers. Use ASP.NET Core policy-based
  authorisation with named policies: "DirectorOnly", "StaffOrDirector", "ParentOnly".
- Roles: Director (full tenant admin), Staff/Caregiver (operational access),
  Parent (read-only access to their own children's data).
- All auth endpoints remain Minimal API style — no Controllers.
- Scalar UI stays at /scalar/v1 for dev documentation.

Edge cases:
- A parent belongs to multiple tenants (child attends two different KDVs under
  different organisations). Each org is a separate tenant; the parent has a
  separate account in each.
- A staff member is transferred to a different location (same tenant) — their
  account stays the same; only their assignment changes.
- Google/Apple tokens must be validated server-side before a JWT is issued —
  never trust the client's assertion of identity.

Out of scope:
- Magic link login (considered but deferred).
- SSO / SAML (enterprise feature, much later).
- Open registration — all accounts are created by director invitation.
```

**Shipped 2026-07-06** — `specs/003-auth/` (spec → plan → tasks → implementation, 66/66 tasks, 30 new integration tests + all 24 pre-existing tests passing). Scope deltas from the plan above, worth knowing before starting feature 004+:

- Two issues were found and fixed beyond the original prompt's scope, both during `/speckit-plan`'s codebase review: (1) `GoogleSignInAsync`/`AppleSignInAsync` were auto-creating a new account when no match existed — an open-registration path via OAuth, exactly the thing "Open registration" being out-of-scope was meant to prevent. Fixed to link-only, matching `/register`'s removal. (2) Two hardcoded English error strings in `AuthEndpoints.cs` (a pre-existing Constitution Principle IV violation) were replaced with `errors.auth.token_invalid_or_expired`.
- **Client-facing change every downstream feature/app needs to know**: every pre-session auth request (login, Google, Apple, refresh, forgot-password) now requires a client-supplied `organisationSlug` field — the app must know or ask which organisation it's signing into before calling any of these endpoints. Password-reset/verification links carry the slug as a `&org=` query parameter instead, so the client reads it back out of the link rather than asking the user again.
- `Role` (Director/Staff/Parent) now exists on every `TenantUser`, carried on the JWT as a standard `ClaimTypes.Role` claim, with `DirectorOnly`/`StaffOrDirector`/`ParentOnly` authorization policies registered and ready for every feature from 004 onward to declare via `.RequireAuthorization("...")` — no feature after this one should invent its own role-comparison logic.
- The full `AuthService`/`AuthEndpoints.cs` direct-service-call pattern (feature 002's one deliberately-deferred Constitution Principle III gap) is gone — every auth flow is now a MediatR command under `ChildCare.Application/Auth/`, the same pattern features 004+ should follow for their own writes.
- **Flag for 005 (Staff) and 006/013 (Children/Parent Communication)**: this feature deliberately does not build staff- or parent-account provisioning UI — it only builds the auth/authorization mechanics (Role field, policies, sign-in flows) those features will provision accounts into. Whoever builds 005/006/013 needs to decide the actual invitation UX for staff and parent accounts; this feature assumed (spec.md Assumptions) they'll reuse the same `TenantUser`-creation primitives.

---

### 004 — Locations

```
Build location management — the physical KDV buildings that belong to
an organisation (tenant).

What to build:
- CRUD for locations within a tenant: name, address, phone, email,
  max licensed capacity (number of places).
- Settings per location (stored on the location record or a related
  settings table):
    naam_locatie: official KDV name as registered with Opgroeien
    dossiernummer: Opgroeien location identifier (nullable — not required at creation)
    verantwoordelijke: name of the responsible person (for Opgroeien XML reports)
    flex_permission: boolean — does this location have flex opvang toestemming
                     (care >11h/day)? Default false. Required for FO-SU-05 XML later.
    bo_permission: boolean — buitenschoolse opvang (after-school care)? Default false.
- KBO/ondernemingsnummer lives at the organisation (tenant) level, not per location.
- A location belongs to exactly one organisation (tenant schema FK).
- Multiple locations per organisation are supported from day one.
- The director manages all locations from the web admin.

Key constraints:
- dossiernummer and verantwoordelijke are nullable — directors fill them in when
  they have the information. Do not block location creation on these fields.
- All user-facing strings use i18n keys (NL/FR/EN).
- A location cannot be hard-deleted if it has active contracts or staff assignments.
  Soft-delete (deactivated_at) instead.

Edge cases:
- An organisation starts with one location and adds a second later. Staff and
  children created before the second location existed should not be affected.
- A location is deactivated while children still have active contracts there.
  The system must prevent deactivation or prompt the director to resolve.

Out of scope:
- Multi-location staff assignment (feature 005).
- Group/section management within a location (feature 010 dependencies — handled
  as part of attendance or a dedicated groups feature).
- Physical access control hardware (Paxton — Phase 4).
```

**Shipped 2026-07-06** — `specs/004-locations/` (spec → plan → tasks → implementation, 44/44 tasks incl. 2-task convergence pass, 22 new integration tests + all 56 pre-existing tests passing, 78/78 total). Scope deltas worth knowing before starting feature 005+:

- `Location` carries no `OrganisationId`/tenant column — tenant scoping is structural via schema, same pattern as `TenantUser`. No feature after this one should add an explicit tenant FK to a tenant-schema entity.
- `ILocationDeactivationGuard` extension point exists for blocking deactivation when a location has active dependents, but **zero guards are registered by this feature, by design** — features 005 (staff) and 007 (contracts) are expected to register their own guard once staff/contracts exist, so a location with active staff or contracts can't yet be deactivated until they do.
- Convergence passes (2nd pass) added phone-format and field-length FluentValidation rules beyond the original spec — closing gaps found on a re-read of spec.md's Edge Cases, not scope creep.
- Found and fixed a FluentValidation cascade bug (chained `.NotEmpty().EmailAddress()` producing two errors on one property crashed the global exception handler's `ToDictionary`) — worth checking for in any validator written before this fix, via `.Cascade(CascadeMode.Stop)`.

---

### 005 — Staff

```
Build staff member management for KDV caregivers and directors.

What to build:
- Staff member profiles: first name, last name, email, phone,
  qualification level (qualified caregiver, auxiliary, student/volunteer),
  profile photo (signed GCS URL).
- Role assignment: Director | Staff (Caregiver). A person can be director
  of one location and staff at another within the same organisation.
- Multi-location assignment: a staff member can be assigned to work at
  different locations on different days (resolved in feature 012 scheduling).
  At this stage, store which locations a staff member is eligible to work at.
- Each staff member has a user account (email + password, director-provisioned).
  Caregiver app uses email/password only — no social login for staff.
- Staff are stored in the tenant schema (scoped to the organisation).

Qualification matters for BKR:
- Only qualified caregivers and auxiliaries count toward the BKR ratio.
- Students and volunteers do NOT count, even if physically present.
- The qualification field must be stored and used when computing BKR in
  feature 010 (attendance).

Key constraints:
- Directors create staff accounts (invite-only). Staff cannot self-register.
- A staff member cannot be deleted while they have future scheduled shifts
  or active group assignments. Soft-delete instead.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A staff member works at location A on Monday and location B on Tuesday.
  Their profile is one record; their daily assignment is per-schedule (012).
- A director is also a staff member (covers a group when short-staffed).
  The role field allows both roles on the same account.
- A staff member leaves the organisation. Soft-delete preserves their history
  (past shifts, event authorship) without exposing them as an active user.

Out of scope:
- Staff HR dossier (contracts, training records) — Phase 2.
- Leave requests via staff app — Phase 2.
- Time registration (clock in/out) — Phase 2.
- Payroll / Humanwave integration — Phase 4.
```

**Shipped 2026-07-06** — `specs/005-staff/` (spec → clarify → plan → tasks → checklist → analyze → implement → converge, 75/75 tasks incl. a 10-task requirements-quality follow-up phase and a 2-task convergence pass, 31 new integration tests + all 78 pre-existing tests passing, 109/109 total). Scope deltas worth knowing before starting feature 006+:

- **Every finding from `/speckit-checklist` (26 items) and `/speckit-analyze` was fixed, not just logged as advisory** — a process change made mid-feature (see `checklists/requirements-quality.md` for the per-item resolution notes). Future features should do the same: don't leave checklist/analyze items open with a "proceeding anyway" note.
- A director covering shifts gets an **optional Staff Profile attached to their existing Director account** — no second account, no role change capability exists anywhere in this feature (deliberately: User Story 3 originally implied role-changing, which was removed as a genuine spec-internal contradiction found during the checklist pass).
- **Two real bugs found and fixed during implementation, beyond the original plan**: (1) `POST /api/staff/accept-invitation` needed an `organisationSlug` field — it's anonymous/tenant-exempt, so `TenantMiddleware` has no JWT to resolve a tenant from, exactly the same problem `ResetPasswordCommandHandler` (feature 003) already solves; the original contract draft omitted this and would have shipped unusable. (2) `LoginCommandHandler` crashed with a `500` instead of a clean `401` when `BCrypt.Verify` ran against an empty `PasswordHash` (a staff account whose invitation hasn't been accepted yet) — fixed with an explicit empty-hash check before calling `BCrypt.Verify`.
- Deactivating a `Staff`-role account now **invalidates all of that account's refresh tokens** (mirrors `ResetPasswordCommandHandler`'s session-invalidation) — but this is deliberately conditioned on `Role == Staff`: a Director's own optional Staff Profile being deactivated must never block that Director's login. **Flag for any future feature touching `LoginCommandHandler`**: preserve this role-conditional check.
- `IProfilePhotoStorage`/`GcsProfilePhotoStorage` (GCS V4 signed URLs) is this project's first real Cloud Storage integration — feature 006 (children) is expected to reuse this port rather than rebuild it, following the same "object path stored, URL re-signed fresh on every read" pattern.
- `IStaffDeactivationGuard` extension point exists (mirrors feature 004's `ILocationDeactivationGuard`) for features 010/012 to register their own guards once shifts/group assignments exist — zero guards registered by this feature, by design.

---

### 006 — Children

```
Build the child file — the central record for every child enrolled
or on the waiting list at a KDV.

What to build:
- Child profile: first name, last name, date of birth, profile photo
  (signed GCS URL), gender (optional), nationality (optional).
- Medical information: allergies (free text + severity), medical conditions,
  dietary restrictions, GP name + phone, health insurance number.
- Contacts: multiple contacts per child with roles
  (mother, father, guardian, emergency contact, authorised pickup).
  Each contact: name, phone, email, relationship, can_pickup boolean.
- Group/section assignment with date ranges (a child moves groups as they grow).
- kindcode field (TEXT, nullable): Opgroeien child identifier in format
  YYMMDD-NNN. Not required for private KDVs in Phase 1 but the field must
  exist for Phase 3 IKT reporting.
- Vaccine / health record tracking: vaccine name, date administered,
  next due date. Alert when a vaccine is due.
- Soft-delete / deactivation when a child leaves. History is preserved.
- locale preference on the child's primary contact (for parent app language).

Medical quick-access on caregiver app:
- Allergies and medical notes must be reachable in one tap from the group view.
  The data model must support this — no nested navigation required.

Key constraints:
- Children belong to the tenant schema (schema-per-tenant). No cross-tenant
  child data is ever accessible.
- A child can be linked to multiple contacts, including contacts shared with
  a sibling (same family, different child records). Model contacts separately
  from children with a junction table.
- All user-facing strings use i18n keys (NL/FR/EN).
- Photo storage: GCS signed URLs only. No public blob URLs.

Edge cases:
- Two children in the same KDV are siblings — they share the same parents
  as contacts. The data model should not duplicate contact records.
- A child's primary contact changes (e.g. custody change). The old contact
  history should be auditable.
- A child is on the waiting list (no contract yet) but already has a file.
  The file exists independently of a contract.

Out of scope:
- Contract creation (feature 007).
- eID auto-fill registration (Phase 2).
- Child document storage / file uploads beyond photos (Phase 2).
- GDPR data export per child (Phase 2).
```

**Shipped 2026-07-07** — `specs/006-children/` (spec → clarify → plan → tasks → checklist → analyze → implement → converge, 96/96 tasks, 30 new integration tests + all 109 pre-existing tests passing, 139/139 total). Scope deltas worth knowing before starting feature 007+:

- **`Group` is a new, minimal entity** (name, scoped to a `Location`) — the first feature to introduce it, since 004 explicitly deferred group/section management. No full group administration (capacity, BKR config) exists yet; only enough to name a group and assign children to it. A group can only be created against an *active* location.
- **`IProfilePhotoStorage` (feature 005) was generalized** from a staff-only path convention (`staff/{id}/photo.jpg`) to `(category, subjectId)` — every feature-005 call site was updated to pass `"staff"` explicitly, with no behavior change. Future features needing signed-URL photo storage should reuse this port with their own category string rather than building a parallel mechanism.
- **A genuine data-model bug was found and fixed during `/speckit-analyze`, before it shipped**: an earlier draft let a `(ChildId, ContactId)` pair have multiple `ChildContact` rows (one per relationship), but the `PUT`/`DELETE /api/children/{childId}/contacts/{contactId}` routes had no way to disambiguate between them. Fixed by collapsing to one relationship per pair (mutable via update) instead of adding route complexity — worth remembering as a general caution: a join entity's route design and its uniqueness constraint must agree before either ships.
- Contacts are data records only — no parent-facing login account is provisioned here. **Flag for 013 (Parent Communication)**: whoever builds parent account provisioning will need to decide how a `Contact` record relates to a future parent `TenantUser` (e.g., linking by email, or a separate provisioning step) — this feature deliberately left that undecided.
- `IChildDeactivationGuard` extension point exists (mirrors `ILocationDeactivationGuard`/`IStaffDeactivationGuard`) for feature 007 to register once a child can have an active contract — zero guards registered by this feature, by design.

---

### 007 — Contracts

```
Build the enrolment contract — the agreement between a KDV location
and a child's family defining care days, rate, and consent.

What to build:
- Contract record linking a child to a location with:
    start_date, end_date (nullable = open-ended)
    contracted days: which weekdays (Mon–Fri) and planned hours per day
    daily_rate_cents: the private rate (fixed amount in cents)
    status: draft | active | ended
- Contract versioning: when terms change, the old contract is ended
  (end_date set) and a new contract is created. Full audit trail.
- Photo/media consent types stored as JSONB on the contract:
    { photos_internal, photos_website, photos_social_media,
      video_internal, photos_press }
  Each is a boolean. Replaces the old single photo_consent boolean.
- Contract PDF generation using QuestPDF. The PDF must include all
  contracted terms, consent types, and a signature line.
- A child may have at most one active contract per location at any time.

Split-location enrolment (key business rule):
- A child may hold two simultaneous active contracts at DIFFERENT locations
  within the SAME organisation (tenant), provided the contracted weekdays
  do not overlap.
- Example: Mon+Tue at location A, Wed+Thu at location B — allowed.
  Mon+Tue at A, Tue+Wed at B — rejected (Tuesday overlap).
- Day-overlap validator runs on contract activation. Checks all active
  contracts for the same child across all locations in the tenant.
  Rejects activation if any weekday appears in more than one contract.

Key constraints:
- Phase 1 = private KDVs only. No IKT rate fields in this feature.
  The data model must have columns for tarief_code, rate_valid_until
  (nullable) so Phase 3 IKT can be added without migration pain.
- All money values stored in cents (integers). Never floats for money.
- daily_rate_cents represents the parental contribution — what the parent
  actually pays. Not the gross KDV rate.
- Contract PDF uses QuestPDF (MIT). No other PDF library.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- The day-overlap validator must handle concurrent activation requests
  (two staff members activating two contracts for the same child simultaneously).
- A contract is created for a child who is still on the waiting list.
  The waiting list entry status should update to 'enrolled' on activation.
- A contract ends and a new one starts same day (transition). The validator
  must not block this — only check against currently ACTIVE contracts.

Out of scope:
- IKT attest entry and rate fields (Phase 3).
- Digital e-signature (Phase 2).
- SEPA mandate in contract signing (Phase 2).
- Wisseldagen (exchange days) — handled in feature 010 attendance.
```

**Shipped 2026-07-07** — `specs/007-contracts/` (spec → clarify → plan → tasks → checklist → analyze → implement → converge, 72/72 tasks, 38 new tests + 139 pre-existing = 177/177 passing). Worth knowing before starting a dependent feature:

- **`Contract` stores contracted weekdays and photo/media consent as JSONB owned types** (`ContractedDays`, `Consent`), not separate tables — each weekday has its own independent start/end time. `TariefCode`/`RateValidUntil` columns exist (Phase 3 IKT placeholders) but are never set or exposed by this feature.
- **The split-location day-overlap validator (constitution Principle II) is made atomic under concurrency via a new `IAdvisoryLockService`**, a Postgres advisory lock keyed on the child — a deliberately *separate* port from feature 001's `ITenantProvisioningService.RunExclusiveAsync`, reusing the pattern rather than generalizing that existing interface (out of this feature's scope). Any future feature needing "serialize concurrent requests touching the same aggregate" should reuse `IAdvisoryLockService`, not reinvent a third copy.
- **A real bug was caught by `/speckit-implement`'s own tests, not by review**: an early design assumed EF Core's identity-map would let a query "see" an in-memory (unsaved) `Status = Ended` change on a tracked entity — it doesn't; the query's SQL WHERE clause matches the *persisted* column value regardless. `AmendContractCommand` now excludes the predecessor by explicit id instead. Worth remembering generally: don't rely on EF's tracked-instance identity map to reflect an unsaved property change inside a query filter.
- **`IContractPdfGenerator` (QuestPDF) is the first PDF generation in this codebase** — future features needing PDFs (fiscal attestations, feature 015) should follow its port/adapter split and locale-keyed label-lookup pattern rather than adding a second PDF mechanism.
- Contract PDF generation accepts an optional `?locale=nl|fr|en` query parameter (defaults to `nl`) — the only endpoint in this codebase where language is server-selected at request time rather than client-resolved from an error key, since a PDF is a fixed set of bytes rendered once.

---

### 007a — Web Admin Scaffold

**Added 2026-07-08, while planning feature 008a.** Every feature so far — including 005 (staff)
and 007 (contracts) — has referenced "the web admin" for director-facing screens, but nothing
has actually built it: `web/` is still the original Habits walking-skeleton template
(`app/(app)/habits/`, `subscription/`, `settings/`), and features 005/007 shipped backend-only,
with zero corresponding UI. Feature 008a's PIN-management and device-revocation user stories
are scoped backend-only for exactly this reason (see its spec's Assumptions) — this feature is
the actual mobile-scaffold-equivalent for the web app, so 008a (and future features) have
somewhere real to put a screen.

```
Bootstrap the director web admin app — mirrors what feature 008 did for the
caregiver Expo app, but for Next.js. Remove the Habits walking skeleton,
wire director authentication, establish the navigation shell, and ship one
real screen (staff list, since features 005/008a both need it) so future
features stop having to defer their web UI.

What to build:

1. App cleanup and structure
   - Remove all Habits screens, navigation, and references (habits/, subscription/, settings/
     as they exist today) from the Next.js app.
   - Establish the app directory structure: (auth)/login, (app)/layout.tsx (sidebar nav,
     per design-decisions.md's "director web uses sidebar navigation"), (app)/staff/page.tsx.
   - Tailwind + shadcn/ui, per constitution's stack — high-density layouts per
     platform-rules.md's Director Web section (tables, filtering, information density).
   - i18n wired (next-intl), NL/FR/EN from day one, per constitution Principle IV.

2. Director authentication
   - Login screen: email + password, and Google OAuth (constitution: "Web admin: email/password
     + Google OAuth") — reuses feature 003's existing auth contract, same as feature 008 reused
     it for the caregiver app.
   - JWT + refresh token, stored appropriately for a web context (httpOnly cookie via the
     existing app/api/set-refresh-token route already scaffolded in web/, not localStorage).
   - Session persists across browser restarts until explicit logout or token expiry.

3. Navigation shell
   - Sidebar navigation (design-decisions.md), collapsible, showing the organisation name and
     the signed-in director's name.
   - Empty/placeholder nav entries are fine for sections not yet built (locations, contracts,
     etc.) — this feature only needs Staff to be real.

4. Staff list screen (first real content)
   - GET /api/staff (feature 005, already exists) rendered as a filterable/searchable table —
     per platform-rules.md's director-web density expectations, not a caregiver-tablet-style
     card list.
   - Each row: name, role, location(s), active/deactivated status.
   - Row action: set/reset a caregiver's 4-digit PIN (feature 008a's
     PUT/DELETE /api/staff/{id}/pin, already built by that feature — this screen is the first
     real caller of it).
   - Row action: deactivate/reactivate (feature 005, already exists as an API, wire the UI).

5. Devices screen (feature 008a device management)
   - List paired tablets (location, group, paired-by, paired-at).
   - Revoke action (feature 008a's POST /api/devices/{id}/revoke).

Key constraints:
- Follow design-system.md/platform-rules.md/reference-products.md, same as every feature since
  008 — director web should feel like Linear/Notion/Airtable, not a caregiver-tablet layout
  stretched wide.
- All user-facing strings use i18n keys (NL/FR/EN).
- Reuse feature 008's openapi-typescript + openapi-fetch API-client pattern rather than the
  Habits-era `lib/api.ts` fetch wrapper currently in web/.

Out of scope:
- Any screen beyond Staff and Devices — locations, contracts, children, reporting, etc. are
  later features' job, this one just proves the shell works end-to-end with one real screen.
- Google OAuth for caregivers/parents — unrelated to this feature.
```

**Post-shipping note (added while planning, before this feature has been implemented):** if a
future feature's spec needs a "web admin" screen and this feature isn't done yet, treat that as
a hard dependency, not something to defer with another backend-only workaround — the pattern
of "web admin" being referenced-but-never-built has already happened twice (005, 007).

---

### 008 — Caregiver App Scaffold

```
Bootstrap the caregiver Expo app — remove the Habits walking skeleton,
wire authentication, set up the API client, establish the navigation
structure, and build the shared offline infrastructure layer that all
caregiver features (child events, attendance, scheduling) will depend on.

This feature ships NO domain features. Its sole output is a working,
authenticated caregiver app shell with offline capability ready for
feature 009 (child events) to build on top of.

What to build:

1. App cleanup and structure
   - Remove all Habits screens, navigation, and references from the Expo app.
   - Establish the Expo Router directory structure:
       app/(auth)/login.tsx        — login screen
       app/(app)/_layout.tsx       — authenticated shell (tab bar)
       app/(app)/index.tsx         — group view (home screen)
       app/(app)/child/[id].tsx    — child detail (event entry point)
   - Tailwind / NativeWind configured for landscape-first layout.
   - i18n wired: expo-localization + react-i18next, NL/FR/EN from day one.

2. Caregiver authentication
   - Login screen: email + password. No social login for caregivers.
   - On successful login: store JWT + refresh token (SecureStore, not AsyncStorage).
   - Per-device refresh token rotation: re-use the backend mechanism from feature 003.
   - Auto-refresh: intercept 401, refresh silently, retry the original request.
   - Logout: call revoke endpoint, clear SecureStore, redirect to login.
   - Session persists across app restarts (SecureStore survives backgrounding).

3. API client
   - Generate typed client from the backend OpenAPI spec using openapi-typescript.
   - HTTP client: openapi-fetch (already decided in PROJECT-BRIEF.md — no Axios, no NSwag).
   - Auth interceptor: attach Bearer token to every request; handle refresh on 401.
   - Base URL configurable via Expo env vars (dev = localhost, prod = Cloud Run URL).
   - Typed error handling: API errors surface the i18n key from the error body.

4. Group view (home screen)
   - After login, the caregiver sees the children in their assigned group for today.
   - Loads: GET /children?groupId=... + GET /groups (scoped to caregiver's location).
   - Each child card shows: name, profile photo (signed URL), age, any active alerts
     (allergy icon if allergies exist, temperature icon if fever recorded today).
   - Medical quick-access: one tap from child card → allergies + medical notes sheet
     (data already exists from feature 006 — just wire the read endpoint).
   - Pull-to-refresh. Empty state when no children are assigned.
   - Offline: child list and medical notes for today's group are cached in SQLite
     on successful load. Available with no network.

5. Offline infrastructure layer (shared, used by 009 and 010)
   This is the critical piece. Build it generically — 009 (child events) and
   010 (attendance) will register their own sync handlers against it.

   Local SQLite store (expo-sqlite):
   - offline_queue table:
       id TEXT PRIMARY KEY (client-generated UUID),
       entity_type TEXT NOT NULL,    — 'child_event' | 'attendance_record' | ...
       operation TEXT NOT NULL,      — 'create' | 'update' | 'delete'
       payload TEXT NOT NULL,        — JSON-serialised request body
       endpoint TEXT NOT NULL,       — relative API path
       http_method TEXT NOT NULL,    — 'POST' | 'PATCH' | 'DELETE'
       created_at TEXT NOT NULL,     — ISO8601, used for ordering on sync
       synced_at TEXT,               — NULL = pending sync
       sync_error TEXT               — last sync attempt error, NULL = none
   - read_cache table:
       cache_key TEXT PRIMARY KEY,
       data TEXT NOT NULL,           — JSON
       cached_at TEXT NOT NULL,
       expires_at TEXT               — NULL = never expires (e.g. child list for today)

   Network state detection:
   - @react-native-community/netinfo: subscribe to connection changes.
   - Expose a useNetworkStatus() hook that the app-wide banner reads.
   - When offline: show a banner ("Werken offline — wijzigingen worden gesynchroniseerd").
   - When network returns: trigger sync automatically (no manual tap needed).

   Sync engine:
   - syncPendingQueue(): reads unsynced rows from offline_queue ordered by created_at ASC.
   - For each row: replay the HTTP request against the live API.
   - On success: set synced_at = now(). Leave the row (for audit); do NOT delete.
   - On conflict (409): entity_type handler decides — default is server wins
     (discard the queued write and mark as synced with a 'conflict' note in sync_error).
   - On transient error (5xx, network timeout): leave row unsynced, retry on next sync.
   - On auth error (401): refresh token first, retry once, then stop sync and surface error.
   - Sync runs: on network reconnect, on app foreground, on explicit pull-to-refresh.
   - Sync status: expose useSyncStatus() hook → { pendingCount, lastSyncedAt, isSyncing }.

   This feature ships the infrastructure only. No entity_type handlers are registered
   yet — 009 registers 'child_event' and 010 registers 'attendance_record'.

Key constraints:
- SecureStore for all tokens (JWT, refresh). Never AsyncStorage for auth data.
- All user-facing strings use i18n keys (NL/FR/EN) — including offline banner and
  sync error messages.
- Landscape-first layout, minimum 48pt touch targets everywhere (preparing for 009).
- The offline_queue and read_cache tables are tenant-scoped: include tenant_id on
  every row so the cache is invalidated correctly on logout or account switch.
- On logout: clear SecureStore, clear offline_queue (unsynced writes are lost —
  document this as a known limitation), clear read_cache.

Edge cases:
- A caregiver logs in on a new tablet. There is no cached data yet. The group view
  must load from the network on first use and cache on success.
- The app is offline on first launch (a caregiver left the tablet in airplane mode
  overnight). Login must fail gracefully with a clear message — cannot auth offline.
- The caregiver's account is deactivated by the director while the caregiver is
  mid-shift. The next API call returns 401, the refresh attempt returns 401 again
  (token was revoked by deactivation in feature 005). App must log the caregiver
  out cleanly, not loop on the refresh.
- Sync queue has 50+ events queued from a long offline period. Sync must process
  them sequentially (not in parallel) to preserve created_at order within the same
  child/entity.

Out of scope:
- Caregiver scheduling view (their own rota) — feature 012.
- Parent app setup — parent app is a separate Expo project; this feature only
  touches the caregiver app.
- Push notification token registration for caregivers — feature 009 (temperature alert).
- Biometric unlock (Face ID / fingerprint for app re-open) — Phase 2.
- Kiosk/PIN mode — see feature 008a below. The email/password auth built here
  is the underlying mechanism; the kiosk layer sits on top of it.
```

**Post-shipping note (added before implementation completed):** Industry research confirmed that caregivers share a single tablet per section and do not individually log in per shift. Brightwheel, Procare, and Famly all converge on the same pattern: a room-based kiosk tablet (set up once by the director) with a 4-digit PIN per caregiver for shift identification. The email/password + JWT infrastructure built in this feature remains valid as the underlying auth mechanism. A kiosk mode layer will be built on top of it in feature `008a-caregiver-kiosk-mode` before any caregiver UI features ship to real users. The personal login screen delivered here is scaffolding only — do not invest in its UX.

---

### 008a — Caregiver App Kiosk Mode

```
Replace the personal-login model from feature 008 with a room-based
shift register. The tablet is permanently authenticated as a room device.
Caregivers check in and out with a PIN to record their physical presence.
Event logging requires no individual auth — the device token is sufficient.

Auth layers (both always active):

  Layer 1 — Device token (tablet ↔ backend security):
    The tablet holds a long-lived JWT scoped to its location + group,
    obtained during one-time director setup and stored in SecureStore.
    Every API call carries this token. The backend rejects any request
    without it. A phone on the same WiFi cannot post events. This layer
    is never bypassed — it is the security boundary between the tablet
    and the API regardless of who is or isn't checked in.

  Layer 2 — Shift register (caregiver presence log):
    Rides on top of the device token. Tracks who is physically in the
    room and when. PIN check-in/out writes to a server-side shift log.
    This is identity and accountability, not HTTP authentication.
    Two caregivers can be simultaneously checked in — this is the norm,
    not an edge case.

Background:
Belgian KDVs typically have 2 caregivers per room simultaneously (BKR).
A single "active session" model — where one caregiver owns the tablet —
does not reflect reality. The shift register model accurately captures
presence: Marie arrives at 08:02, Thomas at 08:47; both are on duty;
either can log any routine event without needing to "claim" the tablet.

What to build:

1. Room setup (director, one-time per tablet)
   - Director opens the app and logs in with email + password (008 flow).
   - Director selects: this tablet belongs to location X, group Y.
   - Backend issues a device token: a long-lived JWT signed with a
     dedicated device secret, carrying { tenant_id, location_id, group_id,
     device_id }. Stored in SecureStore. Never expires passively —
     only revoked explicitly (see lost-tablet below).
   - App locks into room mode. The email/password screen is no longer
     reachable by normal caregivers.
   - Room assignment survives app restarts and OS backgrounding.
   - Director can exit room mode via a 6-digit director override PIN
     (set during room setup, stored as bcrypt hash in SecureStore).

2. Room home screen (permanent state of the tablet)
   - Shows: location name, group name, current date + time.
   - Shows who is currently checked in (names + check-in time). Empty
     at the start of the day.
   - Large PIN keypad (minimum 64pt targets). Used for both check-in
     and check-out.
   - "Who's here" list below the keypad — visible at a glance.

3. Caregiver check-in / check-out
   UX pattern: select-then-PIN (industry standard for small, known
   populations; PIN-only is better suited for large parent pools).

   - Room home screen shows all caregivers assigned to this group as
     large photo cards (name + avatar). Checked-in caregivers are
     visually distinct (e.g. green ring, check-mark).
   - Caregiver taps their own photo card.
   - A PIN keypad overlay appears: "Geef je code in, [Name]".
   - If not checked in: POST /room-shifts/check-in with device token +
     { staff_id, pin }. Server validates PIN (bcrypt) against the
     caregiver identified by staff_id, checks they are assigned to
     this location, records check-in timestamp. Card updates to
     checked-in state.
   - If already checked in: same flow → POST /room-shifts/check-out.
     Records check-out timestamp. Card returns to unchecked state.
   - The app returns to the room home screen immediately after either
     action. No "session" is opened — the tablet state is unchanged.
   - Incorrect PIN: shake animation + "Ongeldige code" message.
     Rate limit: 5 failed attempts in 2 minutes → 10-minute lockout
     for that staff_id on this device (device token remains valid).
   - Why select-then-PIN over PIN-only: with 2 caregivers per room,
     photo cards are immediately obvious and eliminate lookup ambiguity.
     The server receives staff_id explicitly; it does not need to
     reverse-lookup who owns the PIN (cleaner, avoids 4-digit
     collisions across a growing staff list).

4. Event logging (no auth gate)
   - Any caregiver can tap a child and log any routine event at any time.
   - No PIN prompt. The device token on the request is sufficient auth.
   - recorded_by on the event is populated server-side from the shift log:
     who was checked in at occurred_at. If one caregiver: that person.
     If two: store both IDs as a JSONB array (recorded_by UUID[]).
     If no one checked in yet (opening minutes of the day): recorded_by
     = null. Do not block logging.

5. Medical events — PIN confirmation
   - medication and temperature events prompt an extra step before submit:
     "Bevestig wie dit registreert" — same select-then-PIN pattern as
     check-in/out (section 3): shows the currently-checked-in caregivers
     as tappable cards (typically 1-2, per BKR), caregiver taps their own,
     then a PIN keypad overlay addressed by name. POST with { staff_id, pin }.
   - Entering a valid PIN attaches that caregiver's ID to a dedicated
     administered_by field (separate from recorded_by).
   - This is a UX confirmation step, not a re-auth. The device token
     already authorised the request; the PIN just names the individual.
   - If the caregiver taps "Skip" (allowed): administered_by = null.
     Director can fill it in retroactively from the web admin.
   - Sending staff_id keeps this on the same simple per-caregiver PIN
     lockout as check-in/out (shared counter, section 3) rather than
     needing a separate mechanism for an anonymous PIN-only guess.

6. PIN management (web admin, feature 005 extended)
   - Director sets or resets a caregiver's PIN from the staff screen.
   - PIN stored as bcrypt hash on the staff record.
   - PINs must be unique within a location.
   - PIN reset does not affect the caregiver's account password.

7. Device token lifetime and rotation
   - TTL: 30 days from issuance.
   - Silent rotation: on any API call where the token has fewer than
     7 days remaining, the server issues a new token in a response
     header (X-Device-Token-Refresh). The app swaps it into SecureStore
     and the old token is immediately invalidated server-side.
   - Rotation is time-based, NOT on every request. Rotation-on-every-
     request would break offline sync: if 30 queued events arrive on
     reconnect, the first would rotate the token and the remaining 29
     would be rejected with a stale-token error. Time-based avoids this.
   - On reconnect after an offline period: if the token needs rotation,
     the app rotates first (one call), then replays the offline queue.
     All queued events carry the still-valid old token and are accepted.
   - If offline for the full 30-day TTL and the token expires: next
     API call returns 401 device.token_expired. App shows a
     "Heractivatie vereist" screen — director must log in to re-pair.
     In a functioning KDV this is an extreme edge case.
   - Rotation requires no user action and is invisible to caregivers.

8. Device revocation (lost or stolen tablet)
   - Director marks a tablet as revoked in the web admin (Devices
     section, scoped to the location).
   - Server adds device_id to a revocation list checked on every
     API call — not only at token-issuance time.
   - Next API call from the tablet returns 401 device.revoked. App
     wipes SecureStore entirely and returns to the email/password
     setup screen, ready to be re-paired on a replacement tablet.
   - Any queued offline events from the revoked tablet are rejected
     on sync and logged server-side for audit.
   - Two lines of defence for a lost tablet:
       Primary:  explicit revocation by director (immediate).
       Backstop: 30-day TTL — an unrevoked stolen tablet becomes
                 useless within one rotation window at most.

Key constraints:
- The device token is the security boundary. All API calls carry it.
  No call is unauthenticated regardless of shift state.
- Token storage: SecureStore only. Never AsyncStorage. Must survive
  app restarts and OS-level backgrounding.
- PIN never sent in plaintext. All PIN checks happen server-side
  (POST /room-shifts/check-in, POST /room-shifts/check-out,
  POST /events/:id/confirm-administrator). Client sends raw PIN over
  HTTPS; server bcrypt-compares and discards immediately.
- Offline: check-in/out events queued in offline_queue (entity_type
  = 'room_shift'). Routine event logging works offline unchanged.
  Medical PIN confirmation skipped offline (administered_by = null;
  director fills in retroactively from web admin).
- All user-facing strings use i18n keys (NL/FR/EN).
- room_shifts table (tenant schema):
    id, device_id, staff_id, location_id, group_id,
    checked_in_at TIMESTAMPTZ, checked_out_at TIMESTAMPTZ (nullable),
    created_at

Edge cases:
- Two caregivers check in simultaneously (race). Both succeed — the
  server accepts concurrent check-ins for the same room.
- A caregiver forgets to check out at end of day. Auto-checkout at
  midnight for any shift without a check-out. Director can correct.
- Tablet reassigned to a different group mid-day. Director override →
  exit room mode → re-run room setup. Existing open shift entries
  for the old group are auto-closed at the moment of re-setup.
- A caregiver's account is deactivated (feature 005). Their PIN
  immediately returns a 403 on check-in attempt. If they were already
  checked in, their open shift is closed server-side on deactivation.
- Dual-location caregiver (feature 005): one PIN, works at any
  location they're assigned to. Server validates assignment at
  check-in time.

Out of scope:
- QR code or NFC tap for check-in (Phase 2).
- Biometric unlock (Face ID / fingerprint) — Phase 2.
- Live multi-tablet presence sync (two tablets showing each other's
  checked-in caregivers in real time) — Phase 2. Phase 1: each
  tablet shows who it knows about from the last successful sync.
```

---

### 009 — Child Events

```
Build the child event timeline — the daily log that caregivers record
and parents see in real time.

What to build:
- child_events table (single JSONB table, replaces the old daily_logs flat table):
    id, child_id, event_type, occurred_at, ended_at (sleep only),
    payload JSONB, visible_to_parent BOOLEAN, recorded_by, created_at, updated_at

- Event types and their payload schemas:
    sleep:           { quality: "good|okay|restless", duration_minutes: int }
                     ended_at stores when the nap ended. duration_minutes
                     stored explicitly in payload for efficient filtering.
    temperature:     { celsius: decimal }
                     If celsius > 38.0, trigger push notification to all
                     authorised contacts of this child with can_pickup = true.
    medication:      { name: "perdolan|nurofen|antibiotics|other",
                       dose_description: text, reason: text,
                       next_dose_not_before: timestamptz (optional) }
    feeding_bottle:  { ml: int }
    feeding_solid:   { description: text }
    diaper:          { type: "wet|dirty|both", notes: text (optional) }
    mood:            { value: "great|good|okay|difficult" }
    activity:        { description: text }
    note:            { text: text }
    weight:          { kg: decimal }  — legally required in Belgian KDVs
    measurement:     { weight_kg: decimal (optional), height_cm: decimal (optional),
                       head_cm: decimal (optional) }
                     Any subset of fields is valid.

- Full CRUD per event. Caregivers can edit same-day events; directors can
  edit any event. Soft-delete preferred over hard-delete.
- visible_to_parent = false marks an event as internal staff note only.
  These events never appear in the parent app.
- Photos can be attached to an event (child_event_id FK on photos table)
  or to a child + date (no event FK). Signed GCS URLs.

Caregiver tablet UI requirements:
- Icon-based quick-entry (large touch targets, minimum 48pt, prefer 64pt).
- Quick-action bottom sheet for routine entries (diaper, bottle, mood)
  rather than full-screen modal.
- Optimised for wet hands and divided attention — minimal typing required.
- Landscape-first layout.
- The Expo app scaffold, auth, navigation, and API client already exist
  (feature 008). Build event entry UI on top of that — do not re-implement
  auth, offline infrastructure, or API client setup.

Offline event recording (CRITICAL — caregivers must work through network outages):
- Uses the offline_queue + sync engine built in feature 008.
- Register 'child_event' as an entity_type in the sync engine.
- When online: POST directly to /child-events. On success, update read_cache.
- When offline: write to offline_queue (entity_type='child_event',
  operation='create'). Show event immediately in the local UI (optimistic).
- Sleep end (PATCH with ended_at): if the original create is still in the queue
  (not yet synced), merge ended_at into the queued payload — do not queue a
  separate PATCH. If already synced, queue a PATCH normally.
- Conflict policy: ALL WRITES PRESERVED. Events are append-only logs — two
  tablets recording different events offline are both valid. The only
  genuine conflict is two tablets ending the same sleep event: server timestamp
  wins for ended_at. All other events never conflict.
- "Pending sync" indicator on events not yet confirmed by the server.
- On sync: swap the client-generated id for the server id. No visible change
  to the caregiver.

Temperature push notification:
- If celsius > 38.0: trigger Expo Push Notification to all can_pickup contacts
  of this child with a registered push token.
- Register caregiver app push token at login (first push token in caregiver app).
- If no push tokens exist: log the attempt, do not crash.
- While offline: queue the event normally. On sync, the server fires the push
  notification — never fire it from the client directly.

Parent daily summary:
- Computed at query time by aggregating child_events for a given date.
  No materialised view needed for Phase 1.
- Returns counts + last values: naps taken, bottles given, diaper changes,
  mood assessment, latest temperature, any medication administered.

Key constraints:
- Indexes: (child_id, occurred_at DESC) and (child_id, event_type, occurred_at DESC).
- All user-facing strings use i18n keys (NL/FR/EN).
- This table grows fast. Design queries with pagination from the start.

Edge cases:
- A sleep event is recorded offline with no ended_at (nap in progress). The UI
  shows "in progress". Caregiver ends it later, still offline. Both operations
  land in the queue; the create is merged with the ended_at before sync.
- Caregiver offline 4 hours, 30 events queued. On reconnect, all 30 sync in
  created_at ASC order and all land correctly.
- A caregiver records an event for the wrong child. Edit/delete must be possible
  within a grace period (same-day, same caregiver or director).

Out of scope:
- Meal list for kitchen (read-only view from planning data — tracked here).
- Learning journal narrative (Phase 2).
```

---

### 010 — Attendance

```
Build daily attendance registration — the core operational record of
which children are present at the KDV each day.

What to build:
- attendance_records table:
    id, child_id, location_id, date, status (present|absent|closure),
    check_in_at TIMESTAMPTZ, check_out_at TIMESTAMPTZ,
    planned_duration_minutes INT (from contract schedule — used for Opgroeien
    reporting in Phase 3; must be stored now),
    absence_justified BOOLEAN (true = respijtdag / gerechtvaardigde afwezigheid;
    false = niet_gerechtvaardigd),
    absence_reason TEXT,
    recorded_by UUID

- Caregiver app: one-tap check-in per child from the group view.
  Children are pre-populated for the day based on their contracted schedule.
  The Expo app scaffold, auth, and API client already exist (feature 008).
  Build the attendance UI on top of that scaffold — do not re-implement them.
- Offline check-in: uses the offline_queue + sync engine from feature 008.
  Register 'attendance_record' as an entity_type in the sync engine.
  Conflict policy: server wins. If a check-in arrives out of order
  (reconnecting tablet with stale data), the server timestamp is authoritative.
  A duplicate check-in for the same child+date returns 409 — mark as synced
  with a conflict note, do not retry.
- Absence registration: caregiver or director marks a child absent and
  classifies as justified or unjustified. Justified = director authorised
  the absence (sick with note, agreed holiday). Unjustified = not pre-approved.
- Exchange day and extra day requests (from parents, via feature 013) appear
  in the attendance view once approved.
- Closure days (from feature 011) auto-mark all children absent with
  status = 'closure'. No manual check-in is possible on closure days.

BKR ratio (begeleider-kind-ratio) — display only in Phase 1:
- Compute live: present children ÷ on-duty qualified staff at this location now.
- Rules (already decided): solo caregiver max 8; 2+ caregivers max 9 per caregiver;
  nap time (≤2h) max 14; leefgroep (living group) max 18.
- Students and volunteers do NOT count as qualified staff for BKR.
- Show colour-coded indicator in caregiver app (green/amber/red).
- Do NOT hard-block check-in on BKR breach in Phase 1. Warning only.
  Hard enforcement is Phase 2.

planned_duration_minutes calculation:
- Derived from the contract's weekday schedule (monday_hours, tuesday_hours, etc.).
- Convert contracted hours to minutes. A Wednesday contracted at 8h = 480 minutes.
- This value feeds into the Opgroeien FO-SU-05 XML in Phase 3, which classifies
  each day as min5u (<5h = 300 min), min11u (5–11h), or min11uFlex (>11h).
- Store it on attendance_records so Phase 3 doesn't need to recompute from contracts.

Key constraints:
- Attendance is per location per child per day. One record only.
- All money decisions (billing for absences) are made in feature 014 invoicing.
  Attendance records only store the justification flag, not billing amounts.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A child is checked in but their contract doesn't cover that weekday (extra day
  approved by director). Record must still be created.
- Check-out is forgotten at end of day. Director can correct the following morning.
- Offline tablet reconnects after several hours. Queued check-ins arrive out of
  order — the sync must handle this without creating duplicate records.

Out of scope:
- FO-SU-05 XML generation (Phase 3).
- QR contactless check-in (Phase 2).
- Paxton/Net2 hardware integration (Phase 4).
```

---

### 011 — Closure Calendar

```
Build the KDV closure calendar — each location's own holiday and
closure schedule, independent of school holidays.

What to build:
- kdv_closure_days table (per tenant schema, scoped to a location):
    id, location_id, date DATE, label TEXT (e.g. "Kerstvakantie"),
    closure_type TEXT ('holiday' | 'training' | 'extraordinary'),
    notify_parents BOOLEAN DEFAULT TRUE,
    notification_sent_at TIMESTAMPTZ,
    created_by UUID, created_at TIMESTAMPTZ
    UNIQUE(location_id, date)

- Year calendar view in web admin: director sees the full year with
  closure days highlighted. Director adds, edits, or removes closure days.
- When a closure day is published with notify_parents = true, a push
  notification and in-app message is sent to all parents of children
  enrolled at that location.
- Closure days feed into attendance (010): no check-in is possible on a
  closure day; the attendance view marks all children as status = 'closure'.
- Closure days feed into invoicing (014): a contracted day that falls on a
  closure day is NOT billed. This exclusion is automatic — the invoice
  generator must query closure days when computing billable days.

Closure types:
- holiday: planned closure (summer, Christmas, Carnival, Easter, etc.)
- training: staff pedagogical training day (pedagogische studiedag)
- extraordinary: unexpected closure (building issue, extreme weather, etc.)

Key constraints:
- Closure days are per location. Two locations in the same organisation can
  have different closure calendars.
- Notifications are sent immediately when the director publishes a closure.
  No scheduled send — publish = notify.
- A closure day cannot be added in the past (only today or future dates).
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A closure day is added for a date that already has children checked in
  (director adds a closure retroactively). The system must warn rather than
  silently corrupt existing attendance records.
- An extraordinary closure is added same-day (e.g. burst pipe at 8am).
  Push notification must go out immediately to all enrolled parents.
- A closure day is removed after the notification has been sent. Parents
  need to be informed the closure was cancelled.

Out of scope:
- Email notification fallback (Phase 2).
- iCal / Google Calendar export for parents (Phase 2).
- Advance warning rules (remind director to notify 4 weeks before) — Phase 2.
```

---

### 012 — Caregiver Scheduling

```
Build the weekly staff rota — who works where, on which days and hours.

What to build:
- staff_schedules table:
    id, staff_id, location_id, group_id (nullable = unassigned),
    date DATE, start_time TIME, end_time TIME,
    is_absent BOOLEAN DEFAULT FALSE,
    absence_reason TEXT ('sick' | 'leave' | 'holiday'),
    created_at, updated_at
    UNIQUE(staff_id, date, start_time)

- Weekly rota builder in web admin: director builds the schedule for the
  upcoming week by assigning staff members to locations and groups per day.
- Rota copy: copy this week's schedule to next week. Major time saver —
  most KDVs run the same rota week after week.
- Director can mark a staff member absent for a day (sick, leave, holiday).
- When a staff member is absent, BKR ratio (feature 010) must reflect
  that they are not on duty.
- The rota is visible to caregivers in the caregiver app (their own shifts only).
- Multi-location: a staff member can appear at different locations on different
  days within the same week.

BKR integration:
- The live BKR count in feature 010 uses staff_schedules to know who is
  on duty right now at a given location. Only staff with is_absent = false
  and a shift covering the current time count toward BKR.
- Only qualified staff count (qualification field from feature 005).

Key constraints:
- A staff member cannot be scheduled at two locations at the same time
  (overlapping start_time/end_time on the same date at different locations).
- All user-facing strings use i18n keys (NL/FR/EN).
- Soft-delete of past schedules is not needed — past schedules are
  historical records and should be immutable after the date has passed.

Edge cases:
- A staff member's shift is entered incorrectly (wrong location). The director
  corrects it — update must be allowed until the shift date arrives.
- Rota copy: the following week has a closure day (from feature 011).
  The copied schedule for that day should be flagged or excluded.
- A staff member is added to the system mid-week. Their schedule for the
  current week must be enterable immediately.

Out of scope:
- Staff leave requests via staff app (Phase 2).
- Staff clock in/out (Phase 2).
- Staff HR dossier (Phase 2).
- Payroll / Humanwave export (Phase 4).
```

---

### 013 — Parent Communication

```
Build two-way communication between parents and the KDV, plus the
parent's daily summary of their child's day.

What to build:
- message_threads table: id, subject TEXT, child_id (nullable), created_at
- message_thread_participants table: thread_id, user_id (PRIMARY KEY both)
- messages table: id, thread_id, sender_id, body TEXT, sent_at, read_at

- Two-way messaging: a parent can send a message to the director/staff.
  A director/staff member can reply. Thread-based (like email threads).
- Announcements: director sends a broadcast message to all parents of a
  location, or all parents of a specific group. These are one-to-many
  (no reply from parents on announcements, or read-only thread).
- In-app notification centre: all notifications (new message, request
  approved, temperature alert, etc.) visible in one place in the parent app.
- Push notifications (Expo Push): triggered on new message received,
  announcement posted, day request approved/rejected.
- Parent daily summary: the parent app home screen shows an aggregated
  summary of their child's day — pulled from child_events (feature 009).
  Only events with visible_to_parent = true are shown.
  Includes: naps, bottles, meals, diaper changes, mood, temperature,
  photos, activities.

Key constraints:
- Staff-only events (visible_to_parent = false) must NEVER appear in the
  parent view, regardless of how the query is constructed.
- Push notifications require a push_token stored per user+device. Token
  registration happens at app launch. Tokens can expire — handle gracefully.
- All user-facing strings use i18n keys (NL/FR/EN). Notification bodies
  are also internationalised (use the parent's locale preference).
- The parent app only shows data for their own children. No cross-child
  data is ever accessible to a parent.

Edge cases:
- A parent has two children at the same KDV. The daily summary shows
  both children's events on the home screen, clearly separated.
- A parent uninstalls and reinstalls the app — their push token changes.
  The new token must replace the old one on next login.
- A push notification fails (token expired or invalid). Log the failure,
  fall back to in-app notification. Do not crash the sender.
- An announcement is sent to "all parents at location A" but one parent
  has no app installed and no push token. They see the announcement next
  time they log in (in-app notification centre).

Out of scope:
- Email notification fallback (Phase 2).
- Staff group chat (Phase 2).
- Message translation via DeepL (Phase 2).
- Day reservation requests (feature 010 handles approval; the request
  submission UI for parents can be included here or in 010 — decide at plan time).
```

---

### 014 — Invoicing

```
Build monthly invoice generation and payment tracking for private KDVs.

What to build:
- Invoice generation: for each child with an active contract, compute
  billable days for the month and generate an invoice.
- Billable day rule (already decided — Model A):
    Present days only. Justified absences (respijtdagen) = not billed.
    Unjustified absences = billed at the daily_rate_cents from the contract.
    Closure days (from feature 011) = not billed, regardless of absence type.
  So: billable = (present days) + (unjustified absent days) - (closure days).
- invoices table:
    id, child_id, contract_id, location_id, period_month DATE (first of month),
    status TEXT ('draft'|'sent'|'paid'|'overdue'),
    subtotal_cents INT, total_cents INT,
    line_items JSONB, ogm_reference TEXT,
    sent_at, paid_at, due_date, created_at, updated_at
- line_items JSONB stores the breakdown:
    present_days, unjustified_absent_days, daily_rate_cents,
    closure_days_excluded, extra_charges (array: {label, amount_cents}).
    Also store duration-categorised counts (days_min5u, days_min11u) for
    future Belcotax/Opgroeien reporting compatibility — even if not used yet.
- OGM structured reference: Belgian payment reference (+++format+++),
  12 digits, modulo-97 checksum. One unique OGM per invoice. Allows
  automatic bank payment matching later.
- Invoice PDF generated with QuestPDF. Required fields:
    KDV name, address, KBO/ondernemingsnummer (if set), erkenningsnummer (if set)
    Parent name, child name
    Period (month + year)
    Line items breakdown
    Total due, due date
    OGM reference (prominent — parents use this for bank transfer)
    Bank account number of the KDV
- Invoice status lifecycle: draft → sent (email/in-app) → paid (manual) | overdue.
- Payment recording: director manually marks an invoice as paid with a date.
- Parent can view invoices and download PDF in the parent app.

Key constraints:
- Phase 1 = private KDVs only. No IKT subsidy lines, no tarief_code.
  The data model must accommodate IKT fields as nullable for Phase 3.
- All money in cents (integers). Never floats.
- One invoice per child per month per location. A child with split-location
  contracts (two locations) gets two invoices per month.
- QuestPDF (MIT licence) for all PDF output.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A contract starts mid-month. The invoice covers only the contracted days
  from the start date to end of month.
- A contract ends mid-month. Invoice covers from start of month to end date.
- A child has no present days and no unjustified absences in a month (all
  closure days or all justified absences). The invoice total = 0. Still
  generate it for the audit trail, or skip? Decide at plan time.
- Director regenerates an invoice after correcting an attendance record.
  The old PDF must be replaced, and the parent notified.

Out of scope:
- CODA/CODABOX bank import (Phase 2).
- SEPA direct debit XML (Phase 2).
- In-app payment (Phase 4 — Belgian parents prefer bank transfer).
- IKT subsidy lines and inkomenstarief billing (Phase 3).
```

---

### 015 — Fiscal Attestations

```
Build annual fiscal attestations for parents to claim childcare costs
on their Belgian tax return.

What to build:
- fiscal_attestations table:
    id, child_id, contract_id, tax_year INT,
    line_items JSONB (up to 4 periods — see below),
    total_amount_cents INT,
    pdf_gcs_path TEXT,
    generated_at, created_at

- line_items JSONB structure (up to 4 periods per attestation):
    [{ period_start: date, period_end: date, days: int, amount_cents: int,
       daily_rate_cents: int (optional) }]
  This supports Belcotax Fiche 281.86 electronic submission in Phase 3
  without schema migration.

- PDF generation per child per year (QuestPDF). Required fields on the PDF:
    KDV name, address, KBO/ondernemingsnummer, erkenningsnummer (if set)
    Parent first name + last name
    Child first name + last name, date of birth
    Tax year
    Blank field for parent to fill in their own NRN (nationaal registernummer)
    — do NOT store NRN in the database (GDPR)
    Per-period breakdown: start date, end date, number of days, amount paid
    Total amount paid across all periods
    Certification type: code 1 (Kind & Gezin / Opgroeien regie)
    Declaration text (use Opgroeien official template wording)
    Signature line for KDV responsible

- Amount = parental contribution actually paid (total from paid invoices
  for the tax year). Not the gross KDV rate.
- Bulk generation: director generates attestations for all children at once
  at year-end.
- The generated PDF is stored in GCS and served via signed URL.

Key constraints:
- NRN (nationaal registernummer / SSIN) is NEVER stored in the database.
  The PDF has a blank field that the parent fills in themselves.
- QuestPDF (MIT) for PDF output.
- The data model (line_items JSONB, KBO field) must support Phase 3 Belcotax
  electronic submission without migration.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A child's daily rate changed mid-year (new contract). The attestation must
  show two periods with different daily rates — this is why up to 4 periods
  are supported.
- A parent requests a corrected attestation (they declared the wrong amount
  to the tax authority). Director can regenerate and re-send.
- A child left the KDV mid-year. The attestation covers only the months they
  were enrolled.

Out of scope:
- Belcotax On-web JSON API submission (Phase 3). MVP = PDF only.
  Directors enter data in Belcotax On-web manually.
- SSIN collection and secure storage (only needed for Phase 3 electronic submission).
```

---

### 016 — Developmental Milestones

```
Build developmental milestone tracking — monitoring each child's
progress across developmental domains.

What to build:
- Reference data (seeded, tenant-agnostic but stored in tenant schema):
    developmental_domains: id, code, name_nl, name_fr, name_en
    Domains: motor_gross, motor_fine, language, cognitive,
             social, emotional, self_care
    developmental_milestones: id, domain_id, age_from_months, age_to_months,
    description_nl, description_fr, description_en, sort_order

- Observation records (per child):
    child_milestone_observations: id, child_id, milestone_id,
    status TEXT ('emerging'|'achieved'|'not_yet'),
    observed_at DATE, observed_by UUID, notes TEXT, created_at

- Milestone portfolio view per child: timeline showing all milestones
  grouped by domain, with current status and age-appropriateness highlighted.
- Age-appropriate milestones highlighted: for a child who is 18 months old,
  show milestones in the 15–21 month band as the current focus.
- Share milestone portfolio with parents: either as a PDF export (QuestPDF)
  or in-app view in the parent app.
- Caregiver can record observations from the caregiver app.
  Director can view and manage from web admin.

Key constraints:
- Milestone seed data is pre-loaded per tenant schema on provisioning.
  It uses the standard Belgian developmental framework (not a custom one).
- Observations are immutable once recorded (no edit — add a new observation
  instead). This preserves the historical progression record.
- All user-facing strings use i18n keys (NL/FR/EN). Milestone descriptions
  are stored in three languages in the database.

Edge cases:
- A new milestone is added to the reference data (seed update). Existing
  observations for other milestones are not affected.
- A child regresses on a milestone (was 'achieved', now 'not_yet'). Record
  a new observation — do not edit the old one.
- A child's age moves into the next milestone band. The portfolio view must
  update automatically based on current age vs. age_from/to_months.

Out of scope:
- Custom curriculum frameworks (Montessori, Pikler) — Phase 4.
- Learning journal narrative (photos + observations in journal format) — Phase 2.
- MeMoQ quality self-evaluation (feature 017, separate).
```

---

### 017 — MeMoQ

```
Build the MeMoQ pedagogical quality self-evaluation instrument —
the mandatory Flemish quality framework for all licensed KDVs.

Background:
MeMoQ (Meten + Monitoren + Kwaliteit) is the official Flemish
self-evaluation tool developed by UGent, KU Leuven, and Opgroeien.
Zorginspectie (the care inspectorate) uses it during inspection visits.
Every KDV with an erkenning (operating licence) must complete it,
including private (vrije prijs) KDVs. It is NOT IKT-specific.

What to build:
- The MeMoQ instrument has 6 fixed dimensions:
    1. Child development and wellbeing
    2. Caregiver-child interactions
    3. Family involvement
    4. Diversity and inclusion
    5. Team quality
    6. Organisational policy
  Each dimension has dozens of items with a scored scale (e.g. 1–4 or
  yes/partially/no). The exact items and scale must match the official
  Opgroeien instrument — do not invent them.

- memoq_evaluations table:
    id, location_id, evaluator_id (director), tax_year INT,
    status TEXT ('draft'|'completed'), completed_at,
    created_at, updated_at

- memoq_responses table:
    id, evaluation_id, dimension_code TEXT, item_code TEXT,
    score INT (or TEXT for qualitative scales), notes TEXT,
    created_at

- Digital self-evaluation form in web admin: director completes the
  instrument dimension by dimension.
- Completed evaluations are stored per year and retrievable before an
  inspection visit.
- Progress tracking: which dimensions have been completed vs. pending.

Key constraints:
- The item structure (dimension codes, item codes, scale values) must match
  the official Opgroeien MeMoQ instrument exactly. This is a regulatory
  document — do not approximate.
- Evaluations are per location per year (a multi-location organisation
  completes one evaluation per location).
- All user-facing strings use i18n keys (NL/FR/EN).
- A completed evaluation cannot be edited — start a new one for the next year.

Edge cases:
- A director starts an evaluation, saves progress, and returns to complete
  it later. Draft state must persist.
- Opgroeien updates the MeMoQ instrument (new items, changed scale).
  The data model must support versioning of the instrument without
  invalidating historical responses.
- An inspection visit is announced. The director needs to quickly find
  last year's completed evaluation to print or share.

Out of scope:
- The scientific measurement instrument used by researchers (different tool).
- The monitoring instrument used by Zorginspectie themselves (they use their
  own system — we only support the KDV's self-evaluation side).
```

---

### 018 — Management Reporting

```
Build the director dashboard and management reports for operational
oversight of the KDV.

What to build:
- Occupancy dashboard (today + week ahead):
    Per group/section: how many children are present vs. capacity.
    Per location: total present vs. licensed capacity.
    Colour-coded (green = normal, amber = near capacity, red = over).
- BKR compliance overview:
    Live ratio per group (present children ÷ on-duty qualified staff).
    History of ratio breaches (moments when BKR was exceeded).
- Monthly attendance summary:
    Total present days, total absent days (justified/unjustified split),
    total closure days — per child, per group, per location.
    Exportable as CSV or PDF.
- Invoice status overview:
    Paid / outstanding / overdue invoices for the current month.
    Total revenue collected vs. total invoiced.
    List of overdue invoices with days overdue.

All reports are scoped to the director's tenant. Multi-location directors
see an aggregate view with the ability to filter by location.

Key constraints:
- Reports read from existing tables (attendance_records, invoices, staff_schedules,
  contracts). No separate reporting schema or data warehouse for Phase 2.
- Queries must be efficient — add appropriate indexes if not already present.
- All user-facing strings use i18n keys (NL/FR/EN).
- Export formats: PDF (QuestPDF) for formal reports, CSV for data exports.

Edge cases:
- A location has no children present on a given day (closure or holiday).
  The dashboard must show 0/capacity cleanly, not an error.
- A director views the dashboard at midnight during a shift transition.
  The "today" window must be unambiguous (calendar date, not rolling 24h).
- Historical attendance data is queried for a period spanning multiple
  contracts for the same child. The report must aggregate correctly.

Out of scope:
- Revenue forecasting (Phase 3).
- In/outflow child trend reports (Phase 3).
- Staff hour reports for payroll (Phase 2 — staff module).
- IKT subsidy tracking and 80% occupancy KPI (Phase 3).
- Opgroeien monthly XML report (Phase 3).
```

---

### 019 — IKT Compliance

```
Build the IKT (inkomenstarief) subsidy integration for Opgroeien —
the module that enables subsidised KDVs to use the platform.

This is a Phase 3 feature. Private KDVs are the Phase 1–2 target.
All data model foundations (kindcode, planned_duration_minutes,
dossiernummer, tarief_code columns) must already exist by the time
this feature is built.

Prerequisites before starting this feature:
- X.509 certificate obtained from Kind & Gezin.
  Contact: software-ontwikkeling@opgroeien.be — initiate at least 4 weeks
  before Phase 3 development starts.
  Certificate format: 4096-bit key, KBO in CN: CBE=<10digits>KG.
  Validity: 24 months. One certificate for the whole platform (not per KDV).

What to build:

1. IKT attest entry screen (web admin):
   When a parent brings their "attest inkomenstarief" (issued by Opgroeien
   via mijn.opgroeien.be), the director enters it into the platform:
     kindcode (9 chars: YYMMDD-NNN)
     tariefcode (11 chars: YYMMDD-NNN-NN)
     inkomenstarief (daily rate in €)
     geldig_van (validity start date)
     geldig_tot (validity end date — 30 September for attestations from 2026+)
   These fields are stored on or linked to the contract.

2. IKT rate estimator (director tool):
   A director can enter an approximate household income and see the estimated
   daily rate. Uses the formula-parameter model:
     Band 1 (income ≤ €54,125.08): rate = income × 0.000385
     Band 2 (income €54,125.09–€77,442.79): rate = income × 0.000380
     Above €77,442.79: rate += €0.60 per €3,700 increment
     Maximum: €35.89/day. Minimum (standard): €6.47/day.
     Low-income reduction: up to €0.25/day for income < €21,178.54.
   This is an estimator only — the real rate comes from the parent's attest.

3. IKT webservice SOAP integration (MatchKind operation):
   Query Opgroeien's webservice to look up a child's current IKT rate.
   WSDL: https://arws.kindengezin.be/kinderopvang-ikg-webservices/service/ikg.wsdl
   Protocol: SOAP with WS-Security X.509.
   Client: System.ServiceModel.Http + dotnet-svcutil generated client.
   MatchKind input: naam, voornaam, geboorteDatum, kindCode (or tariefCode).
   MatchKind response codes:
     1000 = matched successfully (use returned inkomenstarief)
     1001 = attest rejected
     1002 = kindCode found but name doesn't match
     1003 = no valid attest on requested date
     1004 = not found
     1005 = registered but not yet processed
   Rate validity: attest valid until 30 September (from 2026 onward — October indexing).

4. FO-SU-05 XML monthly attendance report:
   Generate the XML file for Opgroeien monthly attendance submission.
   Schema: FO-SU-05.xsd v2.3. Root element: <IKG>.
   Submit via email to ko.formulieren@kindengezin.be (MVP path).
   Required fields from settings: dossiernummer, naam_locatie, verantwoordelijke,
   bo flag (buitenschoolse opvang), flex flag.
   Per child: kindcode (or tariefCode), naam, voornaam, and attendance
   counts classified by duration:
     min5u: days with planned_duration_minutes < 300 (< 5h)
     min11u: days with planned_duration_minutes 300–659 (5–10h59)
     min11uFlex: days with planned_duration_minutes ≥ 660 (>11h, flex locations only)
   Plus justified and unjustified absence counts in the same duration buckets.
   UsedViewer field must contain "ChildCare vX.Y.Z" (software name + version).
   FormulierGecontroleerd must be "J" — if "N", Opgroeien silently ignores it.

5. Belcotax Fiche 281.86 electronic submission:
   Submit fiscal attestation data electronically to FOD Financiën.
   API: REST, https://server.minfin.be/external/api/bulks/v1 (live from 2026).
   Schema: urn:f:d:2025:86:958.
   Fields: debtor (KDV: naam + cbein/KBO + adres),
           beneficiary (parent: achternaam + voornamen + ssin + adres),
           child: (achternaam + voornamen + geboortedatum),
           certRef: Opgroeien as certification authority,
           field 2031 (certificeringstype = 1),
           field 4101 (array of up to 4 periods: begin, einde, aantal_dagen,
           betaald_bedrag, dagtarief optional).
   SSIN (nationaal registernummer) of the parent must be collected and stored
   securely for this feature. This is new — Phase 1–2 explicitly do not store SSIN.

Key constraints:
- kindkorting (sibling discount) age limit rule:
    Contracts started before 1 Jan 2026: sibling discount until age 12.
    Contracts started from 1 Jan 2026: sibling discount until age 30 months.
  enrollment_start_date (= contract start_date) determines which rule applies.
- All money in cents. Never floats.
- The X.509 certificate must be renewed ≥ 10 business days before expiry.
  Build a certificate expiry alert in the director dashboard.

Out of scope:
- AARON daily registration for vrije prijs / KOT children (Phase 4).
- Groeipakket attendance report (Phase 4).
- IKT-mix (simultaneous IKT + vrije prijs places) administration (Phase 4).
```

---

### 020 — Email Communications

```
Build templated email delivery on top of the existing notification
infrastructure — bulk emails to parents and an emailed version of the
child daily report.

Context: IEmailSender / EmailService (MailKit-based) already exists for
transactional auth emails (verify-email, reset-password), built as
inline C# raw string literals. This feature fulfils the "Email
notification fallback" items deferred by 011 (closure calendar) and 013
(parent communication), and adds two new director-facing capabilities.

What to build:
- Bulk parent email: director selects a location (and, optionally, a
  specific group/section within it — the group/section assignment already
  modelled on the child in feature 006) and sends a one-off email to every
  parent/contact of children currently enrolled there. One email per family
  household, not per child — a parent with two children at the same location
  must receive a single combined email, not two.
- Daily report email: the same aggregated daily summary shown in the parent
  app (feature 013, sourced from child_events in feature 009) can be sent
  as an email to a child's contacts, either on-demand (director/caregiver
  triggers it) or as an opt-in daily digest per contact.
- Closure day emails: closure day notifications (feature 011) and
  announcements (feature 013) gain an email channel alongside push/in-app,
  reusing the same bulk-send mechanism built here.
- A real email templating approach — the current pattern of raw C# string
  literals per method does not scale to this many templates. Decide the
  concrete templating mechanism (e.g. Scriban, a Razor Class Library, or
  embedded HTML files with placeholder substitution) at plan time; whichever
  is chosen must render NL/FR/EN per the recipient's locale preference
  (already tracked on the primary contact per feature 006).

Key constraints:
- Every email respects the tenant boundary — a bulk send can never reach a
  contact outside the director's organisation.
- Bulk sends must tolerate partial failure (a subset of addresses bounce
  or are invalid) — log and continue, never fail the whole batch for one
  bad address.
- All user-facing email copy uses i18n keys/templates per locale, consistent
  with every other feature (constitution non-negotiable #3).
- Respect the photos/media consent flags already on the contract (feature 007)
  if a daily report email includes photos.

Edge cases:
- A location or group/section has zero enrolled children at send time —
  no-op, not an error.
- A parent contact has no email on file — skip that contact, log it, and
  still send to the child's other contacts.
- A daily report is requested for a child with no events recorded yet that
  day — send an email that clearly says "no updates yet" rather than an
  empty-looking template.
- Rate limiting / provider throttling on large bulk sends (a big location
  could have 100+ families) — batch or queue rather than sending
  synchronously in the request.

Out of scope:
- SMS or WhatsApp channels.
- Open/click tracking or delivery analytics.
- Parent-side unsubscribe/preference centre (Phase 3, alongside the
  broader notification preferences work).
```

---

## Notes

- Each feature branch follows the Spec Kit cycle: `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`
- Before specifying any feature, run: `/speckit-specify @PROJECT-BRIEF.md @BACKLOG.md` then paste the prompt for that feature
- The auth layer (003) already exists as a walking skeleton — the spec should describe what to keep, what to replace, and what to add
- Contracts (007) must include the split-location day-overlap validator
- BKR ratio enforcement lives in attendance (010), not in contracts
- IKT compliance (019) requires an X.509 certificate — initiate procurement at least 4 weeks before Phase 3 starts
- Email communications (020) consolidates the "Email notification fallback (Phase 2)" items deferred by both 011 (closure calendar) and 013 (parent communication) — no need to build email delivery separately in either of those specs
- **Flag for 005 (Staff), 007 (Contracts), 012 (Caregiver Scheduling)**: feature 004 (Locations) deliberately does not build any "move/relocate a location" continuity — when a KDV physically relocates (old building closes, new one opens), 004 only offers a "duplicate location" convenience (clones location-level settings, no data carryover). Whoever builds 005/007/012 needs to design the actual staff/child reassignment UX for a location closing down, keeping in mind staff are NOT bound to a single location (a caregiver can already work different locations on different days per 012's scheduling model) — so "moving" a location is really a bulk reassignment of active contracts (007) and future schedule entries (012), not a 1:1 staff/child transfer.
