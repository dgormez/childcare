<!--
Sync Impact Report
==================
Version change: 1.3.0 → 1.4.0 (MINOR — existing guidance materially expanded)

Modified sections:
- II. Regulatory Compliance by Design (NON-NEGOTIABLE) — added a "Regulation is time-versioned"
  clause. The BKR ratio thresholds this principle lists are the CURRENT (pre-2027) regime; the
  Vlaamse Regering has enacted a lower kindratio that becomes mandatory on 1 January 2027
  (verified 2026-07-15 from Opgroeien's official kindratio special, archived in
  docs/integrations/opgroeien/regulation/): baby-only leefgroep (≤12 months) 1:5, only >12
  months 1:8, mixed leefgroep 1:7; rest-moment and 18-cap unchanged; ratio assessed at location
  level; early adoption allowed per location and subsidy-linked. Regulatory thresholds must
  therefore be treated as effective-dated configuration, not constants — BACKLOG feature
  041-bkr-2027-ruleset implements the versioning; until it ships, the current thresholds remain
  the only enforced regime and this clause is informational for planning.
- Added a pointer that docs/integrations/opgroeien/ is the authoritative in-repo source library
  for government/regulatory contracts (XSDs, Swagger JSON, official document models) — specs for
  features 015/019/033–041 must cite those files rather than re-deriving rules from memory.

Trigger: 2026-07-15 market/regulatory research pass (Opgroeien crawl + official documents
supplied by the product owner) added features 033–041 and 014a to BACKLOG.md and surfaced the
2027 kindratio change, which contradicts a literal reading of Principle II's threshold list as
timeless.

Added sections: none (additive clause within an existing principle)

Removed sections: none

Templates requiring updates:
- .specify/templates/plan-template.md — ✅ compatible as-is (Constitution Check reads gates
  dynamically from this file)

Follow-up TODOs: none. RATIFICATION_DATE unchanged; LAST_AMENDED_DATE updated.
-->

<!--
Sync Impact Report (previous)
==================
Version change: 1.2.1 → 1.3.0 (MINOR — existing guidance materially expanded)

Modified sections:
- II. Regulatory Compliance by Design (NON-NEGOTIABLE) — added a "Carve-out (leefgroep ratio,
  pending group-type data model)" clause. The BKR ratio thresholds (solo max 8; 2+ caregivers max
  9/caregiver; nap time max 14; leefgroep max 18) were previously listed as a single block this
  principle requires enforcing; the leefgroep cap specifically cannot be enforced yet because no
  feature to date has given `Group`/`Location` any way to be flagged as a leefgroep versus a
  standard room — there is no data for the system to branch on. The other three thresholds are
  unaffected and remain required.

Trigger: /speckit-plan on feature `010-attendance` found the Constitution Check gate would
otherwise fail — feature 010 implements live BKR computation but, per its own clarification
session, explicitly does not implement the leefgroep regime (no group-type distinction exists to
implement it against). Per the precedent set by the 1.1.0 and 1.2.0 amendments (codify the
exception here rather than bend it via an undocumented plan-level justification), this amendment
records the carve-out formally instead.

Added sections: none (this is an additive carve-out clause within an existing principle, not a
new principle)

Removed sections: none

Templates requiring updates:
- .specify/templates/plan-template.md — ✅ compatible as-is (Constitution Check section reads
  gates dynamically from this file; no edits needed)

Follow-up TODOs: none. RATIFICATION_DATE unchanged; LAST_AMENDED_DATE updated to this amendment's
date.
-->

<!--
Sync Impact Report (previous)
==================
Version change: 1.2.0 → 1.2.1 (PATCH — wording/documentation-accuracy fix, no semantic change)

Modified sections:
- Development Workflow & Phase Discipline, `child_events` bullet — the parenthetical event-type
  list is updated from `measurement` to `growth_check` (renamed) plus a new `custom` type, per
  feature `009a-child-events-custom-type`. Purely descriptive text (examples of what lives in
  the single JSONB-backed table) — the rule itself ("single table, no per-type table") is
  unchanged.

Trigger: feature 009a's own tasks.md (Polish phase) flagged this bullet as stale once the
`measurement` -> `growth_check` rename and the new `custom` type shipped, per the standing
practice (see 1.2.0/1.1.0 entries below) of keeping this file's descriptive examples in sync
with what's actually implemented rather than letting them silently drift.

Added sections: none

Removed sections: none

