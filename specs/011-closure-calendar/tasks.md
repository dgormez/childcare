# Tasks: Closure Calendar

**Input**: Design documents from `specs/011-closure-calendar/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by spec Technical Requirements and quickstart.md.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish shared backend/web surfaces and route registration.

- [X] T001 [P] Add closure request contracts in `backend/ChildCare.Contracts/Requests/ClosureCalendarRequests.cs`
- [X] T002 [P] Add closure response contracts in `backend/ChildCare.Contracts/Responses/ClosureCalendarResponses.cs`
- [X] T003 [P] Add closure i18n keys to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, and `web/i18n/locales/nl.json`
- [X] T004 Register the director web closure route in `web/components/Sidebar.tsx`
- [X] T005 Register backend closure endpoints in `backend/ChildCare.Api/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core closure entities, persistence, mapping, and shared result types that all stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 [P] Create `ClosureType`, `ClosureStatus`, and `ClosureNotificationKind` enums in `backend/ChildCare.Domain/Enums/`
- [X] T007 [P] Create `KdvClosureDay` entity in `backend/ChildCare.Domain/Entities/KdvClosureDay.cs`
- [X] T008 [P] Create `ClosureNotificationDelivery` entity in `backend/ChildCare.Domain/Entities/ClosureNotificationDelivery.cs`
- [X] T009 [P] Create `ParentClosureMessage` entity in `backend/ChildCare.Domain/Entities/ParentClosureMessage.cs`
- [X] T010 Add closure DbSets, enum conversions, indexes, uniqueness, and relationships in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T011 Add tenant migration for closure calendar tables and attendance audit/source columns in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [X] T012 Add closure DbSets to `backend/ChildCare.Application/Common/ITenantDbContext.cs`
- [X] T013 [P] Create `ClosureCalendarResult`, failure enum, and mapper in `backend/ChildCare.Application/ClosureCalendar/ClosureCalendarResult.cs`
- [X] T014 [P] Create `IClosureCalendarReader` in `backend/ChildCare.Application/Common/IClosureCalendarReader.cs`
- [X] T015 Create `ClosureCalendarEndpoints` route group with `DirectorOnly` authorization in `backend/ChildCare.Api/Endpoints/ClosureCalendarEndpoints.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Maintain Location Closure Calendar (Priority: P1) MVP

**Goal**: Director can create/edit/list draft closure days per location/year.

**Independent Test**: Create closure days for one location and verify only that location/year shows them; duplicate and past-date validation fail.

### Tests for User Story 1

- [X] T016 [P] [US1] Integration test: director creates and lists closure days by location/year in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T017 [P] [US1] Integration test: duplicate `(locationId, date)` returns `409 errors.closures.duplicate_date` in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T018 [P] [US1] Integration test: past closure date returns `400 errors.closures.past_date` in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T019 [P] [US1] Integration test: staff/parent tokens cannot create/list/update closure days in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T020 [P] [US1] Web component test: closure page loads locations/year data and renders empty/loading/error states in `web/__tests__/closures.test.tsx`

### Implementation for User Story 1

- [X] T021 [P] [US1] Implement `ListClosureDaysQuery` in `backend/ChildCare.Application/ClosureCalendar/ListClosureDaysQuery.cs`
- [X] T022 [P] [US1] Implement `CreateClosureDayCommand` validator/handler in `backend/ChildCare.Application/ClosureCalendar/CreateClosureDayCommand.cs`
- [X] T023 [P] [US1] Implement `UpdateClosureDayCommand` validator/handler in `backend/ChildCare.Application/ClosureCalendar/UpdateClosureDayCommand.cs`
- [X] T024 [US1] Map `GET /api/closures`, `POST /api/closures`, and `PATCH /api/closures/{id}` in `backend/ChildCare.Api/Endpoints/ClosureCalendarEndpoints.cs`
- [X] T025 [US1] Add generated OpenAPI types/client updates for closure endpoints in `web/lib/generated/api-types.ts`
- [X] T026 [P] [US1] Implement `ClosureCalendar` year grid component in `web/components/ClosureCalendar.tsx`
- [X] T027 [P] [US1] Implement `ClosureList` dense summary component in `web/components/ClosureList.tsx`
- [X] T028 [P] [US1] Implement `ClosureDialog` create/edit form in `web/components/ClosureDialog.tsx`
- [X] T029 [US1] Implement closure page data loading/actions in `web/app/(app)/closures/page.tsx`

**Checkpoint**: User Story 1 should be fully functional and independently testable.

---

## Phase 4: User Story 2 - Publish Closures and Notify Parents (Priority: P2)

**Goal**: Director publishes closures and affected parents receive immediate push plus one-way in-app messages.

**Independent Test**: Publish a notify-enabled closure for enrolled children and verify deduplicated parent messages and push attempts.

### Tests for User Story 2

- [X] T030 [P] [US2] Integration test: notify-enabled publish sends push attempts and creates parent closure messages in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T031 [P] [US2] Integration test: `notifyParents=false` publish creates no parent push/message in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T032 [P] [US2] Integration test: parent linked to multiple children receives one message for one closure in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T033 [P] [US2] Integration test: push failure records failed delivery but publish remains successful in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T034 [P] [US2] Web component test: publish action shows notification summary/partial failure state in `web/__tests__/closures.test.tsx`

### Implementation for User Story 2

- [X] T035 [P] [US2] Implement parent recipient resolver from active contracts/contacts in `backend/ChildCare.Application/ClosureCalendar/ClosureParentRecipientResolver.cs`
- [X] T036 [P] [US2] Implement `ClosureNotificationService` using `IExpoPushSender` and parent message records in `backend/ChildCare.Application/ClosureCalendar/ClosureNotificationService.cs`
- [X] T037 [US2] Implement `PublishClosureDayCommand` notification path in `backend/ChildCare.Application/ClosureCalendar/PublishClosureDayCommand.cs`
- [X] T038 [US2] Map `POST /api/closures/{id}/publish` notification response handling in `backend/ChildCare.Api/Endpoints/ClosureCalendarEndpoints.cs`
- [X] T039 [US2] Add publish UI action and delivery summary rendering in `web/app/(app)/closures/page.tsx`

**Checkpoint**: User Stories 1 and 2 should work independently.

---

## Phase 5: User Story 3 - Apply Closures to Attendance (Priority: P2)

**Goal**: Published closures generate attendance `closure` records and preserve check-in blocking.

**Independent Test**: Publish closure for enrolled children, verify attendance closure records and check-in rejection.

### Tests for User Story 3

- [X] T040 [P] [US3] Integration test: publish creates `status=closure` attendance records for enrolled children in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T041 [P] [US3] Integration test: check-in against published closure date returns existing `errors.attendance.closure_day` in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T042 [P] [US3] Integration test: same-day checked-in children require confirmation before closure status is applied in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T043 [P] [US3] Integration test: confirmed same-day closure preserves prior attendance audit/source data in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T044 [P] [US3] Integration test: billable-exclusion query returns only published non-cancelled dates in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`

