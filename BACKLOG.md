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
| 007a | `007a-web-admin-scaffold` | Next.js app cleanup, director auth (email/password + Google OAuth), nav shell, first real screen (staff list + PIN management) | 003, 005 | ✅ Done |
| 008 | `008-caregiver-app-scaffold` | Expo app structure, caregiver auth, API client, offline sync infrastructure | 003, 006 | ✅ Done |
| 008a | `008a-caregiver-kiosk-mode` | Room tablet kiosk mode, PIN per caregiver, session management | 008 | ✅ Done |
| 008b | `008b-configurable-caregiver-pin` | Per-location director-web toggle to make the caregiver PIN step optional at check-in/check-out, keeping the tap-to-identify roster flow (and BKR ratio / event attribution, which depend on it) intact | 008a | ✅ Done |
| 009 | `009-child-events` | Daily tracking (sleep, feeding, diaper, mood, weight, etc.) | 006, 008a | ✅ Done |
| 009a | `009a-child-events-custom-type` | Add a `custom` child event type (caregiver-defined label + free text) for anything the 11 fixed types don't cover; consider renaming `measurement` → `growth_check` for clarity (it's weight+height+head-circumference together, not just height) | 009 | ✅ Done |
| 010 | `010-attendance` | Daily attendance register, BKR ratio enforcement | 007, 008a | ✅ Done |
| 011 | `011-closure-calendar` | KDV holiday/closure schedule, parent notification | 004 | ✅ Done |
| 012 | `012-caregiver-scheduling` | Shift planning, multi-location day assignment | 005, 010 | ✅ Done |
| 012a | `012a-waiting-list` | Waiting list management — entries, priority ordering, status tracking, occupancy view | 004, 006 | ✅ Done |
| 009b | `009b-group-activities` | Group-level activity moments (garden, musician, drawing, ...) — caregiver adds description + optional photos; surfaced to parents in daily report and parent app | 009, 008a | ✅ Done |
| 013 | `013-parent-communication` | Messaging, daily reports to parents | 006, 009, 009b | ✅ Done |
| 013a | `013a-day-reservations` | Parent online requests (sick day, extra day, exchange day) + director approval queue | 007, 013 | ✅ Done |
| 009c | `009c-multi-child-events` | Caregiver selects multiple children before logging an event (nap, feeding round, diaper check) — one submission creates one record per selected child; reduces repetitive tapping | 009, 008a | ✅ Done |
| 013f | `013f-reservation-settings` | Per-location configurability of day reservations: enable/disable swap requests, absence requests; or set to informational-only (no approval queue, just a notification to director) | 013a | ✅ Done |
| 013b | `013b-incident-reports` | Digital incident/accident report form (legal requirement under Kwaliteitsbesluit) | 006, 010 | ✅ Done |
| 013c | `013c-vaccine-health-records` | Vaccination schedule tracking, health records, due-date alerts | 006 | ✅ Done |
| 006a | `006a-child-profile-ui` | Full child profile tab (web + mobile) — director create/edit for core details and medical contacts, adding a pediatrician (kinderarts) field distinct from the existing GP (huisarts) field; extends the `/children/[id]` screen 013c introduced | 006, 013c | ✅ Done |
| 013g | `013g-vaccine-catalog` | Shared, admin-maintained vaccine catalog (seeded from the Vlaamse basisvaccinatieschema) backing 013c's free-text vaccineName; adds attachment support on VaccineRecord so a director can attach a scan/photo of the child's vaccinatieboekje | 013c | ✅ Done |
| 013h | `013h-platform-admin-vaccine-catalog` | Platform-admin role + management UI for the shared vaccine catalog 013g introduced (create/rename/reorder/deactivate catalog entries) — the catalog is currently seeded/maintained only via direct data changes, no in-app surface | 013g | ✅ Done |
| 013d | `013d-meal-list` | Daily maaltijdenlijst for kitchen — who eats what, allergen flags, meal texture per child (mixed/pieces/solid), printable | 007, 009 | ✅ Done |
| 013e | `013e-monthly-menu` | Monthly menu management by director + parent view in parent app; per-child meal personalisation (texture, dietary: halal/kosher/vegan/allergen); parent change requests | 013d, 013 | ✅ Done |
| 013i | `013i-monthly-menu-csv-import` | Bulk-populate a month's menu via CSV upload (parse rows, map to days, validate, preview before save) as an alternative to 013e's manual day-grid entry — raised mid-implementation of 013e, deferred to keep that feature's MVP to the manual grid only | 013e | ✅ Done |
| 013j | `013j-monthly-menu-variants` | Optional alternative menu per dietary restriction (e.g. vegetarian/halal/allergen-free), separate from a location's base monthly menu, defaulting to the base menu when no variant is set — a real data-model change (variant dimension on MonthlyMenu, variant resolution on the parent read) raised mid-implementation of 013e, deferred to keep that feature's MVP to a single base menu | 013e | ✅ Done |
| 014 | `014-invoicing` | Monthly invoice generation (QuestPDF), payment tracking, sibling family bundling option | 007, 011 | ✅ Done |

### Phase 2 (after Phase 1 is stable)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 014a | `014a-invoice-payments-plus` | Follow-up on shipped 014: optional PSP payment link per invoice (Stripe/Mollie/POM — all support Bancontact; provider must be investigated & decided before implementation, design behind an abstraction), automatic payment reminders for overdue invoices, automatic payment receipt (betalingsbewijs) on paid — competitive parity with D-Care (POM) and Bitcare (Mollie) | 014, 013 | ✅ Done |
| 015 | `015-fiscal-attestations` | Annual tax certificates (QuestPDF) | 014 | ✅ Done |
| 016 | `016-developmental-milestones` | Child development tracking | 006 | ✅ Done |
| 018 | `018-management-reporting` | KPIs, occupancy, financial summaries | 010, 014 | ✅ Done |
| 020 | `020-email-communications` | Bulk parent emails by location/group (with attachment upload), auto daily-report emails with unsubscribe | 004, 006, 009, 011, 013 | ✅ Done |
| 030 | `030-family-siblings` | Multi-child family: link siblings under one parent account, family dashboard in parent app, sibling flag on child/contract records, impact on invoicing and day-reservations | 006, 007 | ✅ Done |
| 031 | `031-photo-lifecycle-governance` | Photo retention/archiving on child departure, GCS storage-class cost tiering, explicit staff/admin/director photo-action RBAC, parent original-download support | 006, 009b, 013b | ✅ Done |
| 021 | `021-qr-checkin` | QR contactless check-in — parent shows QR on phone, caregiver tablet scans, no staff tap needed at drop-off | 010 | ✅ Done |
| 022 | `022-id-verified-registration` | Streamlined child/parent registration form with director "ID/birth certificate seen" verification checkbox — replaces eID card reader approach | 006 | 🔲 Not started |
| 023 | `023-digital-enrollment` | Public online enrollment form + parent-initiated waiting list self-registration | 012a | 🔲 Not started |
| 024 | `024-esignature` | Digital contract e-signature; SEPA direct debit mandate embedded in signing flow | 007 | 🔲 Not started |
| 025 | `025-coda-payment-matching` | CODA/CODABOX bank statement import + automatic payment matching against open invoices | 014 | 🔲 Not started |
| 026 | `026-sepa-direct-debit` | SEPA direct debit XML generation for batch collection from parent bank accounts | 014, 024 | 🔲 Not started |
| 027 | `027-staff-app` | Staff mobile app (Expo, separate from caregiver group tablet) — personal assignment schedule (which group/room/day), leave requests, director on-the-fly rescheduling for sick cover, push notifications | 012 | 🔲 Not started |
| 028 | `028-staff-hr-dossier` | Staff personnel dossier (contracts, training, documents), clock in/out time registration, contract expiry reminders | 005, 012 | 🔲 Not started |
| 032 | `032-platform-admin-portal` | Dedicated platform-admin (super-admin) portal — send director/organisation invitations, manage shared platform data (e.g. vaccine catalog) | 001 | 🔲 Not started |
| 033 | `033-kinderopvangtoeslag-aaron` | Monthly attendance submission to Opgroeien for the kinderopvangtoeslag (Groeipakket) — REST/JSON webservice (AARON backend, bearer-token auth). Applies to vrije-prijs (free-price) KDVs = the Phase 1 target segment; replaces the director's manual entry in the free AARON web app | 010 | 🔲 Not started |
| 034 | `034-jaarregistraties-xml` | Annual Opgroeien jaarregistratie forms via XML export (all forms except the medewerkers form, which must stay PDF); import of Opgroeien's pre-filled per-organisation values (Excel) to minimise validation errors | 004, 005, 006, 010 | 🔲 Not started |
| 035 | `035-safety-risk-register` | Safety & risk-analysis register: the legally required risicoanalyse across 4 domains (injuries/accidents, crises/life-threatening situations, child disappearance, illness/contamination), (near-)incident logbook, digitised actielijst checklists, attest slaaphouding tracking on the child profile | 006, 013b | 🔲 Not started |
| 036 | `036-crisis-mandatory-reporting` | Crisis & grensoverschrijdend gedrag workflow: register the event, pre-fill Opgroeien's official meldingsformulier, track the melding to the klantenbeheerder; verontrusting (home-situation concern) signal registration with restricted access; 10-year retention | 006, 035 | 🔲 Not started |
| 037 | `037-attendance-register-compliance` | Legal compliance layer on the attendance register: parent confirmation of presences (electronic, configurable frequency), record-at-the-moment enforcement/audit, ≥12-month retention, Zorginspectie export | 010, 013 | 🔲 Not started |
| 038 | `038-data-retention` | Automated data-retention lifecycle per Opgroeien terms: 10y complaints/crisis records, 5y child/family and staff data (staff clock starts at end of employment), 3y uittreksel strafregister (destroy previous on receipt of new), ≥12m attendance register; destruction jobs with audit trail | 006, 010, 013b | 🔲 Not started |
| 039 | `039-tenant-onboarding-migration` | Productised KDV onboarding & supplier-switch migration: guided setup wizard, bulk import of children/parents/contracts/staff from CSV/Excel (as exported by D-Care/Bitcare/spreadsheets), dry-run validation + preview, shadow-run mode, and a full tenant data export (data portability / no lock-in) | 001, 006, 007 | 🔲 Not started |
| 040 | `040-occupancy-placement-planning` | Forward occupancy & placement planning: available places per day/week/month/year per group, BKR-aware "can this child be placed on these days?" simulation for enquiries, and doorstroom (age-based group-transition) planning to avoid occupancy gaps | 007, 010, 012a | 🔲 Not started |
| 041 | `041-bkr-2027-ruleset` | Date-versioned BKR rulesets: the lower Flemish kindratio becomes mandatory 1 Jan 2027 (transition period until 31 Dec 2026, early adoption allowed per location); includes new countable staffing profiles (AKT trainees, logistieke medewerkers under conditions) and an average-over-staffing-hours ratio report at location level (how Opgroeien assesses the linked subsidy) | 010, 012 | 🔲 Not started |
| 017 | `017-memoq` | MeMoQ self-evaluation companion — **unblocked 2026-07-19**: the official instrument documents (handleiding + six groepsopvang dimension forms + Zorginspectie monitoring note + pedagogisch raamwerk) are now committed under `docs/memoq/`, and the prompt block below was fully rewritten from them with product-owner decisions (structured-but-all-optional cycles, web full-cycle + tablet observation capture, participant-private-until-shared ratings, child rows detached from child records, versioned seed content). One open question remains in the block (content-licensing permission from Opgroeien) with a defined fallback, so it does not re-block the feature. | 004, 005, 008 | 🔲 Not started |

### Phase 3 (post-revenue)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 019 | `019-ikt-compliance` | IKT subsidy integration, Opgroeien API | All Phase 1 | 🔲 Not started |

### Phase 4 — Integrations (later)

| # | Branch | Feature | Depends on | Status |
|---|---|---|---|---|
| 029 | `029-accounting-export` | Accounting system export (Exact Online, Yuki, CSV) for bookkeepers | 014 | 🔲 Not started |

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

**Shipped 2026-07-08** — `specs/007a-web-admin-scaffold/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 48/48 tasks, 7 new backend tests + 5 new web
component/logic tests, 225/225 backend + 18/18 web passing). Scope deltas worth knowing before
starting a dependent feature:

- **Two small, additive backend endpoints were needed and added**, since this feature's UI
  surfaced gaps 008a and earlier auth work never had a reason to close: `GET /api/devices`
  (008a built pairing/revocation but no list) and `GET /api/organisations/me` plus a new
  `Name` field on `AuthenticatedUser` (no endpoint anywhere returned the tenant's display name
  or a user's name, both needed for the sidebar shell). Both are pure reads, no new tables, no
  new authorization rules — see spec.md FR-013a/FR-005a for the full reasoning.
- **Two genuine pre-existing bugs were found and fixed while wiring login**, not caused by this
  feature but silently broken since feature 003 shipped: the `web/app/api/refresh` BFF route
  and the Google sign-in flow never sent `organisationSlug`, which 003's `RefreshTokenCommand`/
  `GoogleSignInCommand` have required since that feature landed. Fixed by threading the
  organisation slug through a new `org_slug` cookie alongside the refresh-token cookie.
  **Flag for any future web feature touching auth**: this cookie pair is now the source of
  truth for "which organisation is this browser session for."
- **`web/lib/generated/api-types.ts` (openapi-typescript output) is committed, not
  gitignored** — mirrors `mobile/services/generated/api-types.ts`'s existing precedent, since
  CI never runs a live backend to regenerate it. Any future web feature adding/changing
  backend endpoints must regenerate this file locally (`npm run generate-api-client` against a
  running backend) and commit the diff — it will not happen automatically.
- **`web/theme/colors.ts` is a hand-synced TypeScript duplicate of `mobile/theme/colors.js`**,
  not a shared import (research.md R6) — no monorepo/workspace tooling exists yet to share it
  properly. A future feature introducing a third color-token consumer should treat that as the
  trigger to finally set up a shared package, rather than adding a fourth hardcoded copy.
- **First component-level frontend tests in this repo's `web/` app** — added `jsdom` +
  `@testing-library/react` + `@testing-library/user-event` + `@vitejs/plugin-react` as new
  devDependencies (`vitest.config.ts` environment changed from `node` to `jsdom`). Every
  prior `web/` test was pure `lib/` logic; testing table rendering, search filtering, and
  dialog confirmation flows needed real DOM rendering. Future web features should follow this
  same pattern rather than reverting to logic-only tests.
- **No dropdown-menu component was actually built**, despite plan.md naming it as one of the
  shadcn primitives to add — row actions (PIN reset, deactivate/reactivate, revoke) are inline
  buttons instead, per reference-products.md's explicit "avoid hidden actions" guidance found
  applicable during implementation. Not a gap; a corrected implementation-level decision.
- Placeholder sidebar nav entries (Locations, Contracts, Children) are inert (non-navigating)
  list items, but each does have a real, working route (`/locations`, `/contracts`,
  `/children`) rendering a shared `NotYetAvailable` component — so a director typing the URL
  directly never hits a broken route or raw 404. Whichever feature builds one of these next
  should replace that placeholder page's contents directly rather than routing around it.

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

### 008b — Configurable Caregiver PIN

**Added 2026-07-13, from a director asking whether PIN entry could be optional** — some KDVs'
established way of working doesn't fit a per-caregiver PIN step. Investigated what PIN actually
protects in 008a's design before proposing this: `RoomShift.StaffProfileId` (which caregiver
tapped their card and checked in) is what feature 009/010/013b's `IShiftAttributionService`
uses to resolve `recorded_by`/`administered_by`, and it is *also* the sole data source for
`GetBkrRatioQuery`'s legally-required live begeleider-kind-ratio (`qualifiedStaffCount` comes
straight from open `RoomShifts`). Removing check-in itself would silently zero out BKR and leave
every event/incident unattributed — not acceptable. PIN entry (the *verification* step, on top
of the tap) is separable from that: `CheckInCommand`/`CheckOutCommand` already gate on
`VerifyPinCommand.VerifyAsync` before writing the `RoomShift` row, so skipping just that call
when a location opts out preserves BKR and attribution exactly as today — self-asserted from the
tapped card instead of PIN-confirmed.

```
Add a per-location director-web setting to make the caregiver PIN step
optional at check-in/check-out, for KDVs whose way of working doesn't fit a
per-caregiver PIN entry — while keeping the underlying tap-to-identify
check-in flow intact, since BKR ratio (010) and event/incident attribution
(009/010/013b) both depend on RoomShifts existing, not on the PIN itself.

Context: 008a's kiosk flow is select-then-PIN — a caregiver taps their own
photo card (identifying themselves), then confirms with a PIN
(CheckInCommand → VerifyPinCommand.VerifyAsync → writes RoomShift). Every
downstream legal/audit feature (GetBkrRatioQuery's qualifiedStaffCount,
IShiftAttributionService's recorded_by/administered_by resolution used by
009/010/013b) reads only from RoomShifts.CheckedOutAt/StaffProfileId — none
of it reads or requires that PIN verification happened. This means the tap
(identity claim) and the PIN (proof of that claim) are separable: a location
can skip the proof step without breaking BKR or attribution, at the cost of
self-asserted rather than PIN-verified identity per check-in.

What to build:
- New `Location.RequiresCaregiverPin` (bool, default true) — follows 013f's
  precedent (per-location settings on the `Location` entity, surfaced on
  the existing `/locations/[id]` settings screen 013f already built; add a
  new tab or extend "Algemeen" — decide at plan time which fits better
  next to the existing Reserveringsinstellingen tab).
- `CheckInCommand`/`CheckOutCommand`: when `RequiresCaregiverPin` is false
  for the location, skip the `VerifyPinCommand.VerifyAsync` call entirely
  and write the `RoomShift` row directly off the tapped `StaffId` — confirm
  at plan time whether the caregiver-tablet client omits the PIN
  field/keypad step entirely when it knows the setting is off (the better
  UX — no keypad shown at all) rather than showing a keypad that accepts
  anything.
- Kiosk tablet UI (mobile `app/(room)/`): when the location's roster
  response indicates PIN is not required, tapping a caregiver's card
  completes check-in/out immediately — no PIN keypad overlay appears.
  Fetch this per-location flag alongside the existing roster call
  (GET /api/room-shifts/roster) rather than a separate request.
- `confirmAdministrator` (sensitive-action confirmation, 008a User Story 5):
  decide at plan time whether this also becomes tap-only when
  `RequiresCaregiverPin` is false, or whether it always requires a PIN
  regardless of the location setting since it exists specifically as an
  extra verification step for medical/sensitive actions, not just routine
  presence — a real product decision, flag for /speckit-clarify.
- Web: a labelled toggle on the location settings screen, plus explanatory
  copy naming the tradeoff (self-asserted identity vs PIN-verified) so a
  director makes an informed choice, not just flips a switch.

Key constraints:
- BKR ratio (010) and event/incident recorded_by/administered_by resolution
  (009/010/013b) must behave identically whether or not PIN is required —
  both already read only from RoomShifts, so no changes needed there as
  long as check-in still writes a RoomShift row either way.
- Existing PINs (StaffProfile's PinHash) are NOT deleted or invalidated when
  a location disables the requirement — re-enabling it later must not force
  every caregiver to re-set a PIN.
- All user-facing strings (toggle label, tradeoff copy) use i18n keys
  (NL/FR/EN).

Edge cases:
- A location has `RequiresCaregiverPin = false`, then a director re-enables
  it mid-day while caregivers are already checked in. Existing open
  RoomShifts are unaffected (they were validly created); the PIN keypad
  reappears only for the next check-in/check-out action.
- Two caregivers share visually similar names/photos and PIN is off — a
  caregiver taps the wrong card by mistake. There is no PIN to catch the
  error; the only correction path is the caregiver noticing and checking
  themselves out / the right person in. Call this out in the tradeoff copy,
  since it's the concrete risk of turning PIN off, not just an abstraction.
- Offline behavior (008a's existing skip-PIN-when-offline path for
  administrator confirmation) must remain distinct from this
  always-skip-PIN setting — don't collapse the two code paths in a way
  that makes it unclear later which one fired.

Out of scope:
- Removing the tap-to-identify step itself (anonymous/no-selection
  check-in) — always keeps a per-caregiver identity claim, only PIN
  verification of that claim becomes optional.
- Any change to how PINs are set/reset/rate-limited when the requirement is
  on — this feature only adds the off switch, not a new PIN-lifecycle
  mechanism.
```

**Shipped 2026-07-13** — `specs/008b-configurable-caregiver-pin/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 39/39 tasks, 600/600 backend + 102/102 web + 156/156
mobile passing). New `Location.RequiresCaregiverPin` (default `true`), a director-web "Inchecken"
tab with tradeoff copy, and a mobile kiosk that skips the PIN keypad entirely when a location's
roster reports the setting off. Resolved via `/speckit-clarify` (no user stop needed — a clear
recommended default existed): `confirmAdministrator` always requires PIN verification regardless
of this setting, since it's a distinct, higher-bar confirmation for medical/sensitive actions with
its own existing "Skip" escape hatch — this feature's setting governs routine check-in/check-out
only. The trickiest implementation bug, caught by a test before merge: `CheckInCommand`/
`CheckOutCommand` must never let a `null` PIN mean two different things — "server decided
verification doesn't apply" (safe) vs. "client happened to omit a PIN while the location still
requires one" (must fail) — conflating them via `VerifyPinCommand.VerifyAsync`'s new nullable-PIN
skip path would have let any PIN-required location be bypassed by simply sending no PIN; fixed by
coercing a null/empty client PIN to empty string before the call, reserving actual `null` for the
server's own location-based decision. `/speckit-analyze` caught two stale test-file-path
assumptions in tasks.md (a nonexistent `ChildCare.Application.Tests` project and a nonexistent
`web/__tests__/components/` subdirectory — this repo's real conventions are `ChildCare.Api.Tests`
flat integration tests and `web/__tests__/*.test.tsx` flat files) and one inaccurate premise (FR-016
assumed an existing audit-log mechanism that doesn't exist in this codebase — corrected to a plain
`ILogger` entry). `/speckit-converge` found one genuine edge-case gap after implementation (no test
proved PIN-off check-in works for a caregiver who never had a PIN set at all, only ones who did) —
fixed, not deferred, same standing rule as every prior feature. Also extended the
`TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern once more
(012a, 013c, 013d, 006a) for this feature's own migration.

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

**Shipped 2026-07-09** — `specs/009-child-events/` (spec → clarify → plan → tasks → checklist →
analyze → implement → converge, 53/53 tasks incl. an 8-task checklist-driven mid-implementation
correction and a 3-task convergence pass, 33 new backend tests + 12 new mobile tests, 258/258
backend + 88/88 mobile passing). Scope deltas worth knowing before starting a dependent feature:

- **FR-006's edit authorization was redesigned mid-implementation**: the spec and its own
  clarification session originally called for checking a caregiver's `StaffLocationEligibility`
  row, but that's not implementable — routine tablet actions are device-token authenticated only
  (constitution's Technology Stack Constraints), so there is no individual caregiver identity on
  the request to check eligibility against. Corrected to comparing the requesting *device's own*
  `LocationId` claim against the event's `LocationId` instead (research.md R4) — same-day
  corrections are scoped to "any caregiver at that room's tablet," not a per-staff-member check.
  **Flag for any future feature considering a per-caregiver authorization rule on a
  device-token-authenticated route**: it isn't available; only device/location identity is.
- **A new `DeviceOrDirector` authorization policy was added** (`Program.cs`) — no existing
  endpoint in this codebase previously accepted both a device token and a director JWT on the
  same route (an earlier BACKLOG draft claimed `RoomShiftEndpoints`'s correction route did this;
  it doesn't, it's `DirectorOnly` only). `PATCH`/`DELETE /api/child-events/{id}` are the first
  routes using this pattern — reuse it rather than inventing a third dual-auth mechanism.
- **`ChildEventType`'s multi-word wire values need explicit mapping**: `feeding_bottle`/
  `feeding_solid` don't round-trip through the `.ToString().ToLowerInvariant()` convention every
  other enum in this codebase uses (e.g. `ContractStatus`), since that produces `feedingbottle`
  with no underscore. `ChildEventTypeExtensions.ToWireString()`/`TryParseWireString()` handles
  this — any future multi-word enum with a snake_case wire format should follow the same pattern
  rather than the default convention.
- **`Contact` gains a nullable `PushToken` column** (feature 009, not feature 006) — added so the
  temperature-alert recipient query has something real to filter on. No client populates it yet;
  the parent-app feature (013) is expected to add the registration path, not a new column.
- **`lucide-react-native` + `react-native-svg` finally added** per design-system.md's standing
  instruction ("add it as part of whichever feature next touches iconography") — features 008/
  008a didn't. The remaining emoji placeholders on the caregiver group view (allergy/fever icons)
  were replaced as part of this same change since leaving a mixed emoji/lucide app was exactly
  the inconsistency design-system.md warns about.
- **`/speckit-converge` found two data-model constraints nothing enforced**: `EndedAt` (spec:
  "Sleep only") and `AdministeredBy` (spec: "Medication/temperature only") were both accepted
  silently for any event type by `RecordChildEventCommand`/`UpdateChildEventCommand` — fixed
  with explicit validation rather than left as documented-but-unenforced.
- Photo attachment (in the original backlog prompt below) was descoped during `/speckit-clarify`
  — no dedicated user story existed for it, and building it without one risked scope creep. It's
  not tracked as a separate backlog item since no concrete requirement was ever specified beyond
  "photos can be attached" — a future feature should treat this as unscoped, not deferred.
- Reachable only via raw API, no UI in this app: a director's any-day event correction
  (`PATCH`/`DELETE` via `DeviceOrDirector` + `DirectorOnly` role) and filling in a skipped
  `AdministeredBy` retroactively. Whichever feature builds director-facing web screens for
  `child_events` next should wire these rather than building new backend capability for them.

---

### 009a — Child Events: Custom Type

**Added 2026-07-09, during a live review of feature 009's implementation** (which shipped the
11 fixed event types listed above, still `🔲 Not started` at the table row above until this
follow-up lands — 009 itself was implemented and tested in the same session this entry was
added). Two things came up that were deliberately deferred rather than reopening 009 mid-flight:

```
Add a `custom` child event type — a caregiver-defined label plus free text — for anything the
11 fixed event types (sleep, temperature, medication, feeding_bottle, feeding_solid, diaper,
mood, activity, note, weight, measurement) don't cover.

Context: feature 009 was asked whether EventType should be "an enum instead of a string" — it
already is, end-to-end (ChildCare.Domain.Enums.ChildEventType on the backend, a closed TS union
in mobile/types/index.ts), so no change needed there. Separately, whether a distinct `custom`
type should exist alongside the closed 11: `note` (free text) already serves as the catch-all
today, so this needs a real design decision, not just "add an enum value" — what does `custom`
provide that `note` doesn't? Candidates: a caregiver-supplied label/title distinct from the body
text (so the timeline can show something more specific than "Note"), or a structured key/value
bag for a one-off measurement the fixed types don't anticipate. Needs a clarify pass to settle
this before planning, not an assumption.

