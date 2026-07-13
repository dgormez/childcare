---

description: "Task list for feature 013g-vaccine-catalog"
---

# Tasks: Vaccine Catalog & Attachments

**Input**: Design documents from `/specs/013g-vaccine-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative/regulatory flows per feature.

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

**Purpose**: Nothing to remove or initialize — this feature extends the existing 013c
`VaccineRecord` aggregate and `PublicDbContext`/`TenantDbContext`, no new projects (constitution
Principle VII). Proceed directly to Foundational.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New entities, both new tables (public + tenant schema), the extended
`VaccineRecord` columns, and the storage-port change every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 [P] Create `VaccineType` entity per data-model.md in `backend/ChildCare.Domain/Entities/VaccineType.cs`
- [X] T002 [P] Create `TenantCustomVaccineEntry` entity per data-model.md in `backend/ChildCare.Domain/Entities/TenantCustomVaccineEntry.cs`
- [X] T003 [P] Extend `VaccineRecord` entity: add nullable `VaccineTypeId`, `CustomVaccineEntryId`, `AttachmentObjectPath` per data-model.md, in `backend/ChildCare.Domain/Entities/VaccineRecord.cs`
- [X] T004 Add `DbSet<VaccineType> VaccineTypes` to `IPublicDbContext` and `PublicDbContext`, with EF model configuration (table `vaccine_types`, index on `(category, sort_order)`, partial index on `is_active`) in `backend/ChildCare.Application/Common/IPublicDbContext.cs` and `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs` (depends on T001)
- [X] T005 Generate the `public`-schema EF Core migration `AddVaccineTypeCatalog`: create `vaccine_types`, `InsertData` the ~9 seed rows from data-model.md, in `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/` (depends on T004)
- [X] T006 Add `DbSet<TenantCustomVaccineEntry> TenantCustomVaccineEntries` to `ITenantDbContext` and `TenantDbContext`, with EF model configuration (table `tenant_custom_vaccine_entries`, unique index on `normalized_name`) in `backend/ChildCare.Application/Common/ITenantDbContext.cs` and `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T002)
- [X] T007 Add EF model configuration for `VaccineRecord`'s three new columns to `TenantDbContext`: index on `VaccineTypeId` (no DB FK, research.md R2), same-schema FK `CustomVaccineEntryId` → `tenant_custom_vaccine_entries(id)` with no cascade delete (depends on T003, T006)
- [X] T008 Generate the tenant-schema EF Core migration `AddVaccineCatalogAndAttachments`: create `tenant_custom_vaccine_entries`, add the three new columns + FK to `vaccine_records`, plus a `CHECK ("VaccineTypeId" IS NULL OR "CustomVaccineEntryId" IS NULL)` constraint enforcing spec.md FR-004's mutual-exclusivity rule at the database level (not application-layer-only), in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T007)
- [X] T009 Extend `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`'s `RevertToPreExtensionSchemaAsync`: add `DROP TABLE "tenant_custom_vaccine_entries"` and `ALTER TABLE "vaccine_records" DROP COLUMN` for the three new columns, plus this migration's name to the `__EFMigrationsHistory` cleanup clause (research.md R8) (depends on T008)
- [X] T010 Extend `backend/ChildCare.Api.Tests/VaccineRecords/LegacyVaccinationMigrationTests.cs`'s revert helper with the same additions as T009, for the same reason (research.md R8) (depends on T008)
- [X] T011 Extend `IHealthAttachmentStorage.CreateUploadUrlAsync` with an optional `category` parameter (default `"health-records"`, preserving existing call sites), update `GcsHealthAttachmentStorage` to use it in the object path, and update `FakeHealthAttachmentStorage` test double to match (research.md R4), in `backend/ChildCare.Application/Common/IHealthAttachmentStorage.cs`, `backend/ChildCare.Infrastructure/Storage/GcsHealthAttachmentStorage.cs`, `backend/ChildCare.Api.Tests/FakeHealthAttachmentStorage.cs`

**Checkpoint**: Schema and entities exist; solution builds; both migrations are reversible via
the extended rollout tests. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director picks a vaccine from a shared catalog instead of free-typing (Priority: P1) 🎯 MVP

**Goal**: A director sees a searchable, category-grouped catalog when recording a vaccine;
picking an entry auto-fills the name and stores the reference.

**Independent Test**: Open the vaccine record form, see catalog entries grouped by category,
select one, confirm the name field auto-fills and the saved record references the catalog entry;
deactivate that entry and confirm the existing record still displays correctly.

### Tests for User Story 1

