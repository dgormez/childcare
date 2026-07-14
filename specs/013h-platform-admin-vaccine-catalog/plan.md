# Implementation Plan: Platform-Admin Vaccine Catalog Management

**Branch**: `013h-platform-admin-vaccine-catalog` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013h-platform-admin-vaccine-catalog/spec.md`

## Summary

Add the first cross-tenant admin capability in this codebase: a new `IsPlatformAdmin` flag on an
existing director account (`TenantUser`), surfaced as a new claim in the director JWT, gating a
new platform-admin-only route in the existing director-web app and five new backend endpoints
(create/rename/reorder/deactivate/reactivate) for 013g's shared `vaccine_types` catalog. 013g's
existing tenant-facing `GET /api/vaccine-types` is untouched. Deactivation carries a
who/when audit trail (three new nullable columns on `VaccineType`, no DB-level FK across the
tenant/public schema boundary — same precedent 013g already set). Reordering reuses
`WaitingListTable`'s existing up/down-button pattern (012a) rather than introducing a drag
library. A new `grant-platform-admin` CLI command, following the existing
`migrate-tenants`/`backfill-growth-check` pattern, grants the flag to a specific account by email
across all tenant schemas — run once against the platform operator's own account
(dgormez@gmail.com) as part of this feature's rollout, per spec.md's Assumptions.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (Next.js 15 web admin)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Next.js App Router, Tailwind, shadcn/ui, openapi-fetch (web) — no new npm dependency
(research.md R4).

**Storage**: PostgreSQL 16 — one new column (`IsPlatformAdmin`, tenant schema, `TenantUser`'s
`Users` table) and three new nullable columns (`DeactivatedByUserId`, `DeactivatedByEmail`,
`DeactivatedAt`, public schema, `VaccineType`). No new tables (data-model.md).

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V); Vitest + `@testing-library/react` (web, for the new management screen/reorder controls).

**Target Platform**: Cloud Run (backend API); web browsers ≥1280px (director web only — no
caregiver-tablet or parent-mobile surface, per spec.md's UX Requirements).

**Project Type**: Web application — ASP.NET Core API + Next.js web admin (existing monorepo
structure, no new projects, no mobile changes).

**Performance Goals**: Same as 013g — small, bounded, uncached-but-cheap reference-table
operations (tens of rows); no specific SLA beyond this codebase's existing director-web list-query
norm.

**Constraints**: No DB-level cross-schema FK for `DeactivatedByUserId` (research.md R2, mirrors
013g's `VaccineRecord.VaccineTypeId`); no in-app write path for the `IsPlatformAdmin` flag itself
(FR-001, granted only via the new CLI command); 013g's existing tenant-facing read endpoint's
shape/behavior/auth policy MUST NOT change (FR-010).

**Scale/Scope**: Single-digit platform-admin accounts expected; catalog stays in the same ~9-20
row range 013g scoped it to. Lowest-frequency admin surface in this codebase to date (one
operator, occasional edits).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS (with a named new pattern) | `IsPlatformAdmin` lives on `TenantUser`, tenant-schema-scoped like every other director field — no cross-tenant read/write of tenant domain data. The new write endpoints act only on `vaccine_types` (`PublicDbContext`, already the first non-tenant-management public-schema table per 013g) — no tenant domain data is read or written by any endpoint in this feature. `DeactivatedByUserId` deliberately has no DB-level FK across the schema boundary (research.md R2), same precedent as 013g's `VaccineRecord.VaccineTypeId` — an application-layer-only reference to an account whose home schema is not statically knowable. This feature's `PlatformAdminOnly` policy is an *additive* claim check on top of the existing per-request `tenant_id`-driven `TenantMiddleware` flow (research.md R1) — it does not bypass or weaken tenant scoping for any endpoint, existing or new. |
| II. Regulatory Compliance by Design | PASS | Not a BKR/contract-overlap/closure-calendar feature — no regulatory ratio logic applies. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All five new write operations go through new MediatR commands (`CreateVaccineTypeCommand`, `UpdateVaccineTypeCommand`, `ReorderVaccineTypeCommand`, `DeactivateVaccineTypeCommand`, `ReactivateVaccineTypeCommand`); the new list-with-audit-fields read goes through a new MediatR query (`ListVaccineTypesForPlatformAdminQuery`, distinct from 013g's unchanged `ListVaccineTypesQuery`). Endpoint file (`PlatformAdminVaccineTypeEndpoints.cs`) contains no business logic. |
| IV. Internationalization First | PASS | All new platform-admin screen strings ship as NL/FR/EN locale keys from the start, matching every other director-facing surface's existing precedent. (Low-priority in practice — this screen's only user today is the developer/platform operator — but the constitution draws no exception for that, so full i18n coverage ships regardless.) |
| V. Test with Real Infrastructure | PASS | New integration tests run against TestContainers PostgreSQL: unauthorized-access rejection (director without flag → 403), full create/rename/reorder/deactivate/reactivate happy path, deactivation audit-field correctness (including the no-op-if-already-inactive and fresh-audit-on-redeactivation edge cases), and a regression test proving 013g's `GET /api/vaccine-types` contract is byte-for-byte unchanged. |
| VI. Secure Configuration & Storage | PASS | No secrets/storage involved. Both new migrations (public + tenant) are normal reviewed EF Core migrations, not auto-applied to production (constitution's standing rule, unaffected by this feature). |
| VII. Monolith-First Simplicity | PASS | No new deployable/service, no new npm dependency (research.md R4 — reuses `WaitingListTable`'s existing button-based reorder pattern instead of introducing a drag library). New MediatR handlers/endpoints inside the existing five-project backend solution and existing web app; new CLI command follows the existing `MigrateTenantsCommand`/`BackfillGrowthCheckCommand` shape exactly (research.md R3), not a new pattern. |

No unjustified violations. The one item worth flagging explicitly (a cross-schema-boundary
reference with no DB-level FK, and an authorization policy that checks a claim without any new
authentication scheme) both directly extend patterns 013g and the existing `DeviceOrStaffOrDirector`
policy already established — not new categories of risk.

## Project Structure

### Documentation (this feature)

```text
specs/013h-platform-admin-vaccine-catalog/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/            # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   ├── TenantUser.cs                       # EXTENDED — IsPlatformAdmin
│   │   └── VaccineType.cs                       # EXTENDED — DeactivatedByUserId/Email/At
├── ChildCare.Api/
│   ├── Cli/
│   │   └── GrantPlatformAdminCommand.cs          # NEW (research.md R3)
│   ├── Services/
│   │   └── JwtService.cs                         # EXTENDED — is_platform_admin claim
│   ├── Program.cs                                # EXTENDED — PlatformAdminOnly policy, CLI dispatch
│   └── Endpoints/
│       └── PlatformAdminVaccineTypeEndpoints.cs  # NEW
├── ChildCare.Application/
│   └── VaccineTypes/                              # EXTENDED (013g's existing folder)
│       ├── ListVaccineTypesForPlatformAdminQuery.cs   # NEW
│       ├── CreateVaccineTypeCommand.cs                 # NEW
│       ├── UpdateVaccineTypeCommand.cs                 # NEW
│       ├── ReorderVaccineTypeCommand.cs                # NEW
│       ├── DeactivateVaccineTypeCommand.cs             # NEW
│       └── ReactivateVaccineTypeCommand.cs             # NEW
├── ChildCare.Infrastructure/
│   └── Persistence/
│       └── Migrations/
│           ├── Public/…AddVaccineTypeDeactivationAudit.cs   # NEW (research.md R5)
│           └── Tenant/…AddIsPlatformAdminToUsers.cs          # NEW (research.md R5)
├── ChildCare.Contracts/
│   ├── Responses/PlatformAdminVaccineTypeResponse.cs  # NEW
│   └── Requests/PlatformAdminVaccineTypeRequests.cs    # NEW
└── ChildCare.Api.Tests/
    ├── PlatformAdmin/…                                # NEW — auth + CRUD + audit tests
    ├── VaccineTypes/…                                 # EXTENDED — 013g contract-unchanged regression test
    └── TenantMigrationRolloutTests.cs                 # EXTENDED — revert-helper for the new Users column (established pattern, 012a/013c/006a/013d/013g)

web/
├── app/(app)/platform-admin/vaccine-types/page.tsx   # NEW — gated route
├── components/platform-admin/
│   └── VaccineTypeManagementTable.tsx                # NEW — reuses WaitingListTable's up/down-button pattern (research.md R4)
├── components/layout/Sidebar.tsx                     # EXTENDED — conditional nav entry when IsPlatformAdmin
└── lib/generated/api-types.ts                        # regenerated
```

**Structure Decision**: Existing monorepo layout (`backend/`, `web/`, `mobile/`) — no new
top-level projects, no mobile changes (director-web only, per spec.md). Backend extends 013g's
existing `VaccineTypes/` MediatR folder in place rather than creating a parallel folder, since
these commands operate on the same `VaccineType` aggregate 013g already modeled. New CLI command
lives alongside the two existing ones in `ChildCare.Api/Cli/`, following that established
location and dispatch pattern exactly (research.md R3).

## Complexity Tracking

No unjustified Constitution Check violations — this section is not needed. See the Constitution
Check table above for the two deliberate extensions of already-established patterns (cross-schema
reference with no DB FK; additive claim-based authorization policy), addressed there with their
rationale rather than listed as violations requiring justification.
