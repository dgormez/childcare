# Tasks: Multi-Child Events

**Input**: Design documents from `specs/009c-multi-child-events/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by spec Technical Requirements ("Testing requirements") and constitution
Principle V.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts, i18n scaffolding, and route registration shared across all stories.

- [ ] T001 [P] Add `RecordChildEventBatchRequest` DTO (an `Items: IReadOnlyList<ChildEventBatchItem>` of `{ ChildId, Id }` pairs, per contracts/child-events-batch-api.md — `Id` is client-generated for per-child idempotency, research.md R5) in `backend/ChildCare.Contracts/Requests/ChildEventRequests.cs`
- [ ] T002 [P] Add `ChildEventBatchResponse`/`ChildEventBatchCreatedItem`/`ChildEventBatchErrorItem` DTOs in `backend/ChildCare.Contracts/Responses/ChildEventResponses.cs`
- [ ] T003 [P] Add `childEvents.batch.*` and `groupView.multiSelect.*` i18n keys (toggle label, select-all, action bar count, success toast, partial-failure list, retry button, per-reason messages) to `mobile/i18n/locales/en.json`, `mobile/i18n/locales/nl.json`, `mobile/i18n/locales/fr.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The R2 auth fix (without which the room roster can't load on a real kiosk session
at all), shared result/failure types, and the batch endpoint's route registration skeleton.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T004 Add a `DeviceOrStaffOrDirector`-equivalent composite authorization policy accepting both the `DeviceToken` scheme and the default user-JWT scheme with `RequireRole("staff", "director")` in `backend/ChildCare.Api/Program.cs` (research.md R2, mirrors the existing `DeviceOrDirector` policy)
- [ ] T005 Apply the new policy to `GET /api/children` and `GET /api/children/{id}` in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs`, replacing `StaffOrDirector` on the `reads` group only (write routes unchanged)
- [ ] T006 Apply the new policy to `GET /api/groups` in `backend/ChildCare.Api/Endpoints/GroupsEndpoints.cs`, replacing `StaffOrDirector` on the `groupReads` group only (write routes unchanged)
- [ ] T007 [P] Add `ChildEventBatchFailureReason` enum (`ChildNotFound`, `NotPresent`, `ValidationFailed`) and `ChildEventBatchResult` in `backend/ChildCare.Application/ChildEvents/ChildEventResult.cs` (data-model.md)
- [ ] T008 Map `POST /api/child-events/batch` (device-token authenticated, same `deviceGroup` as the existing `POST /api/child-events`) returning `200` always, in `backend/ChildCare.Api/Endpoints/ChildEventEndpoints.cs` (contracts/child-events-batch-api.md) — wired to a not-yet-implemented `RecordChildEventBatchCommand` (stub returning empty result) so routing/auth can be tested before US1's handler logic lands

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Log a group event for all present children (Priority: P1) 🎯 MVP

**Goal**: A caregiver can select multiple present children on the room roster and submit one
event that creates one `ChildEvent` row per selected child.

**Independent Test**: Toggle multi-select, tap "select all," submit a `diaper` event for 8
present children, and verify 8 `ChildEvent` rows exist sharing the same `eventType`/`occurredAt`/
`payload`.

### Tests for User Story 1

- [ ] T009 [P] [US1] Integration test: `POST /api/child-events/batch` with 8 valid `childIds` returns `200` with 8 `created` entries and empty `errors`, and 8 `ChildEvent` rows exist with matching `eventType`/`occurredAt`/`payload` in `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventBatchTests.cs`
- [ ] T010 [P] [US1] Integration test: batch with an individual-only `eventType` (`temperature`) is rejected `422 errors.child_events.batch_type_not_supported` before any row is created in `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventBatchTests.cs`
- [ ] T011 [P] [US1] Integration test: batch with > 30 `childIds` is rejected `422 errors.child_events.batch_too_large` before any row is created in `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventBatchTests.cs`
- [ ] T012 [P] [US1] Integration test: batch containing a `childId` that doesn't exist in this tenant reports that child under `errors` with `reason: "child_not_found"`, other children still succeed in `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventBatchTests.cs`
- [ ] T013 [P] [US1] Integration test: `GET /api/children` and `GET /api/groups` succeed with a device token (regression test for T004-T006's R2 fix) in `backend/ChildCare.Api.Tests/CaregiverReadScopingTests.cs`
- [ ] T014 [P] [US1] Mobile component test: room roster header shows a multi-select entry point; entering multi-select mode makes present children's cards selectable and absent children non-selectable in `mobile/__tests__/groupView.test.tsx`
- [ ] T015 [P] [US1] Mobile component test: "Alles selecteren" selects every present child; submitting via the batch-mode `QuickActionSheet` calls `recordChildEventBatch` with all selected `childIds` and shows a success toast naming the count in `mobile/__tests__/quickActionSheet.test.tsx`

### Implementation for User Story 1

- [ ] T016 [US1] Implement `RecordChildEventBatchCommand`/validator/handler: dedupe `items` by `childId`, cap at 30, reject unsupported `eventType` types before the loop, then loop per item calling `RecordChildEventCommand`'s existing validation/creation logic including its idempotency-by-`id` check (reused, not duplicated per plan.md/research.md R5), one `SaveChangesAsync` per child, collecting `created`/`errors` in `backend/ChildCare.Application/ChildEvents/RecordChildEventBatchCommand.cs`
- [ ] T017 [US1] Replace T008's stub wiring with the real `RecordChildEventBatchCommand` call in `backend/ChildCare.Api/Endpoints/ChildEventEndpoints.cs`
- [ ] T018 [P] [US1] Regenerate OpenAPI types for the batch endpoint in `mobile/services/generated/api-types.ts`
- [ ] T019 [P] [US1] Add `recordChildEventBatch()` (generates a client-side `id` per selected child via the existing `generateId()` helper, builds the `items` array, online path only for this story — offline queuing is US3) in `mobile/services/childEvents.ts`
- [ ] T020 [US1] Add multi-select mode state, header entry-point button, per-card selected state, and "Alles selecteren" to the "children" tab in `mobile/app/(app)/index.tsx` (research.md R7) — long-press remains bound to absence-marking, unaffected
- [ ] T021 [US1] Add a bottom action bar (selected count + "Log event" button) shown when ≥1 child is selected, in `mobile/app/(app)/index.tsx`
- [ ] T022 [US1] Extend `QuickActionSheet` to accept `childIds: string[]` in addition to the existing single `childId`, filter `EVENT_TYPES` to the 8 batch-eligible types when in batch mode, and call `recordChildEventBatch` (not `recordChildEvent`) on submit, showing a success toast with the created count in `mobile/components/QuickActionSheet.tsx`

**Checkpoint**: User Story 1 fully functional — a caregiver can multi-select and submit a
successful batch, online.

---

## Phase 4: User Story 2 - Recover from a partial failure without redoing the batch (Priority: P1)

**Goal**: When some children in a batch fail (e.g. checked out mid-selection), the caregiver
sees exactly which ones and can retry only those.

**Independent Test**: Check one child out of a selected 6-child batch before submitting; verify
the response has 5 successes + 1 named failure, the 5 `ChildEvent` rows persist, and retrying
resubmits only the failed child.

### Tests for User Story 2

- [ ] T023 [P] [US2] Integration test: a child with no present `AttendanceRecord` today (or `CheckOutAt` already set) fails with `reason: "not_present"` while the other selected children in the same batch still succeed, in `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventBatchTests.cs`
- [ ] T024 [P] [US2] Integration test: a batch where every child fails returns `200` with `created: []` and every child under `errors` — never a whole-batch error response, in `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventBatchTests.cs`
- [ ] T025 [P] [US2] Mobile component test: a partial-failure batch response renders the failed children with their reasons, and tapping retry calls `recordChildEventBatch` again with only the previously-failed `childIds` in `mobile/__tests__/quickActionSheet.test.tsx`

### Implementation for User Story 2

- [ ] T026 [US2] Add the presence check (`AttendanceRecord` for today, device's `LocationId`, `Status = Present`, `CheckOutAt == null`) per child inside `RecordChildEventBatchCommand`'s loop, reported as `ChildEventBatchFailureReason.NotPresent` (research.md R4) in `backend/ChildCare.Application/ChildEvents/RecordChildEventBatchCommand.cs`
- [ ] T027 [US2] Render the partial/full-failure result in `QuickActionSheet` (list of failed children + plain-language reason per `childEvents.batch.*` i18n keys, paired icon per design-system.md — never color alone) and a retry action that resubmits only failed `childIds` in `mobile/components/QuickActionSheet.tsx`

**Checkpoint**: User Stories 1 and 2 both fully functional online.

---

## Phase 5: User Story 3 - Use the multi-select flow while offline (Priority: P2)

**Goal**: A batch submitted while offline is queued as one entry and replays as one call,
including correctly surfacing a partial-failure result discovered only at sync time.

**Independent Test**: Disable network, submit a multi-select batch of 5, verify exactly one
`offline_queue` row was created; re-enable network, verify it replays as one
`POST /api/child-events/batch` call and produces the expected per-child result (full or partial).

### Tests for User Story 3

- [ ] T028 [P] [US3] Mobile test: submitting a multi-select batch while offline creates exactly one `offline_queue` row with `entity_type = 'child_event_batch'`, `endpoint = '/api/child-events/batch'`, and a payload containing all selected `childIds` in `mobile/__tests__/childEvents.test.ts`
- [ ] T029 [P] [US3] Mobile test: `syncPendingQueue()` replaying a `child_event_batch` row whose response is `2xx` with a non-empty `errors` array marks the row via `markSyncError` with a `"partial: "`-prefixed message (counted as failed, not synced); an `errors: []` response marks it synced normally in `mobile/__tests__/syncEngine.test.ts`

### Implementation for User Story 3

- [ ] T030 [US3] Add the offline branch to `recordChildEventBatch()` (queue via `enqueue()` when `!isConnected`, matching `recordChildEvent`'s existing pattern) in `mobile/services/childEvents.ts`
- [ ] T031 [US3] Extend `syncEngine.ts`'s `response.ok` branch: for `entity_type === "child_event_batch"`, parse the body and route to `markSyncError` with a `"partial: "` prefix when `errors.length > 0` instead of `markSynced` (research.md R6) in `mobile/services/syncEngine.ts`
- [ ] T032 [US3] Register a `child_event_batch` sync handler (interface-shape only, mirrors `child_event`'s `onConflict: () => "discard"`, since batches are append-only creates with no update/delete path) in `mobile/services/childEvents.ts`

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T033 [P] Run `quickstart.md` end-to-end (curl steps + on-device steps) and fix anything it surfaces
- [ ] T034 [P] Verify spacing/touch-target/motion compliance of the new roster multi-select UI and `QuickActionSheet` batch mode against `design-system.md`/`platform-rules.md` (48pt targets, no color-only state, spacing scale)
- [ ] T035 Update `specs/009c-multi-child-events/data-model.md`/`contracts/child-events-batch-api.md` if implementation diverged from the plan

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup. BLOCKS all user stories — in particular, T004-T006
  (the R2 auth fix) block every mobile task in US1-US3, since the room roster cannot load
  children/groups under a real kiosk session without it.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational + US1's `RecordChildEventBatchCommand`
  (T016) and `QuickActionSheet` batch mode (T022) existing to extend — not independent of US1's
  code, but independently testable once both are in place.
- **User Story 3 (Phase 5)**: Depends on Foundational + US1's `recordChildEventBatch()` (T019)
  existing to extend with an offline branch.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- All Setup tasks (T001-T003) in parallel.
- T004→T005/T006 are sequential (policy must exist before routes reference it); T007 and T008 can
  run in parallel with T004-T006 (different files) but T008's real wiring waits on US1's T016.
- Within US1: all test tasks (T009-T015) in parallel with each other; T018/T019 in parallel; T020
  and T021 touch the same file (`index.tsx`) so run sequentially.
- Within US2: T023-T025 in parallel.
- Within US3: T028/T029 in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational — **critical**, includes the R2 auth fix without which nothing
   else can be exercised on a real device.
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: run `quickstart.md` steps 1-2 (backend) and mobile steps 1-5 online.

### Incremental Delivery

1. Setup + Foundational → foundation ready, R2 fix verified.
2. User Story 1 → online happy-path batch logging (MVP).
3. User Story 2 → partial-failure recovery.
4. User Story 3 → offline support.
5. Polish.
