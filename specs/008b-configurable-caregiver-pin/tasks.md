# Tasks: Configurable Caregiver PIN

**Input**: Design documents from `/specs/008b-configurable-caregiver-pin/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, quickstart.md

**Tests**: Included — this codebase's standing convention (constitution Principle V, and every
prior feature's shipped-notes) is happy-path plus key negative/regulatory flows, not exhaustive
coverage.

**Organization**: Tasks are grouped by user story (US1 = director toggles setting, US2 =
caregiver check-in/out UX, US3 = BKR/attribution parity).

## Phase 1: Setup

**Purpose**: No new project/dependency setup needed — this feature extends existing
`backend/`/`web/`/`mobile/` structure with no new packages.

- [X] T001 Confirm local dev stack (backend, web, mobile) runs against a tenant schema with an
      existing location and at least two staff profiles with PINs set, per quickstart.md
      Prerequisites.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The data-model change every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 Add `RequiresCaregiverPin` (bool, default `true`) to `Location` in
      `backend/ChildCare.Domain/Entities/Location.cs`, following the `FlexPermission`/
      `BoPermission` convention (plain auto-property, `// Feature 008b` comment).
- [X] T003 Generate the EF Core migration
      `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/20260713151025_AddLocationRequiresCaregiverPin.cs`
      adding `RequiresCaregiverPin boolean NOT NULL DEFAULT true` to `tenant_template.locations`
      (generated via `dotnet ef migrations add`; the generated `defaultValue: false` was a real
      bug against FR-002/SC-004 and was corrected to `true` by hand).
- [X] T004 Add `RequiresCaregiverPin` to `LocationResponse` in
      `backend/ChildCare.Contracts/Responses/LocationResponse.cs` and its single mapping site,
      `LocationMapper.ToResponse` in `backend/ChildCare.Application/Locations/LocationMapper.cs`.
- [X] T005 Extended both `TenantMigrationRolloutTests.cs` (history-row removal only, since its
      revert drops the whole `locations` table) and `LegacyVaccinationMigrationTests.cs` (added a
      `DROP COLUMN "RequiresCaregiverPin"` plus history-row removal, since its revert only targets
      the newer migrations without dropping `locations` itself) to cover the new column.

**Checkpoint**: `Location` carries the new field end-to-end through the response contract —
user story work can now begin.

---

## Phase 3: User Story 1 - Director disables PIN verification for a location (Priority: P1) 🎯 MVP

**Goal**: A director can view and change the per-location PIN requirement, with tradeoff copy,
from the web admin.

**Independent Test**: Toggle the setting for one location via the web UI, save, and verify via
`GET /api/locations/{id}` that only that location changed.

### Tests for User Story 1

