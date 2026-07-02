<!--
Sync Impact Report
==================
Version change: [TEMPLATE] → 1.0.0 (initial ratification)

Modified principles: N/A (first fill of the template)

Added sections:
- Core Principles I–VII (Multi-Tenant Isolation, Regulatory Compliance by Design,
  CQRS via MediatR & Thin Endpoints, Internationalization First, Test with Real
  Infrastructure, Secure Configuration & Storage, Monolith-First Simplicity)
- Technology Stack Constraints (Section 2)
- Development Workflow & Phase Discipline (Section 3)
- Governance

Removed sections: none (template placeholders only)

Templates requiring updates:
- .specify/templates/plan-template.md — ✅ compatible as-is (Constitution Check
  section reads gates dynamically from this file; no edits needed)
- .specify/templates/spec-template.md — ✅ compatible as-is (no constitution-specific
  references)
- .specify/templates/tasks-template.md — ✅ compatible as-is (no constitution-specific
  references)
- No command files present under .specify/templates/commands/

Follow-up TODOs:
- None. RATIFICATION_DATE set to the date this constitution was first adopted.
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

**Rationale**: Belgian KDVs handle sensitive data on minors (medical
notes, authorised pickups). A tenant-isolation bug is not a bug class we
can afford to discover in production.

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

**Rationale**: Erkenning (the operating licence) depends on these ratios
being genuinely enforced. UI-only checks can be bypassed by direct API
calls or bugs, risking the customer's licence.

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

**Rationale**: Childcare data includes medical notes and minors' PII;
config and storage practices default to the safer option even at some
convenience cost during development.

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
  Caregiver app: email/password only (employer-provisioned). Web admin:
  email/password + Google OAuth.
- **PDF**: QuestPDF (MIT licence) — no other PDF library.
- **Push notifications**: Expo Push Notification Service.
- **Storage**: GCP Cloud Storage, signed URLs only.
- **API docs**: Scalar, dev-only (`/scalar/v1`).
- **Client generation**: openapi-typescript + openapi-fetch — no NSwag.
- **Web admin**: Next.js (App Router), TypeScript, Tailwind, shadcn/ui.
- **Caregiver / parent apps**: Expo (React Native); caregiver app is
  tablet/landscape, parent app is phone/portrait.
- **Infrastructure**: GCP project `kindergartenmanager`,
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
  medication, feeding, diaper, mood, activity, note, weight, measurement)
  live in a single JSONB-backed table — do not create a separate table
  per event type.
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

**Version**: 1.0.0 | **Ratified**: 2026-07-02 | **Last Amended**: 2026-07-02
