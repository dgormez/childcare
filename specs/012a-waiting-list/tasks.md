# Tasks: Waiting List Management

**Input**: Design documents from `specs/012a-waiting-list/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by spec Technical Requirements and quickstart.md (constitution Principle V).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts, i18n scaffolding, and route registration shared across all stories.

- [X] T001 [P] Add `WaitingListRequests` (create/update/reorder/status/link-child request DTOs) in `backend/ChildCare.Contracts/Requests/WaitingListRequests.cs`
- [X] T002 [P] Add `WaitingListResponses` (entry, occupancy-day, list) in `backend/ChildCare.Contracts/Responses/WaitingListResponses.cs`
- [X] T003 [P] Add `waitingList.*` i18n keys (nav label, empty state, status labels, duplicate badge, errors) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, and `web/i18n/locales/nl.json`
- [X] T004 Register the director web "Waiting List" nav entry in `web/components/Sidebar.tsx`
- [X] T005 Register `MapWaitingListEndpoints()` in `backend/ChildCare.Api/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core entity, persistence, email port extension, and shared result types that all stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 [P] Create `WaitingListStatus` enum in `backend/ChildCare.Domain/Enums/WaitingListStatus.cs`
- [X] T007 [P] Create `WaitingListEntry` entity in `backend/ChildCare.Domain/Entities/WaitingListEntry.cs`
- [X] T008 Add `WaitingListEntry` DbSet, enum conversion, and index `(LocationId, Status, Priority)` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T009 Add tenant migration for the `waiting_list_entries` table in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [X] T010 Add `DbSet<WaitingListEntry> WaitingListEntries` to `backend/ChildCare.Application/Common/ITenantDbContext.cs`
- [X] T011 [P] Create `WaitingListResult`, `WaitingListFailure` enum, and response mapper in `backend/ChildCare.Application/WaitingList/WaitingListResult.cs`
- [X] T012 Create `WaitingListEndpoints` route group (`DirectorOnly`) in `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs`
- [X] T013 [P] Add `SendWaitingListOfferedAsync` to `IEmailSender` (`backend/ChildCare.Application/Common/IEmailSender.cs`) and implement it in `EmailService` (`backend/ChildCare.Api/Services/EmailService.cs`), following `SendStaffInvitationAsync`'s English-only raw-HTML precedent (research.md R3)

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Director registers and reviews the waiting list (Priority: P1) 🎯 MVP

**Goal**: Director can create waiting-list entries and view them per location, sorted by
priority, filterable by status (defaulting to `waiting`), with likely duplicates flagged.

**Independent Test**: Create several entries for a location (including two sharing the same
child name/DOB) and verify each persists with the correct fields, appears in the
priority-sorted list, defaults to showing only `waiting` entries, and the duplicate pair is
visually flagged — independent of reordering or status transitions.

### Tests for User Story 1

- [X] T014 [P] [US1] Integration test: director creates and lists entries filtered by location/status, default `status=waiting` (FR-001, FR-003) in `backend/ChildCare.Api.Tests/WaitingList/WaitingListEndpointsTests.cs`
- [X] T015 [P] [US1] Integration test: creating an entry with only the required fields (omitting contact email/phone/requested start date/notes) succeeds in `backend/ChildCare.Api.Tests/WaitingList/WaitingListEndpointsTests.cs`
- [X] T016 [P] [US1] Integration test: two entries sharing child first/last name + DOB are both flagged `isDuplicate: true`, neither blocked nor auto-merged, including when one is `waiting` and the other is `withdrawn` and the list is filtered to the default `waiting`-only view (FR-004) in `backend/ChildCare.Api.Tests/WaitingList/WaitingListEndpointsTests.cs`
- [X] T017 [P] [US1] Integration test: `status=all` returns entries across every status in `backend/ChildCare.Api.Tests/WaitingList/WaitingListEndpointsTests.cs`
- [X] T018 [P] [US1] Integration test: staff/parent tokens cannot create, update, or list waiting-list entries — only `DirectorOnly` tokens can (FR-016/FR-017) in `backend/ChildCare.Api.Tests/WaitingList/WaitingListEndpointsTests.cs`
- [X] T019 [P] [US1] Web component test: waiting list page loads the table, renders the empty state (icon + sentence), and shows the duplicate badge in `web/__tests__/waitingList.test.tsx`

