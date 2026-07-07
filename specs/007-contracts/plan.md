# Implementation Plan: Enrolment Contracts

**Branch**: `007-contracts` | **Date**: 2026-07-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-contracts/spec.md`

## Summary

Adds `Contract` as a new tenant-domain entity — the binding agreement linking a `Child` to a `Location` with a care period, per-weekday contracted hours, a daily rate in cents, and a `draft`/`active`/`ended` lifecycle. Contracted weekdays (each with its own start/end time) and the five photo/media consent booleans are stored as JSONB owned collections/types on `Contract` (research.md R1), avoiding extra join tables for Phase 1. A director creates a draft, activates it (subject to the one-active-contract-per-location rule and the constitution-mandated split-location day-overlap validator, both enforced atomically via a Postgres advisory lock keyed on the child — research.md R2, reusing the pattern introduced by the org-registration concurrency fix), amends it (ends the current contract, creates+activates a successor with updated terms, full audit trail via a `PreviousContractId` chain), or terminates it outright (ends it with no successor, for a family leaving entirely — spec.md Clarifications). A new `IContractPdfGenerator` port/adapter pair (mirrors `IProfilePhotoStorage`'s split) generates the contract PDF via QuestPDF — the first use of QuestPDF in this codebase. This feature also supplies the two deactivation-guard implementations features 004/006 reserved extension points for: `ILocationDeactivationGuard` and `IChildDeactivationGuard`, both checking for any currently `active` contract. Backend-only, matching every prior feature's boundary.

## Technical Context

**Language/Version**: C# / .NET 10, EF Core 10 (unchanged from features 001–006)

**Primary Dependencies**: ASP.NET Core Minimal APIs + `DirectorOnly` policy (feature 003) — every contract endpoint is director-managed, matching features 004–006; MediatR + FluentValidation (constitution Principle III); EF Core 10 + Npgsql; QuestPDF (MIT, constitution's fixed PDF library) — new dependency, first use in this codebase; Npgsql raw ADO.NET for the advisory-lock helper (research.md R2, same primitive as `TenantProvisioningService.RunExclusiveAsync`)

**Storage**: PostgreSQL 16, schema-per-tenant (unchanged) — one new tenant-schema migration (`AddContracts`) covering `contracts` (with `contracted_days` and `consent` as JSONB columns via EF Core's `OwnsMany/OwnsOne(...).ToJson()`), applied the same way every prior feature's migration was (auto-applied to new schemas via `TenantProvisioningService`, rolled out to existing schemas via the `migrate-tenants` CLI)

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V), extending `TestWebAppFactoryBase`; the advisory-lock concurrency test (US2 Scenario 3) fires two real concurrent HTTP requests against the TestContainers database, matching the precedent set by feature 003's `AuthMultiTenantLoginTests` and feature 001's `Register_WithConcurrentAttempts_CreatesExactlyOneTenant`

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`) — no new infrastructure; QuestPDF is a pure-managed-code PDF renderer with no native/font-server dependency beyond what the base .NET container image already provides

**Project Type**: Web service (ASP.NET Core API) — backend only, no web admin UI work, same boundary as features 001–006

**Performance Goals**: SC-001's task-completion-time framing (director creates and activates a contract in under 2 minutes); no explicit throughput target — contract volume is one to a few active rows per child, matching the small-to-moderate per-organisation scale established in prior features

**Constraints**: Deny-by-default tenant scoping — every contract endpoint goes through `TenantMiddleware`/`ICurrentTenantService` (feature 002); every endpoint requires `DirectorOnly`; money is stored as whole-number cents only, never floating-point (FR-012); the day-overlap validator and one-active-per-location check MUST be atomic under concurrent activation requests for the same child (FR-006, constitution Principle II names this validator explicitly); `tarief_code`/`rate_valid_until` columns exist but are unused/unexposed in Phase 1 (FR-013)