- [X] T012 [P] [US1] Integration test: `GET /api/vaccine-types` returns seeded entries, active-only, grouped/sorted by `category` then `sortOrder`, in `backend/ChildCare.Api.Tests/VaccineTypes/VaccineTypeListTests.cs`
- [X] T013 [P] [US1] Integration test: creating a vaccine record with a valid `vaccineTypeId` stores the reference; deactivating that catalog entry afterward leaves the existing record's `vaccineName` rendering unaffected (quickstart.md Scenario 1), in `backend/ChildCare.Api.Tests/VaccineRecords/VaccineTypeReferenceTests.cs`
- [X] T014 [P] [US1] Integration test: `vaccineTypeId` pointing at a nonexistent id returns `422 errors.vaccine_records.vaccine_type_not_found`, extending `backend/ChildCare.Api.Tests/VaccineRecords/VaccineRecordValidationTests.cs`

### Implementation for User Story 1

- [X] T015 [US1] `VaccineTypeResponse` contract in `backend/ChildCare.Contracts/Responses/VaccineTypeResponse.cs`
- [X] T016 [US1] `ListVaccineTypesQuery` + handler (active-only, ordered `category`/`sortOrder`) + `VaccineTypeMapper` in `backend/ChildCare.Application/VaccineTypes/` (depends on T004)
- [X] T017 [US1] `VaccineTypeEndpoints.cs`: `GET /api/vaccine-types` under `DirectorOnly`, mapped in `backend/ChildCare.Api/Program.cs` (depends on T016)
- [X] T018 [US1] Add nullable `VaccineTypeId` to `CreateVaccineRecordRequest`/`UpdateVaccineRecordRequest` in `backend/ChildCare.Contracts/Requests/VaccineRecordRequests.cs`
- [X] T019 [US1] Add `VaccineTypeId` to `VaccineRecordResponse` in `backend/ChildCare.Contracts/Responses/VaccineRecordResponse.cs`
- [X] T020 [US1] Extend `CreateVaccineRecordCommand`/handler/validator: accept `vaccineTypeId`, validate it resolves to some `vaccine_types` row (active or not) else `422 vaccine_type_not_found`, set `VaccineTypeId` on the entity, in `backend/ChildCare.Application/VaccineRecords/CreateVaccineRecordCommand.cs` (depends on T004, T018)
- [X] T021 [US1] Same extension for `UpdateVaccineRecordCommand`, in `backend/ChildCare.Application/VaccineRecords/UpdateVaccineRecordCommand.cs` (depends on T004, T018)
- [X] T022 [US1] Update `VaccineRecordMapper` to include `VaccineTypeId` in the response, in `backend/ChildCare.Application/VaccineRecords/VaccineRecordMapper.cs` (depends on T019)
- [X] T023 [US1] Wire `vaccineTypeId` through `VaccineRecordEndpoints.cs` create/update request mapping (depends on T020, T021)
- [X] T024 [US1] Build `VaccineNameCombobox.tsx` (research.md R5): plain `<input>` + `role="listbox"` dropdown, arrow-key navigation, `aria-activedescendant`, entries grouped by category, in `web/components/health/VaccineNameCombobox.tsx`
- [X] T025 [US1] Fetch `/api/vaccine-types` in `web/app/(app)/children/[id]/page.tsx` and wire into `VaccineRecordForm.tsx` via the new combobox, replacing the plain `vaccineName` `Input` (depends on T024, T017)
- [X] T026 [US1] Add NL/FR/EN i18n keys for combobox labels and the two category values (`basisvaccinatieschema`/`aanbevolen_niet_gratis`, research.md R6)
- [X] T027 [P] [US1] Web component test: `VaccineNameCombobox` renders catalog groups, filters on keystroke, is keyboard-selectable, in `web/components/health/VaccineNameCombobox.test.tsx`
- [X] T028 [P] [US1] Regenerate `web/lib/generated/api-types.ts` and commit the diff

**Checkpoint**: User Story 1 is independently functional and testable (quickstart.md Scenario 1).

---

## Phase 4: User Story 2 - Director's custom vaccine entry is remembered for next time (Priority: P1)

**Goal**: A typed, non-catalog vaccine name is remembered per-tenant and offered as a picker
option for every subsequent record at that same KDV, deduped case/whitespace-insensitively.

**Independent Test**: Type a vaccine name matching no catalog entry, save, then open the form for
a different child in the same KDV and confirm that name is now selectable under "Other (used
before)"; confirm a different tenant never sees it.

### Tests for User Story 2