### Implementation for User Story 1

- [X] T020 [US1] Implement `CreateWaitingListEntryCommand` validator/handler (appends priority after existing entries for the location, FR-002) in `backend/ChildCare.Application/WaitingList/CreateWaitingListEntryCommand.cs`
- [X] T021 [US1] Implement `UpdateWaitingListEntryCommand` validator/handler (non-lifecycle fields only) in `backend/ChildCare.Application/WaitingList/UpdateWaitingListEntryCommand.cs`
- [X] T022 [P] [US1] Implement `ListWaitingListEntriesQuery` (filter by location/status defaulting to `waiting`, priority sort, duplicate-flagging per data-model.md) in `backend/ChildCare.Application/WaitingList/ListWaitingListEntriesQuery.cs`
- [X] T023 [US1] Map `GET /api/waiting-list`, `POST /api/waiting-list`, `PATCH /api/waiting-list/{id}` in `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs`
- [X] T024 [US1] Regenerate OpenAPI types for waiting-list endpoints in `web/lib/generated/api-types.ts`
- [X] T025 [P] [US1] Implement `WaitingListTable` component (sortable/filterable, duplicate badge, empty state) per design-system.md/platform-rules.md in `web/components/WaitingListTable.tsx`
- [X] T026 [P] [US1] Implement `WaitingListEntryDialog` create/edit form, keyboard-operable per spec.md UX Requirements Accessibility in `web/components/WaitingListEntryDialog.tsx`
- [X] T027 [US1] Implement waiting-list page data loading/actions/location selection in `web/app/(app)/waiting-list/page.tsx`

**Checkpoint**: User Story 1 should be fully functional and independently testable.

---

## Phase 4: User Story 2 - Director reorders the priority queue (Priority: P2)

**Goal**: Director moves a `waiting` entry up/down within its location's queue, with no
effect on other locations' queues, via both pointer and keyboard.

**Independent Test**: Reorder entries within one location's queue and verify the new order
persists and reloads correctly, that a second location's order is unaffected, and that a
non-`waiting` entry cannot be reordered — independent of status transitions or occupancy.

### Tests for User Story 2

- [X] T028 [P] [US2] Integration test: reordering an entry up/down within a location updates its priority and the list re-sorts (FR-005) in `backend/ChildCare.Api.Tests/WaitingList/ReorderTests.cs`
- [X] T029 [P] [US2] Integration test: reordering entries in one location does not change another location's queue order (FR-006) in `backend/ChildCare.Api.Tests/WaitingList/ReorderTests.cs`
- [X] T030 [P] [US2] Integration test: reordering an `offered`/`enrolled`/`withdrawn` entry is rejected with `409 errors.waiting_list.not_reorderable_in_current_status` (FR-005) in `backend/ChildCare.Api.Tests/WaitingList/ReorderTests.cs`
- [X] T031 [P] [US2] Web component test: up/down reorder actions are operable via keyboard only (Tab + Enter/Space, no mouse) in `web/__tests__/waitingList.test.tsx`

### Implementation for User Story 2

- [X] T032 [US2] Implement `ReorderWaitingListEntryCommand` handler (per-location scoping, `waiting`-only restriction, research.md R2 — no locking needed) in `backend/ChildCare.Application/WaitingList/ReorderWaitingListEntryCommand.cs`
- [X] T033 [US2] Map `POST /api/waiting-list/{id}/reorder` in `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs`
- [X] T034 [US2] Add keyboard-operable up/down reorder actions to `WaitingListTable` component in `web/components/WaitingListTable.tsx`

**Checkpoint**: User Stories 1 and 2 should work independently.

