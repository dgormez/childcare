---

description: "Task list for feature implementation"
---

# Tasks: Organisation Onboarding

**Input**: Design documents from `/specs/001-organisation-onboarding/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL for the happy path plus key negative/regulatory flows; quickstart.md's Scenario 3 explicitly calls for TestContainers-based tests for the resilience/concurrency behavior. Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1, US2 both P1; US3 P2) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

Backend-only feature (no frontend/mobile changes). All paths are under `backend/`, per plan.md's Project Structure.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Introduce the constitution's 5-project solution structure (this is the first feature since ratification — research.md R1). The existing `ChildCare.Api`/`ChildCare.Api.Tests` are extended, never restructured or gutted.

- [X] T001 Create `backend/ChildCare.sln` referencing the existing `ChildCare.Api` and `ChildCare.Api.Tests` plus the four new projects created below
- [X] T002 [P] Scaffold `backend/ChildCare.Domain/ChildCare.Domain.csproj` (net10.0, no EF Core / ASP.NET package references — constitution: Domain has no EF deps)
- [X] T003 [P] Scaffold `backend/ChildCare.Contracts/ChildCare.Contracts.csproj` (net10.0, no package references)
- [X] T004 [P] Scaffold `backend/ChildCare.Application/ChildCare.Application.csproj` (net10.0; PackageReference `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`; ProjectReference → `ChildCare.Domain`, `ChildCare.Contracts`)
- [X] T005 [P] Scaffold `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj` (net10.0; PackageReference `Microsoft.EntityFrameworkCore.Design` + `Npgsql.EntityFrameworkCore.PostgreSQL`, matching the versions already in `backend/ChildCare.Api/ChildCare.Api.csproj`; ProjectReference → `ChildCare.Domain`, `ChildCare.Application`)
- [X] T006 Add ProjectReferences from `ChildCare.Api` to `ChildCare.Application`, `ChildCare.Infrastructure`, `ChildCare.Contracts` in `backend/ChildCare.Api/ChildCare.Api.csproj`
- [X] T007 [P] Add `Testcontainers.PostgreSql` PackageReference to `backend/ChildCare.Api.Tests/ChildCare.Api.Tests.csproj` (constitution Principle V — real infrastructure, not InMemory)

**Checkpoint**: Solution builds with all 6 projects wired together; no feature code yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain entities, both DbContexts, the MediatR/FluentValidation pipeline, and invitation creation — every user story's independent test depends on being able to issue an invitation first (spec.md US1's own Independent Test starts with "Issue a valid invitation...").

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T008 [P] Create `Tenant` entity in `backend/ChildCare.Domain/Entities/Tenant.cs` (`Id`, `Name`, `Slug`, `SchemaName`, `Plan`, `ProvisioningStatus`, `CreatedFromInvitationId`, `CreatedAt` — data-model.md)
- [X] T009 [P] Create `Invitation` entity in `backend/ChildCare.Domain/Entities/Invitation.cs` (`Id`, `Email`, `TokenHash`, `ExpiresAt`, `CreatedAt` — data-model.md; no `UsedAt`, per research.md R10)
- [X] T010 [P] Create `TenantUser` entity in `backend/ChildCare.Domain/Entities/TenantUser.cs` (`Id`, `Email`, `PasswordHash`, `Name`, `CreatedAt` — data-model.md)
- [X] T011 [P] Create `PlanTier` and `ProvisioningStatus` enums in `backend/ChildCare.Domain/Enums/PlanTier.cs` and `backend/ChildCare.Domain/Enums/ProvisioningStatus.cs` (research.md R9 — stored as `text` + `CHECK` via `HasConversion<string>()`, not a native Postgres enum)
- [X] T012 Create `PublicDbContext` in `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs` (`Tenants`, `Invitations` DbSets; unique constraints on `Tenant.Slug`, `Tenant.SchemaName`, `Tenant.CreatedFromInvitationId` — data-model.md, research.md R10) — depends on T008, T009, T011
- [X] T013 Generate the initial `PublicDbContext` migration (`dotnet ef migrations add InitialPublicSchema --context PublicDbContext`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/` — depends on T012
- [X] T014 Create `TenantDbContext` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` with a runtime-settable default schema and a `Users` DbSet — depends on T010
- [X] T015 Implement `DynamicSchemaModelCacheKeyFactory` in `backend/ChildCare.Infrastructure/Persistence/DynamicSchemaModelCacheKeyFactory.cs` (`IModelCacheKeyFactory` so EF Core's compiled-model cache is keyed per schema name — research.md R6) — depends on T014
- [X] T016 Generate the baseline `TenantDbContext` migration (`dotnet ef migrations add InitialTenantSchema --context TenantDbContext`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` — depends on T014
- [X] T017 Implement `TenantProvisioningService` in `backend/ChildCare.Infrastructure/Persistence/TenantProvisioningService.cs`: `CREATE SCHEMA IF NOT EXISTS`, apply `TenantDbContext.Database.Migrate()` against that schema, insert the director's `TenantUser` row (research.md R6) — depends on T015, T016
- [X] T018 [P] Implement `ValidationBehavior<TRequest,TResponse>` MediatR pipeline behaviour in `backend/ChildCare.Application/Common/Behaviors/ValidationBehavior.cs` (constitution Principle III — FluentValidation runs before every handler)
- [X] T019 [P] Define the `IAccessTokenIssuer` port in `backend/ChildCare.Application/Common/IAccessTokenIssuer.cs` (research.md R8)
- [X] T020 Register `PublicDbContext`, a `TenantDbContext` factory, MediatR (with `ValidationBehavior`), FluentValidation validators, and `TenantProvisioningService` in `backend/ChildCare.Api/Program.cs` — depends on T012, T014, T017, T018
- [X] T021 [P] Create `CreateInvitationRequest`/`CreateInvitationResponse` records in `backend/ChildCare.Contracts/Requests/CreateInvitationRequest.cs` and `backend/ChildCare.Contracts/Responses/CreateInvitationResponse.cs` (per contracts/create-invitation.md — mirrors the Contracts-project split used for `RegisterOrganisation`, T030)
- [X] T022 [P] Create `CreateInvitationCommand` in `backend/ChildCare.Application/Invitations/CreateInvitationCommand.cs` (per contracts/create-invitation.md) — depends on T021
- [X] T023 [P] Create `CreateInvitationCommandValidator` in `backend/ChildCare.Application/Invitations/CreateInvitationCommandValidator.cs` (email required, valid format)
- [X] T024 Implement `CreateInvitationCommandHandler` in `backend/ChildCare.Application/Invitations/CreateInvitationCommandHandler.cs`: generate an opaque token, persist its SHA-256 hash + email + expiry via `PublicDbContext` (research.md R4) — depends on T009, T012, T022
- [X] T025 Add `SuperAdmin:ApiKey` configuration and a constant-time comparison check in `backend/ChildCare.Api/Auth/SuperAdminKeyHandler.cs` (research.md R11) — depends on T020
- [X] T026 Map `POST /api/admin/invitations` in `backend/ChildCare.Api/Endpoints/AdminEndpoints.cs`, gated by the superadmin key check, dispatching `CreateInvitationCommand` (contracts/create-invitation.md) — depends on T024, T025
- [X] T027 Register the new endpoint mapping (`app.MapAdminEndpoints()`) in `backend/ChildCare.Api/Program.cs` — depends on T026

