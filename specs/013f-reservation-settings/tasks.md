# Tasks: Reservation Settings

**Input**: Design documents from `specs/013f-reservation-settings/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web/parent-mobile), same standard every prior feature has
followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [X] T001 [P] Add `UpdateLocationReservationSettingsRequest` (absencesMode, extrasMode, swapsMode, noticeHours, confirmDespitePending) to `backend/ChildCare.Contracts/Requests/LocationRequests.cs`
- [X] T002 [P] Extend `LocationResponse` with `reservationAbsencesMode`, `reservationExtrasMode`, `reservationSwapsMode`, `reservationNoticeHours` in `backend/ChildCare.Contracts/Responses/LocationResponse.cs`
- [X] T003 [P] Add `locations.*` reservation-settings i18n keys (nav label promoted to real, list column headers, tab labels "Algemeen"/"Reserveringsinstellingen", the three mode dropdown option labels + one-line explanation per option, notice-hours field label, pending-requests warning dialog copy) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`
- [X] T004 [P] Add `dayReservations.*` disabled-type i18n keys (parent-mobile inline "not available for this child" block message) to `parent-mobile/i18n/locales/en.json`, `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `ReservationRequestMode` enum, the four `Location` columns, and the migration
every story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Create `ReservationRequestMode` enum (`Disabled`, `Informational`, `Approval`) in `backend/ChildCare.Domain/Enums/ReservationRequestMode.cs`
- [X] T006 Add `ReservationAbsencesMode`, `ReservationExtrasMode` (default `Approval`), `ReservationSwapsMode` (default `Disabled`), `ReservationNoticeHours` (default `0`) to `backend/ChildCare.Domain/Entities/Location.cs`
- [X] T007 Configure the four new columns (enum-as-text conversion mirroring `DayReservation.Type`'s existing pattern, `HasMaxLength(20)`, `HasDefaultValue`) on the `Location` entity block in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T008 Add tenant migration `AddLocationReservationSettings` (four columns with column-level defaults so existing rows backfill per FR-002) in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [X] T009 [P] Extend `LocationMapper.ToResponse` with the four new fields in `backend/ChildCare.Application/Locations/LocationMapper.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Director configures request-type behaviour per location (Priority: P1) 🎯 MVP

**Goal**: A director can view and update a location's three modes and notice-hours from the web
admin; changes persist immediately and are scoped to that location only.

**Independent Test**: `PUT /api/locations/{id}/reservation-settings` changes
`reservation_absences_mode`, save, re-fetch via `GET /api/locations/{id}` and confirm the new
value, and confirm a second location's settings are untouched.

### Tests for User Story 1