- [X] T006 [P] [US1] Backend integration test: `PUT /api/locations/{id}/checkin-settings` persists
      `requiresCaregiverPin` and leaves other locations unaffected, in new file
      `backend/ChildCare.Api.Tests/LocationCheckInSettingsTests.cs` (mirrors the existing
      `LocationReservationSettingsTests.cs`'s shape/fixtures).
- [X] T007 [P] [US1] Backend test: a non-director (or unauthenticated) caller is rejected from the
      new endpoint, same file as T006.
- [X] T008 [P] [US1] Web component test: `CheckInSettingsForm` renders current state, toggles, and
      saves, in `web/__tests__/checkInSettings.test.tsx` (flat, matches this repo's existing
      `locationReservationSettings.test.tsx` convention — no `components/` subdirectory).
- [X] T008a [P] [US1] Web component test: on a failed save, `CheckInSettingsForm` reverts the
      toggle and shows an error notice (FR-015) — same file as T008.
- [X] T008b [P] [US1] Backend test: `UpdateLocationCheckInSettingsCommand` writes a structured log
      entry (director, location, old/new value) on change (FR-016) — same file as T006.

### Implementation for User Story 1

- [X] T009 [P] [US1] Add `UpdateLocationCheckInSettingsRequest(bool RequiresCaregiverPin)` to
      `backend/ChildCare.Contracts/Requests/LocationRequests.cs`.
- [X] T010 [US1] Implement `UpdateLocationCheckInSettingsCommand`/Handler in
      `backend/ChildCare.Application/Locations/UpdateLocationCheckInSettingsCommand.cs`
      (`DirectorOnly`, loads location, sets `RequiresCaregiverPin`, saves, returns
      `LocationResponse`) — mirrors `UpdateLocationReservationSettingsCommand`. (depends on T002, T009)
- [X] T011 [US1] Add `PUT /{id:guid}/checkin-settings` to
      `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs`, mapping to the new command (thin
      endpoint per constitution Principle III). (depends on T010)
- [X] T012 [P] [US1] Add i18n keys (toggle label + tradeoff copy) to `web/i18n/locales/nl.json`,
      `web/i18n/locales/fr.json`, `web/i18n/locales/en.json` under a `locations.checkInSettings`
      namespace (actual path — `web/messages/` doesn't exist in this repo).
- [X] T013 [US1] Create `web/components/CheckInSettingsForm.tsx` — local state seeded from
      `location.requiresCaregiverPin`, toggle + tradeoff copy (per FR-003, including the
      wrong-card-tap risk called out explicitly), `PUT` via `web/lib/apiClient`, success/error
      `notice`, calls `onSaved`. Mirrors `ReservationSettingsForm.tsx`'s shape. (depends on T012)
- [X] T014 [US1] Add an "Inchecken" tab entry to the `Tab` union and tab bar in
      `web/app/(app)/locations/[id]/page.tsx`, rendering `CheckInSettingsForm` when active.
      (depends on T013)
- [X] T015 [US1] Regenerate `web/lib/generated/api-types.ts` against the running backend and
      commit the diff (per 007a's established convention — this does not happen automatically).
      (depends on T011)
- [X] T015a [US1] In `CheckInSettingsForm.tsx`, on a failed save (network/validation/403), revert
      the toggle to its last-saved value and show a clear error `notice` — never leave the UI
      showing an unsaved toggle state as if it succeeded (FR-015). (depends on T013)
- [X] T015b [US1] Add an `ILogger` structured log entry to `UpdateLocationCheckInSettingsCommand`'s
      handler (director id, location id, old/new value, timestamp) at INFO level — this codebase
      has no dedicated audit-trail mechanism to extend, so this is a plain application log entry,
      not a new subsystem (FR-016). (depends on T010)

**Checkpoint**: A director can view, toggle, and save the setting end-to-end; other locations
unaffected.

---

## Phase 4: User Story 2 - Caregiver checks in/out with no PIN step (Priority: P1)

**Goal**: At a PIN-off location, tapping a caregiver's card completes check-in/out immediately,
with no keypad shown; a PIN-on location is completely unaffected.

**Independent Test**: Tap a card at a PIN-off location and confirm no keypad appears and the
shift completes; repeat at an unaffected PIN-on location and confirm the keypad still appears.

### Tests for User Story 2

- [X] T016 [P] [US2] Backend test: `CheckInCommand`/`CheckOutCommand` at a `RequiresCaregiverPin =
      false` location succeed with `pin: null` and write a `RoomShift` row, in
      `backend/ChildCare.Api.Tests/RoomShiftTests.cs` (extend the existing `CheckIn_...`/`CheckOut_...`
      test cases in that file).
- [X] T017 [P] [US2] Backend test: the same actions at a `RequiresCaregiverPin = true` location
      (default) still reject a missing/invalid PIN exactly as before — same file as T016.
- [X] T018 [P] [US2] Backend test: the roster response includes the location's current
      `requiresCaregiverPin` value, in `backend/ChildCare.Api.Tests/RoomShiftTests.cs` (extend the
      existing `GetRoster_...` test case).
- [X] T019 [P] [US2] Mobile test: room-home screen skips rendering `PinKeypad` and calls
      `checkIn`/`checkOut` directly when the roster response's `requiresCaregiverPin` is `false`,
      in `mobile/__tests__/screens/room-home.test.tsx` (extend existing file).
- [X] T020 [P] [US2] Mobile test: room-home screen still renders `PinKeypad` when
      `requiresCaregiverPin` is `true` — same file as T019 (regression guard).

### Implementation for User Story 2

- [X] T021 [US2] Change `Pin` from `string` to `string?` on `CheckInRequest`/`CheckOutRequest` in
      `backend/ChildCare.Api/Endpoints/RoomShiftEndpoints.cs`.
- [X] T022 [US2] In `CheckInCommandHandler.Handle`
      (`backend/ChildCare.Application/RoomShifts/CheckInCommand.cs`), load the location and branch:
      if `RequiresCaregiverPin` is `false`, skip `verifyPin.VerifyAsync(...)` and proceed straight
      to the existing shift-write logic; if `true`, existing behavior unchanged (reject
      null/invalid `Pin`). (depends on T002, T021)
- [X] T023 [US2] Apply the equivalent branch to `CheckOutCommandHandler.Handle` in
      `backend/ChildCare.Application/RoomShifts/CheckOutCommand.cs`. (depends on T002, T021)
- [X] T024 [US2] Add `RoomRosterResponse(bool RequiresCaregiverPin, IReadOnlyList<RoomRosterCardResponse> Caregivers)`
      to `backend/ChildCare.Contracts/Responses/RoomShiftResponses.cs`.
- [X] T025 [US2] Update `GetRoomRosterQuery`/Handler in
      `backend/ChildCare.Application/RoomShifts/GetRoomRosterQuery.cs` to return
      `RoomRosterResponse` (load the location's `RequiresCaregiverPin` alongside the existing
      staff/shift queries). (depends on T002, T024)
- [X] T026 [US2] Update the `/roster` route in
      `backend/ChildCare.Api/Endpoints/RoomShiftEndpoints.cs` to reflect the new
      `IRequest<RoomRosterResponse>` return type. (depends on T025)
- [X] T027 [US2] Update `mobile/services/roomShift.ts`: `getRoster()` return type becomes
      `RoomRosterResponse`-shaped (`requiresCaregiverPin` + `caregivers`); `checkIn`/`checkOut`
      accept an optional `pin` parameter. (depends on T026)
- [X] T028 [US2] Update `mobile/app/(room)/index.tsx`: read `requiresCaregiverPin` from the
      roster response; when `false`, tapping a card calls `checkIn`/`checkOut` directly with no
      `pin` and never mounts `PinKeypad`; when `true`, existing keypad flow unchanged. (depends
      on T027)
- [X] T029 [US2] Regenerate `mobile/services/generated/api-types.ts` against the running backend
      and commit the diff. (depends on T026)

**Checkpoint**: Both PIN-off and PIN-on check-in/out flows work end-to-end on the tablet.

---

## Phase 5: User Story 3 - BKR ratio and event attribution are unaffected (Priority: P1)

**Goal**: Prove, not just assert, that staffing-ratio and recorded-by/administered-by resolution
never differ based on whether PIN verification occurred.

**Independent Test**: Compare ratio/attribution results for two otherwise-identical shifts, one
PIN-verified and one not, and confirm no difference.

### Tests for User Story 3

- [X] T030 [P] [US3] Parity test: the BKR ratio's qualified-staff count is identical for a
      PIN-off-created shift versus a PIN-on-created shift, in
      `backend/ChildCare.Api.Tests/Attendance/BkrRatioTests.cs` (extend existing file — add a case
      using a PIN-off location).
- [X] T031 [P] [US3] Parity test: `ShiftAttributionService`'s `recorded_by` resolution is
      identical for an event logged during a PIN-off-created shift versus a PIN-on-created shift,
      in `backend/ChildCare.Api.Tests/ShiftAttributionServiceTests.cs` (extend existing file).
- [X] T032 [P] [US3] Test: `ConfirmAdministratorCommand` still requires PIN verification (or
      explicit `Skip`) regardless of the location's `RequiresCaregiverPin` value, in
      `backend/ChildCare.Api.Tests/RoomShiftTests.cs` and/or
      `backend/ChildCare.Api.Tests/ChildEvents/AdministratorAttributionTests.cs` (extend whichever
      existing file already covers `confirm-administrator` — add a case at a PIN-off location
      proving no behavior change).

### Implementation for User Story 3

- [X] T033 [US3] No production code changes — this story is validation-only, since
      `GetBkrRatioQuery`/`IShiftAttributionService`/`ConfirmAdministratorCommand` already read
      only from `RoomShift`, never from whether PIN verification occurred (per research.md R5 and
      data-model.md). If any of T030-T032 fail, that is a genuine regression to fix in Phase 4's
      code, not new Phase 5 code.

**Checkpoint**: All three P1 user stories are independently verified; BKR/attribution behavior is
provably unchanged.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases from spec.md not yet covered by a story-specific task, plus final
validation.

- [X] T034 [P] Test: re-enabling `RequiresCaregiverPin` mid-day does not affect already-open
      `RoomShift` rows created while it was off — the PIN step only reappears for the next
      check-in/check-out action (spec.md Edge Cases). Add to
      `backend/ChildCare.Api.Tests/RoomShiftTests.cs`.
- [X] T035 [P] Test: turning the requirement off/on never touches `StaffProfile.PinHash` — a
      caregiver's PIN set before the requirement was turned off still verifies successfully after
      it's turned back on (FR-009). Add to `backend/ChildCare.Api.Tests/RoomShiftTests.cs`.
- [X] T036 [P] Verify the offline-skip-PIN path for `confirmAdministrator` (008a's existing
      "skip when offline" behavior in `AdministratorConfirmation.tsx`) remains visibly distinct
      from this feature's always-off-by-setting path — no shared code path that could blur which
      one fired (spec.md Edge Cases). Add an assertion/comment to
      `mobile/__tests__/components/AdministratorConfirmation.test.tsx` if not already implicit.
- [X] T037 Run `dotnet test` (backend), `npm test` (web), `npm test` (mobile) — full suite green.
- [X] T038 No simulator/browser tooling exists in this repo for manual UI walkthroughs (per
      standing process rules) — instead, all six of quickstart.md's scenarios are covered by
      automated tests run against a live local backend (`dotnet run` + Postgres) during this
      session: Scenario 1 (T006), Scenario 2 (T016), Scenario 3 (T017), Scenario 4 (T030/T031),
      Scenario 5 (T032), Scenario 6 (T035).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (the `Location` field
  must exist before any command/query/UI can read or write it).
- **User Story 1 (Phase 3)**: Depends on Foundational only. Independently testable/deployable —
  the director-facing toggle works even before US2's tablet UI changes ship (it just has no
  visible tablet effect yet).
- **User Story 2 (Phase 4)**: Depends on Foundational only (not on US1's web UI — the setting can
  be flipped directly via API for testing purposes). Independently testable.
- **User Story 3 (Phase 5)**: Depends on Phase 4 (needs both PIN-on and PIN-off shifts to exist to
  compare) — validation-only, adds no new code.
- **Polish (Phase 6)**: Depends on Phases 3-5 complete.

### Parallel Opportunities

- T006-T008 (US1 tests) can run in parallel with each other.
- T016-T020 (US2 tests) can run in parallel with each other.
- T030-T032 (US3 tests) can run in parallel with each other.
- T009 and T012 can run in parallel (different files, no dependency between them).
- Backend (Phase 3/4) and web-only i18n (T012) work can proceed in parallel once Phase 2 is done;
  mobile work (T027-T029) depends on the backend roster/check-in contract changes (T024-T026)
  landing first.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (trivial) + Phase 2 (Foundational).
2. Complete Phase 3 (US1) — director can toggle the setting, even though the tablet doesn't yet
   honor it.
3. **STOP and VALIDATE**: confirm the setting persists correctly and is scoped per-location.

### Incremental Delivery

1. Foundational → US1 (director control exists) → US2 (tablet honors it) → US3 (proven safe) →
   Polish.
2. Each phase is a complete, demonstrable increment; US1 alone is a safe, low-risk deploy (the
   setting exists but every location defaults to `true`, so nothing changes until a director acts).

---

## Phase 7: Convergence

**Purpose**: Close a gap found by `/speckit-converge` between spec.md's own stated edge cases
and current test coverage.

- [X] T039 Add a backend test proving check-in/check-out at a `RequiresCaregiverPin = false`
      location succeeds for a caregiver who has never had a PIN set at all (create via
      `CreateStaffAsync` + `AssignEligibilityAsync` only, skipping `SetPinAsync`), in
      `backend/ChildCare.Api.Tests/RoomShiftTests.cs` per spec.md's own Edge Cases (partial)