---

## Phase 5: User Story 3 - Director moves an entry through its status lifecycle (Priority: P2)

**Goal**: Director transitions an entry through `waiting → offered → enrolled/withdrawn` (and
`offered → waiting` to revert), with an email sent only on the `waiting → offered` transition
when a contact email is present, and every other transition rejected.

**Independent Test**: Move a single entry through `waiting → offered → enrolled` and,
separately, `waiting → offered → withdrawn`, verifying each transition updates status
correctly, an email is sent only on `→ offered` (and only with a contact email present), and
any transition outside the allow-list is rejected — independent of reordering.

### Tests for User Story 3

- [X] T035 [P] [US3] Integration test: `waiting → offered` with a `contactEmail` present sends a notification (verify `IEmailSender` invocation via test double) (FR-008) in `backend/ChildCare.Api.Tests/WaitingList/StatusTransitionTests.cs`
- [X] T036 [P] [US3] Integration test: `waiting → offered` with no `contactEmail` succeeds, no email attempted, no error (FR-008) in `backend/ChildCare.Api.Tests/WaitingList/StatusTransitionTests.cs`
- [X] T037 [P] [US3] Integration test: `offered → enrolled` and `offered → withdrawn` both succeed (FR-007) in `backend/ChildCare.Api.Tests/WaitingList/StatusTransitionTests.cs`
- [X] T038 [P] [US3] Integration test: `offered → waiting` reverts the status and sends no email for this reverse transition (FR-009) in `backend/ChildCare.Api.Tests/WaitingList/StatusTransitionTests.cs`
- [X] T039 [P] [US3] Integration test: any transition attempted from `enrolled` or `withdrawn` is rejected with `409 errors.waiting_list.invalid_status_transition` (FR-007) in `backend/ChildCare.Api.Tests/WaitingList/StatusTransitionTests.cs`
- [X] T040 [P] [US3] Integration test: `waiting → withdrawn` directly (skipping `offered`) succeeds (FR-007) in `backend/ChildCare.Api.Tests/WaitingList/StatusTransitionTests.cs`
- [X] T041 [P] [US3] Web component test: status badges pair color with an icon per design-system.md, and the status-transition action is available per row in `web/__tests__/waitingList.test.tsx`

### Implementation for User Story 3

- [X] T042 [US3] Implement `TransitionWaitingListStatusCommand` handler (FR-007 allow-list enforcement per research.md R4, email trigger per FR-008/FR-009) in `backend/ChildCare.Application/WaitingList/TransitionWaitingListStatusCommand.cs`
- [X] T043 [US3] Map `POST /api/waiting-list/{id}/status` in `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs`
- [X] T044 [US3] Add status-transition action and status badges to `WaitingListTable` component in `web/components/WaitingListTable.tsx`

**Checkpoint**: User Stories 1, 2, and 3 should work independently.

---

## Phase 6: User Story 4 - Director views projected occupancy for a location (Priority: P3)

**Goal**: Director views free-capacity-or-`Closed` per date for a location, computed from
active contracts and `Location.MaxCapacity`, honoring the closure calendar — never from
attendance records.

**Independent Test**: Query occupancy for a location across a date range with known active
contracts and known closure days, and verify the free-capacity count and closed-day flags are
both correct — independent of any specific waiting-list entry.

### Tests for User Story 4

- [X] T045 [P] [US4] Integration test: occupancy computes `freeCapacity = MaxCapacity - active contracts covering that weekday/date` (FR-014) in `backend/ChildCare.Api.Tests/WaitingList/OccupancyTests.cs`
- [X] T046 [P] [US4] Integration test: a date with a published `KdvClosureDay` returns `closed: true`, `freeCapacity: null`, never a numeric count (FR-015) in `backend/ChildCare.Api.Tests/WaitingList/OccupancyTests.cs`
- [X] T047 [P] [US4] Regression test: occupancy for a future date with zero `AttendanceRecord` rows still computes correctly — proves the computation never depends on attendance (research.md R1) in `backend/ChildCare.Api.Tests/WaitingList/OccupancyTests.cs`
- [X] T048 [P] [US4] Web component test: occupancy panel renders a closed day as "Closed" (never a number) and defaults to an entry's location/requested date when opened from that entry in `web/__tests__/waitingList.test.tsx`

