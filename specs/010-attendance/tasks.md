---

description: "Task list for feature 010-attendance"
---

# Tasks: Daily Attendance Registration

**Input**: Design documents from `/specs/010-attendance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/attendance-api.md, quickstart.md

**Tests**: Included — constitution Principle V (Test with Real Infrastructure) requires
TestContainers-backed integration tests covering happy path plus key negative/regulatory flows;
spec.md's Technical Requirements explicitly calls out the unique-constraint conflict, offline
sync conflict handling, BKR threshold boundaries, and contract-derived duration conversion as
required coverage.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to spec.md's User Story 1–4

## Path Conventions

Backend: `backend/ChildCare.{Domain,Application,Contracts,Infrastructure,Api}/...` (existing
five-project solution). Mobile: `mobile/{services,components,app,i18n}/...` (existing Expo app).
Web: `web/{app,lib}/...` (existing Next.js app). Per plan.md's Project Structure.

---

## Phase 1: Setup

- [X] T001 [P] Create `AttendanceStatus` enum (`present`, `absent`, `closure`) in
  `backend/ChildCare.Domain/Enums/AttendanceStatus.cs`
- [X] T002 [P] Add an empty `attendance` top-level key to each of
  `mobile/i18n/locales/nl.json`, `mobile/i18n/locales/fr.json`, `mobile/i18n/locales/en.json`
  (matches the existing `childEvents` key convention from feature 009)
- [X] T002a [P] Add an empty `attendance` top-level key to each of `web/messages/nl.json`,
  `web/messages/fr.json`, `web/messages/en.json` (next-intl convention, feature 007a)

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every story reads
or writes `AttendanceRecord` through this entity/table/endpoint-group/DTO layer.

- [X] T003 Create `AttendanceRecord` entity per data-model.md in
  `backend/ChildCare.Domain/Entities/AttendanceRecord.cs` (`Id`, `ChildId`, `LocationId`, `Date`,
  `Status`, `CheckInAt`, `CheckOutAt`, `PlannedDurationMinutes`, `AbsenceJustified`,
  `AbsenceReason`, `RecordedBy` (`Guid[]`), `CreatedAt`, `UpdatedAt`)
- [X] T004 Add `DbSet<AttendanceRecord> AttendanceRecords` to `ITenantDbContext` and
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`, mapping `RecordedBy` as a
  native `uuid[]` column (`HasColumnType("uuid[]")`, same pattern as `ChildEvent.RecordedBy`),
  and configuring the unique `(ChildId, LocationId, Date)` index and the
  `(LocationId, Date, Status)` index from data-model.md
- [X] T005 Generate the EF Core tenant migration for the new `attendance_records` table (`dotnet
  ef migrations add AddAttendanceRecords --project backend/ChildCare.Infrastructure --context
  TenantDbContext --output-dir Persistence/Migrations/Tenant`) and verify it applies cleanly
  against a fresh dev schema
- [X] T006 [P] Create request/response contracts per contracts/attendance-api.md in
  `backend/ChildCare.Contracts/Requests/AttendanceRequests.cs` (`AttendanceCheckInRequest`,
  `AttendanceCheckOutRequest`, `MarkAbsentRequest`, `CorrectAttendanceRequest` — the first two are
  named `Attendance*Request` rather than the shorter `CheckInRequest`/`CheckOutRequest` to avoid a
  real OpenAPI schema-id collision with `RoomShiftEndpoints.cs`'s existing same-named types,
  discovered during implementation: ASP.NET Core's default schema-id is the short type name, so
  two C# types sharing one collide silently and the generated mobile/web client ends up typed
  against the wrong shape) and
  `backend/ChildCare.Contracts/Responses/AttendanceResponses.cs` (`AttendanceRecordResponse`,
  `BkrRatioResponse`, `PagedAttendanceResponse`)
- [X] T007 [P] Create `PlannedDurationCalculator` (looks up the child's active `Contract` at the
  given `LocationId`, finds the `ContractedDay` matching the record's weekday, returns
  `(EndTime - StartTime)` in minutes, or `null` if no matching entry — research.md R6) in
  `backend/ChildCare.Application/Attendance/PlannedDurationCalculator.cs`
