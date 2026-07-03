# Implementation Plan: Organisation Onboarding

**Branch**: `001-organisation-onboarding` | **Date**: 2026-07-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-organisation-onboarding/spec.md`

## Summary

An invite-only registration flow: a platform operator issues a single-use, email-locked invitation via a credential-gated internal endpoint; a director completes registration (org name, their name, email, password) and the system synchronously creates the organisation's registry record, an isolated Postgres schema for that organisation with baseline structure, and the director's account inside it — returning a ready-to-use access token in the same response. Technical approach: introduce the constitution's 5-project solution structure now (this is the first feature built after ratification), with MediatR + FluentValidation handling the `RegisterOrganisationCommand`/`CreateInvitationCommand`, a `PublicDbContext` for the shared `tenants`/`invitations` tables, and a provisioning-only `TenantDbContext` (dynamic default schema per invocation) used solely to create and migrate a brand-new tenant schema — without yet wiring the request-time `TenantMiddleware`/`ICurrentTenantService` that feature 002 owns.

## Technical Context

**Language/Version**: C# / .NET 10 (already the walking skeleton's target; PROJECT-BRIEF.md says "EF Core 9" but the installed packages are already EF Core 10 — continuing on the installed version, see research.md R13)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 10 + Npgsql, MediatR, FluentValidation, BCrypt.Net-Next (already present)

**Storage**: PostgreSQL 16. Local dev = Docker Postgres; deployed pre-revenue = Neon direct/non-pooled connection (constitution Principle I; no pgBouncer transaction-mode pooling, since that resets `search_path` between statements)

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V — no InMemory provider)

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`)

**Project Type**: Web service (ASP.NET Core API); this feature touches backend only — no web admin / mobile UI work

**Performance Goals**: No explicit target in spec.md beyond "synchronous, single flow" (FR-008); schema creation + baseline migration + one row insert is expected to complete in low single-digit seconds

**Constraints**: Registration MUST be synchronous end-to-end (FR-008); MUST NOT require any manual operator step post-invitation (FR-011); connection string MUST be non-pooled for the tenant-provisioning path (research.md R6, constitution Principle I)

