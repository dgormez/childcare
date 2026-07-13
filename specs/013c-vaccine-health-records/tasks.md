---

description: "Task list for feature 013c-vaccine-health-records"
---

# Tasks: Vaccine & Health Records

**Input**: Design documents from `/specs/013c-vaccine-health-records/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/,  quickstart.md

**Tests**: Included — constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative/regulatory flows per feature.

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/`, `mobile/` (see plan.md's Project Structure).

---

## Phase 1: Setup

**Purpose**: Nothing new to initialize — this feature adds to the existing five-project backend
solution and existing `web`/`mobile` apps (constitution Principle VII, no new projects). This
phase only covers removing the superseded legacy code (research.md R1) so Foundational work
starts from a clean slate.

- [X] T001 [P] Delete `backend/ChildCare.Domain/Entities/VaccinationRecord.cs`
- [X] T002 [P] Delete `backend/ChildCare.Application/Groups/RecordVaccinationCommand.cs` and `backend/ChildCare.Application/Groups/ListChildVaccinationsQuery.cs`
- [X] T003 [P] Remove `VaccinationResponse` from `backend/ChildCare.Contracts/Responses/GroupResponse.cs` and `RecordVaccinationRequest` from `backend/ChildCare.Contracts/Requests/ChildRequests.cs`
- [X] T004 Remove `VaccinationResult` from `backend/ChildCare.Application/Groups/GroupResult.cs` and `GroupMapper.ToVaccinationResponse` from `backend/ChildCare.Application/Groups/GroupMapper.cs` (depends on T002)
- [X] T005 Remove the `/api/children/{childId}/vaccinations` route group from `backend/ChildCare.Api/Endpoints/GroupsEndpoints.cs` (depends on T004)
- [X] T006 [P] Delete `backend/ChildCare.Api.Tests/ChildVaccinationTests.cs`

**Checkpoint**: Legacy vaccination code fully removed; solution still builds (new tables/entities
not yet added, so `ITenantDbContext.VaccinationRecords` references must be cleared too — see T007).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New entities, DB schema, and migration that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 Remove `DbSet<VaccinationRecord> VaccinationRecords` from `backend/ChildCare.Application/Common/ITenantDbContext.cs` and its implementation + model config from `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T001)
- [X] T008 [P] Create `HealthRecordType` enum (`allergy`, `chronic_condition`, `medication_standing`, `doctor_note`, `other`) in `backend/ChildCare.Domain/Enums/HealthRecordType.cs`
- [X] T009 [P] Create `HealthRecordTypeExtensions` (explicit wire-string mapping, mirrors `ChildEventTypeExtensions` per research.md/data-model.md — default `ToString().ToLowerInvariant()` would drop underscores for `medication_standing`/`chronic_condition`) in `backend/ChildCare.Domain/Enums/HealthRecordTypeExtensions.cs`
- [X] T010 [P] Create `VaccineRecord` entity per data-model.md in `backend/ChildCare.Domain/Entities/VaccineRecord.cs`
- [X] T011 [P] Create `HealthRecord` entity per data-model.md in `backend/ChildCare.Domain/Entities/HealthRecord.cs`
- [X] T012 Add `DbSet<VaccineRecord> VaccineRecords` and `DbSet<HealthRecord> HealthRecords` to `ITenantDbContext` and `TenantDbContext`, with EF model configuration (FKs to `children`/`users`, `record_type` enum conversion via T009, indexes per data-model.md: `vaccine_records(child_id)`, partial `vaccine_records(next_due_date) WHERE deleted_at IS NULL`, `health_records(child_id)`) (depends on T007, T008, T009, T010, T011)
- [X] T013 Generate the EF Core migration: create `vaccine_records` + `health_records`, copy existing `vaccination_records` rows into `vaccine_records` (mapping `child_id`/`vaccine_name`/`date_administered`→`administered_on`/`next_due_date`, `recorded_by` left null), then drop `vaccination_records`, in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T012)
- [X] T014 Extend `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`'s `RevertToPreExtensionSchemaAsync`: remove the obsolete `vaccination_records` drop line, add `DROP TABLE` lines for `vaccine_records`/`health_records` (before `children` in drop order) and this feature's migration name(s) to the `__EFMigrationsHistory` cleanup clause, per research.md R6 (depends on T013)
- [X] T015 [P] Integration test: migrating a schema seeded with a legacy `vaccination_records` row correctly backfills it into `vaccine_records` and drops the old table (quickstart.md Scenario 6), in `backend/ChildCare.Api.Tests/VaccineRecords/LegacyVaccinationMigrationTests.cs` (depends on T013)

**Checkpoint**: Schema and entities exist; solution builds; migration is reversible via the
extended rollout test. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director records a vaccination (Priority: P1) 🎯 MVP

**Goal**: Director can add/edit/delete a child's vaccine records and see them listed on a new
Gezondheid tab.

**Independent Test**: Open a child's Gezondheid tab, add a vaccine record with a future
`nextDueDate`, confirm it appears in history most-recent-first; edit and soft-delete it.

### Tests for User Story 1

- [X] T016 [P] [US1] Integration test: create/list/update/delete vaccine record happy path, in `backend/ChildCare.Api.Tests/VaccineRecords/VaccineRecordCrudTests.cs`
- [X] T017 [P] [US1] Integration test: reject `administeredOn` in the future and missing `vaccineName` (422 with correct error keys), in `backend/ChildCare.Api.Tests/VaccineRecords/VaccineRecordValidationTests.cs`
- [X] T018 [P] [US1] Integration test: `childId` not in tenant returns 404, in `backend/ChildCare.Api.Tests/VaccineRecords/VaccineRecordNotFoundTests.cs`

### Implementation for User Story 1

- [X] T019 [P] [US1] `VaccineRecordResponse` in `backend/ChildCare.Contracts/Responses/VaccineRecordResponse.cs`
- [X] T020 [P] [US1] `CreateVaccineRecordRequest`/`UpdateVaccineRecordRequest` in `backend/ChildCare.Contracts/Requests/VaccineRecordRequests.cs`
- [X] T021 [US1] `VaccineRecordResult`/`VaccineRecordFailure` wrapper in `backend/ChildCare.Application/VaccineRecords/VaccineRecordResult.cs`
- [X] T022 [US1] `VaccineRecordMapper.ToResponse` in `backend/ChildCare.Application/VaccineRecords/VaccineRecordMapper.cs` (depends on T019)
- [X] T023 [US1] `CreateVaccineRecordCommand` + validator + handler (vaccine name required ≤200 chars, administeredOn required not-in-future, doseNumber ≥1 if present, recordedBy from caller JWT) in `backend/ChildCare.Application/VaccineRecords/CreateVaccineRecordCommand.cs` (depends on T021, T022)
- [X] T024 [US1] `UpdateVaccineRecordCommand` + validator + handler in `backend/ChildCare.Application/VaccineRecords/UpdateVaccineRecordCommand.cs` (depends on T021, T022)
- [X] T025 [US1] `DeleteVaccineRecordCommand` + handler (soft-delete, sets `deleted_at`) in `backend/ChildCare.Application/VaccineRecords/DeleteVaccineRecordCommand.cs` (depends on T021)
- [X] T026 [US1] `ListChildVaccineRecordsQuery` + handler (excludes soft-deleted, sorted `administeredOn` descending) in `backend/ChildCare.Application/VaccineRecords/ListChildVaccineRecordsQuery.cs` (depends on T022)
- [X] T027 [US1] `VaccineRecordEndpoints.cs` mapping POST/GET/PUT/DELETE under `/api/children/{childId}/vaccine-records`, `DirectorOnly`, registered in `backend/ChildCare.Api/Program.cs` (depends on T023, T024, T025, T026)
- [X] T028 [US1] Add NL/FR/EN i18n keys for all `errors.vaccine_records.*` validation messages (constitution Principle IV) in the backend's locale resource files
- [X] T029 [US1] New minimal child-detail screen shell — a name header plus the Gezondheid tab only, no other tabs (per spec.md Assumptions — director web has no per-child detail screen yet, and this feature scopes only what it needs) — in `web/app/(app)/children/[id]/layout.tsx`, replacing the inert `NotYetAvailable` placeholder for `/children/[id]` routes
- [X] T030 [US1] "Gezondheid" tab route `web/app/(app)/children/[id]/health/page.tsx` with a Vaccines section (list + add/edit form), calling the new endpoints via the regenerated openapi-fetch client
- [X] T031 [US1] `VaccineRecordForm` component in `web/components/health/VaccineRecordForm.tsx`, following design-system.md forms (surface-soft fill, 8px radius) and platform-rules.md director-web density
- [X] T032 [US1] Empty state (icon + short sentence, design-system.md) for the Vaccines section when a child has no records
- [X] T033 [US1] Regenerate `web/lib/generated/api-types.ts` against the running backend and commit the diff (per 007a's established convention — this does not happen automatically)
- [X] T034 [P] [US1] Web component test: add/edit/delete a vaccine record via the Gezondheid tab form, in `web/components/health/VaccineRecordForm.test.tsx`

**Checkpoint**: A director can fully manage vaccine records end-to-end via the web admin.

---

## Phase 4: User Story 2 - Director records a detailed health record (Priority: P1)

**Goal**: Director can add/edit/delete a categorized health record, optionally with a signed-URL
attachment, without blocking the save if the attachment upload fails.

**Independent Test**: Add a health record of each `recordType` to a child's Gezondheid tab,
confirm each appears with title/description/validity window; attach then fetch a PDF via a signed
URL; submit a record with no attachment and confirm it still saves.

### Tests for User Story 2

- [X] T035 [P] [US2] Integration test: create/list/update/delete health record happy path (all five record types), in `backend/ChildCare.Api.Tests/HealthRecords/HealthRecordCrudTests.cs`
- [X] T036 [P] [US2] Integration test: attachment upload-url issuance + signed download URL round-trip, and a record saves successfully with zero attachment calls made, in `backend/ChildCare.Api.Tests/HealthRecords/HealthRecordAttachmentTests.cs`
- [X] T037 [P] [US2] Integration test: reject invalid `recordType`, missing `title`/`description`, `validUntil` before `validFrom`, and an attachment content-type/size outside PDF/JPEG/PNG/10MB (FR-006), in `backend/ChildCare.Api.Tests/HealthRecords/HealthRecordValidationTests.cs`
- [X] T038 [P] [US2] Integration test: a record with `validUntil` in the past is still returned by `GET` (never hidden) with `isExpired: true`, in `backend/ChildCare.Api.Tests/HealthRecords/HealthRecordExpiryTests.cs`

### Implementation for User Story 2

- [X] T039 [P] [US2] `IHealthAttachmentStorage` port in `backend/ChildCare.Application/Common/IHealthAttachmentStorage.cs` per research.md R2 (category+subjectId signed upload/download URL pair, content-type aware)
- [X] T040 [US2] `HealthAttachmentStorage` GCS implementation in `backend/ChildCare.Infrastructure/Storage/HealthAttachmentStorage.cs`, reusing the existing `Storage:ProfilePhotosBucketName` bucket with a `health-records/{id}/attachment.{ext}` path, registered in `backend/ChildCare.Api/Program.cs` (depends on T039)
- [X] T041 [P] [US2] `HealthRecordResponse` (including computed `isExpired`, `attachmentDownloadUrl`) in `backend/ChildCare.Contracts/Responses/HealthRecordResponse.cs`
- [X] T042 [P] [US2] `CreateHealthRecordRequest`/`UpdateHealthRecordRequest` in `backend/ChildCare.Contracts/Requests/HealthRecordRequests.cs`
- [X] T043 [US2] `HealthRecordResult`/`HealthRecordFailure` wrapper in `backend/ChildCare.Application/HealthRecords/HealthRecordResult.cs`
- [X] T044 [US2] `HealthRecordMapper.ToResponse` (computes `isExpired` from `validUntil`, resolves `attachmentDownloadUrl` via `IHealthAttachmentStorage`) in `backend/ChildCare.Application/HealthRecords/HealthRecordMapper.cs` (depends on T039, T041)
- [X] T045 [US2] `CreateHealthRecordCommand` + validator + handler in `backend/ChildCare.Application/HealthRecords/CreateHealthRecordCommand.cs` (depends on T043, T044)
- [X] T046 [US2] `UpdateHealthRecordCommand` + validator + handler in `backend/ChildCare.Application/HealthRecords/UpdateHealthRecordCommand.cs` (depends on T043, T044)
- [X] T047 [US2] `DeleteHealthRecordCommand` + handler (soft-delete) in `backend/ChildCare.Application/HealthRecords/DeleteHealthRecordCommand.cs` (depends on T043)
- [X] T048 [US2] `ListChildHealthRecordsQuery` + handler in `backend/ChildCare.Application/HealthRecords/ListChildHealthRecordsQuery.cs` (depends on T044)
- [X] T049 [US2] `CreateHealthRecordAttachmentUploadUrlCommand` + handler (validates content-type is PDF/JPEG/PNG and requested size ≤10MB per FR-006, sets `attachment_object_path` deterministically, returns signed upload URL) in `backend/ChildCare.Application/HealthRecords/CreateHealthRecordAttachmentUploadUrlCommand.cs` (depends on T039)
- [X] T050 [US2] `HealthRecordEndpoints.cs` mapping POST/GET/PUT/DELETE + POST `.../attachment-upload-url` under `/api/children/{childId}/health-records`, `DirectorOnly`, registered in `Program.cs` (depends on T045, T046, T047, T048, T049)
- [X] T051 [US2] Add NL/FR/EN i18n keys for all `errors.health_records.*` validation messages
- [X] T052 [US2] Health Records section (list + add/edit form + attachment upload control) on the Gezondheid tab, `web/components/health/HealthRecordForm.tsx`, with the same empty-state/validity-window display rules as data-model.md (expired records visibly marked, never hidden)
- [X] T053 [US2] Client-side: on attachment upload failure, save the record anyway and surface a retry affordance rather than blocking the whole form submit (FR-007); the upload control itself is keyboard-operable and announces progress/success/failure via an `aria-live` region, not a visual-only indicator (FR-020)
- [X] T054 [P] [US2] Web component test: submit a health record with no attachment (succeeds), then attach a file after the fact, in `web/components/health/HealthRecordForm.test.tsx`

**Checkpoint**: A director can fully manage health records, with or without attachments,
independently of User Story 1.

---

## Phase 5: User Story 3 - Director sees which children have a booster due soon (Priority: P2)

**Goal**: A dashboard block lists every child across the director's locations with a vaccine due
or overdue within 30 days.

**Independent Test**: Create vaccine records at various `nextDueDate` offsets across children;
confirm only the within-30-days/overdue subset appears, soonest/most-overdue first; confirm the
empty state when nothing is due.

### Tests for User Story 3

- [X] T055 [P] [US3] Integration test: due-soon query returns only children within the 30-day window (inclusive of overdue), correctly excludes a 60-day-out record, sorted correctly, in `backend/ChildCare.Api.Tests/VaccineRecords/VaccinationsDueSoonTests.cs`
- [X] T056 [P] [US3] Integration test: empty result set returns `200` with `[]`, and a child with multiple due-soon vaccines appears once (its most urgent), in `backend/ChildCare.Api.Tests/VaccineRecords/VaccinationsDueSoonCollapsingTests.cs`

### Implementation for User Story 3

- [X] T057 [US3] `VaccinationsDueSoonResponse` in `backend/ChildCare.Contracts/Responses/VaccinationsDueSoonResponse.cs` (depends on T019)
- [X] T058 [US3] `ListVaccinationsDueSoonQuery` + handler per research.md R4 (join `vaccine_records`→`children`→active `ChildGroupAssignment`→`Group`→`Location`, filtered to the director's accessible locations, `next_due_date <= today + withinDays`, one row per child collapsed to the most urgent record, `isOverdue = next_due_date < today` — a due date of exactly today is due-soon, not overdue, per FR-009) in `backend/ChildCare.Application/VaccineRecords/ListVaccinationsDueSoonQuery.cs` (depends on T057)
- [X] T059 [US3] `GET /api/vaccine-records/due-soon` endpoint (`withinDays` query param, default 30), `DirectorOnly`, in `VaccineRecordEndpoints.cs` (depends on T058)
- [X] T060 [US3] `DueSoonBlock` dashboard component (icon-paired overdue/due-soon indicators per design-system.md's WCAG 1.4.1 rule — never color alone) in `web/components/health/DueSoonBlock.tsx`, wired into the existing director dashboard/home page
- [X] T061 [US3] Calm empty state for the dashboard block when nothing is due
- [X] T062 [P] [US3] Web component test: dashboard block renders correct subset and overdue styling, in `web/components/health/DueSoonBlock.test.tsx`

**Checkpoint**: The dashboard due-soon block works end-to-end, independently of US1/US2's CRUD
screens (though it has no data to show until US1 has shipped some vaccine records).

---

## Phase 6: User Story 4 - Caregiver glances at a child's health summary (Priority: P1)

**Goal**: A caregiver taps a child from the group view and sees the health/allergy summary and
due-soon vaccine flags read-only, in one tap, respecting location-eligibility, cached for offline.

**Independent Test**: Give a child an active health record and a due-soon vaccine; confirm an
eligible caregiver sees both in the quick-access sheet with no edit affordance; confirm an
ineligible caregiver gets a 404; confirm the cached summary is available offline.

### Tests for User Story 4

- [X] T063 [P] [US4] Integration test: eligible caregiver/device sees active health records + due-soon vaccines in the summary; ineligible caller gets the same 404 as a nonexistent child (research.md R3), in `backend/ChildCare.Api.Tests/Children/ChildHealthSummaryTests.cs`
- [X] T064 [P] [US4] Integration test: no write endpoint is reachable for a caregiver/device credential against vaccine-records or health-records routes (401/403), in `backend/ChildCare.Api.Tests/Children/ChildHealthSummaryReadOnlyTests.cs`
- [X] T065 [P] [US4] Integration test: expired health records and non-due vaccines are excluded from the summary, in `backend/ChildCare.Api.Tests/Children/ChildHealthSummaryFilteringTests.cs`

### Implementation for User Story 4

- [X] T066 [US4] `ChildHealthSummaryResponse` in `backend/ChildCare.Contracts/Responses/ChildHealthSummaryResponse.cs`
- [X] T067 [US4] `GetChildHealthSummaryQuery` + handler reusing `GetChildByIdQuery`'s `StaffLocationEligibility` scoping (research.md R3), returning active (non-expired, non-deleted) health records + every due-soon/overdue vaccine for that child (not collapsed to one — FR-013 deliberately differs from the dashboard's per-child collapsing since only one child is in view), in `backend/ChildCare.Application/Children/GetChildHealthSummaryQuery.cs` (depends on T066)
- [X] T068 [US4] `GET /api/children/{childId}/health-summary` endpoint under `DeviceOrStaffOrDirector`, in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs` (depends on T067)
- [X] T069 [US4] Extend the caregiver app's existing feature-008 medical quick-access sheet with the health-summary data in `mobile/components/health/HealthSummarySheet.tsx` (extends, does not replace, the existing allergy/medical-notes sheet — verify the sheet's existing layout has room for this content without a redesign, per spec.md Assumptions; reuse the same overdue-vs-due-soon icon pairing as the director dashboard's `DueSoonBlock`, T060, for cross-surface consistency per FR-011)
- [X] T070 [US4] Wire the health-summary fetch into the existing feature-008 read-cache pattern (cache-on-load, tenant-scoped) so it's available offline, in `mobile/services/` (the existing offline read-cache module, not a new mechanism)
- [X] T071 [US4] Calm empty state ("No known health information") when a child has no records/flags, and a distinct "cannot load — check connection" state when offline with no cached data (spec.md Edge Cases)
- [X] T072 [US4] Regenerate `mobile/services/generated/api-types.ts` and commit the diff
- [X] T073 [P] [US4] Mobile component test: summary sheet renders health records + due-soon flags, shows the empty state, and exposes no edit affordance, in `mobile/components/health/HealthSummarySheet.test.tsx`

**Checkpoint**: All four user stories are independently functional and testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: GDPR export-exclusion guard, final i18n sweep, and end-to-end validation.

- [X] T074 [P] Integration test proving no existing serialization/export path includes `VaccineRecord`/`HealthRecord` data without an explicit opt-in (FR-016, quickstart.md Scenario 5 — a forward-looking regression test since no bulk-export feature exists yet), in `backend/ChildCare.Api.Tests/VaccineRecords/HealthDataExportExclusionTests.cs`
- [X] T074a [P] Integration test: deactivating a child with existing vaccine and health records leaves both fully queryable by a director afterward (no cascade delete, FR-017/SC-005); a vaccine record already overdue at deactivation time is still returned by the due-soon query on a later call, never cleared by the deactivation itself (FR-012's "never auto-dismiss," exercised here since no dismiss code path exists to test in isolation), in `backend/ChildCare.Api.Tests/VaccineRecords/RecordRetentionAfterDeactivationTests.cs` (depends on T026, T048, T058)
- [X] T075 [P] Update `.specify/memory/Workflows/health-safety.md`'s "Flow — ongoing information" section to describe the new structured vaccine/health-record flow, superseding the plain feature-006-only description (workflows.md governance rules — document what changed, why, which features affected)
- [X] T076 Full i18n sweep: confirm every new user-facing string (web Gezondheid tab, dashboard block, caregiver summary sheet, all validation/error messages) has NL/FR/EN keys with no hardcoded text (constitution Principle IV)
- [X] T077 Run quickstart.md Scenarios 1–6 end-to-end against a local TestContainers/dev backend
- [X] T078 Full backend + web + mobile test suite run; fix any regressions surfaced by removing the legacy `vaccination_records` code path

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (needs the legacy `VaccinationRecord` references
  gone before the new `DbSet`s can cleanly replace them) — BLOCKS all user stories.
- **User Stories (Phase 3–6)**: All depend on Foundational completion.
  - US1 and US2 are independent of each other (different tables/endpoints/screens).
  - US3 depends on US1's `vaccine_records` data existing to have anything to show, but its own
    query/endpoint/component can be built and tested (with seeded data) independently of US1's
    UI shipping.
  - US4 depends on US1 and US2's data existing to summarize, but its own query/endpoint/component
    can likewise be built and tested independently.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Parallel Opportunities

- All Setup tasks (T001–T006) marked [P] run in parallel — different files.
- Foundational entity/enum tasks (T008–T011) marked [P] run in parallel.
- Within each user story, all test tasks marked [P] run in parallel with each other (but after
  the story's own implementation tasks they test).
- US1 and US2 can be implemented in parallel by different developers once Phase 2 is complete.

---

## Parallel Example: User Story 1

```bash
Task: "Integration test: create/list/update/delete vaccine record happy path"
Task: "Integration test: reject administeredOn in the future and missing vaccineName"
Task: "Integration test: childId not in tenant returns 404"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (remove legacy code).
2. Complete Phase 2: Foundational (schema, migration, entities).
3. Complete Phase 3: User Story 1 (vaccine record CRUD + Gezondheid tab).
4. **STOP and VALIDATE**: quickstart.md Scenario 1, independently.
5. Deploy/demo if ready — this alone satisfies the legal Vaccinatieboekje record-keeping
   requirement, even before health records, the dashboard, or caregiver access ship.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 (vaccine CRUD) → validate → demo.
3. US2 (health records + attachments) → validate → demo.
4. US3 (due-soon dashboard) → validate → demo.
5. US4 (caregiver read-only summary) → validate → demo.
6. Polish (GDPR guard test, workflow doc, i18n sweep, full suite run).

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- Commit after each task or logical group.
- Legacy `vaccination_records`/`VaccinationRecord` removal (Phase 1) touches shared
  infrastructure files (`ITenantDbContext`, `Program.cs`) — do those sequentially, not in
  parallel with Foundational's additions to the same files (T007/T012 are sequenced accordingly).

---

## Phase 8: Convergence

- [X] T079 Add a dedicated unit test for `mobile/services/healthSummary.ts` per US4/AC4 and spec.md's offline Edge Case (partial) — mock `apiClient`/`readCache` directly (not the whole service) and cover: a successful fetch caches the result under `health-summary:{childId}`; a failed fetch falls back to a previously-cached value; a failed fetch with nothing cached returns `{ status: "unavailable" }`. Mirrors `__tests__/screens/group-view.test.tsx`'s `fetchChildren` cache-fallback test, the only equivalent precedent in this codebase.