- [X] T029 [P] [US2] Integration test: a typed non-catalog name creates a remembered entry scoped to the tenant, and a second record for a different child reuses the same entry (quickstart.md Scenario 2, steps 1-3), in `backend/ChildCare.Api.Tests/VaccineRecords/CustomVaccineEntryTests.cs`
- [X] T030 [P] [US2] Integration test: two near-simultaneous writes with case/whitespace/diacritic-different spellings of the same name (e.g. "Rabiës" vs "rabies ") resolve to a single `tenant_custom_vaccine_entries` row — exercises the unique-index dedupe under a race, not just sequential dedupe (research.md R3), in the same file
- [X] T031 [P] [US2] Integration test: a custom entry created in one tenant is never returned by `GET /api/vaccine-custom-entries` for a different tenant (quickstart.md Scenario 2, step 4), in the same file

### Implementation for User Story 2

- [X] T032 [US2] `CustomVaccineEntryResponse` contract in `backend/ChildCare.Contracts/Responses/CustomVaccineEntryResponse.cs`
- [X] T033 [US2] `ListTenantCustomVaccineEntriesQuery` + handler (ordered alphabetically by `name`) + mapper in `backend/ChildCare.Application/VaccineCustomEntries/` (depends on T006)
- [X] T034 [US2] `GET /api/vaccine-custom-entries` endpoint under `DirectorOnly`, added to `backend/ChildCare.Api/Endpoints/VaccineTypeEndpoints.cs` (depends on T033)
- [X] T035 [US2] Implement the resolution helper (research.md R3): given a typed `vaccineName` and null `vaccineTypeId`, normalize (case-fold + trim + strip diacritics via Unicode NFKD normalization, per spec.md FR-007), `INSERT ... ON CONFLICT (normalized_name) DO NOTHING` then re-select the `tenant_custom_vaccine_entries` row, returning its id — a small shared helper reused by both create/update handlers, in `backend/ChildCare.Application/VaccineRecords/CustomVaccineEntryResolver.cs` (depends on T006)
- [X] T036 [US2] Wire the resolver into `CreateVaccineRecordCommand`/`UpdateVaccineRecordCommand`: when `vaccineTypeId` is null, resolve/create a custom entry and set `CustomVaccineEntryId` (mutually exclusive with `VaccineTypeId`, research.md R3), in the same two handler files (depends on T035, T020, T021)
- [X] T037 [US2] Fetch `/api/vaccine-custom-entries` in `web/app/(app)/children/[id]/page.tsx` and pass into `VaccineNameCombobox` as a separate "Other (used before)" group, with an "add new" affordance for unmatched typed text (depends on T024, T034)
- [X] T038 [US2] Add NL/FR/EN i18n keys for the "Other (used before)" group label and "add custom" affordance
- [X] T039 [P] [US2] Web component test: `VaccineNameCombobox` shows the custom-entries group separately from catalog groups and supports the "add new" flow, extending `VaccineNameCombobox.test.tsx`
- [X] T040 [P] [US2] Regenerate `web/lib/generated/api-types.ts` and commit the diff

**Checkpoint**: User Story 2 is independently functional and testable (quickstart.md Scenario 2).

---

## Phase 5: User Story 3 - Director attaches a photo of the paper vaccination booklet (Priority: P2)

**Goal**: A director can attach a photo/scan to a saved vaccine record without it ever blocking
the record's own save.

**Independent Test**: Save a vaccine record, separately upload an attachment, confirm it's
retrievable via a signed URL; confirm a failed upload never affects the already-saved record.

### Tests for User Story 3

