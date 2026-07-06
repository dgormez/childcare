# Implementation Plan: Location Management

**Branch**: `004-locations` | **Date**: 2026-07-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-locations/spec.md`

## Summary

Introduces `Location` as the first real tenant-domain entity built on top of features 001–003's foundation — every prior feature only touched provisioning (`tenants`) or auth (`users`) data; this is the first feature whose endpoints read tenant domain data through the already-built `TenantMiddleware`/`ICurrentTenantService` (feature 002) and `ITenantDbContext` (feature 002/003). A director creates, edits, lists, deactivates, and reactivates locations scoped to their own organisation (FR-001–002, FR-008), fills in nullable Opgroeien reporting settings independently of creation (FR-003–005), and can duplicate an existing location's fields into a new draft to avoid re-entering them when a physical KDV relocates (FR-015, clarified session 2026-07-06). No explicit `OrganisationId`/tenant foreign key is needed on `Location` — like `TenantUser`, tenant scoping is structural: the entire schema *is* the tenant (constitution Principle I), so cross-tenant leakage of a location is impossible by construction, not by an application-level filter that could be forgotten. Deactivation blocks on active dependents only as an extension point (FR-012): no staff/contract entities exist yet (features 005/007), so this feature registers zero guards and always permits deactivation, but the guard mechanism (`IEnumerable<ILocationDeactivationGuard>`) is designed so 005/007 can each add their own guard independently without one feature's registration overwriting another's. Following features 001–003's established boundary, this feature is backend-only — the web admin UI consuming these endpoints is a separate, subsequent effort.

## Technical Context

**Language/Version**: C# / .NET 10, EF Core 10 (continuing on the installed version, consistent with features 001–003's Technical Context note)

**Primary Dependencies**: ASP.NET Core Minimal APIs + `DirectorOnly` policy (feature 003, unchanged), MediatR + FluentValidation (constitution Principle III), EF Core 10 + Npgsql

**Storage**: PostgreSQL 16, schema-per-tenant (unchanged). One new tenant-schema migration (`AddLocations`) — applied to newly-provisioned schemas automatically via `TenantProvisioningService`'s baseline generation (feature 001/002 mechanism), and rolled out to already-existing tenant schemas via the existing `migrate-tenants` CLI command (feature 002) — no new migration mechanism needed

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V), extending the existing `TestWebAppFactoryBase`/`OrganisationOnboardingWebAppFactory` seeded-tenant pattern

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`) — no infra changes

**Project Type**: Web service (ASP.NET Core API) — backend only, no web admin UI work, same boundary features 001/002/003 already drew (their Technical Context sections each state "no web admin / mobile UI work" even though their spec prose describes director-facing/UI-adjacent journeys). The web admin screens that call these endpoints are a separate implementation effort.

**Performance Goals**: No explicit latency target beyond SC-001/SC-006's "under 2 minutes"/"under 1 minute" (director task-completion time, not a system throughput figure); location lists are small per organisation (dozens, not thousands) so no pagination is required for this feature

**Constraints**: Deny-by-default tenant scoping — every location read/write MUST go through the already-non-exempt request pipeline (`TenantMiddleware` sets `ICurrentTenantService` before any handler runs); every endpoint MUST require the `DirectorOnly` policy (FR-011); no hard-delete code path may exist (FR-009); concurrent edits resolve last-write-wins with no concurrency token (FR-017, clarified)

