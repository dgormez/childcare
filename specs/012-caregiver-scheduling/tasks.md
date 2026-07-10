# Tasks: Caregiver Scheduling (Weekly Staff Rota)

**Input**: Design documents from `specs/012-caregiver-scheduling/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by spec Technical Requirements and quickstart.md (constitution Principle V).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts, i18n scaffolding, and route registration shared across all stories.

- [X] T001 [P] Add `StaffScheduleRequests` (create/update/copy-week/absence request DTOs) in `backend/ChildCare.Contracts/Requests/StaffScheduleRequests.cs`
- [X] T002 [P] Add `StaffScheduleResponses` (entry, copy-week result, projected-on-duty, my-schedule) in `backend/ChildCare.Contracts/Responses/StaffScheduleResponses.cs`
- [X] T003 [P] Add `scheduling.*` i18n keys (nav label, empty state, grid labels, absence reasons, errors) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, and `web/i18n/locales/nl.json`
- [X] T004 Register the director web "Scheduling" nav entry in `web/components/Sidebar.tsx`
- [X] T005 Register `MapStaffScheduleEndpoints()` in `backend/ChildCare.Api/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core entity, persistence, and shared result types that all stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 [P] Create `AbsenceReason` enum in `backend/ChildCare.Domain/Enums/AbsenceReason.cs`
- [X] T007 [P] Create `StaffSchedule` entity in `backend/ChildCare.Domain/Entities/StaffSchedule.cs`
- [X] T008 Add `StaffSchedule` DbSet, enum conversion, and unique index `(StaffProfileId, Date, StartTime)` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T009 Add tenant migration for the `staff_schedules` table (incl. `(StaffProfileId, Date)` and `(LocationId, Date)` indexes per plan.md's Performance Goals) in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [X] T010 Add `DbSet<StaffSchedule> StaffSchedules` to `backend/ChildCare.Application/Common/ITenantDbContext.cs`
- [X] T011 [P] Create `StaffScheduleResult`, `StaffScheduleFailure` enum, and response mapper in `backend/ChildCare.Application/StaffScheduling/StaffScheduleResult.cs`
- [X] T012 Create `StaffScheduleEndpoints` route group (`DirectorOnly`) plus the standalone `StaffOrDirector` `/me` route in `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Director builds the weekly rota (Priority: P1) 🎯 MVP

**Goal**: Director can create/edit/delete schedule entries per location/group/day, with
overlap rejection and past-date immutability.

**Independent Test**: Create schedule entries for several staff across a week, edit a
future-dated entry, confirm an overlapping entry (same location or cross-location) is
rejected, confirm a past-dated entry cannot be edited/deleted.

### Tests for User Story 1

- [X] T013 [P] [US1] Integration test: director creates and lists schedule entries by location/week in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T014 [P] [US1] Integration test: multi-location, non-overlapping shifts for the same staff member on the same date both save (FR-010) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T015 [P] [US1] Integration test: overlapping shift is rejected with `409 errors.staff_schedules.overlap` both cross-location and within the same location (FR-003), including a concurrent-write variant using `IAdvisoryLockService` in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T016 [P] [US1] Integration test: edit/delete on a future-dated entry succeeds; edit/delete on a past-dated entry returns `400 errors.staff_schedules.past_date` (FR-004) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T017 [P] [US1] Integration test: staff/parent tokens cannot create/edit/delete schedule entries, and cannot list schedule entries or read the projected on-duty count (FR-015) — only `DirectorOnly` tokens can in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T018 [P] [US1] Web component test: scheduling page loads week grid, renders empty state, and an overlap error surfaces on the conflicting cell in `web/__tests__/scheduling.test.tsx`

### Implementation for User Story 1

- [X] T019 [P] [US1] Implement `ListStaffScheduleQuery` (by location + week) in `backend/ChildCare.Application/StaffScheduling/ListStaffScheduleQuery.cs`
- [X] T020 [US1] Implement `CreateStaffScheduleCommand` validator/handler with `IAdvisoryLockService`-guarded overlap check (research.md R2) in `backend/ChildCare.Application/StaffScheduling/CreateStaffScheduleCommand.cs`
- [X] T021 [US1] Implement `UpdateStaffScheduleCommand` validator/handler (past-date rejection, FR-004) in `backend/ChildCare.Application/StaffScheduling/UpdateStaffScheduleCommand.cs`
- [X] T022 [US1] Implement `DeleteStaffScheduleCommand` handler (past-date rejection) in `backend/ChildCare.Application/StaffScheduling/DeleteStaffScheduleCommand.cs`
- [X] T023 [US1] Map `GET /api/staff-schedules`, `POST /api/staff-schedules`, `PATCH /api/staff-schedules/{id}`, `DELETE /api/staff-schedules/{id}` in `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`
- [X] T024 [US1] Regenerate OpenAPI types for staff-schedule endpoints in `web/lib/generated/api-types.ts`
- [X] T025 [P] [US1] Implement `SchedulingGrid` week × staff grid component — density per platform-rules.md Director Web section, every cell/action keyboard-reachable with a visible focus ring (spec.md UX Requirements Accessibility) in `web/components/SchedulingGrid.tsx`
- [X] T026 [P] [US1] Implement `ScheduleEntryDialog` create/edit form (location/group/time picker), keyboard-operable per spec.md UX Requirements Accessibility in `web/components/ScheduleEntryDialog.tsx`
- [X] T027 [US1] Implement scheduling page data loading/actions/week navigation in `web/app/(app)/scheduling/page.tsx`

**Checkpoint**: User Story 1 should be fully functional and independently testable.

---

## Phase 4: User Story 2 - Director copies a week's rota forward (Priority: P2)

**Goal**: Director copies a populated week onto a target week, with closure-day and
existing-entry conflicts skipped and reported (never silently overwritten).

**Independent Test**: Copy a populated week onto an empty target week and verify every
entry replicates; copy onto a week containing a closure day and a pre-existing conflicting
entry and verify both are skipped and reported while all other entries still copy.

### Tests for User Story 2

- [X] T028 [P] [US2] Integration test: copying a fully-scheduled week replicates every entry onto the target week's dates (FR-008) in `backend/ChildCare.Api.Tests/StaffScheduling/CopyWeekTests.cs`
- [X] T029 [P] [US2] Integration test: target week containing a `KdvClosureDay` skips that day's entries and reports `reason: closure_day` (FR-009) in `backend/ChildCare.Api.Tests/StaffScheduling/CopyWeekTests.cs`
- [X] T030 [P] [US2] Integration test: target week with a pre-existing conflicting entry skips only that slot, reports `reason: existing_entry`, and completes all other slots (FR-009a) in `backend/ChildCare.Api.Tests/StaffScheduling/CopyWeekTests.cs`
- [X] T030a [P] [US2] Integration test: copy request targeting a week not after the source week (same or earlier) is rejected with `400 errors.staff_schedules.invalid_copy_target` (FR-016) in `backend/ChildCare.Api.Tests/StaffScheduling/CopyWeekTests.cs`
- [X] T031 [P] [US2] Web component test: copy-week action shows the skipped-entries summary in `web/__tests__/scheduling.test.tsx`

### Implementation for User Story 2

- [X] T032 [US2] Implement `CopyWeekCommand` handler (bulk-insert transaction, closure-day + existing-entry skip logic, target-week validity check per FR-016, research.md R4) in `backend/ChildCare.Application/StaffScheduling/CopyWeekCommand.cs`
- [X] T033 [US2] Map `POST /api/staff-schedules/copy-week` in `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`
- [X] T034 [US2] Add copy-week action and skipped-entries summary UI to `web/app/(app)/scheduling/page.tsx`

**Checkpoint**: User Stories 1 and 2 should work independently.

---

## Phase 5: User Story 3 - Director marks a staff member absent (Priority: P2)

**Goal**: Director marks/un-marks a schedule entry absent with a reason; the rota builder's
own projected on-duty count reflects it; feature 010's live BKR ratio remains untouched.

**Independent Test**: Mark an entry absent and verify it's excluded from the projected
on-duty count for that slot, while feature 010's live BKR endpoint output is unchanged;
un-mark it and verify it reverts.

### Tests for User Story 3

- [X] T035 [P] [US3] Integration test: marking an entry absent with a reason excludes it from `GetProjectedOnDutyQuery`, and un-marking (on a future-dated entry) reverts it (FR-005, FR-006) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T036 [P] [US3] Integration test: `isAbsent=true` without `absenceReason` returns `400 errors.validation`; past-dated entry rejects absence changes in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T037 [P] [US3] Integration test: qualification exclusion — a `StudentVolunteer` entry is excluded from the projected on-duty count regardless of absence state (mirrors feature 010's exclusion rule) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T038 [P] [US3] Integration test: a deactivated staff member's future-dated entries are excluded from the projected on-duty count without being deleted (FR-009b) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T039 [P] [US3] Regression test: marking a staff member absent in `staff_schedules` does NOT change feature 010's `GetBkrRatioQuery` output for the same location/time (research.md R1 — proves decoupling) in `backend/ChildCare.Api.Tests/StaffScheduling/BkrDecouplingTests.cs`
- [X] T040 [P] [US3] Web component test: marking a cell absent updates its visual state and the projected on-duty indicator in `web/__tests__/scheduling.test.tsx`

### Implementation for User Story 3

- [X] T041 [US3] Implement `MarkAbsenceCommand` validator/handler (past-date rejection, reason-required-iff-absent) in `backend/ChildCare.Application/StaffScheduling/MarkAbsenceCommand.cs`
- [X] T042 [US3] Implement `GetProjectedOnDutyQuery` (absence + qualification + deactivation exclusion, research.md R1/R5) in `backend/ChildCare.Application/StaffScheduling/GetProjectedOnDutyQuery.cs`
- [X] T043 [US3] Map `POST /api/staff-schedules/{id}/absence` and `GET /api/staff-schedules/projected-on-duty` in `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`
- [X] T044 [US3] Add absence-marking action and projected on-duty indicator to `web/components/SchedulingGrid.tsx` and `web/app/(app)/scheduling/page.tsx`

**Checkpoint**: User Stories 1, 2, and 3 should work independently; feature 010's BKR
endpoint is provably unaffected (T039).

---

## Phase 6: User Story 4 - Caregiver's own schedule is readable via API (Priority: P3)

**Goal**: A caregiver's own account can read their own upcoming schedule entries via a
personal-account-scoped endpoint. No UI ships in this feature (spec.md Assumptions).

**Independent Test**: Call the endpoint as an authenticated staff account and verify only
that account's own entries are returned; call as a staff account with no entries and verify
an empty result, not an error.

### Tests for User Story 4

- [X] T045 [P] [US4] Integration test: `GET /api/staff-schedules/me` returns only the caller's own entries, excluding other staff members' entries at the same location (FR-012) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T046 [P] [US4] Integration test: `GET /api/staff-schedules/me` for a staff account with no entries returns an empty array in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T047 [P] [US4] Integration test: a director-only token without a linked `StaffProfile` gets `404 errors.staff.profile_not_found` from `/me` (mirrors `GET /api/staff/me`'s precedent) in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`

### Implementation for User Story 4

- [X] T048 [US4] Implement `GetMyScheduleQuery` (resolves `StaffProfileId` from JWT claim, mirrors `GetStaffMeQuery`) in `backend/ChildCare.Application/StaffScheduling/GetMyScheduleQuery.cs`
- [X] T049 [US4] Map `GET /api/staff-schedules/me` as a standalone `StaffOrDirector` route (outside the `DirectorOnly` group, per research.md R3) in `backend/ChildCare.Api/Endpoints/StaffScheduleEndpoints.cs`

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories.

- [X] T050 [P] Add mid-week staff addition scenario (FR-011): integration test confirming a newly-created `StaffProfile` is immediately schedulable in the current week in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T051 [P] Add unassigned-group scenario (`groupId = null`) integration test confirming it doesn't block the projected on-duty count in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs`
- [X] T052 Run `specs/012-caregiver-scheduling/quickstart.md` validation end-to-end against a local backend + web instance
- [X] T053 Verify all new director-web strings resolve via `next-intl` in NL/FR/EN (no hardcoded text, constitution Principle IV)

---

## Phase 8: Convergence

- [X] T054 Add a `StaffLocationEligibility` check to `CreateStaffScheduleCommand` (and to `UpdateStaffScheduleCommand` when the location changes), returning a new `StaffScheduleFailure.NotEligible` (`403 errors.staff_schedules.not_eligible`) per cross-feature consistency with `VerifyPinCommand`/`CheckInCommand`'s existing `NotEligible` enforcement (missing)
- [X] T055 [P] Filter the web `SchedulingGrid`'s staff rows (or at minimum the "add shift" affordance) to staff eligible for the currently selected location, using `StaffResponse.eligibleLocationIds` already returned by `GET /api/staff` — no new endpoint needed (missing)
- [X] T056 [P] Integration test: creating (or updating into) a schedule entry for a staff member not eligible at that location returns `403 errors.staff_schedules.not_eligible` in `backend/ChildCare.Api.Tests/StaffScheduling/StaffScheduleEndpointsTests.cs` (missing)
- [X] T057 [P] Integration test: `CopyWeekCommand` rejects a non-Monday `sourceWeekStart`/`targetWeekStart` with `400 errors.validation` in `backend/ChildCare.Api.Tests/StaffScheduling/CopyWeekTests.cs` (partial)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories.
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion.
  - US1 (P1) has no dependency on other stories — it's the MVP.
  - US2 (P2, copy) depends on US1's entries existing to be meaningful, and reads
    `KdvClosureDay` (feature 011, already shipped) — no new dependency introduced.
  - US3 (P2, absence) operates on entries created by US1; independently testable via its own
    fixture data without requiring US2.
  - US4 (P3, own-schedule read) is a pure read on entries created by US1; no dependency on
    US2/US3.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Within Each User Story

- Tests written first, confirmed to fail before implementation (constitution Principle V).
- Domain/Application layer before Endpoints.
- Endpoints before web UI.
- Story complete and checkpoint-verified before moving to the next priority.

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel.
- All Foundational tasks marked [P] (T006, T007, T011) can run in parallel.
- All tests within a story marked [P] can run in parallel (different test methods, same or
  different files).
- T019 (query) and the two grid/dialog components (T025, T026) can proceed in parallel with
  each other once T020-T023 (commands/endpoints) land, since the components only need the
  contract shapes from Phase 1.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together:
Task: "Integration test: create and list schedule entries by location/week"
Task: "Integration test: multi-location non-overlapping shifts both save"
Task: "Integration test: overlapping shift rejected, same-location and cross-location (incl. concurrency)"
Task: "Integration test: past-date immutability"
Task: "Integration test: DirectorOnly authorization"
Task: "Web component test: week grid load/empty/overlap-error states"

# Launch independent US1 implementation pieces together:
Task: "Implement ListStaffScheduleQuery"
Task: "Implement SchedulingGrid component"
Task: "Implement ScheduleEntryDialog component"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1 (create/edit/delete rota entries, overlap + immutability
   rules).
4. **STOP and VALIDATE**: quickstart.md Scenario 1, independently.
5. Deploy/demo if ready — a director can already build a rota by hand at this point.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → test independently → MVP.
3. US2 (copy) → test independently → major time-saver lands.
4. US3 (absence + projected on-duty) → test independently, including the BKR-decoupling
   regression check (T039) — this is the highest-risk story to get wrong silently, so it
   gets its own dedicated regression test.
5. US4 (own-schedule API) → test independently — no UI, ready for feature 027.
6. Polish.

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- FR-006/FR-007's "projected on-duty count" is deliberately separate from feature 010's live
  BKR ratio (research.md R1) — T039's regression test exists specifically to catch any future
  change that accidentally couples the two.
- Commit after each task or logical group.
- Stop at any checkpoint to validate story independently.