**Checkpoint**: An operator can create an invitation end-to-end. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director registers and gets an immediately usable organisation (Priority: P1) 🎯 MVP

**Goal**: A director with a valid invitation completes registration and immediately has a working, logged-in admin session for their own organisation — synchronously, in one request.

**Independent Test**: Issue a valid invitation (Foundational phase capability), complete registration through it, and verify the response includes a working `accessToken` for a `ready` organisation — with no operator intervention between registration and login.

### Tests for User Story 1 ⚠️

> Write these tests first; confirm they fail before the implementation tasks below make them pass.

- [X] T028 [P] [US1] Integration test: full happy-path registration (spec.md US1 acceptance scenarios 1–4; quickstart.md Scenario 1) in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs`, using TestContainers PostgreSQL
- [X] T029 [P] [US1] Integration test: registration succeeds with no `dossiernummer`/KBO fields present in the request at all (FR-012, SC-006) in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs`

### Implementation for User Story 1

- [X] T030 [P] [US1] Create `RegisterOrganisationRequest`/`RegisterOrganisationResponse` records in `backend/ChildCare.Contracts/Requests/RegisterOrganisationRequest.cs` and `backend/ChildCare.Contracts/Responses/RegisterOrganisationResponse.cs` (contracts/register-organisation.md)
- [X] T031 [US1] Create `RegisterOrganisationCommand` in `backend/ChildCare.Application/Organisations/RegisterOrganisationCommand.cs` — depends on T030
- [X] T032 [US1] Create `RegisterOrganisationCommandValidator` in `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandValidator.cs` (org name / director name required; email format; password minimum 8 characters — research.md R12) — depends on T031
- [X] T033 [US1] Implement `RegisterOrganisationCommandHandler` in `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandHandler.cs`: resolve invitation by token hash, reject not-found/expired with a generic `errors.invitation.not_found` (404 — FR-003, FR-005, research.md R5), derive slug with collision retry (research.md R14), atomically insert the `Tenant` row keyed by `CreatedFromInvitationId` (research.md R10), call `TenantProvisioningService`, mark `Tenant` ready, issue the access token via `IAccessTokenIssuer` — depends on T017, T019, T032
- [X] T034 [P] [US1] Add a `JwtService.GenerateAccessToken(Guid userId, string email, Guid tenantId)` overload with a `tenant_id` claim in `backend/ChildCare.Api/Services/JwtService.cs` (additive; the existing `User`-typed overload is untouched — research.md R8)
- [X] T035 [US1] Implement the `IAccessTokenIssuer` adapter in `backend/ChildCare.Api/Services/JwtAccessTokenIssuer.cs`, delegating to `JwtService` — depends on T019, T034
- [X] T036 [US1] Register the `IAccessTokenIssuer` adapter in `backend/ChildCare.Api/Program.cs` DI — depends on T035
- [X] T037 [US1] Map `POST /api/organisations/register` in `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs`, dispatching `RegisterOrganisationCommand` and mapping responses per contracts/register-organisation.md — depends on T033, T036
- [X] T038 [US1] Register the new endpoint mapping (`app.MapOrganisationEndpoints()`) in `backend/ChildCare.Api/Program.cs` — depends on T037

