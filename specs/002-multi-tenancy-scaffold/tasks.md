---

description: "Task list for feature implementation"
---

# Tasks: Multi-Tenancy Scaffold

**Input**: Design documents from `/specs/002-multi-tenancy-scaffold/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL — schema-per-tenant `search_path`/isolation behavior has no InMemory equivalent. Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 isolation, US2 P1 fail-closed rejection, US3 P2 migration rollout). Note: unlike a typical feature, almost the entire mechanism (`TenantMiddleware`, `ICurrentTenantService`, `ITenantDbContextResolver`, the legacy-auth migration) is shared infrastructure every story depends on, so it lives in Foundational — the story phases are primarily validation (tests) of that shared mechanism, not story-specific implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

Backend-only feature (no frontend/mobile changes). All paths are under `backend/`, per plan.md's Project Structure.

---

## Phase 1: Setup

**Purpose**: This feature introduces no new project — all 5 projects already exist (feature 001). Nothing to scaffold beyond confirming the baseline.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds on this branch before starting (no new projects/packages needed for this feature)

**Checkpoint**: Baseline confirmed; no new project structure needed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The scaffold mechanism itself (`TenantMiddleware`, `ICurrentTenantService`, `ITenantDbContextResolver`), plus retiring the legacy single-shared-schema model. Every user story's tests depend on this being complete — in particular, the only currently-existing authenticated, tenant-resolvable routes in the whole app are the migrated `AuthEndpoints.cs` routes (feature 001's `AdminEndpoints`/`OrganisationEndpoints` are exempt, not tenant-scoped), so the Auth migration is a prerequisite for US1/US2's tests, not a separate, deferrable concern.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Data model

- [X] T002 [P] Extend `TenantUser` entity in `backend/ChildCare.Domain/Entities/TenantUser.cs`: add `GoogleId`, `AppleId`, `EmailVerified`, `EmailVerificationToken`, `EmailVerificationExpiry`, `PasswordResetToken`, `PasswordResetExpiry` (data-model.md, research.md R5)
- [X] T003 [P] Create `TenantUserRefreshToken` entity in `backend/ChildCare.Domain/Entities/TenantUserRefreshToken.cs` (`Id`, `TenantUserId`, `Token`, `ExpiresAt` — data-model.md, research.md R5)
- [X] T004 Update `TenantDbContext` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`: extend `TenantUser` EF configuration for the new fields, register `TenantUserRefreshToken` DbSet with FK/cascade-delete/unique-index-on-Token config (data-model.md) — depends on T002, T003
- [X] T005 Generate the updated `TenantDbContext` migration (`dotnet ef migrations add ExtendUsersAddRefreshTokens --context TenantDbContext`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (research.md R6) — depends on T004

### Scaffold mechanism

- [X] T006 [P] Create `ICurrentTenantService` port in `backend/ChildCare.Application/Common/ICurrentTenantService.cs` (read-only `TenantId`, `SchemaName`, `TenantSlug` — research.md R2)
- [X] T007 [P] Create `CurrentTenantService` concrete class in `backend/ChildCare.Infrastructure/Persistence/CurrentTenantService.cs` (settable properties, implements `ICurrentTenantService` — research.md R2) — depends on T006
- [X] T008 [P] Create `ITenantDbContextResolver` port in `backend/ChildCare.Application/Common/ITenantDbContextResolver.cs` (`TenantDbContext ForSchema(string schemaName)` — research.md R1). **Implementation-time deviation**: `TenantDbContext` lives in `ChildCare.Infrastructure`, which already depends on `ChildCare.Application` (for `IPublicDbContext`/`ITenantProvisioningService`) — a literal `TenantDbContext ForSchema(...)` signature in `Application.Common` would require `Application` → `Infrastructure`, a circular project reference that does not build. Fixed by introducing a new `ITenantDbContext` port (`backend/ChildCare.Application/Common/ITenantDbContext.cs`, mirroring the existing `IPublicDbContext` pattern: `SchemaName`, `DbSet<TenantUser> Users`, `DbSet<TenantUserRefreshToken> RefreshTokens`, `SaveChangesAsync`, `MigrateAsync`) and having `ITenantDbContextResolver.ForSchema` return `ITenantDbContext` instead; `TenantDbContext` implements it.
- [X] T009 Implement `TenantDbContextResolver` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContextResolver.cs` (centralizes the `DbContextOptionsBuilder` + `DynamicSchemaModelCacheKeyFactory` wiring already used by feature 001's design-time factory — research.md R1) — depends on T008, T004
- [X] T010 [P] Create `TenantExemptAttribute` marker class plus a `RequireTenantExempt()` `RouteHandlerBuilder`/`IEndpointConventionBuilder` extension method in `backend/ChildCare.Api/Middleware/TenantExemptAttribute.cs` (research.md R3)
- [X] T011 Implement `TenantMiddleware` in `backend/ChildCare.Api/Middleware/TenantMiddleware.cs` **implementing `IMiddleware`** (not the conventional constructor+`InvokeAsync` pattern — `IMiddleware` is resolved via DI's `IMiddlewareFactory`, so a Singleton registration is resolvable both by the pipeline and by test code via `factory.Services.GetRequiredService<TenantMiddleware>()`; the conventional pattern is constructed via `ActivatorUtilities` and is *not* DI-resolvable, which would make the test seam below unreachable — research.md R3): `InvokeAsync(HttpContext context, RequestDelegate next)` resolves the *scoped* `PublicDbContext`/`CurrentTenantService` from `context.RequestServices` (never via constructor injection, since the middleware instance itself is a singleton); skip if `[TenantExempt]` endpoint metadata present; else read the `tenant_id` claim, look up the tenant via `PublicDbContext`, reject with `errors.tenant.missing` (FR-006) / `errors.tenant.not_found` (FR-007, and FR-008a's lookup-failure case, after logging the real exception server-side) / `errors.tenant.not_ready` (FR-008); on success, populate `CurrentTenantService`. Include a settable `FailureInjectionHookForTests` property, checked immediately before the `PublicDbContext` lookup, so integration tests can deterministically simulate a lookup failure (FR-008a, research.md R3 — mirrors feature 001's `TenantProvisioningService.FailureInjectionHookForTests` pattern) — depends on T007, T010
- [X] T012 [P] Add `errors.tenant.missing`, `errors.tenant.not_found`, `errors.tenant.not_ready` entries to `backend/ERROR_KEYS.md` (research.md R10)
- [X] T013 Register `CurrentTenantService`/`ICurrentTenantService` (Scoped), `TenantDbContextResolver`/`ITenantDbContextResolver` (Singleton — stateless, mirrors `TenantProvisioningService`'s existing registration), `TenantDbContext` (Scoped, built via the resolver reading `ICurrentTenantService.SchemaName`), `TenantMiddleware` (Singleton, since it implements `IMiddleware` — research.md R3), and `app.UseMiddleware<TenantMiddleware>()` (after `UseAuthentication()`/`UseAuthorization()`) in `backend/ChildCare.Api/Program.cs` — depends on T007, T009, T011
- [X] T014 [P] Mark `POST /api/admin/invitations`, `POST /api/organisations/register`, and `GET /health` with `.RequireTenantExempt()` in `backend/ChildCare.Api/Endpoints/AdminEndpoints.cs`, `OrganisationEndpoints.cs`, and `Program.cs` (FR-015, research.md R3) — depends on T010

### Legacy cleanup

- [X] T015 [P] Delete `backend/ChildCare.Api/Endpoints/{HabitEndpoints,PaymentEndpoints,NotificationEndpoints}.cs` and their route registrations in `Program.cs` (research.md R4)
- [X] T016 [P] Delete `backend/ChildCare.Api/Services/StripeService.cs` and its DI registration + `Stripe.StripeConfiguration.ApiKey` wiring in `Program.cs` (research.md R4) — depends on T015
- [X] T017 [P] Delete `backend/ChildCare.Api/Data/AppDbContext.cs` and `backend/ChildCare.Api/Models/{User,UserRefreshToken,Habit,HabitCompletion}.cs` (research.md R4) — depends on T015, T016
- [X] T018 Delete `backend/ChildCare.Api/Migrations/` (AppDbContext's own migrations — research.md R4) — depends on T017
- [X] T019 Update `backend/ChildCare.Api/Services/JwtService.cs`: remove the `GenerateAccessToken(User user)` overload (research.md R7) — depends on T017
- [X] T020 Update `backend/ChildCare.Api/Services/AuthService.cs`: replace `AppDbContext`/`Models.User` with `TenantUser`, accessed via `ITenantDbContextResolver.ForSchema(...)` called explicitly in every method — **never** the DI-scoped `TenantDbContext` (which would be unset/wrong for the pre-auth exempt routes, since `TenantMiddleware` never runs for them). Pre-auth methods (register/login/google/apple/refresh/forgot-password/reset-password/verify-email) resolve the schema via the default-tenant shim (query `IPublicDbContext.Tenants` for the earliest `Ready` tenant); post-login methods (logout/account-deletion/resend-verification) resolve it via `ICurrentTenantService.SchemaName` (already set correctly by `TenantMiddleware`, since these routes are non-exempt). Issue tokens via `JwtService.GenerateAccessToken(Guid, string, Guid)` so every legacy-auth JWT carries a real `tenant_id` claim (research.md R7) — depends on T009, T019, T017, T007
- [X] T021 [P] Apply `.RequireTenantExempt()` to `register`/`login`/`google`/`apple`/`refresh`/`forgot-password`/`reset-password`/`verify-email` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs`; leave `logout`, `account` (DELETE), and `resend-verification` non-exempt so they resolve normally via the shim's baked-in `tenant_id` claim (research.md R3, R7) — depends on T020, T010
- [X] T022 Migrate `backend/ChildCare.Api.Tests/AuthEndpointTests.cs` from `ChildCareWebAppFactory` (InMemory) to `OrganisationOnboardingWebAppFactory` (real TestContainers Postgres — constitution Principle V, and PROJECT-BRIEF.md's own previously-flagged debt item); assert the migrated model and that issued tokens carry a `tenant_id` claim — depends on T020. Factory now seeds one Ready tenant in `InitializeAsync` (via the real invitation+registration HTTP flow) so `AuthService`'s pre-auth shim has something to resolve against.
- [X] T023 [P] Delete `backend/ChildCare.Api.Tests/HabitEndpointTests.cs` and `backend/ChildCare.Api.Tests/MiniStackWebAppFactory.cs` (both fully superseded once Habits are gone and Auth tests move off it — research.md R4) — depends on T015, T022

**Checkpoint**: Solution builds; `AuthEndpoints.cs` compiles and runs against the new model; no Habit/Payment/Notification/Stripe code remains; user story validation can now begin.

---

## Phase 3: User Story 1 - Every Request Is Automatically Scoped to Its Own Organisation (Priority: P1)

**Goal**: Prove that a resolved organisation's requests are structurally confined to that organisation's own data, including under concurrency and connection reuse.

**Independent Test**: Seed two organisations, issue requests as each (via the now tenant-resolving `AuthEndpoints.cs` routes), confirm each only ever touches its own schema — including two organisations' requests running concurrently, and sequential requests reusing the same underlying connection.

### Tests for User Story 1

- [X] T024 [P] [US1] Integration test: two organisations' authenticated users each only affect their own tenant schema when calling a non-exempt route, in `backend/ChildCare.Api.Tests/TenantIsolationTests.cs` (SC-001, quickstart.md Scenario 1) — depends on T021
- [X] T025 [P] [US1] Integration test: concurrent requests from two different organisations never leak tenant context between them (`Task.WhenAll`, mirroring feature 001's `OrganisationOnboardingResilienceTests.cs` concurrency pattern), same file — depends on T021
- [X] T026 [P] [US1] Integration test: two *sequential* (not concurrent) requests for two different organisations, made with the same `HttpClient`/underlying connection, resolve correctly each time — the second request's schema must never be stale from the first (spec.md Edge Cases, 3rd bullet), same file — depends on T021

**Checkpoint**: US1 is proven — the mechanism built in Foundational is validated end-to-end; no additional implementation needed for this story.

---

## Phase 4: User Story 2 - Invalid or Missing Organisation Context Is Rejected Before Any Data Is Touched (Priority: P1)

**Goal**: Prove the fail-closed guarantee for every rejection case in FR-006/007/008/008a.

**Independent Test**: Send requests with no tenant identified, an unknown tenant, a not-yet-ready tenant, and a lookup that fails outright; confirm each is rejected before any organisation data is queried, and that a failed lookup is indistinguishable from an unknown tenant to the caller.

### Tests for User Story 2

- [X] T027 [P] [US2] Integration test: a JWT with no `tenant_id` claim is rejected before any data access, `errorKey: errors.tenant.missing` (FR-006), in `backend/ChildCare.Api.Tests/TenantRejectionTests.cs` (quickstart.md Scenario 2) — depends on T021
- [X] T028 [P] [US2] Integration test: a `tenant_id` that matches no known tenant is rejected, `errorKey: errors.tenant.not_found` (FR-007), same file — depends on T021
- [X] T029 [P] [US2] Integration test: a `tenant_id` for a tenant with `ProvisioningStatus != Ready` is rejected, `errorKey: errors.tenant.not_ready` (FR-008), same file — depends on T021
- [X] T030 [P] [US2] Integration test: using `TenantMiddleware.FailureInjectionHookForTests` (T011) to simulate a lookup failure for a *valid, known, ready* tenant — assert the response is byte-for-byte the same `errorKey: errors.tenant.not_found` as the unknown-tenant case (T028), and separately assert (e.g. via a captured `ILogger`/log sink) that the real simulated exception was logged server-side (FR-008a), same file — depends on T011, T021
- [X] T031 [US2] Integration test: a malformed/non-GUID `tenant_id` claim is rejected the same way as unknown (spec.md Edge Cases), same file — depends on T027, T028, T029. **Implementation-time bug found and fixed**: `TenantMiddleware` originally treated a malformed claim identically to a *missing* claim (401 `errors.tenant.missing`); spec.md's Edge Cases section is explicit that a malformed/garbled claim must be treated the same as "unknown organisation" (403 `errors.tenant.not_found`), which this test caught — `TenantMiddleware.cs` and `ERROR_KEYS.md` corrected accordingly.

**Checkpoint**: US2 is proven — every FR-006/007/008/008a rejection path is covered.

---

## Phase 5: User Story 3 - Rolling Out a Change to Every Organisation Without Manual Work (Priority: P2)

**Goal**: An operator applies a pending structural change to every existing organisation's workspace via one action.

**Independent Test**: Introduce a pending migration, run the rollout command, confirm every existing `Ready` organisation has it applied with no per-organisation manual step; re-running is a no-op.

### Implementation for User Story 3

- [X] T032 [P] [US3] Implement `MigrateTenantsCommand` in `backend/ChildCare.Api/Cli/MigrateTenantsCommand.cs`: iterate `Ready` tenants via `IPublicDbContext`, apply pending migrations per tenant, print per-tenant outcome + summary, non-zero exit on any failure (contracts/migrate-tenants-cli.md, research.md R8). **Implementation-time bug found and fixed** (caught by manually smoke-testing against real local dev tenants, not just the automated tests): the contract's literal `Database.MigrateAsync()` cannot work against a real tenant schema at all — (1) EF Core's `PendingModelChangesWarning` check compares the live model (real schema name) against the compiled ModelSnapshot (baked-in design-time placeholder schema `tenant_template`) and always sees a mismatch; (2) more fundamentally, each generated migration's SQL has the placeholder schema baked in as a literal string (the same constraint `TenantProvisioningService`'s baseline-script generation already documents, research.md R6), so even past the warning, `Database.MigrateAsync()` fails with `schema "tenant_template" does not exist`. Fixed by extending `TenantDbContext.MigrateAsync()` (`backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`) to use the same generate-script-against-placeholder-then-substitute-then-execute-raw-SQL technique as `TenantProvisioningService`, generalized to generate from the schema's own last-applied migration (via `Database.GetAppliedMigrationsAsync()`) rather than always from empty — added `ITenantDbContext.HasPendingMigrationsAsync()`/`MigrateAsync()` accordingly. Verified against two real tenants provisioned by feature 001 in local dev Postgres: first run reported `migrated` for both and the extended columns/table were confirmed present via `psql`; second run reported `already up to date` for both (idempotent).
- [X] T033 Wire the `migrate-tenants` args check at the very top of `backend/ChildCare.Api/Program.cs`, before the web host is built (research.md R8) — depends on T032. **Implementation-time bug found and fixed**: `PublicDbContext` is registered Scoped, but the CLI's minimal host was resolving it from the *root* service provider, which EF Core's scope validator rejects (`Cannot resolve scoped service ... from root provider`) — fixed by creating an explicit `IServiceScope` around the `MigrateTenantsCommand.RunAsync` call.

### Tests for User Story 3

- [X] T034 [P] [US3] Integration test: the rollout command applies a pending migration to every existing `Ready` tenant with no manual per-tenant step, in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (FR-010, quickstart.md Scenario 3) — depends on T033
- [X] T035 [P] [US3] Integration test: re-running the rollout against already-migrated tenants is a no-op — no error, nothing re-applied (FR-011), same file — depends on T033

**Checkpoint**: All three user stories are independently proven.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T036 [P] Write a manual, reviewed SQL script at `backend/scripts/drop-legacy-appdbcontext-tables.sql` to drop the orphaned legacy tables (`Users`, `Habits`, `HabitCompletions`, `UserRefreshTokens`, default Postgres schema) — documented as a manual, per-environment operation, never invoked by CI/CD or application startup (research.md R9). **Discovered while writing this**: `public."__EFMigrationsHistory"` is shared with `PublicDbContext` (feature 001) — the script deletes only `AppDbContext`'s own named migration rows, never the whole table. Verified end-to-end against local dev Postgres: the four legacy tables dropped cleanly and `PublicDbContext`'s own migration row (`..._InitialPublicSchema`) was left intact.
- [X] T037 Run quickstart.md's 5 scenarios end-to-end against local Docker Postgres. **Bug found and fixed**: manually running Scenario 5 (a deleted route like `/api/habits` must 404) against the real running server surfaced that `TenantMiddleware` was rejecting *unmatched* routes with `401 errors.tenant.missing` instead of letting them fall through to the framework's ordinary 404 — the middleware only checked for `[TenantExempt]` metadata, never whether an endpoint had matched at all. Fixed in `TenantMiddleware.cs` (`endpoint is null` now short-circuits to `next()`); added a regression test (`TenantRejectionTests.UnmatchedRoute_ReturnsNotFound_NotATenantRejection`). All 5 scenarios then re-verified against the real local dev server (`dotnet run`, Docker Postgres, two feature-001-provisioned tenants) via curl: health/exempt routes work, fail-closed rejection returns `errors.tenant.missing`, `register`→`login`→`resend-verification` round-trips correctly with a real `tenant_id` claim, and the deleted legacy routes 404 correctly after the fix.
- [X] T038 [P] Update `specs/002-multi-tenancy-scaffold/plan.md` with any implementation-time deviations discovered (mirroring feature 001's convergence discipline)
- [X] T039 Full solution build + full test suite pass (`dotnet build backend/ChildCare.sln`, `dotnet test backend/ChildCare.Api.Tests`) confirming no regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (the scaffold mechanism and the only tenant-resolvable routes in the app are both built here).
- **User Stories (Phase 3-5)**: All depend on Foundational completion. US1 and US2 are both P1 and share the same underlying mechanism/tests file conventions but touch different, independent test files — they can proceed in parallel. US3 is independent of US1/US2 (different mechanism entirely — migration rollout, not request-time resolution) and can also proceed in parallel once Foundational is done.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Within Each User Story

- US1/US2: test-only phases (no new implementation — the mechanism is Foundational); tests can be written in parallel within each phase since they target different scenarios. T030 (US2) depends specifically on T011's `FailureInjectionHookForTests` seam.
- US3: `MigrateTenantsCommand` (implementation) before its own tests.

### Parallel Opportunities

- All `[P]`-marked Foundational tasks touching different files (T002/T003, T006/T007, T008, T010, T012, T014-T017) can run in parallel within their dependency constraints.
- Once Foundational completes, US1, US2, and US3 can all proceed in parallel (different test files, no shared mutable state).

---

## Parallel Example: Foundational Data Model

```bash
# Launch in parallel (different files, no dependencies on each other):
Task: "Extend TenantUser entity in backend/ChildCare.Domain/Entities/TenantUser.cs"
Task: "Create TenantUserRefreshToken entity in backend/ChildCare.Domain/Entities/TenantUserRefreshToken.cs"
```

## Parallel Example: User Stories (post-Foundational)

```bash
# Launch together once Phase 2 is complete:
Task: "Integration tests for tenant isolation + connection reuse in TenantIsolationTests.cs" (US1)
Task: "Integration tests for tenant rejection incl. lookup-failure injection in TenantRejectionTests.cs" (US2)
Task: "Implement MigrateTenantsCommand + its tests" (US3)
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1: Setup (trivial — no new projects).
2. Complete Phase 2: Foundational (CRITICAL — this is most of the feature's real work).
3. Complete Phase 3 + Phase 4 (US1 + US2, both P1) — this is the MVP: proven tenant isolation and fail-closed rejection.
4. **STOP and VALIDATE**: run quickstart.md Scenarios 1, 2, 4, 5.
5. Add Phase 5 (US3, P2 — migration rollout) when ready.

### Incremental Delivery

1. Foundational → the scaffold mechanism exists and legacy code is gone.
2. US1 + US2 → isolation and fail-closed rejection proven (MVP).
3. US3 → rollout mechanism proven.
4. Polish → cleanup script, full validation, full test suite.

---

## Notes

- `[P]` tasks touch different files with no unmet dependencies.
- `[Story]` labels map every user-story-phase task back to spec.md for traceability.
- Tests are included per constitution Principle V (NON-NEGOTIABLE), scoped to the happy path + key negative/fail-closed flows (global CLAUDE.md convention) — not exhaustive coverage of every helper method.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently before continuing.
- The Foundational phase is unusually large for this feature because US1/US2/US3 validate an already-built shared mechanism rather than adding story-specific implementation — this is expected for a scaffold-type feature, not a sign of miscategorized work.
- `/speckit-analyze` (2026-07-03 first pass) found two HIGH coverage gaps (FR-008a lookup-failure rejection, and the connection-reuse edge case) and one MEDIUM format gap (T034's missing file path, now `backend/scripts/drop-legacy-appdbcontext-tables.sql`) — all three are closed by T011's `FailureInjectionHookForTests` seam + T030 (US2), T026 (US1), and T036's explicit path, respectively.
- While re-verifying those closes before the second `/speckit-analyze` pass, two design bugs surfaced and were fixed in research.md/tasks.md before either was ever implemented: (1) `TenantMiddleware` must implement `IMiddleware` (Singleton, DI-resolvable), not the conventional constructor+`InvokeAsync` pattern — the latter isn't resolvable from the container, so the `FailureInjectionHookForTests` seam would have been unreachable by tests (T011); (2) `AuthService` must call `ITenantDbContextResolver.ForSchema(...)` explicitly in every method rather than taking the DI-scoped `TenantDbContext` as a constructor dependency, since that registration reads `ICurrentTenantService.SchemaName`, which is never set for the pre-auth exempt routes `TenantMiddleware` doesn't run for (T020).
