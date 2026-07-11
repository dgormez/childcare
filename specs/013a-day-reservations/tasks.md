# Tasks: Day Reservations (Parent Requests + Director Approval Queue)

**Input**: Design documents from `specs/013a-day-reservations/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by spec Technical Requirements and constitution Principle V (real
PostgreSQL via TestContainers for backend integration tests; component tests for web/parent-mobile).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs, i18n scaffolding, and route/nav registration shared across all stories.

- [X] T001 [P] Add `SubmitDayReservationRequest`, `ApproveDayReservationRequest`, `RejectDayReservationRequest` in `backend/ChildCare.Contracts/Requests/DayReservationRequests.cs`
- [X] T002 [P] Add `DayReservationResponse` (incl. denormalized child display name/location for FR-007) in `backend/ChildCare.Contracts/Responses/DayReservationResponses.cs`
- [X] T003 [P] Add `dayReservations.*` i18n keys (nav label "Verzoeken", queue labels, empty state, approve/reject actions, justified toggle, error keys) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`
- [X] T004 [P] Add `dayReservations.*` i18n keys (home action labels "Mijn kind is ziek"/"Extra dag aanvragen"/"Dagwissel aanvragen", form labels, status labels, history, error keys) to `parent-mobile/i18n/locales/en.json`, `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`
- [X] T005 Register the director web "Verzoeken" nav entry in `web/components/Sidebar.tsx`
- [X] T006 Create empty `DayReservationEndpoints` route groups (`ParentOnly` and `DirectorOnly`) and register `MapDayReservationEndpoints()` in `backend/ChildCare.Api/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core entity, persistence, and shared result/mapper types every story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 [P] Create `DayReservationType` enum (`Absence`, `Extra`, `Exchange`) in `backend/ChildCare.Domain/Enums/DayReservationType.cs`
- [X] T008 [P] Create `DayReservationStatus` enum (`Pending`, `Approved`, `Rejected`, `Cancelled`) in `backend/ChildCare.Domain/Enums/DayReservationStatus.cs`
- [X] T009 Create `DayReservation` entity per data-model.md in `backend/ChildCare.Domain/Entities/DayReservation.cs`
- [X] T010 Add `DayReservation` DbSet, enum-to-string conversions, and indexes `(Status, CreatedAt DESC)` + `(ChildId, CreatedAt DESC)` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T011 Add tenant migration `AddDayReservations` (table + indexes) in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`, and extend `TenantMigrationRolloutTests`' schema-revert helper for the new table (no FKs to non-tenant tables, but follow the same revert-coverage pattern every migration-adding feature since 003 has needed)
- [X] T012 Add `DbSet<DayReservation> DayReservations` to `backend/ChildCare.Application/Common/ITenantDbContext.cs`
- [X] T013 [P] Create `DayReservationResult`, `DayReservationFailure` enum, and `DayReservationMapper` (entity → `DayReservationResponse`, including denormalized child name via a join) in `backend/ChildCare.Application/DayReservations/DayReservationResult.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Parent reports a sick day (Priority: P1) 🎯 MVP

**Goal**: Parent submits an `absence` request; director approves (setting justified) or rejects
(with note); approval creates a pre-registered `AttendanceRecord` and either decision notifies
the parent.

**Independent Test**: Parent submits an absence request for their own child; director approves
it from the queue with `absenceJustified = true`; verify the attendance record exists with the
justified flag and a push notification was sent. Separately verify rejection with a note reaches
the parent's notification and creates no attendance record.

### Tests for User Story 1

- [X] T014 [P] [US1] Integration test: parent submits an `absence` request for their own linked child, request persists as `pending` (FR-001) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T015 [P] [US1] Integration test: absence request dated more than 1 day in the past is rejected with `errors.day_reservations.past_date` (FR-002) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T016 [P] [US1] Integration test: parent cannot submit or cancel a request for a child not linked to their `Contact` (FR-005) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T017 [P] [US1] Integration test: director approves a pending absence request with `absenceJustified = true`, resulting `AttendanceRecord` has `Status = Absent` and `AbsenceJustified = true`, and a push notification is sent (FR-008, FR-010, FR-013) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T018 [P] [US1] Integration test: director rejects a pending request with a `directorNotes` value, status becomes `rejected`, no `AttendanceRecord` is created, and the push notification payload includes the note (FR-009, FR-013) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T019 [P] [US1] Integration test: approving an absence request whose date has since become a published closure day fails with `errors.day_reservations.closure_day` and creates no `AttendanceRecord` (FR-011) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T020 [P] [US1] Integration test: two concurrent approve/reject calls against the same pending request — exactly one succeeds, the other receives `409 errors.day_reservations.not_pending` (FR-016) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T021 [P] [US1] Integration test: `StaffOrDirector`/device tokens cannot call parent-facing endpoints, and parent tokens cannot call director-facing endpoints (policy boundary) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`

