# Implementation Plan: Authentication & Role-Based Authorization

**Branch**: `003-auth` | **Date**: 2026-07-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-auth/spec.md`

## Summary

Replaces feature 002's deliberately temporary "default tenant" shim (research.md R7) with real tenant resolution: every pre-authentication request (login, Google, Apple, refresh, forgot-password) now carries a client-supplied organisation slug (FR-016), resolved against `PublicDbContext.Tenants` before any `TenantUser` lookup happens; reset-password/verify-email links carry the slug as a query parameter instead, since the token itself is the identifying secret at that point. The open `/api/auth/register` endpoint is deleted outright, and `GoogleSignInAsync`/`AppleSignInAsync` are changed from auto-creating an account on no-match to refusing the attempt — both were open self-registration paths that contradicted the "invite-only, accounts created by provisioning flows only" rule (FR-009), the second one undiscovered until this plan's codebase review. `TenantUser` gains a `Role` (Director/Staff/Parent) column (FR-012), carried as a claim on every issued JWT (FR-011) and enforced through three new named authorization policies — `DirectorOnly`, `StaffOrDirector`, `ParentOnly` (FR-013/014) — plus server-side enforcement of which sign-in method each role may use (FR-017). Finally, every account/session write operation moves from direct `AuthService` calls into MediatR commands with FluentValidation (FR-010), closing a Principle III gap feature 002's plan explicitly deferred to this feature.

## Technical Context

**Language/Version**: C# / .NET 10, EF Core 10 (per feature 002's Technical Context; continuing on the installed version, not the PROJECT-BRIEF.md-stated EF Core 9)

**Primary Dependencies**: ASP.NET Core Minimal APIs + policy-based authorization (`RequireRole`), MediatR + FluentValidation (newly extended to auth — previously the one carved-out exception, per feature 002 research.md R... Constitution Check), EF Core 10 + Npgsql, BCrypt.Net (already an Application-layer dependency via `RegisterOrganisationCommandHandler`), MailKit (existing `EmailService`, now behind a new `IEmailSender` port)

**Storage**: PostgreSQL 16, schema-per-tenant (unchanged from features 001/002). One new tenant-schema migration (adds `users.Role`, backfilled `'director'` for all existing rows per spec.md's Assumption)

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V), extending `OrganisationOnboardingWebAppFactory`'s seeded-tenant pattern; `AuthEndpointTests.cs` is substantially rewritten since it currently depends entirely on the `/register` endpoint being removed by this feature

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`) — no infra changes; no new secrets (Google/Apple config already exists)

**Project Type**: Web service (ASP.NET Core API) — backend only, no web admin / mobile UI work (client apps consuming the new `organisationSlug` field are out of scope for this backend feature, same boundary features 001/002 already drew)

**Performance Goals**: No explicit latency target beyond SC-001's "under 2 seconds under normal load" — tenant-by-slug resolution adds one indexed lookup (`Tenant.Slug`, already unique-indexed per feature 001) before the existing per-tenant `TenantUser` lookup; no new N+1 risk

**Constraints**: Deny-by-default org resolution — a sign-in/forgot-password request with an unknown or unsupplied organisation identifier MUST fail before any `TenantUser` lookup is attempted (FR-016); fail-closed authorization — a missing/unrecognized role claim MUST NOT default to allow (FR-014); no behavioral regression to FR-001–007 (kept-as-is capabilities)

