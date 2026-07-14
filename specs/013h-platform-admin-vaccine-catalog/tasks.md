---

description: "Task list for feature 013h-platform-admin-vaccine-catalog"
---

# Tasks: Platform-Admin Vaccine Catalog Management

**Input**: Design documents from `/specs/013h-platform-admin-vaccine-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative flows per feature (here:
unauthorized-access rejection is the key negative flow, since this is the first cross-tenant
authorization capability in the codebase).

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US3)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/` (see plan.md's Project Structure). No mobile
changes — this feature is director-web only.

---

## Phase 1: Setup

**Purpose**: Nothing to remove or initialize — this feature extends 013g's existing `VaccineType`
entity and `VaccineTypes/` MediatR folder, no new projects (constitution Principle VII). Proceed
directly to Foundational.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `IsPlatformAdmin` flag, JWT claim, `PlatformAdminOnly` policy, `VaccineType`
audit columns, and the `grant-platform-admin` CLI command — every user story depends on all of
these existing first.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 [P] Add `IsPlatformAdmin` (`bool`, default `false`) to `TenantUser` per data-model.md, in `backend/ChildCare.Domain/Entities/TenantUser.cs`
- [X] T002 [P] Add `DeactivatedByUserId (Guid?)`, `DeactivatedByEmail (string?)`, `DeactivatedAt (DateTime?)` to `VaccineType` per data-model.md, in `backend/ChildCare.Domain/Entities/VaccineType.cs`
- [X] T003 Add EF model configuration for `TenantUser.IsPlatformAdmin` to `TenantDbContext` (depends on T001), in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T004 Add EF model configuration for `VaccineType`'s three new columns to `PublicDbContext` — no DB-level FK on `DeactivatedByUserId` (research.md R2) (depends on T002), in `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs`
- [X] T005 Generate the tenant-schema EF Core migration `AddIsPlatformAdminToUsers` (depends on T003), in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [X] T006 Generate the `public`-schema EF Core migration `AddVaccineTypeDeactivationAudit` (depends on T004), in `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/`
- [X] T007 Extend `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`'s revert helper: drop the new `IsPlatformAdmin` column and this migration's name from the `__EFMigrationsHistory` cleanup clause (established pattern — research.md, 012a/013c/006a/013d/013g), depends on T005
- [X] T008 Extend `backend/ChildCare.Api.Tests/VaccineTypes/`'s equivalent public-schema revert helper (or create one, mirroring `LegacyVaccinationMigrationTests.cs`'s pattern) for the new `VaccineType` audit columns, depends on T006
- [X] T009 Add `is_platform_admin` claim to `JwtService.GenerateAccessToken`, present only when `IsPlatformAdmin == true` (research.md R1), in `backend/ChildCare.Api/Services/JwtService.cs` (depends on T001)
- [X] T010 Register `PlatformAdminOnly` authorization policy (`RequireAssertion`: `RequireRole("director")` AND `is_platform_admin` claim present and `"true"`, mirroring `DeviceOrStaffOrDirector`'s shape — research.md R1), in `backend/ChildCare.Api/Program.cs` (depends on T009)
- [X] T011 [P] Create `GrantPlatformAdminCommand` (loop `Ready` tenants, parameterized `UPDATE "{schema}"."Users" SET "IsPlatformAdmin" = true WHERE "Email" = @email`, per-tenant try/catch, summary line — mirrors `MigrateTenantsCommand`/`BackfillGrowthCheckCommand`, research.md R3), in `backend/ChildCare.Api/Cli/GrantPlatformAdminCommand.cs`
- [X] T012 Wire `grant-platform-admin <email>` dispatch into the `args[0]` CLI switch (depends on T011), in `backend/ChildCare.Api/Program.cs`
- [X] T013 [P] Integration test: a director JWT without the flag has no `is_platform_admin` claim; a director JWT with the flag does, in `backend/ChildCare.Api.Tests/PlatformAdmin/JwtClaimTests.cs` (depends on T009)
- [X] T014 [P] Integration test: `GrantPlatformAdminCommand` sets the flag for a matching email in its tenant, leaves non-matching tenants/accounts untouched, and is idempotent on a second run, in `backend/ChildCare.Api.Tests/PlatformAdmin/GrantPlatformAdminCommandTests.cs` (depends on T011)

**Checkpoint**: Schema, claim, and policy exist; solution builds; both migrations are reversible
via the extended rollout tests. User story implementation can now begin.

---

## Phase 3: User Story 1 - Platform admin adds a new catalog entry without touching the database (Priority: P1) 🎯 MVP

**Goal**: A platform-admin-flagged director can view the full catalog (active + inactive, with
audit fields) and create a new entry, visible immediately via 013g's existing read endpoint.

**Independent Test**: Log in as a platform-admin-flagged director, open the management screen,
create a new entry, confirm it appears in the management list and in 013g's `GET
/api/vaccine-types` for any tenant. Confirm a non-flagged director gets 403 on the same endpoints.

### Tests for User Story 1

- [X] T015 [P] [US1] Integration test: `GET /api/platform-admin/vaccine-types` returns all entries (active + inactive) with audit fields; a director without the flag gets `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/ListVaccineTypesForPlatformAdminTests.cs`
- [X] T016 [P] [US1] Integration test: `POST /api/platform-admin/vaccine-types` creates an entry, defaults `sortOrder` to `max+1` and `isActive` to `true`, rejects an empty name with `422` (corrected from the contract's originally-written `400` — this codebase's `ValidationBehavior` pipeline always returns 422 for FluentValidation failures, e.g. `WaitingListEndpoints`; `400` is reserved for domain-level edge cases like the reorder boundary, per T031); the created entry is immediately visible via 013g's `GET /api/vaccine-types`; a director without the flag gets `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/CreateVaccineTypeTests.cs`
- [X] T017 [P] [US1] Regression test: 013g's `GET /api/vaccine-types` response shape, ordering, and `DirectorOnly` auth policy are byte-for-byte unchanged (FR-010), extending `backend/ChildCare.Api.Tests/VaccineTypes/VaccineTypeListTests.cs`

### Implementation for User Story 1

- [X] T018 [US1] `PlatformAdminVaccineTypeResponse` contract per contracts/platform-admin-vaccine-types-api.md, in `backend/ChildCare.Contracts/Responses/PlatformAdminVaccineTypeResponse.cs`
- [X] T019 [US1] `CreateVaccineTypeRequest` contract, in `backend/ChildCare.Contracts/Requests/PlatformAdminVaccineTypeRequests.cs`
- [X] T020 [US1] `ListVaccineTypesForPlatformAdminQuery` + handler (all entries, audit fields included) + mapper, in `backend/ChildCare.Application/VaccineTypes/ListVaccineTypesForPlatformAdminQuery.cs` (depends on T004, T018)
- [X] T021 [US1] `CreateVaccineTypeCommand` + handler + FluentValidation validator (name required, category valid-or-null) per data-model.md, in `backend/ChildCare.Application/VaccineTypes/CreateVaccineTypeCommand.cs` (depends on T004, T019)
- [X] T022 [US1] `PlatformAdminVaccineTypeEndpoints.cs`: map `GET /api/platform-admin/vaccine-types` and `POST /api/platform-admin/vaccine-types`, both `.RequireAuthorization("PlatformAdminOnly")`, in `backend/ChildCare.Api/Endpoints/PlatformAdminVaccineTypeEndpoints.cs` (depends on T010, T020, T021)
- [X] T023 [US1] Register `PlatformAdminVaccineTypeEndpoints` in the endpoint-mapping startup code, in `backend/ChildCare.Api/Program.cs` (depends on T022)
- [ ] T024 [US1] Regenerate `web/lib/generated/api-types.ts` from the updated OpenAPI spec (depends on T023) — deferred to a single regeneration pass after all US1-3 endpoints exist (T024/T036/T046 produce an identical result either way)
- [ ] T025 [P] [US1] `VaccineTypeManagementTable.tsx` (list view: name, category, sortOrder, active/inactive, audit fields when inactive) in `web/components/platform-admin/VaccineTypeManagementTable.tsx` (depends on T024)
- [ ] T026 [US1] Create-entry form (name + category) within the management screen, in `web/components/platform-admin/VaccineTypeManagementTable.tsx` (depends on T025)
- [ ] T027 [US1] `web/app/(app)/platform-admin/vaccine-types/page.tsx` — gated route rendering the management table, redirecting/404-ing if the authenticated director lacks the flag (depends on T025)
- [ ] T028 [US1] Conditional platform-admin nav entry in the director-web sidebar, shown only when the authenticated director's token carries `is_platform_admin`, in `web/components/layout/Sidebar.tsx` (depends on T027)
- [ ] T029 [US1] NL/FR/EN locale keys for all new platform-admin screen strings (constitution Principle IV), in `web/messages/*.json`

**Checkpoint**: A platform admin can view and create catalog entries end-to-end; a non-flagged
director is denied at both the route and API level; 013g's existing read endpoint is unaffected.

---

## Phase 4: User Story 2 - Platform admin fixes a typo or reorders the catalog (Priority: P2)

**Goal**: A platform admin can rename/re-categorize an entry and reorder entries via up/down
buttons (research.md R4), both reflected immediately in 013g's read endpoint.

**Independent Test**: Rename an entry, confirm the new name via 013g's `GET /api/vaccine-types`;
reorder two entries via the up/down buttons, confirm the new order via the same endpoint.

### Tests for User Story 2

- [X] T030 [P] [US2] Integration test: `PATCH /api/platform-admin/vaccine-types/{id}` renames/re-categorizes and the new name/category is immediately visible via 013g's `GET /api/vaccine-types` (FR-011); existing `VaccineRecord`s that reference this entry keep their own originally-saved name text unchanged (013g FR-010, re-verified here); unknown id → `404`; director without flag → `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/UpdateVaccineTypeTests.cs`
- [X] T031 [P] [US2] Integration test: `POST /api/platform-admin/vaccine-types/{id}/reorder` swaps adjacent `sortOrder` values, with the new order immediately visible via 013g's `GET /api/vaccine-types` (FR-011); `"up"` on the first entry and `"down"` on the last both return `400`; unknown id → `404`; director without flag → `403` (FR-009), in `backend/ChildCare.Api.Tests/PlatformAdmin/ReorderVaccineTypeTests.cs`

### Implementation for User Story 2

- [X] T032 [P] [US2] `UpdateVaccineTypeRequest`/`ReorderVaccineTypeRequest` contracts, in `backend/ChildCare.Contracts/Requests/PlatformAdminVaccineTypeRequests.cs`
- [X] T033 [US2] `UpdateVaccineTypeCommand` + handler + validator, in `backend/ChildCare.Application/VaccineTypes/UpdateVaccineTypeCommand.cs` (depends on T004, T032)
- [X] T034 [US2] `ReorderVaccineTypeCommand` + handler (adjacent-swap logic, boundary validation per contracts.md; scoped to the entry's own category, matching the list's Category-then-SortOrder grouping), in `backend/ChildCare.Application/VaccineTypes/ReorderVaccineTypeCommand.cs` (depends on T004, T032)
- [X] T035 [US2] Map `PATCH /api/platform-admin/vaccine-types/{id}` and `POST /api/platform-admin/vaccine-types/{id}/reorder` in `PlatformAdminVaccineTypeEndpoints.cs`, both `.RequireAuthorization("PlatformAdminOnly")` (FR-009 — same policy as T022's routes, restated here since it must independently apply per-endpoint) (depends on T022, T033, T034)
- [ ] T036 [US2] Regenerate `web/lib/generated/api-types.ts` (depends on T035) — deferred to a single regeneration pass after all US1-3 endpoints exist
- [ ] T037 [US2] Add rename (inline edit) to `VaccineTypeManagementTable.tsx` (depends on T025, T036)
- [ ] T038 [US2] Add up/down reorder buttons to `VaccineTypeManagementTable.tsx`, reusing `WaitingListTable.tsx`'s existing button pattern (research.md R4) (depends on T037)
- [ ] T039 [US2] NL/FR/EN locale keys for rename/reorder UI strings

**Checkpoint**: Rename and reorder work end-to-end alongside User Story 1's create/list, all still
reflected correctly in 013g's unaffected read endpoint.

---

## Phase 5: User Story 3 - Platform admin deactivates a discontinued entry, with accountability (Priority: P2)

**Goal**: A platform admin can deactivate/reactivate an entry; deactivation records who and when;
013g's active-only tenant-facing behavior is unaffected.

**Independent Test**: Deactivate an entry as one platform-admin account; confirm it disappears
from 013g's active-only tenant view but remains in the management list marked inactive with
who/when; reactivate it and confirm the audit fields clear.

### Tests for User Story 3

- [X] T040 [P] [US3] Integration test: `POST /api/platform-admin/vaccine-types/{id}/deactivate` sets `IsActive=false` and populates `DeactivatedByUserId`/`Email`/`At` from the caller; the entry no longer appears in 013g's `GET /api/vaccine-types` active-only response (FR-011, US3 Independent Test); is a no-op (unchanged audit fields) if already inactive; director without flag → `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/DeactivateVaccineTypeTests.cs`
- [X] T041 [P] [US3] Integration test: `POST /api/platform-admin/vaccine-types/{id}/reactivate` sets `IsActive=true`, clears all three audit fields, and the entry reappears in 013g's `GET /api/vaccine-types` (FR-011); a subsequent deactivate populates fresh audit fields (spec.md FR-008); director without flag → `403` (FR-009), in `backend/ChildCare.Api.Tests/PlatformAdmin/ReactivateVaccineTypeTests.cs`
- [X] T042 [P] [US3] Integration test: an existing `VaccineRecord` referencing a now-deactivated entry is completely unaffected — same read paths, same fields, no error (013g FR-010, re-verified here), extending `backend/ChildCare.Api.Tests/VaccineRecords/VaccineTypeReferenceTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] `DeactivateVaccineTypeCommand` + handler (reads acting user's id/email from claims, no-op guard) per data-model.md's state-transition rules, in `backend/ChildCare.Application/VaccineTypes/DeactivateVaccineTypeCommand.cs` (depends on T004)
- [X] T044 [US3] `ReactivateVaccineTypeCommand` + handler (clears audit fields, no-op guard), in `backend/ChildCare.Application/VaccineTypes/ReactivateVaccineTypeCommand.cs` (depends on T004)
- [X] T045 [US3] Map `POST /api/platform-admin/vaccine-types/{id}/deactivate` and `.../reactivate` in `PlatformAdminVaccineTypeEndpoints.cs`, both `.RequireAuthorization("PlatformAdminOnly")` (FR-009 — same policy as T022's routes, restated here since it must independently apply per-endpoint) (depends on T022, T043, T044)
- [ ] T046 [US3] Regenerate `web/lib/generated/api-types.ts` (depends on T045) — deferred to a single regeneration pass after all US1-3 endpoints exist
- [ ] T047 [US3] Add deactivate/reactivate action + inactive-state audit display (who/when) to `VaccineTypeManagementTable.tsx` (depends on T025, T046)
- [ ] T048 [US3] NL/FR/EN locale keys for deactivate/reactivate UI strings and audit display

**Checkpoint**: All four actions (create/rename/reorder/deactivate+reactivate) work end-to-end;
013g's tenant-facing read endpoint remains fully unaffected throughout.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Rollout of the flag itself, and final validation across all three user stories.

- [ ] T049 Run `dotnet run --project ChildCare.Api -- grant-platform-admin dgormez@gmail.com` against the target environment(s) so the feature is usable immediately after merge (spec.md Assumptions, research.md R3)
- [ ] T050 [P] Run quickstart.md's full validation sequence end-to-end (create → verify via 013g read endpoint → rename → reorder → deactivate → reactivate → non-admin denial → 013g regression suite)
- [ ] T051 [P] Design-compliance pass on `VaccineTypeManagementTable.tsx`/the new route against design-system.md (spacing scale, no nested cards, shared component reuse) and platform-rules.md (director-web density/keyboard-focus requirements)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No-op, proceed directly to Foundational.
- **Foundational (Phase 2)**: BLOCKS all user stories — flag, claim, policy, audit columns, and
  the grant command must all exist first, since every endpoint in every user story is gated by
  `PlatformAdminOnly` and every audit-bearing action needs the new `VaccineType` columns.
- **User Stories (Phase 3-5)**: All depend on Foundational. US1 delivers the MVP (view + create);
  US2 and US3 both build on US1's `VaccineTypeManagementTable.tsx` and endpoint file but touch
  disjoint handler/action code, so they can proceed in parallel once US1's table component exists.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Foundational only. No dependency on US2/US3.
- **User Story 2 (P2)**: Foundational + reuses US1's `VaccineTypeManagementTable.tsx` (extends it
  in place) and `PlatformAdminVaccineTypeEndpoints.cs` (adds routes to the same file) — not a
  logical dependency on US1's *behavior*, just shared files, so US1 should land first in practice
  to avoid file-conflict churn.
- **User Story 3 (P2)**: Same shared-file relationship to US1 as US2.

### Parallel Opportunities

- T001/T002 (entity changes) in parallel.
- T013/T014 (Foundational tests) in parallel once their respective implementation tasks land.
- T015/T016/T017 (US1 tests) in parallel.
- T030/T031 (US2 tests) in parallel.
- T040/T041/T042 (US3 tests) in parallel.
- T050/T051 (Polish) in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (no-op) + Phase 2 (Foundational).
2. Complete Phase 3 (User Story 1) — a platform admin can view and create catalog entries.
3. **STOP and VALIDATE**: run T015-T017's tests plus quickstart.md's create/view/013g-regression
   steps independently.
4. This alone already closes the core gap (spec.md's "no direct database access" goal) even
   without rename/reorder/deactivate.

### Incremental Delivery

1. Foundational → flag/claim/policy/audit-columns/grant-command ready.
2. US1 → view + create, MVP, independently testable.
3. US2 → rename + reorder, independently testable, no regression to US1.
4. US3 → deactivate + reactivate + audit trail, independently testable, no regression to US1/US2.
5. Polish → grant the flag to the real operator account, full quickstart validation, design
   compliance.

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Integration test for GET /api/platform-admin/vaccine-types in backend/ChildCare.Api.Tests/PlatformAdmin/ListVaccineTypesForPlatformAdminTests.cs"
Task: "Integration test for POST /api/platform-admin/vaccine-types in backend/ChildCare.Api.Tests/PlatformAdmin/CreateVaccineTypeTests.cs"
Task: "Regression test for GET /api/vaccine-types unchanged in backend/ChildCare.Api.Tests/VaccineTypes/VaccineTypeListTests.cs"
```

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- Every write endpoint in this feature is gated by `PlatformAdminOnly` (Phase 2) — there is no
  user story that can skip that dependency.
- Commit after each task or logical group.
- Stop at any checkpoint to validate story independently.