### Implementation for User Story 1

- [X] T022 [US1] Implement `SubmitDayReservationCommand` + validator (absence branch: resolve parent `Contact` via `ICurrentParentContactResolver`, verify child linkage, reject past dates >1 day) in `backend/ChildCare.Application/DayReservations/SubmitDayReservationCommand.cs`
- [X] T023 [US1] Implement `ApproveDayReservationCommand` (guarded `Pending`→`Approved` transition, requires `absenceJustified` for absence type, sends `MarkAbsentCommand` via `IMediator` for absence type surfacing `ClosureDay`/`AlreadyRecorded` failures per research.md R1, sets `DecidedBy`/`DecidedAt`) in `backend/ChildCare.Application/DayReservations/ApproveDayReservationCommand.cs`
- [X] T024 [US1] Implement `RejectDayReservationCommand` (guarded `Pending`→`Rejected` transition, stores `DirectorNotes`, sets `DecidedBy`/`DecidedAt`) in `backend/ChildCare.Application/DayReservations/RejectDayReservationCommand.cs`
- [X] T025 [US1] Implement `ListPendingDayReservationsQuery` (status filter, defaults to `pending`, newest-`CreatedAt`-first, denormalized child name) in `backend/ChildCare.Application/DayReservations/ListPendingDayReservationsQuery.cs`
- [X] T026 [US1] Wire `POST /api/day-reservations`, `POST /{id}/approve`, `POST /{id}/reject`, `GET /api/day-reservations` in `backend/ChildCare.Api/Endpoints/DayReservationEndpoints.cs`
- [X] T027 [US1] Send Expo push notification via `IExpoPushSender` on approve/reject, including `directorNotes` on rejection, logging (not throwing) when the parent has no registered push token (FR-013) in `ApproveDayReservationCommand.cs`/`RejectDayReservationCommand.cs`
- [X] T028 [US1] Regenerate and commit `web/lib/generated/api-types.ts` and `parent-mobile/services/generated/api-types.ts` against the new endpoints
- [X] T029 [P] [US1] Create shared `DayReservationForm` component (date picker + optional reason textarea, 48pt+ touch targets) in `parent-mobile/components/DayReservationForm.tsx`
- [X] T030 [US1] Add "Mijn kind is ziek" quick action on the home screen and the absence entry screen (pushes `DayReservationForm` with `type=absence`) in `parent-mobile/app/(app)/index.tsx`, `parent-mobile/app/(app)/requests/absence.tsx`
- [X] T031 [US1] Register `requests/absence` (and future `requests/*`) as non-tab stack screens (`href: null`, per the existing `announcements` precedent) in `parent-mobile/app/(app)/_layout.tsx`
- [X] T032 [US1] Create `parent-mobile/services/dayReservations.ts` API wrapper (submit/cancel/mine)
- [X] T033 [US1] Create the director web "Verzoeken" queue page — pending list newest-first, child/date/type/reason visible inline, approve (with justified toggle for absence) / reject (with optional note) actions, no separate navigation required (FR-006, FR-007) in `web/app/(app)/requests/page.tsx`
- [X] T034 [P] [US1] Web component test: queue renders pending absence requests, approve sets `absenceJustified`, reject accepts a note in `web/__tests__/dayReservations.test.tsx`
- [X] T035 [P] [US1] parent-mobile component test: absence form submit succeeds and past-date validation surfaces the error in `parent-mobile/__tests__/dayReservations.test.tsx`