- [X] T010 [P] [US1] Integration test: `GET /api/locations/{id}` on a location with no settings ever saved returns the column defaults (`approval`/`approval`/`disabled`/`0`) (FR-002) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`
- [X] T011 [P] [US1] Integration test: `PUT .../reservation-settings` updates all four fields, persists them, and leaves every other location's settings unchanged (FR-004) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`
- [X] T012 [P] [US1] Integration test: `PUT .../reservation-settings` with an invalid mode string or `noticeHours` outside 0–8760 returns `422` with `fieldErrors` (FR-011) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`
- [X] T013 [P] [US1] Integration test: `PUT .../reservation-settings` on an unknown location id returns `404 errors.location.not_found` in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`
- [X] T014 [P] [US1] Integration test: non-`DirectorOnly` callers (e.g. device token) are rejected on `PUT .../reservation-settings` (policy boundary, mirrors `LocationCrudTests` precedent) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`

### Implementation for User Story 1

- [X] T015 [US1] Implement `UpdateLocationReservationSettingsCommand` + `UpdateLocationReservationSettingsCommandValidator` (mode-string parsing mirroring `DayReservationMapper.TryParseType`'s convention, `noticeHours` `InclusiveBetween(0, 8760)`) in `backend/ChildCare.Application/Locations/UpdateLocationReservationSettingsCommand.cs`
- [X] T016 [US1] Implement the command handler: load location, apply the four fields, save, return `LocationResult.Success` (pending-requests warning logic deferred to US4 — this task only handles the direct-apply path) in the same file
- [X] T017 [US1] Wire `PUT /api/locations/{id}/reservation-settings` in `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs`
- [X] T018 [US1] Regenerate and commit `web/lib/generated/api-types.ts` against the new endpoint and response fields
- [X] T019 [US1] Move `locations` from `PLACEHOLDER_NAV` to `REAL_NAV` (real `href: "/locations"`) in `web/components/Sidebar.tsx`
- [X] T020 [P] [US1] Create `LocationsTable.tsx` (name, address, capacity, active/deactivated status, row click navigates to detail) in `web/components/LocationsTable.tsx`
- [X] T021 [US1] Replace the `NotYetAvailable` placeholder with a real list page (loads `GET /api/locations`, renders `LocationsTable`, loading/empty/error states per `platform-rules.md` director-web density) in `web/app/(app)/locations/page.tsx`
- [X] T022 [P] [US1] Create `ReservationSettingsForm.tsx` (three mode dropdowns with per-option explanation text, notice-hours number input, save action) in `web/components/ReservationSettingsForm.tsx`
- [X] T023 [US1] Create the location detail route with "Algemeen" (existing `PUT /api/locations/{id}` general fields) and "Reserveringsinstellingen" (`ReservationSettingsForm`) tabs in `web/app/(app)/locations/[id]/page.tsx`
- [X] T024 [P] [US1] Web component test: settings tab loads current values, saving a mode change calls the new endpoint and reflects the updated value in `web/__tests__/locationReservationSettings.test.tsx`

**Checkpoint**: At this point, a director can fully configure a location's reservation policy end
to end via the web admin, independently of any enforcement on the parent side.

---

## Phase 4: User Story 2 - Parent app respects the location's configured modes (Priority: P1)

**Goal**: A `disabled` type has no entry point for an affected parent; an `informational`
submission auto-approves with the same downstream effects an approval would have, and appears in
the director's existing queue distinguished from actionable items.

**Independent Test**: Configure one location `disabled` and another `informational` for the same
type; verify a parent at the first location has no entry point while a parent at the second gets
an immediate `approved` result with no director action, visible in the queue as system-decided.

### Tests for User Story 2

- [X] T025 [P] [US2] Integration test: submitting a request whose resolved candidate location(s) all have that type `disabled` returns `403 errors.day_reservations.request_type_disabled` and creates no row (FR-007) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T026 [P] [US2] Integration test: submitting an absence request under `informational` mode creates the row already `approved`, `decidedBy: null`, `decidedAt` set, and a matching `AttendanceRecord` exists (FR-008, FR-009's absence branch) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T027 [P] [US2] Integration test: an `informational`-mode absence submission whose date is a published closure day is rejected the same way an `approval`-mode submission's later approval would be, not silently auto-approved (FR-009, spec Edge Cases) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T028 [P] [US2] Integration test: an `informational`-mode `extra`/`exchange` auto-approval creates no `AttendanceRecord`, matching `approval`-mode's existing FR-012 behavior (013a) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T029 [P] [US2] Integration test: `GET /api/day-reservations?status=approved` (director) includes a `decidedBy: null` auto-approved row alongside director-decided rows in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T030 [P] [US2] Integration test: `approval`-mode submission and decision behavior is byte-for-byte unchanged from 013a (regression guard for FR-015) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`

### Implementation for User Story 2

- [X] T031 [US2] Implement `ReservationPolicyResolver` per research.md R3 (candidate-location resolution per type, most-restrictive-wins aggregation, zero-candidate fallback to `Approval`) in `backend/ChildCare.Application/DayReservations/ReservationPolicyResolver.cs`
- [X] T032 [US2] Add `RequestTypeDisabled` failure to `DayReservationFailure` in `backend/ChildCare.Application/DayReservations/DayReservationResult.cs`
- [X] T033 [US2] In `SubmitDayReservationCommandHandler`, resolve the policy via `ReservationPolicyResolver` before creating the reservation; return `RequestTypeDisabled` when `Mode == Disabled` in `backend/ChildCare.Application/DayReservations/SubmitDayReservationCommand.cs`
- [X] T034 [US2] When `Mode == Informational`: set `Status = Approved`, `DecidedBy = null`, `DecidedAt = now`, and — for `absence` — invoke the same `MarkAbsentCommand` path `ApproveDayReservationCommandHandler` uses (with `AbsenceJustified = true`), surfacing `ClosureDayConflict` on failure exactly as an approval would, in the same file
- [X] T035 [US2] Wire `RequestTypeDisabled` → `403 errors.day_reservations.request_type_disabled` in `MapFailure` in `backend/ChildCare.Api/Endpoints/DayReservationEndpoints.cs`
- [X] T036 [P] [US2] Extend `DayReservationsTable.tsx` to render a distinguishing "auto-approved" badge/label when `decidedBy` is `null` in `web/components/DayReservationsTable.tsx`
- [X] T037 [P] [US2] Web component test: an auto-approved row renders the distinguishing badge and a director-decided row does not in `web/__tests__/dayReservations.test.tsx`