**Scale/Scope**: Same Phase 1 scale as prior features (dozens to low hundreds of organisations); a handful of locations per organisation. One new entity, one new migration, ~7 endpoints (list, get, create, update, deactivate, reactivate, duplicate)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | First feature to expose endpoints reading/writing tenant domain data beyond `users` — no carve-out applies or is needed. `Location` carries no `OrganisationId`/tenant column at all (mirrors `TenantUser`'s existing shape): the schema itself is the isolation boundary, so there is no application-level filter that a future handler could forget to apply. Every `LocationEndpoints.cs` route is non-exempt, so `TenantMiddleware` (feature 002) always resolves `ICurrentTenantService`/`ITenantDbContext` before a handler runs (FR-002, FR-007). |
| II. Regulatory Compliance by Design | ✅ N/A | No BKR/contract/regulatory enforcement logic in this feature — `flex_permission`/`bo_permission`/`dossiernummer`/`verantwoordelijke` are stored fields for Phase 3 Opgroeien XML reporting (FO-SU-05), not enforced constraints here. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Every write (create, update, deactivate, reactivate, duplicate) is a MediatR command with a FluentValidation validator. Reads (list, get-by-id) are also modeled as MediatR queries for consistency with the write side, even though the constitution permits direct simple lookups — no repository/service layer bypasses MediatR. `LocationEndpoints.cs` only maps HTTP ↔ MediatR. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | All validation/error responses use `errorKey`s (new `errors.location.*` keys, `backend/ERROR_KEYS.md` updated during implementation), never raw text. No UI in this feature (backend-only, see Technical Context). |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | TestContainers-backed integration tests, extending `TestWebAppFactoryBase`; tenant-isolation assertions (two seeded organisations, confirm a location created in one is invisible to the other) are exactly the kind of `search_path`-dependent behavior InMemory cannot exercise. |
| VI. Secure Configuration & Storage | ✅ Pass | No new secrets. The `AddLocations` migration is authored/reviewed as normal code and does **not** auto-apply to production — it follows the ordinary "generate SQL script, run manually" path (constitution Principle VI) for existing tenant schemas via `migrate-tenants`; the "new-tenant-schema provisioning" carve-out (v1.1.0) already covers *new* schemas created after this feature ships, unchanged from how it already covers `AddUserRole`/`ExtendUsersAddRefreshTokens` today. |
| VII. Monolith-First Simplicity | ✅ Pass | No new project — extends the existing 5-project structure. `Location` lives in `ChildCare.Domain`, `Locations/` command/query handlers in `ChildCare.Application`, `LocationEndpoints.cs` in `ChildCare.Api`, request/response DTOs in `ChildCare.Contracts`. |

**Overall**: 7 of 7 clean passes. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/004-locations/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── locations-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       └── Location.cs                        #   NEW — no OrganisationId/tenant column (research.md R1)
├── ChildCare.Application/
│   ├── Common/
│   │   └── ILocationDeactivationGuard.cs      #   NEW — extension point for 005/007 (research.md R4)
│   └── Locations/                              #   NEW
│       ├── CreateLocationCommand.cs / …Validator.cs / …Handler.cs
│       ├── UpdateLocationCommand.cs / …Validator.cs / …Handler.cs
│       ├── DeactivateLocationCommand.cs / …Handler.cs
│       ├── ReactivateLocationCommand.cs / …Handler.cs
│       ├── DuplicateLocationCommand.cs / …Validator.cs / …Handler.cs
│       ├── ListLocationsQuery.cs / …Handler.cs
│       ├── GetLocationByIdQuery.cs / …Handler.cs
│       └── LocationResult.cs                   #   shared success/failure result shape (mirrors AuthResult)
├── ChildCare.Infrastructure/
│   └── Persistence/
│       ├── TenantDbContext.cs                  #   MODIFIED — + DbSet<Location>, OnModelCreating config
│       └── Migrations/Tenant/                  #   NEW migration — AddLocations
├── ChildCare.Contracts/
│   ├── Requests/
│   │   └── LocationRequests.cs                 #   NEW — Create/Update request DTOs
│   └── Responses/
│       └── LocationResponse.cs                 #   NEW
├── ChildCare.Api/
│   ├── Endpoints/
│   │   └── LocationEndpoints.cs                #   NEW — /api/locations/*, all DirectorOnly, non-exempt
│   └── Program.cs                              #   MODIFIED — app.MapLocationEndpoints();
│                                                #     register default ILocationDeactivationGuard (research.md R4)
└── ChildCare.Api.Tests/
    ├── LocationCrudTests.cs                     #   NEW — US1 (SC-001, FR-001/002/006/007/010/011/014/017)
    ├── LocationOpgroeienSettingsTests.cs         #   NEW — US2 (SC-002, FR-003/004/005)
    ├── LocationDeactivationTests.cs              #   NEW — US3 (SC-005, FR-008/009/012/016)
    └── LocationDuplicateTests.cs                 #   NEW — US4 (SC-006, FR-015)
```

**Structure Decision**: Web-service (backend-only). No new projects — extends the existing 5-project structure. `Location` is the first tenant-domain entity added alongside `TenantUser`; the diff is concentrated in one new Domain entity, one new Application folder (`Locations/`), one new migration, and one new endpoint file — the same shape as features 001/003's additions, not a structural change.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan.
