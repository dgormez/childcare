---

description: "Task list for feature implementation"
---

# Tasks: Enrolment Contracts

**Input**: Design documents from `/specs/007-contracts/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL — this is especially load-bearing here since the day-overlap concurrency guarantee (FR-006) can only be validated against a real Postgres advisory lock. Scope follows the project convention (global CLAUDE.md): happy path + key negative/regulatory flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 create+activate, US2 P1 split-location day-overlap, US3 P2 amendment, US3a P2 termination, US4 P2 PDF generation). `Contract` (+ `ContractedDay`/`ContractConsent` owned types), its EF configuration/migration, the new `IContractPdfGenerator`/`IAdvisoryLockService` ports, the shared `ContractActivationChecker`, the two deactivation-guard implementations, the shared result/response/request shapes, and the endpoint group's policy wiring are shared prerequisites every story depends on, so they live in Foundational.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1/US2/US3/US3a/US4) — omitted for Setup/Foundational/cross-cutting/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

Backend-only feature (no frontend/mobile changes). All paths are under `backend/`.

---

## Phase 1: Setup

**Purpose**: Confirm baseline before adding a new tenant-domain entity and two new external-service ports.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds on this branch before starting

**Checkpoint**: Baseline confirmed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `Contract` entity (with its `ContractedDay`/`ContractConsent` owned JSONB types), its EF configuration and migration, the `IContractPdfGenerator` port (interface only — the QuestPDF adapter is built in US4), the `IAdvisoryLockService` port and its Postgres adapter, the shared `ContractActivationChecker` (used by both US1's activation and US3's amendment), the shared result/response/request shapes, the two deactivation-guard implementations, and the endpoint group. Every user story's commands, queries, and tests depend on this being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Entities

- [X] T002 [P] Create `ContractStatus` enum (`Draft`, `Active`, `Ended`) in `backend/ChildCare.Domain/Enums/ContractStatus.cs` (data-model.md)
- [X] T003 [P] Create `ContractedDay` owned value type (`Weekday` as `DayOfWeek`, `StartTime`/`EndTime` as `TimeOnly`) in `backend/ChildCare.Domain/ValueObjects/ContractedDay.cs` (data-model.md, Clarifications — each weekday has its own independent hours range)
- [X] T004 [P] Create `ContractConsent` owned value type (`PhotosInternal`, `PhotosWebsite`, `PhotosSocialMedia`, `VideoInternal`, `PhotosPress`, all `bool`) in `backend/ChildCare.Domain/ValueObjects/ContractConsent.cs` (data-model.md, FR-010)
- [X] T005 Create `Contract` entity in `backend/ChildCare.Domain/Entities/Contract.cs` per data-model.md's full field list: `Id`, `ChildId`, `LocationId`, `PreviousContractId?`, `StartDate`, `EndDate?`, `ContractedDays` (`List<ContractedDay>`), `DailyRateCents`, `Status`, `Consent`, `TariefCode?`, `RateValidUntil?`, `CreatedAt`, `UpdatedAt` — depends on T002, T003, T004
- [X] T006 Add `DbSet<Contract> Contracts` to `ITenantDbContext` in `backend/ChildCare.Application/Common/ITenantDbContext.cs` — depends on T005
- [X] T007 Add the `Contract` `DbSet` and its `OnModelCreating` configuration to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`: `OwnsMany(x => x.ContractedDays, ...).ToJson()` mapped to a `contracted_days` column, `OwnsOne(x => x.Consent, ...).ToJson()` mapped to a `consent` column, `HasOne<Child>()`/`HasOne<Location>()` FKs, self-referencing `HasOne<Contract>().WithMany().HasForeignKey(x => x.PreviousContractId).OnDelete(DeleteBehavior.Restrict)`, a check constraint `"DailyRateCents" > 0`, and indexes on `(ChildId, Status)` and `(LocationId, Status)` (data-model.md) — depends on T006
- [X] T008 Generate the `TenantDbContext` migration (`dotnet ef migrations add AddContracts --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` — depends on T007

### PDF port (interface only — QuestPDF adapter is built in US4)

- [X] T009 [P] Add the `QuestPDF` (MIT) package reference to `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj` (constitution's fixed Phase 1 PDF library, first use in this codebase — research.md R4)
- [X] T010 [P] Create `IContractPdfGenerator` interface and `ContractPdfModel` record (flattened, display-ready fields: child name, location name, status, contracted days/hours, daily rate, all five consent flags) in `backend/ChildCare.Application/Common/IContractPdfGenerator.cs` (research.md R4)

### Advisory lock port

- [X] T011 [P] Create `IAdvisoryLockService` interface (`Task<T> RunExclusiveAsync<T>(Guid key, Func<Task<T>> action, CancellationToken ct)`) in `backend/ChildCare.Application/Common/IAdvisoryLockService.cs` (research.md R2)
- [X] T012 Create `PostgresAdvisoryLockService` implementing `IAdvisoryLockService` in `backend/ChildCare.Infrastructure/Concurrency/PostgresAdvisoryLockService.cs`, using `pg_advisory_lock`/`pg_advisory_unlock` on a dedicated `NpgsqlConnection` exactly like `TenantProvisioningService.RunExclusiveAsync` (feature 001, commit `4625480`) — a new, feature-scoped implementation, not a shared refactor of that existing method (research.md R2) — depends on T011

### Shared activation-check helper (used by both US1's activation and US3's amendment)

- [X] T013 Create `ContractActivationChecker` (plain class, not a MediatR handler) in `backend/ChildCare.Application/Contracts/ContractActivationChecker.cs`: given a `Contract` row already tracked by the `ITenantDbContext` (status still `Draft`) and the `db` itself, query `db.Contracts.Where(c => c.ChildId == contract.ChildId && c.Status == ContractStatus.Active)`; if any row shares `LocationId` → return `AlreadyActiveAtLocation`; else if any row shares a `Weekday` in `ContractedDays` (any location) → return `DayOverlap`; else set `contract.Status = ContractStatus.Active` and `SaveChangesAsync` — **critical correctness note**: when called from an amendment (US3), the old contract being ended MUST already have `Status = ContractStatus.Ended` set in-memory on the same tracked `ITenantDbContext` instance *before* this method runs, so EF Core's identity-map query merge returns the already-tracked (in-memory `Ended`) instance rather than the still-`Active` DB row — this is what makes the old contract correctly excluded from the conflict check without an explicit "exclude this id" parameter — depends on T005

### Shared Application/Contracts shapes

- [X] T014 [P] Create `ContractResult` shared success/failure result type in `backend/ChildCare.Application/Contracts/ContractResult.cs`, mirroring `ChildResult`/`GroupResult` — failure cases: `NotFound`, `ChildNotFound`, `LocationNotFound`, `NotDraft`, `NotActive`, `AlreadyActiveAtLocation`, `DayOverlap`, `AmendmentStartDateInvalid`, `TerminationDateInvalid`
- [X] T015 [P] Create `ContractResponse` (+ nested `ContractedDayResponse`, `ContractConsentResponse`) in `backend/ChildCare.Contracts/Responses/ContractResponse.cs`
- [X] T016 [P] Create `CreateContractRequest`, `UpdateContractRequest`, `AmendContractRequest`, `TerminateContractRequest` in `backend/ChildCare.Contracts/Requests/ContractRequests.cs`
- [X] T017 Create `ContractMapper` in `backend/ChildCare.Application/Contracts/ContractMapper.cs` mapping `Contract` → `ContractResponse` — depends on T015

### Deactivation guards (feature 004/006 extension points — research.md R3)

- [X] T018 [P] Create `ContractLocationDeactivationGuard` implementing `ILocationDeactivationGuard` in `backend/ChildCare.Application/Contracts/ContractLocationDeactivationGuard.cs`: `db.Contracts.AnyAsync(c => c.LocationId == locationId && c.Status == ContractStatus.Active)` — depends on T005
- [X] T019 [P] Create `ContractChildDeactivationGuard` implementing `IChildDeactivationGuard` in `backend/ChildCare.Application/Contracts/ContractChildDeactivationGuard.cs`: `db.Contracts.AnyAsync(c => c.ChildId == childId && c.Status == ContractStatus.Active)` — depends on T005

### Endpoint group wiring

- [X] T020 [P] Create `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs` with `MapContractsEndpoints(this WebApplication app)`: `/api/children/{childId}/contracts` and `/api/contracts` groups, both `.RequireAuthorization("DirectorOnly")` — empty route bodies for now — depends on T005
- [X] T021 Register `app.MapContractsEndpoints();` in `backend/ChildCare.Api/Program.cs` alongside the existing endpoint-mapping calls; register `services.AddScoped<IAdvisoryLockService, PostgresAdvisoryLockService>()`, `services.AddScoped<ILocationDeactivationGuard, ContractLocationDeactivationGuard>()`, `services.AddScoped<IChildDeactivationGuard, ContractChildDeactivationGuard>()` (additive — joins whatever else is already registered against `IEnumerable<...>`), and set `QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;` once at startup — depends on T012, T018, T019, T020
- [X] T022 [P] Add `errors.contract.not_found` (404), `errors.contract.not_draft` / `errors.contract.not_active` (409), `errors.contract.already_active_at_location` / `errors.contract.day_overlap` (409), `errors.contract.amendment_start_date_invalid` / `errors.contract.termination_date_invalid` (422), plus field-required/invalid validation keys (`errors.contract.weekday_required`, `errors.contract.weekday_invalid`, `errors.contract.time_range_invalid`, `errors.contract.daily_rate_invalid`, `errors.contract.start_date_required`, `errors.contract.end_date_before_start_date`) to `backend/ERROR_KEYS.md` under a new "Enrolment Contracts (feature `007-contracts`)" section — also note the reuse of `errors.child.not_found`/`errors.location.not_found` rather than inventing duplicates

**Checkpoint**: `Contract` exists end-to-end (entity → EF config → migration), both new ports (`IContractPdfGenerator`, `IAdvisoryLockService`) are defined, the shared activation-check helper exists, both deactivation guards are implemented and registered, and the endpoint group is registered and policy-protected. User story implementation can now begin.

---

## Phase 3: User Story 1 - Create and Activate an Enrolment Contract (Priority: P1) 🎯 MVP

**Goal**: A director creates a draft contract for a child at a location and activates it, subject to the one-active-contract-per-location rule.

**Independent Test**: Create a draft contract with weekdays/hours/rate/consent, activate it, confirm it becomes the child's active contract at that location; attempting to activate a second contract at the same location fails (quickstart.md Scenario 1).

### Tests for User Story 1

- [X] T023 [P] [US1] Integration test: `POST /api/children/{childId}/contracts` with minimal required fields (one weekday, `dailyRateCents`, `consent` all false) → `201`, `status: "draft"` — in `backend/ChildCare.Api.Tests/ContractLifecycleTests.cs` (FR-001, quickstart.md Scenario 1 step 1) — depends on T021
- [X] T024 [P] [US1] Integration test: create with an `endDate`, multiple weekdays each with its own start/end time, and mixed consent flags → `201`, every field round-trips on `GET /api/contracts/{id}` — same file (FR-001, Clarifications) — depends on T021
- [X] T025 [P] [US1] Integration test: `POST /api/contracts/{id}/activate` on a draft → `200`, `status: "active"` — same file (FR-003, quickstart.md Scenario 1 step 2) — depends on T021
- [X] T026 [P] [US1] Integration test: activate a second draft contract for the same child at the same location while the first is active → `409 errors.contract.already_active_at_location`, the existing active contract's status is unchanged — same file (FR-004, quickstart.md Scenario 1 step 3) — depends on T021
- [X] T027 [P] [US1] Integration test (theory over cases): create requests with an empty `contractedDays` (`errors.contract.weekday_required`), a Saturday/Sunday weekday (`errors.contract.weekday_invalid`), the same weekday listed twice (`errors.contract.weekday_invalid`, FR-001 duplicate-weekday rule), `startTime >= endTime` for a day (`errors.contract.time_range_invalid`), `dailyRateCents <= 0` (`errors.contract.daily_rate_invalid`), missing `startDate` (`errors.contract.start_date_required`), and `endDate` before `startDate` (`errors.contract.end_date_before_start_date`) → each `422 errors.validation` with the corresponding field key — same file (FR-001) — depends on T021, T022
- [X] T027a [P] [US1] Integration test: create a contract omitting the `consent` object entirely, and a second omitting only some of its fields → both `201` with every unspecified consent flag `false`, never `true` — same file (FR-010) — depends on T021
- [X] T027b [P] [US1] Integration test: `POST /api/children/{childId}/contracts` targeting a deactivated location → `404 errors.location.not_found`; `POST /api/contracts/{id}/activate` for a draft whose location was deactivated after the draft was created → same `404 errors.location.not_found` — same file (FR-004a, `/speckit-analyze` E1) — depends on T021
- [X] T028 [P] [US1] Integration test: `PUT /api/contracts/{id}` edits a draft's terms in place → `200`, updated fields reflected; the same edit attempted on an already-active contract → `409 errors.contract.not_draft` — same file (research.md R5) — depends on T021
- [X] T029 [P] [US1] Integration test: `POST /api/contracts/{id}/activate` on an already-active contract → `409 errors.contract.not_draft` — same file (FR-003) — depends on T021
- [X] T030 [P] [US1] Integration test: a contract created in Org A is invisible to Org B — `GET /api/contracts/{orgAContractId}` as Org B's director → `404 errors.contract.not_found`; `GET /api/children/{orgAChildId}/contracts` as Org B → `404 errors.child.not_found` — same file (constitution Principle I, FR-015) — depends on T021
- [X] T031 [P] [US1] Integration test: a Staff-role and a Parent-role access token each receive `403` against `POST /api/children/{id}/contracts` and `POST /api/contracts/{id}/activate` — same file (FR-014, mirrors feature 005/006's role-policy test pattern) — depends on T021

### Implementation for User Story 1

- [X] T032 [US1] Create `CreateContractCommand` + `CreateContractCommandValidator` + `CreateContractCommandHandler` in `backend/ChildCare.Application/Contracts/CreateContractCommand.cs`: verify `Child`/`Location` exist and `Location` is not deactivated (`errors.location.not_found` reused, FR-004a); validate ≥1 contracted day, no duplicate/invalid weekday (FR-001), `startTime < endTime` per day, `dailyRateCents > 0`, `startDate` required, `endDate` (if present) not before `startDate`; any consent flag not explicitly `true` in the request — including when the whole `consent` object is omitted — is persisted as `false` (FR-010, never inferred `true`) — every `.WithMessage(...)` MUST use the corresponding `errors.contract.*` key from T022, never a raw string (constitution Principle IV) — depends on T016, T014
- [X] T033 [US1] Create `UpdateContractCommand` + `UpdateContractCommandValidator` + `UpdateContractCommandHandler` in `backend/ChildCare.Application/Contracts/UpdateContractCommand.cs`, reusing the same field rules as `CreateContractCommandValidator` (T032) as a full replacement of the draft's terms (FR-001a — `ChildId`/`LocationId` are never accepted in this request and cannot be changed); handler rejects with `ContractResult.NotDraft` unless `Status == Draft` — depends on T016, T014
- [X] T034 [US1] Create `ActivateContractCommand` + `ActivateContractCommandHandler` in `backend/ChildCare.Application/Contracts/ActivateContractCommand.cs`: loads the contract, fails `ContractResult.NotDraft` unless `Status == Draft`, otherwise calls `IAdvisoryLockService.RunExclusiveAsync(contract.ChildId, () => ContractActivationChecker.CheckAndActivateAsync(db, contract, ct))`, mapping the checker's failure (if any) to `ContractResult.AlreadyActiveAtLocation`/`DayOverlap` — depends on T011/T012, T013, T014
- [X] T035 [P] [US1] Create `GetContractByIdQuery` + `GetContractByIdQueryHandler` in `backend/ChildCare.Application/Contracts/GetContractByIdQuery.cs` — depends on T014
- [X] T036 [P] [US1] Create `ListChildContractsQuery` + `ListChildContractsQueryHandler` (ordered most-recent-first) in `backend/ChildCare.Application/Contracts/ListChildContractsQuery.cs` (FR-017) — depends on T014
- [X] T037 [US1] Implement `GET/POST /api/children/{childId}/contracts`, `GET/PUT /api/contracts/{id}`, `POST /api/contracts/{id}/activate` in `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs`, mapping `ContractResult` cases to the `200`/`201`/`404`/`409`/`422` responses from contracts/contracts-api.md — depends on T032, T033, T034, T035, T036, T017

**Checkpoint**: US1 fully functional and independently testable — a director can create and activate a single-location contract, non-Director roles are rejected, and tenant isolation holds.

---

## Phase 4: User Story 2 - Split-Location Enrolment Across Non-Overlapping Days (Priority: P1)

**Goal**: A child can hold two simultaneously active contracts at different locations provided their contracted weekdays never overlap, enforced atomically even under concurrent activation attempts.

**Independent Test**: Activate a contract at Location A for Mon+Tue, then a second at Location B for Wed+Thu (succeeds); a third at a third location for Tue+Wed fails on the Tuesday conflict; two conflicting concurrent activation requests resolve to exactly one success (quickstart.md Scenario 2).

**Note**: No new implementation is required for this story — `ActivateContractCommand` (T034) and `ContractActivationChecker` (T013) already implement the cross-location day-overlap check and the per-child advisory lock as part of Foundational/US1. This phase is purely the test coverage that proves that shared mechanism holds at the scope constitution Principle II specifically names (multi-location, concurrent).

### Tests for User Story 2

- [X] T038 [P] [US2] Integration test: activate a contract for a child at Location A (Mon+Tue), then activate a second contract for the same child at Location B (Wed+Thu) → both `200`, both simultaneously `active` — in `backend/ChildCare.Api.Tests/ContractSplitLocationTests.cs` (FR-005, quickstart.md Scenario 2 step 1) — depends on T037
- [X] T039 [P] [US2] Integration test: with the Location A (Mon+Tue) contract active, activate a third contract at a third location for Tue+Wed → `409 errors.contract.day_overlap`, and neither the new contract nor the existing active one changes status — same file (FR-005, quickstart.md Scenario 2 step 2) — depends on T037
- [X] T040 [P] [US2] Integration test: two draft contracts for the same child that would conflict with each other (not with anything currently active) have their `POST /api/contracts/{id}/activate` requests fired concurrently (`Task.WhenAll`) → exactly one returns `200`, the other returns `409 errors.contract.day_overlap` — same file (FR-006, quickstart.md Scenario 2 step 3) — depends on T037
- [X] T041 [P] [US2] Integration test: a contract's care period ends and a new contract's period begins the immediately following day at the same location (same-day transition via independent create+activate, not amendment) → activation succeeds, not blocked by the prior (now-`Ended`, not `Active`) contract — same file (Edge Cases) — depends on T037

**Checkpoint**: US1 and US2 both work independently — split-location enrolment is provably correct even under concurrency.

---

## Phase 5: User Story 3 - Amend Contract Terms with Full Audit Trail (Priority: P2)

**Goal**: A director changes an active contract's terms by ending it and creating an activated successor, preserving the original unmodified.

**Independent Test**: Amend an active contract with new terms effective a future date, confirm the original is `ended` with the correct `endDate` and the new one is `active`, both retrievable in history (quickstart.md Scenario 3).

### Tests for User Story 3

- [X] T042 [P] [US3] Integration test: `POST /api/contracts/{id}/amend` on an active contract with new terms and a future `effectiveStartDate` → `201`, new contract `active` with `previousContractId` set; original contract → `ended`, `endDate` = day before `effectiveStartDate` — in `backend/ChildCare.Api.Tests/ContractAmendmentTests.cs` (FR-007, quickstart.md Scenario 3 step 1) — depends on T037
- [X] T043 [P] [US3] Integration test: `GET /api/children/{childId}/contracts` after an amendment shows both the ended original and the new active contract, most-recent-first — same file (FR-009, quickstart.md Scenario 3 steps 2–3) — depends on T042
- [X] T044 [P] [US3] Integration test: amend with `effectiveStartDate` on or before the current contract's own `startDate` → `422 errors.contract.amendment_start_date_invalid`, original contract unchanged — same file — depends on T037
- [X] T045 [P] [US3] Integration test: amend a contract whose new terms would conflict (day-overlap or same-location) with another active contract → `409 errors.contract.day_overlap` / `409 errors.contract.already_active_at_location`, and the original contract being amended remains `active` and unmodified (the whole amendment is rolled back, not partially applied) — same file (FR-008) — depends on T037
- [X] T046 [P] [US3] Integration test: amend a `draft` or already-`ended` contract → `409 errors.contract.not_active` — same file — depends on T037

### Implementation for User Story 3

- [X] T047 [US3] Create `AmendContractCommand` + `AmendContractCommandValidator` + `AmendContractCommandHandler` in `backend/ChildCare.Application/Contracts/AmendContractCommand.cs`: request carries the **complete** set of new terms (weekdays/hours, daily rate, end date, consent — a full replacement per FR-007, reusing `CreateContractCommandValidator`'s field rules including the FR-010 consent-defaults-to-false behavior) plus `effectiveStartDate`; fails `ContractResult.NotActive` unless the target is `Active`; fails `ContractResult.AmendmentStartDateInvalid` if `effectiveStartDate <= contract.StartDate`; otherwise, inside `IAdvisoryLockService.RunExclusiveAsync(contract.ChildId, ...)`: set the current contract's `Status = Ended` and `EndDate = effectiveStartDate.AddDays(-1)` (tracked, not yet saved), create a new `Contract` (`Status = Draft`, `PreviousContractId` = current contract's id, new terms), then call `ContractActivationChecker.CheckAndActivateAsync(db, newContract, ct)` — if it fails, do not call `SaveChangesAsync` at all (discards both the in-memory `Ended` transition and the new draft, so nothing persists); if it succeeds, the checker's own `SaveChangesAsync` persists both atomically — depends on T013, T011/T012, T014, T016
- [X] T048 [US3] Implement `POST /api/contracts/{id}/amend` in `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs` — depends on T047

**Checkpoint**: US1–US3 all work independently — amendments preserve full history without touching termination or PDF generation.

---

## Phase 6: User Story 3a - Terminate a Contract with No Successor (Priority: P2)

**Goal**: A director ends an active contract entirely (family leaves) without creating any replacement.

**Independent Test**: Terminate an active contract, confirm it becomes `ended` with no new contract created, and its former weekdays no longer block future activations (quickstart.md Scenario 4).

### Tests for User Story 3a

- [X] T049 [P] [US3a] Integration test: `POST /api/contracts/{id}/terminate` with an `endDate` on an active contract → `200`, `status: "ended"`, `endDate` set, no new contract row created (assert contract count for the child is unchanged) — in `backend/ChildCare.Api.Tests/ContractTerminationTests.cs` (FR-009a, quickstart.md Scenario 4 step 1) — depends on T037
- [X] T050 [P] [US3a] Integration test: after terminating a contract, create and activate a brand-new contract for the same child reusing the terminated contract's former weekdays → `200`/`201` success, not blocked — same file (FR-009a, quickstart.md Scenario 4 step 2) — depends on T049
- [X] T051 [P] [US3a] Integration test: terminate with `endDate` before the contract's own `startDate` → `422 errors.contract.termination_date_invalid` — same file — depends on T037
- [X] T052 [P] [US3a] Integration test: terminate a `draft` or already-`ended` contract → `409 errors.contract.not_active` — same file — depends on T037
- [X] T052a [P] [US3a] Integration test: `PUT /api/contracts/{id}` on a contract already `ended` by termination → `409 errors.contract.not_draft`, unchanged (FR-009's immutability guarantee — draft-edit is impossible for any non-`draft` status, not only `active`; `/speckit-analyze` E2) — same file — depends on T049

### Implementation for User Story 3a

- [X] T053 [US3a] Create `TerminateContractCommand` + `TerminateContractCommandValidator` + `TerminateContractCommandHandler` in `backend/ChildCare.Application/Contracts/TerminateContractCommand.cs`: fails `ContractResult.NotActive` unless `Status == Active`; fails `ContractResult.TerminationDateInvalid` if `endDate < contract.StartDate`; otherwise sets `Status = Ended`, `EndDate = endDate`, saves — no lock needed (no cross-contract check on termination, only a single-row status change) — depends on T014, T016
- [X] T054 [US3a] Implement `POST /api/contracts/{id}/terminate` in `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs` — depends on T053

**Checkpoint**: US1–US3a all work independently.

---

## Phase 7: User Story 4 - Generate a Signable Contract PDF (Priority: P2)

**Goal**: A director generates a PDF of a contract (draft or active) containing all terms, consent choices, and a signature line.

**Independent Test**: Generate a PDF for an active contract and confirm it contains the expected content markers; generate one for a draft contract and confirm it still succeeds (quickstart.md Scenario 5).

### Tests for User Story 4

- [X] T055 [P] [US4] Integration test: `GET /api/contracts/{id}/pdf` on an active contract → `200`, `Content-Type: application/pdf`, non-empty body starting with the `%PDF` magic bytes — in `backend/ChildCare.Api.Tests/ContractPdfTests.cs` (FR-011, quickstart.md Scenario 5 step 1) — depends on T060
- [X] T056 [P] [US4] Integration test: `GET /api/contracts/{id}/pdf` on a `draft` contract → `200`, PDF still generated — same file (FR-011, quickstart.md Scenario 5 step 2) — depends on T060
- [X] T057 [P] [US4] Integration test: `GET /api/contracts/{id}/pdf` for a nonexistent contract id → `404 errors.contract.not_found` — same file — depends on T060
- [X] T057a [P] [US4] Integration test: `GET /api/contracts/{id}/pdf` with no `locale` query param defaults to Dutch content; `?locale=fr` and `?locale=en` each produce a PDF with that locale's static labels (assert via the known label strings baked into each locale's resource, not full-text OCR) — same file (FR-011) — depends on T060

### Implementation for User Story 4

- [X] T058 [US4] Create `QuestPdfContractGenerator` implementing `IContractPdfGenerator` in `backend/ChildCare.Infrastructure/Pdf/QuestPdfContractGenerator.cs`: renders child name, location name, status, a contracted-days/hours table, the daily rate, all five consent choices, and a signature line, using QuestPDF's fluent `Document.Create(...)` API; all static labels ("Daily rate", "Contracted days", "Signature", etc.) are resolved from `ContractPdfModel.Locale` ("nl"/"fr"/"en") via a small locale-keyed string lookup, never hardcoded in one language (constitution Principle IV, FR-011/FR-016) — depends on T009, T010
- [X] T059 [US4] Create `GenerateContractPdfQuery` + `GenerateContractPdfQueryHandler` in `backend/ChildCare.Application/Contracts/GenerateContractPdfQuery.cs`: loads the contract plus its child's and location's names, builds a `ContractPdfModel` including the requested `Locale` (defaulting to `"nl"` when not supplied or unrecognized), calls `IContractPdfGenerator.GenerateAsync` — depends on T058, T014
- [X] T060 [US4] Implement `GET /api/contracts/{id}/pdf` (accepting an optional `locale` query parameter) in `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs` returning `Results.File(bytes, "application/pdf")`; register `services.AddScoped<IContractPdfGenerator, QuestPdfContractGenerator>()` in `backend/ChildCare.Api/Program.cs` — depends on T059

**Checkpoint**: All primary user stories (US1–US4, plus US3a) are independently functional.

---

## Phase 8: Deactivation Guard Integration (cross-cutting)

**Purpose**: Prove the two guard implementations built in Foundational (T018/T019) correctly block deactivation of a location or child with an active contract, and correctly stop blocking once the contract ends. No new implementation — this phase is test-only.

- [X] T061 [P] Integration test: `POST /api/locations/{id}/deactivate` on a location with an active contract → `409 errors.location.has_active_dependents` — in `backend/ChildCare.Api.Tests/ContractDeactivationGuardTests.cs` (research.md R3, quickstart.md Scenario 6 step 1) — depends on T037
- [X] T062 [P] Integration test: `POST /api/children/{id}/deactivate` on a child with an active contract → `409 errors.child.has_active_dependents` — same file (research.md R3, quickstart.md Scenario 6 step 2) — depends on T037
- [X] T063 [P] Integration test: terminate the contract, then retry both deactivations → both `200` — same file (quickstart.md Scenario 6 step 3) — depends on T053, T061, T062

**Checkpoint**: The feature 004/006 extension points are fully wired and verified.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all stories.

- [X] T064 [P] Run `dotnet ef migrations script --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api` and review the generated SQL for the `AddContracts` migration (constitution Principle VI — rollout to existing tenant schemas uses the existing `migrate-tenants` CLI, unchanged mechanism from feature 002)
- [X] T065 Run the full `dotnet test backend/ChildCare.sln` suite and confirm no regressions in pre-existing tests (features 001–006) — extend `TenantMigrationRolloutTests`' revert-simulation to also drop `contracts` (before `children`/`locations`, FK-dependency order) and add `AddContracts` to the migration-history-row DELETE clause (same class of gap features 004–006 already hit)
- [X] T066 Walk through every scenario in `quickstart.md` manually (or via the automated tests already covering them) and confirm all six pass end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–7)**: All depend on Foundational phase completion
  - US1 has no dependency on other stories
  - US2 depends on US1's `ActivateContractCommand`/endpoint (T034/T037) — it adds no new implementation, only tests proving the shared mechanism at cross-location/concurrent scope
  - US3 (amend) and US3a (terminate) each depend on US1's `ContractsEndpoints.cs`/create+activate existing (T037) since both need an active contract to act on; they are otherwise independent of each other
  - US4 (PDF) depends on US1's `ContractsEndpoints.cs` (T037) for a contract to generate a PDF from, but not on US2/US3/US3a
- **Deactivation Guard Integration (Phase 8)**: Depends on US1 (T037) and US3a (T053, to prove the guard stops blocking after termination)
- **Polish (Phase 9)**: Depends on all user stories + Deactivation Guard Integration being complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Entity/config before commands/queries
- Commands/queries before endpoints
- Story complete before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (T002, T003, T004, T009, T010, T011, T014, T015, T016, T018, T019, T020, T022)
- Once Foundational completes, US1's tests (T023–T031, T027a, T027b) can run in parallel
- US2's tests (T038–T041) can all run in parallel once US1's T037 exists — no implementation tasks to sequence
- US3's and US3a's implementation (T047/T053) can be built in parallel with each other once Foundational + US1 exist (different files, no shared dependency beyond the checker/lock already built)
- US4's `QuestPdfContractGenerator` (T058) has no dependency on US2/US3/US3a and can be built as soon as Foundational's T009/T010 exist, in parallel with those stories

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Integration test: create draft with minimal fields in backend/ChildCare.Api.Tests/ContractLifecycleTests.cs"
Task: "Integration test: create draft with full optional fields in backend/ChildCare.Api.Tests/ContractLifecycleTests.cs"
Task: "Integration test: activate draft in backend/ChildCare.Api.Tests/ContractLifecycleTests.cs"
Task: "Integration test: same-location double-activation rejected in backend/ChildCare.Api.Tests/ContractLifecycleTests.cs"

# Launch command/query scaffolding for User Story 1 together:
Task: "Create GetContractByIdQuery + Handler in backend/ChildCare.Application/Contracts/GetContractByIdQuery.cs"
Task: "Create ListChildContractsQuery + Handler in backend/ChildCare.Application/Contracts/ListChildContractsQuery.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (quickstart.md Scenario 1)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently (proves the shared mechanism at full scope) → Deploy/Demo
4. Add User Story 3 (amend) → Test independently → Deploy/Demo
5. Add User Story 3a (terminate) → Test independently → Deploy/Demo
6. Add User Story 4 (PDF) → Test independently → Deploy/Demo
7. Add Deactivation Guard Integration → Test independently → Deploy/Demo
8. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No task in this feature touches web/mobile — backend only (plan.md Technical Context)
- The day-overlap/one-active-per-location check and its advisory lock (T013, T011/T012) are built once in Foundational/US1 and reused unchanged by US2's tests and US3's amendment — this is deliberate, not an oversight (see US2's phase note)
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

---

## Phase 10: Convergence

- [X] T067 [P] Add role-policy test coverage proving Staff/Parent roles receive `403` on `POST /api/contracts/{id}/amend`, `POST /api/contracts/{id}/terminate`, and `GET /api/contracts/{id}/pdf` in `backend/ChildCare.Api.Tests/ContractLifecycleTests.cs` per FR-014 (FR-014, missing)
- [X] T068 [P] Update plan.md's Project Structure section to include `ChildCare.Domain/ValueObjects/ContractedDay.cs`/`ContractConsent.cs` and the `ITenantDbContext.cs` modification, matching what tasks.md T003/T004/T006 already correctly required and what was actually built (plan: project structure, partial)