**Scale/Scope**: Early access — low volume, operator-issued invitations only. Two new endpoints, ~3 new entities, one baseline tenant migration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | This feature *creates* tenant schemas (`CREATE SCHEMA`, `HasDefaultSchema` per invocation) but does not build `TenantMiddleware`/`ICurrentTenantService` (explicitly feature 002's scope per BACKLOG.md). Covered by the constitution's codified "Carve-out (provisioning-only features)" (v1.1.0): this feature exposes zero endpoints reading existing tenant domain data — the only tenant-schema write is the director-user insert performed by the same provisioning code path that just created that schema. |
| II. Regulatory Compliance by Design | ✅ N/A | This feature has no BKR/contract/regulatory logic — it's pure account/workspace provisioning. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | `RegisterOrganisationCommand` and `CreateInvitationCommand`, each with a FluentValidation validator as a MediatR pipeline behaviour; endpoint files only map HTTP ↔ MediatR (research.md R1, R3). |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | All error responses use `errorKey` envelopes (contracts/*.md), never raw text; no UI is built in this feature (backend-only). |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | quickstart.md specifies TestContainers-based integration tests for the resilience/concurrency scenarios; schema-per-tenant behavior has no InMemory equivalent. |
| VI. Secure Configuration & Storage | ✅ Pass | (a) `SuperAdmin:ApiKey` sourced from GCP Secret Manager, not a plain env var (research.md R11). (b) Baseline tenant-schema migrations *auto-apply* at registration time, in production, covered by the constitution's codified "Carve-out (new-tenant-schema provisioning)" (v1.1.0): the schema is brand-new and empty, so there is no prior data to corrupt, and migration content is still authored/reviewed as normal code. |
| VII. Monolith-First Simplicity | ✅ Pass | Introduces exactly the 5 constitution-mandated projects, no more; no new services; existing `AppDbContext`/`AuthEndpoints`/`HabitEndpoints` are left untouched rather than prematurely refactored (research.md R2). |

**Overall**: 7 of 7 clean passes. Principles I and VI were "⚠ Partial, justified" as of this plan's original draft; `/speckit-analyze` flagged that reasoning as insufficient for NON-NEGOTIABLE-adjacent principles (findings D1, D2), so both were resolved by amending the constitution to v1.1.0 with two named, codified carve-outs rather than relying on ad hoc plan-level justification. See Complexity Tracking below for the historical record of that resolution.

## Project Structure

### Documentation (this feature)

```text
specs/001-organisation-onboarding/
├── plan.md              # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   ├── create-invitation.md
│   └── register-organisation.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.sln                          # NEW — solution file tying all 5 projects together
├── ChildCare.Api/                         # EXISTING — extended, not restructured
│   ├── Program.cs                         #   + PublicDbContext/TenantDbContext DI registration,
│   │                                       #     MediatR + FluentValidation pipeline registration
│   ├── Data/AppDbContext.cs                #   UNTOUCHED (research.md R2)
│   ├── Endpoints/AuthEndpoints.cs          #   UNTOUCHED
│   ├── Endpoints/HabitEndpoints.cs         #   UNTOUCHED
│   ├── Endpoints/AdminEndpoints.cs         #   NEW — maps POST /api/admin/invitations
│   ├── Endpoints/OrganisationEndpoints.cs  #   NEW — maps POST /api/organisations/register
│   ├── Auth/SuperAdminKeyHandler.cs        #   NEW — constant-time X-Superadmin-Key check (research.md R11)
│   ├── Services/JwtService.cs              #   + new primitive-typed overload (research.md R8);
│   │                                        #     existing User-typed overload untouched
│   └── Services/JwtAccessTokenIssuer.cs    #   NEW — IAccessTokenIssuer adapter delegating to JwtService
├── ChildCare.Api.Tests/                   # EXISTING — extended with new integration tests
│   ├── OrganisationOnboardingTests.cs       #   NEW — TestContainers-based (constitution Principle V); US1/US2 happy-path + rejection tests
│   ├── OrganisationOnboardingResilienceTests.cs #   NEW — US3 failure-injection + concurrency tests
│   ├── AdminInvitationTests.cs              #   NEW — SuperAdminKeyHandler auth tests
│   └── OrganisationOnboardingWebAppFactory.cs #   NEW — TestContainers Postgres fixture (this feature's own factory; ChildCareWebAppFactory/InMemory is unrelated and untouched — research.md R2)
├── ChildCare.Domain/                      # NEW project
│   └── Entities/
│       ├── Tenant.cs
│       ├── Invitation.cs
│       └── TenantUser.cs                   #   the minimal baseline "users" entity (data-model.md)
├── ChildCare.Application/                 # NEW project
│   ├── Organisations/
│   │   ├── RegisterOrganisationCommand.cs
│   │   ├── RegisterOrganisationCommandHandler.cs
│   │   ├── RegisterOrganisationCommandValidator.cs
│   │   ├── RegisterOrganisationResult.cs   #   NEW (research.md R15/analyze finding F2) — success/InvitationNotFound/EmailMismatch result type, since 404 vs 422 needs richer signaling than a thrown exception
│   │   └── OrganisationSlugGenerator.cs    #   NEW — slug derivation + collision-suffix retry (research.md R14)
│   ├── Invitations/
│   │   ├── CreateInvitationCommand.cs
│   │   ├── CreateInvitationCommandHandler.cs
│   │   ├── CreateInvitationCommandValidator.cs
│   │   └── InvitationTokenCodec.cs         #   NEW — symmetric encode (creation) / decode+hash (registration lookup), shared so the two sides can't drift (research.md R4)
│   ├── Common/Behaviors/ValidationBehavior.cs
│   ├── Common/IAccessTokenIssuer.cs        #   port; adapter implemented in ChildCare.Api (research.md R8)
│   ├── Common/IPublicDbContext.cs          #   NEW (research.md R16) — port over PublicDbContext, avoids a circular Infrastructure→Application→Infrastructure reference
│   └── Common/ITenantProvisioningService.cs #  NEW (research.md R16) — port over TenantProvisioningService, same reason
├── ChildCare.Infrastructure/               # NEW project
│   └── Persistence/
│       ├── PublicDbContext.cs
│       ├── PublicDbContextFactory.cs        #   NEW — IDesignTimeDbContextFactory, needed for `dotnet ef migrations add/database update --context PublicDbContext`
│       ├── TenantDbContext.cs               #   dynamic default-schema, provisioning-only (research.md R6)
│       ├── TenantDbContextFactory.cs        #   NEW — IDesignTimeDbContextFactory using the "tenant_template" placeholder schema (research.md R15)
│       ├── DynamicSchemaModelCacheKeyFactory.cs
│       ├── TenantProvisioningService.cs     #   CREATE SCHEMA + runtime-generated-and-substituted baseline SQL + idempotent director user upsert (research.md R6, R15, R17 — NOT a plain Database.Migrate() call; see research.md R15 for why)
│       └── Migrations/
│           ├── Public/                      #   tenants, invitations tables
│           └── Tenant/                      #   baseline users table
└── ChildCare.Contracts/                    # NEW project
    ├── Requests/RegisterOrganisationRequest.cs
    ├── Requests/CreateInvitationRequest.cs
    ├── Responses/RegisterOrganisationResponse.cs
    └── Responses/CreateInvitationResponse.cs

infra/gcp/
└── main.tf                                 # + google_secret_manager_secret for SUPERADMIN_API_KEY,
                                             #   IAM binding, Cloud Run secret_key_ref (research.md R11)
```

**Structure Decision**: Web-service (Option 2-shaped, backend-only — no `frontend/` changes in this feature). This is the first feature to introduce the constitution's mandated 5-project split; all new code lives in the 4 new projects plus additive changes to `ChildCare.Api`. Nothing in the existing `ChildCare.Api` skeleton (`AppDbContext`, `AuthEndpoints`, `HabitEndpoints`, `AuthService`) is modified or removed — that is explicitly feature 002's responsibility per BACKLOG.md (research.md R2).

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan.

**Historical record**: Principles I and VI were originally "⚠ Partial, justified" here, defended by ad hoc reasoning specific to this plan (no `TenantMiddleware` built yet; tenant-schema migrations auto-applying in production). `/speckit-analyze` flagged both as CRITICAL (findings D1, D2): a plan-level justification does not clear a NON-NEGOTIABLE-adjacent constitution MUST violation — that requires either revising the plan to comply, or a genuine constitution amendment. The user chose the amendment path: `.specify/memory/constitution.md` v1.1.0 added two named, codified carve-outs —

- **Carve-out (provisioning-only features)** on Principle I: features with zero tenant-data-read endpoints are exempt from the `TenantMiddleware` requirement; this feature is named as the qualifying example.
- **Carve-out (new-tenant-schema provisioning)** on Principle VI: auto-applying already-reviewed migration content to a brand-new, empty tenant schema is exempt from the manual-SQL-script-review rule.

Both are now reflected as clean passes in the Constitution Check table above, citing the amendment rather than a plan-local argument.
