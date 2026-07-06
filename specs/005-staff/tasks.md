---

description: "Task list for feature implementation"
---

# Tasks: Staff Management

**Input**: Design documents from `/specs/005-staff/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL. Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 create profile + invitation + login, US2 P2 multi-location eligibility, US3 P2 update profile + photo, US4 P3 deactivate/reactivate). `StaffProfile`/`StaffInvitation`/`StaffLocationEligibility`, their EF configuration/migration, the new `IProfilePhotoStorage`/`IStaffDeactivationGuard` ports, the shared result/response shapes, and the endpoint group's policy wiring are shared prerequisites every story depends on, so they live in Foundational — the story phases add story-specific commands/queries and their tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

Backend-only feature (no frontend/mobile changes). All paths are under `backend/`, except one Terraform task under `infra/gcp/`, per plan.md's Project Structure.

---

## Phase 1: Setup

**Purpose**: This is the first feature needing real GCP Cloud Storage integration — add the client library and bucket infrastructure before any application code depends on them.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds on this branch before starting
- [X] T002 [P] Add `Google.Cloud.Storage.V1` package reference to `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj`
- [X] T003 [P] Add a `google_storage_bucket` resource (profile photos, uniform bucket-level access, no public access) and a service-account IAM binding permitting `roles/iam.serviceAccountTokenCreator` (required for V4 URL signing without a downloaded key file) to `infra/gcp/main.tf`, plus a bucket-name output in `infra/gcp/outputs.tf` — matches the existing Terraform conventions already used for Cloud Run/Artifact Registry in this file

**Checkpoint**: Baseline confirmed; GCS dependency and bucket declared.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `StaffProfile`/`StaffInvitation`/`StaffLocationEligibility` entities, their EF Core configuration and migration, the `IProfilePhotoStorage`/`IStaffDeactivationGuard` ports, the shared result/response shapes, invitation email support, and the endpoint group. Every user story's commands, queries, and tests depend on this being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Entities

- [X] T004 [P] Create `QualificationLevel` enum (`QualifiedCaregiver`, `Auxiliary`, `StudentVolunteer`) in `backend/ChildCare.Domain/Enums/QualificationLevel.cs` (data-model.md)
- [X] T005 Create `StaffProfile` entity in `backend/ChildCare.Domain/Entities/StaffProfile.cs`: `Id`, `TenantUserId`, `FirstName`, `LastName`, `Phone`, `QualificationLevel?`, `ProfilePhotoObjectPath?`, `DeactivatedAt?`, `CreatedAt`, `UpdatedAt` — no `OrganisationId`/tenant column (data-model.md, research.md R1) — depends on T004
- [X] T006 [P] Create `StaffInvitation` entity in `backend/ChildCare.Domain/Entities/StaffInvitation.cs`: `Id`, `StaffProfileId`, `Email`, `TokenHash`, `ExpiresAt`, `CreatedAt` — no `UsedAt` column (data-model.md, research.md R2) — depends on T005
- [X] T007 [P] Create `StaffLocationEligibility` join entity in `backend/ChildCare.Domain/Entities/StaffLocationEligibility.cs`: `StaffProfileId`, `LocationId`, `CreatedAt` — depends on T005
- [X] T008 Add `DbSet<StaffProfile> StaffProfiles`, `DbSet<StaffInvitation> StaffInvitations`, `DbSet<StaffLocationEligibility> StaffLocationEligibility` to `ITenantDbContext` in `backend/ChildCare.Application/Common/ITenantDbContext.cs` — depends on T005, T006, T007
- [X] T009 Add the three `DbSet`s and their `OnModelCreating` configuration to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`: unique index on `StaffProfile.TenantUserId`, FK to `TenantUser`; index on `StaffProfile.DeactivatedAt`; `StaffInvitation` FK to `StaffProfile` + index on `Email`; `StaffLocationEligibility` composite PK `(StaffProfileId, LocationId)` + FKs to `StaffProfile`/`Location` (data-model.md) — depends on T008
- [X] T010 Generate the `TenantDbContext` migration (`dotnet ef migrations add AddStaff --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` — depends on T009

### Shared Application/Contracts shapes

- [X] T011 [P] Create `StaffResult` shared success/failure result type in `backend/ChildCare.Application/Staff/StaffResult.cs`, mirroring `LocationResult` (feature 004) — failure cases: `NotFound`, `EmailAlreadyExists`, `TenantUserNotFound`, `HasActiveDependents`, `InvitationInvalidOrExpired`, `AccountAlreadyActive` (FR-006a, resend-invitation)
- [X] T012 [P] Create `StaffResponse` in `backend/ChildCare.Contracts/Responses/StaffResponse.cs` (data-model.md: `Id`, `TenantUserId`, `FirstName`, `LastName`, `Email`, `Phone`, `Role`, `QualificationLevel?`, `PhotoDownloadUrl?`, `EligibleLocationIds`, `DeactivatedAt`, `CreatedAt`, `UpdatedAt`)
- [X] T013 [P] Create `RequestPhotoUploadUrlResponse` (`UploadUrl`, `ObjectPath`) in `backend/ChildCare.Contracts/Responses/StaffResponse.cs`

### New ports

- [X] T014 [P] Create `IStaffDeactivationGuard` in `backend/ChildCare.Application/Common/IStaffDeactivationGuard.cs`: `Task<bool> HasActiveDependentsAsync(Guid staffProfileId, ITenantDbContext db, CancellationToken ct)` (research.md R4, data-model.md) — no implementation registered by this feature
- [X] T015 [P] Create `IProfilePhotoStorage` in `backend/ChildCare.Application/Common/IProfilePhotoStorage.cs`: `CreateUploadUrlAsync(Guid staffProfileId, ...)` returning `(ObjectPath, UploadUrl)`, `CreateDownloadUrlAsync(string? objectPath, ...)` returning `string?` (data-model.md, research.md R3)
- [X] T016 Create `GcsProfilePhotoStorage` implementing `IProfilePhotoStorage` in `backend/ChildCare.Infrastructure/Storage/GcsProfilePhotoStorage.cs` using `Google.Cloud.Storage.V1`'s V4 `UrlSigner`, bucket name and service-account credential path read from configuration (never hardcoded — constitution Principle VI) — depends on T015, T002, T003
- [X] T017 Extend `IEmailSender` with `Task SendStaffInvitationAsync(string toEmail, string inviteLink)` in `backend/ChildCare.Application/Common/IEmailSender.cs`
- [X] T018 Implement `SendStaffInvitationAsync` in `backend/ChildCare.Api/Services/EmailService.cs`, following the existing `SendEmailVerificationAsync`/`SendPasswordResetAsync` raw-HTML-string-literal pattern (feature 001/003; the templating refactor is feature 019's job, not this feature's) — depends on T017

### Endpoint group wiring

- [X] T019 Create `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs` with `MapStaffEndpoints(this WebApplication app)`: `app.MapGroup("/api/staff").WithTags("Staff").RequireAuthorization("DirectorOnly")` for all routes except `POST /api/staff/accept-invitation`, which is mapped as a separate anonymous route on the same group prefix with `.RequireTenantExempt()` (contracts/staff-api.md — found during implementation: an unauthenticated route has no JWT for `TenantMiddleware` to resolve a tenant from, exactly like `ResetPasswordCommandHandler`/`VerifyEmailCommandHandler`, feature 003) — no `.RequireTenantExempt()` on the `DirectorOnly` routes (`TenantMiddleware` must run for those) — empty route bodies for now, filled in per story below — depends on T005
- [X] T020 Register `app.MapStaffEndpoints();` and `services.AddScoped<IProfilePhotoStorage, GcsProfilePhotoStorage>();` in `backend/ChildCare.Api/Program.cs` alongside the existing `app.MapLocationEndpoints();` call — no `IStaffDeactivationGuard` registration (by design, research.md R4) — depends on T019, T016
- [X] T021 [P] Add `errors.staff.not_found` (404), `errors.staff.email_already_exists` (409), `errors.staff.tenant_user_not_found` (404), `errors.staff.has_active_dependents` (409), `errors.staff.invitation_invalid_or_expired` (400), `errors.staff.account_already_active` (409, FR-006a), `errors.staff.firstname_required`, `errors.staff.lastname_required`, `errors.staff.email_required`, `errors.staff.email_invalid`, `errors.staff.phone_required`, `errors.staff.phone_invalid`, `errors.staff.phone_too_long`, `errors.staff.firstname_too_long`, `errors.staff.lastname_too_long`, `errors.staff.email_too_long`, `errors.staff.qualification_required` (all 422) entries to `backend/ERROR_KEYS.md` under a new "Staff Management (feature `005-staff`)" section (contracts/staff-api.md)

**Checkpoint**: `StaffProfile`/`StaffInvitation`/`StaffLocationEligibility` exist end-to-end (entities → EF config → migration), both new ports are defined (with a real GCS-backed implementation registered), the endpoint group is registered and policy-protected, and every command/query has the shared result/response shapes to return. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director Creates a Staff Profile (Priority: P1) 🎯 MVP

**Goal**: A director creates a new staff profile (or attaches one to their own existing Director account); the invitee sets a password via emailed invitation and logs in.

**Independent Test**: Submit a new staff profile with a qualification level, confirm it appears in the staff list, accept the invitation, and log in with the new password (quickstart.md Scenarios 1–2).

### Tests for User Story 1

- [X] T022 [P] [US1] Integration test: `POST /api/staff` with `role: "Staff"` and a valid `qualificationLevel` → `201`, profile appears in `GET /api/staff`; an invitation is created (assert via a test seam exposing the raw token, mirroring feature 001's invitation tests) — in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` (FR-001, FR-005, FR-006, quickstart.md Scenario 1) — depends on T020
- [X] T023 [P] [US1] Integration test: `POST /api/staff` with `role: "Staff"` omitting `qualificationLevel` → `422 errors.validation`, `fieldErrors.qualificationLevel` populated (FR-003) — same file — depends on T020, T021
- [X] T024 [P] [US1] Integration test: accept a valid invitation token with a new password → `200`; subsequent `POST /api/auth/login` (feature 003) with the organisation slug, the staff member's email, and the new password → `200`, JWT has `Role: Staff` claim — same file (FR-006, FR-007, quickstart.md Scenario 1 step 5) — depends on T020
- [X] T025 [P] [US1] Integration test: accept an expired or unknown invitation token → `400 errors.staff.invitation_invalid_or_expired` — same file (FR-006) — depends on T020, T021
- [X] T026 [P] [US1] Integration test: `POST /api/staff` with `existingTenantUserId` set to the calling director's own `TenantUserId` and a `qualificationLevel` → `201`, no invitation created, no second `TenantUser` row exists, `GET /api/staff` now includes the director — same file (research.md R6, quickstart.md Scenario 2) — depends on T020
- [X] T027 [P] [US1] Integration test: `POST /api/staff` twice with the same email → the second call → `409 errors.staff.email_already_exists` — same file (FR-008) — depends on T020
- [X] T028 [P] [US1] Integration test: a staff profile created in Org A is invisible to Org B — `GET /api/staff/{orgAStaffId}` as Org B's director → `404 errors.staff.not_found`; `GET /api/staff` as Org B never includes it — same file (constitution Principle I, FR-015, quickstart.md Scenario 6) — depends on T020
- [X] T029 [P] [US1] Integration test: a Staff-role and a Parent-role access token (seeded the same way `AuthRolePolicyTests.cs` seeds non-Director roles, feature 003) each receive `403` against `GET /api/staff`, `GET /api/staff/{id}`, and `POST /api/staff` — same file (constitution Principle II's authorization posture) — depends on T020

### Implementation for User Story 1

- [X] T030 [P] [US1] Create `CreateStaffProfileRequest` and `AcceptStaffInvitationRequest` (with `OrganisationSlug` — data-model.md, found during implementation) in `backend/ChildCare.Contracts/Requests/StaffRequests.cs`
- [X] T031 [P] [US1] Create `CreateStaffProfileCommand` + `CreateStaffProfileCommandValidator` in `backend/ChildCare.Application/Staff/CreateStaffProfileCommand.cs` / `CreateStaffProfileCommandValidator.cs`: required `FirstName`/`LastName` (`MaximumLength(100)`), `Email` (required, `EmailAddress()`, `MaximumLength(254)`), `Phone` (required, `Matches(CreateLocationCommandValidator.PhonePattern)`, `MaximumLength(30)` — reuse feature 004's pattern constant rather than duplicating it), each with `.Cascade(CascadeMode.Stop)` per feature 004's fixed cascade-bug precedent (research.md); `QualificationLevel` required only when `Role == Staff` (an async rule resolving the target role — either the request's own `Role`, or the `Role` of the account referenced by `ExistingTenantUserId` — FR-003, research.md R7, FR-001's field-length enumeration) — every rule's `.WithMessage(...)` MUST use the corresponding `errors.staff.*` key from T021, never a raw English string (constitution Principle IV) — depends on T030
- [X] T032 [US1] Create `CreateStaffProfileCommandHandler` in `backend/ChildCare.Application/Staff/CreateStaffProfileCommandHandler.cs` implementing both paths in one transaction (research.md R5, R6): when `ExistingTenantUserId` is null, create a new `TenantUser` (`Role = Staff`, empty `PasswordHash`) + `StaffProfile` + `StaffInvitation`, build the invite link with the current tenant's slug as a query param (mirrors `AuthLinkBuilder`'s `?token=...&org=...` shape, feature 003 — needed so the anonymous accept-invitation request in T034 can resolve its tenant), then call `IEmailSender.SendStaffInvitationAsync` — wrap `SaveChangesAsync` in a try/catch for a unique-constraint `DbUpdateException` on `Users.Email` and map it to `StaffResult.EmailAlreadyExists`, mirroring `RegisterOrganisationCommandHandler.IsUniqueConstraintViolation` (FR-008, Edge Cases — two directors inviting the same email concurrently), and wrap the `SendStaffInvitationAsync` call itself in a try/catch that logs and swallows any send failure rather than failing the request (FR-006); when `ExistingTenantUserId` references an existing `Director`-role account, create only the linked `StaffProfile` — reject with `StaffResult.EmailAlreadyExists`/`TenantUserNotFound` as appropriate — depends on T031, T011, T017
- [X] T033 [P] [US1] Create `AcceptStaffInvitationCommand` + `AcceptStaffInvitationCommandValidator` (required `OrganisationSlug`/`Token`/`Password`, `Password` minimum 8 characters, mirroring feature 003's reset-password rule) in `backend/ChildCare.Application/Staff/AcceptStaffInvitationCommand.cs` / `Validator.cs` — depends on T030
- [X] T034 [US1] Create `AcceptStaffInvitationCommandHandler` in `backend/ChildCare.Application/Staff/AcceptStaffInvitationCommandHandler.cs`: this is an exempt-route command (no tenant context yet) — resolve the tenant via `OrganisationSlugResolver`/`ITenantDbContextResolver.ForSchema` exactly like `ResetPasswordCommandHandler` (feature 003, found during implementation), then hash the incoming token, look up a matching non-expired `StaffInvitation` in that schema, load the linked `TenantUser`, and explicitly check `TenantUser.PasswordHash` is still empty before proceeding — if it is already non-empty, fail with `StaffResult.InvitationInvalidOrExpired` (FR-006b) even though `ExpiresAt` hasn't elapsed; otherwise set `PasswordHash` — "used" is derived from `PasswordHash` no longer being empty (research.md R2), so a second accept attempt on the same token fails the same way an expired one does — depends on T033, T011
- [X] T035 [P] [US1] Create `ListStaffQuery` (with `bool IncludeDeactivated = false`) + `ListStaffQueryHandler` in `backend/ChildCare.Application/Staff/ListStaffQuery.cs`: filter `DeactivatedAt == null` unless `IncludeDeactivated`; join `StaffLocationEligibility` for `EligibleLocationIds`; call `IProfilePhotoStorage.CreateDownloadUrlAsync` per row for `PhotoDownloadUrl` (research.md R8) — depends on T011, T015
- [X] T036 [P] [US1] Create `GetStaffByIdQuery` + `GetStaffByIdQueryHandler` in `backend/ChildCare.Application/Staff/GetStaffByIdQuery.cs` — same eligible-locations/photo-URL inlining as T035 — depends on T011, T015
- [X] T037 [US1] Implement `GET /api/staff` (with `includeDeactivated` query param), `GET /api/staff/{id}`, `POST /api/staff`, and the anonymous `POST /api/staff/accept-invitation` in `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs`, mapping `StaffResult`/query results to `StaffResponse` and the `200`/`201`/`404`/`409`/`400` responses from contracts/staff-api.md — depends on T032, T034, T035, T036

**Checkpoint**: US1 fully functional and independently testable — a director can create a staff profile (new account or director opt-in), the invitee can set a password and log in, non-Director roles are rejected, and tenant isolation holds.

---

## Phase 4: User Story 2 - Director Assigns Location Eligibility (Priority: P2)

**Goal**: A director marks which locations a staff member is eligible to work at, independent of any day-by-day schedule.

**Independent Test**: Assign a staff member to two locations, confirm both appear on their profile, then unassign one and confirm only the other remains (quickstart.md Scenario 3).

### Tests for User Story 2

- [X] T038 [P] [US2] Integration test: assign a staff member to two locations via two `PUT /api/staff/{id}/locations/{locationId}` calls → both `200`, `GET /api/staff/{id}` shows both in `eligibleLocationIds` — in `backend/ChildCare.Api.Tests/StaffLocationEligibilityTests.cs` (FR-004, quickstart.md Scenario 3) — depends on T037
- [X] T039 [P] [US2] Integration test: unassign one of the two locations via `DELETE /api/staff/{id}/locations/{locationId}` → `200`, `GET /api/staff/{id}` shows only the remaining one — same file (FR-004) — depends on T037
- [X] T040 [P] [US2] Integration test: a staff profile with zero assigned locations → `GET /api/staff/{id}` shows an empty `eligibleLocationIds` array, no error — same file (edge case) — depends on T037
- [X] T041 [P] [US2] Integration test: assigning a location id that belongs to a different organisation's tenant schema → `404 errors.location.not_found` (feature 004's existing key) — same file (constitution Principle I) — depends on T037

### Implementation for User Story 2

- [X] T042 [P] [US2] Create `AssignLocationEligibilityCommand` + `AssignLocationEligibilityCommandHandler` in `backend/ChildCare.Application/Staff/AssignLocationEligibilityCommand.cs`: verify both the staff profile and the location exist in the current tenant schema, insert the `StaffLocationEligibility` row (idempotent — no duplicate if already assigned, composite PK prevents it) — depends on T011
- [X] T043 [P] [US2] Create `UnassignLocationEligibilityCommand` + `UnassignLocationEligibilityCommandHandler` in `backend/ChildCare.Application/Staff/UnassignLocationEligibilityCommand.cs`: remove the row if present (idempotent if not assigned) — depends on T011
- [X] T044 [US2] Implement `PUT /api/staff/{id}/locations/{locationId}` and `DELETE /api/staff/{id}/locations/{locationId}` in `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs` — depends on T042, T043

**Checkpoint**: US1 and US2 both work independently — location eligibility can be assigned/unassigned without touching account creation or login.

---

## Phase 5: User Story 3 - Director Updates a Staff Profile and Photo (Priority: P2)

**Goal**: A director updates a staff member's phone/qualification/photo; photos are always served via signed URLs, never public ones.

**Independent Test**: Edit an existing staff member's qualification level and phone number, upload a photo via a signed URL, and confirm the profile reflects both changes (quickstart.md Scenario 4).

### Tests for User Story 3

- [X] T045 [P] [US3] Integration test: `PUT /api/staff/{id}` changing `phone`/`qualificationLevel` → `200`, `GET /api/staff/{id}` reflects the change — in `backend/ChildCare.Api.Tests/StaffProfileUpdateTests.cs` (FR-009, quickstart.md Scenario 4) — depends on T037
- [X] T046 [P] [US3] Integration test: `POST /api/staff/{id}/photo/upload-url` → `200`, response includes `uploadUrl`/`objectPath` (`IProfilePhotoStorage` faked with an in-memory test double per constitution Principle V — no real GCS call in CI) — same file (research.md R3) — depends on T037
- [X] T047 [P] [US3] Integration test: after requesting an upload URL, `GET /api/staff/{id}` returns a non-null `photoDownloadUrl` (from the faked `IProfilePhotoStorage`) — same file (research.md R3) — depends on T046
- [X] T048 [P] [US3] Integration test: a Staff-role access token attempting `PUT /api/staff/{id}` on their own profile → `403` (director-only editing, Clarifications session 2026-07-06 Q2, FR-009) — same file — depends on T037

### Implementation for User Story 3

- [X] T049 [P] [US3] Create `UpdateStaffProfileRequest` in `backend/ChildCare.Contracts/Requests/StaffRequests.cs`; create `UpdateStaffProfileCommand` + `UpdateStaffProfileCommandValidator` in `backend/ChildCare.Application/Staff/UpdateStaffProfileCommand.cs` / `Validator.cs`, reusing the same conditional-qualification-requirement rule and the same length/format rules for `FirstName`/`LastName`/`Phone` as `CreateStaffProfileCommandValidator` (T031, FR-001) — depends on T011
- [X] T050 [US3] Create `UpdateStaffProfileCommandHandler` in `backend/ChildCare.Application/Staff/UpdateStaffProfileCommandHandler.cs`: load by id (`NotFound` if missing), overwrite `FirstName`/`LastName`/`Phone`/`QualificationLevel`, set `UpdatedAt` — depends on T049
- [X] T051 [P] [US3] Create `RequestPhotoUploadUrlCommand` + `RequestPhotoUploadUrlCommandHandler` in `backend/ChildCare.Application/Staff/RequestPhotoUploadUrlCommand.cs`: call `IProfilePhotoStorage.CreateUploadUrlAsync`, persist the returned `ObjectPath` onto `StaffProfile.ProfilePhotoObjectPath` immediately (research.md R3 — no separate "confirm upload" step) — depends on T015, T011
- [X] T052 [US3] Implement `PUT /api/staff/{id}` and `POST /api/staff/{id}/photo/upload-url` in `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs` — depends on T050, T051

**Checkpoint**: US1, US2, and US3 all work independently — profiles can be edited and photographed without touching eligibility or account lifecycle.

---

## Phase 6: User Story 4 - Director Deactivates and Reactivates a Staff Member (Priority: P3)

**Goal**: A director soft-deletes a staff member who has left (blocking login, hiding them from active rosters, preserving history) and can reactivate them later.

**Independent Test**: Deactivate a staff member with no dependents, confirm they can no longer log in and disappear from the active list, then reactivate and confirm login works again (quickstart.md Scenario 5).

### Tests for User Story 4

- [X] T053 [P] [US4] Integration test: `POST /api/staff/{id}/deactivate` on a profile with no dependents → `200`, `deactivatedAt` set; default `GET /api/staff` excludes it; `GET /api/staff?includeDeactivated=true` includes it — in `backend/ChildCare.Api.Tests/StaffDeactivationTests.cs` (FR-010, quickstart.md Scenario 5) — depends on T037
- [X] T054 [P] [US4] Integration test: after deactivation, `POST /api/auth/login` (feature 003) with the staff member's credentials fails with the existing generic invalid-credentials response (no account-enumeration leak) — same file (FR-010) — depends on T053, T059
- [X] T055 [P] [US4] Integration test: `POST /api/staff/{id}/reactivate` → `200`, `deactivatedAt` cleared; the same login now succeeds again — same file (FR-012) — depends on T053, T059
- [X] T056 [P] [US4] Integration test: deactivating/reactivating an already-deactivated/already-active profile is idempotent (`200`, no state change on the second call) — same file — depends on T037
- [X] T057 [P] [US4] Integration test: after deactivation, `GET /api/staff/{id}` (director, `includeDeactivated=true`) still returns the full profile including name — confirming the row is never hard-deleted so historical authorship (once features 008+ exist) remains attributable — same file (FR-010, SC-004) — depends on T053

### Implementation for User Story 4

- [X] T058 [US4] Create `DeactivateStaffProfileCommand` + `DeactivateStaffProfileCommandHandler` and `ReactivateStaffProfileCommand` + `ReactivateStaffProfileCommandHandler` in `backend/ChildCare.Application/Staff/DeactivateStaffProfileCommand.cs` / `ReactivateStaffProfileCommand.cs`: deactivate resolves `IEnumerable<IStaffDeactivationGuard>` from DI, fails with `StaffResult.HasActiveDependents` if any returns `true` (FR-011), else sets `DeactivatedAt = DateTime.UtcNow` (idempotent); reactivate sets `DeactivatedAt = null` (idempotent) — depends on T014, T011 — see T071 for the refresh-token-invalidation addition
- [X] T059 [US4] Modify `LoginCommandHandler` in `backend/ChildCare.Application/Auth/LoginCommandHandler.cs` (feature 003): after resolving the `TenantUser`, if `TenantUser.Role == Staff` AND a linked `StaffProfile` exists with a non-null `DeactivatedAt`, fail with the existing generic `errors.auth.invalid_credentials` result (same response as a wrong password — no enumeration of deactivated-vs-nonexistent accounts, matching feature 001's invitation non-enumeration precedent) — the `Role == Staff` condition is required so a Director's own deactivated (optional) Staff Profile never blocks their login (FR-010, Edge Cases, see T072) — depends on T058
- [X] T060 [US4] Implement `POST /api/staff/{id}/deactivate` and `POST /api/staff/{id}/reactivate` in `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs`, mapping `StaffResult.HasActiveDependents` to `409 errors.staff.has_active_dependents` — depends on T058

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all stories.

- [X] T061 [P] Run `dotnet ef migrations script --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api` and review the generated SQL for the `AddStaff` migration (constitution Principle VI — rollout to existing tenant schemas uses the existing `migrate-tenants` CLI, unchanged mechanism from feature 002; no new-tenant-schema carve-out applies here)
- [X] T062 Run the full `dotnet test backend/ChildCare.sln` suite and confirm no regressions in pre-existing tests (features 001–004) — extend `TenantMigrationRolloutTests`' revert-simulation to also drop `staff_profiles`/`staff_invitations`/`staff_location_eligibility` and `AddStaff`'s migration history row (same class of gap feature 004 already fixed for `locations`)
- [X] T063 Walk through every scenario in `quickstart.md` manually (or via the automated tests already covering them) and confirm all six pass end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1 has no dependency on other stories
  - US2 depends on US1's `CreateStaffProfileCommand`/`StaffEndpoints.cs` existing (T020–T037) since it only adds eligibility on top of an already-creatable profile — cannot start meaningfully before US1's checkpoint
  - US3 depends on Foundational only for its command/photo work, but needs a staff profile to update, so in practice follows US1
  - US4 depends on Foundational only for its own commands, but T059 (login rejection) requires `LoginCommandHandler` to exist (feature 003, already shipped) — in practice follows US1 since it needs a real staff account to deactivate
- **Polish (Phase 7)**: Depends on all four user stories being complete
- **Requirements-Quality Follow-ups (Phase 8)**: T064–T070 depend on US1 (T032, T034, T037); T071–T073 depend on US4 (T058, T059) — this phase can run immediately after both, in parallel with Phase 7

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Entity/config before commands/queries
- Commands/queries before endpoints
- Story complete before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (T004, T006, T007, T011, T012, T013, T014, T015, T021)
- Once Foundational completes, US1's tests (T022–T029) can run in parallel; US1's command/query scaffolding (T030, T031, T033, T035, T036) can run in parallel before their handlers
- US2's two commands (T042, T043) and US3's two commands (T049, T051) can each be built in parallel with each other once Foundational + US1's `StaffEndpoints.cs` skeleton exist, since they touch different files
- Different user stories can be worked on in parallel by different team members once Foundational is done, keeping in mind the practical sequencing note above (US2/US3/US4 are more meaningful once US1's create/list endpoints exist to exercise)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Integration test: create staff profile with qualification, invitation created in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: missing qualification for Staff role rejected in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: accept invitation then log in in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: expired/invalid invitation token rejected in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: director opt-in staff profile, no invitation in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: duplicate email rejected in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: tenant isolation in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"
Task: "Integration test: Staff/Parent roles get 403 in backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs"

# Launch command/query scaffolding for User Story 1 together:
Task: "Create CreateStaffProfileCommand + Validator in backend/ChildCare.Application/Staff/CreateStaffProfileCommand.cs"
Task: "Create AcceptStaffInvitationCommand + Validator in backend/ChildCare.Application/Staff/AcceptStaffInvitationCommand.cs"
Task: "Create ListStaffQuery + Handler in backend/ChildCare.Application/Staff/ListStaffQuery.cs"
Task: "Create GetStaffByIdQuery + Handler in backend/ChildCare.Application/Staff/GetStaffByIdQuery.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (quickstart.md Scenarios 1–2, 6)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Add User Story 4 → Test independently → Deploy/Demo
6. Each story adds value without breaking previous stories

---

## Phase 8: Requirements-Quality Follow-ups

**Purpose**: Per-loop policy, every finding from `/speckit-checklist` (26 items, `checklists/requirements-quality.md`) and `/speckit-analyze` (C1/G1/G2/A1) must actually be fixed, not just logged as advisory. This phase adds the tasks those fixes require, beyond what Phases 2–6 already covered by editing spec.md.

- [X] T064 [P] Create `ResendStaffInvitationCommand` + `ResendStaffInvitationCommandHandler` in `backend/ChildCare.Application/Staff/ResendStaffInvitationCommand.cs`: load the profile's linked `TenantUser` (`NotFound` if the profile doesn't exist), fail with `StaffResult.AccountAlreadyActive` if `PasswordHash` is already non-empty (covers both "already accepted" and "director opt-in, no invitation ever needed"), else supersede (set `ExpiresAt = now`) any still-valid `StaffInvitation` for the profile, create a new one via `InvitationTokenCodec.Generate()`, call `IEmailSender.SendStaffInvitationAsync` — mirrors `CreateInvitationCommandHandler`'s (feature 001) "supersede still-pending" pattern (FR-006a) — depends on T011, T017
- [X] T065 [US1] Implement `POST /api/staff/{id}/resend-invitation` in `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs` — depends on T064
- [X] T066 [P] [US1] Integration test: resend invitation → old token now rejected as invalid/expired, new token accepted → `200` on accept — in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` (FR-006a, quickstart.md Scenario 1 addendum) — depends on T065
- [X] T067 [P] [US1] Integration test: accepting an invitation twice (second attempt after a successful first accept, token not yet expired) → `400 errors.staff.invitation_invalid_or_expired` on the second attempt (FR-006b, /speckit-checklist CHK021) — in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` — depends on T034 (amended)
- [X] T068 [P] [US1] Integration test: a login attempt for a `Staff`-role account whose invitation has never been accepted (empty `PasswordHash`) fails with the standard `errors.auth.invalid_credentials` and does not 500 — in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` (Edge Cases, /speckit-analyze finding G1) — depends on T032
- [X] T069 [P] [US1] Integration test: director opt-in (`existingTenantUserId` path) with `qualificationLevel` omitted → `201` (FR-003's Director-optional path, /speckit-analyze finding G2) — in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` — depends on T032
- [X] T070 [P] [US1] Integration test: two concurrent `POST /api/staff` requests for the same new email → exactly one `201`, the other `409 errors.staff.email_already_exists` (not an unhandled exception) — in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` (Edge Cases, /speckit-checklist CHK020, mirrors `RegisterOrganisationCommandHandler`'s race handling) — depends on T032 (amended)
- [X] T071 [US4] Modify `DeactivateStaffProfileCommandHandler` (T058) to also remove all `RefreshTokens` for the linked `TenantUser` when deactivating a `Staff`-role account's profile, mirroring `ResetPasswordCommandHandler`'s session-invalidation pattern (FR-010/SC-004, /speckit-checklist CHK006) — depends on T058
- [X] T072 [P] [US4] Integration test: deactivating a Director's own attached Staff Profile does not block that director's login or admin access; they simply drop out of `GET /api/staff` — in `backend/ChildCare.Api.Tests/StaffDeactivationTests.cs` (Edge Cases, /speckit-checklist CHK016, T059's `Role == Staff` condition) — depends on T059
- [X] T073 [P] [US4] Integration test: after deactivating a `Staff`-role account, that account's existing refresh token can no longer be used to obtain a new access token (`POST /api/auth/refresh` fails) — in `backend/ChildCare.Api.Tests/StaffDeactivationTests.cs` (FR-010/SC-004) — depends on T071

**Checkpoint**: Every `/speckit-checklist` and `/speckit-analyze` finding for this feature has a corresponding fix and test, not just a note.

---

## Phase 9: Convergence

**Purpose**: Two test-coverage gaps found by `/speckit-converge` — the underlying code is already correct, but neither behavior had a test proving it.

- [X] T074 Add a `FakeEmailSender` test double (mirrors `FakeProfilePhotoStorage`) registered Singleton in `OrganisationOnboardingWebAppFactory`, with a settable `Behavior` that can throw; add an integration test in `backend/ChildCare.Api.Tests/StaffProfileCrudTests.cs` that makes `SendStaffInvitationAsync` throw and asserts `POST /api/staff` still returns `201` (FR-006, partial)
- [X] T075 Add an integration test in `backend/ChildCare.Api.Tests/StaffLocationEligibilityTests.cs`: deactivate a location a staff member is eligible for (`POST /api/locations/{id}/deactivate`), then confirm `GET /api/staff/{id}` still returns `200` with the location still present in `eligibleLocationIds` and no error (spec.md Edge Cases, partial)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No task in this feature touches web/mobile — backend only (plan.md Technical Context)
- `IProfilePhotoStorage` is faked in all tests — no test hits real GCS (constitution Principle V's InMemory-vs-TestContainers concern is about database behavior; this is an external-service seam, faked the same way Google/Apple OAuth validation already is in feature 003's tests)
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