- [X] T041 [P] [US3] Integration test: attachment-upload-url + successful PUT sets `attachmentDownloadUrl` on a subsequent `GET` (quickstart.md Scenario 3, steps 1-4), in `backend/ChildCare.Api.Tests/VaccineRecords/VaccineRecordAttachmentTests.cs`
- [X] T042 [P] [US3] Integration test: unsupported content-type / oversized file rejected with `422`, and the vaccine record itself remains unaffected (quickstart.md Scenario 3, step 5), in the same file
- [X] T042a [P] [US3] Regression test proving FR-014 decoupling: the caregiver-facing health-summary read path (`GetChildHealthSummaryQuery`, 013c) returns the same shape/content for a vaccine record regardless of whether it carries a `VaccineTypeId`/`CustomVaccineEntryId`/attachment — no picker or attachment-upload capability is exposed through that path (mirrors feature 012's `RoomShift`/`StaffSchedule` decoupling-test precedent), in `backend/ChildCare.Api.Tests/Children/ChildHealthSummaryUnaffectedByVaccineCatalogTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] `CreateVaccineRecordAttachmentUploadUrlCommand` + handler, calling `IHealthAttachmentStorage` with `category: "vaccine-records"` (research.md R4), in `backend/ChildCare.Application/VaccineRecords/CreateVaccineRecordAttachmentUploadUrlCommand.cs` (depends on T011)
- [X] T044 [US3] Add `attachmentDownloadUrl` to `VaccineRecordResponse` and `VaccineRecordMapper` (freshly-signed per read via `IHealthAttachmentStorage`, never the raw object path) (depends on T019, T022)
- [X] T045 [US3] `POST /api/children/{childId}/vaccine-records/{id}/attachment-upload-url` endpoint under `DirectorOnly` in `backend/ChildCare.Api/Endpoints/VaccineRecordEndpoints.cs` (depends on T043)
- [X] T046 [US3] Wire `HealthRecordAttachmentControl.tsx` (confirmed generic/reusable as-is, plan.md) into `VaccineRecordForm.tsx`'s edit view, calling the new upload-url endpoint, in `web/app/(app)/children/[id]/page.tsx` and `web/components/health/VaccineRecordForm.tsx` (depends on T045)
- [X] T047 [P] [US3] Regenerate `web/lib/generated/api-types.ts` and commit the diff

**Checkpoint**: All three user stories are independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: i18n sweep, workflow documentation, and end-to-end validation.

- [X] T048 [P] Full i18n sweep: confirm every new string (combobox, category labels, "used before" group, attachment-control reuse) has NL/FR/EN keys with no hardcoded text (constitution Principle IV)
- [X] T049 [P] Update `.specify/memory/Workflows/health-safety.md`'s "Flow — vaccination & health record tracking" section to describe the catalog/custom-entry/attachment additions (workflows.md governance rules — document what changed, why, which features affected)
- [X] T050 Run quickstart.md Scenarios 1-4 end-to-end against a local TestContainers/dev backend (Scenario 4 is a manual browser check)
- [X] T051 Full backend + web test suite run; fix any regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Empty — no dependencies, nothing to remove.
- **Foundational (Phase 2)**: No dependencies beyond Setup — BLOCKS all user stories (schema must
  exist before any handler can reference the new columns/tables).
- **User Stories (Phase 3-5)**: All depend on Foundational completion.
  - US1 and US2 both extend the same create/update handlers and the same `VaccineNameCombobox`
    component, so in practice US2's implementation tasks (T035-T040) build directly on US1's
    (T020-T025) rather than being fully parallel — but each story's own *test* task can be
    written and validated independently once its slice of the handler exists.
  - US3 (attachment) is independent of US1/US2's catalog/custom-entry logic — it only needs
    Foundational's `AttachmentObjectPath` column and the extended `IHealthAttachmentStorage`
    (T003, T011), not any catalog/custom-entry code. It can be implemented in parallel with US1/US2
    by a different developer.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- Foundational entity tasks (T001-T003) marked [P] run in parallel — different files.
- Within each user story, test tasks marked [P] run in parallel with each other.
- US3 (attachment) can be implemented in parallel with US1/US2 (catalog/custom-entry) once
  Foundational is complete — different files, no shared handler logic beyond the same two
  command files, which is a sequencing concern only if the same developer touches both on the
  same day.

---

## Parallel Example: User Story 1

```bash
Task: "Integration test: GET /api/vaccine-types returns seeded entries active-only grouped/sorted"
Task: "Integration test: creating a vaccine record with a valid vaccineTypeId stores the reference"
Task: "Integration test: vaccineTypeId pointing at a nonexistent id returns 422"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (schema, migrations, entities, storage-port change).
2. Complete Phase 3: User Story 1 (catalog picker).
3. **STOP and VALIDATE**: quickstart.md Scenario 1, independently.
4. Deploy/demo if ready — this alone eliminates the free-text-spelling problem feeding 013c's
   due-soon reminder, even before custom-entry memory or attachments ship.

### Incremental Delivery

1. Foundational → foundation ready.
2. US1 (catalog picker) → validate → demo.
3. US2 (remembered custom entries) → validate → demo.
4. US3 (attachment) → validate → demo.
5. Polish (i18n sweep, workflow doc, full suite run).

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- Commit after each task or logical group.
- T009/T010 (migration-revert-test extensions) are a recurring maintenance point every
  migration-adding feature since 003 has hit (research.md R8) — do not defer past this feature's
  own PR, per the standing rule that findings get fixed, not logged as debt.

---

## Phase 7: Convergence

- [X] T052 Add a new BACKLOG.md entry (`013h-platform-admin-vaccine-catalog`) for the
  platform-admin catalog-management surface deferred out of this feature, per spec.md
  Assumptions' explicit commitment (missing) — resolved immediately rather than deferred,
  same standing rule as every prior finding in this feature.