### Implementation for User Story 3

- [X] T045 [P] [US3] Implement `ClosureAttendanceService` in `backend/ChildCare.Application/ClosureCalendar/ClosureAttendanceService.cs`
- [X] T046 [US3] Extend `PublishClosureDayCommand` to generate closure attendance records and return confirmation-required conflicts in `backend/ChildCare.Application/ClosureCalendar/PublishClosureDayCommand.cs`
- [X] T047 [US3] Persist closure source/prior attendance audit fields in `backend/ChildCare.Domain/Entities/AttendanceRecord.cs`
- [X] T048 [US3] Implement `IClosureCalendarReader` and billable exclusion query handler in `backend/ChildCare.Application/ClosureCalendar/ListBillableClosureDatesQuery.cs`
- [X] T049 [US3] Map `GET /api/closures/billable-exclusions` in `backend/ChildCare.Api/Endpoints/ClosureCalendarEndpoints.cs`
- [X] T050 [US3] Add checked-in attendance confirmation dialog copy/flow in `web/app/(app)/closures/page.tsx`

**Checkpoint**: User Story 3 should complete the attendance integration.

---

## Phase 6: User Story 4 - Cancel a Published Closure (Priority: P3)

**Goal**: Director cancels/removes closures and parents are notified when a published closure is cancelled.

**Independent Test**: Publish and cancel a closure; verify cancellation messages and attendance release behavior.

### Tests for User Story 4