**Checkpoint**: User Story 1 is fully functional and independently testable — this is the MVP.

---

## Phase 4: User Story 2 - Invitations are the only way in, and only while valid (Priority: P1)

**Goal**: Prove the invite-only guarantee — expired, already-used, unknown, or email-mismatched invitations can never result in an organisation, workspace, or account.

**Independent Test**: Attempt registration with an expired invitation, an already-used invitation, a nonexistent invitation, and a valid-but-wrong-email invitation; verify all four are refused with zero side effects.

### Tests for User Story 2 ⚠️

- [X] T039 [P] [US2] Integration test: registration with an expired invitation returns `404`/`errors.invitation.not_found`, zero side effects (spec.md US2 scenario 1, FR-003, SC-003, research.md R5) in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs`
- [X] T040 [P] [US2] Integration test: registration with an already-used invitation returns `404`/`errors.invitation.not_found` — same response as a nonexistent token, by design (spec.md US2 scenario 2, FR-004, research.md R5) — in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs`
- [X] T041 [P] [US2] Integration test: registration with an unknown/invalid invitation token returns `404`/`errors.invitation.not_found` (spec.md US2 scenario 3, FR-005) in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs`
- [X] T042 [P] [US2] Integration test: registration with an email that doesn't match the invitation's target email returns `422`/`errors.registration.email_mismatch` — distinct from the 404 family since the caller holds a valid token here (FR-018, SC-007, research.md R5) — in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs`
- [X] T043 [P] [US2] Integration test: `POST /api/admin/invitations` rejects a missing or incorrect `X-Superadmin-Key` (FR-002, FR-017) in `backend/ChildCare.Api.Tests/AdminInvitationTests.cs`

### Implementation for User Story 2

- [X] T044 [US2] Add the "already used" rejection branch to `RegisterOrganisationCommandHandler`: when the atomic `Tenant` insert conflicts and the existing row's `ProvisioningStatus = 'ready'`, return the same generic `errors.invitation.not_found` (404) as any other unresolvable invitation (FR-004, research.md R5, R10) in `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandHandler.cs` — depends on T033
- [X] T045 [US2] Add the email-lock check (case-insensitive exact match against `Invitation.Email`, FR-018) to `RegisterOrganisationCommandValidator`/handler in `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandValidator.cs` — depends on T032
- [X] T046 [US2] Map rejection outcomes to their finalized status codes and i18n error-key envelopes — `404`/`errors.invitation.not_found` for not-found/expired/already-used, `422`/`errors.registration.email_mismatch` for a resolvable-but-wrong-email token (constitution Principle IV; research.md R5) — in `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs` — depends on T037, T044, T045

**Checkpoint**: User Stories 1 and 2 both work independently — the invite-only guarantee is proven, not just assumed.

---

## Phase 5: User Story 3 - Registration survives failures and races without corrupting state (Priority: P2)

