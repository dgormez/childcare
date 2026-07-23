---

description: "Task list for feature implementation"
---

# Tasks: Staff HR Dossier & Time Registration

**Input**: Design documents from `/specs/028-staff-hr-dossier/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/staff-hr-api.md,
quickstart.md (all present)

**Tests**: Included — this codebase's constitution (Principle V) requires integration tests
against real (TestContainers) infrastructure for the happy path plus key negative/regulatory
flows on every feature.

**Organization**: Tasks are grouped by user story (spec.md US1–US4) to enable independent
implementation and testing of each story. Phase order follows spec.md's story order (a natural
build sequence: clock in/out → correct entries → dossier → report), not strict priority order —
US1 and US4 are both P1, US2 and US3 both P2, but each remains independently testable regardless
of phase position (see Dependencies below).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

## Path Conventions

Web application extending three existing projects, per plan.md's Project Structure: `backend/`,
`web/`, `staff-mobile/` (all existing — no new project scaffolding needed, so there is no
separate Setup phase; Phase 1 below is Foundational).

---

## Phase 1: Foundational (blocking prerequisites)

**Purpose**: New entities, enums, storage port, and migration every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 [P] Add `StaffTimeEntryFunction` enum (`Kinderbegeleider`/`Logistiek`/
      `Verantwoordelijke`) with `TryParseWireString`/`ToWireString` extensions (mirrors
      `ChildEventTypeExtensions`, feature 009) in
      `backend/ChildCare.Domain/Enums/StaffTimeEntryFunction.cs`
- [X] T002 [P] Add `StaffDocumentType` enum (`EmploymentContract`/`Amendment`/`Qualification`/
      `Training`/`Other`) with the same wire-string extension pattern in
      `backend/ChildCare.Domain/Enums/StaffDocumentType.cs`
- [X] T003 [P] Create `StaffTimeEntry` entity (data-model.md) in
      `backend/ChildCare.Domain/Entities/StaffTimeEntry.cs` (depends on T001)
- [X] T004 [P] Create `StaffDocument` entity (data-model.md) in
      `backend/ChildCare.Domain/Entities/StaffDocument.cs` (depends on T002)
- [X] T005 [P] Add `TimeEntryFunctions: List<StaffTimeEntryFunction>` to `StaffProfile` in
      `backend/ChildCare.Domain/Entities/StaffProfile.cs` (depends on T001)
- [X] T006 [P] Add `IStaffDocumentStorage` port (research.md R3, mirrors
      `IHealthAttachmentStorage`) in `backend/ChildCare.Application/Common/IStaffDocumentStorage.cs`
- [X] T007 Add `DbSet<StaffTimeEntry>` and `DbSet<StaffDocument>` to
      `backend/ChildCare.Application/Common/ITenantDbContext.cs` (depends on T003, T004)
- [X] T008 Configure EF Core mapping for `StaffTimeEntry`/`StaffDocument`, the
      `TimeEntryFunctions` `text[]` value converter + `ValueComparer` (mirrors
      `ContractedDays`'s existing conversion, feature 027), and the `(LocationId, ClockedInAt)`/
      `(DocumentType, ValidUntil)` indexes (data-model.md) in
      `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T007, T005)
- [X] T009 Implement `GcsStaffDocumentStorage` (`IStaffDocumentStorage`) in
      `backend/ChildCare.Infrastructure/Storage/GcsStaffDocumentStorage.cs` (depends on T006)
- [X] T010 [P] Register `IStaffDocumentStorage` → `GcsStaffDocumentStorage` in the main builder's
      DI setup in `backend/ChildCare.Api/Program.cs` (depends on T009)