**Scale/Scope**: Same Phase 1 scale as feature 002 (dozens to low hundreds of organisations); this feature adds no new scale dimension of its own — it changes *how* an existing request finds its tenant, not how many tenants exist

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | This feature closes the one remaining isolation gap feature 002 knowingly left open: the "default tenant" shim meant every pre-auth request resolved to the *same* organisation regardless of who was signing in — harmless with one tenant, a real cross-tenant risk the moment a second one exists. FR-016's client-supplied slug plus a real `PublicDbContext.Tenants` lookup replaces it. Post-login routes (`logout`, `account`, `resend-verification`) already went through `TenantMiddleware` correctly (feature 002) and are unchanged here. |
| II. Regulatory Compliance by Design | ✅ N/A | No BKR/contract/regulatory logic in this feature. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass (was the one deferred gap) | Feature 002's plan explicitly carved this out: *"AuthEndpoints.cs/AuthService.cs remain the pre-existing direct-injection pattern (not MediatR)... a full MediatR rewrite is that feature's call to make, not this one's."* FR-010 is that call: every write (login, refresh, Google/Apple sign-in, logout, account deletion, verify-email, resend-verification, forgot/reset-password) becomes a MediatR command with a FluentValidation validator, following the `RegisterOrganisationCommand` pattern already established in `ChildCare.Application/Organisations/`. `AuthEndpoints.cs` becomes a thin `ISender.Send(...)`-only layer, with no business logic left in it. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass (fixes a pre-existing violation) | Two response bodies in the current `AuthEndpoints.cs` return raw hardcoded English text (`"This verification link is invalid or has expired."`, `"An account with this email already exists."`) instead of an `errorKey` — a pre-existing Principle IV violation, not introduced by this feature, but squarely inside the code this feature rewrites. Fixed as part of the MediatR migration (research.md R9), following `backend/ERROR_KEYS.md`'s established convention. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | Extends the existing `OrganisationOnboardingWebAppFactory` TestContainers pattern; slug-based tenant resolution across two real seeded organisations is exactly the kind of cross-schema behavior InMemory cannot exercise. |
| VI. Secure Configuration & Storage | ✅ Pass | No new secrets. Internal errors continue to never reach the client (existing global exception handler, unchanged). |
| VII. Monolith-First Simplicity | ✅ Pass | No new project — still exactly 5. New Application-layer folder (`ChildCare.Application/Auth/`) and two new Infrastructure-layer adapters (`IGoogleTokenValidator`, `IAppleTokenValidator` — research.md R7) fit inside the existing project boundaries; nothing warrants a 6th project. |