**Goal**: Prove that a partial provisioning failure is recoverable via retry, and that concurrent registration attempts on the same invitation never produce more than one organisation.

**Independent Test**: Simulate a failure partway through provisioning and confirm the same invitation can be retried to completion; fire two concurrent registration requests on the same invitation and confirm exactly one organisation results.

### Tests for User Story 3 ⚠️

- [X] T047 [P] [US3] Integration test: a partial provisioning failure leaves `Tenant.ProvisioningStatus` in `provisioning`/`failed`, and a retry with the same invitation token completes successfully without a duplicate `Tenant` row (spec.md US3 scenario 1, FR-014) in `backend/ChildCare.Api.Tests/OrganisationOnboardingResilienceTests.cs`
- [X] T048 [P] [US3] Integration test: two concurrent registration requests with the same invitation token (e.g., via `Task.WhenAll`) result in exactly one `ready` `Tenant` row (spec.md US3 scenario 2, FR-015, SC-005) in `backend/ChildCare.Api.Tests/OrganisationOnboardingResilienceTests.cs`

### Implementation for User Story 3

- [X] T049 [US3] Add a test-only injectable failure seam in `TenantProvisioningService` (invoked between schema creation and migration) so T047 can deterministically simulate a mid-provisioning failure without adding any production-facing failure surface, in `backend/ChildCare.Infrastructure/Persistence/TenantProvisioningService.cs` — depends on T017
- [X] T050 [US3] Add the "resume" branch to `RegisterOrganisationCommandHandler`: when the atomic `Tenant` insert conflicts and the existing row's `ProvisioningStatus` is `provisioning` or `failed`, resume provisioning against that existing row instead of creating a new one (FR-014) in `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandHandler.cs` — depends on T044
- [X] T051 [US3] Make `TenantProvisioningService`'s schema-creation and director-user-insert steps idempotent (`CREATE SCHEMA IF NOT EXISTS`; upsert-by-email for the director row) so a resumed attempt is safe to re-run in `backend/ChildCare.Infrastructure/Persistence/TenantProvisioningService.cs` — depends on T017

**Checkpoint**: All three user stories are independently functional; the foundation is provably resilient, not just apparently so.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T052 [P] Add `google_secret_manager_secret` + `google_secret_manager_secret_version` + IAM binding + a Cloud Run `secret_key_ref` for `SUPERADMIN_API_KEY` in `infra/gcp/main.tf` (research.md R11)
- [X] T053 [P] Add a `SuperAdmin:ApiKey` placeholder entry to `backend/ChildCare.Api/appsettings.Development.example.json`, matching the existing convention for other secrets
- [X] T054 [P] Document every `errorKey` this feature introduces (`errors.invitation.not_found`, `errors.registration.email_mismatch`, `errors.unexpected`, plus any from create-invitation.md) in `backend/ERROR_KEYS.md`, with a one-line trigger condition for each — a backend-owned reference for whichever frontend later renders these keys (constitution Principle IV), not a translation task itself; no frontend project exists yet in this repo for actual NL/FR/EN copy
- [X] T055 Run quickstart.md's full scenario walkthrough manually against local Docker Postgres and confirm every item in its "Expected outcomes checklist" passes
- [X] T056 [P] Update `BACKLOG.md`: mark row 001's status as done, noting any scope deltas between what was planned and what actually shipped

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. **Blocks all user stories** — in particular, every story's independent test relies on invitation creation (T021–T027) already working.
- **User Stories (Phase 3–5)**: All depend on Foundational completion.
  - US1 and US2 are both P1 and share the same handler/endpoint files (`RegisterOrganisationCommandHandler.cs`, `OrganisationEndpoints.cs`) — they are **not** parallelizable against each other despite both being priority 1; implement US1 first (it establishes the handler), then US2 (which adds explicit rejection branches to that same handler).
  - US3 depends on US2's "already used" branch (T044) existing, since its "resume" branch (T050) is the sibling case in the same conditional.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational. No dependency on US2/US3.
- **User Story 2 (P1)**: Can start after Foundational, but shares files with US1 (see above) — implement sequentially after US1, not in parallel with it.
- **User Story 3 (P2)**: Can start after Foundational, but its handler changes (T050) build directly on US2's T044 — implement after US2.

### Parallel Opportunities