- [X] T011 Generate the EF Core migration: create `staff_time_entries`, `staff_documents`, add
      `StaffProfile.TimeEntryFunctions` (backfilled to `{}`) in
      `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T008)
- [X] T012 Extend `TenantMigrationRolloutTests` and `LegacyVaccinationMigrationTests`'s
      schema-revert helpers for the two new tables (research.md R7 — the recurring pattern every
      migration-adding feature since 012a has needed) in `backend/ChildCare.Api.Tests/`
      (depends on T011)
- [X] T013 Generate the production SQL script for this migration per constitution Principle VI
      (EF Core migrations never auto-apply in production) (depends on T011)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 2: User Story 1 - Staff clocks in and out (Priority: P1) 🎯 MVP

**Goal**: A staff member can clock in and out from staff-mobile, with the function picker
appearing only when genuinely ambiguous.

**Independent Test**: Configure a staff member's `TimeEntryFunctions`, clock in via the API/app,
confirm a `StaffTimeEntry` row exists with the correct function and no double-open-entry is
possible, then clock out.

### Tests for User Story 1

- [X] T014 [P] [US1] Integration test: `ClockInCommand` auto-selects the function when exactly
      one is configured, requires `function` in the request when more than one is configured,
      and rejects with `400 errors.staff_time_entries.no_function_configured` when none is
      configured (FR-004/FR-005/FR-010) in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/ClockInOutTests.cs`
- [X] T015 [P] [US1] Integration test: a second `ClockInCommand` while one entry is already open
      returns `409 errors.staff_time_entries.already_clocked_in` (FR-003) in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/ClockInOutTests.cs`
- [X] T016 [P] [US1] Integration test: `ClockOutCommand` closes the caller's own open entry;
      returns `404 errors.staff_time_entries.no_open_entry` when none is open (FR-002) in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/ClockInOutTests.cs`
- [X] T016a [P] [US1] Integration test: `ClockInCommand` rejects a `locationId` the caller has no
      `StaffLocationEligibility` grant for with `403 errors.staff_time_entries
      .location_not_eligible` (FR-001a), and rejects a `function` not in the caller's own
      `TimeEntryFunctions` with `400 errors.staff_time_entries.function_not_configured`
      (FR-005a) — both checked even when a different, eligible/configured value was never
      offered by the client UI, in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/ClockInOutTests.cs`
- [X] T016b [P] [US1] Integration test: `ClockInCommand` rejects a `groupId` whose
      `Group.LocationId` doesn't match the supplied `locationId` with `400
      errors.staff_time_entries.group_location_mismatch` (FR-004a) in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/ClockInOutTests.cs`