Also evaluate renaming `measurement` → `growth_check` (or similar) — it bundles weightKg/
heightCm/headCm together (a pediatric growth check), not just height; the current name reads as
narrower than what it actually captures. This is a rename touching a shipped enum value (backend
enum + DB column values via ChildEventTypeExtensions' wire-string mapping, mobile TS union,
i18n keys, and existing test fixtures) — treat it as a deliberate migration-safe rename
(add-new-alongside-old, backfill, remove-old), not a find-and-replace, since feature 009's
`child_events` table will have live data under the old wire value by the time this runs.

Depends on 009 (extends its ChildEventType enum, validator, and quick-action UI directly).
```

**Shipped 2026-07-09** — `specs/009a-child-events-custom-type/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 26/26 tasks, 21 new backend tests + 9 new mobile
tests, 267/267 backend + 96/96 mobile passing). Two design decisions were resolved with the
product owner before specifying, since the backlog prompt above explicitly flagged them as
needing a real decision rather than an assumption: `custom`'s payload is `{ label, text? }`
(a caregiver-supplied headline distinct from `note`'s body-only shape, plain free text with no
autocomplete), and the `measurement` → `growth_check` rename was bundled into this feature rather
than split out, since both already touch `ChildEventType` end-to-end. Scope deltas worth knowing:

- **The rename's data backfill ships as a new `backfill-growth-check` CLI command**, mirroring
  feature 002's `migrate-tenants` tenant-loop pattern exactly, run as a raw per-tenant SQL
  `UPDATE` rather than an EF Core migration (no schema/column change, just a value rewrite). This
  command **must run against every tenant schema before deploying the build** that removes
  `"measurement"` recognition from `ChildEventTypeExtensions` — a hard cutover with no dual-write
  window (an un-migrated row would otherwise throw when the value converter reads it back).
- **A real bug was caught by the new tests before merge**: the first draft of the backfill SQL
  used lowercase `event_type`/`id` column names, but this codebase's Postgres columns are
  PascalCase (`"EventType"`, `"Id"`) since no snake_case naming convention is configured anywhere
  in `TenantDbContext` — fixed in both the CLI command and its test once the tests failed against
  a real database (constitution Principle V doing exactly what it's for).
- **A `fireEvent.changeText` + `act()` quirk was found while writing mobile tests**: in this
  repo's `@testing-library/react-native` v14 / React 19 setup, `changeText` (unlike `press`)
  needs an explicit `await act(async () => ...)` wrap or the state update never flushes before
  the next assertion — worth remembering for any future mobile test that types into a `TextInput`
  and immediately asserts on the result.
- `/speckit-checklist` and `/speckit-analyze` each surfaced small gaps (a missing offline-sync
  test for `custom`, a missing `EditEventModal` test, an orphaned `note.text` i18n key left behind
  by a field-label refactor) — all fixed during implementation/convergence, not deferred.
- The constitution's `child_events` event-type list (Development Workflow section) was updated
  from `measurement` to `growth_check`/`custom` as a PATCH-level (1.2.0 → 1.2.1) documentation-
  accuracy fix — no principle changed, only the descriptive example list.

---

### 009b — Group Activities

```
Let caregivers record group-level activity moments — garden time, a visiting
musician, a drawing session, a walk, a birthday celebration — with a
description and optional photos. Parents see these in the parent app and
in the daily report, giving them a window into their child's day beyond
just individual events (diaper, feeding, sleep).

Context: child_events (009) are per-child (a diaper change, a temperature
reading). Group activities are different: one moment that all or most
children in the group shared. Modelling them as child_events would mean
duplicating the same record for every child in the group — wrong approach.
Group activities are their own entity.

What to build:
- group_activities table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    group_id        UUID REFERENCES groups(id) NOT NULL,
    location_id     UUID REFERENCES locations(id) NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL,
    title           TEXT NOT NULL,        -- short label: "In de tuin", "Muzikant"
    description     TEXT,                 -- optional free text
    activity_type   TEXT CHECK (activity_type IN (
                      'outdoor',          -- garden, walk, playground
                      'creative',         -- drawing, painting, crafts
                      'music',            -- singing, instruments, visitor
                      'story',            -- reading, storytelling
                      'celebration',      -- birthday, seasonal event
                      'other'
                    )) DEFAULT 'other',
    recorded_by     UUID REFERENCES users(id) NOT NULL,
    created_at      TIMESTAMPTZ DEFAULT NOW()

- group_activity_photos table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    activity_id     UUID REFERENCES group_activities(id) ON DELETE CASCADE,
    gcs_url         TEXT NOT NULL,        -- signed GCS URL
    thumbnail_url   TEXT,                 -- smaller version for list views
    caption         TEXT,
    uploaded_at     TIMESTAMPTZ DEFAULT NOW()

- Caregiver app:
  - "Activiteit toevoegen" button on the group home screen (alongside
    individual child event logging).
  - Form: activity type picker (icon-based), title (pre-filled from type
    but editable), optional description, photo upload (camera or gallery,
    up to 10 photos per activity).
  - After saving, the activity appears in a group timeline alongside the
    individual child events in the caregiver's view.

- Parent app:
  - Group activities appear in the daily report feed for that day. Parents
    whose children are in that group see the activity, photos, and description.
  - Photo display: parents only see photos of children they have consent for.
    The photo consent flag (photo_consent_types on contracts, from 007) governs
    this: if a parent has not given photo consent, they still see the activity
    text and description but not the photos.
  - A "Galerij" tab shows all group activity photos across the month for
    their child's group — a parent-facing photo album.

- Director web admin:
  - Activities are visible in the group timeline view alongside individual events.
  - Director can delete an activity (e.g. inappropriate photo uploaded by mistake).

Key constraints:
- Photos are stored in GCS with signed URLs; never public blob URLs.
- Photo consent is respected at render time in the parent app, not at upload
  time. The caregiver uploads freely; the API filters by consent when serving
  photos to parents.
- A child who was absent on a given day should not appear in group activity
  photos, but we cannot enforce this technically (the caregiver took the photo).
  Add a note in the caregiver UI: "Foto's mogen enkel aanwezige kinderen tonen."
- All user-facing strings use i18n keys (NL/FR/EN).
- Maximum 10 photos per activity, each max 10MB before server-side resize.
  Resize to max 1920px on the long edge; generate a 400px thumbnail.

Edge cases:
- A parent has partial photo consent (e.g. 'internal' but not 'external').
  Show photos in the parent app (internal use) but do not include them in
  any external sharing or bulk export.
- Caregiver records an activity for the wrong group (fat-finger). Director
  can delete it; caregiver re-creates on the correct group.
- Activity is recorded offline (no connectivity). Text + metadata are queued
  in offline_queue (008). Photos are queued separately — upload resumes on
  reconnect. Show a "Foto's worden geüpload..." indicator.

Out of scope:
- Director-initiated activity templates ("Every Friday we go to the garden"
  recurring activities) — Phase 2.
- Video upload (photo only for MVP; video storage costs are high).
- Parent commenting on activities — Phase 2.
```

**Shipped 2026-07-11** — `specs/009b-group-activities/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 71/71 tasks, PR #19 squash-merged after green CI —
458/458 backend + 114/114 mobile + 47/47 parent-mobile + 53/53 web tests passing). Two new tenant
tables (`group_activities`/`group_activity_photos`), a device-authenticated create/photo-upload/
timeline surface mirroring `ChildEventEndpoints.cs`, a merged group-timeline query, a
consent-filtered parent daily-summary extension + monthly gallery, and a director-only delete.
This run resumed a prior session's backend work that was implemented but never committed and had
zero test coverage exercised against a live process — picked up from `tasks.md` rather than
re-running specify/plan/tasks, and found three real bugs only visible once the backend was
actually run rather than just read:

- The photo-upload endpoint 500'd on every call: minimal APIs require antiforgery middleware for
  any `IFormFile`-bound route, and this device-token-only API (no cookie/browser session to
  protect) never registers one — fixed with `.DisableAntiforgery()` on that one endpoint. Worth
  remembering for any future minimal-API file-upload endpoint in this codebase.
- No `FakeGroupActivityPhotoStorage` test double existed, so every photo-upload test was silently
  hitting real GCS with no local credentials and 500ing — added it (mirrors
  `FakeProfilePhotoStorage`'s existing role) and registered it in
  `OrganisationOnboardingWebAppFactory`.
- `TenantMigrationRolloutTests`' schema-revert helper dropped the two new tables but never added
  `AddGroupActivities` to its `__EFMigrationsHistory` cleanup — the same class of gap every
  migration-adding feature since 003 has hit (see 012a's shipped-note); fixed, and the standing
  "extend this test's revert helper" reminder applies to the next migration-adding feature too.
- A stray always-failing debug test (`DebugPhotoTest.cs`) from that earlier debugging session was
  found and deleted.

`quickstart.md` itself had a real bug (its own Scenario 1 example omitted the required
`occurredAt` field) — found by running the scenarios against a live local backend and fixed in
the doc. Implemented from scratch in this pass: all mobile/parent-mobile/web UI and NL/EN/FR
i18n, plus mobile's `photoUploadQueue.ts` — a dedicated local queue distinct from the existing
offline-write queue, since a photo upload is `multipart/form-data` and can't ride the JSON-body
`syncEngine.ts` replay path. `/speckit-converge` found no gaps against spec/plan/tasks.

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

**Shipped 2026-07-09** — `specs/010-attendance/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 57/57 tasks, PR #14 squash-merged after green CI —
309/309 backend + 23/23 web checks; local validation also covered 105/105 mobile tests and web
typecheck). Scope deltas worth knowing before starting feature 011/012/014:

- **Attendance now has the end-to-end operational spine**: tenant-schema `attendance_records`
  (`present`/`absent`/`closure`), device-token caregiver check-in/check-out, director/caregiver
  corrections under the same-day/location edit-window rule, director history/correction web UI,
  absence registration with justified/unjustified classification, and a caregiver-tablet BKR
  indicator sourced from present children plus feature 008a's room-shift roster.
- **The prompt's single `recorded_by UUID` became a `uuid[]` array**, following feature 009's
  already-shipped precedent: device-token tablet writes identify the room tablet, not one exact
  caregiver, so attribution stores the checked-in caregiver set at the time of the action rather
  than inventing false precision.
- **Closure and exchange/extra-day dependencies are extension points, not placeholder workflows**:
  010 ships the `closure` status and blocks manual check-in against an existing closure record,
  but bulk closure generation remains feature 011. Parent exchange/extra-day request UI remains
  feature 013; 010 already supports manual check-in on a non-contracted weekday by storing
  `planned_duration_minutes = null`.
- **BKR is warning-only and uses the data model that exists today**: solo max 8, 2+ qualified
  caregivers max 9 each, nap-time max 14 inferred from open sleep events, students/volunteers
  excluded. The backlog's leefgroep 18-cap stays out of scope because no group/location type flag
  exists yet; this is documented in the spec/plan and constitution carve-out rather than guessed.
- **Two finish-pass issues were fixed before merge**: the director web correction dialog initially
  only edited check-in/check-out times and kept stale form state between records; it now supports
  status/absence corrections with i18n coverage and a regression test. CI also exposed an overly
  strict exact `DateTime` equality assertion after a PostgreSQL round-trip (`.4072354Z` vs
  `.4072350Z`); the test now asserts sub-millisecond stability while still catching a real
  overwrite.

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

**Shipped 2026-07-09** — `specs/011-closure-calendar/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 65/65 tasks, PR #15 squash-merged after green CI).
Scope deltas worth knowing before starting feature 012/014/020:

- **Closure days now have a draft/publish/cancel lifecycle**: directors create per-location
  closure days in the web admin, publish them to make them operational, and cancellation preserves
  audit history for published closures while draft removals stay quiet.
- **Attendance integration is real, not a placeholder**: publishing a closure creates `closure`
  attendance records for actively contracted children, blocks manual check-in, requires explicit
  confirmation before replacing same-day checked-in records, and restores preserved attendance on
  cancellation where appropriate.
- **Parent notification is closure-specific until feature 020**: publish/cancel writes parent
  closure messages and per-recipient push delivery records, with partial push failures retained for
  audit/retry visibility. Email fallback remains feature 020.
- **Invoicing has a stable reader contract**: feature 014 should use the shipped billable-exclusion
  closure reader/API rather than re-querying closure tables ad hoc.

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

**Shipped 2026-07-10** — `specs/012-caregiver-scheduling/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 58/58 tasks, 32 new backend tests + 5 new web
component tests, 359/359 backend + 34/34 web passing). Scope deltas worth knowing before
starting a dependent feature:

