---
description: "Task list for feature 013e-monthly-menu"
---

# Tasks: Monthly Menu

**Input**: Design documents from `/specs/013e-monthly-menu/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — Constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative flows; this repo's convention
(CLAUDE.md) also expects web/mobile component tests for new UI.

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/`, `parent-mobile/` (see plan.md's Project
Structure).

---

## Phase 1: Setup

**Purpose**: No new dependencies — `lucide-react`/`lucide-react-native` are already installed
(design-system.md's icon requirement is already satisfied on both surfaces). Nothing to
initialize; proceed directly to Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The three new entities, one migration, and shared contracts. Every user story
depends on this phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 [P] Create `MealPreferenceChangeRequestStatus` enum (`Pending`, `Approved`, `Rejected`) in `backend/ChildCare.Domain/Enums/MealPreferenceChangeRequestStatus.cs`
- [X] T002 [P] Add `MealPreferenceRequestDecided` to the existing `NotificationType` enum in `backend/ChildCare.Domain/Enums/NotificationType.cs` (research.md R3)
- [X] T003 [P] Create `MonthlyMenu` entity (`Id`, `LocationId`, `Year`, `Month`, `PublishedAt`, `CreatedBy`, `CreatedAt`) in `backend/ChildCare.Domain/Entities/MonthlyMenu.cs`
- [X] T004 [P] Create `MonthlyMenuDay` entity (`Id`, `MenuId`, `MenuDate`, `Soup`, `MainCourse`, `Dessert`, `Notes`) in `backend/ChildCare.Domain/Entities/MonthlyMenuDay.cs`
- [X] T005 [P] Create `MealPreferenceChangeRequest` entity (`Id`, `ChildId`, `RequestedBy`, `NewTexture`, `NewDietaryType`, `Notes`, `Status` default `Pending`, `DecidedBy`, `DecidedAt`, `DecisionNotes`, `CreatedAt`) in `backend/ChildCare.Domain/Entities/MealPreferenceChangeRequest.cs` (depends on T001)
- [X] T006 Add `MonthlyMenus`, `MonthlyMenuDays`, `MealPreferenceChangeRequests` `DbSet`s and entity configuration to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` — `UNIQUE (LocationId, Year, Month)` on `MonthlyMenu`, `UNIQUE (MenuId, MenuDate)` on `MonthlyMenuDay` with cascade delete on `MenuId`, `NewDietaryType` mapped as a native `text[]` (mirrors `MealPreference.DietaryType`'s existing value converter) (depends on T003, T004, T005)
- [X] T007 Generate the EF Core migration `AddMonthlyMenuAndMealPreferenceRequests` and its SQL script in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`, per this repo's manual-apply convention (depends on T006)
- [X] T008 [P] Create `MonthlyMenuResponse`/`MonthlyMenuDayEntry`/`ParentMonthlyMenuEntry` records (per contracts/monthly-menu-api.md's response shapes) in `backend/ChildCare.Contracts/Responses/MonthlyMenuResponse.cs`
- [X] T009 [P] Create `MealPreferenceChangeRequestResponse`/`ParentMealPreferenceResponse` records in `backend/ChildCare.Contracts/Responses/MealPreferenceChangeRequestResponse.cs`
- [X] T010 [P] Create `UpsertMonthlyMenuRequest` record in `backend/ChildCare.Contracts/Requests/MonthlyMenuRequests.cs`
- [X] T011 [P] Create `SubmitMealPreferenceChangeRequestRequest`/`RejectMealPreferenceChangeRequestRequest` records in `backend/ChildCare.Contracts/Requests/MealPreferenceRequestRequests.cs`

**Checkpoint**: Schema, entities, and shared contracts exist. No endpoints yet — API client
regeneration happens per-phase once each phase's endpoints are mapped. User story implementation
can now begin.

---

## Phase 3: User Story 1 - Director creates and publishes a monthly menu (Priority: P1) 🎯 MVP

**Goal**: A director fills in a month's soup/main/dessert per day, saves as a draft, and
publishes it.

**Independent Test**: Create a monthly menu with entries for several days, save as draft
(confirm not yet parent-visible via the parent endpoint from US2, or by inspecting
`PublishedAt`), then publish (confirm `PublishedAt` is set).

### Tests for User Story 1

- [X] T012 [P] [US1] Integration test: `PUT /api/locations/{locationId}/monthly-menus/{year}/{month}` creates a draft menu when none exists for that location/year/month, and a second `PUT` updates the existing menu rather than creating a duplicate (FR-001, FR-005), in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`
- [X] T013 [P] [US1] Integration test: `GET` before publish returns `isPublished: false`; `POST .../publish` sets `publishedAt`; the director `GET` returns the menu regardless of publish state (FR-002, FR-003), in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`
- [X] T014 [P] [US1] Integration test: `POST .../publish` and `POST .../unpublish` both return `404` when no menu exists yet for that location/year/month, in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`
- [X] T015 [P] [US1] Integration test: a non-Director caller receives `403` on every director monthly-menu endpoint, in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Create `GetMonthlyMenuQuery` + handler in `backend/ChildCare.Application/MonthlyMenus/GetMonthlyMenuQuery.cs` — returns `exists: false` shell when no row exists (depends on T003, T004, T008)
- [X] T017 [US1] Create `UpsertMonthlyMenuCommand` + handler + validator in `backend/ChildCare.Application/MonthlyMenus/UpsertMonthlyMenuCommand.cs` — find-or-create the `MonthlyMenu` row (draft), replace the full `MonthlyMenuDay` set for the month; validator rejects any `date` outside the URL's `year`/`month` and enforces the 500-char field lengths (depends on T003, T004, T010)
- [X] T018 [P] [US1] Create `PublishMonthlyMenuCommand` + handler in `backend/ChildCare.Application/MonthlyMenus/PublishMonthlyMenuCommand.cs` — sets `PublishedAt = NOW()`, fails with `NotFound` if no menu row exists (depends on T003)
- [X] T019 [P] [US1] Create `UnpublishMonthlyMenuCommand` + handler in `backend/ChildCare.Application/MonthlyMenus/UnpublishMonthlyMenuCommand.cs` — sets `PublishedAt = null`, fails with `NotFound` if no menu row exists (depends on T003)
- [X] T020 [US1] Create `backend/ChildCare.Api/Endpoints/MonthlyMenuEndpoints.cs` with the director group (`GET`/`PUT`/`.../publish`/`.../unpublish` on `/api/locations/{locationId}/monthly-menus/{year}/{month}`) under `DirectorOnly`, mapping the `404` `NotFound` failure from T018/T019 (depends on T016, T017, T018, T019)
- [X] T021 Regenerate `web/lib/generated/api-types.ts` via `npm run generate-api-client` against the local backend running with the new migration applied (depends on T020)
- [X] T022 [US1] Create `web/components/menu/MonthlyMenuDayGrid.tsx` — day-by-day soup/main/dessert/notes inputs for the selected month, Save draft / Publish / Un-publish actions, clearly distinguishing Publish from Un-publish (label + icon, not color alone) (depends on T021)
- [X] T023 [US1] Create `web/app/(app)/menu/page.tsx` — location selector + month selector, renders `MonthlyMenuDayGrid`, mirrors `web/app/(app)/meal-list/page.tsx`'s existing location-selector pattern (depends on T022)
- [X] T024 [US1] Add a "Menu" entry (icon: `CalendarRange`, from `lucide-react`) to `REAL_NAV` in `web/components/Sidebar.tsx`, linking to `/menu` (depends on T023)
- [X] T025 [P] [US1] Add `menu.*` i18n keys (page title, day grid field labels, Save/Publish/Un-publish buttons, month/location selectors) to `web/i18n/locales/{en,fr,nl}.json`
- [X] T026 [P] [US1] Web component test: `MonthlyMenuDayGrid` calls the correct save/publish/unpublish handlers and renders each day's fields, in `web/__tests__/MonthlyMenuDayGrid.test.tsx`

**Checkpoint**: A director can create, save as draft, publish, and un-publish a monthly menu
entirely from the web admin — independently testable and deployable as the MVP's supply side.

---

## Phase 4: User Story 2 - Parent views the current month's published menu (Priority: P1)

**Goal**: A parent sees the current month's published menu for their child's location(s), with
closure days greyed out and the child's own preference shown alongside.

**Independent Test**: Publish a menu for the current month (US1), open the parent-facing read
endpoint/screen, and confirm the correct days/courses render, closure days are visibly
distinguished, and the "Menu nog niet beschikbaar" placeholder appears for a location with no
published menu.

### Tests for User Story 2

- [X] T027 [P] [US2] Integration test: `GET /api/parent/monthly-menu` returns one entry per distinct location where the requesting parent has an active-contract child, `isPublished: false` with empty `days` for a location with no published menu, and includes `closureDates` for the requested month (FR-006, FR-008, FR-009, research.md R4/R5), in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`
- [X] T028 [P] [US2] Integration test: a child with active contracts at two different locations produces two distinct entries, each correctly labeled by `locationName` (FR-018, research.md R5), in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`
- [X] T029 [P] [US2] Integration test: a parent with no linked child at a given location never sees that location's menu, even if published (tenant/authorization scoping), in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`

### Implementation for User Story 2

- [X] T030 [US2] Create `GetParentMonthlyMenuQuery` + handler in `backend/ChildCare.Application/MonthlyMenus/GetParentMonthlyMenuQuery.cs` — resolves distinct active-contract `LocationId`s across every child linked to the requesting parent via `ChildContacts`/`Contract` (research.md R5), reads each location's published `MonthlyMenu`/`MonthlyMenuDay` rows, and calls `IClosureCalendarReader.ListPublishedClosureDatesAsync` per location for the requested month (research.md R4) (depends on T003, T004, T008)
- [X] T031 [US2] Add the parent group (`GET /api/parent/monthly-menu`, defaulting `year`/`month` to the current Europe/Brussels month) to `backend/ChildCare.Api/Endpoints/MonthlyMenuEndpoints.cs` under `ParentOnly` (depends on T020, T030)
- [X] T032 Regenerate `parent-mobile/services/generated/api-types.ts` via `npm run generate-api-client` against the same backend (depends on T031)
- [X] T033 [US2] Create `parent-mobile/services/menu.ts` — fetch `/api/parent/monthly-menu`, cache on success, fall back to the last cached result on failure, mirroring `healthSummary.ts`'s fetch-then-cache-fallback shape. Deviation: that precedent (`mobile/services/healthSummary.ts`) lives in the caregiver app on top of feature 008's SQLite offline-sync store, which parent-mobile deliberately has none of (spec.md Assumptions: parents are expected to have network access, no offline queue). No persistence library existed in parent-mobile at all (checked `package.json` — no AsyncStorage, no zustand `persist` usage). Implemented as an in-memory, per-year/month cache scoped to the current app session (resets on cold start) — satisfies the fetch-then-cache-fallback *behavior* T038 tests without introducing a new persistence dependency for one read (depends on T032)
- [X] T034 [US2] Create `parent-mobile/app/(app)/menu/index.tsx` — renders each location's month grid (soup/main/dessert per day, "—" for a day with no entries), closure days greyed out and labeled (not color alone), and the "Menu nog niet beschikbaar" placeholder per-location when `isPublished` is `false` (depends on T033)
- [X] T035 [US2] Add a `menu` `Tabs.Screen` entry to `parent-mobile/app/(app)/_layout.tsx`, alongside the existing `index`/`gallery`/`messages`/`notifications`/`settings` tabs (depends on T034)
- [X] T036 [P] [US2] Add `menu.*` i18n keys (tab label, "Menu nog niet beschikbaar", closure-day label, day-of-week/course headers) to `parent-mobile/i18n/locales/{en,fr,nl}.json`
- [X] T037 [P] [US2] Mobile test: menu screen renders a published menu correctly, the unpublished placeholder for a location with none, a blank day as "—", and closure days with distinct (non-color-only) styling, in `parent-mobile/__tests__/menu.test.tsx`
- [X] T038 [P] [US2] Mobile test: offline-cache-fallback path renders the previously-cached menu when the fetch fails, mirroring 013c's health-summary cache-fallback test precedent. Deviation: written as a direct service-level test of `getMonthlyMenu`'s cache logic in `parent-mobile/__tests__/services/menu.test.ts` (mirrors `mobile/__tests__/services/healthSummary.test.ts`'s own file location), not the screen-level `menu.test.tsx` — matches this codebase's actual convention of testing cache-fallback logic at the service layer, not through a rendered screen (depends on T033)

**Checkpoint**: A parent can open the Menu tab and see every one of their child's location(s)'
published menus — independently testable and, together with US1, the feature's complete MVP
(publish → view).

---

## Phase 5: User Story 3 - Parent requests a meal-preference change (Priority: P2)

**Goal**: A parent submits a new texture/dietary-tag/note request for their child.

**Independent Test**: Submit a preference-change request from the parent app and confirm it is
created with `status: "pending"` and a second submission for the same child is rejected.

### Tests for User Story 3

- [X] T039 [P] [US3] Integration test: `POST /api/parent/children/{childId}/meal-preference-requests` creates a `Pending` request for a linked child, and `MealPreference` is unchanged until a decision is made (FR-011), in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T040 [P] [US3] Integration test: a second `POST` for the same child while one request is still `Pending` returns `409` with `errorKey: "errors.meal_preference_requests.duplicate_pending"` (FR-012, research.md R6), in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T041 [P] [US3] Integration test: `POST` for a child the requester is not linked to via `ChildContacts` returns `403` (research.md R2), in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T042 [P] [US3] Integration test: `GET /api/parent/children/{childId}/meal-preference` returns `hasPendingRequest: true` after submission, and `texture`/`dietaryType: null` for a child with no `MealPreference` row yet, in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] Create `SubmitMealPreferenceChangeRequestCommand` + handler + validator in `backend/ChildCare.Application/MealPreferenceRequests/SubmitMealPreferenceChangeRequestCommand.cs` — resolves the parent via `ICurrentParentContactResolver` + `ChildContacts` check (research.md R2), rejects a duplicate pending request (research.md R6), and requires at least one of `NewTexture`/`NewDietaryType` (depends on T005, T009, T011)
- [X] T044 [US3] Create `GetParentChildMealPreferenceQuery` + handler in `backend/ChildCare.Application/MealPreferenceRequests/GetParentChildMealPreferenceQuery.cs` — reads the child's `MealPreference` row (013d, `null` fields if none) plus whether a `Pending` `MealPreferenceChangeRequest` currently exists for that child (depends on T005, T009)
- [X] T045 [US3] Create `backend/ChildCare.Api/Endpoints/MealPreferenceRequestEndpoints.cs` with the parent group (`GET /api/parent/children/{childId}/meal-preference`, `POST /api/parent/children/{childId}/meal-preference-requests`) under `ParentOnly`, mapping the `403`/`409` failures per contracts/monthly-menu-api.md (depends on T043, T044)
- [X] T046 Regenerate `parent-mobile/services/generated/api-types.ts` (and `web/lib/generated/api-types.ts`, mechanical) (depends on T045)
- [X] T047 [US3] Create `parent-mobile/services/mealPreferenceRequests.ts` — `getMealPreference(childId)`, `submitMealPreferenceChangeRequest(childId, body)`, mirroring `dayReservations.ts`'s thin-wrapper shape (depends on T046)
- [X] T048 [US3] Create `parent-mobile/app/(app)/menu/request-preference-change.tsx` — texture select, dietary-tag multi-select, free-text note, submit; shows a clear inline error on a `409` duplicate-pending response (depends on T047)
- [X] T049 [US3] Add the child's meal-preference indicator plus a "Voorkeur aanpassen" entry point to `parent-mobile/app/(app)/menu/index.tsx`, linking to the new request screen and reflecting `hasPendingRequest` (button relabeled/disabled while pending) (depends on T034, T048)
- [X] T050 [P] [US3] Add `mealPreferenceRequests.*` i18n keys (indicator labels, form fields, pending state, duplicate-request error) to `parent-mobile/i18n/locales/{en,fr,nl}.json`
- [X] T051 [P] [US3] Mobile test: the preference-change form submits successfully and shows the inline duplicate-pending error on a `409` response, in `parent-mobile/__tests__/request-preference-change.test.tsx`

**Checkpoint**: A parent can see their child's current preference and submit a change request —
independently testable; the director-side review queue (US4) is not required for this story's
own acceptance scenarios to pass.

---

## Phase 6: User Story 4 - Director reviews and decides a preference-change request (Priority: P2)

**Goal**: A director sees a pending request alongside the child's active health records and
approves or rejects it.

**Independent Test**: Submit a request (US3), open the director review queue, approve it, and
confirm `MealPreference` reflects the change and the parent receives a decision notification;
separately, reject a request with a reason and confirm the notification includes it.

### Tests for User Story 4

- [X] T052 [P] [US4] Integration test: `GET /api/meal-preference-requests?status=pending` returns pending requests each paired with the child's currently-active `HealthRecord` rows (`ValidFrom`/`ValidUntil` covering today) (FR-013), in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T053 [P] [US4] Integration test: `POST .../approve` creates or updates `MealPreference` with the requested texture/dietary tags via the existing upsert path, marks the request `Approved` with `decidedBy`/`decidedAt` set, and sends a decision notification; approving a non-`Pending` request returns `409` (research.md R1); a texture-only request approved against a child with an existing `MealPreference` row leaves that row's `PortionSize`/`AdditionalNotes`/dietary tags unchanged (FR-014's partial-write-through rule), in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T054 [P] [US4] Integration test: `POST .../reject` leaves `MealPreference` unchanged, marks the request `Rejected` with `decisionNotes` set when a reason is given, and the notification body differs between a reason present vs. absent (research.md R3, FR-016), in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T055 [P] [US4] Integration test: a non-Director caller receives `403` on every director meal-preference-request endpoint, in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs`
- [X] T056 [P] [US4] Integration test: approving a request whose target child has since been deactivated fails cleanly with a clear error and modifies neither `MealPreference` nor the request's decided state, in `backend/ChildCare.Api.Tests/MealPreferenceRequests/MealPreferenceRequestTests.cs` (spec.md Edge Cases)

### Implementation for User Story 4

- [X] T057 [US4] Create `ListMealPreferenceChangeRequestsQuery` + handler in `backend/ChildCare.Application/MealPreferenceRequests/ListMealPreferenceChangeRequestsQuery.cs` — filters by `status` (default `pending`), joins each request's child's currently-active `HealthRecord` rows (013c) for context (depends on T005, T009)
- [X] T058 [US4] Create `MealPreferenceRequestNotificationService` in `backend/ChildCare.Application/MealPreferenceRequests/MealPreferenceRequestNotificationService.cs`, mirroring `DayReservationNotificationService`'s exact shape — resolves the requesting parent's `Contact`, writes an in-app `Notification` row (`NotificationType.MealPreferenceRequestDecided`), sends an Expo push if a token is registered (try/catch, logged not thrown), and uses a distinct i18n `BodyKey` when `DecisionNotes` is non-blank vs. blank (research.md R3). Also registered the service in `Program.cs`'s DI container (`AddScoped`) — missing at first, caught by T053/T054's own tests failing with a 500 `InvalidOperationException` (unresolvable service), not by inspection (depends on T002)
- [X] T059 [US4] Create `ApproveMealPreferenceChangeRequestCommand` + handler in `backend/ChildCare.Application/MealPreferenceRequests/ApproveMealPreferenceChangeRequestCommand.cs` — sends `UpsertMealPreferenceCommand` via `IMediator` (research.md R1), which itself rejects a deactivated child (013d's existing validator) — that failure MUST propagate as a clean error, not a swallowed no-op (spec.md Edge Cases); only updates the fields the request actually specified, per FR-014's partial-write-through rule; marks the request `Approved` on success, calls the notification service; fails with `Conflict` if not currently `Pending` (depends on T005, T058)
- [X] T060 [US4] Create `RejectMealPreferenceChangeRequestCommand` + handler in `backend/ChildCare.Application/MealPreferenceRequests/RejectMealPreferenceChangeRequestCommand.cs` — marks the request `Rejected` with `DecisionNotes`, calls the notification service; fails with `Conflict` if not currently `Pending` (depends on T005, T058)
- [X] T061 [US4] Add the director group (`GET /api/meal-preference-requests`, `POST .../approve`, `POST .../reject`) to `backend/ChildCare.Api/Endpoints/MealPreferenceRequestEndpoints.cs` under `DirectorOnly`, mapping the `409` `Conflict` failure (depends on T045, T057, T059, T060)
- [ ] T062 Regenerate `web/lib/generated/api-types.ts` (depends on T061)
- [ ] T063 [US4] Create `web/components/menu/MealPreferenceRequestQueue.tsx` — pending-request list, each item showing requested texture/dietary tags, the parent's note, the child's active health records, and Approve / Reject (with an optional reason field) actions (depends on T062)
- [ ] T064 [US4] Wire `MealPreferenceRequestQueue` into `web/app/(app)/menu/page.tsx`, alongside the day-grid authoring section (depends on T023, T063)
- [ ] T065 [P] [US4] Add `mealPreferenceRequests.*` i18n keys (queue labels, approve/reject actions, reason field, status badges — pairing icon with color per design-system.md) to `web/i18n/locales/{en,fr,nl}.json`
- [ ] T066 [US4] Locate the existing `NotificationType`-to-i18n-key mapping that already renders `DayReservationDecided` (web notifications screen and `parent-mobile/app/(app)/notifications/`) and add the `MealPreferenceRequestDecided` case alongside it, so the new notification type renders correctly rather than falling through to a generic/unknown case (depends on T002)
- [ ] T067 [P] [US4] Web component test: `MealPreferenceRequestQueue` renders pending requests with health-record context and calls the approve/reject handlers, in `web/__tests__/MealPreferenceRequestQueue.test.tsx`

**Checkpoint**: The full request → review → decision → notification loop works end-to-end — all
four P1/P2 user stories independently verified.

---

## Phase 7: User Story 5 - Director corrects a published menu mid-month (Priority: P3)

**Goal**: A director un-publishes a menu with a typo, corrects it, and re-publishes.

**Independent Test**: Publish a menu, un-publish it (confirm it becomes parent-invisible again),
correct a day's entry, and re-publish (confirm parents see the corrected value).

### Tests for User Story 5

- [ ] T068 [P] [US5] Integration test: publish → unpublish → edit a day's field via `PUT` → re-publish round trip results in the parent-facing `GET /api/parent/monthly-menu` reflecting only the corrected value, never the un-published intermediate draft state (FR-003, FR-004), in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuTests.cs`

### Implementation for User Story 5

- [ ] T069 [US5] Verify (and adjust if needed) that `MonthlyMenuDayGrid.tsx`'s Un-publish action is visually and textually distinct from Publish — separate labels/icons per design-system.md's icon-pairing convention, not just a toggled button state that could be mis-tapped (depends on T022)
- [ ] T070 [P] [US5] Web component test: `MonthlyMenuDayGrid`'s Un-publish action calls the unpublish endpoint and updates local state back to draft, in `web/__tests__/MonthlyMenuDayGrid.test.tsx`

**Checkpoint**: All five user stories independently verified end-to-end.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T071 Extend `TenantMigrationRolloutTests`' schema-revert helper for the three new tables' FKs (`monthly_menu_days` before `monthly_menus` in FK-dependency order; `meal_preference_change_requests` independently, FK to `children`), following the same fix every migration-adding feature since 012a has needed (research.md R7), in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`
- [ ] T072 [P] Run through quickstart.md's four scenarios end-to-end locally (backend + web + parent-mobile) and fix any discrepancy found
- [ ] T073 [P] Verify NL/FR/EN i18n key parity (no missing translation in any locale file touched by T025/T036/T050/T065) across `web/i18n/locales/` and `parent-mobile/i18n/locales/`
- [ ] T074 [P] Verify FR-019: confirm no monthly-menu or meal-preference-request endpoint is reachable under any caregiver device-token authorization policy — review `MonthlyMenuEndpoints.cs`/`MealPreferenceRequestEndpoints.cs` for `DeviceOrStaffOrDirector`/`DeviceOrDirector`-style policies and confirm neither is used here

---

## Dependencies & Execution Order

- **Phase 2 (Foundational)** blocks all user stories.
- **US1 (Phase 3)** and **US2 (Phase 4)** are both P1. US2 depends on US1's director endpoints
  existing (a parent can only view what a director has published) but not on US1's *web UI* being
  built — the backend piece of US2 (T030-T032) can start as soon as Phase 2 and T020 (US1's
  endpoint file) are done, in parallel with US1's own web UI tasks (T022-T026).
- **US3 (Phase 5)** depends only on Foundational (the `MealPreferenceChangeRequest` entity) and
  013d's existing `MealPreference`/`UpsertMealPreferenceCommand` — independent of US1/US2.
- **US4 (Phase 6)** depends on US3 (a request must exist to review) and reuses US1's endpoint file
  (`MealPreferenceRequestEndpoints.cs` is new, but the notification service mirrors an existing
  013a pattern with no direct code dependency on US1/US2).
- **US5 (Phase 7)** reuses US1's publish/unpublish endpoints and `MonthlyMenuDayGrid.tsx`
  entirely — effectively a verification + UI-polish pass, not new backend surface.
- **Phase 8 (Polish)** last.

## Parallel Execution Examples

- Phase 2: T001, T002, T003, T004, T005 in parallel (independent enum/entity files, T005 waits on
  T001) → T006 → T007; T008, T009, T010, T011 in parallel.
- Phase 3 (US1): T012-T015 in parallel (same test file, independent test methods — parallelize
  authorship, not file writes) → T016, T017 → T018, T019 in parallel → T020 → T021 → T022 → T023
  → T024; T025 in parallel with T023/T024; T026 after T022.
- Phase 4 (US2) backend (T027-T032) can run in parallel with Phase 3's web UI tasks (T022-T026)
  once T020 is done; its own mobile UI tasks (T033-T038) are sequential after T032.
- Phase 5 (US3) and Phase 6 (US4) share no files until T061 (US4's endpoint file), so US3's full
  stack (T039-T051) can be built in parallel with US1/US2 once Foundational is done; US4 must wait
  for US3's entity/commands (T043) to exist for its own tests to have data to review.

## Implementation Strategy

**MVP scope**: Phase 2 (Foundational) + Phase 3 (US1) + Phase 4 (US2) — a director can publish a
menu and a parent can see it. This is the feature's core value and, per the spec's own framing,
neither story alone is a complete MVP without the other (a menu nobody can view, or a viewer with
nothing to view, both fall short of the feature's purpose).

**Incremental delivery**: Foundational → US1 + US2 (P1, ship together) → US3 (P2, viewable
without a review queue) → US4 (P2, closes the loop US3 opens) → US5 (P3, verification pass over
US1's existing publish/unpublish) → Polish.
