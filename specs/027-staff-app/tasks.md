---

description: "Task list for feature implementation"
---

# Tasks: Staff App (Personal Rota & Leave)

**Input**: Design documents from `/specs/027-staff-app/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/staff-app-api.md,
quickstart.md (all present)

**Tests**: Included — this codebase's constitution (Principle V) requires integration tests
against real (TestContainers) infrastructure for the happy path plus key negative/regulatory
flows on every feature.

**Organization**: Tasks are grouped by user story (spec.md P1–P4) to enable independent
implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

## Path Conventions

Web application + new mobile app, per plan.md's Project Structure: `backend/`, `web/`
(existing), `staff-mobile/` (new Expo project).

---

## Phase 1: Setup (staff-mobile project scaffold)

**Purpose**: Bring the new `staff-mobile` Expo project into existence, mirroring
`parent-mobile/`'s established conventions (research.md R7), before any story-specific screen
work begins.

- [X] T001 Scaffold `staff-mobile/` Expo project (`package.json`, `app.config.js`,
      `babel.config.js`, `metro.config.js`, `tailwind.config.js`, `tsconfig.json`,
      `expo-env.d.ts`, `nativewind-env.d.ts`, `global.css`) mirroring `parent-mobile/`'s file
      set and dependency versions
- [X] T002 [P] Copy the design-system.md token set into `staff-mobile/theme/colors.js`
      (matches `design-decisions.md`'s "one hand-maintained copy per platform" pattern)
- [X] T003 [P] Scaffold `staff-mobile/i18n/` (i18next + `expo-localization` config, empty
      `locales/{en,fr,nl}.json`) mirroring `parent-mobile/i18n/`
- [X] T004 [P] Configure `staff-mobile/jest.config.js` and
      `staff-mobile/jest-mock-component-transform.js` mirroring `parent-mobile/`'s Jest +
      `@testing-library/react-native` setup
- [X] T005 [P] Scaffold a Zustand session/auth store skeleton in `staff-mobile/store/`
      (token storage via `expo-secure-store`, mirroring `parent-mobile/store/`)

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Data-model and infrastructure changes every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 [P] Add `StaffScheduleStatus` enum (`Scheduled`/`Confirmed`/`Absent`/`Covered`) in
      `backend/ChildCare.Domain/Enums/StaffScheduleStatus.cs`
- [X] T007 [P] Add `StaffLeaveRequestType` enum (`Sick`/`Annual`/`Other`) in
      `backend/ChildCare.Domain/Enums/StaffLeaveRequestType.cs`
- [X] T008 [P] Add `StaffLeaveRequestStatus` enum (`Pending`/`Approved`/`Rejected`) in
      `backend/ChildCare.Domain/Enums/StaffLeaveRequestStatus.cs`
- [X] T009 Extend `StaffSchedule` entity: add `Status`, `CoverStaffId`, `Notes`, `CreatedBy`,
      `IsPublished`, `PublishedAt`; replace the persisted `IsAbsent` column with a computed
      `IsAbsent => Status == StaffScheduleStatus.Absent` property (data-model.md,
      research.md R3) in `backend/ChildCare.Domain/Entities/StaffSchedule.cs` (depends on T006)
- [X] T010 [P] Add `ContractedDays` (`List<DayOfWeek>`) to `StaffProfile` in
      `backend/ChildCare.Domain/Entities/StaffProfile.cs`
- [X] T011 [P] Create `StaffLeaveRequest` entity in
      `backend/ChildCare.Domain/Entities/StaffLeaveRequest.cs` (depends on T007, T008)
- [X] T012 Add `SchedulePublished`, `AssignmentChanged`, `LeaveRequestDecided` to
      `NotificationType` in `backend/ChildCare.Domain/Enums/NotificationType.cs`
- [X] T013 Configure EF Core mapping for `StaffSchedule`'s new columns, the `ContractedDays`
      `text[]` value converter + `ValueComparer` (mirrors `MealPreference.DietaryType`,
      research.md R2), and `StaffLeaveRequest` in
      `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T009, T010,
      T011)
- [X] T014 Add `DbSet<StaffLeaveRequest>` to
      `backend/ChildCare.Application/Common/ITenantDbContext.cs` (depends on T011)