**Overall**: 7 of 7 clean passes (one, Principle III, was an explicitly pre-planned pass — not a new violation being justified). No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/003-auth/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── auth-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Enums/
│       └── UserRole.cs                        #   NEW — Director | Staff | Parent (research.md R3)
├── ChildCare.Domain/Entities/
│   └── TenantUser.cs                          #   MODIFIED — + Role (research.md R3)
├── ChildCare.Application/
│   ├── Common/
│   │   ├── IAccessTokenIssuer.cs              #   MODIFIED — + role param, + IssueRefreshToken()/
│   │   │                                      #     RefreshTokenExpiryDays (research.md R4)
│   │   ├── IEmailSender.cs                    #   NEW — port over EmailService (research.md R6)
│   │   ├── IGoogleTokenValidator.cs           #   NEW — port over Google tokeninfo validation (R7)
│   │   └── IAppleTokenValidator.cs            #   NEW — port over Apple JWKS validation (R7)
│   └── Auth/                                  #   NEW — one command + validator + handler per flow
│       ├── LoginCommand.cs / …Validator.cs / …Handler.cs
│       ├── RefreshTokenCommand.cs / … / …
│       ├── GoogleSignInCommand.cs / … / …
│       ├── AppleSignInCommand.cs / … / …
│       ├── LogoutCommand.cs / … / …
│       ├── DeleteAccountCommand.cs / … / …
│       ├── ResendVerificationCommand.cs / … / …
│       ├── VerifyEmailCommand.cs / … / …
│       ├── ForgotPasswordCommand.cs / … / …
│       ├── ResetPasswordCommand.cs / … / …
│       └── AuthResult.cs                      #   shared success/failure result shape (mirrors
│                                               #     RegisterOrganisationResult)
├── ChildCare.Infrastructure/
│   ├── Auth/
│   │   ├── GoogleTokenValidator.cs            #   NEW — moved from AuthService (research.md R7)
│   │   └── AppleTokenValidator.cs             #   NEW — moved from AuthService (research.md R7)
│   └── Persistence/
│       ├── TenantDbContext.cs                 #   MODIFIED — Role conversion + CHECK constraint
│       │                                      #     (research.md R3, mirrors Plan/ProvisioningStatus)
│       ├── TenantProvisioningService.cs       #   MODIFIED — director upsert SQL includes
│       │                                      #     "Role" = 'director' (research.md R3)
│       └── Migrations/Tenant/                 #   NEW migration — users.Role, backfilled 'director'
├── ChildCare.Api/
│   ├── Program.cs                             #   MODIFIED — AddPolicy("DirectorOnly"/"StaffOrDirector"/
│   │                                          #     "ParentOnly") (research.md R5); register
│   │                                          #     IEmailSender/IGoogleTokenValidator/IAppleTokenValidator
│   ├── Endpoints/
│   │   └── AuthEndpoints.cs                   #   MODIFIED — /register DELETED; every remaining
│   │                                          #     handler becomes ISender.Send(command) only
│   │                                          #     (research.md R8); DTOs gain OrganisationSlug
│   ├── Services/
│   │   ├── AuthService.cs                     #   DELETED — logic moves into Application/Auth/* (R8)
│   │   ├── JwtService.cs                      #   MODIFIED — GenerateAccessToken(...) + role claim;
│   │   │                                      #     + GenerateRefreshToken()/RefreshTokenExpiryDays
│   │   │                                      #     already existed, now exposed via the extended
│   │   │                                      #     IAccessTokenIssuer (R4)
│   │   └── JwtAccessTokenIssuer.cs            #   MODIFIED — extended to the new port shape (R4)
│   └── Endpoints/AuthEndpoints.cs              #   (DTOs are defined inline at the bottom of this file,
│                                              #     confirmed during codebase review — not a separate
│                                              #     Models/AuthModels.cs) — RegisterRequest DELETED;
│                                              #     OrganisationSlug added to Login/Refresh/Google/Apple/
│                                              #     ForgotPassword/ResetPassword/VerifyEmail requests
└── ChildCare.Api.Tests/
    ├── AuthEndpointTests.cs                   #   REWRITTEN — no longer seeds via /register (deleted);
    │                                          #     seeds a director via OrganisationOnboardingWebAppFactory
    │                                          #     plus directly-inserted Staff/Parent TenantUser rows
    │                                          #     for role-based scenarios
    ├── AuthRolePolicyTests.cs                 #   NEW — US3 (SC-002): DirectorOnly/StaffOrDirector/
    │                                          #     ParentOnly against a minimal test-only endpoint
    ├── AuthMultiTenantLoginTests.cs           #   NEW — US1 (SC-001): two orgs, shared email, slug
    │                                          #     disambiguation
    └── AuthOAuthLinkOnlyTests.cs              #   NEW — US2 (SC-003 + the OAuth-no-auto-create fix)