### Implementation for User Story 4

- [X] T049 [US4] Implement `GetOccupancyQuery` (contract weekday/date-range coverage, closure override per research.md R1) in `backend/ChildCare.Application/WaitingList/GetOccupancyQuery.cs`
- [X] T050 [US4] Map `GET /api/waiting-list/occupancy` in `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs`
- [X] T051 [P] [US4] Implement `OccupancyPanel` component in `web/components/OccupancyPanel.tsx`
- [X] T052 [US4] Wire `OccupancyPanel` into the waiting-list page, defaulting to the selected location and an opened entry's requested start date in `web/app/(app)/waiting-list/page.tsx`

**Checkpoint**: User Stories 1 through 4 should work independently.

---

## Phase 7: User Story 5 - Director enrolls a family and links the child record (Priority: P3)

**Goal**: Director links an `enrolled` entry to an existing child record, or creates a new one
pre-filled from the entry's name/DOB, reusing feature 006's `CreateChildCommand` directly.

**Independent Test**: Transition an entry to `enrolled` and link it to an existing child
record, and separately trigger "create child record now?" when no match exists — independent
of occupancy or reordering.

### Tests for User Story 5

- [X] T053 [P] [US5] Integration test: linking an entry to an existing `childId` sets `WaitingListEntry.ChildId` (FR-010) in `backend/ChildCare.Api.Tests/WaitingList/ChildLinkTests.cs`
- [X] T054 [P] [US5] Integration test: `createNewChild: true` creates a new `Child` pre-filled with the entry's first/last name and DOB (via `CreateChildCommand`, research.md R5) and links it (FR-011) in `backend/ChildCare.Api.Tests/WaitingList/ChildLinkTests.cs`
- [X] T055 [P] [US5] Integration test: an `enrolled` entry left unlinked remains linkable at a later time (FR-012) in `backend/ChildCare.Api.Tests/WaitingList/ChildLinkTests.cs`
- [X] T056 [P] [US5] Integration test: providing neither `childId` nor `createNewChild` (or providing both) returns `400 errors.validation` in `backend/ChildCare.Api.Tests/WaitingList/ChildLinkTests.cs`
- [X] T057 [P] [US5] Web component test: the "create child record now?" prompt is pre-filled with the entry's first/last name and date of birth in `web/__tests__/waitingList.test.tsx`

### Implementation for User Story 5

- [X] T058 [US5] Implement `LinkChildToWaitingListEntryCommand` handler (existing-child link, or `CreateChildCommand` delegation per research.md R5) in `backend/ChildCare.Application/WaitingList/LinkChildToWaitingListEntryCommand.cs`
- [X] T059 [US5] Map `POST /api/waiting-list/{id}/link-child` in `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs`
- [X] T060 [P] [US5] Implement `EnrollChildLinkDialog` component (existing-child picker + pre-filled create-new prompt) in `web/components/EnrollChildLinkDialog.tsx`
- [X] T061 [US5] Wire `EnrollChildLinkDialog` into the waiting-list page's `enrolled`-entry row action in `web/app/(app)/waiting-list/page.tsx`

**Checkpoint**: All five user stories independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories.

