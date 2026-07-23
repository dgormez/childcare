# Implementation Plan: Platform-Admin Portal — Invitations, Registration & Organisation Directory

**Branch**: `032-platform-admin-portal` | **Date**: 2026-07-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/032-platform-admin-portal/spec.md`

## Summary

Give a platform-admin (an existing director account flagged `IsPlatformAdmin`, feature 013h) a
real, in-app way to onboard new KDV customers end-to-end: create/list/resend/revoke director
invitations (extending feature 001's existing `Invitation`/token model, which today only has a
create path gated by an unrelated ops API key); a new public, unauthenticated director-web page
that finally consumes feature 001's already-existing `POST /api/organisations/register`
endpoint (no page for it exists anywhere today); and a read-only directory of every organisation
already on the platform, answerable entirely from the existing `Tenant` (Public schema) +
`Invitation` join with no per-tenant-schema fan-out. All three capabilities, plus 013h's existing
standalone vaccine-catalog screen, are retrofitted into one shared platform-admin nav
shell/section in `web/`.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Next.js 15 App Router (web)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Tailwind, shadcn/ui, next-intl, openapi-fetch (web) — all per the constitution's
fixed Technology Stack Constraints, no new dependency introduced by this feature.

**Storage**: PostgreSQL 16, Public schema only (`PublicDbContext.Invitations`,
`PublicDbContext.Tenants` — read-only for `Tenants`). No tenant-schema (`TenantDbContext`)
changes.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per Principle V — no
InMemory provider); Jest + React Testing Library (web component tests).

**Target Platform**: Existing Cloud Run deployment (backend), existing Next.js web deployment —
no new deployable, no new infrastructure.

**Project Type**: Web application (existing monorepo: `backend/`, `web/`) — this feature touches
only these two, no mobile app impact.

**Performance Goals**: N/A — an internal operations tool (invitations, directory) plus a
low-volume public form (registration), not a high-traffic path.

**Constraints**: None beyond the constitution's standing ones (see Constitution Check below).

**Scale/Scope**: Small — a handful of new endpoints, one new migration adding 4 columns to one
existing table, 3 new web screens (Invitations, Organisations, a public Registration page) plus
a shared shell/nav retrofit.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | **PASS** | Every new endpoint (invitations, directory) reads/writes `PublicDbContext` only — no tenant-schema access, so no `ICurrentTenantService`/tenant-scoping concern arises. The authenticated request still carries a normal `tenant_id` JWT claim (the platform-admin is a real director of some tenant) and `TenantMiddleware` still runs normally; these handlers simply don't consume tenant domain data, mirroring 013h's already-passing `PlatformAdminVaccineTypeEndpoints` precedent exactly. The registration page's backend endpoint (`POST /api/organisations/register`) already exists, already `RequireTenantExempt()`, and is unchanged by this feature — feature 001 already passed this gate for it. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | **N/A** | No BKR ratios, contracts, or other regulated childcare data touched by this feature. |
| III. CQRS via MediatR & Thin Endpoints | **PASS** | New writes (`CreatePlatformAdminInvitationCommand`, `ResendPlatformAdminInvitationCommand`, `RevokePlatformAdminInvitationCommand` — all distinct from the existing, unchanged `CreateInvitationCommand` the out-of-scope `SuperAdmin` ops-key path uses) and reads (`ListPlatformAdminInvitationsQuery`, `ListPlatformAdminOrganisationsQuery`) all go through MediatR; new endpoint files map HTTP↔MediatR only, mirroring `PlatformAdminVaccineTypeEndpoints.cs`'s existing shape. |
| IV. Internationalization First (NON-NEGOTIABLE) | **PASS** | The Invitations/Organisations director-web screens use standard `next-intl` locale keys like every other director-web screen. The public registration page follows the existing public-enrollment page's pattern (feature 023): its own nested per-locale message loading + in-page language toggle, defaulting to Dutch, since the visitor has no stored locale yet. The invitation email itself is sent in a platform-admin-selected locale (nl/fr/en, default nl) — a genuinely new send path, so it does NOT reuse feature 020's documented "accepted English-only gap" (that gap was explicitly scoped to pre-020 flows, not new ones). |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | **PASS** | All new backend tests run against TestContainers Postgres via this codebase's existing test fixtures — no InMemory provider. |
| VI. Secure Configuration & Storage | **PASS** | No secrets introduced. The new `Invitation` migration is Public-schema (applies once, reviewed/deployed like any other migration — not the tenant-schema auto-apply carve-out, which doesn't apply here). No file storage in this feature. Acting-user resolved server-side only (never client-supplied), matching 013h's `ActingUserOf` pattern — applied to both creation and revoke attribution (research.md R12, a gap `/speckit-checklist` caught and this plan now includes). `POST /api/organisations/register` gains rate limiting (research.md R13) since this feature is what first makes it genuinely publicly reachable — also a checklist-driven fix, not part of the original draft. |
| VII. Monolith-First Simplicity | **PASS** | No new backend project, no new web app, no new service. Everything lives in the existing five backend projects and the existing `web/` app. |

No violations — Complexity Tracking section is empty (omitted).

## Project Structure

### Documentation (this feature)

```text
specs/032-platform-admin-portal/
├── plan.md              # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── platform-admin-portal-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/
│   └── Invitation.cs                              # extend: OrganisationNameNote, Locale,
│                                                     RevokedByUserId/Email/At
├── ChildCare.Infrastructure/Persistence/
│   ├── PublicDbContext.cs                          # map new Invitation columns
│   └── Migrations/Public/                          # new migration
├── ChildCare.Application/
│   ├── Invitations/
│   │   ├── CreatePlatformAdminInvitationCommand.cs   # new, distinct from the existing
│   │   │                                              # CreateInvitationCommand (unchanged) —
│   │   │                                              # that one still backs the out-of-scope
│   │   │                                              # SuperAdmin ops-key endpoint; extending it
│   │   │                                              # would leak PlatformAdminOnly-specific
│   │   │                                              # fields into that unrelated path
│   │   ├── ListPlatformAdminInvitationsQuery.cs      # new — derives Pending/Accepted/Expired/Revoked
│   │   ├── ResendPlatformAdminInvitationCommand.cs   # new — mirrors ResendStaffInvitationCommand
│   │   ├── RevokePlatformAdminInvitationCommand.cs   # new
│   │   └── OrganisationInvitationLinkBuilder.cs      # new — mirrors EnrollmentLinkBuilder
│   ├── Organisations/
│   │   └── ListPlatformAdminOrganisationsQuery.cs    # new — Tenant + Invitation join, read-only
│   └── Common/IEmailSender.cs                       # extend: SendOrganisationInvitationAsync(locale)
├── ChildCare.Contracts/
│   ├── Requests/ (Create/Resend/Revoke invitation requests)
│   └── Responses/ (PlatformAdminInvitationResponse, PlatformAdminOrganisationResponse)
├── ChildCare.Api/Endpoints/
│   ├── PlatformAdminInvitationEndpoints.cs           # new — mirrors PlatformAdminVaccineTypeEndpoints.cs
│   ├── PlatformAdminOrganisationEndpoints.cs         # new, read-only GET
│   └── OrganisationEndpoints.cs                      # extend: rate limiting on /register (research.md R13)
└── ChildCare.Api/RateLimiting/RateLimiterPolicies.cs # extend: OrganisationRegister policy

