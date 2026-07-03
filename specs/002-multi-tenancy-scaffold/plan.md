# Implementation Plan: Multi-Tenancy Scaffold

**Branch**: `002-multi-tenancy-scaffold` | **Date**: 2026-07-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-multi-tenancy-scaffold/spec.md`

## Summary

Builds the request-time half of schema-per-tenant isolation that feature 001 deliberately deferred: `TenantMiddleware` resolves the `tenant_id` JWT claim into a scoped `ICurrentTenantService`, denying any non-exempt request before touching domain data (FR-006/007/008/008a); a request-scoped `TenantDbContext` (reusing the class already introduced in feature 001) is registered via a new `ITenantDbContextResolver`; a CLI subcommand rolls out pending migrations across every existing tenant schema (FR-010/011). Alongside the scaffold itself, this feature retires the walking-skeleton's single-shared-schema model: `AppDbContext`, `HabitEndpoints.cs`, `PaymentEndpoints.cs`, `NotificationEndpoints.cs`, and `StripeService.cs` are deleted outright (FR-013), while `AuthEndpoints.cs`/`AuthService.cs` are migrated onto an extended `TenantUser` entity living in `TenantDbContext` (FR-014), using a deliberately temporary "default tenant" shim for the handful of pre-authentication routes until feature 003 delivers real tenant-aware login resolution (research.md R7).

## Technical Context

**Language/Version**: C# / .NET 10, EF Core 10 (already the installed version per feature 001 research.md R13; PROJECT-BRIEF.md says EF Core 9, continuing on the installed version)

**Primary Dependencies**: ASP.NET Core Minimal APIs + custom middleware (`TenantMiddleware`), EF Core 10 + Npgsql, MediatR/FluentValidation (unchanged from feature 001, not newly extended to `AuthService` — see Constitution Check, Principle III)

**Storage**: PostgreSQL 16. Local dev = Docker Postgres; deployed pre-revenue = Neon direct (non-pooled) connection (constitution Principle I, research.md R11) — unchanged from feature 001

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V), extending the `TestWebAppFactoryBase`/`OrganisationOnboardingWebAppFactory` pattern from feature 001

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`) — no infra changes needed for this feature (no new secrets, no new external services)

**Project Type**: Web service (ASP.NET Core API) — backend only, no web admin / mobile UI work

**Performance Goals**: No explicit latency target beyond "acceptable" (spec.md has no SC for per-request overhead); tenant resolution adds one indexed lookup (`Tenant` by `Id`) per non-exempt request

**Constraints**: Deny-by-default tenant resolution (FR-015); non-pooled DB connection (FR-012); migration rollout MUST be an explicit operator action, never auto-applied (constitution Principle VI, research.md R8)

**Scale/Scope**: Phase 1 target of dozens to low hundreds of organisations (SC-005, clarified) — the rollout mechanism iterates tenants sequentially, which is acceptable at this scale; revisit if Phase 3 scale materially changes this

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | This feature *is* the mechanism the principle describes — `TenantMiddleware`, `ICurrentTenantService` (Scoped, research.md R2), `search_path` via `HasDefaultSchema` per resolved schema. It also formally retires feature 001's codified carve-out ("`TenantMiddleware` is built in feature `002-multi-tenancy-scaffold`") — that carve-out's exemption ends the moment a *future* feature adds a tenant-domain-data-reading endpoint; this feature itself adds no such endpoint (scaffold only), so no new carve-out is needed here, but none remains available after this ships either. |
| II. Regulatory Compliance by Design | ✅ N/A | No BKR/contract/regulatory logic — pure request-plumbing infrastructure. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | New surface (none — this feature adds no new HTTP endpoints of its own, only middleware + a CLI subcommand) has nothing to route through MediatR. `AuthEndpoints.cs`/`AuthService.cs` remain the pre-existing direct-injection pattern (not MediatR), unchanged in *architecture* by this feature — only their data dependency moves (`AppDbContext` → `TenantDbContext`). This is inherited, pre-existing structure (feature 001 research.md R2 already left it untouched), not a new deviation this feature introduces; BACKLOG.md's feature 003 — Auth section frames the existing skeleton as something to evolve, not replace, so a full MediatR rewrite is that feature's call to make, not this one's. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | New rejection responses (FR-006/007/008/008a) use `errorKey` envelopes per research.md R10, extending `ERROR_KEYS.md`'s existing convention. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | Isolation, fail-closed, and rollout scenarios (quickstart.md) all require real Postgres (`search_path`/schema behavior has no InMemory equivalent) — extends the existing TestContainers factory pattern. |
| VI. Secure Configuration & Storage | ✅ Pass | Migration rollout is an explicit, manually-invoked CLI action (research.md R8) — the constitution's carve-out for auto-applying migrations covers only brand-new tenant schemas at onboarding (feature 001); rolling changes out to *existing* schemas stays outside that carve-out, satisfied here by never auto-applying. |
| VII. Monolith-First Simplicity | ✅ Pass | No new project — still exactly 5 (`ChildCare.Api`, `.Application`, `.Domain`, `.Infrastructure`, `.Contracts`). The migration rollout CLI is a subcommand of the existing `ChildCare.Api` entry point, not a 6th project (research.md R8). |