- [X] T051 [P] [US4] Integration test: cancelling a published notified closure sends cancellation push/message in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T052 [P] [US4] Integration test: cancelling a draft removes it with no parent notification in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T053 [P] [US4] Integration test: cancelling releases only system-generated closure attendance records and preserves manually changed records in `backend/ChildCare.Api.Tests/ClosureCalendarTests.cs`
- [X] T054 [P] [US4] Web component test: cancel/remove confirmation uses i18n labels and updates row state in `web/__tests__/closures.test.tsx`

### Implementation for User Story 4

- [X] T055 [US4] Implement `CancelClosureDayCommand` with draft delete and published soft-cancel paths in `backend/ChildCare.Application/ClosureCalendar/CancelClosureDayCommand.cs`
- [X] T056 [US4] Extend `ClosureNotificationService` for cancellation messages in `backend/ChildCare.Application/ClosureCalendar/ClosureNotificationService.cs`
- [X] T057 [US4] Extend `ClosureAttendanceService` for cancellation release/preserve behavior in `backend/ChildCare.Application/ClosureCalendar/ClosureAttendanceService.cs`
- [X] T058 [US4] Map `POST /api/closures/{id}/cancel` in `backend/ChildCare.Api/Endpoints/ClosureCalendarEndpoints.cs`
- [X] T059 [US4] Add cancel/remove UI flow in `web/app/(app)/closures/page.tsx`

**Checkpoint**: All user stories should be independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation, design compliance, generated API hygiene, and workflow documentation.

- [X] T060 [P] Add closure calendar workflow note to `.specify/memory/workflows.md` and/or `.specify/memory/Workflows/attendance.md`
- [X] T061 [P] Ensure all closure API/web/parent message strings have NL/FR/EN i18n keys in `web/i18n/locales/` and backend error-key mappings
- [X] T062 [P] Static design review for `web/app/(app)/closures/page.tsx`, `web/components/ClosureCalendar.tsx`, `web/components/ClosureDialog.tsx`, and `web/components/ClosureList.tsx` against `.specify/memory/design-system.md`
- [X] T063 Run backend quickstart validation with `dotnet test backend/ChildCare.sln`
- [X] T064 Run web quickstart validation with `cd web && npm test -- --run && npm run typecheck`
- [X] T065 Regenerate/update OpenAPI-derived web client types if backend contract changes in `web/lib/generated/api-types.ts`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 Setup: no dependencies.
- Phase 2 Foundational: depends on Phase 1 and blocks all user stories.
- Phase 3 US1: depends on Phase 2.
- Phase 4 US2: depends on US1 create/list/publish target records.
- Phase 5 US3: depends on US1 and shares the US2 publish command path.
- Phase 6 US4: depends on published closure lifecycle from US2/US3.
- Phase 7 Polish: depends on all desired stories.

### User Story Dependencies

- US1 is MVP and can be validated alone.
- US2 needs US1 closure records but is independently testable with notify on/off.
- US3 needs US1 closure records and integrates through the publish command.
- US4 needs published closures from US2/US3.

### Parallel Opportunities

- T001-T004 can run in parallel.
- T006-T009, T013-T014 can run in parallel.
- Tests within each story marked [P] can be written together before implementation.
- US1 web components T026-T028 can be built in parallel after API shapes are stable.
- US2 notification resolver/service work T035-T036 can run in parallel.

## Parallel Example: User Story 1

```text
Task: "Integration test: director creates and lists closure days by location/year in backend/ChildCare.Api.Tests/ClosureCalendarTests.cs"
Task: "Integration test: duplicate (locationId, date) returns 409 in backend/ChildCare.Api.Tests/ClosureCalendarTests.cs"
Task: "Web component test: closure page loads locations/year data and renders empty/loading/error states in web/__tests__/closures.test.tsx"
```

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete US1.
3. Validate create/list/edit calendar behavior before adding publish side effects.

### Incremental Delivery

1. US1 adds location-year closure management.
2. US2 adds parent notification/message publishing.
3. US3 adds attendance and invoicing integration.
4. US4 adds cancellation and cleanup.
5. Polish locks i18n, design compliance, and test/build validation.

## Notes

- [P] tasks affect different files or can be authored independently.
- Tests should be written before implementation and initially fail.
- Mark each task `[X]` in this file when completed.