**Scale/Scope**: Same Phase 1 scale as prior features (dozens of organisations, dozens to low hundreds of children each, each with one to a handful of contracts over their enrolment history). One new entity: `Contract` (plus two owned/JSONB value types: `ContractedDay`, `ContractConsent`). Two new ports: `IContractPdfGenerator` (QuestPDF adapter), `IAdvisoryLockService` (Postgres advisory-lock adapter, feature-scoped — research.md R2). Two deactivation-guard implementations registered (`ILocationDeactivationGuard`, `IChildDeactivationGuard`). ~8 endpoints under `/api/children/{childId}/contracts` and `/api/contracts/*`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | `ContractsEndpoints.cs` routes are all non-exempt, going through `TenantMiddleware`/`ICurrentTenantService` (feature 002), same as features 004–006. `Contract` carries no `OrganisationId` column — schema-per-tenant is the isolation boundary, consistent with every other tenant-domain entity. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass | This feature **is** the constitution-named split-location day-overlap validator ("runs on every contract activation when a child holds two simultaneous contracts at different locations of the same organisation") — implemented in the Application layer (`ActivateContractCommandHandler`), never client-side, and covered by an integration test that exercises real concurrent activation (US2 Scenario 3). |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Every write (create, update-draft, activate, amend, terminate) is a MediatR command with a FluentValidation validator; PDF generation and history/detail reads are MediatR queries/simple lookups. `ContractsEndpoints.cs` only maps HTTP ↔ MediatR. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | All validation/error responses use new `errors.contract.*` keys (added to `backend/ERROR_KEYS.md`), reusing `errors.child.not_found`/`errors.location.not_found` where the underlying gap is identical to an existing key (no duplicate invented). PDF body text uses locale-resolved strings passed in from the caller's locale, not hardcoded language. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | TestContainers-backed integration tests extending `TestWebAppFactoryBase`. The advisory-lock concurrency guarantee (FR-006) is specifically the kind of behavior that only a real PostgreSQL instance can validate — `pg_advisory_lock` has no InMemory equivalent, reinforcing why this principle is NON-NEGOTIABLE for this feature in particular. |
| VI. Secure Configuration & Storage | ✅ Pass | No new secrets — QuestPDF requires only a one-line in-process license declaration (`QuestPDF.Settings.License = LicenseType.Community`), not a credential. PDF bytes are returned directly in the HTTP response (not persisted to GCS), so no new signed-URL surface is introduced. The `AddContracts` migration follows the ordinary reviewed/manual-rollout path for existing tenant schemas — no new-tenant-schema carve-out needed. |
| VII. Monolith-First Simplicity | ✅ Pass | No new project. `Contract`/`ContractedDay`/`ContractConsent`/`ContractStatus` live in `ChildCare.Domain`; `Contracts` command/query handlers in `ChildCare.Application`; `IContractPdfGenerator`/`IAdvisoryLockService` are small new ports in `ChildCare.Application/Common` with `ChildCare.Infrastructure` adapters — no new deployable, no microservice, no message queue for what is fundamentally an in-process locked check-then-write. |