Templates requiring updates: none (no template references this bullet's specific event-type list)

Follow-up TODOs: none. RATIFICATION_DATE unchanged; LAST_AMENDED_DATE updated to this
amendment's date.
-->

<!--
Sync Impact Report (previous)
==================
Version change: 1.1.0 → 1.2.0 (MINOR — existing guidance materially expanded)

Modified sections:
- Technology Stack Constraints, "Auth" bullet — the "Caregiver app:
  email/password only (employer-provisioned)" line is superseded by feature
  `008a-caregiver-kiosk-mode`'s room-tablet model: a director still pairs a
  tablet via email/password (feature 008's existing flow, unchanged) to
  obtain a long-lived, revocable device token, which is the tablet's actual
  security boundary; individual caregivers on that tablet then identify via
  a 4-digit PIN checked against a server-side shift-presence log, which is
  accountability/attribution tracking, not a second HTTP authentication
  mechanism. Email/password remains real and in use — it just no longer
  happens per caregiver per shift, only once per tablet at setup time.

Trigger: /speckit-plan on feature 008a found the Constitution Check gate
would otherwise fail against stale stack-constraint text, the same class of
issue the 1.1.0 amendment addressed for feature 001 — codifying the change
here rather than bending it via an undocumented plan-level justification.

Added sections: none (this is a clarifying rewrite of one existing bullet,
not a new principle)

Removed sections: none

Templates requiring updates:
- .specify/templates/plan-template.md — ✅ compatible as-is
- specs/008-caregiver-app-scaffold/plan.md — not updated (out of scope,
  already merged; its own auth description was accurate for what it shipped
  — feature 008's email/password flow is unchanged, only extended by 008a)

Follow-up TODOs: none. RATIFICATION_DATE unchanged; LAST_AMENDED_DATE
updated to this amendment's date.
-->

<!--
Sync Impact Report (previous)
==================
Version change: 1.0.0 → 1.1.0 (MINOR — existing guidance materially expanded)

Modified principles:
- I. Multi-Tenant Isolation (NON-NEGOTIABLE) — added a "Carve-out
  (provisioning-only features)" clause, exempting features with zero
  tenant-data-read endpoints from the TenantMiddleware/ICurrentTenantService
  requirement, naming feature 001-organisation-onboarding as the qualifying
  case. Core rule unchanged; this narrows scope, it does not weaken it — the
  exemption explicitly ends once any endpoint reads tenant domain data.
- VI. Secure Configuration & Storage — added a "Carve-out (new-tenant-schema
  provisioning)" clause, exempting brand-new empty tenant schema provisioning
  from the migrations-MUST-NOT-auto-apply rule. Migration content is still
  authored/reviewed as normal code; only auto-application to a schema with
  zero prior data is exempted. Rolling reviewed migrations out to *existing*
  tenant schemas remains outside this exemption.

Trigger: /speckit-analyze on feature 001-organisation-onboarding flagged both
readings as CRITICAL findings (D1, D2) — bending NON-NEGOTIABLE-adjacent
principle text via ad hoc plan-level justification, rather than a codified
constitution exception, was assessed as insufficient. This amendment resolves
both by codifying the exceptions explicitly, at the user's direction.

Added sections: none (both changes are additive clauses within existing
principles, not new principles/sections)

Removed sections: none

Templates requiring updates:
- .specify/templates/plan-template.md — ✅ compatible as-is (Constitution Check
  section reads gates dynamically from this file; no edits needed)
- .specify/templates/spec-template.md — ✅ compatible as-is (no constitution-specific
  references)
- .specify/templates/tasks-template.md — ✅ compatible as-is (no constitution-specific
  references)
- No command files present under .specify/templates/commands/
- specs/001-organisation-onboarding/plan.md — ⚠ pending manual update: its
  Constitution Check table (lines ~37, 42) and Complexity Tracking section
  should be updated to reflect that Principles I and VI are now clean passes
  under the codified carve-outs, not "Partial, justified" bends of an
  uncodified reading. Not updated by this command (constitution-only scope).

Follow-up TODOs:
- None. RATIFICATION_DATE unchanged (original adoption); LAST_AMENDED_DATE
  updated to this amendment's date.
-->

# ChildCare Constitution

## Core Principles

### I. Multi-Tenant Isolation (NON-NEGOTIABLE)

Cross-tenant data leakage MUST be structurally impossible, not merely
prevented by convention. Tenant = Organisation. Every tenant gets its own
PostgreSQL schema (schema-per-tenant); `PublicDbContext` holds only the
shared `tenants` table, `TenantDbContext` holds all domain data, and
`TenantMiddleware` MUST resolve the tenant from the JWT `tenant_id` claim
and set `search_path` before any query executes. Connections MUST be
direct (non-pooled / no pgBouncer transaction-mode pooling) so
`search_path` is never reset mid-session. A single owner's locations
share one tenant schema. Any code path that queries domain data without
going through the tenant-scoped context is a defect, regardless of
whether it would leak data in practice today.

**Carve-out (provisioning-only features)**: A feature that exposes zero
endpoints reading existing tenant domain data is exempt from the
`TenantMiddleware`/`ICurrentTenantService` requirement, provided every
write it performs targets only the single tenant schema it just created
within that same operation. Feature `001-organisation-onboarding`
qualifies: it writes to the shared `tenants` table and provisions a new
tenant schema, but has no endpoint of any kind that reads tenant domain
data. `TenantMiddleware` is built in feature `002-multi-tenancy-scaffold`.
This exemption ends the moment any feature adds an endpoint that reads
tenant domain data — at that point `TenantMiddleware` MUST already exist
and be wired in before that endpoint ships.

**Rationale**: Belgian KDVs handle sensitive data on minors (medical
notes, authorised pickups). A tenant-isolation bug is not a bug class we
can afford to discover in production. The carve-out exists because
requiring `TenantMiddleware` before it has a reason to exist would force
either building it prematurely (risking a rushed, throwaway version that
outlives its intended lifespan) or blocking the one feature that must
come first sequentially (organisation onboarding creates the tenants
`TenantMiddleware` will later resolve).

### II. Regulatory Compliance by Design (NON-NEGOTIABLE)

Belgian childcare regulations MUST be enforced in the Application/Domain
layer, never only in UI validation. This includes: BKR
(begeleider-kind-ratio) caregiver-to-child ratios (solo max 8; 2+
caregivers max 9/caregiver; nap time max 14; leefgroep max 18); the
split-location day-overlap validator that runs on every contract
activation when a child holds two simultaneous contracts at different
locations of the same organisation; and closure-calendar notification
rules. A feature that only checks a regulatory constraint client-side is
incomplete.

**Carve-out (leefgroep ratio, pending group-type data model)**: The
leefgroep (living group) 18-child cap is not yet enforceable — no
feature to date has introduced a way to flag a `Group`/`Location` as a
leefgroep versus a standard room, so the system has no data to decide
which cap applies where. Feature `010-attendance` implements and
enforces the other three thresholds (solo max 8; 2+ caregivers max
9/caregiver; nap time max 14, inferred from open sleep events) but does
not implement the leefgroep cap. This exemption ends the moment a future
feature adds a group-type distinction to the data model — at that point
the leefgroep cap MUST be enforced for any group flagged as such.

**Regulation is time-versioned**: The thresholds listed above are the
CURRENT (pre-2027) regime. A lower kindratio becomes mandatory on
1 January 2027 (baby-only leefgroep ≤12m 1:5; only >12m 1:8; mixed
leefgroep 1:7; rest-moment max 14 and the 18-cap unchanged; assessed at
location level; per-location early adoption allowed and subsidy-linked —
source: docs/integrations/opgroeien/regulation/kindratio-kinderopvang_special.pdf).
Regulatory thresholds are therefore effective-dated configuration, not
constants; feature `041-bkr-2027-ruleset` implements the versioning.
Until 041 ships, the current thresholds above remain the only enforced
regime — this clause changes planning posture, not today's gates. More
broadly, `docs/integrations/opgroeien/` (see its README) is the
authoritative in-repo source library for government contracts and
official document models; specs for features 015/019/033–041 MUST cite
those files rather than re-derive regulatory rules from memory.

**Rationale**: Erkenning (the operating licence) depends on these ratios
being genuinely enforced. UI-only checks can be bypassed by direct API
calls or bugs, risking the customer's licence. The leefgroep carve-out
targets a genuinely different problem than the rule it modifies: unlike
the other three thresholds (computable today from existing check-in and
staff-qualification data), leefgroep enforcement requires a data-model
concept — which rooms are leefgroepen — that does not exist anywhere yet.
Inventing a speculative `Group.IsLeefgroep` flag now, with no feature
ready to set or consume it correctly, risks exactly the kind of premature,
throwaway field Principle I's own tenant-isolation carve-out reasoning
warns against.

### III. CQRS via MediatR & Thin Endpoints

All write operations MUST go through MediatR commands; complex reads
MUST go through MediatR queries; only simple, single-entity lookups may
query directly. FluentValidation runs as a MediatR pipeline behaviour and
MUST fire before every command handler executes. Endpoint files
(`*Endpoints.cs`) MUST contain no business logic — they map HTTP to
MediatR requests and map results back to HTTP responses, nothing else.

**Rationale**: Keeping business logic out of endpoint handlers keeps it
testable, reusable across the three client apps, and reviewable in one
place instead of scattered across route handlers.

### IV. Internationalization First (NON-NEGOTIABLE)

No user-facing string may be hardcoded anywhere in the codebase — web,
mobile, or API. Dutch, French, and English MUST all be supported from
day one via locale keys (`next-intl` on web, `expo-localization` +
`react-i18next` on mobile). API error messages and validation responses
MUST return locale-aware keys, not raw text, for the client to resolve.
C# identifiers (types, members, namespaces) MUST be English only — no
Dutch or French words or acronyms in code, even where the domain term is
Belgian/Flemish (e.g., use `Ratio`/`Group`, not `Bkr`/`Leefgroep`, as
identifier names; Dutch/French terms belong in documentation and
translation resources, not code).

**Rationale**: The product serves Flemish (NL), Walloon (FR), and
international (EN) families and staff simultaneously; retrofitting i18n
after strings are hardcoded is expensive and error-prone.

### V. Test with Real Infrastructure (NON-NEGOTIABLE)

Integration and API tests MUST run against TestContainers-provisioned
PostgreSQL, never EF Core's InMemory provider. InMemory does not enforce
schema constraints, `search_path` behaviour, or PostgreSQL-specific
features (e.g., JSONB queries on `child_events`), so it hides bugs that
only appear against a real database. Not every code path needs a test —
focus coverage on the happy path plus the key negative/regulatory
flows (BKR limits, tenant isolation, contract overlap) for each feature.

**Rationale**: This project already migrated away from InMemory once
(see `AuthEndpointTests.cs` follow-up); regressing back to it would
silently reintroduce the same class of false-positive tests.

### VI. Secure Configuration & Storage

Secrets and connection strings MUST never be hardcoded — they come from
environment variables or a secrets manager (GCP Secret Manager /
GitHub-managed keyvault via Terraform), never committed to the repo.
File access MUST use signed, time-limited GCP Cloud Storage URLs — no
public blob URLs. EF Core migrations MUST NOT auto-apply in production;
a SQL script is generated and reviewed/run manually. Internal errors and
stack traces MUST NOT be exposed to end users — return a clear,
human-readable, localized message and log the full error server-side
regardless of environment.

**Carve-out (new-tenant-schema provisioning)**: The migrations-MUST-NOT-
auto-apply rule does not apply when provisioning a brand-new tenant
schema as part of organisation onboarding (feature
`001-organisation-onboarding`). A newly created schema holds no prior
data, so there is nothing an auto-applied migration could corrupt, and
the whole point of self-service onboarding is that no operator manually
runs a script per registration. The migration **content** is still
authored via normal EF Core migration files and reviewed in the PR like
any other code change — only its *application* to that one brand-new,
empty schema happens automatically. Rolling an already-reviewed
migration out to *existing* tenant schemas remains a deliberate,
explicit operation, not something any feature auto-applies blindly.

**Rationale**: Childcare data includes medical notes and minors' PII;
config and storage practices default to the safer option even at some
convenience cost during development. The provisioning carve-out targets
a structurally different risk than the rule it modifies: a blind
auto-migrate against a shared, populated production schema (what the
rule prevents) versus applying already-reviewed SQL to a schema with
zero rows and zero blast radius beyond the one tenant being created.

### VII. Monolith-First Simplicity

The system is one deployable ASP.NET Core API (Minimal APIs, no
Controllers) serving three clients (web admin, caregiver app, parent
app) from five projects: `ChildCare.Api`, `ChildCare.Application`,
`ChildCare.Domain`, `ChildCare.Infrastructure`, `ChildCare.Contracts` —
no more, no fewer, without a documented reason. No YARP, no
microservices, no additional services until a proven need exists. All
code uses the `ChildCare` name — never `Kdv`, `KdvPlatform`, or other
Dutch acronyms in namespaces, projects, or the solution.

**Rationale**: A solo/small-team build on a new product benefits far
more from a simple, single deployable than from premature service
boundaries; splitting later is cheaper than paying distributed-systems
tax now.

## Technology Stack Constraints

The stack below is fixed for Phase 1–3 unless a principle-level amendment
records a change:

- **Backend**: .NET 10 / C#, Minimal APIs, EF Core 9, PostgreSQL 16.
- **Auth**: ASP.NET Core Identity + JWT, per-device refresh token
  rotation. Parent app: Google OAuth + Apple Sign-In + email/password.
  Caregiver app: room-tablet model (feature 008a) — a director pairs a
  tablet once via email/password (feature 008's flow, unchanged) to obtain
  a long-lived, revocable device token that is the tablet's actual
  security boundary; individual caregivers then identify via a 4-digit PIN
  checked against a server-side shift-presence log — accountability
  tracking, not a second HTTP authentication mechanism. Web admin:
  email/password + Google OAuth.
- **PDF**: QuestPDF (MIT licence) — no other PDF library.
- **Push notifications**: Expo Push Notification Service.
- **Storage**: GCP Cloud Storage, signed URLs only.
- **API docs**: Scalar, dev-only (`/scalar/v1`).
- **Client generation**: openapi-typescript + openapi-fetch — no NSwag.
- **Web admin**: Next.js (App Router), TypeScript, Tailwind, shadcn/ui.
- **Caregiver / parent apps**: Expo (React Native); caregiver app is
  tablet/landscape, parent app is phone/portrait.
- **Infrastructure**: GCP project `childcare-501020`,
  `europe-west1`; Cloud Run (scale-to-zero); Artifact Registry;
  Terraform (`infra/gcp/`); GitHub Actions CI/CD (push to `master` →
  build → push image → deploy).
- **Database by environment**: local dev = Docker PostgreSQL; CI =
  TestContainers; deployed pre-revenue = Neon free tier (Frankfurt);
  deployed post-revenue = Cloud SQL `db-f1-micro`.

## Development Workflow & Phase Discipline

- **Phase scoping**: Phase 1 covers private KDVs only — child profiles,
  contracts, daily tracking (`child_events`), attendance, caregiver
  scheduling, parent communication, closure calendar, and basic
  invoicing. MeMoQ quality tracking, fiscal attestations, developmental
  milestones, and management reporting are Phase 2. IKT subsidy
  integration and advanced financial admin are Phase 3. Features from a
  later phase MUST NOT be built ahead of schedule without an explicit
  decision to re-scope, since IKT in particular adds regulatory
  complexity that is intentionally deferred.
- **`child_events`**: daily tracking events (sleep, temperature,
  medication, feeding, diaper, mood, activity, note, weight, growth_check,
  custom) live in a single JSONB-backed table — do not create a separate
  table per event type.
- **Branching**: solo-developer workflow per user's global conventions —
  work happens directly on `master`; `release/x.x` branches cut
  production releases; `develop/x.x` branches are used when needed.
- **Commit discipline**: commit messages describe intent and the change
  across all affected files, not a file listing.
- **Legacy cleanup**: the walking skeleton's `AppDbContext`
  (Users/Habits) and `HabitEndpoints.cs` are known template leftovers to
  be replaced/removed as part of the tenancy work — do not extend them.

## Governance

This constitution supersedes ad hoc practice for the ChildCare codebase.
All feature plans MUST pass the Constitution Check gate (see
`plan-template.md`) before Phase 0 research begins, and MUST re-check
after Phase 1 design; any violation MUST be justified in the plan's
Complexity Tracking section or the plan MUST be revised to comply.

**Amendment procedure**: Amendments are proposed as edits to this file.
Any addition, removal, or redefinition of a principle MUST update the
Sync Impact Report at the top of this file and MUST be evaluated against
the templates in `.specify/templates/` for consistency before landing.

**Versioning policy** (semantic versioning for governance):
- **MAJOR** — backward-incompatible principle removal or redefinition.
- **MINOR** — a new principle or section added, or existing guidance
  materially expanded.
- **PATCH** — wording, clarification, or typo fixes with no semantic
  change.

**Compliance review**: Every feature spec and plan is expected to be
checked against Principles I (tenant isolation) and II (regulatory
compliance) explicitly, since these are the two categories most likely
to cause customer-facing (licensing or data-leak) harm if violated.

**Version**: 1.4.0 | **Ratified**: 2026-07-02 | **Last Amended**: 2026-07-15