**Checkpoint**: At this point, User Story 1 (sick-day report → director decision → attendance +
notification) is fully functional and independently testable.

---

## Phase 4: User Story 2 - Director clears the request queue (Priority: P1)

**Goal**: The Verzoeken queue generalizes cleanly across all request types (not just absence):
newest-first ordering across mixed types, a clear empty state, and a capacity warning for
`extra` requests near/over location capacity.

**Independent Test**: Seed several pending requests across types directly (bypassing submission
validation, since `extra`/`exchange` submission ships in US3/US4) and verify the queue lists them
newest-first with full context, shows an explicit empty state with zero pending requests, and
shows a capacity warning for an `extra` request on a near-full date.

### Tests for User Story 2

- [X] T036 [P] [US2] Integration test: `GET /api/day-reservations` (default `status=pending`) returns mixed-type pending requests ordered newest-`CreatedAt`-first (FR-006) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T037 [P] [US2] Integration test: `GET /api/day-reservations?status=pending` with zero pending requests returns an empty array (backs the empty state) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T038 [P] [US2] Web component test: empty queue renders the empty state (icon + sentence, per design-system.md), and an `extra` request near/over location capacity renders the capacity warning in `web/__tests__/dayReservations.test.tsx`

### Implementation for User Story 2

- [X] T039 [US2] Extend `ListPendingDayReservationsQuery` to compute a per-request capacity warning for `extra`-type requests, reusing feature 012a's active-contracts-vs-`Location.MaxCapacity` occupancy computation (research.md R5) in `backend/ChildCare.Application/DayReservations/ListPendingDayReservationsQuery.cs`
- [X] T040 [US2] Add the empty state (icon + one-sentence copy) and the capacity-warning badge to the Verzoeken queue page in `web/app/(app)/requests/page.tsx`

**Checkpoint**: The director queue now generalizes across request types with proper empty/warning
states, independent of which specific request types are submittable yet.

---

## Phase 5: User Story 3 - Parent requests an extra day (Priority: P2)

**Goal**: Parent submits an `extra` request; approval only transitions status (no attendance
side effect, per research.md R2) and notifies the parent.

**Independent Test**: Parent submits an extra-day request for a future date with no matching
contracted day; director approves it; verify the reservation is `approved` and no
`AttendanceRecord` was created for that date.

### Tests for User Story 3

- [X] T041 [P] [US3] Integration test: parent submits an `extra` request for a future date, persists as `pending` (FR-001) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T042 [P] [US3] Integration test: approving an `extra` request transitions it to `approved` and creates no `AttendanceRecord` (FR-012) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] Extend `SubmitDayReservationCommand`'s validator with the `extra` branch (same past/future date handling as absence; no contracted-day/closure checks required) in `backend/ChildCare.Application/DayReservations/SubmitDayReservationCommand.cs`
- [X] T044 [US3] Extend `ApproveDayReservationCommand` to skip the `AttendanceRecord`/`MarkAbsentCommand` path entirely for non-`Absence` types in `backend/ChildCare.Application/DayReservations/ApproveDayReservationCommand.cs`
- [X] T045 [US3] Add "Extra dag aanvragen" quick action + entry screen (reuses `DayReservationForm` with `type=extra`) in `parent-mobile/app/(app)/index.tsx`, `parent-mobile/app/(app)/requests/extra.tsx`

**Checkpoint**: Extra-day requests work end-to-end without disturbing US1's absence flow.

---

## Phase 6: User Story 4 - Parent requests a day exchange (Priority: P2)

**Goal**: Parent submits an `exchange` request (giving up a real contracted day for a new date);
submission is rejected when the source day isn't actually contracted or the target date is a
closure day.