**Overall**: 7 of 7 clean passes. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/002-multi-tenancy-scaffold/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── migrate-tenants-cli.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       ├── TenantUser.cs                     #   MODIFIED — + GoogleId, AppleId, EmailVerified,
│       │                                     #     EmailVerificationToken/Expiry, PasswordResetToken/Expiry
│       │                                     #     (research.md R5)
│       └── TenantUserRefreshToken.cs         #   NEW — moved from ChildCare.Api.Models.UserRefreshToken (R5)
├── ChildCare.Application/
│   └── Common/
│       ├── ICurrentTenantService.cs          #   NEW — read-only port (research.md R2)
│       └── ITenantDbContextResolver.cs       #   NEW — port over "build a TenantDbContext for schema X" (R1)
├── ChildCare.Infrastructure/
│   └── Persistence/
│       ├── TenantDbContext.cs                #   MODIFIED — register TenantUserRefreshToken,
│       │                                     #     extended TenantUser config (R5)
│       ├── CurrentTenantService.cs           #   NEW — concrete, settable implementation (R2)
│       ├── TenantDbContextResolver.cs        #   NEW — implements ITenantDbContextResolver,
│       │                                     #     centralizes DbContextOptionsBuilder +
│       │                                     #     DynamicSchemaModelCacheKeyFactory wiring (R1)
│       └── Migrations/Tenant/                #   NEW migration — extended users table + refresh_tokens (R6)
├── ChildCare.Api/
│   ├── Program.cs                            #   MODIFIED — register ICurrentTenantService (Scoped),
│   │                                         #     TenantDbContext (Scoped, via resolver), TenantMiddleware;
│   │                                         #     migrate-tenants CLI subcommand check at top (R8);
│   │                                         #     remove AppDbContext/StripeService/Habit+Payment+Notification
│   │                                         #     endpoint registrations
│   ├── Middleware/
│   │   ├── TenantMiddleware.cs               #   NEW (research.md R3)
│   │   └── TenantExemptAttribute.cs          #   NEW — marks a route exempt from tenant resolution (R3)
│   ├── Cli/
│   │   └── MigrateTenantsCommand.cs          #   NEW — the migrate-tenants subcommand body (R8)
│   ├── Services/
│   │   ├── AuthService.cs                    #   MODIFIED — AppDbContext/Models.User → TenantDbContext/
│   │   │                                     #     TenantUser; default-tenant shim for pre-auth routes (R7)
│   │   └── JwtService.cs                     #   MODIFIED — remove GenerateAccessToken(User) overload,
│   │                                         #     AuthService now uses the existing tenant-aware overload
│   ├── Endpoints/
│   │   ├── AuthEndpoints.cs                  #   MODIFIED — [TenantExempt] on register/login/google/apple/
│   │   │                                     #     refresh/forgot-password/reset-password/verify-email (R3)
│   │   ├── AdminEndpoints.cs                 #   MODIFIED — [TenantExempt] (R3)
│   │   └── OrganisationEndpoints.cs          #   MODIFIED — [TenantExempt] (R3)
│   ├── Data/AppDbContext.cs                  #   DELETED (R4)
│   ├── Models/{User,UserRefreshToken,Habit,HabitCompletion}.cs  # DELETED (R4)
│   ├── Endpoints/{HabitEndpoints,PaymentEndpoints,NotificationEndpoints}.cs  # DELETED (R4)
│   ├── Services/StripeService.cs             #   DELETED (R4, unused once PaymentEndpoints is gone)
│   └── Migrations/                           #   DELETED (AppDbContext's own migrations; orphaned tables
│                                              #     cleaned up via a manual script, not EF — research.md R9)
└── ChildCare.Api.Tests/
    ├── TenantIsolationTests.cs               #   NEW — US1 (SC-001)
    ├── TenantRejectionTests.cs               #   NEW — US2 (SC-002)
    ├── TenantMigrationRolloutTests.cs        #   NEW — US3 (SC-003/SC-004)
    ├── AuthEndpointTests.cs                  #   MODIFIED — TenantUser/TenantDbContext instead of
    │                                         #     Models.User/AppDbContext; asserts tenant_id claim present
    ├── HabitEndpointTests.cs                 #   DELETED (R4)
    └── MiniStackWebAppFactory.cs / TestWebAppFactoryBase.cs  # MODIFIED as needed to drop AppDbContext wiring
