---

description: "Task list for feature implementation"
---

# Tasks: Authentication & Role-Based Authorization

**Input**: Design documents from `/specs/003-auth/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL. Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 login resolves the correct organisation, US2 P2 OAuth link-only + per-role method gating, US3 P3 role-based authorization policies, US4 P4 per-device session lifecycle). The `Role` field, JWT role claim, org-slug resolution helper, and the three named policies are shared prerequisites every story depends on, so they live in Foundational — the story phases add story-specific MediatR commands and their tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

Backend-only feature (no frontend/mobile changes). All paths are under `backend/`, per plan.md's Project Structure.

---

## Phase 1: Setup

**Purpose**: This feature introduces no new project — all 5 projects already exist. Nothing to scaffold beyond confirming the baseline.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds on this branch before starting (no new projects/packages needed for this feature)

**Checkpoint**: Baseline confirmed; no new project structure needed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `Role` field and JWT role claim, the org-slug resolution mechanism, the Google/Apple validation ports, and the three authorization policies. Every user story's commands and tests depend on this being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Role field

- [X] T002 [P] Create `UserRole` enum (`Director`, `Staff`, `Parent`) in `backend/ChildCare.Domain/Enums/UserRole.cs` (data-model.md, research.md R3)
- [X] T003 Add `Role` property to `TenantUser` in `backend/ChildCare.Domain/Entities/TenantUser.cs` — depends on T002
- [X] T004 Configure `Role` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`: string conversion (lowercase) + `HasMaxLength(20)` + `IsRequired()` + table-level `CHECK ("Role" IN ('director','staff','parent'))`, mirroring `PublicDbContext`'s existing `Plan`/`ProvisioningStatus` configuration (research.md R3) — depends on T003
- [X] T005 Generate the `TenantDbContext` migration (`dotnet ef migrations add AddUserRole --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`, with the new column `NOT NULL DEFAULT 'director'` so existing rows backfill (data-model.md, research.md R3) — depends on T004
- [X] T006 Update the director upsert SQL in `backend/ChildCare.Infrastructure/Persistence/TenantProvisioningService.cs` to explicitly insert `"Role"` = `'director'` (no longer relying on the migration's backfill default for *new* tenants) — depends on T005

### JWT role claim

- [X] T007 [P] Extend `IAccessTokenIssuer` in `backend/ChildCare.Application/Common/IAccessTokenIssuer.cs`: `IssueAccessToken(Guid userId, string email, Guid tenantId, string role)`, plus `string IssueRefreshToken()` and `int RefreshTokenExpiryDays` (research.md R4)
- [X] T008 Update `backend/ChildCare.Api/Services/JwtService.cs`: `GenerateAccessToken` takes a `role` parameter and adds `new Claim(ClaimTypes.Role, role)`; confirm `GenerateRefreshToken()`/`RefreshTokenExpiryDays` (already present) are exposed unchanged (research.md R4) — depends on T007
- [X] T009 Update `backend/ChildCare.Api/Services/JwtAccessTokenIssuer.cs` to implement the extended port shape (research.md R4) — depends on T007, T008
- [X] T010 Update `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandHandler.cs`'s call site to pass `"director"` as the role (compile-fix for the T007 signature change) — depends on T009

### Email / OAuth ports

- [X] T011 [P] Create `IEmailSender` port in `backend/ChildCare.Application/Common/IEmailSender.cs` (`SendEmailVerificationAsync`, `SendPasswordResetAsync` — research.md R6)
- [X] T012 Make `backend/ChildCare.Api/Services/EmailService.cs` implement `IEmailSender` directly (research.md R6) — depends on T011
- [X] T013 [P] Create `IGoogleTokenValidator` port + `GoogleIdentity` record in `backend/ChildCare.Application/Common/IGoogleTokenValidator.cs` (research.md R7)
- [X] T014 [P] Create `IAppleTokenValidator` port + `AppleIdentity` record in `backend/ChildCare.Application/Common/IAppleTokenValidator.cs` (research.md R7)
- [X] T015 Implement `GoogleTokenValidator` in `backend/ChildCare.Infrastructure/Auth/GoogleTokenValidator.cs`, moving the tokeninfo-HTTP-call logic verbatim from `AuthService.GoogleSignInAsync` (research.md R7) — depends on T013
- [X] T016 Implement `AppleTokenValidator` in `backend/ChildCare.Infrastructure/Auth/AppleTokenValidator.cs`, moving the JWKS-validation logic verbatim from `AuthService.VerifyAppleTokenAsync` (research.md R7) — depends on T014

### Authorization policies

- [X] T017 [P] Add `"DirectorOnly"` (`RequireRole("director")`), `"StaffOrDirector"` (`RequireRole("staff", "director")`), `"ParentOnly"` (`RequireRole("parent")`) policies to the existing `AddAuthorization` block in `backend/ChildCare.Api/Program.cs` (research.md R5)

### Wiring

- [X] T018 Register `IEmailSender`→`EmailService`, `IGoogleTokenValidator`→`GoogleTokenValidator`, `IAppleTokenValidator`→`AppleTokenValidator` in `backend/ChildCare.Api/Program.cs` — depends on T012, T015, T016
- [X] T019 [P] Add `errors.auth.organisation_not_found` (404), `errors.auth.invalid_credentials` (401), `errors.auth.method_not_allowed_for_role` (403), `errors.auth.token_invalid_or_expired` (400) entries to `backend/ERROR_KEYS.md` (research.md R9)
- [X] T020 [P] Create `AuthResult` shared success/failure result type in `backend/ChildCare.Application/Auth/AuthResult.cs`, mirroring `RegisterOrganisationResult`'s shape (feature 001)
- [X] T021 Create an organisation-slug resolution helper (e.g. `OrganisationSlugResolver`) in `backend/ChildCare.Application/Auth/OrganisationSlugResolver.cs`: given a slug, look up `IPublicDbContext.Tenants`, return the tenant only if `ProvisioningStatus == Ready`, else a not-found result (research.md R1) — used by every exempt-route command in US1/US2/US4

**Checkpoint**: `Role` exists end-to-end (entity → EF config → migration → provisioning SQL), JWTs carry a role claim, the three policies are registered, and every exempt-route command has the ports/helpers it needs. User story implementation can now begin.

---

## Phase 3: User Story 1 - Email/Password Sign-In Resolves the Correct Organisation (Priority: P1) 🎯 MVP

**Goal**: Replace the feature-002 "default tenant" shim with real, client-supplied-slug-based tenant resolution for login.

**Independent Test**: Seed two organisations with a user sharing the same email in each; log in as each, supplying that organisation's own slug, and confirm each session resolves to its own tenant, never the other's (quickstart.md Scenario 1).

### Tests for User Story 1

- [X] T022 [P] [US1] Integration test: two organisations, shared email, logging in with each organisation's own slug authenticates against that organisation only — **and** the returned access token, when decoded, carries a `role` claim (`ClaimTypes.Role`) matching that account's `Role` (FR-011, closing the gap where the claim's existence was implemented but never asserted end-to-end) — in `backend/ChildCare.Api.Tests/AuthMultiTenantLoginTests.cs` (SC-001, quickstart.md Scenario 1) — depends on T021
- [X] T023 [P] [US1] Integration test: unknown `organisationSlug` → `404 errors.auth.organisation_not_found`, before any `TenantUser` lookup, same file — depends on T021
- [X] T024 [P] [US1] Integration test: `organisationSlug` resolves to a tenant with `ProvisioningStatus != Ready` → `404 errors.auth.organisation_not_found` (collapsed with unknown-slug, research.md R9), same file — depends on T021
- [X] T025 [P] [US1] Integration test: correct organisation, wrong password → `401 errors.auth.invalid_credentials`, response indistinguishable from an unknown email in that organisation (SC-005), same file — depends on T021

### Implementation for User Story 1

- [X] T026 [US1] Add required `OrganisationSlug` field to `LoginRequest` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs`
- [X] T027 [P] [US1] Create `LoginCommand` + `LoginCommandValidator` (required `OrganisationSlug`/`Email`/`Password`) in `backend/ChildCare.Application/Auth/LoginCommand.cs` / `LoginCommandValidator.cs`
- [X] T028 [US1] Create `LoginCommandHandler` in `backend/ChildCare.Application/Auth/LoginCommandHandler.cs`: resolve organisation (T021) → `ITenantDbContextResolver.ForSchema(...)` → find `TenantUser` by email → verify BCrypt hash → issue access+refresh tokens via `IAccessTokenIssuer` (role claim included) → persist refresh token — depends on T026, T027, T021, T009, T020
- [X] T029 [US1] Update `POST /api/auth/login` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs` to call `ISender.Send(new LoginCommand(...))` and map `AuthResult` to `200`/`404 errors.auth.organisation_not_found`/`401 errors.auth.invalid_credentials`. Preserve the existing `.RequireRateLimiting("auth-strict")` and `.RequireTenantExempt()` chain calls on this route verbatim — the rewrite touches the handler body, not these attachments (FR-002/FR-007 must not regress) — depends on T028

**Checkpoint**: US1 fully functional and independently testable — login no longer depends on the default-tenant shim.

---

## Phase 4: User Story 2 - Social Sign-In for Web Admin and Parent App (Priority: P2)

**Goal**: Google/Apple sign-in validates the provider token server-side, links to an existing account only (never auto-creates), and rejects a sign-in method not permitted for the matched account's role.

**Independent Test**: Attempt Google/Apple sign-in against an email with no matching account in the target organisation (rejected, nothing created); seed a matching unlinked account and repeat (links, succeeds); attempt Google sign-in against a Staff-role account, and Apple sign-in against a Director-role account (both rejected regardless of token validity) (quickstart.md Scenario 2 & 4).

### Tests for User Story 2

- [X] T030 [P] [US2] Integration test: Google sign-in with a valid token whose email matches no `TenantUser` in the specified organisation → `401 errors.auth.invalid_credentials`, and no new row is created, in `backend/ChildCare.Api.Tests/AuthOAuthLinkOnlyTests.cs` (FR-009, quickstart.md Scenario 2) — depends on T021, T015
- [X] T031 [P] [US2] Integration test: Google sign-in with a valid token matching an existing, unlinked `TenantUser` by email → 200, and that user's `GoogleId` is now set (link, not duplicate), same file — depends on T021, T015
- [X] T032 [P] [US2] Integration test: Apple sign-in first-time-link behavior (requires/stores the client-supplied email, matches an existing account) — same file — depends on T021, T016
- [X] T033 [P] [US2] Integration test: (a) Google sign-in against an existing account whose `Role = Staff` → `403 errors.auth.method_not_allowed_for_role`, and (b) Apple sign-in against an existing account whose `Role = Director` → the same `403`, even though both tokens are otherwise valid (FR-017 forbids Staff+Google, Staff+Apple, and Director+Apple — this test covers two of the three invalid combinations; Staff+Apple is symmetric with (a)'s Staff+Google case and is not separately required for coverage) (quickstart.md Scenario 4), same file — depends on T021, T015, T016
- [X] T034 [P] [US2] Integration test (regression): a tampered/expired Google ID token and an invalid-signature Apple identity token are both rejected without issuing any session (SC-003); **and**, separately, a Google sign-in and an Apple sign-in each targeting an organisation whose `ProvisioningStatus != Ready` are rejected with `404 errors.auth.organisation_not_found` before any token validation is attempted (FR-015 applies to OAuth, not just login), same file — depends on T015, T016, T021

### Implementation for User Story 2

- [X] T035 [US2] Add required `OrganisationSlug` field to `GoogleAuthRequest` and `AppleAuthRequest` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs`
- [X] T036 [P] [US2] Create `GoogleSignInCommand` + `GoogleSignInCommandValidator` in `backend/ChildCare.Application/Auth/GoogleSignInCommand.cs` / `GoogleSignInCommandValidator.cs`
- [X] T037 [P] [US2] Create `AppleSignInCommand` + `AppleSignInCommandValidator` in `backend/ChildCare.Application/Auth/AppleSignInCommand.cs` / `AppleSignInCommandValidator.cs`
- [X] T038 [US2] Create `GoogleSignInCommandHandler` in `backend/ChildCare.Application/Auth/GoogleSignInCommandHandler.cs`: resolve organisation (T021, reject not-`Ready` per FR-015) → validate token via `IGoogleTokenValidator` (T015) → find existing `TenantUser` by `GoogleId` then email — if none, return `invalid_credentials` (no creation, FR-009) → if found and role doesn't permit Google (FR-017: only Director/Parent) return `method_not_allowed_for_role` → link `GoogleId`/mark verified if needed → issue tokens — depends on T036, T015, T021, T009, T020
- [X] T039 [US2] Create `AppleSignInCommandHandler` in `backend/ChildCare.Application/Auth/AppleSignInCommandHandler.cs`: same shape via `IAppleTokenValidator` (T016), organisation-not-Ready rejection per FR-015, role check permits only Parent (FR-017) — depends on T037, T016, T021, T009, T020
- [X] T040 [US2] Update `POST /api/auth/google` and `POST /api/auth/apple` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs` to call `ISender.Send(...)` and map results including `403 errors.auth.method_not_allowed_for_role`. Preserve the existing `.RequireRateLimiting("auth-oauth")` and `.RequireTenantExempt()` chain calls on both routes verbatim (FR-002/FR-007) — depends on T038, T039

**Checkpoint**: US2 fully functional — OAuth sign-in is link-only and role-gated, independent of US1.

---

## Phase 5: User Story 3 - Role-Based Access Control Across the Three Products (Priority: P3)

**Goal**: Prove the three named policies (built in Foundational, T017) correctly gate access by role and fail closed.

**Independent Test**: One account per role; call a `DirectorOnly`/`StaffOrDirector`/`ParentOnly`-protected endpoint with each — confirm only the permitted role(s) succeed, others get 403, and a missing/unrecognized role claim is refused by all three (quickstart.md Scenario 3).

### Implementation for User Story 3

- [X] T041 [US3] Add three internal, test-only minimal endpoints (one per policy) guarded by `.RequireAuthorization("DirectorOnly"/"StaffOrDirector"/"ParentOnly")` — e.g. in `backend/ChildCare.Api.Tests/TestSupportEndpoints.cs`, registered only in the `Testing` environment — depends on T017

### Tests for User Story 3

- [X] T042 [P] [US3] Integration test: `DirectorOnly` allows a Director token, refuses Staff and Parent with `403`, in `backend/ChildCare.Api.Tests/AuthRolePolicyTests.cs` — depends on T041
- [X] T043 [P] [US3] Integration test: `StaffOrDirector` allows Staff and Director, refuses Parent with `403`, same file — depends on T041
- [X] T044 [P] [US3] Integration test: `ParentOnly` allows Parent only, refuses Director and Staff with `403`, same file — depends on T041
- [X] T045 [P] [US3] Integration test: a JWT with no role claim, and separately one with an unrecognized role value, are refused by all three policies (fail closed, FR-014), same file — depends on T041

**Checkpoint**: US3 fully proven — the policies established in Foundational work correctly and fail closed, independent of US1/US2.

---

## Phase 6: User Story 4 - Per-Device Session Management Continues to Work (Priority: P4)

**Goal**: Refresh, logout, account deletion, email verification, and password reset all keep working under real (slug-based or token-context-based) tenant resolution, migrated to MediatR, with i18n-compliant error responses.

**Independent Test**: Two-device login/logout/refresh-rotation regression; password reset invalidates all sessions; reset/verify links round-trip the embedded org slug correctly; forgot-password never reveals account existence (quickstart.md Scenario 5, 6).

### Tests for User Story 4

- [X] T046 [P] [US4] Integration test (regression): two devices, logging out one does not invalidate the other's refresh token, in `backend/ChildCare.Api.Tests/AuthEndpointTests.cs`
- [X] T047 [P] [US4] Integration test (regression): using a refresh token rotates it — the old one is rejected on reuse (SC-004), same file
- [X] T048 [P] [US4] Integration test (regression): a completed password reset invalidates every refresh token for that account across all devices, same file
- [X] T049 [P] [US4] Integration test: (a) `POST /api/auth/refresh` requires `OrganisationSlug` — an unknown slug is rejected before any token lookup, and a slug resolving to a not-`Ready` tenant is rejected the same way (FR-015 applies to refresh too); (b) `POST /api/auth/forgot-password` likewise requires `OrganisationSlug` and rejects an unknown/not-ready one the same way (FR-016 explicitly names forgot-password among the requests requiring a client-supplied organisation identifier), same file — depends on T021
- [X] T050 [P] [US4] Integration test: (a) the emailed reset/verification links carry `&org={slug}`, and submitting `ResetPasswordRequest`/`VerifyEmailRequest` with that slug resolves the correct tenant schema (research.md R2); (b) `POST /api/auth/forgot-password` with a registered email and with an unregistered email in the same, valid, `Ready` organisation both return `200` with response bodies that reveal no distinguishable difference (SC-005's non-enumeration guarantee, per contracts/auth-api.md), same file — depends on T021
- [X] T051 [P] [US4] Integration test: an invalid/expired reset or verification token returns `400 errors.auth.token_invalid_or_expired` (not the old hardcoded English string — Constitution Principle IV fix), same file
- [X] T052 [P] [US4] Integration test (regression): after T029/T040's endpoint rewrites, `POST /api/auth/login` still enforces the `auth-strict` rate limit (repeated rapid requests eventually receive `429`) — confirms the MediatR migration did not silently drop the existing `.RequireRateLimiting(...)` attachment (FR-002), same file — depends on T029

### Implementation for User Story 4

- [X] T053 [US4] Add required `OrganisationSlug` to `RefreshRequest` and `ForgotPasswordRequest`; add required `OrganisationSlug` to `ResetPasswordRequest` and `VerifyEmailRequest` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs`
- [X] T054 [P] [US4] Create `RefreshTokenCommand` + validator + handler in `backend/ChildCare.Application/Auth/RefreshTokenCommand.cs` (resolve organisation via T021, reject not-`Ready` per FR-015, rotate token) — depends on T021, T009, T020, T053
- [X] T055 [P] [US4] Create `LogoutCommand` + validator + handler in `backend/ChildCare.Application/Auth/LogoutCommand.cs` (non-exempt route — depends on the DI-scoped `ITenantDbContext`, unchanged resolution path from feature 002)
- [X] T056 [P] [US4] Create `DeleteAccountCommand` + validator + handler in `backend/ChildCare.Application/Auth/DeleteAccountCommand.cs` (non-exempt, same pattern as T055)
- [X] T057 [P] [US4] Create `ResendVerificationCommand` + validator + handler in `backend/ChildCare.Application/Auth/ResendVerificationCommand.cs` (non-exempt, uses `IEmailSender`) — depends on T012
- [X] T058 [P] [US4] Create `VerifyEmailCommand` + validator + handler in `backend/ChildCare.Application/Auth/VerifyEmailCommand.cs` (exempt, org-slug from request per T053; returns `errors.auth.token_invalid_or_expired` instead of the current hardcoded string) — depends on T021, T053
- [X] T059 [P] [US4] Create `ForgotPasswordCommand` + validator + handler in `backend/ChildCare.Application/Auth/ForgotPasswordCommand.cs` (exempt, org-slug required per T053, rejects unknown/not-ready the same as login per FR-015/FR-016; always returns success regardless of whether the email matches an account, per SC-005; builds the reset link with `&org={slug}` per research.md R2, sends via `IEmailSender`) — depends on T021, T012, T053
- [X] T060 [P] [US4] Create `ResetPasswordCommand` + validator + handler in `backend/ChildCare.Application/Auth/ResetPasswordCommand.cs` (exempt, org-slug from request per T053; invalidates all refresh tokens on success; returns `errors.auth.token_invalid_or_expired` instead of the current hardcoded string) — depends on T021, T053
- [X] T061 [US4] Update `POST /api/auth/refresh`, `POST /api/auth/logout`, `DELETE /api/auth/account`, `POST /api/auth/resend-verification`, `POST /api/auth/verify-email`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password` in `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs` to call `ISender.Send(...)`. Preserve the existing `.RequireRateLimiting("auth-refresh"/"auth-strict")` and `.RequireTenantExempt()`/`.RequireAuthorization()` chain calls on each route verbatim (FR-002/FR-007) — depends on T054, T055, T056, T057, T058, T059, T060

**Checkpoint**: All four user stories are independently proven; every `AuthEndpoints.cs` route (except the now-deleted `/register`) is a thin MediatR dispatch.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Remove what's now fully superseded, and validate the whole feature end-to-end.

- [X] T062 Rewrite `backend/ChildCare.Api.Tests/AuthEndpointTests.cs` to stop seeding via `/api/auth/register` (deleted next, T063): seed a director via `OrganisationOnboardingWebAppFactory`'s existing onboarding flow, plus directly-inserted `TenantUser` rows (with explicit `Role`) for Staff/Parent scenarios — depends on T029, T040, T061
- [X] T063 Delete `POST /api/auth/register`, `AuthService.RegisterAsync`, and the `RegisterRequest` DTO from `backend/ChildCare.Api/Endpoints/AuthEndpoints.cs` (research.md R10) — depends on T062
- [X] T064 Delete `backend/ChildCare.Api/Services/AuthService.cs` entirely, and the feature-002 `ResolveDefaultTenantAsync` shim within it, now that every method has a MediatR-command replacement — depends on T029, T040, T061, T063
- [X] T065 [P] Run quickstart.md's 6 scenarios end-to-end against local Docker Postgres
- [X] T066 Full solution build + full test suite pass (`dotnet build backend/ChildCare.sln`, `dotnet test backend/ChildCare.Api.Tests`) confirming no regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (Role field, JWT role claim, org-slug resolver, and OAuth-validator ports are all needed by every story's commands).
- **User Stories (Phase 3-6)**: All depend on Foundational completion. US1 (login), US2 (OAuth), and US3 (policy validation, uses only Foundational's T017) are mutually independent and can proceed in parallel. US4 (refresh/logout/reset/verify) is also independent of US1-US3's own commands, though it shares the same `AuthEndpoints.cs` file, so its endpoint-wiring task (T061) should not run concurrently with T029/T040's edits to the same file. US4's T052 (rate-limit regression) additionally depends on T029 (US1) having landed.
- **Polish (Phase 7)**: Depends on all four user stories being complete (T064's `AuthService.cs` deletion requires every one of its methods to already have a replacement).

### Within Each User Story

- Tests before implementation is not strictly enforced here (commands and their tests are listed test-first per convention, but the command/handler/validator trio within a story has internal dependencies as annotated).
- US3 is the one exception to "tests are listed before implementation": T041 (the test-only policy-guarded endpoints) is test *scaffolding*, not product code under test, so it is sequenced — and numbered — before the tests that depend on it (T042-T045), unlike US1/US2/US4 where the tests precede the real product-code commands they exercise.
- Each story's endpoint-wiring task (T029, T040, T061) is the last task in that story, since it depends on that story's command handler(s) existing.

### Parallel Opportunities

- Foundational: T002, T007, T011, T013, T014, T017, T019, T020 have no inter-dependencies and can start together; their respective downstream chains (T003→T006, T008→T010, T012, T015, T016, T018) follow.
- Once Foundational completes: US1, US2, and US3 can proceed fully in parallel (different files). US4's command/validator/handler creation (T054-T060) can also proceed in parallel with US1/US2/US3, but its `AuthEndpoints.cs` edit (T061) should be sequenced with T029/T040 to avoid merge conflicts in the same file, and its rate-limit regression test (T052) needs T029 merged first.

---

## Parallel Example: Foundational

```bash
# Launch in parallel (different files, no dependencies on each other):
Task: "Create UserRole enum in backend/ChildCare.Domain/Enums/UserRole.cs"
Task: "Extend IAccessTokenIssuer in backend/ChildCare.Application/Common/IAccessTokenIssuer.cs"
Task: "Create IEmailSender port in backend/ChildCare.Application/Common/IEmailSender.cs"
Task: "Create IGoogleTokenValidator port in backend/ChildCare.Application/Common/IGoogleTokenValidator.cs"
Task: "Create IAppleTokenValidator port in backend/ChildCare.Application/Common/IAppleTokenValidator.cs"
```

## Parallel Example: User Stories (post-Foundational)

```bash
# Launch together once Phase 2 is complete:
Task: "LoginCommand + tests in AuthMultiTenantLoginTests.cs" (US1)
Task: "GoogleSignInCommand/AppleSignInCommand + tests in AuthOAuthLinkOnlyTests.cs" (US2)
Task: "Test-only policy endpoints + tests in AuthRolePolicyTests.cs" (US3)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (trivial).
2. Complete Phase 2: Foundational (CRITICAL — Role, JWT claim, policies, ports).
3. Complete Phase 3: User Story 1 (login resolves the correct organisation) — this alone fixes the riskiest gap (the shared default-tenant shim).
4. **STOP and VALIDATE**: run quickstart.md Scenario 1.
5. Add US2, US3, US4 incrementally.

### Incremental Delivery

1. Foundational → Role/claim/policies/ports exist.
2. US1 → real login resolution proven (MVP).
3. US2 → OAuth link-only + role gating proven.
4. US3 → policies proven reusable for future features.
5. US4 → session lifecycle regression-proven, MediatR migration complete for those flows.
6. Polish → `/register` and `AuthService.cs` deleted, full validation, full test suite.

### Parallel Team Strategy

With multiple developers, once Foundational is done: Developer A takes US1, Developer B takes US2, Developer C takes US3; US4 can be split further since its five commands are independent of each other. All converge on Phase 7 only once their respective `AuthEndpoints.cs` edits are ready to merge.

---

## Notes

- `[P]` tasks touch different files with no unmet dependencies.
- `[Story]` labels map every user-story-phase task back to spec.md for traceability.
- Tests are included per constitution Principle V (NON-NEGOTIABLE), scoped to the happy path + key negative/regression flows (global CLAUDE.md convention) — not exhaustive coverage of every helper method.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently before continuing.
- `AuthService.cs`'s deletion (T064) is deliberately the very last implementation task, not part of Foundational — it can only be removed once every one of its ten methods has a MediatR-command replacement, and those are split across four independent stories.
- This feature also fixes two pre-existing issues discovered during `/speckit-plan`'s codebase review, not originally called out in BACKLOG.md's feature 003 prompt: (1) Google/Apple sign-in auto-creating accounts (open-registration path, fixed via T038/T039's link-only behavior) and (2) two hardcoded English error strings in the current `AuthEndpoints.cs` (Constitution Principle IV violation, fixed via T019/T058/T060's `errors.auth.token_invalid_or_expired`).
- `/speckit-analyze` (2026-07-05 first pass) found one MEDIUM task-ordering inconsistency (US3's T041/T042 were numbered in the wrong order relative to their dependency — fixed by resequencing so the test-scaffolding endpoint task comes first) and six coverage gaps — one HIGH (FR-011's role claim was implemented but never asserted end-to-end, closed via T022's expanded assertion), the rest MEDIUM (FR-015 not-ready-organisation rejection untested for refresh/OAuth, closed via T034/T049; FR-016's forgot-password org-slug requirement untested, closed via T049; FR-017's Director+Apple rejection untested, closed via T033; SC-005's forgot-password non-enumeration guarantee untested, closed via T050; FR-002/FR-007's rate-limiting/header preservation across the endpoint rewrite unverified, closed via reminder text in T029/T040/T061 plus a new regression test, T052). One LOW ambiguity (SC-001's "normal load" was unquantified) was also closed directly in spec.md rather than left as an accepted convention, and one LOW plan/tasks inconsistency (plan.md's hedged DTO file location) was resolved in plan.md.

---

## Phase 8: Convergence

**Purpose**: `/speckit-converge` (2026-07-06) assessed the implemented code against spec.md/plan.md/tasks.md and found three LOW-severity gaps — no constitution violations, no missing functional coverage, no contradictions.

- [X] T067 Add an integration test confirming Google sign-in succeeds for an existing Parent-role account (linking, 200) in `backend/ChildCare.Api.Tests/AuthOAuthLinkOnlyTests.cs` — the allowed Parent+Google combination is otherwise implemented but untested (only Director+Google, Staff+Google, and Director+Apple have direct test coverage) per FR-017 (partial)
- [X] T068 Add a concurrency test for SC-001 in `backend/ChildCare.Api.Tests/AuthMultiTenantLoginTests.cs` — 50 concurrent logins all resolving to the correct tenant. A hard per-request timing gate was attempted first and found to fail on genuine CPU saturation, not a code defect (plan.md's Implementation-Time Deviations #4): `ThreadPool.SetMinThreads` was added to `Program.cs` as a real, kept mitigation, but the timing assertion itself was dropped in favor of correctness-only, since a strict latency gate would flake across CI runners with different core counts (SC-001, partial — correctness half verified, timing half documented as deferred)
- [X] T069 Update the doc comment on `backend/ChildCare.Application/Common/ITenantDbContextResolver.cs` to describe the current exempt-route MediatR commands (LoginCommandHandler, GoogleSignInCommandHandler, etc.) instead of "AuthService's pre-auth shim", which was deleted in this feature's Polish phase (T064) per plan: research.md R1 (partial)