web/
├── app/
│   ├── (app)/platform-admin/
│   │   ├── layout.tsx                                # new — extracted shared shell
│   │   ├── invitations/page.tsx                       # new
│   │   ├── organisations/page.tsx                     # new
│   │   └── vaccine-types/page.tsx                     # unchanged content, now renders inside the shared layout
│   └── register/page.tsx                              # new — public, outside (app)/(auth) groups
├── components/
│   ├── Sidebar.tsx                                    # extend: PLATFORM_ADMIN_NAV → array
│   └── platform-admin/
│       ├── InvitationTable.tsx                         # new
│       ├── InvitationFormDialog.tsx                    # new
│       └── OrganisationTable.tsx                       # new
└── i18n/locales/{en,nl,fr}.json                        # extend: platformAdmin.invitations/organisations, register namespaces
```

**Structure Decision**: Existing monorepo layout, no new top-level directories. Backend follows
the exact `Invitations`/`Endpoints` module shape 013h and feature 001 already established;
web follows the existing `(app)/platform-admin/` route-group precedent plus a new top-level
public route (`web/app/register/`), mirroring `web/app/enroll/`'s existing precedent for a
public, unauthenticated page living outside the `(app)`/`(auth)` groups.

## Complexity Tracking

*No Constitution Check violations — this section intentionally left empty.*
