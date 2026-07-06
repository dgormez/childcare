---

description: "Task list for feature implementation"
---

# Tasks: Location Management

**Input**: Design documents from `/specs/004-locations/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL. Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 create/manage core fields, US2 P2 optional Opgroeien settings, US3 P3 deactivate/reactivate, US4 P4 duplicate). The `Location` entity, its EF configuration/migration, the shared result/response shapes, and the endpoint group's `DirectorOnly` wiring are shared prerequisites every story depends on, so they live in Foundational — the story phases add story-specific commands/queries and their tests.

**Revision note (2026-07-06, post `/speckit-analyze`)**: Renumbered from the original 40-task version to insert T015 (role-enforcement negative test, finding G1) and T016 (concurrency last-write-wins test, finding G4); extended T011 with a KBO-absence assertion (finding G3) and T013 with a malformed-email case (finding G2); added explicit errorKey-wiring instructions to T018/T020 (finding S2). Every task ID below is the current, authoritative one — do not cross-reference the pre-analysis numbering.

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

**Purpose**: The `Location` entity, its EF Core configuration and migration, the shared command/query result shape, the response DTO, and the `DirectorOnly`-protected, non-exempt endpoint group. Every user story's commands, queries, and tests depend on this being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Entity

- [X] T002 Create `Location` entity in `backend/ChildCare.Domain/Entities/Location.cs`: `Id`, `Name`, `Address`, `Phone`, `Email`, `MaxCapacity`, `NaamLocatie?`, `Dossiernummer?`, `Verantwoordelijke?`, `FlexPermission` (default `false`), `BoPermission` (default `false`), `DeactivatedAt?`, `CreatedAt`, `UpdatedAt` — no `OrganisationId`/tenant column, and no KBO/ondernemingsnummer field of any kind (FR-014 — that field stays organisation-level only, data-model.md, research.md R1)
- [X] T003 Add `DbSet<Location> Locations` to `ITenantDbContext` in `backend/ChildCare.Application/Common/ITenantDbContext.cs` — depends on T002
- [X] T004 Add `DbSet<Location> Locations` and its `OnModelCreating` configuration to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`: table `"locations"`, `CHECK ("MaxCapacity" > 0)`, `HasMaxLength` per data-model.md, index on `DeactivatedAt` — depends on T002, T003
- [X] T005 Generate the `TenantDbContext` migration (`dotnet ef migrations add AddLocations --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` — depends on T004

### Shared Application/Contracts shapes

- [X] T006 [P] Create `LocationResult` shared success/failure result type in `backend/ChildCare.Application/Locations/LocationResult.cs`, mirroring `AuthResult`'s shape (feature 003) — failure cases: `NotFound`, `ValidationFailed` (handled by the existing `ValidationBehavior` pipeline instead), `HasActiveDependents`
- [X] T007 [P] Create `LocationResponse` in `backend/ChildCare.Contracts/Responses/LocationResponse.cs` (data-model.md: `Id`, `Name`, `Address`, `Phone`, `Email`, `MaxCapacity`, `NaamLocatie`, `Dossiernummer`, `Verantwoordelijke`, `FlexPermission`, `BoPermission`, `DeactivatedAt`, `CreatedAt`, `UpdatedAt` — no KBO-like field, FR-014)

### Endpoint group wiring

- [X] T008 Create `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` with `MapLocationEndpoints(this WebApplication app)`: `app.MapGroup("/api/locations").WithTags("Locations").RequireAuthorization("DirectorOnly")` — no `.RequireTenantExempt()` (this is the first tenant-domain-data endpoint group; `TenantMiddleware` must run). Group-level `RequireAuthorization` covers every route mapped inside it (list, get, create, update, deactivate, reactivate, duplicate — FR-011) — empty route bodies for now, filled in per story below — depends on T002
- [X] T009 Register `app.MapLocationEndpoints();` in `backend/ChildCare.Api/Program.cs` alongside the existing `app.MapOrganisationEndpoints();` call — depends on T008
- [X] T010 [P] Add `errors.location.not_found` (404), `errors.location.has_active_dependents` (409), `errors.location.name_required`, `errors.location.address_required`, `errors.location.phone_required`, `errors.location.email_required`, `errors.location.email_invalid`, `errors.location.max_capacity_invalid` entries to `backend/ERROR_KEYS.md` under a new "Location Management (feature `004-locations`)" section (contracts/locations-api.md)

**Checkpoint**: `Location` exists end-to-end (entity → EF config → migration), the endpoint group is registered and policy-protected, and every command/query has the shared result/response shapes to return. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director Creates and Manages Locations (Priority: P1) 🎯 MVP

**Goal**: A director can create, list, view, and update a location's core fields (name, address, phone, email, max capacity) within their own organisation.

**Independent Test**: Create two locations in one organisation via the web admin's API, confirm both appear in that organisation's list independently, edit one, and confirm the other is unaffected (quickstart.md Scenario 1).

### Tests for User Story 1

- [X] T011 [P] [US1] Integration test: create a location with all core fields → `201`, response matches input, `naamLocatie`/`dossiernummer`/`verantwoordelijke` are `null`, `flexPermission`/`boPermission` are `false`, and the response contains no KBO/ondernemingsnummer-like field (FR-014) — in `backend/ChildCare.Api.Tests/LocationCrudTests.cs` (FR-001, FR-005, FR-014, quickstart.md Scenario 1) — depends on T009
- [X] T012 [P] [US1] Integration test: create a second location in the same organisation, `GET /api/locations` returns both independently; editing one via `PUT` does not affect the other, same file (FR-006, SC-003) — depends on T009
- [X] T013 [P] [US1] Integration test: create requests missing `name`/`address`/`phone`/`email`, with `maxCapacity <= 0`, or with a syntactically invalid (but present) `email` value (e.g. `"not-an-email"`) → each `422 errors.validation` with the corresponding `fieldErrors` key from `backend/ERROR_KEYS.md` (`errors.location.email_invalid` for the malformed case) — same file (FR-010) — depends on T009, T010
- [X] T014 [P] [US1] Integration test: a location created in Org A is invisible to Org B — `GET /api/locations/{orgALocationId}` as Org B's director → `404 errors.location.not_found`; `GET /api/locations` as Org B never includes it — in `backend/ChildCare.Api.Tests/LocationCrudTests.cs` (constitution Principle I, FR-007, SC-004, quickstart.md Scenario 5) — depends on T009
- [X] T015 [P] [US1] Integration test: a Staff-role and a Parent-role access token (seeded the same way `AuthRolePolicyTests.cs` seeds non-Director roles, feature 003) each receive `403` against `GET /api/locations`, `GET /api/locations/{id}`, `POST /api/locations`, and `PUT /api/locations/{id}` — in `backend/ChildCare.Api.Tests/LocationCrudTests.cs` (FR-011, constitution Principle II's authorization posture) — depends on T009
- [X] T016 [P] [US1] Integration test: two sequential `PUT` requests to the same location with different `name` values — the response to a subsequent `GET` matches the second (later) write, confirming last-write-wins with no conflict error from the first (FR-017, research.md R3) — same file — depends on T009

### Implementation for User Story 1

- [X] T017 [P] [US1] Create `CreateLocationRequest` and `UpdateLocationRequest` in `backend/ChildCare.Contracts/Requests/LocationRequests.cs` (data-model.md)
- [X] T018 [P] [US1] Create `CreateLocationCommand` + `CreateLocationCommandValidator` (required `Name`/`Address`/`Phone`/`Email`, valid email format, `MaxCapacity > 0`) in `backend/ChildCare.Application/Locations/CreateLocationCommand.cs` / `CreateLocationCommandValidator.cs` — every rule's `.WithMessage(...)` MUST use the corresponding `errors.location.*` key from T010 (e.g. `.NotEmpty().WithMessage("errors.location.name_required")`), mirroring `backend/ChildCare.Application/Auth/LoginCommandValidator.cs`'s existing pattern — never a raw English string (constitution Principle IV, NON-NEGOTIABLE) — depends on T017
- [X] T019 [US1] Create `CreateLocationCommandHandler` in `backend/ChildCare.Application/Locations/CreateLocationCommandHandler.cs`: build a new `Location` (Opgroeien fields left `null`/`false`), `db.Locations.Add(...)`, `SaveChangesAsync` — depends on T018, T006
- [X] T020 [P] [US1] Create `UpdateLocationCommand` + `UpdateLocationCommandValidator` (same required-field rules as create; `NaamLocatie`/`Dossiernummer`/`Verantwoordelijke` optional) in `backend/ChildCare.Application/Locations/UpdateLocationCommand.cs` / `UpdateLocationCommandValidator.cs` — same `errors.location.*` `.WithMessage(...)` wiring requirement as T018 (constitution Principle IV) — depends on T017
- [X] T021 [US1] Create `UpdateLocationCommandHandler` in `backend/ChildCare.Application/Locations/UpdateLocationCommandHandler.cs`: load by id (return `NotFound` via `LocationResult` if missing), overwrite all fields (last-write-wins, no concurrency token — FR-017, research.md R3), set `UpdatedAt`, `SaveChangesAsync` — depends on T020, T006
- [X] T022 [P] [US1] Create `ListLocationsQuery` (with `bool IncludeDeactivated = false`) + `ListLocationsQueryHandler` in `backend/ChildCare.Application/Locations/ListLocationsQuery.cs`: filter `DeactivatedAt == null` unless `IncludeDeactivated` (FR-002, FR-008, research.md R2, R6) — depends on T006
- [X] T023 [P] [US1] Create `GetLocationByIdQuery` + `GetLocationByIdQueryHandler` in `backend/ChildCare.Application/Locations/GetLocationByIdQuery.cs` — depends on T006
- [X] T024 [US1] Implement `GET /api/locations` (with `includeDeactivated` query param), `GET /api/locations/{id}`, `POST /api/locations`, `PUT /api/locations/{id}` in `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs`, mapping `LocationResult`/query results to `LocationResponse` and the `200`/`201`/`404 errors.location.not_found` responses from contracts/locations-api.md — depends on T019, T021, T022, T023

**Checkpoint**: US1 fully functional and independently testable — a director can create, list, view, and update locations scoped to their own organisation; non-Director roles are rejected; concurrent edits resolve deterministically.

---

## Phase 4: User Story 2 - Director Fills In Opgroeien Reporting Settings (Priority: P2)

**Goal**: A director can leave `dossiernummer`/`verantwoordelijke` blank at creation and fill them in later, and set `flexPermission`/`boPermission`, without those fields ever blocking location creation.

**Independent Test**: Create a location with only core fields, confirm it saves with Opgroeien fields empty/false, then edit it to add `dossiernummer`/`verantwoordelijke` and toggle `flexPermission`/`boPermission` (quickstart.md Scenario 2).

### Tests for User Story 2

- [X] T025 [P] [US2] Integration test: create a location omitting `naamLocatie`/`dossiernummer`/`verantwoordelijke` → `201` succeeds (not blocked); `PUT` adding all three later → `200`, `GET` reflects the new values — in `backend/ChildCare.Api.Tests/LocationOpgroeienSettingsTests.cs` (FR-003, FR-004, quickstart.md Scenario 2) — depends on T024
- [X] T026 [P] [US2] Integration test: create a location with no `flexPermission`/`boPermission` supplied → both `false` in the response; `PUT` setting both to `true` → `200`, `GET` reflects `true`, same file (FR-005) — depends on T024

### Implementation for User Story 2

- [X] T027 [US2] Confirm `CreateLocationCommandValidator`/`UpdateLocationCommandValidator` (T018/T020) impose no rules on `NaamLocatie`/`Dossiernummer`/`Verantwoordelijke` (nullable, no `NotEmpty()`) — add an explicit FluentValidation rule comment/test-covered guard if any accidental requirement exists (FR-004) — depends on T018, T020

**Checkpoint**: US1 and US2 both work independently — Opgroeien settings are fully optional at creation and editable later.

---

## Phase 5: User Story 3 - Director Deactivates and Reactivates a Location (Priority: P3)

**Goal**: A director can soft-delete a location (excluded from active listings, never hard-deleted) and reactivate it later, restoring all prior settings.

**Independent Test**: Deactivate a location with no dependents, confirm it disappears from the default list but remains visible with `includeDeactivated=true`, then reactivate it and confirm it reappears unchanged (quickstart.md Scenario 3).

### Tests for User Story 3

- [X] T028 [P] [US3] Integration test: deactivate a location with no dependents → `200`, `deactivatedAt` set; default `GET /api/locations` excludes it; `GET /api/locations?includeDeactivated=true` includes it — in `backend/ChildCare.Api.Tests/LocationDeactivationTests.cs` (FR-008, FR-009, SC-005, quickstart.md Scenario 3) — depends on T024
- [X] T029 [P] [US3] Integration test: reactivate a previously-deactivated location → `200`, `deactivatedAt` is `null`, all prior field values intact; default `GET /api/locations` includes it again, same file (FR-008 clarified) — depends on T028
- [X] T030 [P] [US3] Integration test: deactivate every location in an organisation (down to zero active) → no error at any step; `GET /api/locations` returns an empty array, not a failure, same file (FR-016) — depends on T024
- [X] T031 [P] [US3] Integration test: deactivating/reactivating an already-deactivated/already-active location is idempotent (`200`, no state change on the second call), same file — depends on T024

### Implementation for User Story 3

- [X] T032 [P] [US3] Create `ILocationDeactivationGuard` in `backend/ChildCare.Application/Common/ILocationDeactivationGuard.cs`: `Task<bool> HasActiveDependentsAsync(Guid locationId, ITenantDbContext db, CancellationToken ct)` (research.md R4, data-model.md) — no implementation registered by this feature
- [X] T033 [US3] Create `DeactivateLocationCommand` + `DeactivateLocationCommandHandler` in `backend/ChildCare.Application/Locations/DeactivateLocationCommand.cs`: resolve `IEnumerable<ILocationDeactivationGuard>` from DI, fail with `LocationResult.HasActiveDependents` if any returns `true` (FR-012), else set `DeactivatedAt = DateTime.UtcNow` (idempotent if already set) — depends on T032, T006
- [X] T034 [P] [US3] Create `ReactivateLocationCommand` + `ReactivateLocationCommandHandler` in `backend/ChildCare.Application/Locations/ReactivateLocationCommand.cs`: set `DeactivatedAt = null` (idempotent if already `null`) — depends on T006
- [X] T035 [US3] Implement `POST /api/locations/{id}/deactivate` and `POST /api/locations/{id}/reactivate` in `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs`, mapping `LocationResult.HasActiveDependents` to `409 errors.location.has_active_dependents` — depends on T033, T034

**Checkpoint**: US1, US2, and US3 all work independently — locations can be deactivated and reactivated without data loss, and an organisation may have zero active locations.

---

## Phase 6: User Story 4 - Director Duplicates an Existing Location (Priority: P4)

**Goal**: A director can create a new location by duplicating an existing one's fields, editing only what differs, instead of re-entering everything from scratch.

**Independent Test**: Duplicate a fully-populated location, confirm the new location has identical field values and no reference to the source, then edit the new one and confirm the source is unaffected (quickstart.md Scenario 4).

### Tests for User Story 4

- [X] T036 [P] [US4] Integration test: duplicate an existing location → `201`, new `id`, all copyable field values match the source, response contains no field referencing the source's id — in `backend/ChildCare.Api.Tests/LocationDuplicateTests.cs` (FR-015, quickstart.md Scenario 4) — depends on T024
- [X] T037 [P] [US4] Integration test: edit the duplicated location's `name`/`address` → `200`; `GET` on the original source location shows it unchanged, same file (FR-015) — depends on T036

### Implementation for User Story 4

- [X] T038 [US4] Create `DuplicateLocationCommand` + `DuplicateLocationCommandHandler` in `backend/ChildCare.Application/Locations/DuplicateLocationCommand.cs`: load source by id (`NotFound` if missing), construct a new `Location` with a new `Id` and all copyable field values, no persisted link to the source (research.md R5) — depends on T006
- [X] T039 [US4] Implement `POST /api/locations/{id}/duplicate` in `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` — depends on T038

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all stories.

- [X] T040 [P] Run `dotnet ef migrations script --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api` and review the generated SQL for the `AddLocations` migration (constitution Principle VI — new-tenant-schema auto-apply carve-out covers brand-new schemas only; rollout to already-existing tenant schemas uses the existing `migrate-tenants` CLI, unchanged mechanism from feature 002)
- [X] T041 Run the full `dotnet test backend/ChildCare.sln` suite and confirm no regressions in pre-existing tests (features 001–003) — required extending `TenantMigrationRolloutTests`' revert-simulation to also drop `locations`/`AddLocations`'s history row (same class of gap as feature 003's `AddUserRole` fix)
- [X] T042 Walk through every scenario in `quickstart.md` manually (or via the automated tests already covering them) and confirm all five pass end-to-end — all five are directly exercised by `LocationCrudTests.cs`/`LocationOpgroeienSettingsTests.cs`/`LocationDeactivationTests.cs`/`LocationDuplicateTests.cs` against real TestContainers PostgreSQL

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1 has no dependency on other stories
  - US2 depends on US1's `CreateLocationCommand`/`UpdateLocationCommand`/`LocationEndpoints.cs` existing (T019–T024) since it only adds validator confirmation + tests on top of already-built fields — cannot start meaningfully before US1's T024 checkpoint
  - US3 depends on Foundational only (its own commands/endpoints are new) — independently startable after Phase 2, though `GET ?includeDeactivated` (built in US1's T022) is needed for its tests to be meaningful, so in practice follows US1
  - US4 depends on Foundational only, but its command needs a source location to duplicate, so in practice follows US1
- **Polish (Phase 7)**: Depends on all four user stories being complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Entity/config before commands/queries
- Commands/queries before endpoints
- Story complete before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (T006, T007, T010)
- Once Foundational completes, US1's tests (T011–T016) can run in parallel; US1's command/query scaffolding (T017, T018, T020, T022, T023) can run in parallel before their handlers
- US3's guard interface (T032) and US4's command (T038) can be built in parallel with each other once Foundational + US1's `LocationEndpoints.cs` skeleton exist, since they touch different files
- Different user stories can be worked on in parallel by different team members once Foundational is done, keeping in mind the practical sequencing note above (US2/US3/US4 tests are more meaningful once US1's create/list endpoints exist to exercise)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Integration test: create a location with all core fields (incl. no-KBO-field assertion) in backend/ChildCare.Api.Tests/LocationCrudTests.cs"
Task: "Integration test: create a second location, list independently in backend/ChildCare.Api.Tests/LocationCrudTests.cs"
Task: "Integration test: validation failures incl. malformed email in backend/ChildCare.Api.Tests/LocationCrudTests.cs"
Task: "Integration test: tenant isolation in backend/ChildCare.Api.Tests/LocationCrudTests.cs"
Task: "Integration test: Staff/Parent roles get 403 in backend/ChildCare.Api.Tests/LocationCrudTests.cs"
Task: "Integration test: concurrent edits resolve last-write-wins in backend/ChildCare.Api.Tests/LocationCrudTests.cs"

# Launch command/query scaffolding for User Story 1 together:
Task: "Create CreateLocationCommand + Validator in backend/ChildCare.Application/Locations/CreateLocationCommand.cs"
Task: "Create UpdateLocationCommand + Validator in backend/ChildCare.Application/Locations/UpdateLocationCommand.cs"
Task: "Create ListLocationsQuery + Handler in backend/ChildCare.Application/Locations/ListLocationsQuery.cs"
Task: "Create GetLocationByIdQuery + Handler in backend/ChildCare.Application/Locations/GetLocationByIdQuery.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (quickstart.md Scenario 1 + 5)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Add User Story 4 → Test independently → Deploy/Demo
6. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No task in this feature touches web/mobile — backend only (plan.md Technical Context)
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

---

## Phase 8: Convergence

**Purpose**: Two validation-completeness gaps found by `/speckit-converge` (2026-07-06) — every FR/SC/acceptance scenario is otherwise satisfied and tested (76/76 passing), but a re-read of spec.md's Edge Cases/Assumptions surfaced these two secondary gaps.

- [X] T043 [P] Add a phone-format FluentValidation rule (e.g. `.Matches(...)` with a permissive international phone pattern) to `backend/ChildCare.Application/Locations/CreateLocationCommandValidator.cs` and `UpdateLocationCommandValidator.cs`, returning a new `errors.location.phone_invalid` key (add to `backend/ERROR_KEYS.md`) for a syntactically invalid but present phone value, mirroring the existing `.EmailAddress()` check per spec.md Edge Cases ("invalid email or phone format") (partial) — tested in `LocationCrudTests.cs`; also required fixing pre-existing tests' `"Phone"` placeholder literals across `LocationOpgroeienSettingsTests.cs`/`LocationDeactivationTests.cs`/`LocationDuplicateTests.cs` to a valid phone value, since the literal itself no longer passed format validation
- [X] T044 [P] Add `MaximumLength(...)` FluentValidation rules to `backend/ChildCare.Application/Locations/CreateLocationCommandValidator.cs` (`Name` 200, `Address` 500, `Phone` 30, `Email` 254) and `UpdateLocationCommandValidator.cs` (same four, plus `NaamLocatie` 200, `Dossiernummer` 50, `Verantwoordelijke` 200), matching `TenantDbContext`'s `HasMaxLength` constraints (data-model.md), so an over-length value returns a field-specific `422` instead of falling through to a generic `500 errors.unexpected` on a DB constraint violation, per FR-010 (partial) — tested in `LocationCrudTests.cs`