**Overall**: 7 of 7 clean passes. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/007-contracts/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── contracts-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   └── Contract.cs                          #   NEW — ChildId, LocationId, PreviousContractId?, dates, JSONB-owned ContractedDays/Consent
│   ├── Enums/
│   │   └── ContractStatus.cs                    #   NEW — Draft, Active, Ended
│   └── ValueObjects/
│       ├── ContractedDay.cs                     #   NEW — Weekday/StartTime/EndTime, owned JSONB element
│       └── ContractConsent.cs                   #   NEW — five consent booleans, owned JSONB object
├── ChildCare.Application/
│   ├── Common/
│   │   ├── ITenantDbContext.cs                  #   MODIFIED — + DbSet<Contract> Contracts
│   │   ├── IChildDeactivationGuard.cs           #   unchanged interface; MODIFIED registration only (Program.cs)
│   │   ├── ILocationDeactivationGuard.cs        #   unchanged interface; MODIFIED registration only (Program.cs)
│   │   ├── IContractPdfGenerator.cs             #   NEW — port for QuestPDF adapter
│   │   └── IAdvisoryLockService.cs              #   NEW — port for Postgres advisory-lock adapter (research.md R2)
│   └── Contracts/                                #   NEW
│       ├── CreateContractCommand.cs / …Validator.cs / …Handler.cs
│       ├── UpdateContractCommand.cs / …Validator.cs / …Handler.cs      (draft-only edits)
│       ├── ActivateContractCommand.cs / …Handler.cs                    (day-overlap + lock, FR-004–006)
│       ├── AmendContractCommand.cs / …Validator.cs / …Handler.cs       (FR-007/008)
│       ├── TerminateContractCommand.cs / …Validator.cs / …Handler.cs   (FR-009a)
│       ├── ListChildContractsQuery.cs / …Handler.cs                    (FR-017)
│       ├── GetContractByIdQuery.cs / …Handler.cs
│       ├── GenerateContractPdfQuery.cs / …Handler.cs                   (FR-011)
│       ├── ContractLocationDeactivationGuard.cs                        #   NEW impl of ILocationDeactivationGuard
│       ├── ContractChildDeactivationGuard.cs                           #   NEW impl of IChildDeactivationGuard
│       ├── ContractResult.cs / ContractMapper.cs
├── ChildCare.Infrastructure/
│   ├── Persistence/
│   │   ├── TenantDbContext.cs                  #   MODIFIED — + Contract DbSet/config (OwnsMany/OwnsOne ToJson)
│   │   └── Migrations/Tenant/                  #   NEW migration — AddContracts
│   ├── Pdf/
│   │   └── QuestPdfContractGenerator.cs        #   NEW — implements IContractPdfGenerator
│   └── Concurrency/
│       └── PostgresAdvisoryLockService.cs      #   NEW — implements IAdvisoryLockService (mirrors TenantProvisioningService.RunExclusiveAsync)
├── ChildCare.Contracts/
│   ├── Requests/
│   │   └── ContractRequests.cs                 #   NEW — Create/Update/Activate/Amend/Terminate DTOs
│   └── Responses/
│       └── ContractResponse.cs                 #   NEW
├── ChildCare.Api/
│   ├── Endpoints/
│   │   └── ContractsEndpoints.cs               #   NEW — /api/children/{id}/contracts/*, /api/contracts/*, all DirectorOnly
│   └── Program.cs                              #   MODIFIED — map new endpoints; register IContractPdfGenerator, IAdvisoryLockService,
│                                                #     ILocationDeactivationGuard, IChildDeactivationGuard; QuestPDF.Settings.License
└── ChildCare.Api.Tests/
    ├── ContractLifecycleTests.cs                #   NEW — US1 (create/activate, FR-001–004)
    ├── ContractSplitLocationTests.cs             #   NEW — US2 (day-overlap + concurrency, FR-005/006)
    ├── ContractAmendmentTests.cs                 #   NEW — US3 (FR-007–009)
    ├── ContractTerminationTests.cs               #   NEW — US3a (FR-009a)
    ├── ContractPdfTests.cs                       #   NEW — US4 (FR-010/011)
    └── ContractDeactivationGuardTests.cs         #   NEW — feature 004/006 guard integration (FR-004a and child/location deactivation blocking)
```

**Structure Decision**: Web-service (backend-only). No new projects — extends the existing 5-project structure the same way features 004–006 did. `IContractPdfGenerator` and `IAdvisoryLockService` are small, focused new ports (not a shared "utilities" grab-bag) — each has exactly one Infrastructure adapter and one reason to change, matching the `IProfilePhotoStorage` precedent rather than introducing a generic abstraction ahead of need.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan.
