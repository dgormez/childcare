---

description: "Task list for feature 009-child-events"
---

# Tasks: Child Event Timeline

**Input**: Design documents from `/specs/009-child-events/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/child-events-api.md, quickstart.md

**Tests**: Included — constitution Principle V (Test with Real Infrastructure) requires
TestContainers-backed integration tests covering happy path plus key negative/regulatory flows;
spec.md's Technical Requirements explicitly calls out payload validation, offline merge,
temperature trigger, edit-window enforcement, and visibility filtering as required coverage.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to spec.md's User Story 1–4

## Path Conventions

Backend: `backend/ChildCare.{Domain,Application,Contracts,Infrastructure,Api}/...` (existing
five-project solution). Mobile: `mobile/{services,components,app,i18n}/...` (existing Expo app).
Per plan.md's Project Structure.

---

## Phase 1: Setup

- [X] T001 [P] Create `ChildEventType` enum (`sleep`, `temperature`, `medication`,
  `feeding_bottle`, `feeding_solid`, `diaper`, `mood`, `activity`, `note`, `weight`,
  `measurement`) in `backend/ChildCare.Domain/Enums/ChildEventType.cs`
- [X] T002 [P] Add an empty `childEvents` top-level key to each of
  `mobile/i18n/locales/nl.json`, `mobile/i18n/locales/fr.json`, `mobile/i18n/locales/en.json`
  (i18n here is one flat namespaced JSON file per locale, not a per-locale directory — matches
  the existing `child`/`groupView` key convention)

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every story reads
or writes `ChildEvent` through this entity/table/endpoint-group/DTO layer.

- [X] T003 Create `ChildEvent` entity per data-model.md in
  `backend/ChildCare.Domain/Entities/ChildEvent.cs` (`Id`, `ChildId`, `LocationId`, `GroupId`,
  `EventType`, `OccurredAt`, `EndedAt`, `Payload`, `VisibleToParent`, `RecordedBy` (`Guid[]`),
  `AdministeredBy`, `RecordedByDeviceId`, `DeletedAt`, `CreatedAt`, `UpdatedAt`) — `LocationId`/
  `GroupId` are sourced from the recording device's claims at write time (same values
  `IShiftAttributionService` needs) and are what FR-006's edit-window check authorizes against
- [X] T004 Add `DbSet<ChildEvent> ChildEvents` to `ITenantDbContext` and
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`, mapping `Payload` as
  `HasColumnType("jsonb")` (string column, research.md R1), `RecordedBy` as a `jsonb` array, and
  configuring the `(ChildId, OccurredAt DESC)` and `(ChildId, EventType, OccurredAt DESC)`
  composite indexes from data-model.md
- [X] T005 Generate the EF Core tenant migration for the new `child_events` table (`dotnet ef
  migrations add AddChildEvents --project backend/ChildCare.Infrastructure --context
  TenantDbContext --output-dir Persistence/Migrations/Tenant`) and verify it applies cleanly
  against a fresh dev schema
- [X] T006 [P] Create request/response contracts per contracts/child-events-api.md in
  `backend/ChildCare.Contracts/Requests/ChildEventRequests.cs` (`RecordChildEventRequest`,
  `UpdateChildEventRequest`) and `backend/ChildCare.Contracts/Responses/ChildEventResponses.cs`
  (`ChildEventResponse`, `DailySummaryResponse`, `PagedChildEventsResponse`)
- [X] T007 [P] Create `ChildEventPayloadValidator` (FluentValidation, one rule set per
  `EventType` from data-model.md's Validation Rules table, including the FR-002a numeric ranges
  and the non-empty-subset rule for `measurement`) in
  `backend/ChildCare.Application/ChildEvents/ChildEventPayloadValidator.cs`
- [X] T007a [P] Create a shared `IBelgianCalendarDay` (or equivalent static) helper resolving
  "today" and converting a `DateTime` to its `Europe/Brussels` calendar day, in
  `backend/ChildCare.Application/Common/BelgianCalendarDay.cs` — the single implementation
  `ChildEventEditWindowPolicy` (T008) and `GetDailySummaryQuery` (T045) both call, so FR-018a's
  "same boundary" guarantee can't silently drift between the two (analyze finding C2)