**Independent Test**: Parent submits an exchange swapping a real contracted weekday for a valid
future date — succeeds. Submitting with a non-contracted source day, or a closure-day target
date, is rejected at submission.

### Tests for User Story 4

- [X] T046 [P] [US4] Integration test: exchange request with `exchangeForDate` matching an active `ContractedDay` weekday and a valid non-closure `requestedDate` persists as `pending` (FR-001) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T047 [P] [US4] Integration test: exchange request whose `exchangeForDate` is not one of the child's contracted weekdays is rejected with `errors.day_reservations.not_contracted_day` (FR-003) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T048 [P] [US4] Integration test: exchange request whose `requestedDate` is a published closure day is rejected with `errors.day_reservations.closure_day` (FR-004) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T049 [P] [US4] Integration test: approving an `exchange` request transitions it to `approved` and creates no `AttendanceRecord` (FR-012) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`

### Implementation for User Story 4

- [X] T050 [US4] Extend `SubmitDayReservationCommand`'s validator with the `exchange` branch: require `exchangeForDate`, check it matches an active `Contract.ContractedDays` weekday for the child (research.md R6), and check `requestedDate` against `IClosureCalendarReader.IsPublishedClosureDateAsync` in `backend/ChildCare.Application/DayReservations/SubmitDayReservationCommand.cs`
- [X] T051 [US4] Add "Dagwissel aanvragen" quick action + entry screen (date picker for both `exchangeForDate` and `requestedDate`) in `parent-mobile/app/(app)/index.tsx`, `parent-mobile/app/(app)/requests/exchange.tsx`
- [X] T052 [P] [US4] parent-mobile component test: exchange form requires both dates and surfaces the contracted-day/closure-day validation errors in `parent-mobile/__tests__/dayReservations.test.tsx`

**Checkpoint**: All three request types are submittable and approvable end-to-end.

---

## Phase 7: User Story 5 - Parent withdraws a pending request (Priority: P3)

**Goal**: Parent cancels their own pending request; cannot cancel an already-decided request.

**Independent Test**: Parent cancels their own pending request — status becomes `cancelled` and
it disappears from the director's active queue. Attempting to cancel an approved/rejected
request fails cleanly.

**Note (post-`/speckit-analyze` fix, finding F1/F2)**: FR-019 (own-request history) has no
dedicated User Story priority in spec.md, but US5's own acceptance scenario requires a parent to
see their pending request before cancelling it — so its tasks live here in Phase 7, not in
Polish, to keep task execution order consistent with actual code dependency.

### Tests for User Story 5

- [X] T053 [P] [US5] Integration test: parent cancels their own pending request, status becomes `cancelled`, and it no longer appears in `GET /api/day-reservations?status=pending` (FR-014) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T054 [P] [US5] Integration test: cancelling an already-approved or already-rejected request fails with `409 errors.day_reservations.not_pending` (FR-015) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T055 [P] [US5] Integration test: parent's own-request history returns all statuses across their linked children, newest first (FR-019) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`

### Implementation for User Story 5

- [X] T056 [US5] Implement `CancelDayReservationCommand` (guarded `Pending`→`Cancelled` transition, parent-owns-child check via `ICurrentParentContactResolver`) in `backend/ChildCare.Application/DayReservations/CancelDayReservationCommand.cs`
- [X] T057 [US5] Wire `POST /api/day-reservations/{id}/cancel` in `backend/ChildCare.Api/Endpoints/DayReservationEndpoints.cs`
- [X] T058 [US5] Implement `ListMyDayReservationsQuery` (all statuses, optional `childId` filter, newest first) and wire `GET /api/day-reservations/mine` in `backend/ChildCare.Application/DayReservations/ListMyDayReservationsQuery.cs`, `backend/ChildCare.Api/Endpoints/DayReservationEndpoints.cs` (FR-019)
- [X] T059 [US5] Build the parent's own-request history screen (status per request), reachable from the home screen's quick actions, in `parent-mobile/app/(app)/requests/index.tsx`
- [X] T060 [US5] Add a cancel action to the history screen built in T059, for `pending`-status entries only

