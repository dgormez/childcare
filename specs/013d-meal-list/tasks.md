---
description: "Task list for feature 013d-meal-list"
---

# Tasks: Meal List (Maaltijdenlijst)

**Input**: Design documents from `/specs/013d-meal-list/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — Constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative flows; this repo's convention
(CLAUDE.md) also expects web/mobile component tests for new UI.

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/`, `mobile/` (see plan.md's Project Structure).

---

## Phase 1: Setup

**Purpose**: No new dependencies — `lucide-react-native` (mobile) and `lucide-react` (web) are
already installed (design-system.md's icon requirement is already satisfied). Nothing to
initialize; proceed directly to Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `MealPreference` entity end-to-end, the core aggregation query (present
children only — the "Inclusief verwacht" extension is US4), and both generated API clients.
Every user story depends on this phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 [P] Create `MealTexture` enum (`Pureed`, `Mixed`, `Pieces`, `Normal`) in `backend/ChildCare.Domain/Enums/MealTexture.cs`
- [X] T002 [P] Create `MealPortionSize` enum (`Small`, `Normal`, `Large`) in `backend/ChildCare.Domain/Enums/MealPortionSize.cs`
- [X] T003 [P] Create `DietaryType` enum (`Halal`, `Kosher`, `Vegetarian`, `Vegan`, `GlutenFree`) in `backend/ChildCare.Domain/Enums/DietaryType.cs`
- [X] T004 Create `MealPreference` entity (`Id`, `ChildId`, `Texture` default `Normal`, `DietaryType` as `List<DietaryType>`, `PortionSize` default `Normal`, `AdditionalNotes`, `UpdatedAt`, `UpdatedBy`, `CreatedAt`) in `backend/ChildCare.Domain/Entities/MealPreference.cs` (depends on T001, T002, T003)
- [X] T005 Add `MealPreferences` `DbSet<MealPreference>` and entity configuration to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` — unique index on `ChildId`, FK to `children(Id)`, `DietaryType` mapped to a native `text[]` column via a value converter (mirrors `ChildEvent.RecordedBy`'s `uuid[]` native-array precedent, adapted for an enum collection) (depends on T004)
- [X] T006 Generate the EF Core migration `AddChildMealPreferences` and its SQL script in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`, per this repo's manual-apply convention (research.md R7) (depends on T005)
- [X] T007 [P] Create `MealListResponse`/`MealListChildEntry`/`MealListGroupEntry` records (per contracts/meal-list-api.md's response shape) in `backend/ChildCare.Contracts/Responses/MealListResponse.cs`
- [X] T008 [P] Create `MealPreferenceResponse` record in `backend/ChildCare.Contracts/Responses/MealPreferenceResponse.cs`
- [X] T009 Create `GetMealListQuery(Guid LocationId, DateOnly Date, Guid? RestrictToGroupId)` and its handler in `backend/ChildCare.Application/MealPreferences/GetMealListQuery.cs` — joins `AttendanceRecord` (present today: `Status == Present`), `ChildGroupAssignment` (current group), `Child` (`AllergySeverity`), `HealthRecord` (`RecordType == MedicationStanding`, valid today), and left-joins `MealPreference`; excludes `Absent`/`Closure`; when `RestrictToGroupId` is set, filters to that group only (device-token case, research.md R4); groups results by group (depends on T004, T007)
- [X] T010 [P] Create `MealListMapper` in `backend/ChildCare.Application/MealPreferences/MealListMapper.cs` mapping the joined query result into `MealListResponse` — a child with no `MealPreference` row maps to `HasPreference = false` with default texture/portion/empty dietary tags (FR-005); `AllergySeverity` maps `Severe` → `"severe"`, `Mild`/`Moderate` → `"mild_moderate"`, `null` → `"none"` (research.md R1) (depends on T007)
- [X] T011 Create `backend/ChildCare.Api/Endpoints/MealListEndpoints.cs` with `GET /api/locations/{locationId}/meal-list?date=` under `DeviceOrStaffOrDirector` — for a device-token caller, read `DeviceTokenClaims.GroupId` from the claims principal and pass it as `RestrictToGroupId`; for a Director/Staff (user-JWT) caller, pass `null` (full location); map to `GetMealListQuery` (depends on T009, T010)
- [X] T012 Regenerate `web/lib/generated/api-types.ts` via `npm run generate-api-client` against the local backend running with the new migration applied (depends on T011)
- [X] T013 [P] Regenerate `mobile/services/generated/api-types.ts` via `npm run generate-api-client` against the same backend (depends on T011)

**Checkpoint**: `GET /api/locations/{locationId}/meal-list?date=` returns present children,
correctly excluding absent/closed children, correctly defaulting missing preferences, and
correctly scoped to a device's own group when called with a device token. Both generated API
clients include the new types. User story implementation can now begin.

---

## Phase 3: User Story 1 - Caregiver views today's meal list for their group (Priority: P1) 🎯 MVP

**Goal**: A caregiver on the room tablet sees, for each child present in their own group,
texture, dietary tags, allergy severity, and a standing-medication indicator.

**Independent Test**: Check in several children with different meal preferences and allergy
severities to a group, open the caregiver-tablet meal list, and confirm each present child's
indicators render correctly and no absent child appears.

### Tests for User Story 1

- [X] T014 [P] [US1] Integration test: `GET /api/locations/{locationId}/meal-list` with a device token scoped to one group returns only that group's present children, each with correct texture/dietary/portion/severity/medication fields, and never includes a child from another group or an absent/closed child, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`
- [X] T015 [P] [US1] Integration test: a present child with no `MealPreference` row returns `hasPreference: false` with default texture/portion/empty dietary tags, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`
- [X] T016 [P] [US1] Integration test: a child with `AllergySeverity = Severe` returns `allergySeverity: "severe"`; `Mild`/`Moderate` returns `"mild_moderate"`; unset returns `"none"` — and a child with a currently-valid `MedicationStanding` health record returns `hasStandingMedication: true`, an expired/future one returns `false`; `ValidFrom`/`ValidUntil` equal to today's date counts as valid (inclusive boundary, FR-008); a child with two simultaneously-valid `MedicationStanding` records still returns a single boolean `true`, not a count, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`
- [X] T017 [P] [US1] Integration test: a parent-role JWT calling this endpoint receives `403`, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`

### Implementation for User Story 1

- [X] T018 [P] [US1] Create `mobile/services/mealList.ts` — fetch `/api/locations/{locationId}/meal-list`, cache on success via `readCache.ts`'s `setCached`, fall back to `getCached` on failure, returning a `{ status: "loaded", data } | { status: "unavailable" }` union, mirroring `healthSummary.ts`'s exact shape (research.md R6)
- [X] T019 [US1] Create `mobile/app/(room)/meal-list.tsx` — reads the device's paired group implicitly via the endpoint (no client-side group filter needed), renders children grouped by presence with texture/dietary/portion/"Geen voorkeur" fallback, an allergy severity badge (icon + color, never color alone — FR-007), and a pill icon for standing medication; landscape layout, 48pt+ touch targets, empty state (icon + one sentence) when no children present (depends on T018)
- [X] T020 [US1] Add an entry point to the meal list from `mobile/app/(room)/index.tsx` (room home screen) (depends on T019)
- [X] T021 [P] [US1] Add `mealList.*` i18n keys (screen title, texture labels, dietary tag labels, portion labels, "Geen voorkeur", allergy severity labels, empty state) to `mobile/i18n/locales/{en,fr,nl}.json`
- [X] T022 [P] [US1] Mobile test: meal-list screen renders present children with correct indicators and excludes absent children, in `mobile/__tests__/meal-list.test.tsx`
- [X] T023 [P] [US1] Mobile test: offline-cache-fallback path renders the previously-cached meal list when the fetch fails, mirroring the health-summary cache-fallback test precedent, in `mobile/__tests__/meal-list.test.tsx` (depends on T018)

**Checkpoint**: A caregiver can open the meal list from the room home screen and see their own
group's present children with correct, glanceable indicators — independently testable and
deployable as the MVP.

---

## Phase 4: User Story 2 - Director views and prints the full-location meal list (Priority: P1)

**Goal**: A director sees every present child across all groups at a location, grouped by
group/section, with a Print button producing a black-and-white-legible layout.

**Independent Test**: Open the Maaltijdenlijst page for a location with children present across
two groups, confirm both groups' children appear grouped correctly, and confirm the print output
keeps allergy severity distinguishable without color.

### Tests for User Story 2

- [X] T024 [P] [US2] Integration test: `GET /api/locations/{locationId}/meal-list` called by a Director JWT (no device token) returns children from every group at that location, grouped correctly, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`
- [X] T025 [P] [US2] Web component test: `AllergySeverityBadge` renders a distinct icon per severity level (not color alone), in `web/__tests__/AllergySeverityBadge.test.tsx`
- [X] T026 [P] [US2] Web component test: `MealListTable` groups children by group/section and renders "Geen voorkeur" for a child with no preference, in `web/__tests__/MealListTable.test.tsx`

### Implementation for User Story 2

- [X] T027 [P] [US2] Create `web/components/meal-list/AllergySeverityBadge.tsx` — pill badge pairing icon + color per `design-system.md`'s Status Indicators convention (`danger`/`warning`/no-badge for severe/mild-or-moderate/none), reusable by both web and referenced as the visual spec for the mobile equivalent
- [X] T028 [US2] Create `web/components/meal-list/MealListTable.tsx` — high-density table (per `platform-rules.md`'s Director Web density rules) grouped by group/section, each row showing texture/dietary tags/portion/`AllergySeverityBadge`/medication pill icon, "Geen voorkeur" fallback (depends on T027)
- [X] T029 [US2] Create `web/app/(app)/meal-list/page.tsx` — location selector (if the director's tenant has more than one location), date (defaults to today), renders `MealListTable`, and a Print button; add a `@media print` stylesheet (co-located `print.css` or a scoped `<style>` block) that removes navigation chrome and keeps `AllergySeverityBadge`'s icon visible in grayscale (depends on T028)
- [X] T030 [US2] Add a "Maaltijdenlijst" entry (icon: `Utensils`, from `lucide-react`) to `REAL_NAV` in `web/components/Sidebar.tsx`, linking to `/meal-list`
- [X] T031 [P] [US2] Add `mealList.*` i18n keys (page title, table column headers, Print button, location/date labels) to `web/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: A director can open, view, and print the full-location meal list — independently
testable alongside US1.

---

## Phase 5: User Story 3 - Director edits a child's meal preferences (Priority: P2)

**Goal**: A director sets or updates a child's texture, dietary tags, portion size, and notes
from the child profile.

**Independent Test**: Set a child's texture to `mixed` and dietary tags to `["halal"]` from the
child profile, then confirm the meal list reflects the change on next load.

### Tests for User Story 3

- [X] T032 [P] [US3] Integration test: `PUT /api/children/{childId}/meal-preferences` with no existing row creates one with the submitted values, in `backend/ChildCare.Api.Tests/MealList/UpsertMealPreferenceTests.cs`
- [X] T033 [P] [US3] Integration test: a second `PUT` call updates only the fields present in the request body, leaving previously-set fields unchanged (partial-upsert semantics, data-model.md), in `backend/ChildCare.Api.Tests/MealList/UpsertMealPreferenceTests.cs`
- [X] T034 [P] [US3] Integration test: `additionalNotes` over the max length returns `422` and writes nothing; a non-Director caller receives `403`, in `backend/ChildCare.Api.Tests/MealList/UpsertMealPreferenceTests.cs`
- [X] T035 [P] [US3] Web component test: `ChildMealPreferenceForm` submits only changed fields and shows a validation error for an over-length note, in `web/__tests__/ChildMealPreferenceForm.test.tsx`

### Implementation for User Story 3

- [X] T036 [P] [US3] Create `UpsertMealPreferenceRequest` in `backend/ChildCare.Contracts/Requests/MealPreferenceRequests.cs` (all fields optional per contracts/meal-list-api.md)
- [X] T037 [US3] Create `UpsertMealPreferenceCommand` + handler in `backend/ChildCare.Application/MealPreferences/UpsertMealPreferenceCommand.cs` — creates the row if absent (applying column defaults for omitted fields) or updates only the submitted fields if present (partial upsert), sets `UpdatedAt`/`UpdatedBy` (depends on T004, T036)
- [X] T038 [P] [US3] Create `UpsertMealPreferenceCommandValidator` in `backend/ChildCare.Application/MealPreferences/UpsertMealPreferenceCommandValidator.cs` — `ChildId` must reference an existing, non-deactivated child; `AdditionalNotes` `MaximumLength(2000)` (mirrors feature 012a's precedent) (depends on T037)
- [X] T039 Add `PUT /api/children/{childId}/meal-preferences` (`DirectorOnly`) to `backend/ChildCare.Api/Endpoints/MealListEndpoints.cs`, mapping to `UpsertMealPreferenceCommand` and returning `MealPreferenceResponse` (depends on T008, T037, T038)
- [X] T040 Regenerate both `web/lib/generated/api-types.ts` and `mobile/services/generated/api-types.ts` after this endpoint change (depends on T039)
- [X] T041 [US3] Create `web/components/children/ChildMealPreferenceForm.tsx` — texture/dietary-tags/portion/notes fields, submits only changed fields, inline validation error on over-length notes; wire into the existing child-detail screen (`web/app/(app)/children/[id]/page.tsx`, alongside the existing Profiel/Gezondheid tabs) (depends on T040)
- [X] T042 [P] [US3] Add `mealList.preferenceForm.*` i18n keys (field labels, validation error) to `web/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: A director can set/update any child's meal preference and see it reflected on
the meal list — independently testable; US1/US2 already render "Geen voorkeur" correctly without
this story, so this is a pure additive capability.

---

## Phase 6: User Story 4 - "Inclusief verwacht" toggle shows expected children (Priority: P3)

**Goal**: A toggle reveals children contracted for today but not yet checked in, in a separate
"Verwacht" section.

**Independent Test**: With a child contracted for today but not yet checked in, confirm they are
absent from the default view, and confirm they appear in a separate "Verwacht" section only when
the toggle is enabled.

### Tests for User Story 4

- [X] T043 [P] [US4] Integration test: `GET /api/locations/{locationId}/meal-list?includeExpected=true` returns a child with an active contract covering today's weekday and no attendance record yet in a separate `expected` block; the same request with `includeExpected=false` (or omitted) excludes that child entirely, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`
- [X] T044 [P] [US4] Integration test: a child with an active contract for today but an existing `Absent` or `Closure` attendance record is never included in `expected`, in `backend/ChildCare.Api.Tests/MealList/MealListAggregationTests.cs`

### Implementation for User Story 4

- [X] T045 [US4] Extend `GetMealListQuery`/handler in `backend/ChildCare.Application/MealPreferences/GetMealListQuery.cs` with an `IncludeExpected` flag — when true, join active `Contract`/`ContractedDay` (007) for today's weekday against children at this location with no `AttendanceRecord` for the date, and add them to a flat `Expected` block on the response (research.md R2) (depends on T009)
- [X] T046 Wire `includeExpected` query param through `MealListEndpoints.cs`'s `GET` handler (depends on T011, T045)
- [X] T047 [P] [US4] Add an "Inclusief verwacht" toggle to `web/app/(app)/meal-list/page.tsx`, rendering a visually distinct "Verwacht" section when enabled (depends on T029, T046)
- [X] T048 [P] [US4] Add the same toggle to `mobile/app/(room)/meal-list.tsx` (depends on T019, T046)
- [X] T049 [P] [US4] Add `mealList.expected.*` i18n keys ("Inclusief verwacht", "Verwacht" section header) to both `web/i18n/locales/{en,fr,nl}.json` and `mobile/i18n/locales/{en,fr,nl}.json`

**Checkpoint**: All four user stories complete and independently verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T050 Extend `TenantMigrationRolloutTests`' schema-revert helper for `child_meal_preferences`' FK to `children`, following the same fix every migration-adding feature since 003 has needed (research.md R8), in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`
- [X] T051 [P] Run through quickstart.md's scenarios end-to-end locally (backend + web + mobile) and fix any discrepancy found
- [X] T052 [P] Verify NL/FR/EN i18n key parity (no missing translation in any of the three locale files touched by T021/T031/T042/T049) across `web/i18n/locales/` and `mobile/i18n/locales/`
- [X] T053 [P] Verify FR-015 cross-platform field parity: with the same child data, confirm the director-web `MealListTable` row (T028) and the caregiver-tablet meal-list row (T019) render the identical field set (texture, dietary tags, portion size, allergy severity, standing-medication indicator, "Geen voorkeur" fallback) — no field present on one platform and missing on the other

---

## Dependencies & Execution Order

- **Phase 2 (Foundational)** blocks all user stories.
- **US1 (Phase 3)** and **US2 (Phase 4)** are both P1 and independent of each other (mobile vs.
  web consumers of the same Foundational query) — can be implemented in parallel.
- **US3 (Phase 5)** depends only on Foundational (the `MealPreference` entity); it does not
  depend on US1/US2 being complete, since both already render "Geen voorkeur" correctly without
  it.
- **US4 (Phase 6)** extends the Foundational query and touches both US1's and US2's screens —
  implement after US1/US2 for a clean diff, though its backend piece (T045-T046) could be built
  in parallel with US3.
- **Phase 7 (Polish)** last.

## Parallel Execution Examples

- Phase 2: T001, T002, T003 in parallel (independent enum files) → T004 → T005 → T006; T007, T008
  in parallel; T009 → T010 → T011 → T012, T013 in parallel.
- Phase 3 (US1): T014-T017 in parallel (same test file, independent test methods — parallelize
  authorship, not file writes) → T018 → T019 → T020; T021 in parallel with T019/T020; T022, T023
  in parallel after T018.
- Phase 4 (US2) can run fully in parallel with Phase 3 (US1) once Phase 2 is done — different
  files, same underlying endpoint.

## Implementation Strategy

**MVP scope**: Phase 2 (Foundational) + Phase 3 (US1) — a caregiver can see their own group's
meal list with safe defaults ("Geen voorkeur") even before any director has set a single
preference, since FR-005 guarantees every child still renders. Phase 4 (US2) is equally P1 and
should ship in the same release before this feature is considered complete, but US1 alone is
independently demonstrable.

**Incremental delivery**: Foundational → US1 + US2 (P1, ship together) → US3 (P2) → US4 (P3) →
Polish.