- [X] T062 [P] Consolidated `DirectorOnly` authorization test across every new endpoint (list, create, update, reorder, status, occupancy, link-child) in `backend/ChildCare.Api.Tests/WaitingList/WaitingListEndpointsTests.cs`
- [X] T063 [P] Run `specs/012a-waiting-list/quickstart.md` validation end-to-end against a local backend + web instance
- [X] T064 [P] Verify all new director-web strings resolve via `next-intl` in NL/FR/EN (no hardcoded text, constitution Principle IV)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories.
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion.
  - US1 (P1) has no dependency on other stories — it's the MVP.
  - US2 (P2, reorder) operates on entries created by US1; independently testable via its own
    fixture data.
  - US3 (P2, status lifecycle) operates on entries created by US1; independently testable, no
    dependency on US2.
  - US4 (P3, occupancy) is a pure read against `Location`/`Contract`/`KdvClosureDay` — no
    dependency on any other story's entries.
  - US5 (P3, enrollment/child-link) operates on an `enrolled`-status entry (produced by US3),
    but is independently testable by seeding an entry directly at `Enrolled` status in test
    fixtures rather than requiring US3's full transition flow to run first.
- **Polish (Phase 8)**: Depends on all five user stories being complete.

### Within Each User Story

- Tests written first, confirmed to fail before implementation (constitution Principle V).
- Domain/Application layer before Endpoints.
- Endpoints before web UI.
- Story complete and checkpoint-verified before moving to the next priority.

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel.
- All Foundational tasks marked [P] (T006, T007, T011, T013) can run in parallel.
- All tests within a story marked [P] can run in parallel (different test methods, same or
  different files).
- T022 (query) and the two table/dialog components (T025, T026) can proceed in parallel with
  each other once T020-T023 (commands/endpoints) land, since the components only need the
  contract shapes from Phase 1.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together:
Task: "Integration test: create and list entries by location/status, default waiting"
Task: "Integration test: minimal-required-fields creation succeeds"
Task: "Integration test: duplicate name+DOB flagged, not blocked/merged"
Task: "Integration test: status=all returns every status"
Task: "Integration test: DirectorOnly authorization"
Task: "Web component test: table load/empty-state/duplicate-badge"

# Launch independent US1 implementation pieces together:
Task: "Implement ListWaitingListEntriesQuery"
Task: "Implement WaitingListTable component"
Task: "Implement WaitingListEntryDialog component"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1 (register and review entries).
4. **STOP and VALIDATE**: quickstart.md Scenario 1, independently.
5. Deploy/demo if ready — a director can already track families on a waiting list by hand at
   this point.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → test independently → MVP.
3. US2 (reorder) → test independently → active queue management lands.
4. US3 (status lifecycle) → test independently, including the email-on-`offered` and
   rejected-transition tests — this is the highest-risk story to get subtly wrong (an
   incorrect allow-list could silently skip a step), so it gets the most dedicated tests.
5. US4 (occupancy) → test independently, including the never-reads-attendance regression
   check (T047).
6. US5 (enrollment/child-link) → test independently.
7. Polish.

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- FR-014/FR-015's occupancy computation is deliberately sourced from `Contract`, never
  `AttendanceRecord` (research.md R1) — T047's regression test exists specifically to catch
  any future change that accidentally couples the two.
- Commit after each task or logical group.
- Stop at any checkpoint to validate story independently.

---

## Phase 9: Convergence

- [X] T065 Add `MaximumLength(2000)` validation for `Notes` to `CreateWaitingListEntryCommandValidator` and `UpdateWaitingListEntryCommandValidator` in `backend/ChildCare.Application/WaitingList/CreateWaitingListEntryCommand.cs` / `UpdateWaitingListEntryCommand.cs`, so an over-length value returns `400 errors.validation` instead of an unhandled DB exception (FR-001, Constitution VI) (missing)
- [X] T066 Reject occupancy queries against a deactivated location in `GetOccupancyQueryHandler` (`backend/ChildCare.Application/WaitingList/GetOccupancyQuery.cs`), returning `LocationNotFound` per spec.md's Edge Cases ("the occupancy panel cannot compute projected capacity for a deactivated location") (partial)
- [X] T067 Log an informational message when a `waiting → offered` transition completes with no contact email on file, in `TransitionWaitingListStatusCommandHandler` (`backend/ChildCare.Application/WaitingList/TransitionWaitingListStatusCommand.cs`), per FR-008's "...log that no email was sent" (partial)