- [X] T017 [P] [US1] Component test: `ClockInOutCard` shows "Begin dienst"/"Einde dienst"
      correctly based on open-entry state and is disabled with a connectivity message while
      offline (mirrors `report-sick.tsx`'s existing pattern) in
      `staff-mobile/__tests__/ClockInOutCard.test.tsx`

### Implementation for User Story 1

- [X] T018 [US1] Implement `ClockInCommand` (identity resolved from the JWT `NameIdentifier`
      claim, research.md R2; function-selection logic FR-005; `StaffLocationEligibility` check
      FR-001a; configured-function check FR-005a; `Group.LocationId` match check FR-004a) in
      `backend/ChildCare.Application/StaffTimeEntries/ClockInCommand.cs` (depends on T003, T005)
- [X] T019 [US1] Implement `ClockOutCommand` in
      `backend/ChildCare.Application/StaffTimeEntries/ClockOutCommand.cs`
- [X] T020 [US1] [P] Add `StaffTimeEntryResult` and the clock-in/out request/response contracts
      in `backend/ChildCare.Application/StaffTimeEntries/StaffTimeEntryResult.cs`,
      `backend/ChildCare.Contracts/Requests/StaffTimeEntryRequests.cs`, and
      `backend/ChildCare.Contracts/Responses/StaffTimeEntryResponses.cs`
- [X] T021 [US1] Create `StaffTimeEntryEndpoints.cs` with `POST /api/staff-time-entries/clock-in`,
      `/clock-out`, and `GET /me/current` (`StaffOrDirector`; the third route and its
      `GetMyOpenTimeEntryQuery` were added during implementation — a gap in the original design,
      needed so staff-mobile can render "Einde dienst" vs "Begin dienst" correctly on app
      reopen, not only right after a clock action, FR-001 Acceptance Scenario 3) in
      `backend/ChildCare.Api/Endpoints/StaffTimeEntryEndpoints.cs` (depends on T018, T019, T020)
- [X] T022 [US1] Implement `timeEntries.ts` (clock-in/clock-out API calls, mirrors `schedule.ts`'s
      shape) in `staff-mobile/services/timeEntries.ts` (depends on T021's contract types)
- [X] T023 [US1] Build `ClockInOutCard.tsx` (one-tap; function picker only when ambiguous per
      FR-005; offline-disabled per `useIsOffline`) in `staff-mobile/components/ClockInOutCard.tsx`
- [X] T024 [US1] Mount `ClockInOutCard` at the top of the schedule (app home) screen in
      `staff-mobile/app/(app)/schedule/index.tsx` (research.md R10; depends on T023)
- [X] T025 [US1] Add NL/FR/EN i18n keys for clock in/out in
      `staff-mobile/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 3: User Story 2 - Director corrects a missed clock-out (Priority: P2)

**Goal**: A director can fill in a missing clock-out, and can unlock/correct/re-lock an
older entry.

**Independent Test**: Create an open entry, correct it as director within the lock window;
create an entry older than 7 days, confirm the edit is rejected until unlocked, unlock it,
correct it, then re-lock it.

### Tests for User Story 2

- [X] T026 [P] [US2] Integration test: `UpdateStaffTimeEntryCommand` succeeds within the 7-day
      window and returns `423 errors.staff_time_entries.locked` once past it (FR-006/FR-008) in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/TimeEntryLockTests.cs`
- [X] T027 [P] [US2] Integration test: `UnlockStaffTimeEntryCommand` sets `UnlockedAt` and
      `UnlockedBy` to the acting director (FR-007a), the entry stays editable afterward with no
      auto re-lock, and `RelockStaffTimeEntryCommand` clears both fields back to locked (FR-007,
      research.md R4) in `backend/ChildCare.Api.Tests/StaffTimeEntries/TimeEntryLockTests.cs`
- [X] T028 [P] [US2] Integration test: a correction that overlaps another entry for the same
      staff member returns `overlapWarning: true` and still saves, rather than being blocked
      (FR-009) in `backend/ChildCare.Api.Tests/StaffTimeEntries/TimeEntryOverlapWarningTests.cs`
- [X] T028a [P] [US2] Integration test: `UpdateStaffTimeEntryCommand` rejects a corrected
      `function` that isn't one of the staff member's own `TimeEntryFunctions`, same constraint
      as clock-in (FR-008/FR-005a) in
      `backend/ChildCare.Api.Tests/StaffTimeEntries/TimeEntryLockTests.cs`
- [X] T029 [P] [US2] Component test: `TimeEntryCorrectionDialog` surfaces the overlap warning and
      a clear locked-state message in `web/__tests__/TimeEntryCorrectionDialog.test.tsx`

### Implementation for User Story 2

- [X] T030 [US2] Implement `UpdateStaffTimeEntryCommand` (lock check, overlap detection,
      configured-function check FR-005a) in
      `backend/ChildCare.Application/StaffTimeEntries/UpdateStaffTimeEntryCommand.cs`
- [X] T031 [US2] [P] Implement `UnlockStaffTimeEntryCommand` (sets `UnlockedBy` from the acting
      director's JWT, FR-007a) and `RelockStaffTimeEntryCommand` (clears both `UnlockedAt`/
      `UnlockedBy`) in
      `backend/ChildCare.Application/StaffTimeEntries/UnlockStaffTimeEntryCommand.cs` and
      `RelockStaffTimeEntryCommand.cs`
- [X] T032 [US2] [P] Implement `ListStaffTimeEntriesQuery` in
      `backend/ChildCare.Application/StaffTimeEntries/ListStaffTimeEntriesQuery.cs`
- [X] T033 [US2] Add `GET`/`PATCH`/`unlock`/`relock` routes to `StaffTimeEntryEndpoints.cs`
      (`DirectorOnly`) (depends on T030, T031, T032)
- [X] T034 [US2] Build `StaffTimeEntriesTab.tsx` (list + correction trigger) in
      `web/components/staff/StaffTimeEntriesTab.tsx`
- [X] T035 [US2] Build `TimeEntryCorrectionDialog.tsx` (clock-out fill-in, overlap warning,
      unlock/relock controls) in `web/components/staff/TimeEntryCorrectionDialog.tsx`
- [X] T036 [US2] Add NL/FR/EN i18n keys for the time-entry correction UI in
      `web/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: User Stories 1–2 both work independently.

---

## Phase 4: User Story 3 - Director maintains a staff member's HR dossier (Priority: P2)

**Goal**: A director uploads/manages HR documents per staff member, configures which
function(s) they may clock in under, and sees expiring contracts on the dashboard.

**Independent Test**: Upload one document of each type to a staff member's dossier, confirm each
is listed/downloadable; set an employment-contract `validUntil` within 60 days and confirm it
surfaces on the dashboard block.

### Tests for User Story 3

- [X] T037 [P] [US3] Integration test: the upload-URL + confirm flow creates a `StaffDocument`
      row with a working signed download URL and `CreatedBy` set to the acting director from the
      JWT, never a client-supplied value (FR-011/FR-012/FR-012a) in
      `backend/ChildCare.Api.Tests/StaffDocuments/StaffDossierTests.cs`
- [X] T038 [P] [US3] Integration test: deleting a document soft-deletes the row
      (`DeletedAt`/`DeletedBy` set, FR-012a), excludes it from the list and contracts-expiring
      queries, and best-effort deletes the underlying GCS object in
      `backend/ChildCare.Api.Tests/StaffDocuments/StaffDossierTests.cs`
- [X] T039 [P] [US3] Integration test: `GetContractsExpiringQuery` returns only
      `EmploymentContract`-type documents with `ValidUntil <= today + 60d` (inclusive of
      already-past dates), excludes non-contract document types entirely, regardless of their
      own dates (FR-014, Edge Cases) in
      `backend/ChildCare.Api.Tests/StaffDocuments/ContractsExpiringTests.cs`
- [X] T040 [P] [US3] Component test: `ContractExpiryBlock` renders loading/empty/error/loaded
      states and navigates to the staff detail page on click (mirrors `DueSoonBlock`'s existing
      test) in `web/__tests__/ContractExpiryBlock.test.tsx`
- [X] T040a [P] [US3] Integration test: every dossier endpoint (list/upload-url/confirm/delete
      documents, `time-entry-functions`, `contracts-expiring`) rejects a staff-authenticated (not
      director) request with `403` (FR-013 — dossier access is director-only, mirrors 027's
      `CrossStaffIsolationTests` precedent for this class of check) in
      `backend/ChildCare.Api.Tests/StaffDocuments/StaffDossierTests.cs`

### Implementation for User Story 3

- [X] T041 [US3] Implement `CreateStaffDocumentUploadUrlCommand` and `CreateStaffDocumentCommand`
      (`CreatedBy` resolved server-side from the JWT, FR-012a) in
      `backend/ChildCare.Application/StaffDocuments/CreateStaffDocumentUploadUrlCommand.cs`
      and `CreateStaffDocumentCommand.cs`
- [X] T042 [US3] [P] Implement `DeleteStaffDocumentCommand` (soft-delete: sets `DeletedAt`/
      `DeletedBy`, then calls `IStaffDocumentStorage.DeleteAsync`, FR-012a) and
      `ListStaffDocumentsQuery` (filters `DeletedAt IS NULL`) in
      `backend/ChildCare.Application/StaffDocuments/DeleteStaffDocumentCommand.cs` and
      `ListStaffDocumentsQuery.cs`
- [X] T043 [US3] Implement `GetContractsExpiringQuery` in
      `backend/ChildCare.Application/StaffDocuments/GetContractsExpiringQuery.cs`
- [X] T044 [US3] Implement `UpdateStaffTimeEntryFunctionsCommand` (FR-010) in
      `backend/ChildCare.Application/StaffTimeEntries/UpdateStaffTimeEntryFunctionsCommand.cs`
- [X] T045 [US3] Add the document request/response contracts in
      `backend/ChildCare.Contracts/Requests/StaffDocumentRequests.cs` and
      `backend/ChildCare.Contracts/Responses/StaffDocumentResponses.cs`
- [X] T046 [US3] Extend `StaffEndpoints.cs` with document CRUD, `time-entry-functions`, and
      `contracts-expiring` routes (`DirectorOnly`) (depends on T041–T045)
- [X] T047 [US3] Build the new staff detail screen shell with **Dossier**/**Tijdsregistraties**
      tabs, Dossier as the default/first tab (research.md R9, spec.md SC-002), mirroring
      `children/[id]/page.tsx`'s tab pattern in `web/app/(app)/staff/[id]/page.tsx` (depends on
      T034 for the Tijdsregistraties tab content)
- [X] T048 [US3] Build `StaffDossierTab.tsx`, `StaffDocumentForm.tsx`, and
      `TimeEntryFunctionsForm.tsx` in `web/components/staff/`
- [X] T049 [US3] Make `staff/page.tsx` rows navigate to the new detail page (was inert) in
      `web/app/(app)/staff/page.tsx`
- [X] T050 [US3] Build `ContractExpiryBlock.tsx` (mirrors `DueSoonBlock.tsx`, research.md R8) and
      mount it on the dashboard in `web/components/staff/ContractExpiryBlock.tsx` and
      `web/app/(app)/dashboard/page.tsx`
- [X] T051 [US3] Add NL/FR/EN i18n keys for the dossier and contract-expiry UI in
      `web/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: User Stories 1–3 all work independently.

---

## Phase 5: User Story 4 - Director generates the medewerkersbeleid subsidy report (Priority: P1)

**Goal**: A director generates a child-hours/staff-hours ratio report by function for a
location/period, and downloads the same data as a CSV.

**Independent Test**: Seed known attendance and time-entry data for a location/period, generate
the report, confirm the ratios match a manual calculation, and confirm an open time entry inside
the period is excluded.

### Tests for User Story 4

- [X] T052 [P] [US4] Integration test: `GetStaffHoursReportQuery` computes `totalChildHours` from
      closed `AttendanceRecord`s and `totalStaffHours`/`ratio` per function from closed
      `StaffTimeEntry`s, excludes open entries and incomplete attendance records from the totals
      (FR-017/FR-018/FR-019, research.md R5), and returns `ratio: null` (not a divide-by-zero)
      when `totalStaffHours == 0` (FR-016 Acceptance Scenario 2) in
      `backend/ChildCare.Api.Tests/Reporting/StaffHoursReportTests.cs`
- [X] T053 [P] [US4] Integration test: `ExportStaffHoursReportQuery`'s CSV rows sum to the exact
      same `totalStaffHours` the on-screen query returns for the same location/period (R6 —
      shared aggregation, never disagree) in
      `backend/ChildCare.Api.Tests/Reporting/StaffHoursReportTests.cs`
- [X] T053a [P] [US4] Integration test: `GET /api/reports/staff-hours` and its `/export` route
      both reject a staff-authenticated (not director) request with `403` (FR-016 — director-only,
      same class of check as T040a) in
      `backend/ChildCare.Api.Tests/Reporting/StaffHoursReportTests.cs`

### Implementation for User Story 4

- [X] T054 [US4] Implement `GetStaffHoursReportQuery` in
      `backend/ChildCare.Application/Reporting/GetStaffHoursReportQuery.cs`
- [X] T055 [US4] Implement `IStaffHoursCsvWriter`/`StaffHoursCsvWriter` (mirrors
      `IAttendanceSummaryCsvWriter`) and `ExportStaffHoursReportQuery` (reuses
      `GetStaffHoursReportQuery` via `IMediator.Send`, research.md R6) in
      `backend/ChildCare.Infrastructure/Reporting/StaffHoursCsvWriter.cs` and
      `backend/ChildCare.Application/Reporting/ExportStaffHoursReportQuery.cs` (depends on T054)
- [X] T056 [US4] Add the `StaffHoursReportResponses` contract in
      `backend/ChildCare.Contracts/Responses/StaffHoursReportResponses.cs`
- [X] T057 [US4] Add `GET /api/reports/staff-hours` and `/export` to the existing
      `ReportingEndpoints.cs` group (`DirectorOnly`, alongside feature 018's report routes)
      (depends on T054, T055, T056)
- [X] T058 [US4] Build `StaffHoursReportTable.tsx` and the new flat top-level
      `staff-hours-report` page (location/period selector, ratio table, CSV download link —
      this codebase has no "Rapporten" parent nav) in
      `web/components/reporting/StaffHoursReportTable.tsx` and
      `web/app/(app)/staff-hours-report/page.tsx`
- [X] T059 [US4] Add a `staffHoursReport` nav entry to `web/components/Sidebar.tsx` (flat,
      mirrors every other top-level item)
- [X] T060 [US4] Add NL/FR/EN i18n keys for the report page in
      `web/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: All four user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T061 [P] Regenerate the web and staff-mobile openapi-fetch client types from the final
      backend OpenAPI spec in `web/lib/generated/api-types.ts` and
      `staff-mobile/services/generated/`
- [ ] T062 Design-compliance static review of every new/changed web and staff-mobile screen
      against `design-system.md`/`platform-rules.md` (4/8/12/16/24/32 spacing, no nested cards,
      48pt staff-mobile touch targets, motion under 250ms) — fix findings in place
- [ ] T063 Run `quickstart.md`'s full validation scenario set end-to-end and fix any gaps found
- [ ] T064 [P] Regenerate `web/package-lock.json` and `staff-mobile/package-lock.json` via a
      clean install to catch lockfile drift before CI (recurring `npm ci` issue per 007a/010/
      013b/006a/023's shipped-notes)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — can start immediately. BLOCKS all user stories.
- **User Stories (Phase 2–5)**: All depend on Foundational (Phase 1) completion. US3's dossier
  tab and US2's time-entry tab share one detail-page shell (T047) — US3's phase includes that
  shell task since it's the first to need it, but US2's tab component (T034) is built in US2's
  own phase and only wired into the shell during US3. This is the one cross-story wiring point;
  every other task is self-contained within its story.
- **Polish (Phase 6)**: Depends on all four user stories being complete.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories — clock in/out is the foundation the report (US4)
  and corrections (US2) both need real data from, though each remains independently *testable*
  via seeded fixtures per its own Independent Test criteria.
- **US2 (P2)**: Independently testable against seeded `StaffTimeEntry` fixtures; in practice
  extends US1's entries.
- **US3 (P2)**: Independent of US1/US2 for the dossier half (documents, contract-expiry); its
  `StaffTimeEntriesTab` wiring (T047) reuses US2's `TimeEntryCorrectionDialog`/
  `StaffTimeEntriesTab` component once both exist.
- **US4 (P1)**: Independently testable against seeded `AttendanceRecord`/`StaffTimeEntry`
  fixtures; in practice reports on US1/US2's real data once live.

### Within Each User Story

- Tests are written first and must fail before implementation.
- Backend entities/contracts before commands/queries before endpoints.
- Backend endpoints before the web/staff-mobile screens that consume them.
- Story complete before moving to the next phase.

### Parallel Opportunities

- All Foundational enum/entity tasks marked [P] (T001–T006) can run in parallel; T007 depends on
  T003/T004, T008 depends on T005/T007, T010 depends on T009.
- Tests within a story marked [P] can run in parallel with each other (different test
  methods/files).
- Backend and web/staff-mobile UI tasks within a story can proceed in parallel once the relevant
  contracts (Txxx contract task) exist.

---

## Parallel Example: User Story 4

```bash
# Launch both tests for User Story 4 together:
Task: "Integration test: GetStaffHoursReportQuery aggregation, exclusions, zero-division"
Task: "Integration test: ExportStaffHoursReportQuery CSV parity with on-screen report"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational (CRITICAL — blocks all stories).
2. Complete Phase 2: User Story 1 (clock in/out).
3. **STOP and VALIDATE**: confirm clock in/out works end-to-end via Scenario 1 of quickstart.md.

### Incremental Delivery

1. Foundational → foundation ready.
2. US1 → validate → staff can clock in/out, real hours data starts accumulating.
3. US2 → validate → directors can correct/unlock time entries — the data stays trustworthy.
4. US3 → validate → HR dossier and contract-expiry alerts, independent of time registration.
5. US4 → validate → the medewerkersbeleid subsidy report, this feature's headline payoff.
6. Polish → design compliance, quickstart end-to-end, lockfile hygiene.

---

## Notes

- [P] tasks touch different files with no unmet dependency.
- [Story] labels map each task to spec.md's US1–US4 for traceability.
- Verify each story's tests fail before implementing, per constitution Principle V.
- Commit after each task or logical group; never merge with a failing test.