- [X] T008 [P] Create `AttendanceEditWindowPolicy` (director JWT → always allowed; device token →
  allowed only when the record's `Date` equals today's `Europe/Brussels` calendar day (reusing
  feature 009's `BelgianCalendarDay` helper) AND the requesting device's own `LocationId` claim
  matches the record's `LocationId` — mirrors `ChildEventEditWindowPolicy`, research.md R5) in
  `backend/ChildCare.Application/Attendance/AttendanceEditWindowPolicy.cs`
- [X] T009 Scaffold `AttendanceEndpoints.cs` with an empty `/api/attendance` device-token-
  authenticated `MapGroup` (mirrors `ChildEventEndpoints.cs`'s structure) in
  `backend/ChildCare.Api/Endpoints/AttendanceEndpoints.cs`, and wire
  `app.MapAttendanceEndpoints();` into `backend/ChildCare.Api/Program.cs`
- [X] T010 [P] Create `mobile/services/attendance.ts` skeleton: typed request/response interfaces
  matching contracts/attendance-api.md and a `registerSyncHandler("attendance_record", {...})`
  call (empty handler body for now) so `syncEngine.ts` recognizes the entity type
- [X] T010a Implement `GetTodayAttendanceQuery`/handler and wire `GET /api/attendance/today`
  (device-scoped to its own `LocationId` claim) — discovered during mobile implementation
  (not in the original contract draft): the group view needs today's per-child present/absent
  state to render check-in/out correctly on load and after another tablet's action, but every
  other read endpoint besides `bkr` is `DirectorOnly`. See contracts/attendance-api.md.

**Checkpoint**: `attendance_records` table exists, endpoint group is mounted, contracts compile.
User story phases can begin.

---

## Phase 3: User Story 1 - Caregiver checks a child in and out with one tap (Priority: P1) 🎯 MVP

**Goal**: A caregiver can check a child in and out from the group view, online or offline, with
`planned_duration_minutes` correctly derived from the child's contract.

**Independent Test**: Log in as a caregiver, open the group view, check a child in, confirm the
card reflects present status with a check-in time, check them out, confirm the card reflects
checked-out status — works with no other story implemented.

### Tests for User Story 1

- [X] T011 [P] [US1] Integration test: `POST /api/attendance/check-in` happy path creates a
  `present`-status record with `checkInAt` set and `recordedBy` populated, returns `201`, in
  `backend/ChildCare.Api.Tests/Attendance/CheckInTests.cs`
- [X] T012 [P] [US1] Integration test: a second `POST /api/attendance/check-in` for the same
  `childId`/`locationId`/`date` returns `409 errors.attendance.already_recorded` and does not
  create a second record, in the same file
- [X] T013 [P] [US1] Integration test: `POST /api/attendance/check-out` sets `checkOutAt` on the
  matching present record and returns `200`; a check-out with no matching present record returns
  `404 errors.attendance.not_found`, in
  `backend/ChildCare.Api.Tests/Attendance/CheckOutTests.cs`
- [X] T014 [P] [US1] Integration test: `planned_duration_minutes` matches the child's contracted
  hours for a weekday with a `ContractedDay` entry, and is `null` for a weekday with no matching
  entry (the "extra day" case, FR-006a), in
  `backend/ChildCare.Api.Tests/Attendance/PlannedDurationCalculatorTests.cs`
- [X] T015 [P] [US1] Integration test: checking in a child against a record whose existing
  `status = closure` is rejected with `403 errors.attendance.closure_day` (FR-015), in
  `backend/ChildCare.Api.Tests/Attendance/CheckInTests.cs`
- [X] T015a [P] [US1] Integration test: checking in a child against an existing `absent`-status
  record transitions it to `present` with `checkInAt` set and returns `200` (FR-001a), rather than
  a `409` conflict, in the same file
- [X] T015b [P] [US1] Integration test: a check-out against a record with no `checkInAt`, and a
  second check-out against a record whose `checkOutAt` is already set, both return `404
  errors.attendance.not_found` (FR-002a) without altering the existing timestamp, in
  `backend/ChildCare.Api.Tests/Attendance/CheckOutTests.cs`
- [X] T015c [P] [US1] Integration test: `planned_duration_minutes` for a child holding two
  simultaneous contracts at two different locations (feature 007 split-location case) is derived
  from the contract matching the record's own `locationId` only, never the other contract
  (FR-006), in `backend/ChildCare.Api.Tests/Attendance/PlannedDurationCalculatorTests.cs`
- [X] T016 [P] [US1] Unit test: `mobile/__tests__/services/attendance.test.ts` — `checkIn`/
  `checkOut`/`markAbsent` post directly when online; when offline, each enqueues via the offline
  queue with the correct entity type/endpoint/method (research.md R9 corrected the original plan
  here — no `onBeforeEnqueue` payload merge between check-in/check-out, since they're separate
  endpoints with different shapes and merging would silently drop the check-out; FIFO queue
  ordering alone is sufficient, see contracts/attendance-api.md)

### Implementation for User Story 1

- [X] T017 [US1] Implement `CheckInCommand`/handler — resolves `RecordedBy` via
  `IShiftAttributionService.ResolveRecordedByAsync` (research.md R1), derives
  `PlannedDurationMinutes` via `PlannedDurationCalculator` (T007, matched to the record's own
  `LocationId` only, FR-006), rejects if an existing record for this
  `childId`/`locationId`/`date` has `status = closure` (FR-015), transitions an existing
  `absent`-status record to `present` instead of conflicting (FR-001a), or rejects with `409` if
  an existing `present`-status record already exists (FR-012), persists the record — in
  `backend/ChildCare.Application/Attendance/CheckInCommand.cs`
- [X] T018 [US1] Implement `CheckOutCommand`/handler (finds the matching record by
  `childId`/`locationId`/`date` with `status = present AND checkInAt IS NOT NULL AND checkOutAt IS
  NULL`; returns not-found otherwise, per FR-002a, rather than overwriting an existing
  `checkOutAt`) in `backend/ChildCare.Application/Attendance/CheckOutCommand.cs`
- [X] T019 [US1] Wire `POST /api/attendance/check-in` and `POST /api/attendance/check-out` into
  `backend/ChildCare.Api/Endpoints/AttendanceEndpoints.cs` (depends on T017, T018)
- [X] T020 [US1] Add tap-to-check-in/out handling to the child card in
  `mobile/app/(app)/index.tsx` (single tap toggles check-in/check-out state, per spec.md's
  one-tap requirement — distinct from the existing tap-to-navigate behavior, which moves to a
  secondary affordance or long-press per implementation's own UX judgment call, documented in
  this feature's shipped-notes)
- [X] T021 [US1] Implement `mobile/services/attendance.ts`'s check-in/check-out API calls and the
  `attendance_record` sync handler's `onBeforeEnqueue` merge logic (research.md R4/contracts,
  T016's test target) and server-wins `onConflict` handler (marks `sync_error =
  "conflict: already recorded"` on `409`, does not retry) (depends on T010)
- [X] T022 [P] [US1] Add i18n keys (check-in/out status labels, pending-sync reuse of feature
  008's existing components) to `mobile/i18n/locales/{nl,fr,en}.json`'s `attendance` key

**Checkpoint**: User Story 1 is fully functional and independently testable — check-in/out works
online and offline with correct planned-duration derivation.

---

## Phase 4: User Story 2 - Caregiver sees a live BKR ratio indicator (Priority: P1)

**Goal**: The caregiver tablet shows a live, colour-coded BKR indicator computed from present
children and qualified on-duty staff, with nap-time inferred automatically.

**Independent Test**: With a known number of present children and checked-in qualified staff at a
location, request the BKR indicator and confirm it reflects the correct status — independent of
check-in/out UI specifics.

### Tests for User Story 2

- [X] T023 [P] [US2] Integration test: BKR threshold boundaries — 8 (solo, non-nap), 9×N (2+
  caregivers, non-nap), 14 (solo, nap-time inferred), 14×N (2+ caregivers, nap-time inferred) —
  in `backend/ChildCare.Api.Tests/Attendance/BkrRatioTests.cs`
- [X] T024 [P] [US2] Integration test: zero qualified staff checked in with ≥1 present child
  returns a breached/red status rather than an error or a hidden indicator (FR-007b), in the same
  file
- [X] T025 [P] [US2] Integration test: a `StudentVolunteer`-qualification staff member checked in
  does not count toward the qualified-staff denominator (FR-007a), in the same file
- [X] T026 [P] [US2] Integration test: nap-time inference's exact rounding rule (`nappingCount × 2
  >= presentCount`) — 2 napping out of 3 present qualifies as nap time, 1 out of 3 does not
  (FR-007c), in the same file
- [X] T026a [P] [US2] Integration test: a child with `status = absent` or `status = closure` is
  excluded from `presentCount` (FR-007d), in the same file
- [X] T026b [P] [US2] Integration test: `status` is `green` when present count is strictly below
  threshold, `amber` when exactly at threshold, `red` when over threshold (FR-007e), for at least
  one solo and one 2+-caregiver case, in the same file

### Implementation for User Story 2

- [X] T027 [US2] Implement `GetBkrRatioQuery`/handler — present count (T017's records,
  `status = present AND checkOutAt IS NULL`), qualified on-duty staff (reusing
  `GetRoomRosterQuery`'s roster pattern including its `CloseStaleShiftsHelper` call, excluding
  `StudentVolunteer`), nap-time inference (open `sleep` `ChildEvent`s ratio), threshold selection
  per data-model.md's BKR Ratio table — in
  `backend/ChildCare.Application/Attendance/GetBkrRatioQuery.cs`
- [X] T028 [US2] Wire `GET /api/attendance/bkr` into
  `backend/ChildCare.Api/Endpoints/AttendanceEndpoints.cs` (depends on T027)
- [X] T029 [P] [US2] Build `BkrIndicator` component (colour-coded green/amber/red, always paired
  with a text label/icon per design-system.md's accessibility rule, never colour alone) in
  `mobile/components/BkrIndicator.tsx`
- [X] T030 [US2] Wire `BkrIndicator` into `mobile/app/(app)/index.tsx`'s group view header,
  polling `GET /api/attendance/bkr` at least every 15 seconds and recomputing immediately
  (optimistic local state) within 5 seconds of a local check-in/check-out/absence action
  (FR-008a/SC-006) (depends on T029)
- [X] T031 [P] [US2] Add i18n keys for BKR status labels to
  `mobile/i18n/locales/{nl,fr,en}.json`'s `attendance` key

**Checkpoint**: User Stories 1–2 both work independently — check-in/out and the live BKR
indicator are functional together.

---

## Phase 5: User Story 3 - Caregiver or director marks a child absent (Priority: P2)

**Goal**: A caregiver or director can mark a child absent for the day with a justified/
unjustified classification and an optional reason.

**Independent Test**: Mark a child absent as justified with a reason, then as unjustified,
confirming each classification is stored correctly and the child does not appear as present in
today's count — independent of check-in/BKR stories.

### Tests for User Story 3

- [X] T032 [P] [US3] Integration test: `POST /api/attendance/absence` creates an `absent`-status
  record with `absenceJustified`/`absenceReason` stored correctly, callable by both device token
  and director JWT, in `backend/ChildCare.Api.Tests/Attendance/MarkAbsentTests.cs`
- [X] T033 [P] [US3] Integration test: a duplicate absence-mark for the same
  `childId`/`locationId`/`date` returns `409 errors.attendance.already_recorded`, in the same file
- [X] T033a [P] [US3] Integration test: an absence-mark request and a check-in request racing for
  the same `childId`/`locationId`/`date` result in exactly one record persisted and the losing
  request receiving `409` (FR-005), in the same file
- [X] T034 [P] [US3] Integration test: an absent child is excluded from the BKR present count
  (T027's query), in the same file

### Implementation for User Story 3

- [X] T035 [US3] Implement `MarkAbsentCommand`/handler (same conflict/closure-day rules as
  `CheckInCommand`, T017) in `backend/ChildCare.Application/Attendance/MarkAbsentCommand.cs`
- [X] T036 [US3] Wire `POST /api/attendance/absence` into
  `backend/ChildCare.Api/Endpoints/AttendanceEndpoints.cs` (depends on T035)
- [X] T037 [P] [US3] Build `AbsenceDialog` component (a visually and interactionally distinct
  action from the one-tap check-in gesture per spec.md's UX Requirements, with
  justified/unjustified selection + optional reason field) in
  `mobile/components/AbsenceDialog.tsx`
- [X] T038 [US3] Wire `AbsenceDialog` into `mobile/app/(app)/index.tsx` (a separate, deliberate
  entry point per child card — e.g. long-press or a secondary icon, not the primary tap target)
  (depends on T037)
- [X] T039 [US3] Implement `mobile/services/attendance.ts`'s absence-marking API call and offline
  queue registration (depends on T021)
- [X] T040 [P] [US3] Add i18n keys (absence dialog labels, justified/unjustified options) to
  `mobile/i18n/locales/{nl,fr,en}.json`'s `attendance` key

**Checkpoint**: User Stories 1–3 all work independently — absence marking is layered on top of
check-in/out and BKR.

---

## Phase 6: User Story 4 - Director corrects a missed check-out or wrong entry (Priority: P2)

**Goal**: A director can correct any attendance record regardless of age; a caregiver can correct
only same-day records at their own location. A director-web screen surfaces the history/
correction view.

**Independent Test**: Create an attendance record with a check-in but no check-out, have a
director correct it with a check-out time, confirming the update succeeds regardless of which day
the original record is from — independent of other stories.

### Tests for User Story 4

- [X] T041 [P] [US4] Integration test: a caregiver (device token) can correct a same-day record at
  their own paired location, in
  `backend/ChildCare.Api.Tests/Attendance/AttendanceCorrectionTests.cs`
- [X] T042 [P] [US4] Integration test: a caregiver attempting to correct a prior-day record
  receives `403 errors.attendance.edit_window_expired`, in the same file
- [X] T043 [P] [US4] Integration test: a caregiver attempting to correct a record at a different
  location than their device's own `LocationId` claim receives `403`, in the same file
- [X] T044 [P] [US4] Integration test: a director can correct any record regardless of date, in
  the same file
- [X] T045 [P] [US4] Integration test: attempting to set `status = closure` directly via `PATCH`
  is rejected with `403 errors.attendance.closure_status_immutable` (FR-015), in the same file
- [X] T045a [P] [US4] Integration test: a `PATCH` that would result in `status = present` with no
  `checkInAt`, or `status = absent` with no `absenceJustified` set, is rejected with `422
  errors.validation` (FR-011a), in the same file
- [X] T046 [P] [US4] Integration test: `DELETE /api/attendance/{id}` removes the record under the
  same authorization rules as `PATCH`, in the same file
- [X] T047 [P] [US4] Integration test: `GET /api/attendance?locationId=...&date=...` (director-
  only) paginates correctly via the `before` cursor, in
  `backend/ChildCare.Api.Tests/Attendance/ListAttendancePaginationTests.cs`

### Implementation for User Story 4

- [X] T048 [US4] Implement `CorrectAttendanceRecordCommand`/handler, applying
  `AttendanceEditWindowPolicy` (T008) before persisting changes, rejecting a direct
  `status = closure` write, and validating the FR-011a status invariants (`present` requires
  `checkInAt`; `absent` requires `absenceJustified` and clears any stale `checkInAt`/
  `checkOutAt`) via FluentValidation before persisting, in
  `backend/ChildCare.Application/Attendance/CorrectAttendanceRecordCommand.cs`
- [X] T049 [US4] Implement `DeleteAttendanceRecordCommand`/handler (same
  `AttendanceEditWindowPolicy` check) in
  `backend/ChildCare.Application/Attendance/DeleteAttendanceRecordCommand.cs`
- [X] T050 [US4] Implement `ListAttendanceQuery`/handler (cursor pagination on `(Date DESC, Id)`,
  scoped to `locationId`/`date`, per research.md R8) in
  `backend/ChildCare.Application/Attendance/ListAttendanceQuery.cs`
- [X] T051 [US4] Wire `PATCH /api/attendance/{id}` and `DELETE /api/attendance/{id}` onto the
  existing `DeviceOrDirector` policy (feature 009), and `GET /api/attendance` onto `DirectorOnly`,
  into `backend/ChildCare.Api/Endpoints/AttendanceEndpoints.cs` (depends on T048, T049, T050)
- [X] T052 [US4] Regenerate `web/lib/generated/api-types.ts` against the running backend
  (`npm run generate-api-client` per feature 007a's precedent) and commit the diff
- [X] T053 [US4] Build the director-web attendance correction/history screen (table view per
  platform-rules.md's director-web density expectations, row action to correct a record) in
  `web/app/(app)/attendance/page.tsx`, replacing the existing `NotYetAvailable` placeholder route
  (feature 007a's precedent) (depends on T052)
- [X] T054 [P] [US4] Add i18n keys for the director-web attendance screen to
  `web/messages/{nl,fr,en}.json`'s `attendance` key

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T055 Run `quickstart.md`'s full validation sequence (backend curl steps + manual mobile/web
  steps) end-to-end against a local dev environment
- [X] T056 Review every new mobile/web file (`BkrIndicator.tsx`, `AbsenceDialog.tsx`,
  `attendance.ts`, `web/app/(app)/attendance/page.tsx`) for hardcoded user-facing strings —
  constitution Principle IV compliance sweep
- [X] T057 Run the full backend (`dotnet test`), mobile (`npm test`), and web (`npm test`) suites
  and fix any regressions before handoff

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001's enum is referenced by T003). Blocks all
  user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only. This is the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational and on User Story 1's `CheckInCommand`
  (T017) existing, since BKR's present-count query reads the records US1 creates.
- **User Story 3 (Phase 5)**: Depends on Foundational only for its own write path, but is only
  meaningfully testable against BKR (T034) once User Story 2's query (T027) exists.
- **User Story 4 (Phase 6)**: Depends on Foundational only for its own CRUD paths, but its tests
  are more meaningful once US1's create path (T017) exists to generate records to correct.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Within Each User Story

- Tests before implementation (write and confirm failing first).
- Commands/queries before endpoint wiring.
- Backend endpoint before the mobile/web screen or service code that calls it.

### Parallel Opportunities

- All Phase 1 tasks (`T001`, `T002`, `T002a`) in parallel.
- `T006`, `T007`, `T008`, `T010` (Phase 2) in parallel once `T003`–`T005` land (they don't touch
  the same files).
- All test tasks within a single user story phase marked `[P]` in parallel (different test files
  or independent test methods in the same file).
- `T029` (BKR indicator component) in parallel with `T027`/`T028` (backend BKR query/endpoint).
- US3's tests (`T032`–`T034`) in parallel; US4's tests (`T041`–`T047`) in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests together:
Task: "Integration test: POST /api/attendance/check-in happy path in backend/ChildCare.Api.Tests/Attendance/CheckInTests.cs"
Task: "Integration test: duplicate check-in conflict in backend/ChildCare.Api.Tests/Attendance/CheckInTests.cs"
Task: "Integration test: check-out in backend/ChildCare.Api.Tests/Attendance/CheckOutTests.cs"
Task: "Integration test: planned_duration_minutes derivation in backend/ChildCare.Api.Tests/Attendance/PlannedDurationCalculatorTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (Setup) + Phase 2 (Foundational).
2. Complete Phase 3 (User Story 1) — check-in/check-out, online and offline.
3. **STOP and VALIDATE**: run quickstart.md's backend check-in/check-out steps and the mobile
   one-tap steps.
4. This alone is a demonstrable, valuable increment (caregivers can record daily presence).

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. User Story 1 → MVP, demo-able.
3. User Story 2 → live BKR compliance indicator layered on top.
4. User Story 3 → absence registration layered on top.
5. User Story 4 → director correction workflow + web screen.
6. Polish.