- [X] T008 [P] Create `ChildEventEditWindowPolicy` (director JWT → always allowed; device token →
  allowed only when `OccurredAt`'s `Europe/Brussels` calendar day (via T007a) equals today AND
  the requesting device's own `LocationId` claim matches the event's `LocationId` — corrected
  during implementation from a per-caregiver `StaffLocationEligibility` check, which isn't
  implementable since no individual caregiver identity exists on a device-token-authenticated
  request, per research.md R4) in
  `backend/ChildCare.Application/ChildEvents/ChildEventEditWindowPolicy.cs` (depends on T007a)
- [X] T009 Scaffold `ChildEventEndpoints.cs` with an empty `/api/child-events` device-token-
  authenticated `MapGroup` (mirrors `RoomShiftEndpoints.cs`'s structure) in
  `backend/ChildCare.Api/Endpoints/ChildEventEndpoints.cs`, and wire
  `app.MapChildEventEndpoints();` into `backend/ChildCare.Api/Program.cs`
- [X] T010 [P] Create `mobile/services/childEvents.ts` skeleton: typed request/response
  interfaces matching contracts/child-events-api.md and a `registerSyncHandler("child_event",
  {...})` call (empty handler body for now) so `syncEngine.ts` recognizes the entity type

**Checkpoint**: `child_events` table exists, endpoint group is mounted (returns 404 on unmapped
routes but the group/auth middleware is live), contracts compile. User story phases can begin.

---

## Phase 3: User Story 1 - Caregiver records a routine event in under 5 seconds (Priority: P1) 🎯 MVP

**Goal**: A caregiver can record sleep/temperature/medication/feeding/diaper/mood/activity/
note/weight/measurement events for a child, online or offline, and see them immediately on that
child's timeline.

**Independent Test**: Log in as a caregiver, open the group view, record a diaper change and a
bottle-feeding for a child, confirm both appear on that child's timeline with correct timestamps
— works with no other story implemented.

### Tests for User Story 1

- [X] T011 [P] [US1] Integration test: `POST /api/child-events` happy path for `diaper`,
  `feeding_bottle`, `mood` event types persists correctly and returns `201` in
  `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventTests.cs`
- [X] T012 [P] [US1] Integration test: `POST /api/child-events` rejects a payload with fields
  outside the selected `EventType`'s shape, a missing required field, an empty `measurement`
  payload, or a numeric field outside its FR-002a range, all with `422 errors.validation` (the
  standard `ValidationBehavior` pipeline response), in
  `backend/ChildCare.Api.Tests/ChildEvents/ChildEventPayloadValidationTests.cs`
- [X] T012a [P] [US1] Integration test: `POST /api/child-events` retried with an `id` that
  already exists returns the existing record instead of creating a duplicate (FR-013a), in
  `backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventTests.cs`
- [X] T013 [P] [US1] Integration test: a `sleep` event created with no `EndedAt` shows as
  in-progress; a subsequent `PATCH` with `endedAt` completes it and `payload.durationMinutes`
  reflects the elapsed time, in
  `backend/ChildCare.Api.Tests/ChildEvents/SleepEventLifecycleTests.cs`
- [X] T014 [P] [US1] Integration test: `GET /api/child-events?childId=...&limit=...` paginates
  correctly via the `before` cursor with no gaps or overlap across pages, in
  `backend/ChildCare.Api.Tests/ChildEvents/ListChildEventsPaginationTests.cs`
- [X] T015 [P] [US1] Unit test: `mobile/__tests__/services/childEvents.test.ts` — sleep-end
  merge behavior (research.md R3): queuing an end-update while the create is still pending
  merges `endedAt` into the existing queued create row instead of adding a second row; queuing
  an end-update after the create has already synced adds a normal `PATCH` row

### Implementation for User Story 1

- [X] T016 [US1] Implement `RecordChildEventCommand`/handler — validates payload (T007), resolves
  `RecordedBy` via `IShiftAttributionService.ResolveRecordedByAsync` (research.md R2), returns the
  existing record on a repeat `id` instead of inserting a duplicate (FR-013a), persists the event
  — in `backend/ChildCare.Application/ChildEvents/RecordChildEventCommand.cs`
- [X] T017 [US1] Implement `ListChildEventsQuery`/handler (cursor pagination on `(OccurredAt
  DESC, Id)`, `DeletedAt IS NULL`, per research.md R6) in
  `backend/ChildCare.Application/ChildEvents/ListChildEventsQuery.cs`
- [X] T018 [US1] Wire `POST /api/child-events` and `GET /api/child-events` into
  `backend/ChildCare.Api/Endpoints/ChildEventEndpoints.cs` (depends on T016, T017)
- [X] T019 [P] [US1] Build `QuickActionSheet` component (bottom sheet, not full-screen modal;
  icon-based, 64pt touch targets per design-system.md/platform-rules.md) in
  `mobile/components/QuickActionSheet.tsx`
- [X] T020 [P] [US1] Build `EventTimeline` component (chronological list, "pending sync" badge
  per design-system.md's badge component, "in progress" state for open sleep events, and a
  distinct "needs review" badge/state for a queued event whose sync attempt returned a hard
  validation rejection rather than a transient error — FR-014a, analyze finding C1; depends on
  T022b's `sync_error` marker) in `mobile/components/EventTimeline.tsx`
- [X] T021 [US1] Wire `QuickActionSheet` + `EventTimeline` into
  `mobile/app/(app)/child/[id].tsx`, replacing/extending the current medical-quick-access-only
  screen (depends on T019, T020)
- [X] T022 [US1] Implement `mobile/services/childEvents.ts`'s create/list API calls and the
  `child_event` sync handler's `onBeforeEnqueue` merge logic (research.md R3, T015's test target)
  (depends on T010)
- [X] T022a [US1] Fix `mobile/services/syncEngine.ts`'s `replay()` to re-read each row's current
  `payload` from local storage immediately before transmitting, instead of the in-memory batch
  snapshot (research.md R3, CHK008) — closes a race where a sleep-end merge landing after the
  batch read but before that row's send would otherwise ship the stale pre-merge payload
- [X] T022b [US1] Fix `mobile/services/syncEngine.ts`'s response handling to treat a `422` as a
  permanent rejection (mark the row with a distinct `sync_error` value the UI can recognize as
  "needs review," per FR-014a) rather than falling into the existing transient-error branch that
  retries indefinitely — the current code only special-cases `409`/`401`, everything else
  (including `422`) is treated as retriable
- [X] T023 [P] [US1] Add i18n keys (event-type labels, quick-action button labels, pending-sync
  badge text, empty-state copy) to `mobile/i18n/locales/{nl,fr,en}.json's childEvents key`

**Checkpoint**: User Story 1 is fully functional and independently testable — routine event
recording works online and offline with the sleep in-progress/complete lifecycle.

---

## Phase 4: User Story 2 - Temperature reading alerts guardians when high (Priority: P1)

**Goal**: A temperature reading above 38.0°C triggers a push-notification attempt to every
`can_pickup` contact with a registered device; medication/temperature events support the
select-then-PIN administrator-confirmation step reused from feature 008a.

**Independent Test**: Record a temperature reading above the threshold for a child with a
`can_pickup` contact and a registered push token, confirm a push notification is attempted —
independent of any other event type.

### Tests for User Story 2

- [X] T024 [P] [US2] Integration test: a `temperature` event with `celsius > 38.0` triggers
  `ITemperatureAlertService` exactly once, targeting every `ChildContact` with `CanPickup =
  true`, in `backend/ChildCare.Api.Tests/ChildEvents/TemperatureAlertTests.cs`
- [X] T025 [P] [US2] Integration test: a `temperature` event with `celsius <= 38.0` does not
  trigger the alert service, in the same file
- [X] T026 [P] [US2] Integration test: a child with zero eligible contacts (or none with a
  registered token) still saves the temperature event successfully with no exception, in the
  same file
- [X] T026a [P] [US2] Integration test: a simulated dispatch-transport failure (T028's push
  sender throws) still saves the temperature event successfully (FR-011a), and two separate
  qualifying temperature events for the same child each trigger their own independent alert
  attempt with no de-duplication (FR-011b), in the same file
- [X] T027 [P] [US2] Integration test: `administeredByStaffId` (set only after a successful
  `POST /api/room-shifts/confirm-administrator` call) persists on `medication`/`temperature`
  events as `AdministeredBy`; omitted/skipped leaves it `null`, in
  `backend/ChildCare.Api.Tests/ChildEvents/AdministratorAttributionTests.cs`

### Implementation for User Story 2

- [X] T028 [US2] Create `ITemperatureAlertService`/`ExpoPushSender` (Expo Push Notification
  Service HTTP client, per constitution's Technology Stack Constraints) in
  `backend/ChildCare.Infrastructure/Push/ExpoPushSender.cs`, registered in
  `backend/ChildCare.Api/Program.cs` (`AddScoped<ITemperatureAlertService, ...>`, following the
  existing `IShiftAttributionService` registration pattern)
- [X] T029 [US2] Wire the >38.0°C threshold check into `RecordChildEventCommandHandler` (T016) to
  invoke `ITemperatureAlertService` for `temperature` events — logging and continuing (never
  failing the save) both when no recipients are resolvable (FR-011) and when the dispatch call
  itself throws (FR-011a) — with no de-duplication across separate qualifying events (FR-011b)
  (depends on T016, T028)
- [X] T030 [US2] Extend `RecordChildEventCommand`/handler to accept and persist
  `AdministeredByStaffId` for `medication`/`temperature` events (depends on T016)
- [X] T031 [US2] Wire the reused `AdministratorConfirmation.tsx` (feature 008a) into
  `QuickActionSheet`'s medication/temperature flow before submit, in
  `mobile/components/QuickActionSheet.tsx` (depends on T019)
- [X] T032 [P] [US2] Add i18n keys for medication/temperature entry fields and the administrator-
  confirmation prompt to `mobile/i18n/locales/{nl,fr,en}.json's childEvents key`

**Checkpoint**: User Stories 1 and 2 both work independently — temperature alerting and
administrator attribution are live on top of routine event recording.

---

## Phase 5: User Story 3 - Correcting a mistaken or wrong-child event (Priority: P2)

**Goal**: A caregiver can edit/delete any same-day event for a child at their location; a
director can edit/delete any event regardless of age. Deletes are soft.

**Independent Test**: Record an event as a caregiver, edit it same-day, confirm a caregiver
cannot edit a prior-day event while a director can — independent of other stories.

### Tests for User Story 3

- [X] T033 [P] [US3] Integration test: a caregiver can edit/delete a same-day event recorded by
  another caregiver at the same location (research.md R4 — team correction model), in
  `backend/ChildCare.Api.Tests/ChildEvents/ChildEventEditWindowTests.cs`
- [X] T034 [P] [US3] Integration test: a caregiver attempting to edit/delete a prior-day event
  receives `403 errors.child_events.edit_window_expired`, in the same file
- [X] T035 [P] [US3] Integration test: a director can edit/delete any event regardless of date,
  in the same file
- [X] T035a [P] [US3] Integration test: a same-day event is edited/deleted successfully via a
  device token whose `LocationId` claim matches the event's location, and rejected (`403`) via a
  device token paired to a *different* location — FR-006, research.md R4, in the same file
- [X] T036 [P] [US3] Integration test: a deleted event is excluded from `GET
  /api/child-events` and `GET /api/child-events/daily-summary` but the row still exists in the
  database with `DeletedAt` set, in
  `backend/ChildCare.Api.Tests/ChildEvents/SoftDeleteTests.cs`

### Implementation for User Story 3

- [X] T037 [US3] Implement `UpdateChildEventCommand`/handler, applying
  `ChildEventEditWindowPolicy` (T008) before persisting changes, re-validating the merged
  payload (T007) in `backend/ChildCare.Application/ChildEvents/UpdateChildEventCommand.cs`
- [X] T038 [US3] Implement `DeleteChildEventCommand`/handler (soft-delete, same
  `ChildEventEditWindowPolicy` check) in
  `backend/ChildCare.Application/ChildEvents/DeleteChildEventCommand.cs`
- [X] T039 [US3] Add a new `DeviceOrDirector` authorization policy in `Program.cs`
  (`AddAuthenticationSchemes("DeviceToken", JwtBearerDefaults.AuthenticationScheme)` +
  `RequireAuthenticatedUser()`) and wire `PATCH /api/child-events/{id}` and
  `DELETE /api/child-events/{id}` into `backend/ChildCare.Api/Endpoints/ChildEventEndpoints.cs`
  using it — no existing endpoint accepts both auth types on one route (contracts/
  child-events-api.md correction), so the handler branches explicitly on which claim type is
  present (depends on T037, T038)
- [X] T040 [P] [US3] Add edit/delete affordances to `EventTimeline` entries (visible only within
  the same-day window for a caregiver session, always visible for a director session) in
  `mobile/components/EventTimeline.tsx`
- [X] T041 [P] [US3] Register `update`/`delete` offline-queue operations in
  `mobile/services/childEvents.ts`'s `child_event` sync handler (depends on T022)

**Checkpoint**: User Stories 1–3 all work independently — correction workflow complete.

---

## Phase 6: User Story 4 - Parent-facing daily summary available for consumption (Priority: P3)

**Goal**: A computed daily summary (counts + latest values) is available per child/date via API,
excluding staff-internal events — ready for the future parent-app feature to consume.

**Independent Test**: Record a mix of visible and staff-internal events for a child on a given
day, request that day's summary, confirm counts/latest-values are correct and staff-internal
events are excluded — independent of any parent-facing UI existing.

### Tests for User Story 4

- [X] T042 [P] [US4] Integration test: daily summary counts (`napsCount`, `bottlesCount`,
  `diaperChangesCount`) and latest values (`latestMood`, `latestTemperatureCelsius`,
  `medicationAdministered`) match a known set of recorded events, in
  `backend/ChildCare.Api.Tests/ChildEvents/DailySummaryTests.cs`
- [X] T043 [P] [US4] Integration test: an event with `visibleToParent = false` is excluded from
  the summary's counts and latest values, in the same file
- [X] T044 [P] [US4] Integration test: a child/date with no events returns an all-zero/null
  summary with `200`, never an error, in the same file

### Implementation for User Story 4

- [X] T045 [US4] Implement `GetDailySummaryQuery`/handler (aggregates `ChildEvent` rows per
  data-model.md's Daily Summary derivation table, filtering `DeletedAt IS NULL AND
  VisibleToParent = true`, using T007a's shared helper to resolve the requested date's
  `Europe/Brussels` day boundary per FR-018a) in
  `backend/ChildCare.Application/ChildEvents/GetDailySummaryQuery.cs` (depends on T007a)
- [X] T046 [US4] Wire `GET /api/child-events/daily-summary` into
  `backend/ChildCare.Api/Endpoints/ChildEventEndpoints.cs` (depends on T045)

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T047 [P] Write `Workflows/dailycare.md` and `Workflows/health-safety.md` (both named as
  "no detail file yet" placeholders in `.specify/memory/workflows.md`'s Workflow Map) documenting
  the Child Event Timeline as their first real implementation, per this feature's Product Context
  Workflow Boundary section and `workflows.md`'s governance rules (document what changed, why,
  which features affected)
- [X] T048 Run `quickstart.md`'s full validation sequence (backend curl steps + manual mobile
  steps) end-to-end against a local dev environment
- [X] T049 Review every new mobile file (`QuickActionSheet.tsx`, `EventTimeline.tsx`,
  `child/[id].tsx`, `childEvents.ts`) for hardcoded user-facing strings — constitution Principle
  IV compliance sweep
- [X] T050 Run the full backend (`dotnet test`) and mobile (`npm test`) suites and fix any
  regressions before handoff

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001's enum is referenced by T003). Blocks all
  user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only. This is the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational; T029/T030 additionally depend on User
  Story 1's `RecordChildEventCommand` (T016) existing, since it extends that same handler rather
  than duplicating event-creation logic.
- **User Story 3 (Phase 5)**: Depends on Foundational only for its own CRUD paths, but its tests
  are more meaningful once US1's create path (T016) exists to generate events to edit/delete.
- **User Story 4 (Phase 6)**: Depends on Foundational only for the query itself, but is only
  meaningfully testable once US1 (and ideally US2/US3) have created some events to aggregate.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Within Each User Story

- Tests before implementation (write and confirm failing first).
- Commands/queries before endpoint wiring.
- Backend endpoint before the mobile screen/service code that calls it.

### Parallel Opportunities

- All Phase 1 tasks (`T001`, `T002`) in parallel.
- `T006`, `T007`, `T008`, `T010` (Phase 2) in parallel once `T003`–`T005` land (they don't touch
  the same files).
- All test tasks within a single user story phase marked `[P]` in parallel (different test
  files).
- `T019`/`T020` (US1 mobile components) in parallel; `T023` (i18n) in parallel with both.
- US2's tests (`T024`–`T027`) in parallel; US3's tests (`T033`–`T036`) in parallel; US4's tests
  (`T042`–`T044`) in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests together:
Task: "Integration test: POST /api/child-events happy path in backend/ChildCare.Api.Tests/ChildEvents/RecordChildEventTests.cs"
Task: "Integration test: payload validation rejection in backend/ChildCare.Api.Tests/ChildEvents/ChildEventPayloadValidationTests.cs"
Task: "Integration test: sleep in-progress/completed lifecycle in backend/ChildCare.Api.Tests/ChildEvents/SleepEventLifecycleTests.cs"

# Mobile components together:
Task: "Build QuickActionSheet component in mobile/components/QuickActionSheet.tsx"
Task: "Build EventTimeline component in mobile/components/EventTimeline.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (Setup) + Phase 2 (Foundational).
2. Complete Phase 3 (User Story 1) — routine event recording, online and offline.
3. **STOP and VALIDATE**: run quickstart.md steps 1/3/6 (backend) and the mobile routine-entry
   steps.
4. This alone is a demonstrable, valuable increment (caregivers can log a day's routine care).

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. User Story 1 → MVP, demo-able.
3. User Story 2 → health/safety alerting layered on top.
4. User Story 3 → correction workflow layered on top.
5. User Story 4 → daily summary API, ready for the future parent-app feature.
6. Polish.

---

## Phase 8: Convergence

- [X] T051 Add a validation rule rejecting a non-null `EndedAt` for any `EventType` other than
  `Sleep` (data-model.md: "Sleep only"), in both `RecordChildEventCommandValidator`
  (`backend/ChildCare.Application/ChildEvents/RecordChildEventCommand.cs`) and
  `UpdateChildEventCommandHandler`
  (`backend/ChildCare.Application/ChildEvents/UpdateChildEventCommand.cs`) — currently
  accepted silently for any type (convergence finding F1, missing)
- [X] T052 Add a validation rule rejecting a non-null `AdministeredByStaffId` for any
  `EventType` other than `Medication`/`Temperature` (data-model.md: "Medication/temperature
  only"), in the same two handlers — currently accepted silently for any type (convergence
  finding F2, missing)
- [X] T053 Add a component-level test rendering `QuickActionSheet` and asserting the actual
  2-tap flow for `diaper`/`mood`/`feeding_bottle` (select type → select value → event
  recorded, no intermediate screens) in
  `mobile/__tests__/components/QuickActionSheet.test.tsx` (convergence finding F3, partial —
  FR-021/SC-001 currently only verified by reasoning about the code, not asserted)