```

**Structure Decision**: Web-service (backend-only, no `frontend/` changes). No new projects — extends the existing 5-project structure from feature 001. The bulk of the *deletion* work (Habits/Payments/Notifications + `AppDbContext`) is what makes this feature's diff large despite adding comparatively little new domain surface; the scaffold itself (`TenantMiddleware`, `ICurrentTenantService`, `ITenantDbContextResolver`) is a small, focused addition.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan.

## Implementation-Time Deviations

Discovered and fixed during `/speckit-implement` (2026-07-03), beyond the two design bugs already caught and corrected in tasks.md before implementation began (the `IMiddleware` requirement and `AuthService`'s constructor-injection trap, both R3/R7):

1. **`ITenantDbContextResolver.ForSchema` cannot return the concrete `TenantDbContext` type as research.md R1 originally specified.** `TenantDbContext` lives in `ChildCare.Infrastructure`, which already depends on `ChildCare.Application` (for `IPublicDbContext`/`ITenantProvisioningService`); a `ChildCare.Application.Common` file referencing `TenantDbContext` directly would require the reverse reference too — a circular project reference that doesn't build. Fixed by introducing `ITenantDbContext` (`backend/ChildCare.Application/Common/ITenantDbContext.cs`), a port mirroring the existing `IPublicDbContext` pattern (`SchemaName`, `DbSet<TenantUser> Users`, `DbSet<TenantUserRefreshToken> RefreshTokens`, `SaveChangesAsync`, `MigrateAsync`, `HasPendingMigrationsAsync`); `TenantDbContext` implements it, and the resolver returns the interface.

2. **`Database.MigrateAsync()` cannot apply migrations to a real tenant schema at all**, for two compounding reasons only visible once tested against a live database (the automated tests alone, backed by fresh TestContainers schemas with no prior migration history, didn't exercise the *incremental* rollout path against a genuinely pre-existing schema — this was caught by manually smoke-testing `migrate-tenants` against feature 001's real local dev tenants): (a) EF Core's `PendingModelChangesWarning` compares the live model (real schema name) against the compiled `ModelSnapshot` (which bakes in the design-time placeholder schema `tenant_template`) and always sees a mismatch; (b) each generated migration's SQL also has `tenant_template` baked in as a literal string — the same constraint `TenantProvisioningService`'s baseline-script generation already exists to work around (research.md R6). Fixed by extending `TenantDbContext.MigrateAsync()` to use the same generate-script-against-placeholder-then-substitute-then-execute-raw-SQL technique as `TenantProvisioningService`, generalized to start from the schema's own last-applied migration rather than always from empty.

3. **`TenantMiddleware` was rejecting unmatched routes (404-shaped requests) with `401 errors.tenant.missing`** instead of letting them fall through to the framework's ordinary 404 — it only checked for `[TenantExempt]` metadata, never whether an endpoint had matched at all. Caught by quickstart.md Scenario 5's manual verification (confirming deleted routes like `/api/habits` 404) against the real running server, not by the automated test suite (which only exercised existing, matched routes). Fixed by short-circuiting to `next()` when `context.GetEndpoint()` is null.

4. **`PublicDbContext` (Scoped) can't be resolved from the CLI host's root service provider** — `MigrateTenantsCommand`'s first manual run against real local dev tenants threw `Cannot resolve scoped service ... from root provider`. Fixed by creating an explicit `IServiceScope` around the command invocation in `Program.cs`.

All four were caught either by manually exercising the real running app / real local dev database (deviations 2–4) or by the compiler itself (deviation 1) — a reminder that this feature's scaffold mechanism, despite being thoroughly covered by TestContainers-backed integration tests, still had gaps only a live database with pre-existing (not freshly-provisioned) state and a live HTTP server would surface.