**Checkpoint**: All five user stories are independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation validation and final coverage sweep.

- [X] T061 Run `quickstart.md` scenarios 1–4 end-to-end against a local dev environment and fix any discrepancy found — covered by the 32 new automated tests (20 backend integration, 6 web, 6 parent-mobile), plus a partial live curl smoke run against the real local dev Postgres (org/location/child/contract creation, migration applies cleanly) stopped before the parent-invitation-email step once it became clear the dev environment's `Email:SmtpHost` holds real Gmail credentials — continuing would have sent real emails from the user's account, which isn't appropriate for a self-directed verification pass. The smoke run did catch two environment quirks unrelated to this feature's own code (Location.Phone format validation; `DayOfWeek` serializes as an int, not a string, with no global `JsonStringEnumConverter` configured) — noted here for future manual testing, not bugs in this feature.
- [X] T062 Verify every new user-facing string across `web/` and `parent-mobile/` resolves through i18n keys in all three locales (NL/FR/EN) — no hardcoded strings (constitution Principle IV)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only. MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational + US1's queue page/query existing to extend (T025, T033) — not independent of US1's *code*, but independently *testable* via seeded data per its own Independent Test.
- **User Story 3 (Phase 5)**: Depends on Foundational + US1's `SubmitDayReservationCommand`/`ApproveDayReservationCommand` files existing to extend.
- **User Story 4 (Phase 6)**: Same as US3 — extends the same shared command files.
- **User Story 5 (Phase 7)**: Depends on Foundational only (cancel is independent of approve/reject).
- **Polish (Phase 8)**: Depends on all desired user stories being complete.

### Parallel Opportunities

- All Setup tasks marked [P] run in parallel.
- All Foundational tasks marked [P] run in parallel (T007/T008/T013).
- All US1 test tasks (T014–T021) run in parallel — different test methods in the same file, written before implementation.
- T029 (shared form component) can proceed in parallel with backend US1 tasks.
- US3 and US4 can be implemented in parallel by different developers once US1 lands, since they touch different validator branches (though the same file — expect a small merge, not a conflict in logic).

---

## Parallel Example: User Story 1

```bash
# Tests first (all in the same file, but independent test methods):
Task: "Integration test: parent submits an absence request"
Task: "Integration test: absence request >1 day in the past is rejected"
Task: "Integration test: parent cannot submit for an unlinked child"
Task: "Integration test: director approves absence request, attendance + notification created"
Task: "Integration test: director rejects with a note, notification includes it"
Task: "Integration test: approval on a since-closure-day date fails cleanly"
Task: "Integration test: concurrent decisions on the same request — only one wins"

# Frontend form component, independent of backend commands:
Task: "Create shared DayReservationForm component in parent-mobile/components/DayReservationForm.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (blocks everything).
3. Complete Phase 3: User Story 1 — sick-day report, director decision, attendance +
   notification.
4. **STOP and VALIDATE**: run quickstart.md Scenario 1 end-to-end.
5. This alone delivers the highest-frequency real-world case (illness reporting).

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → MVP: absence request lifecycle complete.
3. US2 → queue generalizes across types, empty/warning states.
4. US3 → extra-day requests.
5. US4 → exchange-day requests (most validation-heavy of the three types).
6. US5 → parent cancellation + own-request history (FR-019, needed for the cancel flow itself).
7. Polish → i18n/quickstart sweep.

## Notes

- [P] tasks touch different files, or are independent test methods appended to the same test
  file before any implementation exists for them — safe to parallelize per this repo's own
  precedent (e.g. 012a's T014–T018).
- Commit after each task or logical group, per user's global commit-discipline convention.
- `SubmitDayReservationCommand` and `ApproveDayReservationCommand` are shared files extended
  incrementally across US1/US3/US4 (one command, `Type`-branched validation) rather than three
  separate commands — this mirrors the single-entity, type-discriminated pattern already used by
  `child_events` (constitution: one JSONB table, not one table per event type).