- [X] T015 Generate the EF Core migration: add `StaffSchedule`'s six new columns with a
      `Status` backfill from the existing `IsAbsent`/`AbsenceReason` values before dropping
      `IsAbsent` (data-model.md's Migration notes), add `StaffProfile.ContractedDays`
      (backfilled to `{}`), create `staff_leave_requests`, in
      `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T013, T014)
- [X] T016 Extend `TenantMigrationRolloutTests` and `LegacyVaccinationMigrationTests`'s
      schema-revert helpers for this migration's new table and altered columns (the recurring
      pattern every migration-adding feature since 012a has needed) in
      `backend/ChildCare.Api.Tests/` (depends on T015)
- [X] T017 Generate the production SQL script for this migration per constitution Principle VI
      (EF Core migrations never auto-apply in production) (depends on T015)
- [X] T018 Update the existing `StaffScheduleEndpointsTests.cs`/`CopyWeekTests.cs` (feature 012)
      for the `IsAbsent`→`Status` refactor so the existing test suite compiles and passes
      against the new shape, in `backend/ChildCare.Api.Tests/StaffScheduling/` (depends on T009)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Director builds and publishes a weekly rota (Priority: P1) 🎯 MVP

**Goal**: A director can build a week of assignments (with contracted-day/closure-day
awareness) and publish it, making it visible to staff for the first time.

**Independent Test**: Build a week via the API/web grid, confirm it's invisible via
`GET /api/staff-schedules/me`, publish, confirm it becomes visible and each affected staff
member is notified.

### Tests for User Story 1

- [X] T019 [P] [US1] Integration test: `PublishScheduleWeekCommand` rejects a non-Monday
      `weekStart` and publishes every row for `(locationId, weekStart)` in
      `backend/ChildCare.Api.Tests/StaffScheduling/PublishVisibilityTests.cs`
- [X] T020 [P] [US1] Integration test: an unpublished week's entries are excluded from
      `GetMyScheduleQuery`; the same entries appear once published, and each distinct affected
      staff member has exactly one `SchedulePublished` push recorded (fake sender) after publish
      (FR-008, SC-004), in
      `backend/ChildCare.Api.Tests/StaffScheduling/PublishVisibilityTests.cs`
- [X] T020a [P] [US1] Component test: `SchedulingGrid.tsx` renders a non-contracted-day cell and
      a closure-day column as greyed/non-interactive for new-assignment purposes (FR-002), in
      `web/__tests__/SchedulingGrid.test.tsx` (matches this codebase's flat `__tests__/`
      convention, e.g. `web/__tests__/checkInSettings.test.tsx`)

### Implementation for User Story 1

- [X] T021 [US1] Implement `PublishScheduleWeekCommand` in
      `backend/ChildCare.Application/StaffScheduling/PublishScheduleWeekCommand.cs`
- [X] T022 [US1] Implement `StaffScheduleNotificationService` (`SchedulePublished` case) in
      `backend/ChildCare.Application/StaffScheduling/StaffScheduleNotificationService.cs`
- [X] T023 [US1] Extend `GetMyScheduleQuery` to filter to `IsPublished == true` rows in
      `backend/ChildCare.Application/StaffScheduling/GetMyScheduleQuery.cs`
- [X] T024 [US1] Add the publish request/response contracts and extend
      `StaffScheduleResponse` with the new fields in
      `backend/ChildCare.Contracts/Requests/StaffScheduleRequests.cs` and
      `backend/ChildCare.Contracts/Responses/StaffScheduleResponses.cs`
- [X] T025 [US1] Add `POST /api/staff-schedules/{locationId}/publish` in
      `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`
- [X] T026 [US1] Extend `SchedulingGrid.tsx` with contracted-day and closure-day greying in
      `web/components/SchedulingGrid.tsx`
- [X] T027 [US1] Add a publish/unpublish control and published-state indicator to
      `web/app/(app)/scheduling/page.tsx`
- [X] T028 [US1] Add NL/FR/EN i18n keys for the publish UI in
      `web/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: User Story 1 is fully functional and independently testable.

---

## Phase 4: User Story 2 - Staff views their own schedule (Priority: P2)

**Goal**: A staff member opens `staff-mobile` and sees their own published schedule for the
next 4 weeks.

**Independent Test**: Log in to `staff-mobile` as a staff member with a published schedule
(from US1), confirm week/day views, split-day, closure-day, and empty states all render
correctly, and confirm the API never returns another staff member's rows.

### Tests for User Story 2

- [X] T029 [P] [US2] Integration test: a schedule/leave-request read never returns another
      staff member's rows regardless of any client-supplied identifier (FR-015), and
      `ReportSickCommand`/`CreateLeaveRequestCommand` always act on the JWT-resolved staff
      profile even if a client-supplied identifier is present in the request (FR-015a), in
      `backend/ChildCare.Api.Tests/StaffScheduling/CrossStaffIsolationTests.cs`
- [X] T030 [P] [US2] Component test: the schedule screen renders week/day toggle views, a
      split-day (two locations, one date), a closure day as "KDV gesloten," and the no-shifts
      empty state, in `staff-mobile/__tests__/screens/schedule.test.tsx`

### Implementation for User Story 2

- [X] T031 [US2] Build the `staff-mobile` login screen wired to the existing
      `POST /api/auth/login` (`role=staff`) in `staff-mobile/app/(auth)/login.tsx` (uses T005's
      store skeleton)
- [X] T032 [US2] Generate the `staff-mobile` openapi-fetch client in
      `staff-mobile/services/generated/` (depends on T024's contract types)
- [X] T033 [US2] [P] Build `ScheduleDayCard.tsx` and `ScheduleWeekList.tsx` in
      `staff-mobile/components/`
- [X] T034 [US2] Build the schedule home screen (day/week toggle, "KDV gesloten", contracted-day
      de-emphasis, loading/empty/error states per spec.md's UX Requirements) in
      `staff-mobile/app/(app)/schedule/index.tsx` (depends on T032, T033)
- [X] T035 [US2] Add a cache-fallback read for the schedule query, mirroring
      `parent-mobile`'s existing offline pattern (feature 013c), in
      `staff-mobile/hooks/useIsOffline.ts`
- [X] T036 [US2] Add NL/FR/EN i18n keys for the schedule screen in
      `staff-mobile/i18n/locales/{en,fr,nl}.json`
- [X] T037 [US2] Wire the `staff-mobile` Expo Router root layout and `(app)` group navigation
      in `staff-mobile/app/(app)/_layout.tsx`

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Sick report and on-the-fly cover (Priority: P3)

**Goal**: A staff member reports sick with one tap; the director sees an urgent banner, picks
eligible cover, and both parties are notified.

**Independent Test**: Report sick via the API/app for a staff member with today's assignment,
confirm the director's eligible-candidate list excludes ineligible/conflicting staff, assign
cover, confirm both the status flip and both push notifications, and confirm `GetBkrRatioQuery`
is unaffected throughout.

### Tests for User Story 3

- [X] T038 [P] [US3] Integration test: `ReportSickCommand` flips the day's `StaffSchedule`
      status to `Absent`, creates an auto-approved `sick`-type `StaffLeaveRequest`, notifies the
      director, and is idempotent on a repeated call for an already-`Absent` day (FR-005a — no
      duplicate request, no duplicate alert), in
      `backend/ChildCare.Api.Tests/StaffScheduling/SickCoverAssignmentTests.cs`
- [X] T039 [P] [US3] Integration test: `GetSickCoverCandidatesQuery` excludes staff without
      `StaffLocationEligibility` and staff with a conflicting overlapping assignment, in
      `backend/ChildCare.Api.Tests/StaffScheduling/SickCoverAssignmentTests.cs`
- [X] T040 [P] [US3] Integration test: `AssignCoverCommand` sets `CoverStaffId` on the original
      row, creates an immediately-visible `Covered`-status row for the replacement, notifies
      both staff members with content limited to their own assignment details (FR-008a — no
      unnecessary third-party PII), rejects an ineligible `coverStaffProfileId` called directly
      even when it wasn't offered by the candidates list (FR-014's write-side enforcement, not
      just `GetSickCoverCandidatesQuery`'s read-side filtering), and is race-safe under two
      concurrent assignment attempts (FR-018), in
      `backend/ChildCare.Api.Tests/StaffScheduling/SickCoverAssignmentTests.cs`
- [X] T041 [P] [US3] Extend `BkrDecouplingTests` to prove `Status`/`CoverStaffId` are never read
      by `GetBkrRatioQuery` (FR-016) in
      `backend/ChildCare.Api.Tests/StaffScheduling/BkrDecouplingTests.cs`

### Implementation for User Story 3

- [X] T042 [US3] Implement `ReportSickCommand` in
      `backend/ChildCare.Application/StaffScheduling/ReportSickCommand.cs`
- [X] T043 [US3] Implement `GetSickCoverCandidatesQuery` in
      `backend/ChildCare.Application/StaffScheduling/GetSickCoverCandidatesQuery.cs`
- [X] T044 [US3] Implement `AssignCoverCommand` under
      `IAdvisoryLockService.RunExclusiveAsync` in
      `backend/ChildCare.Application/StaffScheduling/AssignCoverCommand.cs`
- [X] T045 [US3] Extend `StaffScheduleNotificationService` for the `AssignmentChanged` case in
      `backend/ChildCare.Application/StaffScheduling/StaffScheduleNotificationService.cs`
- [X] T046 [US3] Add `report-sick`, `sick-cover-candidates`, and `assign-cover` endpoints in
      `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`
- [X] T047 [US3] Build `SickCoverDialog.tsx` and the urgent cover-needed banner in
      `web/components/SickCoverDialog.tsx` and `web/app/(app)/scheduling/page.tsx`
- [X] T048 [US3] Build the `staff-mobile` "Ik ben ziek" screen with an explicit confirmation
      step in `staff-mobile/app/(app)/report-sick.tsx`, plus NL/FR/EN i18n keys for both the web
      and `staff-mobile` sick-report/cover UI

**Checkpoint**: User Stories 1–3 all work independently.

---

## Phase 6: User Story 4 - Planned leave request approval (Priority: P4)

**Goal**: A staff member submits a leave request; a director approves or rejects it from a
queue, with approval marking the affected rota days absent.

**Independent Test**: Submit a leave request spanning scheduled and unscheduled dates, confirm
it appears in the director queue, approve it, confirm only the scheduled dates flip to absent,
then repeat with a rejection and confirm no rota change.

### Tests for User Story 4

- [X] T049 [P] [US4] Integration test: `CreateLeaveRequestCommand` validates the date range
      (`dateTo >= dateFrom`, not entirely in the past) in
      `backend/ChildCare.Api.Tests/StaffLeaveRequests/LeaveRequestApprovalTests.cs`
- [X] T050 [P] [US4] Integration test: `DecideLeaveRequestCommand` approval marks every
      matching `StaffSchedule` row `Absent` with `AbsenceReason` set per the
      `Sick→Sick`/`Annual→Leave`/`Other→Leave` mapping (research.md R3), leaves unscheduled
      dates untouched, and leaves an already-`Covered` row's `CoverStaffId` intact rather than
      overwriting it (FR-011/FR-011a); rejection makes no rota change, in
      `backend/ChildCare.Api.Tests/StaffLeaveRequests/LeaveRequestApprovalTests.cs`
- [X] T051 [P] [US4] Integration test: `GetMyLeaveRequestsQuery` never returns another staff
      member's requests (FR-012/015) in
      `backend/ChildCare.Api.Tests/StaffLeaveRequests/CrossStaffLeaveRequestIsolationTests.cs`

### Implementation for User Story 4

- [X] T052 [US4] Implement `CreateLeaveRequestCommand` in
      `backend/ChildCare.Application/StaffLeaveRequests/CreateLeaveRequestCommand.cs`
- [X] T053 [US4] Implement `DecideLeaveRequestCommand` in
      `backend/ChildCare.Application/StaffLeaveRequests/DecideLeaveRequestCommand.cs`
- [X] T054 [US4] [P] Implement `ListLeaveRequestsQuery` and `GetMyLeaveRequestsQuery` in
      `backend/ChildCare.Application/StaffLeaveRequests/ListLeaveRequestsQuery.cs` and
      `backend/ChildCare.Application/StaffLeaveRequests/GetMyLeaveRequestsQuery.cs`
- [X] T055 [US4] Add `LeaveRequestDecided` notification wiring in
      `backend/ChildCare.Application/StaffLeaveRequests/StaffLeaveRequestNotificationService.cs`
- [X] T056 [US4] Add the leave-request request/response contracts in
      `backend/ChildCare.Contracts/Requests/StaffLeaveRequestRequests.cs` and
      `backend/ChildCare.Contracts/Responses/StaffLeaveRequestResponses.cs`
- [X] T057 [US4] Add `StaffLeaveRequestEndpoints.cs` in
      `backend/ChildCare.Api/Endpoints/StaffLeaveRequestEndpoints.cs`
- [X] T058 [US4] Build `LeaveRequestTable.tsx`, the `/leave-requests` director queue page, and
      a Sidebar nav entry in `web/app/(app)/leave-requests/page.tsx`,
      `web/components/LeaveRequestTable.tsx`, and `web/components/Sidebar.tsx`
- [X] T059 [US4] Build the `staff-mobile` leave-request list and new-request form in
      `staff-mobile/app/(app)/leave-requests/index.tsx` and
      `staff-mobile/app/(app)/leave-requests/new.tsx`
- [X] T060 [US4] Add NL/FR/EN i18n keys for the leave-request UI in the web and `staff-mobile`
      locale files

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T061 [P] Build the `staff-mobile` notifications screen (`ICON_BY_TYPE` map,
      tap-to-navigate) covering `SchedulePublished`/`AssignmentChanged`/`LeaveRequestDecided`
      in `staff-mobile/app/(app)/notifications.tsx`
- [X] T062 [P] Regenerate the web and `staff-mobile` openapi-fetch client types from the final
      backend OpenAPI spec in `web/lib/generated/api-types.ts` and
      `staff-mobile/services/generated/`
- [X] T063 Design-compliance static review of every new/changed web and `staff-mobile` screen
      against `design-system.md`/`platform-rules.md` (4/8/12/16/24/32 spacing, no nested cards,
      48pt mobile touch targets, motion under 250ms) — fix findings in place
- [X] T064 Run `quickstart.md`'s full validation scenario set end-to-end and fix any gaps found
- [X] T065 [P] Regenerate `web/package-lock.json` and `staff-mobile/package-lock.json` via a
      clean install to catch lockfile drift before CI (recurring `npm ci` issue per 007a/010/
      013b/006a/023's shipped-notes)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion for `staff-mobile`'s existence, but
  is otherwise backend-only and could start in parallel with Phase 1 in practice — BLOCKS all
  user stories regardless.
- **User Stories (Phase 3–6)**: All depend on Foundational (Phase 2) completion. US2, US3, and
  US4 additionally depend on US1's publish mechanism existing for their own "published" test
  fixtures, but each remains independently *testable* (spec.md's Independent Test criteria) once
  US1 has landed — they are not blocked on US1's UI, only on the underlying publish plumbing
  (T021, T023, T025).
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories — the foundation everything else needs.
- **US2 (P2)**: Needs US1's publish mechanism (T021/T023/T025) to have real published data to
  read; the `staff-mobile` screens themselves are otherwise independent.
- **US3 (P3)**: Needs US1 (a published week to mark absent within) and reuses US2's
  `staff-mobile` scaffold (T031/T032/T037) — not its schedule screen itself.
- **US4 (P4)**: Needs US1 (rota rows to mark absent on approval) and reuses US2's
  `staff-mobile` scaffold, same as US3.

### Within Each User Story

- Tests are written first and must fail before implementation.
- Backend entities/contracts before commands/queries before endpoints.
- Backend endpoints before the web/`staff-mobile` screens that consume them.
- Story complete before moving to the next priority.

### Parallel Opportunities

- All Setup tasks marked [P] (T002–T005) can run in parallel once T001 exists.
- All Foundational enum/entity tasks marked [P] (T006–T008, T010–T011) can run in parallel;
  T009 depends on T006, T013 depends on T009/T010/T011.
- Tests within a story marked [P] can run in parallel with each other (different test methods/
  files).
- Backend and `staff-mobile`/web UI tasks within a story can proceed in parallel once the
  relevant contracts (Txxx contract task) exist.

---

## Parallel Example: User Story 3

```bash
# Launch all tests for User Story 3 together:
Task: "Integration test: ReportSickCommand flips status, creates leave request, notifies director"
Task: "Integration test: GetSickCoverCandidatesQuery excludes ineligible/conflicting staff"
Task: "Integration test: AssignCoverCommand sets CoverStaffId, notifies both staff, race-safe"
Task: "Extend BkrDecouplingTests for Status/CoverStaffId fields"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1 (publish/draft rota).
4. **STOP and VALIDATE**: confirm publish/visibility gating works via Scenario 1 of
   quickstart.md.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → validate → publish/draft rota works end-to-end (director-web only so far).
3. US2 → validate → `staff-mobile` exists and staff can see their own published schedule —
   the feature's headline motivation is now live.
4. US3 → validate → sick report + on-the-fly cover, the other headline scenario.
5. US4 → validate → planned leave requests, fully additive.
6. Polish → design compliance, quickstart end-to-end, lockfile hygiene.

---

## Notes

- [P] tasks touch different files with no unmet dependency.
- [Story] labels map each task to spec.md's US1–US4 for traceability.
- Verify each story's tests fail before implementing, per constitution Principle V.
- Commit after each task or logical group; never merge with a failing test.