**Checkpoint**: Disabled/informational modes are fully enforced server-side and visible in the
director queue.

---

## Phase 5: User Story 3 - Disabled or under-notice submissions are rejected server-side (Priority: P1)

**Goal**: The notice-hours window is enforced server-side for every submission, independent of
what any client displayed, using the same candidate-location resolution as US2.

**Independent Test**: POST a request dated inside the configured notice window directly against
the API and verify a validation rejection identifying the required notice period.

### Tests for User Story 3

- [X] T038 [P] [US3] Integration test: a request dated inside the resolved notice-hours window is rejected `422` with `fieldErrors.requestedDate = errors.day_reservations.notice_period_required` and creates no row (FR-012) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T039 [P] [US3] Integration test: `noticeHours = 0` (default) places no restriction — same-day submission still succeeds, matching 013a's existing behavior (spec Edge Cases) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T040 [P] [US3] Integration test: a child split across two active-contract locations with different notice-hours values is governed by the higher (stricter) of the two (FR-017) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T041 [P] [US3] Integration test: a child with two active-contract locations where one has the type `disabled` and the other `approval` is rejected (most-restrictive-wins, FR-017, Edge Cases) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T042 [P] [US3] Integration test: a child with no active contract at all is exempt from mode/notice-hours enforcement and proceeds as `approval` (FR-017, Edge Cases) in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] Add `NoticePeriodRequired` handling: `ReservationPolicyResolver` returns the resolved `NoticeHours`; `SubmitDayReservationCommandValidator` (or handler, since the check needs DB-resolved policy) rejects when `BelgianCalendarDay.UtcRangeFor(requestedDate).StartUtc - DateTime.UtcNow < TimeSpan.FromHours(noticeHours)` per research.md R4, in `backend/ChildCare.Application/DayReservations/SubmitDayReservationCommand.cs`
- [X] T044 [US3] Ensure the notice-hours field-level error surfaces as `fieldErrors.requestedDate` via the standard `ValidationBehavior`/handler-level validation-result shape (matching FR-012's contract) in the same file

**Checkpoint**: Server-side enforcement (US2 + US3) is complete and independent of client behavior.

---

## Phase 6: User Story 4 - Director is warned before a mode change strands pending requests (Priority: P2)

**Goal**: Saving a mode change away from `approval` for a type with existing pending requests
shows a warning with the affected count before committing; confirming proceeds without altering
those pending requests.

**Independent Test**: Create a pending request of a given type, attempt to change that type's mode
away from `approval`, verify a `409` warning with the count, then confirm and verify the pending
request is untouched.

### Tests for User Story 4

- [X] T045 [P] [US4] Integration test: changing a mode away from `approval` for a type with pending requests and `confirmDespitePending: false` returns `409` with `pendingCounts` for the affected type(s) and does not persist the change (FR-014) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`
- [X] T046 [P] [US4] Integration test: resubmitting with `confirmDespitePending: true` persists the change and the pending requests remain `status: "pending"` (FR-005, FR-014) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`
- [X] T047 [P] [US4] Integration test: changing a mode with zero pending requests of that type requires no confirmation and saves directly (spec Edge Cases) in `backend/ChildCare.Api.Tests/LocationReservationSettingsTests.cs`

### Implementation for User Story 4

- [X] T048 [US4] Add `PendingRequestsWarning` failure (carrying per-type pending counts) to `LocationFailure`/`LocationResult` in `backend/ChildCare.Application/Locations/LocationResult.cs`
- [X] T049 [US4] In `UpdateLocationReservationSettingsCommandHandler`, before applying a mode change away from `Approval`, count pending `DayReservation`s of that type at this location (reusing `ReservationPolicyResolver`'s candidate-location concept in reverse — this location is itself always a candidate for its own pending requests) and return `PendingRequestsWarning` when `confirmDespitePending` is false and any count is nonzero, in `backend/ChildCare.Application/Locations/UpdateLocationReservationSettingsCommand.cs`
- [X] T050 [US4] Wire `PendingRequestsWarning` → `409 errors.location.reservation_settings.pending_requests_warning` with a `pendingCounts` body in `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs`
- [X] T051 [P] [US4] Create `PendingRequestsWarningDialog.tsx` (mirrors `ApproveDayReservationDialog`'s confirm-dialog pattern) in `web/components/PendingRequestsWarningDialog.tsx`
- [X] T052 [US4] Wire the warning dialog into `ReservationSettingsForm.tsx`'s save flow: on `409`, show the dialog with counts; confirming resubmits with `confirmDespitePending: true` in `web/components/ReservationSettingsForm.tsx`
- [X] T053 [P] [US4] Web component test: saving a mode change with pending requests shows the warning dialog with the correct count; confirming resubmits and succeeds in `web/__tests__/locationReservationSettings.test.tsx`

**Checkpoint**: All four user stories are independently functional. Full feature complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Parent-mobile entry-point visibility (spec FR-006/research.md R6) and final
regenerated client wiring — depends on US2/US3's backend enforcement being in place.

No parent-accessible read exposes location/mode data today — `ParentChildResponse` (feature 013)
carries no location reference at all, and every existing parent endpoint is silent on reservation
settings. T054 below adds the one minimal read this feature needs.

- [X] T054 [P] Add `ReservationAvailabilityResponse` (`absence`/`extra`/`exchange` — `DayReservationType` wire strings, per `DayReservationMapper.ToWire` — modes + `noticeHours`) to `backend/ChildCare.Contracts/Responses/DayReservationResponses.cs`, a `GetReservationAvailabilityQuery` (reuses `ReservationPolicyResolver` per type, `ParentOnly`, verifies the child is linked to the caller the same way `SubmitDayReservationCommand` does) in `backend/ChildCare.Application/DayReservations/`, and wire `GET /api/parent/children/{childId}/reservation-availability` in `backend/ChildCare.Api/Endpoints/DayReservationEndpoints.cs`
- [X] T055 [P] Integration test: `GET .../reservation-availability` returns the correct mode per type for a child, including the most-restrictive-wins result for a split-location child, and 403s for a child not linked to the caller in `backend/ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`
- [X] T056 [P] Create `parent-mobile/services/locations.ts` wrapping the new endpoint, exposing a per-child effective-mode helper
- [X] T057 [P] Conditionally hide a home-screen quick-action button only when the type is disabled for every one of the parent's linked children (research.md R6, implements FR-006) in `parent-mobile/app/(app)/index.tsx`
- [X] T058 [P] Add the per-child disabled-type inline block (clear message, submission blocked) to each entry form once a child is selected (implements FR-006) in `parent-mobile/app/(app)/requests/absence.tsx`, `parent-mobile/app/(app)/requests/extra.tsx`, `parent-mobile/app/(app)/requests/exchange.tsx`
- [X] T059 [P] parent-mobile component test: a disabled type is hidden from the home screen for a single-child parent, and blocks submission with the inline message when reached directly in `parent-mobile/__tests__/reservationSettings.test.tsx`
- [X] T060 Regenerate and commit `web/lib/generated/api-types.ts` and `parent-mobile/services/generated/api-types.ts` against the new availability endpoint
- [X] T061 Run the full backend + web + parent-mobile test suites and fix any regressions surfaced by the new columns/enforcement path

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)**: strictly sequential, blocks everything below.
- **Phase 3 (US1)**: depends only on Phase 2. Delivers the settings UI end-to-end (MVP).
- **Phase 4 (US2)** and **Phase 5 (US3)**: both depend only on Phase 2 (not on US1's web UI) —
  can be implemented in parallel with US1, or immediately after, since their independent tests are
  API-level. US3 shares `ReservationPolicyResolver` (T031, US2) — implement US2 first within this
  pair if doing them sequentially.
- **Phase 6 (US4)**: depends on Phase 3 (US1's `ReservationSettingsForm.tsx` and
  `UpdateLocationReservationSettingsCommand` must exist first).
- **Phase 7 (Polish)**: depends on Phase 4/5 (US2/US3's backend enforcement, specifically
  `ReservationPolicyResolver` from T031) being complete — T054's `GetReservationAvailabilityQuery`
  reuses it directly.

## Parallel Execution Examples

- Within Phase 1: T001–T004 touch disjoint files — run together.
- Within Phase 3: T010–T014 (tests) in parallel; then T020/T022 (independent new components) in
  parallel before T021/T023 (which consume them).
- Within Phase 4: T025–T030 (tests) in parallel; T036/T037 in parallel with backend tasks T031–T035
  once the API contract (already fixed in contracts/reservation-settings-api.md) is known.
- US2 (Phase 4) and US3 (Phase 5) can be staffed in parallel by two independent workstreams once
  T031 (`ReservationPolicyResolver`) lands, since T031 is the only shared dependency between them.

## Implementation Strategy

**MVP = Phase 1 + 2 + 3 (User Story 1)**: a director can configure per-location reservation policy
end-to-end via the web admin. This alone delivers no parent-facing behavior change yet (US2/US3
are the enforcement half), but per spec.md's own "Why this priority" reasoning, US1 and US2/US3
are equally load-bearing P1s — ship all three before considering the feature done; US4 (P2) is a
safety-net addition that can genuinely follow after.