- **BACKLOG's own premise for this feature was wrong, and the plan-phase research caught
  it before implementation**: the prompt above says "the live BKR count in feature 010 uses
  staff_schedules to know who is on duty right now" — but `staff_schedules` didn't exist
  until this feature, and reading feature 010's actual shipped `GetBkrRatioQuery` shows it's
  sourced from `RoomShifts` (feature 008a's real-time PIN check-in/out log), not any
  schedule — which is, if anything, the more correct signal (a scheduled-but-absent
  caregiver was already excluded, since they never check in). This feature does **not** wire
  `staff_schedules` into feature 010's live computation. Instead it ships a separate,
  planning-only "projected on-duty count" for the rota builder itself, with a dedicated
  regression test (`BkrDecouplingTests`) proving the two stay decoupled. **Flag for any
  future feature touching BKR or staff scheduling**: these are two intentionally separate
  concerns — planned (rota) vs. actual (room shift) — don't merge them.
- **Caregiver-facing schedule UI is deliberately out of scope** — resolved via an explicit
  scope decision before specification (not a default guess): feature 008a's shared kiosk
  tablet has no personal session to host a personal "my schedule" view, and feature 027
  (Staff App) is already scoped in this backlog for exactly that. This feature ships the
  data model, the director-web rota builder, and a personal-account-scoped
  `GET /api/staff-schedules/me` read endpoint so feature 027 can consume it directly without
  building the read path itself.
- **`/speckit-converge` found a real integrity gap the original spec missed**: nothing
  stopped a director from planning a shift for a staff member at a location they have no
  `StaffLocationEligibility` row for, even though `VerifyPinCommand`/`CheckInCommand`
  (feature 008a) already reject exactly that case at actual check-in time. Fixed by adding
  the same eligibility check to `CreateStaffScheduleCommand`/`UpdateStaffScheduleCommand`
  (`403 errors.staff_schedules.not_eligible`), and by filtering the web grid's staff rows to
  eligible-for-this-location staff using `StaffResponse.eligibleLocationIds` (feature 005),
  already returned — no new endpoint needed.
- **A live browser verification pass (not just mocked component tests) caught a real
  date-handling bug** before it shipped: the week grid's date math used
  `Date.toISOString().slice(0,10)`, which round-trips through UTC and shifts every date
  backward by one day in any positive-UTC-offset local timezone — the grid was showing
  Sunday as the first column of a Monday-start week. Fixed by building the date string from
  local `Date` component getters instead. Worth remembering generally: mocked API tests
  don't exercise real `Date`/timezone conversion against a real day boundary — a feature
  whose correctness depends on date math benefits from at least one real end-to-end run.
- Overlap rejection (FR-003) covers same-location double-booking as well as cross-location —
  broadened from BACKLOG's original "two different locations" wording during the
  requirements-quality checklist pass, since a staff member can't be in two groups at the
  same location simultaneously either.
- Rota-copy (FR-016) rejects a target week that isn't strictly after the source week, or
  that's already (fully or partially) passed — copy is a forward-planning operation only,
  consistent with FR-004's past-date immutability.

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

**Shipped 2026-07-10** — `specs/013-parent-communication/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 109/109 tasks incl. a 3-task convergence pass,
437/437 backend + 49/49 web + 41/41 parent-mobile tests passing). Director-invited parent
accounts, shared per-child family message threads, director announcements, a generic
notification centre, Expo push, and the parent daily summary — plus the first real parent
mobile app (`parent-mobile/`, portrait, its own bundle id, no offline/device-token machinery
since this app has no offline requirement unlike `mobile/`). This session resumed mid-flight:
backend and web admin (`/messages`, `/announcements`) had already landed in an earlier
session with several tasks left uncommitted and `parent-mobile/` never scaffolded at all —
picked up from `tasks.md`'s own checkboxes rather than re-running specify/plan/tasks. Two
real bugs were fixed in the already-written web UI before continuing: the announcement
compose form's labels weren't associated with their inputs, and the invite dialog rendered
"Invitation sent." in two places at once (ambiguous to both accessibility tooling and its own
test). `/speckit-converge` found a real, unrequested-by-tasks.md gap in the already-shipped
US0 backend: `GoogleSignInCommandHandler`/`AppleSignInCommandHandler` authenticated a
pre-invited `Parent`-role user by email match but never linked `Contact.TenantUserId` or ran
the FR-006a thread backfill (only the password accept-flow did) — a parent signing in via
Google/Apple before ever completing that flow got a working token but every `ParentOnly`
endpoint 403'd forever. Fixed by extracting a shared `ParentAccountLinker`, plus a matching
`Apple:BundleId` misconfiguration (defaulted to the caregiver app's bundle id, even though
Apple Sign-In is parent-only). Worth remembering generally: when a role can authenticate two
different ways (password vs. OAuth), any account-linking side effect the "primary" flow
performs needs to be verified against every other path that can also produce a first-time
authenticated session for that role — a passing test suite for one path proves nothing about
the other. The parent-mobile build itself was delegated to a background agent given its
sheer size (a second full Expo app); its output was independently re-verified rather than
taken on faith — this caught a handful of design-system spacing-scale violations (`py-5`,
`pb-10`, etc., all fixed) that had been faithfully mirrored from `mobile/`'s own pre-existing,
already-shipped instances of the same values (left untouched there, out of scope).

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
- In-app payment (moved to feature 014a — payment links, reminders, receipts).
- IKT subsidy lines and inkomenstarief billing (Phase 3).
```

---

### 014a — Invoice Payments Plus

```
Follow-up on the shipped 014-invoicing: online payment links, automatic
payment reminders and payment receipts. 014 itself is DONE — this feature
only ADDS endpoints/jobs/screens on top of the existing invoices table
and lifecycle; do not change 014's billable-day rules or PDF.

Competitive context (verified 2026-07-15): D-Care offers in-app invoice
payment via POM; Bitcare via Mollie, plus automatic reminders and
betalingsbewijzen. Bank transfer with OGM stays our default rail.

What to build:
1. Optional payment link per invoice: the link opens the PSP's hosted
   payment page from the invoice email / parent app; a webhook marks the
   invoice paid (reusing 014's existing paid transition + OGM/audit trail).
   PSP DECISION (product-owner decision 2026-07-16, investigated per the
   2026-07-15 note below): use Mollie Connect for Platforms — OAuth-based
   sub-merchant onboarding + payment splitting, so each organisation's
   invoice payments land in that organisation's own bank account rather
   than a central platform account. Chosen over Stripe (equally viable,
   Accounts v2 Connect, but no added benefit here) and POM (no self-serve
   developer API surfaced during investigation — its integration model
   looks partner/sales-led, not something an unattended implementation
   pass can build against). Still design the PSP integration behind an
   abstraction (a `IPaymentProvider`-style seam) so Stripe or POM can be
   added later without a rewrite — do not hardcode Mollie-specific types
   past that boundary. Each organisation completes Mollie's own hosted
   onboarding (OAuth) from director-web; do not build a custom KYC/PII
   collection flow for this.
2. Automatic payment reminders for overdue invoices (configurable delay
   and cadence per location; respects 020's email infrastructure if built,
   otherwise in-app/push via 013).
3. Automatic payment receipt (betalingsbewijs) to the parent when an
   invoice transitions to paid (manual marking or webhook).

Key constraints:
- Webhook handling must be idempotent and tenant-scoped (payment events
  arrive on a public endpoint — resolve tenant from the payment reference,
  e.g. the OGM, never trust payload tenant claims).
- All money in cents; PSP fees recorded separately, never mutate the
  invoice total.
- i18n keys everywhere (reminder + receipt templates in NL/FR/EN).

Out of scope:
- SEPA direct debit (026), CODA matching (025).
- Partial payments / payment plans — decide at plan time if trivial,
  otherwise defer.
```

**Shipped 2026-07-16** — `specs/014a-invoice-payments-plus/` (spec → clarify → plan → tasks →
checklist → analyze → implement → design-compliance review → converge → PR #34, squash-merged
after green CI — 741/741 backend + 182/182 web + 85/85 parent-mobile tests). Mollie Connect for
Platforms behind a new `IPaymentProvider` abstraction (mirrors `IExpoPushSender`'s existing
port/adapter shape) — OAuth connect/disconnect on director web, a parent-mobile "Pay now" action
opening Mollie's hosted checkout, and a tenant-resolving public webhook (`TenantExempt`) that
confirms payment via two new public-schema tables (`payment_provider_connections`, `payments`)
resolved solely from a system-generated `PaymentReference`, never a client-supplied tenant claim.
OAuth tokens encrypted at rest via ASP.NET Core Data Protection (`IPaymentTokenProtector`) — the
first per-tenant third-party credential this codebase stores. The `send-payment-reminders` CLI
subcommand (mirrors `MigrateTenantsCommand`'s per-tenant loop, including failure isolation) is
this codebase's first scheduled-job entrypoint, wired to a new Cloud Scheduler + Cloud Run Job in
`infra/gcp/payment-reminders-scheduler.tf` — authored but deliberately not applied (manual
post-merge step, same convention as every other production/infra change here). Betalingsbewijs
(payment receipt) renders on-demand from the invoice's `Paid`-state fields, matching 014's own
invoice-PDF pattern discovered during planning to already be on-demand rather than the
stored/signed-URL pattern this feature's spec first assumed — corrected before implementation.
`/speckit-checklist`'s security/regulatory-focused pass found five genuine spec gaps (OAuth
token-expiry handling, reminder-cap persistence across settings changes, reminder-progress
across regenerate, receipt PII scope, per-tenant reminder-job failure isolation) — all fixed in
spec.md, not deferred. `/speckit-converge` found four real implementation gaps after the first
pass: a revoked/expired Mollie token propagated as an unhandled 500 instead of the spec's
required graceful "reconnect" state (fixed — the connection now flips to `Disconnected` and
returns a distinct failure); no payment test actually exercised `FakeExpoPushSender` despite two
new notification types; the reminder job's per-tenant failure isolation existed in code but was
never tested (a prior comment incorrectly claimed it wasn't testable through the shared test
host — it is); and the concurrent-payment race (online payment vs. manual mark-paid at the same
moment) had a correctness guard but no test. Fixing those surfaced two test-isolation bugs of
this pass's own making: `FakePaymentProvider.Payments` is a Singleton shared across every test
in a class (assertions must scope by `InvoiceId`, not assume it's the only entry), and a
deliberately-broken tenant schema needs cleanup (`ProvisioningStatus = Failed`) so it doesn't
fail every subsequent test sharing the same `IClassFixture`. Confirms the recurring
`TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern (012a,
013c, 006a, 013d, 013g, 013h, 014) yet again — this time both files needed extending, and
`LegacyVaccinationMigrationTests` specifically needed explicit `DROP COLUMN`s for the new
`Location` fields since (unlike `TenantMigrationRolloutTests`) it never drops `locations`
wholesale.

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
- Belcotax On-web automated submission (Phase 3). MVP = PDF only.
  Directors enter data in Belcotax On-web manually.
  LEGAL NOTE (verified 2026-07 on opgroeien.be + financien.belgium.be):
  digital filing of the 281.86 attest data with FOD Financiën via
  Belcotax-on-web is a hard obligation (deadline: end of February following
  the income year) and the mandatory federal model attest must be used.
  Manual entry in the Belcotax-on-web portal satisfies the obligation for MVP;
  the automated path is feature 019 item 5. Technical docs:
  https://financien.belgium.be/nl/E-services/Belcotaxonweb/technische-documentatie
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
Build a digital companion for the MeMoQ zelfevaluatie-instrument — the
official Flemish pedagogical-quality self-evaluation for childcare.

REWRITTEN 2026-07-19 against the official instrument documents (handleiding
2016 + the six per-dimension invulbare forms, 2025 layout refresh, © Kind en
Gezin/Opgroeien – UGent – KU Leuven; source markdown conversions in
docs/memoq/). The earlier draft of this block guessed the dimension names
and assumed a director-completed annual form — both wrong. Do not reuse it.

What MeMoQ actually is (verified):
- Six dimensions: 1 Welbevinden, 2 Betrokkenheid, 3 Emotionele
  ondersteuning, 4 Educatieve ondersteuning, 5 Omgeving, 6 Gezinnen en
  diversiteit. Two variants exist (groepsopvang / gezinsopvang) — build
  the GROEPSOPVANG variant only (our segment); the data model must not
  preclude adding the gezinsopvang variant later.
- Per dimension, a quality circle of 3 steps:
    Step 1 "Verken": dimensions 1–2 use per-child OBSERVATION ROUNDS
      (metadata: filled-by + function, date, leefgroep, #children,
      #caregivers, binnen/buiten, time from–to, activity type
      [eetmoment/geleide activiteit/vrij spel/anders]; up to 10 child
      rows, each with free-text notes + a welbevinden and/or
      betrokkenheid score on the official 5-level signal scale).
      Dimensions 3–6 use STATEMENT sets self-rated 1–4 ("doe ik momenteel
      niet" → "echte kwaliteit van mij"), grouped in named themes (e.g.
      dim 3: "positieve relaties", "sensitief-responsieve begeleiding").
      All dimensions also have open reflection questions (free text).
    Step 2 "Stel vast": one overall dimension score 1–10 + a written
      "waarom", plus structured sterktes / aandachtspunten lists, plus an
      external-factors note.
    Step 3 "Onderneem actie": repeatable action blocks — goal ("We willen
      ervoor zorgen dat…"), steps, evaluation date, achieved /
      not-achieved (+ follow-up action if not).
- Philosophy (the instrument's own words): FLEXIBLE. Any dimension first,
  any subset of statements/questions, own pace (~3 months/dimension is
  their rough guide), individual or team, multi-year planning recommended.
  It is a SELF-evaluation: nothing is submitted to Opgroeien. It is
  explicitly NOT a functioneringsgesprek. Zorginspectie uses a separate,
  simpler monitoring instrument (5 dimensions, 1–4, unannounced ≥2h
  visit) — out of scope for software, but self-evaluation prepares for it
  and inspection findings are a valid trigger to start a cycle.

Product decisions (confirmed with product owner 2026-07-19):
- Depth: STRUCTURED BUT ALL OPTIONAL. Digital forms mirror the 3-step
  cycle, but no enforced sequence, no required completeness, any
  dimension in any order, everything skippable, free text everywhere.
  Do not turn this into a compliance wizard.
- Surfaces: director web hosts the full cycle (create cycle, statements,
  reflections, findings, action plans, multi-year planning overview);
  the caregiver TABLET gets one lightweight capture screen for
  observation rounds (dims 1–2) since those happen in the room.
- Privacy: statement ratings and personal reflections are PRIVATE TO THE
  PARTICIPANT until they explicitly share them into the team cycle.
  Directors see shared content and cycle-level aggregates only. This
  mirrors the manual's safe-atmosphere principle.
- Child rows in observation rounds are DETACHED from child records:
  label "Kind 1..10" + optional free-text label, NO FK to children.
  Observation scores must never appear on a child profile, in the parent
  app, or in any per-child export.

What to build:
- memoq_cycles: per location (and optionally per leefgroep), dimension,
  variant ('groepsopvang'), status (open/closed — closing is optional
  bookkeeping, not a lock), started/closed dates, freeform title/notes.
  Multiple cycles per dimension over the years = the multi-year planning
  view (a simple timeline of past and planned cycles per dimension; a
  planned cycle is just a cycle with a future start and nothing in it).
- Observation rounds (dims 1–2): the round form + detached child rows as
  described above; capture on tablet or web; rounds attach to a cycle.
- Statement responses (dims 3–6): per participant, per statement, 1–4 +
  optional note; participant-private until shared (share = snapshot into
  the cycle, further edits stay private again until re-shared).
- Reflection answers: free text per official question, same privacy model.
- Findings (step 2): per cycle: 1–10 score, waarom, sterktes[],
  aandachtspunten[], external factors — team-level, entered by anyone in
  the cycle, visible to the cycle.
- Action plans (step 3): repeatable blocks with goal/steps/evaluation
  date/outcome; surfaced in a simple "open MeMoQ actions" list for the
  location so they don't die in a closed cycle.
- Instrument content as SEED DATA, versioned: dimensions, themes,
  statements, reflection questions, observation-scale descriptions are
  reference data (public schema, like the vaccine catalog 013g), keyed by
  instrument version ("2016/2025-layout"). Cycles reference the version
  they were run against; an instrument update creates a new version and
  never rewrites historical cycles.
- Print/export: a cycle (with its shared content only) exports to a clean
  PDF (QuestPDF) — the thing a director hands to a pedagogisch
  ondersteuner or shows when Zorginspectie asks how quality work is done.

Key constraints:
- Statement/question texts must match the official instrument exactly
  (source: docs/memoq/ conversions; verify against the PDFs) — in NL.
  FR/EN: UI chrome is translated per i18n rules, but the official
  instrument content itself exists only in Dutch — display NL content
  with a note, do not machine-translate the official texts (they are the
  calibrated instrument). This is a deliberate, documented exception to
  the "all user-facing text via i18n keys" rule, scoped to instrument
  content only.
- Tablet observation capture must work offline (rounds happen on the
  floor) — reuse the existing offline-queue infrastructure (008).
- Personal (unshared) responses are excluded from director exports and
  from 038's team-visible surfaces; deleting a participant's unshared
  drafts on their departure is fine (they were never team data).
- No submission to any government system — nothing in 033/019/034 touches
  this feature.

Open questions (do NOT invent — resolve before /speckit-plan):
- Content licensing: embedding the full statement sets verbatim goes
  beyond "citeren mits bronvermelding" — ask Opgroeien
  (klantenbeheerder or software-ontwikkeling@) for permission to
  reproduce the instrument content in-app, with attribution. If refused:
  fall back to cycle/step scaffolding + user-entered content with links
  to the official PDFs (the feature still works — the structure is the
  value; decide the fallback UX at plan time, not now).
- Whether the "waarmee begin ik het best?" quick-start test (handleiding
  p.15–16) is worth digitising in v1 or linked as PDF. Default: link.

Out of scope:
- The gezinsopvang variant (data model ready, content not seeded).
- The scientific measurement instrument and Zorginspectie's monitoring
  instrument (theirs, not ours).
- Machine translation of official instrument content.
- Any per-child longitudinal analysis of observation scores (deliberate:
  rows are detached; see product decisions).
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
- Data-completeness monitor (Bitcare calls this "kwaliteitsmonitor"):
    flags missing/expired critical data per child and staff member —
    e.g. no inlichtingenfiche fields, no authorised pickups, expired
    medical info, staff document gaps. Cheap to build, high director value.

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

4. FO-SU-05 XML monthly attendance report
   (contract verified 2026-07-15 against FO-SU-05.xsd v2.3 + the official
   "registratie aanwezigheden — beschrijving" PDF, 04/2025; commit both to
   docs/integrations/opgroeien/):
   Generate the XML file for Opgroeien monthly attendance submission.
   Schema: FO-SU-05.xsd v2.3. Root element: <IKG>, containing:
     prestaties/prestatie[1..n]: per line kindInfo (kind: code, voornaam,
       naam, bo J/N) + aanwezigheden + gerechtvaardigde_afwezigheden +
       niet_gerechtvaardigde_afwezigheden (each an aanwezigheidsType)
     closing/verantwoordelijke (name of the person who filled the form or
       the location responsible)
     locatiegegevens: naam_locatie (≤50), dossiernummer (9–10 digits),
       naam_organisator (≤50), periode "MM/YYYY", aantal_openingsdagen,
       bo J/N, flex J/N
     Header: DocumentIndicator fixed "FO-SU-05", FormVersion,
       MinReaderVersion, UsedViewer = "ChildCare vX.Y.Z"
     FormulierGecontroleerd must be "J" — "N" is not processed
     Config (optional): TargetURL default ko.formulieren@kindengezin.be,
       Subject default "Opvangprestaties"
   Duration buckets (aanwezigheidsType — counts per month per kindcode,
   NOT per-day begin/end times):
     min3u: < 3h — ONLY allowed when bo=J (buitenschoolse opvang)
     min5u: babies/toddlers < 5h; for BO children 3h–4u59
     min11u: 5h–10u59 (planned_duration_minutes 300–659)
     min11uFlex: > 10u59 (≥ 660) — only locations with flex permission
   Business rules from the beschrijving PDF:
     - "Bestellen is betalen": every reserved day counts — presence,
       justified absence (respijtdag/ziekte), or unjustified absence, each
       in the same duration buckets. Even same-day ad-hoc care counts as
       reserved.
     - Presences counted are between 6u and 20u; night care (20u–6u) and
       >10u59 prestaties may ONLY be registered by flex-permitted locations
       (non-flex locations charge a free tariff for those hours instead and
       must not register them).
     - Per line the sum of each block may not exceed aantal_openingsdagen.
     - Two lines with the same kindcode are only allowed when the child had
       both BO and baby/peuter care in the same month (BO auto-applies from
       full-time school or age > 3.5y / 42 months; mid-month transition =
       two lines, one with bo=J and one with bo=N); then the SUM of both
       lines is checked against aantal_openingsdagen.
     - Opgroeien matches kindcode + naam + voornaam against the attest
       inkomenstarief data with a 90% name-matching procedure (name parts
       split on spaces; swapped first/last names tolerated). On mismatch the
       whole form is returned by email with an error overview — names must
       be copied EXACTLY as on the attest.
     - A child can have MULTIPLE kindcodes (e.g. separated parents each
       with their own inkomenstarief attest) — the data model must allow >1
       kindcode per child.
     - Kindcode = 11 digits YYMMDD-NNN-VV (birthdate + sequence + attest
       volgnummer; a new attest after recalculation bumps VV).
   Submission (MVP path): monthly email to ko.formulieren@opgroeien.be;
   multiple locations may go in ONE mail with the XMLs as separate
   attachments. Opgroeien also emails pre-filled PDF forms monthly to T2
   locations (kindcodes from the previous month) regardless — ignore them
   when submitting XML. Opgroeien has announced webservice-based XML
   exchange (see aanwezigheidsregistratie-webservice PDF; IKGResponse
   returnCode 0000 = OK, 0003 = firewall block) — evaluate at plan time
   whether the webservice path can replace email.

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
- Kinderopvangtoeslag / Groeipakket monthly attendance submission for
  vrije-prijs children — moved to feature 033 (Phase 2), it targets the
  Phase 1 client segment and uses a separate, simpler REST/JSON webservice
  (bearer token), not the IKT SOAP stack.
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
  parent/contact of children currently enrolled there, with the subject/body
  they compose. One email per family household, not per child — a parent
  with two children at the same location must receive a single combined
  email, not two.
- Bulk email attachment: when composing a bulk email, the director can
  upload a document (PDF or image — e.g. a menu, a policy update, a permission
  slip) via a GCS signed URL (constitution's storage convention) and have it
  attached to the email sent to every recipient.
- Daily report email: the same aggregated daily summary shown in the parent
  app (feature 013, sourced from child_events in feature 009) is emailed
  automatically once per day, at end of day, to every parent/guardian contact
  of the child (e.g. both mother and father, when both are on file) —
  independently, each to their own address, not collapsed to a single
  "primary contact." Default-on (opt-out), not opt-in: every parent/guardian
  contact with an email on file receives it unless they have explicitly
  unsubscribed; unsubscribing is the only way to stop receiving it. Each
  email includes an unsubscribe link/action; an unsubscribed contact stops
  receiving the daily digest until they re-subscribe (unsubscribe state is
  per-contact, not per-child, since a contact may have children at multiple
  locations, and is independent of any other contact's subscription state —
  one parent unsubscribing must never affect the other parent's emails). A
  director/caregiver can additionally trigger an on-demand one-off resend,
  independent of the daily automatic send and unaffected by a contact's
  digest-unsubscribe state.
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
- The daily-digest unsubscribe action must work with no login required (a
  signed, single-use-purpose link is standard for this) — a parent should
  never need to authenticate just to opt out of an email.
- Uploaded bulk-email attachments follow the existing GCS signed-URL,
  no-public-blob-URL convention (constitution) and a sane size cap (decide
  at plan time) to avoid provider rejection on large sends.

Edge cases:
- A location or group/section has zero enrolled children at send time —
  no-op, not an error.
- A parent contact has no email on file — skip that contact, log it, and
  still send to the child's other contacts.
- A daily report is requested for a child with no events recorded yet that
  day — send an email that clearly says "no updates yet" rather than an
  empty-looking template.
- A contact has unsubscribed from the daily digest but is also the target of
  an on-demand resend or a bulk/announcement email — those are separate
  channels from the digest-unsubscribe flag and must still be delivered.
- Rate limiting / provider throttling on large bulk sends (a big location
  could have 100+ families) — batch or queue rather than sending
  synchronously in the request.

Out of scope:
- SMS or WhatsApp channels.
- Open/click tracking or delivery analytics.
- A full multi-channel notification preference centre (per-notification-type
  granularity across push/in-app/email) — Phase 3. The daily-digest
  unsubscribe above is a single, narrow opt-out flag, not that broader system.
```

**Shipped 2026-07-19** — `specs/020-email-communications/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 67/68 tasks — see below, PR #38, squash-merged after
green CI — 845/845 backend + 202/202 web + 162/162 caregiver-mobile + 96/96 parent-mobile tests
passing). Resumed mid-flight: a prior session had already implemented all four user stories
(Scriban NL/FR/EN templating replacing `EmailService`'s raw-string-literal English-only
templates, director-web bulk-email compose, the daily-digest job mirroring 014a's
`send-payment-reminders` Cloud Scheduler/Cloud Run Job shape, and the closure/announcement email
fan-out) with 8 commits, but left an uncommitted working-tree diff mid-fix and one task (T066)
outstanding. This session verified and committed that diff (a real converge-style fix for two
gaps: the daily-digest resend and on-demand resend paths didn't tolerate a per-recipient SMTP
failure the way bulk send already did, FR-012; and bulk send/the digest dispatched serially
instead of with bounded parallelism, FR-015 — added a shared `BoundedConcurrency` semaphore
helper, plus attachment content-type/size-cap regression tests for FR-017), then ran
`/speckit-converge` fresh and found zero further actionable gaps — confirmed via spot-checks of
the riskiest items rather than a shallow pass: FR-006's per-child photo-consent flag is honored
correctly not by new code in this feature but by design (research.md R7 — the daily-report
template renders no photos at all; consent is already gated upstream in feature 013's
`GetDailySummaryQuery` before the response reaches this feature's rendering layer, so a second
consent check here would only risk drifting from that one), the `infra/gcp/main.tf`
Cloud Scheduler/Job pair for `send-daily-reports` exists and mirrors 014a's shape, and the
compose screen's keyboard-focus/zero-recipient states (FR-016, FR-019) are implemented. T066
("run quickstart.md end-to-end against a local Mailhog/Mailpit-backed dev environment") was left
unchecked rather than attempted or silently skipped: `EmailService.SendAsync` — pre-existing
infra shared by password-reset/staff-invite/every other transactional email, not owned by this
feature — hardcodes `SecureSocketOptions.StartTls` for any SMTP port other than 465, which
Mailhog's default listener doesn't support, so this app cannot currently point at a local Mailhog
container without a change outside this feature's scope; no `docker-compose` Mailhog service
exists yet either. Worth remembering generally for any future feature whose own quickstart.md
calls for a local Mailhog/Mailpit environment: that infra doesn't exist in this repo yet and the
existing SMTP connection logic would need a fix first (a dev-only `SecureSocketOptions.None` path,
or a Mailhog image with a TLS cert configured) — this is a real, not hypothetical, blocker,
confirmed by reading `EmailService.cs`'s actual `SendAsync` implementation, not assumed. Every
behavior T066 would have exercised is otherwise covered by the extensive `FakeEmailSender`-based
integration test suite this feature and its converge pass built.

### 012a — Waiting List

```
Build the KDV waiting list — the day-to-day tool directors use to track
families who want a place but don't have one yet.

What to build:
- waiting_list_entries table (tenant schema):
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    child_first_name TEXT NOT NULL,
    child_last_name  TEXT NOT NULL,
    date_of_birth    DATE NOT NULL,
    contact_name     TEXT NOT NULL,
    contact_email    TEXT,
    contact_phone    TEXT,
    location_id      UUID REFERENCES locations(id),  -- which KDV they want
    requested_start_date DATE,
    priority         INT DEFAULT 0,  -- manual ordering; lower = higher priority
    status           TEXT CHECK (status IN (
                       'waiting','offered','enrolled','withdrawn'
                     )) DEFAULT 'waiting',
    notes            TEXT,
    registered_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at       TIMESTAMPTZ

- Web admin list view: sortable by priority, filtered by status.
  Shows: child name, DOB, contact, requested start date, status.
- Priority drag-and-drop reordering (or up/down arrows).
- Alongside the list: an occupancy view — which days have free capacity
  from the requested start date. Reads from attendance (010) + contracts (007).
- Status transitions: waiting → offered (director contacts family) →
  enrolled (contract created in 007) or withdrawn (family declined/cancelled).
- When status becomes 'enrolled', allow linking to the child record created
  in 006 (manual link — not auto-created; director confirms identity match).
- Basic email notification when status changes to 'offered' — uses the
  EmailService from 020 if already shipped, otherwise a simple MailKit
  send inline.

Key constraints:
- Waiting list entries are pre-enrolment — no child record, no contract.
  They are intentionally lightweight (name + DOB + contact only).
- The occupancy view must honour closure days (011) — do not show a closed
  day as having free capacity.
- All user-facing strings use i18n keys (NL/FR/EN).
- Soft-delete is sufficient for withdrawn entries (keep for history).

Edge cases:
- A family withdraws and then re-applies. Director creates a new entry —
  no merge with the old one required for MVP.
- The same child appears twice (duplicate entry). Flag visually (same name
  + DOB on multiple rows) — do not auto-merge.
- Director converts a waiting list entry to enrolled but no matching child
  exists in 006 yet. The conversion flow should prompt: "Create child record
  now?" with first/last name + DOB pre-filled.

Out of scope:
- Parent self-registration to the waiting list via public URL (feature 023).
- Automated notification when a place becomes available (feature 023).
- Tour invitation workflow (feature 023).
```

**Shipped 2026-07-10** — `specs/012a-waiting-list/` (spec → clarify → plan → tasks → checklist →
analyze → implement → converge, 67/67 tasks incl. a 3-task convergence pass, 36 new backend
integration tests + all 359 pre-existing passing (395/395), 7 new web component tests + all 34
pre-existing passing (41/41)). Scope deltas from the plan above, worth knowing before starting
feature 013+:

- **Occupancy reads from contracts, not attendance** — corrected during specification against
  this prompt's own premise: feature 010's attendance data is same-day/historical and doesn't
  exist yet for the future dates a waiting-list occupancy check actually needs. Computed
  instead from active `Contract`s (007) against `Location.MaxCapacity` (004), with published
  `KdvClosureDay`s (011) marking a date `Closed` rather than a numeric count — a regression
  test (`OccupancyTests.Occupancy_FutureDateWithNoAttendanceRecords_StillComputesFromContracts`)
  exists specifically to catch any future change that accidentally couples the two. Worth
  remembering generally, same lesson as feature 012's BKR/rota decoupling: a BACKLOG prompt's
  assumed data source needs re-checking against what an earlier feature actually shipped, not
  implemented as written.
- Priority ordering is per-location, not global — reordering and the default sort both operate
  within one selected location's queue; moving a family in location A never touches location
  B's order (`ReorderTests`).
- Reordering is restricted to `waiting`-status entries only — `offered`/`enrolled`/`withdrawn`
  entries return `409 errors.waiting_list.not_reorderable_in_current_status`.
- The status lifecycle is an explicit allow-list (`waiting→offered`, `waiting→withdrawn`,
  `offered→enrolled`, `offered→withdrawn`, `offered→waiting`) — anything else, including any
  transition originating from `enrolled`/`withdrawn`, is rejected with
  `409 errors.waiting_list.invalid_status_transition`. The offer email fires only on
  `waiting→offered` with a contact email present; the `offered→waiting` revert never emails.
- Duplicate detection (same child first/last name + DOB) always compares against the full
  location roster regardless of the currently applied status filter — a `waiting` entry and a
  `withdrawn` twin are still flagged even when the list is filtered to the default
  `waiting`-only view. This was a real gap (CHK012) caught by the requirements-quality
  checklist pass, fixed in spec.md before implementation.
- `/speckit-converge` found three real gaps after implementation, all fixed rather than
  deferred: (1) `GetOccupancyQuery`'s contract projection crashed with an EF
  "tracking query... owned entity" exception because it projected `ContractedDays` (an
  owned-type collection) without `.AsNoTracking()` — worth remembering generally for any future
  query projecting an EF owned-type collection into an anonymous type; (2) `Notes` had no
  `MaximumLength(2000)` validator despite the column being capped at 2000 chars, so an
  over-length value would have reached Postgres as an unhandled exception (500) instead of a
  clean `400 errors.validation`; (3) occupancy silently computed normally for a deactivated
  location instead of being rejected, contradicting this feature's own spec Edge Cases section.
- Extending `TenantMigrationRolloutTests`' schema-revert helper for the new table's FKs (to
  both `children` and `locations`) is now a required step for any future feature adding a table
  with a foreign key — every migration-adding feature since 003 has hit this same test and
  needed the same fix; the test's own docstring lists the full precedent chain.

---

### 013a — Day Reservations

```
Allow parents to submit day requests (absence, extra day, exchange day)
through the parent app, and give directors a single approval queue.

Context: parents currently have no self-service. They call or text to
say their child is sick, or to ask for an extra day. This feature moves
that workflow into the app, creating an audit trail and removing friction
for both sides.

What to build:
- day_reservations table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    child_id        UUID REFERENCES children(id) NOT NULL,
    type            TEXT CHECK (type IN (
                      'absence',    -- child won't come (sick, holiday, other)
                      'extra',      -- parent wants an additional day
                      'exchange'    -- parent swaps a contracted day for another
                    )) NOT NULL,
    requested_date  DATE NOT NULL,
    exchange_for_date DATE,         -- if type='exchange': which contracted day to swap
    reason          TEXT,           -- parent's free-text reason
    absence_justified BOOLEAN,      -- set by director on approval (justified = respijtdag, no charge)
    status          TEXT CHECK (status IN (
                      'pending','approved','rejected'
                    )) DEFAULT 'pending',
    requested_by    UUID REFERENCES users(id),
    decided_by      UUID REFERENCES users(id),
    decided_at      TIMESTAMPTZ,
    director_notes  TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ

- Parent app: three entry points — "Mijn kind is ziek", "Extra dag aanvragen",
  "Dagwissel aanvragen". Each opens a simple form (date picker + optional reason).
- Director web admin: a "Verzoeken" to-do list — all pending requests across
  all children, newest first. Director approves or rejects with one tap.
  For absence requests: director sets absence_justified flag at approval time
  (justified = free for parent per billing model; unjustified = charged).
- Push notification to parent when status changes to approved or rejected.
  If director adds a note on rejection, include it in the notification.
- Approved absences feed directly into attendance (010) as pre-registered
  absences for that date. Approved exchanges update the effective care schedule
  for invoice purposes (014).

Key constraints:
- A parent can only submit requests for their own children.
- Exchange requests must validate that exchange_for_date is a contracted day
  for that child (cross-check with contracts in 007). Reject at submission
  if not.
- Absence requests for dates in the past (> 1 day ago) are blocked — parent
  must ask the director directly for retroactive corrections.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Parent submits an extra-day request for a date that is full (no capacity).
  Director sees the capacity warning when approving — can reject with a note.
- Exchange date falls on a closure day (011). Block at submission.
- Parent withdraws a pending request before the director acts. Add a
  'cancelled' status transition (parent-only action, only on 'pending').

Out of scope:
- Staff leave requests (feature 027-staff-app).
- Automatic slot-availability check at submission time (show warning but
  do not hard-block; director makes the final capacity decision).
```

**Shipped 2026-07-11** — `specs/013a-day-reservations/` (spec → clarify → plan → tasks → checklist
→ analyze → implement → design-compliance → converge, 62/62 tasks, 20 new backend integration
tests + 6 new web tests + 6 new parent-mobile tests, 478 backend + 59 web + 53 parent-mobile
passing). New `day_reservations` tenant table; parent-mobile gets three entry points sharing one
form component plus an own-request-history/cancel screen; director web gets a "Verzoeken" queue.
Worth knowing before starting a dependent feature:

- **`DayReservation` deliberately has no `LocationId`** — a parent reporting illness doesn't pick
  a location. Absence approval resolves it at approval time from the child's active `Contract`
  matching the requested date's weekday (a child can hold contracts at two different locations
  under feature 007's split-location rule), failing cleanly with a new `NoContractedLocation`
  result rather than guessing — found and fixed during implementation, not anticipated in the
  original plan (research.md R7). Any future feature resolving "which location does this
  child-level action apply to" without an explicit location on the request itself should follow
  this same weekday-match-against-active-contracts pattern rather than inventing a new one.
- **Absence approval reuses `MarkAbsentCommand` (feature 010) via `IMediator.Send`, not a second
  attendance-write implementation** — inherits its closure-day guard and unique-constraint race
  handling for free. Extra/exchange approvals only transition the reservation's own status; no
  `AttendanceRecord` is written, since 010's existing check-in flow already handles an unplanned
  day with no matching contracted day (`PlannedDurationMinutes = null`).
- **Every approve/reject/cancel decision runs inside `IAdvisoryLockService.RunExclusiveAsync`
  keyed on the reservation's own id** (feature 007's precedent for serializing concurrent
  requests against the same aggregate) rather than an optimistic-concurrency/`ExecuteUpdateAsync`
  guard — chosen specifically because the naive guarded-update approach has a real lost-update
  window here: absence approval does a side effect (writing `AttendanceRecord`) *before* the
  status flip, and a losing concurrent call could otherwise leave the attendance record written
  even though the reservation itself ends up rejected by the other caller. The lock closes that
  window entirely rather than accepting the risk.
- **Capacity warnings for `extra`-type requests reuse feature 012a's occupancy computation**
  (active contracts vs. `Location.MaxCapacity`), never attendance — attendance doesn't exist for
  future dates, same reasoning 012a already established. Shown as a `warning` (amber) badge, not
  `danger` — the spec explicitly calls this advisory, not blocking.
- **`Badge`'s `warning` variant was added to `web/components/ui/badge.tsx`** — design-system.md
  locks four semantic colors (danger/warning/success/info) but the shared `Badge` component only
  implemented three before this feature. Any future web feature needing an amber/advisory badge
  should reuse this variant rather than adding a fourth copy.
- **A pre-existing test maintenance trap recurred and was fixed**: `TenantMigrationRolloutTests`'
  schema-revert helper needs every new migration's own history-row deleted, or EF's pending-
  migration detection silently stops finding earlier gaps once a newer migration's row is left in
  place — this exact failure mode is already documented in that test file's own comments across
  eight prior features, and this feature initially missed it too (caught by running the full
  backend suite, not the filtered feature-scoped tests, before opening the PR). **Any future
  feature adding a tenant-schema migration must add its migration name to that DELETE list** —
  worth checking this specific test file first, same standing advice 012a's shipped-notes already
  gave for a different part of the same test.
- Design-compliance pass (static review, no simulator) found and fixed three real deviations
  before merge: a parent-mobile cancel-action touch target at 32pt (below the 48pt floor), Home
  quick-action icons at an off-scale 18px (no precedent anywhere in the app; fixed to the
  established 20px), and the capacity-warning badge using `danger` instead of `warning` (see
  above).

---

### 009c — Multi-Child Events

```
Allow caregivers to select multiple children before logging an event,
so one submission creates one child_event record per selected child.

Context: today a caregiver logging nap time for 8 babies must tap each
child individually — 8 separate submissions of the same event with the
same start time. For group moments (everyone goes down for a nap at 13:00,
everyone gets a diaper change at 11:00), this is pure friction.

What to build:
- In the caregiver app, when starting a new child event, add a
  "Meerdere kinderen" toggle before the child picker.
- When toggled on: replace the single-child selector with a multi-select
  list showing all children currently in the room (present today, same group).
  Each child appears as a selectable card (photo + name). Tap to select/deselect.
  A "Alles selecteren" shortcut selects all present children.
- The event form below is identical to the single-child flow (event type,
  fields, timestamp). On submit: the API receives one request with
  child_ids: UUID[] plus the event payload.
- API: POST /child-events/batch
    { child_ids: UUID[], event_type: ..., occurred_at: ..., payload: {...} }
  Server loops internally and creates one child_event row per child_id.
  Response: { created: UUID[], errors: [{child_id, reason}] } — partial
  success is allowed (e.g. if one child has a conflicting record).
- After submission: a summary toast "Opgeslagen voor 8 kinderen ✓."
  If any child failed, show which ones with the reason.

Supported event types for multi-select (the ones that make sense as
group events):
  sleep (nap start/end), diaper, feeding_bottle, feeding_solid, mood,
  activity, note, custom.
NOT supported for multi-select (inherently individual):
  temperature, medication, weight/growth_check — these require
  per-child values and must remain single-child.

Key constraints:
- The batch endpoint must be transactional per child — failure for one
  child does not roll back the others.
- Maximum 30 children per batch (guards against accidental "select all"
  for a large location).
- The existing single-child flow is unchanged — multi-select is opt-in
  per event logging session.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A child in the multi-select list gets checked out mid-flow (another
  caregiver scans them out). The server validates presence at occurred_at
  and returns an error for that child in the batch response.
- Caregiver accidentally selects a child from a different group (shouldn't
  happen with the group filter, but if it does): server rejects with
  403 if the device token's group scope doesn't match.
- Offline: the batch is stored as a single offline_queue entry. On sync,
  it is replayed as one batch call — not exploded into N individual calls.

Out of scope:
- Bulk editing past events (retroactive multi-update) — not needed.
- Multi-select for group activities (009b already handles the group-level
  concept; this is for per-child records that happen to be identical).
```

**Shipped 2026-07-11** — `specs/009c-multi-child-events/` (spec → clarify → plan → tasks →
checklist → analyze → implement → design-compliance → converge, 37/37 tasks including 2
convergence tasks, 7 new backend integration tests + 2 device-token auth-fix regression tests,
487 backend + 124 mobile passing). Caregiver room-roster multi-select mode, a
`POST /api/child-events/batch` endpoint with per-child partial-success semantics, and a
`QuickActionSheet` batch mode with a partial-failure retry-only-failed screen.

- **Two premise corrections made during `/speckit-clarify`/`/speckit-plan`, before any code was
  written** (verified against the actual codebase, not assumed from this backlog prompt): (1) the
  prompt's "toggle before the child picker" doesn't match the shipped caregiver UI — there is no
  child-picker step anywhere; the flow is always child-first (room roster → child detail →
  `QuickActionSheet`). Multi-select is a mode on the room roster screen instead. (2) The prompt's
  "403 if the device token's group scope doesn't match" assumes a per-child scope check that
  doesn't exist on the single-child endpoint either — `LocationId`/`GroupId` have always come
  wholesale from the device token's own claims (never per-child), and the batch endpoint matches
  that existing behavior rather than inventing stricter enforcement. See
  `specs/009c-multi-child-events/research.md` R1/R3 for the full reasoning.
- **A real, pre-existing gap in already-merged features (008a/009/009b/010/012) was found and
  fixed as a confirmed prerequisite, not silently expanded scope**: `GET /api/children` and
  `GET /api/groups` — the room roster's own data source, which this feature's multi-select grid
  depends on — were `StaffOrDirector`-only since feature 008, predating 008a's kiosk/device-token
  model. A paired kiosk tablet's device token carries no role claim, so a pure kiosk session would
  403 on the exact screen this whole feature builds on. Confirmed with the user before including
  the fix (research.md R2) rather than either guessing or silently building on top of a suspected-
  broken foundation — the standing rule for a genuinely new, high-impact, no-precedent finding.
  Fixed via a new `DeviceOrStaffOrDirector` composite policy (`RequireAssertion`, since the two
  accepted schemes need different rules — a device token needs no role, a user JWT still needs
  `staff`/`director` — not one rule ANDed across both the way `RequireRole` alone would apply it).
- **`ChildEventBatchFailureReason` shipped with two values, not three**: an earlier plan-phase
  draft included a per-child `ValidationFailed` reason, but writing the actual handler showed this
  can never happen per-child — the batch's payload is shared across every selected child (one
  `EventType`/`Payload` for the whole request), so a payload validation failure rejects the *whole*
  batch via the standard `ValidationBehavior` pipeline (`422`) before any child is processed,
  exactly like the `batch_too_large`/`batch_type_not_supported` checks — never as one child's
  result alongside others that succeeded. Removed rather than kept as a reason that could never
  actually be returned; `/speckit-converge` separately caught that `contracts/child-events-batch-
  api.md`/`quickstart.md` still documented these two checks as returning a bespoke top-level
  `errorKey` rather than the actual `{errorKey: "errors.validation", fieldErrors}` shape feature
  009's own contract already established as the convention — corrected, not left as debt.
- **Per-child idempotency via a client-generated id per batch item** (`items: [{childId, id}]`,
  not a bare `childIds: string[]` array) — added after `/speckit-checklist` surfaced that a batch
  retried after an ambiguous network failure (server commits several children, connection drops
  before the response) had no way to distinguish "already succeeded" from "never attempted" on
  replay, risking duplicate `ChildEvent` rows. Reuses `RecordChildEventCommand`'s existing
  idempotency-by-id check (FR-013a, feature 009) per child rather than inventing a new mechanism.
- **A sync-time-only partial failure (batch replayed offline, some children fail, caregiver isn't
  watching) reuses feature 009's existing `"rejected: "` "needs review" convention** with a
  distinct `"partial: "` prefix, rather than a new review surface — `syncEngine.ts`'s `response.ok`
  branch now parses a `child_event_batch` row's body and routes a non-empty `errors` array to
  `markSyncError` instead of `markSynced`. Retry is manual (the caregiver reviews later), not
  automatic.
- A CI-only flake (not caught locally): `Batch_AllChildrenPresent_CreatesOneEventPerChild`
  asserted `OccurredAt` exact-equality after a Postgres round-trip — the same class of timestamp-
  precision flake feature 010 hit before (`.4072354Z` vs `.4072350Z`). Fixed with a 1-second
  tolerance; **any future test asserting an exact `DateTime` round-tripped through Postgres should
  use a tolerance, not `==`**, per this and 010's prior instance of the same mistake.

---

### 013f — Reservation Settings

```
Add per-location configuration for the day reservation feature (013a).
Some KDVs do not allow parents to request day swaps or have strict
policies on absences. Directors need control over which request types
are active and how they behave.

Context: 013a ships with a fixed approval-queue model. This feature adds
a settings layer so directors can customise behaviour without code changes.

What to build:
- Add to location settings (004 location record or a linked
  location_settings JSONB/table):
    reservation_absences_mode   TEXT CHECK (reservation_absences_mode IN (
                                  'disabled',       -- parents cannot submit absences via app
                                  'informational',  -- parent submits, director notified, no approval needed; auto-approved
                                  'approval'        -- default: director must approve/reject
                                )) DEFAULT 'approval',
    reservation_extras_mode     TEXT CHECK (reservation_extras_mode IN (
                                  'disabled',
                                  'informational',
                                  'approval'
                                )) DEFAULT 'approval',
    reservation_swaps_mode      TEXT CHECK (reservation_swaps_mode IN (
                                  'disabled',
                                  'informational',
                                  'approval'
                                )) DEFAULT 'disabled',  -- many KDVs disallow swaps by default
    reservation_notice_hours    INT DEFAULT 0  -- minimum hours in advance a request must be submitted
                                               -- (e.g. 24 = parent must submit at least 24h before the day)

- Behaviour by mode:
    disabled:      The request type does not appear in the parent app for
                   this location. If a parent somehow POSTs it, the API
                   returns 403 with "Dit type aanvraag is niet beschikbaar."
    informational: Parent submits; the request is immediately auto-approved
                   (status → 'approved', decided_at = NOW(), decided_by = system).
                   Director receives a push notification ("Melding: [Name] afwezig
                   op [date]") but no action is required.
    approval:      Existing 013a flow — director approves/reject in queue.

- Parent app: hides request types that are 'disabled' for this location.
  No confusing "not available" screen — the button simply doesn't exist.

- Director web admin: "Reserveringsinstellingen" tab in the location settings
  screen (004). Three dropdowns (one per type) + minimum notice hours field.
  A short explanation per option.

Key constraints:
- Mode changes take effect immediately for new requests. In-flight pending
  requests are not retroactively affected.
- reservation_notice_hours: if set to 24, a request for tomorrow before
  the same time yesterday is rejected at submission. Validate at the API
  on POST /day-reservations.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Director switches a type from 'approval' to 'informational' mid-month.
  Existing pending requests in the queue stay pending — director must
  manually close them. Show a warning when switching modes if open
  requests exist for this type.
- Director sets notice_hours = 48 but a caregiver (via the director's
  web admin) submits a retroactive absence for a past date. The notice
  check is bypassed for web-admin submissions — only enforced for parent
  app submissions.

Out of scope:
- Per-child overrides (e.g. "swaps allowed for this family"). All settings
  are per-location.
- Time-window restrictions (e.g. "only submit before 8am") — Phase 3.
```

**Shipped 2026-07-12** — `specs/013f-reservation-settings/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 61/61 tasks, 513/513 backend + 63/63 web + 56/56
parent-mobile tests passing, PR #22 squash-merged after green CI). Adds four settings to
`Location` (`reservation_absences_mode`/`reservation_extras_mode`/`reservation_swaps_mode`/
`reservation_notice_hours`) enforced by a new `ReservationPolicyResolver`, since `DayReservation`
deliberately has no `LocationId` (013a research.md R7 — a child can hold contracts at multiple
locations). When a request has more than one candidate location (a split-location child, or an
`extra`-type request with no weekday to match against), the most restrictive setting wins —
mirroring 013a's own exchange closure-day precedent (reject if any candidate location has a
closure) rather than inventing a new rule. Two premises in the original brief turned out false
and were corrected rather than built as specified: no director push-notification channel exists
anywhere in this codebase (`TenantUser` has no push token, no browser-push infra, the one
notification centre is `ParentOnly`) — auto-approved `informational` requests instead surface in
the existing director "Verzoeken" queue, distinguished by a `decidedBy: null` badge; and no
staff/director day-reservation submission path exists to exempt from the notice-hours check
(013a's `POST /api/day-reservations` is `ParentOnly` only) — building one would have been a
separate feature, not a side effect of a settings screen. Also ships the first real web
`/locations` screen — 007a's `NotYetAvailable` placeholder replaced with a list + two-tab detail
page (Algemeen / Reserveringsinstellingen), per 007a's own shipped-note that a referenced-but-
unbuilt web screen is a hard dependency for the next feature that needs it. A full test-suite run
surfaced two pre-existing bugs unrelated to this feature, fixed rather than left red:
`TenantMigrationRolloutTests` needed the new migration added to its schema-revert helper's
history-removal list (the same fix every migration-adding feature since 003 has needed — worth
checking that test first for any new tenant-schema migration), and `StaffScheduleEndpointsTests`
(feature 012) had a week-start date-math bug that only manifests when "14 days from today" lands
on a Sunday.

---

### 013b — Incident Reports

```
Build a digital incident/accident report form. This is a legal requirement
under the Besluit Kwaliteit Kinderopvang — every KDV must record incidents
involving children and keep them on file for inspection.

What to build:
- incident_reports table (tenant schema):
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    child_id            UUID REFERENCES children(id) NOT NULL,
    occurred_at         TIMESTAMPTZ NOT NULL,
    location_detail     TEXT,           -- 'indoor' | 'outdoor' | 'transit' | free text
    description         TEXT NOT NULL,  -- what happened
    injury_type         TEXT CHECK (injury_type IN (
                          'none','scrape','bump','cut','fall',
                          'bite','burn','allergic_reaction','other'
                        )),
    first_aid_given     TEXT,
    doctor_called       BOOLEAN DEFAULT FALSE,
    doctor_notes        TEXT,
    parent_notified     BOOLEAN DEFAULT FALSE,
    parent_notified_at  TIMESTAMPTZ,
    parent_notified_how TEXT,           -- 'phone' | 'app' | 'in_person'
    reported_by         UUID REFERENCES users(id),
    witnesses           TEXT,
    follow_up           TEXT,
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    updated_at          TIMESTAMPTZ

- Caregiver app: "Incident melden" button on child profile. Opens a form
  pre-filled with child name and current timestamp. Required fields: what
  happened, injury type.
- Web admin: incident history per child (in the child file). Director can
  also view all incidents across the KDV for a date range (inspection view).
- PDF export of a single incident report (QuestPDF) — formatted for printing
  or handing to Zorginspectie. Includes all fields + signature line.
- Push notification to director when a caregiver files an incident report
  for their location.

Key constraints:
- Incident reports are immutable after 24 hours (legal document). Directors
  can add a follow-up note but cannot edit the original description.
- Report remains linked to the child record even if the child is
  deactivated/soft-deleted — never cascade-delete.
- All user-facing strings use i18n keys (NL/FR/EN).
- The PDF must include the KDV name, address, and erkenningsnummer
  (from location settings in 004).

Edge cases:
- Caregiver files a report for a past incident (discovered later).
  Allow backdating occurred_at but log the discrepancy (created_at ≠ occurred_at).
- Two caregivers file a report for the same incident. Director merges
  them manually via director_notes — no auto-merge for MVP.
- A child has no parent push token (no app). Parent notification is
  logged as 'phone' or 'in_person' by the caregiver.

Out of scope:
- Parent digital acknowledgment / e-signature on the incident report (Phase 2).
- Zorginspectie reporting API integration (Phase 3).
```

**Shipped 2026-07-12** — `specs/013b-incident-reports/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 62/62 tasks including one convergence task, 533/533
backend + 133/133 mobile + 70/70 web tests passing, PR #23 squash-merged after green CI). Adds a
new `incident_reports` tenant table: caregiver-tablet filing with `reportedBy` resolved
server-side via feature 009's `IShiftAttributionService` (mirrors `child_events.recorded_by`
exactly — no PIN-confirmation step, since one would block offline filing), a 24-hour
immutability lock enforced server-side (only `follow_up` stays editable after), and a
director-web Incidents screen (list/filter/paginate/PDF export/reviewed-indicator). Two premises
in the original brief turned out false and were corrected during specification, per this
project's standing precedent (012/012a/013f) of documenting a corrected premise rather than
building it as literally specified: no director push-notification channel exists anywhere in
this codebase (same conclusion 013f already reached for an analogous ask) — an in-app
reviewed/unreviewed indicator substitutes; and no per-child "child file" screen exists yet in
`web/` (`/children` is still a placeholder, 007a) — a dedicated Incidents screen with a child
filter substitutes until one exists. `reported_by` is a `UUID[]` (zero, one, or more caregivers),
not the single nullable `UUID` the original schema literally specified — the same
singular-to-array correction feature 009 already made for `recorded_by`, for the identical
reason (device-token writes carry no individual caregiver identity). Two contract-level
deviations were corrected during implementation: `POST /api/incident-reports` is
device-token-only (no director-facing filing screen ships in this feature, and there's no other
source for `locationId`/`groupId`); and validation failures return `422`, not the contract's
literal `400` — every FluentValidation failure in this codebase already returns `422` via the
shared `ValidationBehavior` pipeline, and the contract's `400` was aspirational text, not a real
per-feature exception to that convention. `/speckit-converge` found one real gap after
implementation — the director list screen fetched a paginated response but exposed no way to
reach page 2+, leaving anything past the first 25 reports unreachable (FR-009) — fixed with
next/previous controls, not deferred. Extended `TenantMigrationRolloutTests`' schema-revert
helper for the new table's FKs to `children`/`locations` — the same fix every migration-adding
feature since 003 has needed. A full-suite run surfaced two CI-only issues neither caught
locally: `web/package-lock.json` was missing a nested `@swc/helpers` resolution that only
`npm ci` (not `npm install`) validates strictly, and only under CI's Node 20 (not this
session's Node 22) — the same class of issue 007a's shipped-notes already describe, reproduced
locally via a Node 20 container and fixed with a regenerated lockfile; and two new tests compared
a just-saved in-memory `ReviewedAt` (full .NET tick precision) against a freshly re-queried one
from Postgres (timestamptz's microsecond precision) with exact equality, flaky against
sub-microsecond rounding — the same class of issue feature 010's shipped-notes already describe,
fixed with a sub-millisecond tolerance. Also fixed a latent, unrelated gap found while running
the full web suite: vitest's default include glob matches Playwright's own `e2e/*.spec.ts`
files, so plain `npm test` tried to execute them as vitest tests and failed outright — excluded
`e2e/` in `vitest.config.ts`.

---

### 013c — Vaccine & Health Records

```
Track each child's vaccination schedule and health records. Belgian KDVs
are legally required to track vaccinations (Vaccinatieboekje) and are
expected to flag when boosters are due. This is also a parental trust signal.

What to build:
- vaccine_records table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    child_id        UUID REFERENCES children(id) NOT NULL,
    vaccine_name    TEXT NOT NULL,   -- 'DTP', 'MMR', 'Hep B', 'Meningococcal', etc.
    dose_number     INT,             -- 1, 2, 3, 4 (for multi-dose vaccines)
    administered_on DATE NOT NULL,
    next_due_date   DATE,            -- optional; set when schedule is known
    administered_by TEXT,            -- doctor / clinic name
    notes           TEXT,
    recorded_by     UUID REFERENCES users(id),
    created_at      TIMESTAMPTZ DEFAULT NOW()

- health_records table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    child_id        UUID REFERENCES children(id) NOT NULL,
    record_type     TEXT CHECK (record_type IN (
                      'allergy',        -- separate from the existing allergy field; detailed records
                      'chronic_condition',
                      'medication_standing', -- standing medication (not event-based)
                      'doctor_note',
                      'other'
                    )) NOT NULL,
    title           TEXT NOT NULL,
    description     TEXT NOT NULL,
    valid_from      DATE,
    valid_until     DATE,
    attachment_url  TEXT,   -- signed GCS URL for uploaded PDF/image
    recorded_by     UUID REFERENCES users(id),
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ

- Web admin (director) and caregiver app (read-only for caregivers):
  Child file → "Gezondheid" tab showing vaccine history and health records.
- Director alert dashboard: children with a next_due_date ≤ 30 days from
  today appear in a "Vaccinations due soon" block.
- Caregiver app quick-access: from the group view a caregiver can tap a
  child and see their allergy/health summary without navigating into the
  full child file (critical for daily care decisions).

Key constraints:
- Health records and vaccine records are medical data — they are subject
  to stricter GDPR rules. Never include them in bulk exports or email
  summaries unless the director explicitly selects them.
- Attachments follow the GCS signed-URL convention (no public blob URLs).
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A vaccine next_due_date passes without the vaccination being recorded.
  Keep showing the alert — do not auto-dismiss.
- A child transfers to a different KDV (contract ends). Health records
  stay in the system for the legal retention period; they are not
  transferred automatically.

Out of scope:
- Automated parent reminder for upcoming vaccines (Phase 2 push notification).
- Integration with Belgian vaccinatienet.be (Phase 3).
```

**Shipped 2026-07-13** — `specs/013c-vaccine-health-records/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, all tasks incl. a 1-task convergence pass, 34 new
backend tests + 8 new web tests + 7 new mobile tests, 563/563 backend + 84/84 web + 140/140
mobile passing). Scope deltas worth knowing before starting a dependent feature:

- **A genuinely new, no-precedent scope question was found only during planning research, not
  raised by this feature's own BACKLOG prompt**: an unused `vaccination_records` table/entity
  from feature 006 (nearly a subset of this feature's schema, zero client callers) sat alongside
  the new `vaccine_records` this feature needed to build. Per the standing rule on pausing for
  such questions, this was confirmed directly with the user before proceeding — migrated (data
  backfill inside the EF migration, mirroring feature 009a's `measurement`→`growth_check`
  precedent) and removed, rather than left as orphaned dead code beside a second, competing
  table.
- **Director web's first per-child detail screen** (`/children/[id]`, replacing the
  `NotYetAvailable` placeholder feature 007a left in place) and **first dashboard-shaped screen**
  (`/dashboard`, now the default post-login landing route, replacing the old redirect to
  `/staff`) both shipped as part of this feature — neither existed anywhere in this codebase
  before, and this feature's own "Vaccinations due soon" block needed both to be reachable at
  all. A future feature adding more per-child tabs or dashboard widgets extends these two screens
  rather than building new ones.
- Caregiver tablet's existing feature-008 medical quick-access sheet is extended (not replaced)
  with active health records and due-soon/overdue vaccine flags, offline-cached via a new
  `mobile/services/healthSummary.ts` following the group view's existing cache-fallback pattern.
- `/speckit-converge` found one real gap after implementation: `healthSummary.ts`'s actual
  fetch-then-cache-fallback logic had zero test coverage (the screen-level test only mocked the
  whole service function) — fixed with a dedicated unit test mirroring the group view's own
  cache-fallback test, the only precedent for this pattern in the mobile test suite.
- `IHealthAttachmentStorage`/`GcsHealthAttachmentStorage` is a new signed-URL storage port,
  deliberately separate from feature 005/006's `IProfilePhotoStorage` (which hardcodes a `.jpg`
  object path, wrong for a PDF/JPEG/PNG attachment) — reuses the same GCS bucket via a distinct
  path prefix, no new bucket or Terraform change needed.

---

### 013g — Vaccine Catalog & Attachments

**Added 2026-07-13, from a live product discussion about 013c's vaccine records.** 013c shipped
`VaccineRecord.vaccineName` as pure free text with a manually-typed `nextDueDate` and no
attachment field — reliable only if whoever enters a record types the name and due date
correctly every time. This feature adds a maintainable preset catalog (seeded from the Vlaamse
Departement Zorg basisvaccinatieschema) and lets a director attach a scan/photo of the
vaccination booklet as a fallback source of truth, since parents don't reliably keep the KDV's
own record updated.

```
Add a shared, admin-maintained vaccine catalog to back feature 013c's
vaccineName field, plus attachment support on VaccineRecord itself so a
director can attach a photo/scan of the child's paper vaccination booklet
(vaccinatieboekje) as a fallback when a KDV's own record has fallen behind.

Context: 013c's VaccineRecordForm (web/components/health/VaccineRecordForm.tsx)
is a plain free-text input for vaccineName and a manually-typed nextDueDate —
there is no catalog anywhere, so the "due soon" reminder feature depends
entirely on someone typing both fields correctly. VaccineRecord also has no
attachment field (unlike HealthRecord, which already has AttachmentObjectPath
+ IHealthAttachmentStorage/GcsHealthAttachmentStorage from 013c) — parents are
the actual source of truth for a child's vaccination history and won't
reliably keep the KDV's typed record current, so being able to attach a photo
of the real booklet is a practical fallback, not just a nice-to-have.

What to build:
- New shared reference table `vaccine_types` (NOT tenant-scoped — this is
  national schedule data, not something each KDV maintains separately):
    id, name TEXT, category TEXT (nullable — e.g. "basisvaccinatieschema" vs
    "aanbevolen, niet gratis"), sort_order INT, is_active BOOL (soft-delete —
    never hard-delete, existing VaccineRecords may still reference a retired
    entry), created_at, updated_at.
  Seed data: the Vlaamse Departement Zorg basisvaccinatieschema for children
  0–14 (DTP-Polio-Hib-HepB combination, Pneumococcal, MMR, Meningococcal ACWY,
  HPV) plus the "recommended but not free" set relevant to daycare-age
  children (RSV immunisation for infants, Meningococcal B, Hepatitis A,
  chickenpox). Adult-only entries (flu, shingles, etc.) are out of scope —
  KDV children are the only subjects of this record.
- `VaccineRecord` gains a nullable `VaccineTypeId` FK to `vaccine_types`.
  `vaccineName` stays the source of truth and stays free text: picking a
  catalog entry auto-fills it; typing something not in the list just leaves
  `VaccineTypeId` null. This keeps historical records valid even if a catalog
  entry is later retired or renamed.
- `VaccineRecord` gains `AttachmentObjectPath` (nullable), reusing the
  existing `IHealthAttachmentStorage`/`GcsHealthAttachmentStorage` port from
  013c via a distinct object-path prefix — same signed-URL-on-read pattern,
  no new bucket or Terraform change.
- Web: `VaccineRecordForm.tsx`'s vaccineName `Input` becomes a combobox
  (catalog entries, grouped by category, plus "add custom" free text). Add
  an attachment upload control, reusing whatever component/flow
  `HealthRecordForm.tsx` already built for its own attachment upload
  (contracts/vaccine-health-records-api.md's existing pattern: upload is a
  separate, subsequent action that never blocks saving the record itself).
- New screen to manage the shared `vaccine_types` catalog (create, rename,
  reorder, deactivate) — confirm at plan time whether this is scoped to a
  platform-level super-admin (since the catalog is shared across all
  tenants) or exposed to any director, given no super-admin role currently
  exists in this codebase (003/005's role model is Director/Staff/Parent).
  This may require defining that role for the first time, or scoping
  catalog edits to Director with the explicit understanding that one
  tenant's edits affect every tenant. Flag this as a real open question for
  /speckit-clarify, not an assumption to make silently.

Key constraints:
- No new bucket/Terraform change — reuse 013c's existing GCS health-
  attachment storage port with a new path prefix.
- `vaccine_types` is never tenant-scoped — do not put it in the tenant
  schema alongside `vaccine_records`.
- All user-facing strings use i18n keys (NL/FR/EN).
- Catalog entries are soft-deleted only; a `VaccineRecord.VaccineTypeId`
  pointing at a deactivated entry must still render correctly (show the
  stored `vaccineName`, not fail or blank out).

Edge cases:
- A catalog entry is renamed after several VaccineRecords already reference
  it. Existing records keep their originally-typed `vaccineName` (denormalised
  at creation time) — a rename does not retroactively change past records,
  only what a director sees when picking that entry going forward.
- A director picks a catalog entry, then edits the auto-filled vaccineName
  text before saving. The FK and the free text can diverge — confirm at plan
  time whether this is allowed (record shows the edited text but keeps
  VaccineTypeId) or whether editing the text clears the FK.
- Attachment upload fails after the record itself was already saved. Per
  013c's established pattern, this must not roll back or block the record —
  the record exists without an attachment, retryable separately.

Out of scope:
- Auto-calculating `nextDueDate` from the catalog's standard dosing interval
  (e.g. "DTP booster due 10 years after this dose") — this feature only adds
  the picker and the attachment; automatic due-date computation is a
  separate, future enhancement if the catalog proves reliable in practice.
- OCR/data-extraction from an uploaded vaccination-booklet photo — the
  attachment is a human-readable fallback only, not a structured data
  source.
- Adult (staff) vaccination tracking — VaccineRecord is child-only.
```

---

### 013h — Platform-Admin Vaccine Catalog

**Added 2026-07-13, deferred out of feature 013g's own scope.** Feature 013g's spec.md
Assumptions section explicitly commits to logging this as a separate backlog item: the shared,
platform-wide `vaccine_types` catalog it introduced has no director-facing (or any in-app)
management UI — it's seeded via an EF Core migration and, in production, would only be
maintained today by the platform operator making direct data changes. This item is that
deferred management surface, if/when it's actually needed.

```
Add a platform-admin role and management UI for the shared vaccine_types
catalog feature 013g introduced (create, rename, reorder, deactivate entries).
This is the first platform-level (cross-tenant) admin capability in this
codebase — every existing role (Director/Staff/Parent, feature 003/005) is
scoped to a single tenant; a platform-admin role is a genuinely new kind of
actor that sits above tenant boundaries entirely.

Context: feature 013g deliberately shipped the catalog as read-only from
every tenant-facing endpoint (spec.md FR-009) precisely to avoid inventing
this role speculatively before there was a real, current need for it. The
catalog is currently maintained by the platform operator (the developer of
this application) via direct data changes to the `public.vaccine_types`
table — workable at today's scale, but not something a future non-technical
platform operator (if the business ever brings one on) could do themselves.

Open questions this feature must resolve before implementation (flag for
/speckit-clarify, do not assume silently):
- Where does a platform-admin authenticate? This is the first surface with
  no natural tenant to scope a JWT to — a new auth path, a super-admin flag
  on an existing account, or something else entirely needs deciding.
- Is this a new screen in the existing Next.js web app (behind a
  platform-admin-only route), or a genuinely separate internal tool? Given
  Constitution Principle VII (monolith-first simplicity), default to "same
  app, new gated route" unless a concrete reason argues otherwise.
- Does deactivating a catalog entry need an audit trail (who deactivated
  it, when), given feature 013g's existing precedent of never hard-deleting
  catalog rows for exactly this kind of accountability reason?

Out of scope: any change to feature 013g's existing tenant-facing read
behavior (GET /api/vaccine-types stays exactly as-is); OCR or auto-population
of the catalog from an external vaccine-schedule data source.
```

---

### 006a — Child Profile UI (Full Child File)

```
Build the general-details tab of the child file. Feature 006 modeled a
child's core profile and medical info end to end in the domain/API layer,
but no screen anywhere (web or mobile) lets a director create or edit a
child record — `/children` (web) only has a list and, since 013c, a
detail screen with a single "Gezondheid" tab. There is no create form and
no general-profile edit form; every `Child` row in this system today
exists only because a test, seed script, or the waiting-list-conversion
flow (012a) called the API directly.

What this solves: directors currently cannot see or maintain a child's
core profile through the UI at all, and one specific gap prompted this
item — 006 only ever modeled a GP (huisarts) contact (`GpName`/`GpPhone`),
not a pediatrician (kinderarts/pédiatre). In Belgium a young child's
primary medical contact is commonly a pediatrician rather than a general
practitioner, and the two are frequently different people — the current
schema has no field for the former at all.

What to build:
- New "Profiel" tab on the existing child detail screen
  (`web/app/(app)/children/[id]/page.tsx`), added alongside the existing
  "Gezondheid" tab from 013c, not replacing it (013c's own shipped-notes
  flag this screen as the one future per-child-tab work should extend).
- A create-child form (web) — there is currently no UI path to create a
  `Child` at all outside of the 012a waiting-list-conversion flow, which
  itself leaves every medical field null.
- Add a `PediatricianName`/`PediatricianPhone` pair on `Child`, nullable
  and optional like the existing `GpName`/`GpPhone` (same FR-003 precedent:
  medical info never blocks child creation or save). Requires an EF Core
  migration — generate the SQL script and run it manually, no auto-migrate
  in production.
- Director-editable fields: first/last name, DOB, gender, nationality,
  profile photo, allergies + severity, medical conditions, dietary
  restrictions, GP name/phone, pediatrician name/phone, health insurance
  number, kindcode.
- Mobile (caregiver, read-only): extend the existing allergy/medical/
  dietary summary on the child screen to also show GP and pediatrician
  contact — caregivers need this on-site during a medical incident, not
  just directors in the web admin.
- All user-facing strings use i18n keys (NL/FR/EN) — "Pediater" or
  "Kinderarts" (NL), "Pédiatre" (FR), "Pediatrician" (EN), kept visually
  and linguistically distinct from "Huisarts"/"General practitioner" (GP).

Key constraints:
- Medical fields remain nullable and never block child creation or save
  (FR-003 precedent from 006).
- Pediatrician name/phone are ordinary contact fields, not GDPR
  special-category data by themselves, but sit on the same entity as
  allergy/medical fields and should inherit that section's existing access
  scoping rather than getting a separate rule.

Edge cases:
- A child has a pediatrician but no GP, or vice versa — both fields stay
  independently optional, no "at least one" validation.
- A family switches pediatricians. Overwrite in place; no history is kept
  (same as the existing GP field today).

Out of scope:
- Contacts tab, contracts tab, groups tab on the same child file screen —
  each is separate follow-on work once prioritised, per 013c's precedent
  of extending this screen one tab at a time.
- Pediatrician appointment scheduling or reminders (no such feature exists
  for the GP field either).
```

**Shipped 2026-07-13** — `specs/006a-child-profile-ui/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge, 38/38 tasks, PR #24 squash-merged after green CI —
567/567 backend + 92/92 web + 144/144 mobile passing). Scope deltas worth knowing:
- `/speckit-converge` caught a real gap after implementation: profile photo was explicitly part
  of FR-002/FR-004 and this feature's own field list above, but got dropped from `tasks.md`'s
  task enumeration and never got built — fixed, not deferred.
- Adding a migration after feature 013c's surfaced a latent fragility in two existing test
  helpers (`TenantMigrationRolloutTests`, `LegacyVaccinationMigrationTests`) that assumed their
  target migration would always remain the assembly's latest — both extended to revert this
  migration too, since `TenantDbContext.MigrateAsync()` computes "pending" from the schema's last
  *applied* migration and would otherwise silently no-op. Same class of fix 012a's shipped-note
  already logged for a different pair of tests — worth checking both helpers on any future
  migration-adding feature.
- Web lockfile (`package-lock.json`) needed a regenerate for clean `npm ci` under Node 20 —
  same CI-only class of issue 007a's and 010's shipped-notes already describe.

---

### 013d — Meal List (Maaltijdenlijst)

```
Generate a daily meal list for the kitchen — who eats what today,
with allergen flags and per-child meal texture visible at a glance.

Context: Belgian KDV kitchens prepare meals for 10–40 children daily.
Infants eat puréed food; toddlers eat mixed; older children eat pieces.
D-care has this feature; it is a daily operational necessity.

What to build:
- Add child_meal_preferences table (tenant schema):
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    child_id          UUID REFERENCES children(id) NOT NULL UNIQUE,
    texture           TEXT CHECK (texture IN (
                        'pureed',     -- fully blended (babies < ~8m)
                        'mixed',      -- soft lumps (8–12m)
                        'pieces',     -- small soft pieces (12m+)
                        'normal'      -- regular family food (toddlers)
                      )) DEFAULT 'normal',
    dietary_type      TEXT[],        -- ['halal','kosher','vegetarian','vegan','gluten_free']
    portion_size      TEXT CHECK (portion_size IN ('small','normal','large')) DEFAULT 'normal',
    additional_notes  TEXT,          -- free-text for anything not covered above
    updated_at        TIMESTAMPTZ,
    updated_by        UUID REFERENCES users(id)

- API: GET /locations/{id}/meal-list?date= — returns day's present children
  with their meal_preferences and allergen flags from 013c health_records,
  grouped by group/section.
- Web admin: "Maaltijdenlijst" page with Print button (CSS print stylesheet;
  no PDF needed).
- Caregiver app: accessible from group home screen, shows current group only.
- Allergen severity: RED (anaphylactic/epipen), AMBER (intolerance),
  GREY (none). Use icons + colour for B&W print compatibility.
- Director can edit a child's meal preferences from the child profile.

Key constraints:
- Never show absent children on the meal list.
- Meal list shows dietary data from multiple children — operational document
  only; never email it to parents.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Child not yet checked in but expected: "Inclusief verwacht" toggle shows
  them in a separate "Verwacht" section.
- Standing medication (013c): show a pill icon — kitchen prepares the
  medication-time reminder for the caregiver.
- No preferences set: show child with "Geen voorkeur" — never hide them.

Out of scope:
- Monthly menu planning (feature 013e).
- Kitchen supplier integration (Phase 3).
- Nutritional tracking (out of scope entirely).
```

**Shipped 2026-07-13** — `specs/013d-meal-list/` (spec → clarify → plan → tasks → checklist →
analyze → implement → converge, 53/53 tasks, 15 new backend tests + 7 new web tests + 10 new
mobile tests, 582/582 backend + 99/99 web + 154/154 mobile passing). Daily meal-list aggregation
(`child_meal_preferences` table, one `GET /locations/{id}/meal-list` read model, a director-web
printable page, a caregiver-tablet own-group view). Two premises in the BACKLOG prompt were
wrong and corrected during specification/implementation, not assumed away: allergen severity is
sourced from `Child.AllergySeverity` (006), not `HealthRecord` (013c) — `HealthRecord` has no
severity field at all; and "present" needed an explicit `CheckOutAt == null` check alongside
`Status == Present`, since `CheckOutCommand` (010) never changes `Status` away from `Present` —
a naive `Status`-only filter would have kept showing a child on the lunch list hours after they'd
already been picked up, caught by writing this feature's own tests rather than by inspection.
`/speckit-checklist`'s safety-focused pass found six genuine spec gaps (inclusive date-boundary
handling for standing medication, single-icon-not-count for multiple medication records, and
three others) — all fixed in spec.md directly, same standing rule as every prior feature.
`/speckit-analyze` found two coverage gaps (FR-008's tightened boundary rule and FR-015's
cross-platform parity requirement lacking dedicated tasks) — both fixed by expanding existing
test tasks rather than left as debt. A second, separate migration-rollout test
(`LegacyVaccinationMigrationTests`, not the usual `TenantMigrationRolloutTests`) needed the same
schema-revert extension every migration-adding feature since 003 has required — its own code
comment predicted this exact gap for "any future migration," and this was that migration; worth
checking *every* test that reverts a tenant schema to an earlier migration state, not just the
one file prior shipped-notes name. Also added a small additive `GET
/api/children/{childId}/meal-preferences` endpoint beyond the original plan — the director-facing
edit form needs current values to pre-fill since the `PUT` is a partial-upsert, the same
"additive gap found while wiring UI" pattern 007a/009 already established.

---

### 013e — Monthly Menu

**Scope note added 2026-07-14, mid-implementation.** Two questions came up while resuming this
feature that the spec below doesn't address: (1) how a director bulk-populates a month's menu
instead of typing each day by hand, (2) whether a location needs an alternative menu for a
dietary restriction (vegetarian/halal/allergen-free) distinct from its base menu. Confirmed
directly with the product owner rather than guessed, per the standing rule for no-precedent scope
questions: both are out of this feature's MVP. 013e ships with the manual day-grid only and a
single base menu per location/month, exactly as specced below. The two questions are logged as
follow-ups — `013i-monthly-menu-csv-import` and `013j-monthly-menu-variants`.

```
Let the director publish a monthly meal menu visible to parents in the
parent app. Parents can see what their child will eat and request changes
to their child's meal preferences. Each child's preferences are
personalised (texture, dietary type, allergies from 013c).

Context: parents increasingly care about what their child eats at the KDV —
allergy awareness, halal/kosher requirements, and developmental feeding
stages all drive this. D-care does not offer this; it is a differentiator.

What to build:
- monthly_menus table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id     UUID REFERENCES locations(id) NOT NULL,
    year            INT NOT NULL,
    month           INT NOT NULL CHECK (month BETWEEN 1 AND 12),
    published_at    TIMESTAMPTZ,    -- null = draft, not visible to parents
    created_by      UUID REFERENCES users(id),
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (location_id, year, month)

- monthly_menu_days table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    menu_id         UUID REFERENCES monthly_menus(id) NOT NULL,
    menu_date       DATE NOT NULL,
    soup            TEXT,
    main_course     TEXT,
    dessert         TEXT,
    notes           TEXT,           -- e.g. "geen warme maaltijd deze dag"
    UNIQUE (menu_id, menu_date)

- Web admin: "Menu" section under the location. Director creates/edits
  monthly menus day by day (simple text fields per course). Save as draft,
  then Publish — published menus become visible to parents in the parent app.
  Director can un-publish to make corrections, then re-publish.
- Parent app: "Menu" tab showing the current month's menu. For each day:
  soup / main / dessert. Closure days (011) are shown but greyed out.
  If the location has not published a menu for this month, show a
  "Menu nog niet beschikbaar" placeholder.
- Per-child personalisation indicator: next to the menu, the parent sees
  their child's current meal preferences (texture, dietary type) from
  child_meal_preferences (013d). A "Voorkeur aanpassen" button lets the
  parent request a change.
- Preference change request: a simple form (select new texture, select
  dietary types, free-text note). Creates a preference_change_requests record:
    id              UUID PRIMARY KEY,
    child_id        UUID REFERENCES children(id),
    requested_by    UUID REFERENCES users(id),
    new_texture     TEXT,
    new_dietary     TEXT[],
    notes           TEXT,
    status          TEXT DEFAULT 'pending',  -- pending / approved / rejected
    decided_by      UUID REFERENCES users(id),
    decided_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ DEFAULT NOW()
  Director approves in web admin → child_meal_preferences updated.
  Director rejects → parent notified via push with optional reason.

Key constraints:
- Only published menus are visible to parents. Draft menus are director-only.
- The menu is per-location, not per-group (one kitchen, one menu).
  Child allergies/preferences are shown alongside the shared menu.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Director publishes a menu with a typo and needs to correct it mid-month.
  Un-publish, edit, re-publish. Parents see the correction on next app open.
- A day has no menu (KDV on closure or parents bring their own lunch).
  Leave all fields blank — shown as "—" to parents.
- Parent requests a preference change that conflicts with a health record
  in 013c (e.g. requesting "normal" texture for a child with a swallowing
  note). Director sees the health record alongside the request when deciding.

Out of scope:
- Nutritional information per dish.
- Kitchen supplier or recipe management.
- Allergen matrix per dish (listed on the shared menu) — Phase 3.
```

**Shipped 2026-07-14** — `specs/013e-monthly-menu/` (spec → clarify → plan → tasks → checklist
→ analyze → implement → converge → PR #30, squash-merged after green CI — 655/655 backend +
126/126 web + 70/70 parent-mobile tests). Director-web "Menu" section (day-grid authoring,
Save draft / Publish / Un-publish as distinct labeled+iconed actions) and a new parent-mobile
"Menu" tab (every active-contract location, closure days greyed out and labeled, a per-child
preference indicator, and a "Voorkeur aanpassen" request form). Meal-preference-change requests
reuse 013a's exact authorization primitive (`ICurrentParentContactResolver` + `ChildContacts`)
and exact notification pattern (`IExpoPushSender` + in-app `Notification`); approval writes
through the existing `UpsertMealPreferenceCommand` (013d) rather than a second write path. Two
real EF Core bugs were found and fixed by the new tests, not by inspection: an unflushed
delete+insert unique-constraint collision on menu edits (EF doesn't guarantee delete-before-
insert ordering within one `SaveChanges` batch), and duplicate day rows from double relationship-
fixup when a pre-populated `Guid` PK entity was added via both the DbSet and a navigation
collection. A third real bug — `MealPreferenceRequestNotificationService` never registered in
`Program.cs`'s DI container — surfaced as a 500 on every approve/reject call until the US4 tests
caught it. `/speckit-converge` found one genuine spec gap: the parent-mobile preference
indicator showed only texture, never the dietary tags FR-010 explicitly requires — fixed with a
regression test, same standing rule as every prior feature. Confirms the recurring
`TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern (012a,
013c, 006a, 013d, 013g, 013h) yet again. This session resumed mid-flight: a prior session had
already committed Phase 2 (schema/entities/migration) and left US1's application-layer commands
written but uncommitted with zero tests; this pass picked up from there rather than re-running
specify/plan/tasks. Mid-implementation, this feature's own resumption raised two genuinely new,
no-precedent scope questions (bulk CSV import for menu authoring; an optional per-dietary-
restriction variant menu) — paused and confirmed directly with the product owner before
continuing, per the standing rule: both are out of this feature's MVP, logged as follow-ups
(`013i-monthly-menu-csv-import`, `013j-monthly-menu-variants`) rather than expanding scope
mid-flight. `parent-mobile/services/menu.ts` deviates from tasks.md's literal "mirror
`healthSummary.ts`" instruction: that precedent lives in the caregiver app (`mobile/`) on top of
feature 008's SQLite offline-sync store, which parent-mobile deliberately has none of (spec.md
Assumptions — parents are expected to have network access) and no persistence library exists in
that app at all — implemented as an in-memory per-month cache instead, same fetch-then-cache-
fallback behavior, no new dependency.

---

### 013i — Monthly Menu CSV Import

**Prompt block synthesized 2026-07-14** — unlike every other backlog entry, 013i never got a
dedicated spec-kit prompt written when it was logged as a follow-up mid-013e (see 013e's
shipped-notes above); only the one-line BACKLOG row existed. Grounded the prompt below directly
in 013e's shipped code (`UpsertMonthlyMenuCommand`'s whole-month-replace write model,
`MonthlyMenuDayGrid`'s per-calendar-day in-memory state) rather than re-deciding the write
architecture — this is client-side-only, no new backend endpoint.

```
Let the director bulk-populate a month's menu via CSV upload instead of typing
each day by hand in the existing day-grid (013e). This is a director-web-only
authoring convenience — it does not add any new backend endpoint or change how
parents see the menu.

Context: deferred out of 013e's MVP per an explicit product-owner decision (see
013e's shipped-notes) to keep that feature's launch scope to the manual grid
only. Directors publishing a whole month's menu one day-row at a time is slow;
many already keep the month's menu in a spreadsheet.

What to build:
- On the existing "Menu" section (web/app/(app)/menu/page.tsx +
  MonthlyMenuDayGrid.tsx), add an "Import CSV" action alongside Save/Publish.
- CSV format: one row per day, columns `date` (YYYY-MM-DD), `soup`,
  `main_course`, `dessert`, `notes` — matching MonthlyMenuDaySave's fields.
  A "Download template" link provides a header-only (or header + one example
  row) CSV in the expected format.
- Parse client-side (no new backend endpoint — this reuses the exact same
  PUT /api/locations/{locationId}/monthly-menus/{year}/{month} write path
  MonthlyMenuDayGrid's existing Save button already calls). Parsing happens
  entirely in the browser before Save is pressed.
- On upload, validate every row: `date` must parse as a real calendar date
  within the grid's currently-selected year/month; no duplicate dates within
  the file; unknown/extra columns are ignored. Row-level errors are collected,
  not thrown on the first bad row.
- Preview before applying: show the parsed rows in a table (reusing the day
  grid's own row layout where practical) with invalid rows visually flagged
  and excluded from the merge; a summary line states how many rows will be
  applied vs. skipped. The director explicitly confirms before the parsed
  values are merged into the grid's in-memory day state.
- Merge behavior: imported rows overwrite only the matching dates in the
  grid's current in-memory state (the grid already holds one row per calendar
  day of the month); dates not present in the CSV keep whatever value they
  already had in the grid (typed earlier, or blank). The merge does not save
  automatically — the director still presses the existing Save button
  afterward, so a CSV import behaves exactly like manually retyping those
  rows and can be reviewed/edited further before committing.
- Import is available regardless of draft/published state, same as manual
  grid edits today (013e already requires no separate "must be draft" gate
  for editing — Save works on a published menu too, it just doesn't
  auto-(re)publish).

Key constraints:
- No new backend endpoint, no new write path — CSV parsing and validation are
  entirely client-side; the eventual write is the existing whole-month PUT.
- All parsing/validation error text uses i18n keys (NL/FR/EN), matching every
  other user-facing string in this feature area.
- Field length limits match UpsertMonthlyMenuCommandValidator's existing
  500-character max per field — a CSV cell over that length is a validation
  error on that row, not a silent truncation.

Edge cases:
- CSV has a row for a closure day (011) — accepted like any manual entry;
  closure days are still shown greyed out to parents regardless of content.
- CSV is empty, has no rows, or every row fails validation — show a clear
  error, nothing is merged into the grid.
- CSV has rows for a different month than what's currently selected in the
  grid — those rows are flagged invalid (date out of range), not silently
  applied to the wrong day.
- Director re-uploads a corrected CSV after fixing errors — same flow, no
  special-case incremental merge logic; it's the same overwrite-by-date
  behavior as the first upload.
- Malformed CSV (wrong delimiter, missing header, binary file) — a single
  top-level parse error, no partial row processing.

Out of scope:
- CSV export/download of an existing menu (only the empty template is
  downloadable).
- Any change to the parent-facing menu display, publish/unpublish flow, or
  preference-request flow — this is a director authoring convenience only.
- 013j's per-dietary-restriction variant menu (separate backlog item).
```

**Shipped 2026-07-14** — `specs/013i-monthly-menu-csv-import/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge → PR #31, squash-merged after green CI — 655/655
backend + 153/153 web tests). Client-side-only director-web CSV bulk-import: `web/lib/menu/
csvImport.ts` parses (papaparse), validates, and previews a CSV entirely in the browser, then
merges valid rows into `MonthlyMenuDayGrid`'s existing in-memory state — the existing Save button
(013e's whole-month-replace write) is the only write path, no new backend endpoint or migration.
`/speckit-checklist`'s CSV-import-focused pass (16 findings, all fixed) found the highest-impact
gap: the original synthesized spec required an explicit confirm step but never required the
preview to show *what existing content an import would overwrite* — fixed via new FR-024/SC-005,
alongside a pinned `YYYY-MM-DD` date format, BOM stripping, case/whitespace-tolerant headers, and
an explicit invalid-reason precedence order. Testing then surfaced a real design gap the checklist
missed: a ragged CSV row's `date` cell can still parse successfully when the missing/extra columns
are trailing ones, so column-count mismatches needed their own explicit `malformed_row` check
(highest precedence) rather than being assumed to always corrupt the date. `/speckit-converge`
found two small gaps post-implementation (FR-004's extra-column-ignored behavior was untested; an
unused `MenuCsvFileLevelError` type was dead code) — both fixed. CI hit a new variant of this
codebase's recurring `web/package-lock.json`-out-of-sync-under-Node-20 issue (007a, 010, 013b,
006a): unlike those, regenerating the lockfile — even inside a matching `node:20` Docker
container and verifying `npm ci` passed there — didn't hold, because `@swc/helpers` was only ever
an *optional* peer dependency of `next-intl`'s bundled `@swc/core` (never pinned to a concrete
version), and npm's resolution of whether to satisfy it apparently isn't deterministic across npm
patch versions even at identical Node versions. Fixed by pinning `@swc/helpers` as an explicit
devDependency instead of relying on optional-peer auto-resolution. Also caught mid-fix: a plain
`npm install` under a newer local Node silently rewrote an already-Docker-verified lockfile again
before it was committed — twice, in this session — so any future lockfile fix of this class
should use `npm ci` (never `npm install`) for every local step after the verified regeneration,
worth remembering alongside the npm-ci-lockfile-drift pattern itself. This backlog item never had
its own dedicated Spec Kit Inputs prompt block (only the one-line row + a deferral note from
013e's shipped-notes) — synthesized one directly from 013e's shipped code before starting the
pipeline; see this section's own opening note for how that gap was resolved.

---

### 013j — Monthly Menu Variants

**Prompt block synthesized 2026-07-14**, same situation as 013i above — no dedicated prompt ever
existed, only the one-line BACKLOG row and 013e's deferral note. Unlike 013i (pure frontend
plumbing), this one raised a genuinely new, no-precedent product-shape question with no clear
default: `MealPreference.DietaryType` (013d) is a **list** — a child can be Halal *and*
GlutenFree at once — and the row's own example ("allergen-free") isn't actually one of 013d's 5
enum values (allergies are a separate, severity-based concept, 013c, with no enum precedent).
Confirmed directly with the product owner before specifying, per the standing rule for
no-precedent scope questions:

- **Variant dimension**: the existing `DietaryType` enum only (Halal/Kosher/Vegetarian/Vegan/
  GlutenFree). No new allergen-based variant concept — allergy info stays shown alongside the
  menu exactly as it already is today (013e), not as a selectable variant.
- **Multi-type resolution**: a director-configured priority order among a location's enabled
  variants. A child with multiple qualifying `DietaryType`s sees the highest-priority variant
  that has a published menu that month; falls back to the base menu if none of their types has
  one.
- **Authoring model**: a full parallel day-grid per variant — the same `MonthlyMenu`/
  `MonthlyMenuDay` shape 013e already built, just tagged with an optional `DietaryType`. Not a
  diff/override model (that would need new merge-resolution logic nothing else in this codebase
  has).

```
Let a director optionally publish an alternative monthly menu per dietary
restriction (vegetarian, halal, vegan, kosher, gluten-free — the existing
013d `DietaryType` enum), separate from the location's base menu, so a
child with a matching dietary preference sees the right variant instead of
the base menu.

Context: raised mid-implementation of 013e, deliberately deferred to keep
that feature's MVP to a single base menu per location/month. See this
section's own note above for the three scope decisions already confirmed
with the product owner (variant dimension, multi-type resolution, authoring
model) — implement exactly those, do not re-litigate them.

What to build:
- Extend `MonthlyMenu` (tenant schema) with a nullable `Variant DietaryType?`
  column. `null` = the existing base menu, unchanged. A non-null value is a
  variant menu for that `DietaryType`. Change the existing unique constraint
  from `(LocationId, Year, Month)` to `(LocationId, Year, Month, Variant)` so
  a location can hold one base menu plus one menu per variant, per month.
  `MonthlyMenuDay` and its whole-month-replace write model (013e) are
  otherwise unchanged — a variant menu is just another `MonthlyMenu` row
  with its own set of days.
- Add `MenuVariantPriorityOrder List<DietaryType>` to `Location` (ordered,
  Postgres `text[]`, mirrors `MealPreference.DietaryType`'s existing
  `List<DietaryType>` storage convention). Empty list (the default) means no
  variants are enabled for that location — existing locations are
  unaffected until a director explicitly configures this. The order in the
  list *is* the priority order for resolution.
- Director-web location settings: extend the existing per-location settings
  area (same "Algemeen"/tabs pattern 007a's location detail page and 013f's
  `ReservationSettingsForm` already established) with a control to select
  which `DietaryType`s are enabled as variants for this location and drag/
  reorder their priority.
- Director-web Menu section: add a variant selector (tabs or a dropdown)
  alongside the existing location/month selectors on `/menu`. Selecting a
  variant loads/edits that variant's `MonthlyMenu` through the exact same
  `MonthlyMenuDayGrid` component and CSV import (013i) already built for the
  base menu — both are variant-agnostic once the endpoint is parameterized.
  Only variants enabled in that location's `MenuVariantPriorityOrder` are
  selectable.
- Backend read/write endpoints: extend the existing
  `GET/PUT/POST(publish)/POST(unpublish) /api/locations/{locationId}/
  monthly-menus/{year}/{month}` family with an optional `variant` query
  parameter (absent = base menu, present = that `DietaryType`'s variant).
  Reject a `variant` value not enabled in the location's
  `MenuVariantPriorityOrder`.
- Parent-facing resolution (`GetParentMonthlyMenuQuery`): today this
  returns one entry **per location** (shared across every child a parent
  has enrolled there) — has no child dimension at all. This must become
  one entry **per (location, child)** pair, since variant resolution is
  inherently per-child: for each of the parent's children with an active
  contract at a location, read that child's `MealPreference.DietaryType`
  list, walk the location's `MenuVariantPriorityOrder`, and resolve to the
  first variant in that order that (a) the child qualifies for and (b) has
  a *published* menu that month — falling back to the base (`Variant ==
  null`) menu if no match. `ParentMonthlyMenuEntry` needs a `ChildId`/
  `ChildName` added, and the parent-mobile Menu screen needs to render one
  section per child (not just per location) when a parent has more than
  one child at the same location.
- Parent-mobile: surface which variant (if any) is being shown, in plain
  language (e.g. "Vegetarisch menu voor Emma"), not the raw enum name.

Key constraints:
- The base menu (`Variant == null`) behaves exactly as 013e/013i already
  built it — zero behavior change for any location that never configures a
  variant.
- A director cannot publish a variant `DietaryType` that isn't in the
  location's `MenuVariantPriorityOrder` — configuring the variant as
  enabled is a prerequisite to authoring it, not the other way around.
- No merge/diff logic between a variant and the base menu — a variant menu
  is authored completely independently, per the confirmed authoring-model
  decision.

Edge cases:
- A location has variants enabled but a director never actually publishes
  one for a given month: every child falls back to the base menu, same as
  if variants didn't exist.
- A child qualifies for a variant, but that variant's menu for the month is
  still a draft (not published): treated as "no published variant" — falls
  back to the base menu (or the next-lower-priority variant that IS
  published), consistent with 013e's existing "only published menus are
  parent-visible" rule.
- A director removes a `DietaryType` from `MenuVariantPriorityOrder` after
  menus already exist for it: those `MonthlyMenu` rows are untouched (not
  deleted), simply no longer selectable/resolvable going forward — a
  director can re-enable the type later and the old menus are still there.
- A child has zero `DietaryType`s set (no meal preference recorded yet):
  always sees the base menu, same as today.

Out of scope:
- Allergen-based variants (see the variant-dimension decision above).
- A parent-facing variant picker/override — resolution is fully automatic
  from the child's recorded dietary preference, per the confirmed
  multi-type-resolution decision.
- Per-dish overrides within a variant (see the authoring-model decision).
```

**Shipped 2026-07-15** — `specs/013j-monthly-menu-variants/` (spec → clarify → plan → tasks →
checklist → analyze → implement → converge → PR #32, squash-merged after green CI — 677/677
backend + 157/157 web + 72/72 parent-mobile tests). `MonthlyMenu.Variant` and
`Location.MenuVariantPriorityOrder` are stored as plain `string`/`List<string>` at the EF/entity
level rather than `DietaryType?`/`List<DietaryType>` as originally planned — implementing the
enum-typed version exactly as planned crashed every read/write with a genuine Npgsql provider
bug: a nullable-enum scalar `HasConversion` on `MonthlyMenu.Variant` collided with the
pre-existing `List<DietaryType>` conversion already shipped on `MealPreference.DietaryType`
(013d), throwing `NpgsqlArrayConverter`'s `Nullable<T>`/`T` type-mismatch exception regardless of
which of the two colliding properties was touched first. Fixed by moving all `DietaryType`↔string
translation to the Application layer (`MonthlyMenuVariantHelper`), keeping EF itself unaware of
the enum — Postgres's NULL-is-never-equal-to-NULL semantics also ruled out a nullable `Variant`
column for the unique index, so a `"base"` sentinel string is used instead of `null`. Also
surfaced a second, unrelated latent bug in shared code while fixing the first: `TenantDbContext.
MigrateAsync`'s `ExecuteSqlRawAsync` call treats its SQL argument as a composite format string
even with zero parameters, so the new migration's Postgres `'{}'` empty-array-default literal
threw a `FormatException` — no prior migration had added a non-nullable array column with an
empty-array default through this code path before. Fixed by escaping braces before the call; this
protects every future migration through `migrate-tenants`, not just this one. `/speckit-converge`
found one real gap post-implementation: `GetParentMonthlyMenuVariantResolutionTests.cs` was
missing its own T039 regression test (query count must not scale with child count) — added via a
dedicated `QueryCountingWebAppFactory`/`DbCommandInterceptor` scoped to that one test file, which
confirmed at runtime (not just by code inspection) that resolving 3 children at one location
issues the identical SQL command count as resolving 1. Running the full suite before merge also
caught an unrelated, pre-existing flaky test in `LocationReservationSettingsTests.cs`: a hardcoded
`2026-07-13` reservation date had fallen into the past as real time advanced past it, tripping
`SubmitDayReservationCommand`'s "not more than 1 day in the past" validation — fixed in its own
separate commit, bumped to the same safely-future date `DayReservationEndpointsTests.cs` already
uses.

---

### 030 — Family Siblings

```
Support parents who have multiple children enrolled at the same KDV.
This affects the parent app (single login → multiple children),
invoicing (optional family bundling), day-reservation requests
(one action for multiple children), and child/contract records.

Context: it is common for Belgian KDVs to have siblings enrolled.
D-care handles this poorly (separate logins per child). We can do better
from the start.

What to build:
- family_memberships table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_user_id  UUID REFERENCES users(id) NOT NULL,
    child_id        UUID REFERENCES children(id) NOT NULL,
    relationship    TEXT CHECK (relationship IN (
                      'parent','guardian','foster_parent','other'
                    )) DEFAULT 'parent',
    is_primary      BOOLEAN DEFAULT TRUE,  -- primary contact for invoicing
    UNIQUE (parent_user_id, child_id)
  (This replaces or extends the existing children→parent link from 006.
  Audit the 006 data model and migrate if needed.)

- Parent app changes:
  - Child switcher: after login, if a parent has multiple children, show
    a "Mijn kinderen" home screen with one card per child (photo, name,
    group). Tap to enter a child's context (events, attendance, messages).
  - Day reservation (013a): when submitting a sick-day or absence request,
    parent can select "voor alle kinderen" if all siblings are also absent.
    This creates one reservation record per child in a single API call.
  - Daily report (013): summary view shows all children's events
    side-by-side or with a scroll; no need to switch apps.
  - Notifications: each push notification is child-specific but the parent
    receives all of them. Notification text always includes the child's name
    to avoid confusion.

- Invoicing (014) — sibling discount flag:
  - Add sibling_discount_pct NUMERIC(5,2) to the location settings (004).
    Default 0. If set (e.g. 10%), the second+ child's invoice lines get a
    discount line item automatically.
  - Also support "family invoice bundling": one PDF per family per month
    (all children's lines on one invoice) rather than separate invoices.
    Director configures per-location.

- Web admin:
  - Child profile: shows all linked parent accounts with their relationship.
  - Parent/contact management: when adding a contact to a child, show
    "Is this contact already registered for another child?" — link to the
    existing parent account rather than creating a duplicate.

Key constraints:
- A parent_user_id can be linked to multiple children (siblings) and a
  child_id can be linked to multiple parent_user_ids (co-parenting, shared
  custody). The table is a many-to-many junction.
- One parent_user_id per child must be marked is_primary = TRUE (invoicing
  recipient). Enforce this at the API level.
- Sibling discount applies only to children of the same parent account at
  the same location.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Twins: two children with the same parent, same group, same contracted days.
  Both appear in the child switcher. Day reservations can be submitted for
  both simultaneously.
- Separated parents, shared custody: child has two parent accounts; each
  sees the child's events. Only one is marked is_primary for invoicing.
  Push notifications go to both.
- One sibling leaves (contract ends) but the other stays. The parent account
  remains active; the child switcher shows only the active child (with a
  "Vorige kinderen" archive toggle).
- Family invoice with one sibling on a different payment schedule (different
  contract start date). Handle invoicing periods correctly per child; the
  combined invoice can have different line-item date ranges.

Out of scope:
- Custody agreement scheduling (which parent has the child on which day) —
  not relevant for KDV invoicing/attendance, only for parental communication.
- Co-parent separate invoicing (split invoice between two parents) — Phase 3.
```

---

### 027 — Staff App

```
Allow parents to check their child in and out by showing a QR code on
their phone, scanned by the caregiver tablet. The caregiver still physically
receives the child — no change to the handover ritual — but the attendance
tap is replaced by a scan, which saves time at peak drop-off (8:00–8:30am)
when multiple parents arrive at once.

When is this worth it? Belgian KDVs are small (typically 14–36 children),
so busy moments are real but brief. QR check-in is a quality-of-life
feature, not a safety-critical one. Phase 3, after core operations are solid.

What to build:
- QR code per parent–child pair: encode a signed JWT containing
  {child_id, parent_user_id, tenant_id}. Signed with a tenant-specific
  secret. Short TTL (5 minutes) to prevent replay attacks.
- Parent app: "Inklokken" screen showing a refreshing QR code (auto-refreshes
  every 4 minutes). If a parent has multiple children (feature 030), one
  QR per child with a child switcher.
- Caregiver app: "QR scannen" mode on the group home screen. Opens the
  device camera with a QR overlay. On successful scan: validates the JWT,
  resolves the child, performs the same check-in/out action as a manual tap
  (POST /attendance/check-in from feature 010).
- Feedback: success shows the child's name + photo + "Ingecheckt ✓" for
  3 seconds, then returns to scan mode.
- Both directions: first scan = check-in; second scan = check-out.

Key constraints:
- QR JWT must be signed. An unsigned QR could be forged.
- QR scan offline: JWT validation is local; attendance record queued in
  offline_queue (008) if no connectivity.
- No new attendance schema — new entry path into the existing flow only.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Parent scans for a child at the wrong location → reject with "Dit kind
  is niet ingeschreven op deze locatie."
- QR expires mid-scan → caregiver sees "Code verlopen" and parent app
  auto-refreshes on next open.
- Tablet camera fails → fallback to manual tap; QR mode is additive.

Out of scope:
- NFC / badge / Paxton door integration (Phase 4).
- QR sticker on child's bag (physical QR, not phone-based).
```

---

### 022 — ID-Verified Registration

```
Streamlined child and parent registration with a director "identity
verified" audit trail. Replaces the original eID card-reader approach:
most KDV children are babies/toddlers who don't have an eID chip, making
a card reader largely useless in this context.

What this solves: Opgroeien and GDPR require the KDV to have verified the
identity of each child and guardian. The director does this in person at
drop-in or enrolment. We just need to record that it happened and what was
shown — no hardware required.

What to build:
- Add to the child registration form (006) and contact form:
    id_verified_at      TIMESTAMPTZ,   -- when was identity verified
    id_verified_by      UUID REFERENCES users(id),  -- which director
    id_document_type    TEXT CHECK (id_document_type IN (
                          'birth_certificate',  -- most common for babies
                          'kids_id',            -- Belgian Kids-ID card
                          'eid',                -- Belgian eID (12+ years)
                          'passport',
                          'other'
                        )),
    id_document_note    TEXT           -- optional free note (e.g. "seen doc nr X")

- In the web admin child file and contact view: a "Identiteit bevestigen"
  section. Director selects document type, optionally adds a note, and clicks
  "Bevestigen." Fields are write-once via the UI (editable only by org owner
  to prevent retroactive tampering).
- National Register Number (NRN / rijksregisternummer): optional field on
  the child record, encrypted at rest. Directors with an eID reader can type
  it in manually. Never displayed in plain text after save; show only last
  4 digits. Required for Belcotax Fiche 281.86 (Phase 3).
- Dashboard alert: director sees a "Niet-geverifieerde dossiers" count badge
  on the admin home — children enrolled but without id_verified_at.

Key constraints:
- id_verified_at and id_document_type together form the audit trail — both
  required to mark a record as verified.
- NRN is GDPR special-category data; encrypt at rest, never log.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Director verifies a child's identity months after enrolment (common when
  the family posted documents later). Allow retroactive verification —
  the timestamp captures when it actually happened.
- A child turns 12 and gets an eID. Director can update id_document_type
  from 'birth_certificate' to 'eid'.

Out of scope:
- eID card reader / federal Web Components integration — Phase 3 at earliest,
  if IKT compliance makes it necessary.
- Automatic NRN lookup in Opgroeien systems (Phase 3).
```

---

### 023 — Digital Online Enrollment

```
Give parents a public enrollment form they can complete at any time —
without calling the KDV — and let them add themselves to the waiting list
directly. Feeds directly into 012a (waiting list) and pre-populates child
data when the director converts an entry to enrolled.

What to build:
- Public enrollment form: a publicly accessible URL per KDV location
  (e.g. /enroll/{location-slug}) that requires no login. Form fields:
  child first/last name, date of birth, requested start date, parent/
  guardian name, email, phone, and optional notes. Honeypot + rate limiting
  to prevent spam.
- On submission: creates a waiting_list_entry (012a) with status='waiting',
  sends a confirmation email to the parent (using EmailService from 020),
  and alerts the director via in-app notification.
- Director conversion: when the director converts a waiting list entry to
  'offered' or 'enrolled', the form data pre-fills the child creation flow
  (006) and the contact creation flow — director just confirms, not re-types.
- Parent self-registration to waiting list: same form. The parent receives
  a reference number by email they can use to inquire about their position.
- Tour invitation: from the waiting list (012a) the director can send a
  tour invitation email — a templated email with a proposed date/time and
  an accept/decline link. Director marks the tour outcome manually.

Key constraints:
- The public form has no auth. The submitted data goes into the waiting list
  as an unverified entry — director always reviews before converting.
- Rate limit: max 3 submissions per IP per hour (anti-spam).
- The form and confirmation emails respect the location's primary language
  setting (NL/FR) — but also support EN via a language toggle on the form.
- No child data is committed to the tenant schema until the director
  explicitly converts the waiting list entry.

Edge cases:
- Parent submits a duplicate entry (same child name + DOB already in
  waiting list). Flag to director on the list view (as in 012a), do not
  auto-reject — family may have a legitimate reason to reapply.
- Location is at capacity with no projected availability. The director can
  disable the public form temporarily from web admin settings.

Out of scope:
- Online payment of a registration deposit at enrollment time (Phase 3).
- Parent portal with full account management (future).
```

---

### 024 — Digital Contract E-Signature

```
Allow parents to sign their enrolment contract digitally — no printing,
scanning, or in-person appointment required. Embed a SEPA direct debit
mandate in the same signing flow so payment collection is authorised
at the same moment.

What to build:
- Contract signing flow: when a director finalises a contract (007), they
  can send a signing invitation to the parent's email (using EmailService
  from 020). The email contains a secure, time-limited link (signed JWT,
  72-hour TTL).
- Signing page (no login required): parent opens the link, reviews the
  contract PDF inline, scrolls to bottom, draws or types their signature,
  and clicks "Ondertekenen."
- SEPA mandate: below the contract, a second section captures the parent's
  IBAN and authorises the KDV to collect invoices via direct debit. The
  SEPA mandate creditor ID and mandate reference are generated per signing.
  Mandate data stored on the contract record.
- On completion: contract record updated with signed_at, signature_data
  (SVG or base64 image of the drawn signature), signed_by_ip. A final
  signed PDF (contract + signature block + SEPA mandate) is generated
  (QuestPDF) and stored to GCS. Both director and parent receive a copy.
- contracts table additions:
    signing_token       TEXT,           -- UUID, one-time use
    signing_token_exp   TIMESTAMPTZ,
    signed_at           TIMESTAMPTZ,
    signature_data      TEXT,           -- base64
    signed_by_ip        INET,
    sepa_iban           TEXT,           -- encrypted at rest
    sepa_mandate_ref    TEXT,
    sepa_authorised_at  TIMESTAMPTZ

Key constraints:
- The signing link is single-use. Once signed, the token is invalidated.
- IBAN is sensitive financial data — encrypt at rest (same approach as NRN in 022).
- The signed PDF is the legal document. Store it in GCS and never re-generate
  it from live data after signing — the stored PDF is the source of truth.
- All user-facing strings on the signing page use i18n keys (NL/FR/EN).
  The signing page language defaults to the parent's locale preference.

Edge cases:
- Token expires before the parent signs. Director can re-send a new link.
  Old token is invalidated immediately on re-send.
- Parent signs on a mobile browser (not the Expo app). The signing page
  must be a responsive web page, not an app screen.
- Director wants to revise the contract after sending but before signing.
  Invalidate the existing token, save the revised contract, issue a new link.

Out of scope:
- Qualified electronic signature (eIDAS Level 2+) — the simple/advanced
  e-signature implemented here is sufficient for a Belgian KDV contract
  (same level as DocuSign standard). Level 2 requires eID integration (022).
- In-app signing via the parent mobile app (Phase 3).
```

---

### 025 — CODA/CODABOX Payment Matching

```
Import bank statements in CODA format and automatically match payments
to open invoices. Directors currently do this manually in Excel or their
bank's online portal — this feature saves significant monthly admin time.

What to build:
- CODA file import: director uploads a .coda file (downloaded from their
  bank) in web admin. Parser extracts transactions: date, amount, sender
  IBAN, communication (gestructureerde mededeling / OGM reference).
- Matching logic:
    1. Exact OGM match: compare the transaction's structured communication
       against invoice OGM references (already on invoices in 014). If match
       found: mark invoice as paid with the transaction date and amount.
    2. Amount + IBAN match: if no OGM match, look for an open invoice of the
       same amount from a family whose IBAN matches the sender. Suggest as
       a likely match — director confirms.
    3. Unmatched: transactions that don't match any invoice are listed as
       "onbekende overschrijving" for manual review.
- coda_transactions table (tenant schema):
    id            UUID PRIMARY KEY,
    import_date   DATE,
    value_date    DATE,
    amount_cents  INT,
    sender_iban   TEXT,
    sender_name   TEXT,
    communication TEXT,
    matched_invoice_id UUID REFERENCES invoices(id),
    match_type    TEXT CHECK (match_type IN ('ogm','iban_amount','manual','unmatched')),
    created_at    TIMESTAMPTZ DEFAULT NOW()

- CODABOX integration (Phase 2 extension): CODABOX is a Belgian service
  that delivers CODA files directly to software via API — no manual upload
  needed. Integrate the CODABOX API as an optional upgrade over manual
  import. This avoids the director needing to visit their bank portal
  each month.

Key constraints:
- CODA format is a Belgian standard (Isabel/KBC/BNP format). Use an
  existing .NET CODA parser (NuGet) — do not implement from scratch.
- An invoice can only be marked paid once. If a duplicate payment arrives,
  flag it as "dubbele betaling" for director review.
- IBAN data is financial PII — store encrypted at rest, log access.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A parent pays the wrong amount (partial payment). Do not auto-close the
  invoice. Show the partial payment against the invoice with a remaining
  balance.
- A payment references an invoice from a prior month that was already
  written off. Flag as "betaling voor afgesloten factuur."
- CODABOX API is unreachable. Fall back to manual CODA file upload without
  data loss.

Out of scope:
- Direct bank API integration (PSD2 / open banking) — later.
- Multi-bank account support per tenant (single account per location for MVP).
```

---

### 026 — SEPA Direct Debit

```
Generate SEPA direct debit XML (pain.008.001.02 format) so the KDV
can collect invoice amounts directly from parent bank accounts in a
single batch, rather than waiting for individual transfers.

Context: most Belgian KDVs collect payment by bank transfer today.
SEPA direct debit lets the KDV initiate the collection — parent never
needs to remember to pay. Requires a SEPA mandate (feature 024) signed
by the parent, and a creditor identifier (CID) registered with the KDV's
bank.

What to build:
- SEPA batch generation: director selects a set of invoices (e.g. all
  invoices for month M, status='sent') and triggers "Genereer SEPA XML."
- Output: a pain.008.001.02 XML file ready for upload to the KDV's bank
  portal (Isabel6, KBC Business Dashboard, etc.).
- Each debit instruction in the XML maps to one invoice: amount, debtor
  IBAN (from contract.sepa_iban via 024), mandate reference, mandate
  signing date, end-to-end ID (= invoice OGM reference).
- Settings: store the KDV's creditor identifier (CID), creditor name,
  and creditor IBAN in the location settings (004). These are required
  headers in the pain.008 file.
- After export: invoices in the batch are marked status='pending_debit'.
  When the bank confirms collection (via CODA import in 025), they are
  marked 'paid' automatically.

Key constraints:
- Only invoices for parents with a signed SEPA mandate (sepa_authorised_at
  NOT NULL on the contract) can be included in a batch.
- A parent can revoke their SEPA mandate (add revoked_at to the contract).
  Revoked mandates are excluded from future batches.
- The pain.008 format is strict. Validate the generated XML against the
  EPC schema before offering the download.
- Execution date must be at least 1 business day in the future for a CORE
  sequence (standard consumer direct debit). Let director set the date.

Edge cases:
- A parent's IBAN changes between mandate signing and debit execution.
  Director must update the IBAN and re-sign a new mandate — no silent override.
- A debit is returned (R-transaction: RTRN, RJCT). Director sees the
  returned invoice back in 'sent' status with a note. No auto-retry.

Out of scope:
- B2B SEPA scheme (businesses as debtors) — not relevant for parent payments.
- Pre-notification email to parents before debit (legally required 14 days
  prior for CORE) — add to 020 (email communications) when 026 is shipped.
```

---

### 027 — Staff App

```
A separate Expo mobile app for caregivers/staff (distinct from the shared
caregiver group tablet). Staff use this on their personal phones to:
  1. See their personal assignment schedule — which group/room they work in
     on each day, including upcoming weeks.
  2. Submit leave or shift requests.
  3. Receive push notifications about schedule changes.

The director manages assignments from the web admin — planning weeks in
advance and making on-the-fly changes when someone calls in sick.

Context: the caregiver group tablet (008/008a) is shared in the room.
Staff need a personal app for: "Where am I working next Wednesday?"
and "I need to call in sick." D-care doesn't have this; BitCare does.

What to build:

── Staff assignment model (extends 012) ──────────────────────────────────
- staff_assignments table (tenant schema):
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    staff_id        UUID REFERENCES staff_members(id) NOT NULL,
    location_id     UUID REFERENCES locations(id) NOT NULL,
    group_id        UUID REFERENCES groups(id),     -- nullable: unassigned to group
    assigned_date   DATE NOT NULL,
    shift_start     TIME,                            -- optional if day-based
    shift_end       TIME,
    status          TEXT CHECK (status IN (
                      'scheduled',    -- planned in advance
                      'confirmed',    -- staff acknowledged
                      'absent',       -- called in sick / approved leave
                      'covered'       -- replaced by another staff member
                    )) DEFAULT 'scheduled',
    cover_staff_id  UUID REFERENCES staff_members(id),  -- who covered if absent
    notes           TEXT,
    created_by      UUID REFERENCES users(id),
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (staff_id, assigned_date, group_id)

  This extends or replaces staff_schedules from 012 — audit 012's schema
  and consolidate into this model.

── Director web admin ────────────────────────────────────────────────────
- "Rooster" section: a week-view calendar grid. Columns = days, rows = staff.
  Each cell shows which group that staff member is assigned to that day.
  Director drags/drops or clicks to assign. Can plan weeks in advance.
- Part-time staff: each staff member record (005) has contracted_days TEXT[]
  (e.g. ['mon','tue','wed']). The grid auto-greys non-working days so the
  director cannot accidentally schedule someone on their day off.
- Closure days (011) are greyed in the grid — no assignments on closure days.
- On-the-fly sick cover:
    Director marks a staff member absent for today → system shows a
    "Wie vervangt [Name]?" prompt listing available staff not yet assigned
    to a conflicting group. Director selects a replacement.
    The original assignment status → 'absent', a new assignment is created
    for the replacement with status 'covered', cover_staff_id set.
    Both staff members receive push notifications.
- "Rooster publiceren": director publishes the schedule for a week/period.
  Only published schedules are visible to staff in the app.
  Unpublished schedules are director-draft only.

── Staff app (new Expo project) ──────────────────────────────────────────
- Authentication: personal email/password (standard JWT from 003).
  Not the room device token.
- Schedule view: own assignments for the next 4 weeks. Day view and week
  view toggle. Each day shows: which group/room, start time, end time.
  Closure days shown as "KDV gesloten."
- "Ik ben ziek" button: one-tap sick-day report for today (or tomorrow).
  Creates a staff_leave_request (type='sick') and immediately alerts
  the director. Director sees an "Urgent: sick cover needed" banner in
  the web admin.
- Leave request (planned):
    staff_leave_requests table (tenant schema):
        id          UUID PRIMARY KEY,
        staff_id    UUID REFERENCES staff_members(id),
        type        TEXT CHECK (type IN ('sick','annual','other')),
        date_from   DATE,
        date_to     DATE,
        notes       TEXT,
        status      TEXT CHECK (status IN ('pending','approved','rejected'))
                    DEFAULT 'pending',
        decided_by  UUID REFERENCES users(id),
        decided_at  TIMESTAMPTZ,
        created_at  TIMESTAMPTZ DEFAULT NOW()
- Push notifications: received when schedule is published, an assignment
  changes, or a leave request is approved/rejected.
- Director web admin: "Verlofaanvragen" queue — approve/reject. Approved
  leave auto-sets assignment status to 'absent' for affected dates.

Key constraints:
- Staff can only see their own schedule (not colleagues').
- Part-time staff: contracted_days drives which days appear; the rest are
  greyed out in both the web admin grid and the staff app.
- A schedule week must be published before staff can see it.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Director makes a last-minute assignment change after publishing. Staff
  member receives a push ("Je rooster is gewijzigd") and the app refreshes.
- Staff member is scheduled at two locations on the same day (split day).
  Two assignment rows for the same date; both show in the staff app.
- A public holiday falls on a working day (not a KDV closure, but a legal
  holiday — e.g. 11 November). Director manually marks it or the system
  flags it. No auto-block — Belgian public holidays vary; director decides
  per-location.

Out of scope:
- Time registration / clock in/out (feature 028).
- Staff HR dossier (feature 028).
- Staff-to-staff group chat (Phase 3).
- Automatic optimal scheduling / AI suggestions (Phase 4).
```

---

### 028 — Staff HR Dossier & Time Registration

```
Give directors a digital HR file per staff member — employment contracts,
training records, qualification documents — and let staff clock in/out
so the KDV has accurate hours worked for payroll and for the
medewerkersbeleid subsidy application (Opgroeien 2025 subsidy that rewards
KDVs meeting the new BKR ratios early, verified by hour counts per
caregiver per function).

What to build:
- Staff time registration:
    staff_time_entries table (tenant schema):
        id           UUID PRIMARY KEY,
        staff_id     UUID REFERENCES staff_members(id),
        location_id  UUID REFERENCES locations(id),
        group_id     UUID REFERENCES groups(id),  -- nullable
        clocked_in_at  TIMESTAMPTZ NOT NULL,
        clocked_out_at TIMESTAMPTZ,
        function     TEXT CHECK (function IN (
                       'kinderbegeleider','logistiek','verantwoordelijke'
                     )) NOT NULL,  -- required for medewerkersbeleid subsidy calculation
        notes        TEXT,
        created_at   TIMESTAMPTZ DEFAULT NOW()

- Clock in/out: via the staff app (027) — staff tap "Begin dienst" /
  "Einde dienst" on their personal phone. Time auto-filled; staff selects
  their function for the day if they work multiple roles.
- Staff HR dossier (web admin, director only):
    staff_documents table (tenant schema):
        id            UUID PRIMARY KEY,
        staff_id      UUID REFERENCES staff_members(id),
        document_type TEXT CHECK (document_type IN (
                        'employment_contract','amendment','qualification',
                        'training','other'
                      )),
        title         TEXT,
        gcs_url       TEXT,   -- signed GCS URL
        valid_from    DATE,
        valid_until   DATE,   -- nullable; set for contracts with an end date
        created_at    TIMESTAMPTZ DEFAULT NOW()
- Contract expiry alerts: director dashboard shows a "Personeel — verlopende
  contracten" block with staff whose employment contracts expire within 60 days.
- Medewerkersbeleid subsidy report (web admin):
  A report page showing total child-hours ÷ staff-hours by function, per
  location, for a selected period. This is exactly what Opgroeien requires
  to verify that the KDV meets the new ratios (1:5 baby, 1:7 mixed, 1:8
  toddler-only). Directors download this report when applying for the subsidy.

Key constraints:
- Time entries are immutable after a configurable lock period (e.g. 7 days).
  Director can unlock for corrections.
- The GCS signed-URL convention applies to all staff documents.
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- Staff member forgets to clock out. Clocked_out_at is null. Director
  can fill it in retroactively from the web admin.
- Staff member works across two groups in one day (e.g. morning in babies,
  afternoon in toddlers). Two separate time entries.

Out of scope:
- Payroll calculation (pay rates, deductions) — the hour export feeds a
  payroll system; we don't do payroll.
- Humanwave API integration (Phase 4) — for now, provide a CSV export of
  hours per staff member per period.
```

---

### 029 — Accounting Export

```
Export invoicing and payment data to common Belgian accounting packages.
Directors or their bookkeepers use Exact Online, Yuki, Accountable, or
similar tools — this avoids re-typing invoice data.

What to build:
- CSV export: a configurable export of invoices (and optionally payments)
  for a selected date range. Columns: invoice number, family name,
  amount excl. VAT, VAT amount, amount incl. VAT, due date, paid date,
  payment reference. Downloadable from web admin under Financiën → Export.
- Exact Online UBL export: generate UBL 2.1 XML (the format Exact Online
  and most Belgian accounting packages accept for invoice import). One
  UBL file per invoice, or a ZIP of the period's invoices.
- Yuki export: Yuki uses a specific XML format (Yuki Sales Invoice XML).
  Generate this as an alternative to UBL if the director selects Yuki as
  their accounting package (a setting in the director's profile).
- Accounting package preference: a dropdown in Settings → Boekhouding
  where the director selects their package. Determines which export format
  is offered as the primary option.

Key constraints:
- VAT: Belgian KDVs operating under the Opgroeien licence are VAT-exempt
  (BTW vrijgesteld, Article 44 §2 5° WBTW). All exports must reflect
  this — no VAT amount on any line.
- Export is read-only. No import, no sync, no API keys needed from the
  accounting package for MVP (file-based only).
- All user-facing strings use i18n keys (NL/FR/EN).

Edge cases:
- A credit note (negative invoice from a correction) must export correctly
  with a negative amount in the UBL/CSV.
- Director exports the same period twice (e.g. re-running after a late
  payment). The export is idempotent — same data produces the same file.

Out of scope:
- Live API sync with accounting packages (Exact Online REST API, Yuki
  API) — later, after file-based export proves its value.
- Payroll export (028 handles staff hours; payroll is out of scope).
```

---

### 031 — Photo Lifecycle & Governance

**Added 2026-07-13, from a brainstorm about whether child/group photos needed to move out of
the DB into object storage.** They don't — that migration already happened. `IProfilePhotoStorage`
(005/006), `IGroupActivityPhotoStorage` (009b), and `IHealthAttachmentStorage` (013b/013c) all
store only a GCS object path on the DB row and serve signed URLs; no photo bytes live in Postgres
anywhere in this codebase today. What's still missing is the *governance* layer around those
already-stored photos: what happens to them when a child leaves, how storage cost is kept low
pre-revenue, and who is explicitly allowed to act on them.

```
Build the retention, cost-tiering, and access-control rules that govern
photos already stored in GCS (profile photos, group activity photos,
health attachments) — no new upload/storage mechanism, this feature only
adds policy on top of the existing ports.

Context: child/staff profile photos, group activity photos, and health
attachments are already stored via signed GCS object paths (features 005,
006, 009b, 013b/013c) — never as DB blobs. This feature does not migrate
storage; it closes gaps those features deliberately left open.

What to build:

1. Archive-on-departure (not delete-on-departure)
   - When a child is deactivated (006's soft-delete / DeactivateChildCommand),
     their GCS photo objects (profile photo, any group-activity photos they
     appear in, health attachments) are NOT deleted and NOT auto-expired.
   - Introduce a GCS lifecycle rule that transitions objects belonging to a
     deactivated child from Standard to a cheaper storage class (Nearline or
     Coldline) after a grace period (e.g. 30 days post-deactivation) rather
     than leaving them on Standard indefinitely or deleting them.
   - Explicit deletion (a parent's GDPR erasure request, or a director's
     manual purge) is a separate, deliberate action — never automatic.
   - Group activity photos are trickier: one photo can depict multiple
     children (009b tags multiple children per activity/moment). A photo is
     only eligible for archiving once EVERY tagged child in it is inactive —
     it must stay on Standard tier as long as any actively-enrolled child
     appears in it.

2. Storage-class cost tiering (pre-revenue cost control)
   - Default lifecycle policy for ALL photo buckets (not just departed
     children): move objects from Standard to Nearline after N days of no
     access (configurable; start conservative, e.g. 90 days), since most
     photo views happen in the days immediately after upload.
   - Thumbnails always stay on Standard (cheap, small, frequently re-rendered
     in gallery/list views) — only full-resolution originals are tiered.
   - This is a bucket-level GCS lifecycle configuration (Terraform), not
     application code — no handler changes needed for this part.

3. Explicit photo-action RBAC
   - Staff, Director, and Admin (if a separate admin role exists beyond
     Director — confirm against 003/005's role model) can all: upload,
     caption/edit, delete, and download any photo within their location(s).
   - Confirm this against the CURRENT authorization on
     DeleteGroupActivityCommand and any equivalent profile-photo/health-
     attachment delete path — today's code should be audited for whether
     these already enforce StaffOrDirector-equivalent policies, or whether
     they're gated too loosely/tightly. Close any gap found; this feature
     does not invent a new role, it makes an implicit assumption explicit
     and consistent across all three photo ports.
   - Parents (ParentOnly policy, 003) may VIEW and DOWNLOAD original-
     resolution photos of their own child (including group photos their
     child is tagged in) but may never delete, edit, or upload.

4. Parent original-photo download
   - Today, parent-facing photo access (e.g. GetParentGroupActivityGalleryQuery,
     009b) returns signed view URLs for gallery display. Add an explicit
     "download original" action in the parent app: a signed URL with
     Content-Disposition: attachment (rather than inline), pointing at the
     full-resolution object (not the thumbnail).
   - Group photos: a parent can download any photo in which their child is
     tagged, even though other children also appear in it — no cropping or
     redaction of other children is required for this feature (out of scope
     below).

Key constraints:
- No change to where photo bytes are stored (GCS) or how they're uploaded —
  this is retention/cost/access policy only, layered on existing ports.
- Lifecycle transitions must never be visible to end users as a functional
  change — a Nearline/Coldline object is still served via the same signed-URL
  mechanism, just with higher retrieval latency/cost, which is acceptable
  since departed-child access is rare.
- All user-facing strings (download button, any retention-related director
  setting) use i18n keys (NL/FR/EN).

Edge cases:
- A child is reactivated (e.g. re-enrolled) after being deactivated and
  archived. Photos must remain accessible (signed URLs still resolve against
  Nearline/Coldline objects, just slower) — reactivation does NOT require an
  explicit "un-archive" step, only faster access could optionally be
  restored by moving back to Standard.
- A group photo has 8 tagged children; 7 leave over time, 1 remains enrolled.
  The photo must stay on Standard tier and fully accessible until the last
  tagged child also becomes inactive.
- A director explicitly deletes a departed child's photos (GDPR request).
  Must cascade to every GCS object referencing that child: profile photo,
  every group-activity photo where they're the ONLY tagged child, and health
  attachments — but must NOT delete a group photo still depicting other
  active children.

Out of scope:
- Any change to the underlying storage mechanism/provider (GCS stays GCS
  regardless of whether Postgres ends up on Neon or Google Cloud SQL/AlloyDB
  in production — these are independent infrastructure decisions).
- Photo redaction/cropping/blurring of non-consenting children in group
  photos — if this becomes a real requirement (e.g. a parent revokes photo
  consent, contract's photos_internal/photos_social_media flags from 007),
  handle it as its own feature.
- GDPR data export tooling in general (flagged as Phase 2 out-of-scope back
  in 006) — this feature only handles the deletion cascade for photos
  specifically, not a full export/erasure workflow.
```

---

### 032 — Platform-Admin Portal

**Added 2026-07-13.** No cross-tenant admin surface exists anywhere in this codebase today.
Two gaps prompted this: (1) feature 001 built the director invitation *acceptance* flow
(token → registration) but never a way to *create* that invitation — it's currently a manual
token/DB operation; (2) feature 013h (if/when built) needs a platform-admin role to manage the
013g vaccine catalog, but no such role or portal exists to host it in. This feature is that
portal — the first genuinely cross-tenant (not tenant-scoped) admin capability.

```
Build a dedicated platform-admin (super-admin) portal — sits above all
tenant boundaries, unlike every existing role (Director/Staff/Parent,
feature 003/005) which is scoped to a single tenant.

What to build:
- Director/organisation invitations: platform-admin sends an invitation
  to a prospective KDV director (email + organisation name at minimum).
  Reuse feature 001's existing invitation-token model (signed token,
  expiry, single-use) — 001 already built acceptance, this builds creation.
  Platform-admin can see invitation status (pending / accepted / expired)
  and resend or revoke a pending invitation.
- Platform data management: a management UI for shared, platform-wide
  reference data — starting with whatever is the first concrete dataset
  that needs it (e.g. the vaccine catalog, if feature 013h has not
  separately built its own screen for this by the time this is picked up).
  Structure the portal so a second dataset doesn't require rebuilding the
  shell, but don't build any dataset's screen speculatively.

Open questions this feature must resolve before implementation (flag for
/speckit-clarify, do not assume silently):
- Where does a platform-admin authenticate? No natural tenant exists to
  scope a JWT to — a new auth path, a super-admin flag on an existing
  account, or something else entirely needs deciding.
- Is this a new gated section in the existing Next.js web app, or a
  genuinely separate internal tool? Given Constitution Principle VII
  (monolith-first simplicity), default to "same app, new gated route"
  unless a concrete reason argues otherwise.
- Who can become a platform-admin, and how is the very first one
  provisioned (chicken-and-egg — no invitation mechanism exists yet to
  invite them)? Likely a seeded/manual account — the spec should say so
  explicitly.
- Does revoking an invitation, or editing platform data, need an audit
  trail (who did it, when)?

Out of scope:
- Any change to existing tenant-facing behavior of whatever platform data
  this touches (e.g. GET /api/vaccine-types stays exactly as-is).
- Billing/subscription-plan management from this portal (out of scope per
  feature 001; still deferred).
- Tenant suspension/deletion tooling (out of scope per feature 002; still
  deferred).
```

---

### 033 — Kinderopvangtoeslag (AARON) Submission

```
Build monthly attendance submission to Opgroeien for the kinderopvangtoeslag
(Groeipakket childcare allowance) — for vrije-prijs (free-price) KDVs.

Regulatory context (verified 2026-07 on opgroeien.be):
- Parents using childcare with a FREE PRICE (not IKT) receive a
  kinderopvangtoeslag via the Groeipakket: a fixed amount per (half) day
  the child is present.
- The organisator must report the children's presences to Opgroeien EVERY
  MONTH. Opgroeien offers the free web app AARON for manual entry, but using
  it is not mandatory — software can submit via webservice instead.
- This applies to exactly the Phase 1 target segment (private KDVs), unlike
  IKT (feature 019, Phase 3).

Known technical facts (from "Kinderopvangtoeslag webservice - beschrijving",
opgroeien.be, and the AARON-related pages):
- REST/JSON webservice, NOT the IKT SOAP stack.
- Auth: bearer token. A test-environment token is obtained on request from
  software-ontwikkeling@kindengezin.be.
- Test environment Swagger UI: https://tstgpappr.kindengezin.be/swagger-ui.html
- Request identifies the K&G-dossiernummer and the month; payload models:
  PrestatieData (top level) → KindData (child identification) →
  KindPrestatieData (day of month present) → KindPrestatieDetailsData
  (periods of presence within that day).
- Response: OpvangPrestatieResponseDTO with SUCCESS or ERROR;
  errors detailed in ErrorResponseDTO.

Functional facts from the official AARON handleiding voor ouders
(obtained 2026-07-15):
- AARON records ARRIVAL AND DEPARTURE TIMES per child per day (not just
  day counts) — via a per-child unique QR code + numeric code.
- Child identity in AARON: voornaam, familienaam, geboortedatum, GESLACHT —
  and it must match the child's ID exactly: Opgroeien matches against the
  RIJKSREGISTER; a mismatch means the parent does not get paid.
- Optional field: the date the child starts school.
- Up to 5 contact persons per child (voornaam, familienaam, e-mail); added
  contacts get an AARON registration email and can then follow their
  child's registrations daily (read access) and spot errors.
- Opt-out: if the parent has no right to (or does not want) the toeslag,
  the location flags this and the child's data is NOT sent to Opgroeien.
- Implication for our child model: gender + exact official name fields +
  school-start date + per-child toeslag opt-out flag are needed for this
  feature; parents reviewing registrations is already covered by 037's
  confirmation flow (may double as the AARON accuracy check).
- QR phase-out (snelinfo 25/07/2025): printed AARON QR cards are being
  discontinued — codes are now generated digitally at child registration
  and may live on the parent's smartphone. Opportunity: display the
  child's AARON QR + numeric code inside OUR parent app (ties in with
  021 QR check-in).

Facts from the official AARON sector handleiding (39 p., obtained
2026-07-15):
- DEADLINE: registrations must reach Opgroeien by the 7TH of the month
  following the care month. In AARON this happens automatically when the
  data is error-free; unresolved errors past the 8th trigger an automatic
  reminder e-mail and risk a FINE ("boete") — our 033 must surface a
  pre-deadline error dashboard + alerting well before the 7th.
- Validation: Opgroeien matches each child against a valid
  rijksregisternummer/bisnummer; typical errors = missing arrival or
  departure time, or identity mismatch. Only the first given name is
  needed when a child has several.
- Toeslag right expires when the child turns 2.5 YEARS, unless the child
  starts school later — birthdate correctness is critical.
- Opt-out has a PERIOD (start defaults to birthdate, open end allowed);
  non-paying children INCLUDING the organisator's own children get no
  toeslag and must be flagged.
- Offline registration is supported in AARON (sync on reconnect) and
  paper + manual after-the-fact entry is acceptable — matches our
  caregiver app's offline-first design; 033 derives from 010 data anyway.
- AARON also handles location lifecycle (stop, no licence, no free-price
  places, relocation, new location starting next month) — our submission
  flow must handle mid-month location changes.

What to build:
- Monthly submission job/screen: derive per-child presence days (and periods)
  for a location+month from the existing attendance data (feature 010),
  build the JSON payload, submit, store the response.
- Submission log per location+month: payload sent, response, status,
  resubmission after corrections.
- Director review screen before sending (like the FO-SU-05 review in 019).
- Alerting when a month has not been submitted by its deadline.

FIELD-LEVEL CONTRACT — VERIFIED (Swagger 2.0 definition
KinderOpvangToeslag.json obtained 2026-07-15, host tstgpappr.kindengezin.be;
commit to docs/integrations/opgroeien/):
- Single endpoint: POST /opvangprestaties ("Registreer een nieuwe
  opvangprestatie"), bearer-token security.
- Request: locatieId* (string, dossiernummer, example "91XXXXXXX"),
  periode* (date string = the care month), prestaties* : PrestatieData[].
- PrestatieData: kind* : KindData.
- KindData: kindNaam*, kindVoornaam* (strings), kindGeboortedatum* (date),
  kindGeslacht* (enum "M"/"V"), datumSchoolgaand (date, optional),
  kindPrestaties* : KindPrestatieData[].
- KindPrestatieData: datumOpvang* (date) +
  kindPrestatiesDetails* : KindPrestatieDetailsData[].
- KindPrestatieDetailsData: checkIn* and checkOut* (datetime, e.g.
  2018-06-30T09:30:00) — RAW ARRIVAL/DEPARTURE TIMESTAMPS. No day/half-day
  classification is sent: Opgroeien derives the (half-)day entitlement
  server-side from the periods. Our attendance records (010) map 1:1.
- Response OpvangPrestatieResponseDTO: success (bool), result
  ("SUCCESS"/"ERROR"), crlId (logging reference "GP_REST_..."),
  errorList : ErrorResponseDTO[] (errorNr, errorCode, errorText).
- Note: the API has NO opt-out field — children flagged "no toeslag" are
  simply omitted from the payload.

Open questions (do NOT invent — resolve with Opgroeien before /speckit-plan):
- Production auth/onboarding procedure for the bearer token (only the test
  procedure is documented publicly) — ask software-ontwikkeling@kindengezin.be.
- Correction/resubmission semantics: does re-POSTing the same
  locatieId+periode replace the previous submission? (Deadline is known:
  7th of the following month, fines possible — see sector handleiding
  facts above.)

Key constraints:
- Multi-tenancy respected; submissions are per organisation (dossiernummer).
- All user-facing strings use i18n keys (NL/FR/EN).
- Store what was sent (immutable audit copy) — this is a subsidy-relevant
  government submission.
```

---

### 034 — Jaarregistraties XML Export

```
Build the annual Opgroeien "jaarregistratie" reporting export.

Regulatory context (verified 2026-07 on opgroeien.be, Organisator page):
- Organisators submit yearly registration forms to Kind en Gezin/Opgroeien.
- The data can be delivered in XML format — EXCEPT the medewerkers
  (staff) form, which must be delivered as PDF.
- Opgroeien's PDF forms come with values pre-filled per organisation. When
  submitting via XML, those pre-filled values must be sent back correctly —
  the import runs validation checks against them. Opgroeien can provide an
  Excel file per organisation per form type with these values, intended to
  be imported into the vendor's database to minimise error messages.

What to build:
- XML export per form type per year, built from data already in the platform
  (locations, staff, children, attendance).
- Import screen for Opgroeien's per-organisation Excel file of pre-filled
  values; store them and merge into the XML output.
- Validation before export; export history with the exact files produced.
- PDF path for the medewerkers form (QuestPDF or the official form —
  decide at plan time based on what the official form allows).

Known schema facts (XSDs obtained 2026-07-15 from the official zip —
commit them to the repo, e.g. docs/integrations/opgroeien/, and attach to
the spec):
- Six jaarregistratie XSDs, all SUBSIDY-related annual forms:
    FO-RE-14 — Prestaties inclusieve opvang: per child with an
      IdInclusieveOpvang (pattern I+6 digits): DerdeDag/HalveDag/VolleDag
      counts, optional name/birthdate/period, Gestopt flag.
    FO-RE-15 — Flexibele openingstijden: per location (LocatieId, name):
      counts Min30v7, Min30na18, Min30, Feestdag, Weekend.
    FO-RE-19 — Voorrangsgroepen (IKT): per location: MaxVergundeCapaciteit,
      IKT period, KenmerkPlusSubsidie J/N, children counts
      (Totaal / UitVoorrangsgroepen / UitKwetsbareGezinnen), priority-group
      detail (WerkOpleiding, Alleenstaanden, LaagInkomen, Pleegkinderen,
      GezinslidHandicap, GezinslidZelfzorgvermogen, SociaalPedagogisch,
      LaagOpleidingsniveau), KindPlaatsRatio (decimal 10,2), plus a
      ParamVerschilRatio parameter.
    FO-RE-28 — Dringende opvangplaatsen: per location: StartWerk,
      StartOpleiding, WegvallenOpvang, CrisisgeenZorg, CrisisZorg.
    FO-RE-29 — Ruimere openingsmomenten: per location: Voor7uur, Na18uur,
      Zaterdagen, ZonFeestdagen.
    FO-RE-30 — Kwetsbare gezinnen: like FO-RE-19's counts without
      Pleegkinderen.
- Common envelope on all six: root <KindEnGezin>; Header (DocumentIndicator,
  FormVersion, MinReaderVersion, UsedViewer = software name+version);
  DocumentGegevens (NaamSubsidiegroep, NaamOrganisator — max 50 chars,
  SubsidiegroepId — long, Jaar ≥ 2014); FormulierGecontroleerd 'J'/'N'
  ('N' = Opgroeien does not process it); optional Config
  (TargetURL — default ko.formulieren@kindengezin.be, Subject).
- Timing (per the attendance-registration description PDF): these annual
  forms are requested once per year, at the start of the following
  calendar year.
- IMPORTANT scope note: the six XML forms are tied to subsidies (inclusieve
  opvang, flex, IKT voorrang, dringende plaatsen, ruimere openingsmomenten,
  kwetsbare gezinnen) — for pure vrije-prijs (Phase 1) clients they largely
  do not apply. BUT (verified in the official jaarregistraties brochure,
  dec 2024 version, obtained 2026-07-15): the MEDEWERKERS registration
  applies to ALL organisators, subsidised or not. So 034 has a small
  Phase 2-relevant core (medewerkers support) and a larger subsidy-tied
  tail (the six XML forms) that can shift to Phase 3.

Medewerkers form facts (brochure, groepsopvang variant):
- PDF-only — explicitly CANNOT be submitted as XML ("omwille van de
  complexiteit"). Opgroeien emails a pre-filled Adobe PDF form in the first
  months of the year; it must be completed in Adobe Reader and sent via the
  form's send button (or as mail attachment to ko.formulieren@opgroeien.be);
  printing/scanning is not processed; a confirmation email follows;
  corrections = edit and resend the same form (overwrites previous).
- Snapshot date: staff situation on 1 JANUARY. Include: kinderbegeleiders,
  the (single) verantwoordelijke per location, and staff who systematically
  support the verantwoordelijke — any paid status (employee, self-employed,
  interim, sociale maribel); include both titularis and replacement if
  absent-but-replaced; exclude anyone inactive > 12 consecutive months.
  Exclude: logistics staff, purely administrative staff, unqualified
  helpers, volunteers/interns, external contractors.
- Fields per staff member: naam, voornaam, geboortedatum,
  RIJKSREGISTERNUMMER, geslacht, diploma (from a list), other
  kwalificatiebewijzen (optional), kennis Nederlands (J/N),
  kwalificatietraject verantwoordelijke (J/N), kwalificatietraject
  begeleider (J/N).
- Consequence for ChildCare: since the form is pre-filled and PDF-only, the
  platform cannot submit it — but it CAN generate a "medewerkers
  jaarregistratie helper report" listing exactly these fields from the
  staff module (005/028) so the director can copy/verify in minutes.
  NOTE: staff rijksregisternummer storage would be needed for the full
  helper value — decide deliberately (028's HR dossier is the natural home;
  the current architecture avoids storing NRN).

Open questions (do NOT invent — resolve before /speckit-plan):
- Which of the six XML forms applies to which subsidy trap for a given
  client.
- Exact submission deadline per form (stated only in Opgroeien's
  accompanying emails).

Key constraints:
- All money/counts derived from recorded data, never re-keyed by hand.
- Immutable audit copy of every exported file.
- i18n keys for all UI strings (NL/FR/EN).
```

---

### 035 — Safety & Risk-Analysis Register

```
Build the safety & risk-analysis module: the legally required risicoanalyse,
an incident/near-incident logbook, and sleep-safety attest tracking.

Regulatory context (verified 2026-07 on opgroeien.be, Veiligheid page):
- Every childcare organisator must have a risk analysis covering FOUR legal
  domains: (1) injuries and accidents, (2) crises and life-threatening
  situations, (3) children going missing, (4) illness, infection and
  contamination.
- It is a CONTINUOUS process, not a one-off document: risks are identified,
  weighed (aanvaardbaar/onaanvaardbaar — Opgroeien's "3 A's": Aandacht,
  Afweging, Aanpassing), and unacceptable risks must be addressed immediately.
- Opgroeien publishes non-mandatory "actielijst" checklists (binnen, buiten,
  kwaliteitsvol handelen, omgevingsfactoren, slapen, verzorging, ziekte) as
  .docx downloads — these are the natural seed content for digital checklists.
- Opgroeien explicitly recommends keeping a LOGBOEK of (near-)incidents and
  evaluating them with the 3 A's.
- Sleep safety: babies < 1 year must sleep on their back. If parents insist
  on another position, a signed "attest slaaphouding" is required (official
  .docx model exists), plus a medical attest if the reason is medical.

What to build:
- Risk register per location: risk items classified under the 4 legal
  domains; status (identified / weighed / action decided / resolved);
  linked actions with owner and due date; periodic review reminders.
- Digitised checklist templates seeded from Opgroeien's actielijsten,
  editable per location; completed checklist runs stored with date + user.
- (Near-)incident logbook: quick entry (what/where/when/children involved),
  link to a risk item, link to a 013b incident report when one was filed.
- Attest slaaphouding on the child profile: flag + stored signed document
  (upload; e-signature only if 024 exists), plus medical attest when
  applicable. Caregiver tablet shows the sleep-position flag on the child.
  Verified from the official attest model (31/03/2025, obtained 2026-07-15):
  one attest per child; it must be signed by EVERY person who holds a
  schriftelijke overeenkomst for that child at the location (and by a
  non-consenting parent with parental authority if applicable); two
  declaration checkboxes per signer (request for non-back sleeping +
  confirmation of having read the safe-sleep information); delivered to
  the location's verantwoordelijke. Data model: attest supports multiple
  signers with per-signer name/relation/date.
- Seed checklist content: Opgroeien's actielijst "Veilig slapen" (docx
  obtained 2026-07-15) confirms the checklist structure: per question —
  "Hoe zorg je daar nu voor?" / "Kan de aanpak nog verbeteren? Hoe en
  wanneer?" — grouped in themes (rugligging, wennen en toezicht, ...);
  locations may drop/add questions. Model checklist items with those two
  free-text answer fields and per-location customisation.
- Director dashboard widget: open unacceptable risks, overdue reviews.

Key constraints:
- This is a differentiator: neither D-Care nor Bitcare markets risk-analysis
  support for the Flemish rules. Keep the UX lightweight — caregivers log,
  directors review.
- Documents via GCS signed URLs; i18n keys everywhere; no business logic in
  endpoint handlers.
```

---

### 036 — Crisis & Mandatory Reporting

```
Build the crisis / grensoverschrijdend gedrag workflow and verontrusting
signal registration.

Regulatory context (verified 2026-07 on opgroeien.be, Veiligheid page):
- An organisator is LEGALLY OBLIGED to report to Opgroeien, as fast as
  possible: every crisis and every instance of grensoverschrijdend gedrag;
  criminal investigations or convictions (morality-related) involving anyone
  in regular direct contact with the children; complaints about a crisis;
  problems threatening continuity of the childcare; important governance
  changes in the organisation.
- Crisis = situation where a child's physical or psychological integrity is
  in danger or violated (examples: violence/neglect/abuse even if not yet
  objectively established, death of a child or staff member, serious or
  unexplained injuries, sudden unexplained behavioural change, risk factors
  in staff such as stress/drug/alcohol use).
- Grensoverschrijdend gedrag = child is (at risk of becoming) a victim of
  degrading treatment, threats or violence by a person present in the
  childcare.
- Reporting channel: Opgroeien's official meldingsformulier (.docx:
  crisissituatie-kinderopvang-meldingsformulier.docx) emailed to the
  organisation's klantenbeheerder; in urgent cases by phone via Opgroeipunt.
- Verontrusting = concern about a child's home situation. Opgroeien provides
  an ABC stappenplan (Aandacht, Bespreken, Communicatie met ouders) and a
  signal-registration document ("Signalen van verontrusting registreren").
- Retention: complaints and crisis records must be kept 10 YEARS.

What to build:
- Crisis/GOG event registration: structured record (type, when, who involved,
  actions taken), restricted to director role by default.
- Meldingsformulier pre-fill: generate the report document from the recorded
  event. Fields VERIFIED from the official model ("Meldingsformulier
  Crisissituatie Kinderopvang", 26/07/2024, obtained 2026-07-15):
    melder: naam, adres, telefoon, e-mail, functie;
    locatie: naam, dossiernummer, adres;
    voorval: datum, uitgebreide beschrijving, afloop, wie werd
    geïnformeerd, gerechtelijk onderzoek/politie J/N (+ PV-nummer indien
    gekend), genomen/geplande preventieve acties;
    betrokken kind (bij voorval met 1 specifiek kind): naam, geslacht,
    geboortedatum, startdatum opvang.
  Model note: if the fiche can't be completed immediately, the
  klantenbeheerder must first be informed by phone/e-mail and the fiche
  follows — the workflow must support "reported first, form completed
  later". Track: sent to whom (klantenbeheerder contact stored per
  organisation), when, follow-up notes.
  Explicit NON-reportables (from the model): everyday minor injuries with
  a plausible explanation (schaafwond, buil, blauwe plek...) — these stay
  in 013b incident reports only.
- Continuity-problem and governance-change meldingen as lighter record types
  (same obligation list, simpler payloads).
- Verontrusting signal log per child: dated observations following the ABC
  steps; STRICTLY restricted access (director + the child's caregivers only);
  explicitly excluded from parent-facing views and daily reports.
- 10-year retention class on all records in this module (see feature 038).

Key constraints:
- Extreme access-control care: this is the most sensitive data in the
  platform. Audit-log every read.
- Not a replacement for the legal duty itself — the platform documents and
  supports the melding, the director still sends/confirms it.
- i18n keys everywhere.
```

---

### 037 — Attendance Register Compliance

```
Build the legal compliance layer on top of the existing attendance register
(feature 010, done — do not modify its core model; extend it).

Regulatory context (verified 2026-07 on opgroeien.be, Ouders-en-kinderen page):
- The aanwezigheidsregister must record each child's arrival and departure
  TIMES daily, filled in AT THE MOMENT of arrival/departure — never in
  advance, never afterwards.
- PARENTS must confirm the registered presences, in writing or
  electronically. The frequency is the organisator's choice (daily, weekly,
  ...).
- The register must be kept at least 12 MONTHS for Zorginspectie.

What to build:
- Parent confirmation flow in the parent app: parent reviews the recorded
  arrival/departure times for a configurable period (daily/weekly/monthly,
  set per location) and confirms electronically; confirmation stored with
  timestamp + parent identity.
- Dispute path: parent flags an incorrect time → director sees it, corrects
  via the existing correction mechanism, parent re-confirms.
- Record-at-the-moment audit: server-side recorded_at vs occurred_at delta
  is already implicit — surface it: an inspection view showing when each
  entry was recorded vs the arrival/departure time it claims.
- Zorginspectie export: per location + date range, arrival/departure times
  per child + confirmation status, printable/PDF.
- Retention marker: attendance records enter a ≥12-month legal-hold class
  (see feature 038).

Key constraints:
- Extends 010 and 013 (parent app) — no changes to the done features' core
  behaviour, only additive endpoints/screens.
- i18n keys everywhere; QuestPDF for the export.
```

---

### 038 — Data Retention & GDPR Lifecycle

```
Build automated data-retention management per the sector's legal terms.

Regulatory context (verified 2026-07 on opgroeien.be, Organisator page,
"Bewaartermijnen van gegevens"):
- 10 years: complaints and crisis situations.
- 5 years: data about children and families (identification, medical,
  financial, social vulnerability, their childcare request and usage);
  data about organisators, staff and onthaalouders (medical fitness attest,
  identification, training); for staff the clock starts at END OF EMPLOYMENT.
- 3 years: uittreksel strafregister (criminal-record extract), counted from
  appointment date, for organisators, all staff and every adult in direct
  contact with children; the PREVIOUS extract must be destroyed when a new
  one is received.
- ≥12 months: attendance register (feature 037).
- GDPR baseline: privacy policy in the huishoudelijk reglement,
  verwerkersovereenkomst between KDV and software vendor (Opgroeien publishes
  a template "Overeenkomst tussen verantwoordelijke en verwerker"), image
  consent form for photos (beeldmateriaal toestemmingsformulier).

What to build:
- Retention classes as first-class config: each retention-relevant entity
  is tagged with a class (10y / 5y-child / 5y-staff-post-employment / 3y /
  12m) and an anchor date (event date, departure date, employment end,
  appointment date).
- Scheduled destruction job: irreversibly deletes (or anonymises, decide per
  entity at plan time) expired records; every destruction writes an audit
  entry (what class, what anchor, when destroyed — no content).
- Strafregister special rule: storing a new extract triggers destruction of
  the previous one.
- Director-facing retention report: what is scheduled for destruction, what
  was destroyed.
- Legal-hold override (e.g. ongoing dispute) with reason, per record.

Relationship to 031 (photo lifecycle): 031 stays photo-specific (storage
tiering, RBAC, parent downloads); 038 provides the generic retention engine.
If 031 is built after 038, its retention part should use 038's engine.

Key constraints:
- Destruction must be tenant-scoped and provably complete (including GCS
  objects via lifecycle/delete, and excluded from backups' restore playbook —
  document the backup caveat honestly in the spec).
- Never auto-destroy records under an active legal hold.
- i18n keys everywhere.
```

---

### 039 — Tenant Onboarding & Supplier-Switch Migration

```
Productise KDV onboarding so that moving from another software supplier
(or from paper/spreadsheets) to ChildCare is ultra simple.

Competitive context (verified 2026-07-15): Bitcare's "BOP" onboarding is a
SERVICE — three guided human sessions (1: settings + reading in a
kind/parent list; 2: contract template + planning + tablet walkthrough,
then 1–2 weeks shadow-running; 3: side-by-side first invoicing), then
helpdesk/videos. D-Care offers personal onboarding similarly. Neither
productises migration as self-service tooling — that is the opportunity.

What to build:
1. Guided setup wizard (director-facing, resumable checklist):
   organisation → locations → groups → BKR config → closure calendar →
   staff + PINs → contract templates/rates → invoice settings. Progress
   indicator; each step links to the existing screens (nothing new behind
   the wizard, it orchestrates what features 001–014 already provide).
2. Bulk import with dry-run:
   - CSV/Excel import for: children + family contacts, contracts
     (days/rates/dates), staff, waiting-list entries.
   - Column-mapping UI (source files come from D-Care, Bitcare, or
     arbitrary spreadsheets — do NOT assume a fixed layout).
   - Dry-run validation with a full preview + per-row errors BEFORE
     anything is written; import is transactional per entity type.
   - Import report stored (what was created, skipped, failed).
3. Shadow-run support:
   - A tenant-level "shadow mode" flag: everything works, but parent
     notifications/emails are suppressed so the team can practise with
     real data for 1–2 weeks without spamming parents. Clear banner.
   - One-click "go live" lifts the flag (and optionally sends the parent
     invitation emails in bulk at that moment).
4. Full tenant export (the other direction — trust + GDPR portability):
   - Director can export all tenant data (children, contacts, contracts,
     attendance, events, invoices, documents index) as CSV/JSON zip.
   - This is deliberate: being easy to LEAVE is a selling point and keeps
     us honest; it also satisfies GDPR data-portability expectations.

Key constraints:
- Import must be idempotent-safe: re-running a corrected file must not
  duplicate children/contracts (match on natural keys within the tenant,
  e.g. name + birthdate for children — confirm strategy at plan time).
- Contract imports must pass the same validators as manual entry
  (split-location day-overlap, BKR interactions) — no backdoor.
- Multi-tenancy: import/export strictly tenant-scoped; exports via signed
  GCS URLs.
- i18n keys everywhere.

Open questions (resolve before /speckit-plan):
- Obtain real export files from D-Care and Bitcare (a prospective client
  switching over can supply theirs) to build the default mapping presets —
  do NOT invent their column layouts.
- Whether historical data (past attendance, old invoices) is in scope for
  import or only current-state data. Recommendation: current-state only
  for MVP; history import as a later increment.
```

---

### 040 — Occupancy & Placement Planning

```
Build forward-looking occupancy and placement planning — the director's
answer to "can I take this child?" and "where are my future gaps?".

Competitive context: this is a Bitcare kindplanning headline capability
(direct insight into available places; automatic BKR check on placement;
"soepele doorstroom" between groups). Our backlog covers TODAY's occupancy
(018) and the waiting list (012a), but not forward planning.

What to build:
1. Forward occupancy view:
   - Per group and location: contracted children per weekday, projected
     forward per day/week/month/year (driven by contract start/end dates
     and known age-based group transitions).
   - Free places per weekday = licensed capacity (and BKR-feasible
     capacity given planned staff) minus contracted children.
2. Placement simulation:
   - Director enters a hypothetical child (birthdate, desired days, start
     date) — e.g. from a waiting-list entry (012a) or a phone enquiry —
     and gets an immediate answer: possible in which group from which
     date, and the BKR/capacity impact.
   - One click converts an accepted simulation into a contract draft (007).
3. Doorstroom (group-transition) planning:
   - Age-based transition rules per location (e.g. babies → peuters at X
     months, configurable).
   - Timeline of upcoming transitions; each transition frees a place in
     the source group — surfaced in the forward occupancy view so intake
     can be planned against future, not just current, capacity.
   - Director confirms/adjusts each proposed transition (no automatic
     moves).

Key constraints:
- Read-model heavy: all computed from contracts (007), groups/BKR config
  (004), staff scheduling (012) — no new source-of-truth tables beyond
  transition rules and confirmed transition records.
- BKR logic must reuse the exact enforcement code from 010 — one
  implementation, not a parallel calculation.
- i18n keys everywhere.

Open questions (resolve before /speckit-plan):
- Whether group transitions are formal (a new contract/amendment per 007)
  or an assignment change within the same contract — align with how 007
  models the child↔group relationship.
```

---

### 041 — BKR 2027 Ruleset

```
Make the BKR (kindratio) engine rule-versioned: the Vlaamse Regering
lowers the maximum kindratio, mandatory from 1 JANUARY 2027.

Regulatory context (from Opgroeien's "kindratio special", updated
25/06/2024, markdown copy in the working folder — VERIFY against the
original PDF before /speckit-plan, the conversion lost some bullets):
- Until 31/12/2026 the CURRENT ratio applies (transition period):
  solo caregiver max 8; 2+ caregivers present: per-caregiver ratio;
  rest moment max 14 per caregiver for max 2 consecutive hours with 2+
  caregivers actively supervising.
- From 01/01/2027 the NEW lower ratio is mandatory for everyone
  (VERIFIED 2026-07-15 from the original kindratio special PDF, p.4):
  Groepsopvang — ratio assessed at LOCATION level; the opvang chooses
  its leefgroep layout:
    * leefgroep with only babies (up to 12 months): 1 begeleider per max 5
    * leefgroep with only children > 12 months:     1 begeleider per max 8
    * mixed leefgroep:                              1 begeleider per max 7
    * rest moment: max 14 per kinderbegeleider, max 2 consecutive hours,
      with 2+ kinderbegeleiders present providing active supervision
    * maximum leefgroep size stays 18
  Gezinsopvang: target max 4 children per onthaalouder on a quarterly
  basis; max 7 present simultaneously.
  Locations MAY adopt the new ratio earlier (linked to a higher
  basissubsidie: 60% automatic, 40% on application when the new ratio is
  demonstrably met).
- Assessment method for the 40% subsidy: Opgroeien evaluates the ratio
  at LOCATION level as an AVERAGE over staffing hours vs child-presence
  hours across the calendar year — not moment-by-moment.
- Countable staffing profiles (besides qualified kinderbegeleiders):
  kinderbegeleider in an actief kwalificerend traject (AKT) and, under
  conditions, logistieke medewerkers; unqualified persons only as an
  exception during sudden staff shortage.

What to build:
1. Rule-versioned BKR config: ratio rules get effective-date ranges;
   the enforcement in 010/012 (do NOT modify their behaviour, extend
   their config source) resolves the applicable ruleset by date.
   Per-location opt-in to apply the new ruleset before 2027.
2. Baby-group rule support: ratio dependent on group age composition
   (baby-only leefgroep vs mixed) — requires the group's age profile.
3. Staffing-profile weights: mark staff as qualified / AKT / logistiek
   and define per-ruleset which profiles count (and any caps).
4. Average-ratio report: per location + calendar year, total child
   presence hours vs total countable staffing hours, versus the target
   ratio — the evidence a director needs for the 40% subsidy request
   and for Zorginspectie during the transition.

Key constraints:
- One BKR implementation: 010's enforcement, 012's scheduling checks and
  040's simulations must all read the same versioned ruleset.
- Historic data untouched: past attendance is evaluated against the
  ruleset in force on that date.
- i18n keys everywhere.
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
- **020's scope was refined 2026-07-09** (direct product-owner request, not yet planned): the daily-report email is default-on/opt-out (auto-sent every day, unsubscribe is the only way to stop it) with a per-contact unsubscribe, not the originally-drafted opt-in digest — and it goes to *every* parent/guardian contact of the child independently (e.g. both mother and father each get their own email/unsubscribe state), not a single "primary contact." Bulk parent emails also need a document-upload/attachment capability. All of this is written into 020's prompt block above — flagging here since this changes the shape of 020's data model (an unsubscribe flag per-`Contact`, and a recipient query that fans out to every parent/guardian-role contact rather than picking one) versus what an earlier read of this backlog might have assumed.
- **Research pass 2026-07-15 (opgroeien.be crawl + competitor scan)** added features 033–041 and 014a, and amended the not-started 015/018/019. 014 was originally amended too, but it shipped the same day — its payment-link/reminders/receipts additions were extracted into 014a instead, and 014's prompt block was restored to what was actually built. Sources: opgroeien.be Organisator/Software pages, Ouders-en-kinderen page, Veiligheid page, the IKT webservice cookbook PDF, the kinderopvangtoeslag webservice PDF, daycare-solutions.be, bitcare.com. Done features were deliberately left untouched.
- **Government integration surface (verified 2026-07)** — despite Opgroeien's email stating "no public APIs for external developers", the Organisator → Software page publishes real contracts: (1) IKT SOAP webservice (WSDL test `https://tstarws.kindengezin.be/kinderopvang-ikgwebservices/service/ikg.wsdl`, prod `https://arws.kindengezin.be/...`; WS-Security X.509; operations MatchKind, MatchKinderen, MatchKinderenResultaat, OpvangPrestatiesResultaat; return codes 0000/0002/0003/0004/0005/9999; match opmerkingCodes 1000–1005) → feature 019. (2) Kinderopvangtoeslag REST/JSON webservice (bearer token, test Swagger `https://tstgpappr.kindengezin.be/swagger-ui.html`) → feature 033. (3) Aanwezigheidsregistratie XML/webservice → related to 019 item 4. (4) Jaarregistraties XML → feature 034. Vendor onboarding for all of these goes through software-ontwikkeling@opgroeien.be / @kindengezin.be. A software vendor can hold ONE X.509 certificate on its own KBO number covering all client organisations (confirmed in the IKT cookbook, "certificaat op naam van leverancier") — this is the architecture already assumed in 019.
- **Official contract files obtained 2026-07-15** (product owner downloaded them; commit to the repo under e.g. `docs/integrations/opgroeien/`): the six jaarregistratie XSDs FO-RE-14/15/19/28/29/30 (analysed into 034's prompt), FO-SU-05.xsd v2.3 + the "registratie aanwezigheden — beschrijving" PDF (analysed into 019 item 4), and the softwareleverancier certificate contract (analysed below). The attendance beschrijving PDF's text layer is broken — keep the PDF for reference but treat the extracted rules in 019 item 4 as the working copy.
- **Softwareleverancier certificate contract** (V5.0, 27.10.2021, verified from the official .docx): the vendor signs a contract with Opgroeien to obtain a KIND&GEZIN certificate on the vendor's own KBO number, acting for 1..n organisators. Conditions: data centre must be inside the EU and vendor + all subcontractors under GDPR; vendor must have a **verwerkersovereenkomst with every organisator** (KDV = verantwoordelijke, ChildCare = verwerker); vendor bears full liability for misuse; **Opgroeien has an audit right** at the vendor and can revoke the certificate on breach. Submission: filled + (electronically) signed form to **dpo@opgroeien.be**. Impact: the verwerkersovereenkomst must become a standard step in organisation onboarding before Phase 3 (and arguably earlier, plain GDPR) — and hosting/subprocessors (GCP europe-west1) must be documented for the audit.
- **Reference corpus in the working folder (`Claude-markdown/`, ~60 markdown conversions of official PDFs, indexed 2026-07-15)** — treat as the local source library when speccing. Highlights per feature: Belcotax/BOW technical set for 019 item 5 (`161-presentatie-bow-2-20250526.md`, `161-belcotax-bow-mappage-fiche-01-voorbeeld-nl.md`, `protocol-ogr-vutg-fiscale-attesten.md`, `161-bericht-attest-281-62-*.md`); `kindratio-kinderopvang_special.md` (041 — conversion lost the new ratio table, re-extract from the original PDF); `inkomenstarief-wijzigingen-indexering-2026-snelinfo_0.md` (confirms 019's October-indexing and 30-months kindkorting rules); `verwerkersovereenkomst-sjabloon.md` + `overeenkomst-verantwoordelijke-verwerker.md` (onboarding/039 + certificate contract obligation); `privacyverklaring-ouders-sjabloon.md` (parent privacy statement template); `brochure-jaarregistraties-kleuteropvang.md` (kleuteropvang segment variant of 034); `Vaccinaties volgens leeftijd` HTML (seed data check for 013g's vaccine catalog); `KIB_inkomenstarief_2024.md`, vergunning procedures, subsidy procedures. A `conversion_failures.log` lists PDFs that did not convert — check it before assuming a document is absent.
- **Financial reporting for subsidised organisators** (snelinfo 11/12/2025, in the corpus): all organisators must make an annual budget; organisators receiving > €200,000/year in Opgroeien subsidies (IKT parent contributions count toward this!) must keep double-entry bookkeeping and produce a financial report per opvangvorm within 7 months of closing the book year; Zorginspectie runs financial inspection rounds. Phase 3 relevance: 029 (accounting export) should tag revenue/costs per opvangvorm and per location so subsidised clients can produce this split; 018's revenue reporting can surface the €200k threshold.
- **Documents obtained 2026-07-15 (second batch)**: the original kindratio special PDF (041's full 2027 ratio table now verified), the official meldingsformulier crisissituatie .docx (036's field list now verified), the AARON sector handleiding (033: 7th-of-month deadline, fines, RRN/bisnummer validation, 2.5y toeslag expiry, opt-out periods, offline support), the Opgroeien kinderopvang FAQ page (general reference incl. the "attest aanvragen for a parent" step-by-step relevant to 019), and the huishoudelijk reglement + schriftelijke overeenkomst models — NOTE: the received models are the SCHOOLKIND (school-age) variants; structurally useful for 024, but fetch the baby's & peuters variants (huishoudelijkreglement-model.docx, schriftelijkeovereenkomst-model.docx on the Ouders-en-kinderen page) before speccing 024's templates. One structural takeaway already: the overeenkomst model has an explicit section "wijziging van het huishoudelijk reglement in het nadeel van de contracthouder" — reglement changes to the parent's disadvantage give contract-termination rights, reinforcing the need for reglement version + acknowledgment tracking (open question under 024).
- **Actielijst set complete (obtained 2026-07-15)**: all six risicoanalyse actielijsten are now in hand (binnen, omgevingsfactoren, kwaliteitsvol handelen, ziekte, verzorging, slapen) — 035's checklist seed content is fully covered; same question structure as the verified "slapen" list.
- **Document collection COMPLETE (2026-07-15)**: all previously missing sources are now in hand — the baby's & peuters huishoudelijk reglement model (16/02/2026) + schriftelijke overeenkomst model (15/04/2025) for 024 (both use a mandatory-vs-optional structure: obligatory rubrics in black, suggested content in red/blue — 024's template engine should mirror that fixed/editable split), and the AARON Swagger 2.0 definition `KinderOpvangToeslag.json` (field-level contract now written into 033). Commit all source files to `docs/integrations/opgroeien/`. Remaining open items are QUESTIONS, not documents: AARON production-token onboarding + resubmission semantics (033), FO-SU-05 webservice-vs-email choice (019), form-to-subsidy mapping + deadlines (034), PSP choice (014), reglement-acknowledgment placement (024).
- **Kinderopvangzoeker** (kinderopvangzoeker.be) has NO public API (client-rendered app; confirmed by Opgroeien's email). It is where parents see licences and inspection reports. No integration feature is warranted; at most a marketing note that directors should keep their listing current.
- **Competitor snapshot 2026-07 (updated after full bitcare.com crawl 2026-07-15)**: D-Care (daycare-solutions.be) = Flemish, touchscreen-on-the-floor + hardware-included model, parent web-app with day reports, development tracking, absences, closure days, invoices + in-app payment via POM; IKT documents and fiscal attests auto-generated. Bitcare (bitcare.com) = Dutch company (Delft) with a REAL Belgian module — its BE wetgeving sections confirm: attendance submission to Kind & Gezin, fiscal attests + **BOW (Belcotax-on-web) file generation for the FOD**, subsidy admin, and the "new Flemish kindratio" — so Bitcare already covers a chunk of our 015/019 government-reporting scope. Full Bitcare feature set from the crawl: digital enrollment form → auto-generated contract → e-signature with SEPA-mandate consent in the flow (our 023/024); waiting list + forward occupancy + BKR placement check + doorstroom (our 012a + new 040); parent request queue with push notifications (013a — done); staff planning integrated with child planning, over/under-staffing signals, clock-in/out, plus/min-hours, self-service leave/swap via the Medewerker app, role-based access, salary export via Humanwave/NL CAO (027/028); group app with per-group day view, digital diary, photos, 1-1/group chat, news posts, offline access (008/009/013 — done); invoicing from price models, one-click send, automatic reminders, betalingsbewijzen, Mollie payments, prepayment/refund handling, accounting couplings Exact/AFAS/Twinfield/MS Dynamics/SnelStart (014/025/026/029); dashboards for bezetting/in-uitstroom/contract value/revenue + a "kwaliteitsmonitor" data-completeness check (018); parent app in multiple languages; BOP onboarding = 3 human-guided sessions + 1–2 weeks shadow-running (inspiration for 039, where we productise it instead).
- **Bitcare app-quality intel (Play Store, 2026-07)**: parent app 2.7★ (177 reviews, 50K+ downloads), medewerker app 2.2★ — recurring complaints: crashes, push notifications missing or hours late, photo-download bugs, un-editable hour entries, forced re-logins. Mobile reliability (especially push timeliness and offline behaviour) is itself a competitive differentiator — treat notification-delivery observability as a first-class requirement in the parent/staff apps.
- **Remaining differentiation bets** (nothing on either competitor's public materials): Flemish risk-analysis support (035), crisis/melding workflows (036), verontrusting registration (036), retention automation (038), productised self-service migration + full data export (039), caregiver PIN/kiosk model (008a), NL/FR/EN from day one. D-Care's public site remains thin; a demo is still needed for a real feature-by-feature comparison.
- **Huishoudelijk reglement acknowledgment** (parents must sign for kennisname of the reglement and of changes; schriftelijke overeenkomst signed for akkoord) — currently only partially covered by 024 (e-signature on contracts). OPEN QUESTION for the product owner: extend 024 to cover reglement versioning + per-parent acknowledgment tracking, or add a separate document-management feature.
- **Flag for 005 (Staff), 007 (Contracts), 012 (Caregiver Scheduling)**: feature 004 (Locations) deliberately does not build any "move/relocate a location" continuity — when a KDV physically relocates (old building closes, new one opens), 004 only offers a "duplicate location" convenience (clones location-level settings, no data carryover). Whoever builds 005/007/012 needs to design the actual staff/child reassignment UX for a location closing down, keeping in mind staff are NOT bound to a single location (a caregiver can already work different locations on different days per 012's scheduling model) — so "moving" a location is really a bulk reassignment of active contracts (007) and future schedule entries (012), not a 1:1 staff/child transfer.