- T002–T005 (new project scaffolding) can all run in parallel — different files.
- T008–T011 (Domain entities/enums) can all run in parallel — different files, no cross-dependencies.
- T018, T019 (pipeline behaviour, port interface) can run in parallel with each other and with T008–T011.
- T021, T023 (invitation Contracts records, validator) can run in parallel; T022 depends on T021.
- Within each story's test block, all `[P]`-marked tests can run in parallel (different assertions, same or sibling test files, no shared mutable state given TestContainers gives each test run an isolated database).
- T030 and T034 (contracts record, JwtService overload) can run in parallel — different files.
- T052–T054, T056 (Polish) can all run in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests can be written in parallel:
Task: "Integration test: full happy-path registration in backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs"
Task: "Integration test: registration succeeds with no regulatory fields present in backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs"

# Independent implementation pieces can start in parallel:
Task: "Create RegisterOrganisationRequest/Response records in backend/ChildCare.Contracts/"
Task: "Add JwtService.GenerateAccessToken overload in backend/ChildCare.Api/Services/JwtService.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational — this alone delivers a working "operator issues an invitation" capability, but no story is demoable yet.
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: run quickstart.md Scenario 1 end-to-end.
5. This is the MVP: a director can be invited and register into a working, isolated organisation.

### Incremental Delivery

1. Setup + Foundational → invitation issuance works, nothing else yet.
2. + User Story 1 → MVP: happy-path onboarding works end-to-end.
3. + User Story 2 → the invite-only guarantee is proven under negative-path testing, not just assumed by the happy path.
4. + User Story 3 → resilience under failure/concurrency is proven, not just assumed.
5. + Polish → production secret wiring (Terraform), i18n key documentation, docs.

Note: unlike a typical spec-kit feature, US1 and US2 here are **not** independently parallelizable by separate developers — they share the same handler and endpoint files by design (the rejection logic in US2 is inherent to a correct registration handler, not an optional add-on). Treat US1 → US2 → US3 as a sequential chain for this feature, not a fan-out.

---

## Notes

- `[P]` tasks touch different files with no unmet dependencies.
- `[Story]` labels map every user-story-phase task back to spec.md for traceability.
- Tests are included per constitution Principle V (NON-NEGOTIABLE) but scoped to the happy path + key negative/regulatory flows (global CLAUDE.md convention) — not exhaustive coverage of every helper method.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently before continuing.
- Status codes for invitation rejection are finalized (research.md R5, post-`/speckit-analyze` finding F2): `404`/`errors.invitation.not_found` uniformly for not-found, expired, and already-used invitations (deliberately indistinguishable, to prevent token enumeration); `422`/`errors.registration.email_mismatch` for a resolvable token used with the wrong email.
- The `/api` prefix on new endpoints (`/api/admin/invitations`, `/api/organisations/register`) is confirmed, not just a plan-time default (research.md R3, finding F3).
- Two Constitution Check items in plan.md, previously "Partial, justified," are now backed by codified constitution amendments (v1.1.0) rather than ad hoc plan-level reasoning — see plan.md's Constitution Check table.

---

## Phase 7: Convergence

**Purpose**: `/speckit-converge` assessed the implemented codebase against spec.md/plan.md/tasks.md/constitution.md after all 56 tasks above were completed. No constitution violations and no unmet functional requirements were found — the three items below are documentation-freshness and one missing edge-case test.

- [X] T057 [P] Update plan.md's Project Structure tree to list the 7 implementation-time files added per research.md R15–R17 (`ChildCare.Application/Common/IPublicDbContext.cs`, `ChildCare.Application/Common/ITenantProvisioningService.cs`, `ChildCare.Application/Organisations/RegisterOrganisationResult.cs`, `ChildCare.Application/Organisations/OrganisationSlugGenerator.cs`, `ChildCare.Application/Invitations/InvitationTokenCodec.cs`, `ChildCare.Infrastructure/Persistence/PublicDbContextFactory.cs`, `ChildCare.Infrastructure/Persistence/TenantDbContextFactory.cs`) per plan.md: Project Structure (partial)
- [X] T058 [P] Add the missing `errors.validation` key (the generic field-validation-failure wrapper key returned alongside `fieldErrors` by both FluentValidation failures and the email-mismatch response) to `backend/ERROR_KEYS.md` per T054 / Constitution Principle IV (partial)
- [X] T059 [P] Add an integration test proving that registering a second organisation with an email address already associated with an existing director account (same or different organisation) succeeds rather than being blocked, in `backend/ChildCare.Api.Tests/OrganisationOnboardingTests.cs` per spec.md Edge Cases / Assumptions (missing)