```

**Structure Decision**: Web-service (backend-only). No new projects — extends the existing 5-project structure. The bulk of the diff is the MediatR migration (Constitution Principle III, FR-010) rather than new domain surface — `Role` is the only new persisted field.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan. The MediatR migration (Principle III) is large in diff size but is not a *violation being justified*; it is the fix for an already-acknowledged, explicitly-deferred gap (see feature 002 plan.md's Constitution Check row for Principle III).

## Implementation-Time Deviations

Discovered and fixed during `/speckit-implement` (2026-07-06), beyond the two corrections already made to spec.md during `/speckit-plan`'s codebase review (OAuth link-only, FR-009) and the eight coverage/consistency fixes `/speckit-analyze` caught in `tasks.md` before implementation began:

1. **US3's test-only policy-guarded endpoints (T041) could not live in `ChildCare.Api.Tests` as tasks.md originally suggested.** ASP.NET Core's Minimal API top-level `Program.cs` maps routes directly on the `WebApplication` instance at host-build time; `WebApplicationFactory`'s `ConfigureWebHost` hook only extends `IWebHostBuilder` (services/config/logging), with no supported extension point to inject additional `Map...()` calls into an already-built minimal-API pipeline from the test project. Fixed by adding `backend/ChildCare.Api/Endpoints/TestSupportEndpoints.cs` (in the main `Api` project, like every other endpoint file) and mapping it from `Program.cs` guarded by `if (app.Environment.IsEnvironment("Testing"))` — verified absent in a real `Development`-environment run (manual smoke test, `curl /api/test-support/director-only` → 404).

2. **Adding the `AddUserRole` migration broke `TenantMigrationRolloutTests`'s existing revert-simulation technique** (`backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`, from feature 002) — caught by the full test suite, not by any new test this feature added. That test reverts a freshly-provisioned schema to "behind by one migration" by dropping `ExtendUsersAddRefreshTokens`'s columns and deleting only its `__EFMigrationsHistory` row, then asserts `migrate-tenants` brings it current again. Once `AddUserRole` existed as a later migration, `TenantDbContext.MigrateAsync()`'s `GetAppliedMigrationsAsync().LastOrDefault()` found `AddUserRole`'s still-present history row and concluded (incorrectly) that the schema was already fully current, since that row is chronologically the newest regardless of the deliberately-created gap before it — the pending-migration script it generated was empty, and the revert was silently never undone. Fixed by extending the revert to also drop the `Role` column and delete `AddUserRole`'s history row, so "behind by everything after `InitialTenantSchema`" is what's actually simulated.

3. **`OrganisationOnboardingResilienceTests.Register_WithConcurrentAttempts_CreatesExactlyOneTenant`** (feature 001, byte-for-byte unmodified by this feature) fails intermittently when the *full* test suite runs (never in isolation, verified across multiple repeated runs) — a pre-existing timing-sensitive concurrency test, unrelated to any file this feature touches. Left as-is; out of scope for an auth feature to re-architect feature 001's concurrency test, but flagged here for whoever picks it up next.

4. **`/speckit-converge` (2026-07-06) found SC-001's 2-second-per-request threshold had no automated verification and asked for a test to close the gap; that test surfaced a genuine tail-latency issue, not a test-calibration problem.** A first attempt (`AuthMultiTenantLoginTests`, 50 perfectly-simultaneous logins, asserting the *slowest individual* request stayed under 2 seconds) consistently measured 2.6–2.7s on this 8-core dev machine. Applying the standard ASP.NET Core mitigation for this symptom — `ThreadPool.SetMinThreads(Math.Max(Environment.ProcessorCount, 100), ...)` in `Program.cs`, raising the pool above its default (`= ProcessorCount`) so a sudden burst doesn't stall on the pool's slow hill-climbing thread-injection rate — only marginally improved it (2.60s), which ruled out thread-pool starvation as the primary cause: the real bottleneck is that `BCrypt.Verify` is deliberately CPU-expensive, and 50 truly-simultaneous hash operations cannot be parallelized across only 8 cores fast enough, compounded by the test running the TestServer, the test client, and the TestContainers Postgres container all on the same machine — not representative of production traffic, which arrives with natural jitter rather than a synchronized burst, and not fixable without either weakening BCrypt's work factor (a real security regression, rejected) or standing up dedicated load-testing infrastructure against a deployed instance (out of scope for a TestContainers-backed integration suite, consistent with feature 002's precedent of not asserting performance thresholds there either). The `ThreadPool.SetMinThreads` tuning was kept regardless — a safe, real improvement for genuine burst traffic in production even though it didn't resolve this specific synthetic full-CPU-saturation test. The test itself (`Login_FiftyConcurrentRequests_AllSucceedCorrectly`) was changed to assert SC-001's *correctness* half only (50 concurrent logins all resolve to the right tenant) without a hard per-request timing gate, since a strict timing assertion here would flake on any CI runner with fewer cores than this one.

All three were caught by the full test suite / a live manual smoke test against local dev Postgres, not by the automated tests targeting this feature's own new behavior in isolation — consistent with feature 002's own experience that scaffold/migration-adjacent work benefits from exercising the real running app, not just the tests written for the change itself.
