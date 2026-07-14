# Tasks: Monthly Menu Variants

**Input**: Design documents from `specs/013j-monthly-menu-variants/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web/parent-mobile), same standard every prior feature has
followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [ ] T001 [P] Add `MenuVariantPriorityOrder` (`string[]`) to `LocationResponse` and a new `UpdateLocationMenuVariantSettingsRequest(string[] MenuVariantPriorityOrder)` in `backend/ChildCare.Contracts/Responses/LocationResponse.cs` and `backend/ChildCare.Contracts/Requests/LocationRequests.cs`
- [ ] T002 [P] Add `variant` as an optional field on `UpsertMonthlyMenuRequest` (or route/query passthrough only, no request-body change if query-string) and extend `MonthlyMenuResponse`/`ParentMonthlyMenuEntry` per data-model.md (`variant` on the director response, `childId`/`childName`/`resolvedVariant` on the parent entry) in `backend/ChildCare.Contracts/Requests/MonthlyMenuRequests.cs` and `backend/ChildCare.Contracts/Responses/MonthlyMenuResponse.cs`
- [ ] T003 [P] Add director-web `locations.menuVariants.*` i18n keys (settings section title, enable/disable per `DietaryType`, priority reorder controls, save action) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json` — reuse the existing `mealPreferenceRequests.dietaryType.*` keys for the five display labels rather than duplicating them
- [ ] T004 [P] Add director-web `menu.variantSelector.*` i18n keys (selector label, "Base menu" option label) to the same three locale files
- [ ] T005 [P] Add parent-mobile `menu.variantLabel` i18n key (e.g. "{variant} menu for {childName}") to `parent-mobile/i18n/locales/en.json`, `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json` — reuse the existing `mealPreferenceRequests.dietaryType.*` keys for the variant name itself

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The schema, entity, and EF configuration changes every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T006 Add `Variant` (domain `DietaryType?`, sentinel-mapped per research.md) to `backend/ChildCare.Domain/Entities/MonthlyMenu.cs`
- [ ] T007 Add `MenuVariantPriorityOrder` (`List<DietaryType>`, default empty) to `backend/ChildCare.Domain/Entities/Location.cs`
- [ ] T008 Configure `MonthlyMenu.Variant` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` — `HasConversion` to/from the `"base"` sentinel using `DietaryTypeExtensions.ToWireString`/`TryParseWireString`, `HasColumnType("text")`, `IsRequired()`, and replace the existing `(LocationId, Year, Month)` unique index with `(LocationId, Year, Month, Variant)`
- [ ] T009 Configure `Location.MenuVariantPriorityOrder` in the same file — `text[]` conversion + `ValueComparer`, identical pattern to `MealPreference.DietaryType`'s existing configuration
- [ ] T010 Add tenant migration `AddMonthlyMenuVariants` (new `Variant` column backfilled to `'base'` for existing rows, index rebuild, new `Location` column defaulted to `'{}'`) in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [ ] T011 [P] Extend `MonthlyMenuMapper` (`ToResponse`, `EmptyShell`) to include `variant` in `backend/ChildCare.Application/MonthlyMenus/MonthlyMenuMapper.cs`
- [ ] T012 [P] Extend `LocationMapper.ToResponse` with `menuVariantPriorityOrder` in `backend/ChildCare.Application/Locations/LocationMapper.cs`
- [ ] T013 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new column/index (the recurring pattern every migration-adding feature since 003 has needed — 012a, 013c, 006a, 013d, 013g, 013h, and 013e itself) in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`

**Checkpoint**: Schema and mapping foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Director configures which variants a location offers (Priority: P1) 🎯 MVP

**Goal**: A director can enable a set of `DietaryType` variants for a location and set their
priority order; a location with none enabled behaves exactly as before this feature.

**Independent Test**: `PUT /api/locations/{id}/menu-variant-settings` with `["halal",
"vegetarian"]`, re-fetch via `GET /api/locations/{id}`, confirm the order persisted and a second
location's settings are untouched.

### Tests for User Story 1

- [ ] T014 [P] [US1] Integration test: a location with no `menu-variant-settings` call ever made returns an empty `menuVariantPriorityOrder` (FR-001 default) in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuVariantSettingsTests.cs`
- [ ] T015 [P] [US1] Integration test: `PUT .../menu-variant-settings` persists the array in the given order, leaves every other location's settings unchanged (FR-002) in the same file
- [ ] T016 [P] [US1] Integration test: `PUT .../menu-variant-settings` with a duplicate or unrecognized `DietaryType` string returns `422` with field errors (FR-002) in the same file
- [ ] T017 [P] [US1] Integration test: non-`DirectorOnly` callers are rejected on `PUT .../menu-variant-settings` (policy boundary, mirrors `LocationReservationSettingsTests` precedent) in the same file
- [ ] T048 [P] [US1] Integration test: re-enabling a previously-removed `DietaryType` appends it at the end of `menuVariantPriorityOrder`, not its prior position (FR-002) in the same file
- [ ] T049 [P] [US1] Integration test: `GET /api/locations/{id}` returns `menuVariantsWithPublishedContent` containing exactly the enabled variants with a published `MonthlyMenu` for the current or a future month, and excludes ones with only a draft or no menu at all (FR-014) in the same file

### Implementation for User Story 1

- [ ] T018 [US1] Implement `UpdateLocationMenuVariantSettingsCommand` + validator (no-duplicates rule, valid `DietaryType` wire strings, append-at-end on re-enable per FR-002) in `backend/ChildCare.Application/Locations/UpdateLocationMenuVariantSettingsCommand.cs` (depends on T007, T009)
- [ ] T019 [US1] Wire `PUT /api/locations/{id}/menu-variant-settings` in `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` (depends on T018)
- [ ] T050 [US1] Compute `menuVariantsWithPublishedContent` in `LocationMapper.ToResponse` (or the `GetLocationQuery` handler) — enabled variants with a published `MonthlyMenu` for the current or a future month (FR-014) in `backend/ChildCare.Application/Locations/LocationMapper.cs` (depends on T006, T008)
- [ ] T020 [US1] Regenerate and commit `web/lib/generated/api-types.ts` against the new endpoint and response fields
- [ ] T021 [P] [US1] Create `MenuVariantSettingsForm.tsx` (checkbox per `DietaryType` to enable, drag/keyboard reorder for enabled ones, save action — mirrors `ReservationSettingsForm.tsx`'s structure) in `web/components/MenuVariantSettingsForm.tsx`
- [ ] T051 [US1] Add a `ConfirmDialog` (existing component) warning before saving a removal of any entry present in `menuVariantsWithPublishedContent` (FR-014) in `web/components/MenuVariantSettingsForm.tsx` (depends on T021, T050)
- [ ] T022 [US1] Add a "Menuvarianten" tab to the location detail page alongside the existing "Algemeen"/"Reserveringsinstellingen" tabs in `web/app/(app)/locations/[id]/page.tsx` (depends on T021)
- [ ] T023 [P] [US1] Web component test: settings tab loads current `menuVariantPriorityOrder`, enabling a variant and reordering calls the new endpoint and reflects the updated order in `web/__tests__/menuVariantSettings.test.tsx`
- [ ] T052 [P] [US1] Web component test: removing a variant present in `menuVariantsWithPublishedContent` shows a confirmation dialog before saving; removing one that isn't present saves immediately with no dialog (FR-014) in `web/__tests__/menuVariantSettings.test.tsx`

**Checkpoint**: A director can fully configure a location's variant set and priority order,
independently of any menu authoring or parent-facing resolution.

---

## Phase 4: User Story 2 - Director authors and publishes a variant menu (Priority: P1)

**Goal**: A director can author, save, publish, and un-publish a variant menu through the exact
same day-grid/CSV-import UI as the base menu, fully independent of the base menu's state, and
rejected if the variant isn't enabled.

**Independent Test**: With Vegetarian enabled (US1), select it in the Menu section, fill in and
publish a full month; confirm the base menu for the same location/month is untouched; confirm a
disabled variant is rejected on write.

### Tests for User Story 2

- [ ] T024 [P] [US2] Integration test: `GET`/`PUT`/`publish`/`unpublish` with `variant=vegetarian` operate on a distinct `MonthlyMenu` row from the base menu (`variant` absent), and editing/publishing one never changes the other (FR-004, FR-005) in `backend/ChildCare.Api.Tests/MonthlyMenus/MonthlyMenuVariantAuthoringTests.cs`
- [ ] T025 [P] [US2] Integration test: `PUT`/`publish`/`unpublish` with a `variant` not present in the location's `menuVariantPriorityOrder` returns `422 errors.monthly_menu.variant_not_enabled` (FR-006), including after a previously-enabled variant is later disabled in the same file
- [ ] T026 [P] [US2] Integration test: disabling a `DietaryType` in `menuVariantPriorityOrder` after a `MonthlyMenu` row exists for it does not delete that row; re-enabling makes it selectable/editable again (FR-007) in the same file
- [ ] T027 [P] [US2] Integration test: the unique index rejects two rows for the same `(LocationId, Year, Month, Variant)`, and allows one base row plus one row per distinct variant for the same location/month (data-model.md) in the same file

### Implementation for User Story 2

- [ ] T028 [US2] Add a shared `IsVariantEnabled(Location, DietaryType?)` (or inline check) helper and thread a `Variant` parameter through `GetMonthlyMenuQuery`, `UpsertMonthlyMenuCommand`, `PublishMonthlyMenuCommand`, `UnpublishMonthlyMenuCommand` — default `null`/`"base"` when absent, reject non-enabled variants in each write command's validator per research.md's decision — in `backend/ChildCare.Application/MonthlyMenus/*.cs` (depends on T006, T008, T018)
- [ ] T029 [US2] Add `variant` query parameter passthrough to all four director endpoints in `backend/ChildCare.Api/Endpoints/MonthlyMenuEndpoints.cs` (depends on T028)
- [ ] T030 [US2] Regenerate `web/lib/generated/api-types.ts` again against the variant-aware endpoints
- [ ] T031 [US2] Create `MonthlyMenuVariantSelector.tsx` (Base + one option per the location's enabled `menuVariantPriorityOrder`, reusing `mealPreferenceRequests.dietaryType.*` labels) in `web/components/menu/MonthlyMenuVariantSelector.tsx` (depends on T003, T004)
- [ ] T032 [US2] Wire the selector into `web/app/(app)/menu/page.tsx`: selecting a variant passes it through to the existing `GET`/`PUT`/publish/unpublish calls and to `MonthlyMenuDayGrid`/`MonthlyMenuCsvImportDialog` unchanged otherwise (depends on T031)
- [ ] T033 [P] [US2] Web component test: switching the variant selector loads/saves a distinct menu from the base menu, and CSV import (013i) applies to whichever variant is currently selected in `web/__tests__/menu.variantSwitch.test.tsx`

**Checkpoint**: A director can independently author and publish any enabled variant end to end,
using the identical authoring UI as the base menu.

---

## Phase 5: User Story 3 - Parent automatically sees the right menu per child (Priority: P1)

**Goal**: `GetParentMonthlyMenuQuery` resolves one entry per (location, child), applying the
priority-order/fallback rule; parent-mobile renders one section per child.

**Independent Test**: A child with `DietaryType` including Vegetarian, a published Vegetarian
variant, and a sibling with no preference — confirm the API returns two distinct entries with the
correct `resolvedVariant` each, and the parent-mobile screen renders both correctly labeled.

### Tests for User Story 3

- [ ] T034 [P] [US3] Integration test: a child with no `MealPreference`/no `DietaryType` resolves to the base menu (FR-009) in `backend/ChildCare.Api.Tests/MonthlyMenus/GetParentMonthlyMenuVariantResolutionTests.cs`
- [ ] T035 [P] [US3] Integration test: a child whose `DietaryType` matches one published, enabled variant resolves to it, with `resolvedVariant` set correctly (FR-008, US3/AC2) in the same file
- [ ] T036 [P] [US3] Integration test: a child matching two enabled variants resolves to the higher-priority one per `menuVariantPriorityOrder` (FR-008, US3/AC3) in the same file
- [ ] T037 [P] [US3] Integration test: a child matching a variant whose menu is still a draft (not published) falls back correctly — to the next-lower-priority published match, or the base menu if none (FR-009, US3/AC4) in the same file
- [ ] T038 [P] [US3] Integration test: a parent with two children at the same location gets two distinct entries, each independently resolved, with correct `childId`/`childName` (FR-010, US3/AC5) in the same file
- [ ] T039 [P] [US3] Regression test: confirms the per-location query batching (one menu-set fetch per location, not per child) — asserts query count doesn't scale with child count, per research.md's efficiency decision, in the same file
- [ ] T053 [P] [US3] Integration test: a child recorded as Vegan does NOT match an enabled, published Vegetarian variant (exact-`DietaryType`-equality only, no inferred hierarchy, FR-008) in the same file
- [ ] T054 [P] [US3] Integration test: a child holding active contracts at two different locations resolves independently at each — one location's `MenuVariantPriorityOrder`/published variants never affect the other's resolution for the same child (FR-008, Edge Cases) in the same file

### Implementation for User Story 3

- [ ] T040 [US3] Rewrite `GetParentMonthlyMenuQuery` to resolve per (location, child): for each location, load every published `MonthlyMenu` (base + enabled variants) once, then for each of the parent's children contracted there, walk `menuVariantPriorityOrder` and apply the resolution algorithm from data-model.md — in `backend/ChildCare.Application/MonthlyMenus/GetParentMonthlyMenuQuery.cs` (depends on T028)
- [ ] T041 [US3] Extend `ParentMonthlyMenuEntry` construction with `childId`/`childName`/`resolvedVariant` per data-model.md, in the same file and `MonthlyMenuMapper.cs`
- [ ] T042 [US3] Extend `parent-mobile/services/menu.ts`'s cache/fetch shape for the new per-child entry fields (no behavior change to the fetch-then-cache-fallback pattern itself, per its own header comment)
- [ ] T043 [US3] Update `parent-mobile/app/(app)/menu/index.tsx`'s render loop: key by `${entry.locationId}-${entry.childId}`, section heading shows child name (and the resolved variant in plain language via `mealPreferenceRequests.dietaryType.*` when non-null, per FR-011 — no visible "fallback" messaging) (depends on T040, T041, T005)
- [ ] T044 [P] [US3] Parent-mobile component test: two children at the same location render two distinct sections, correctly labeled with their respective resolved variant (or none) in `parent-mobile/__tests__/menu.variantSections.test.tsx`

**Checkpoint**: All three user stories are independently functional — a director can configure
and author variants, and parents see the correct one automatically per child.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that span all three stories.

- [ ] T045 [P] Accessibility pass: variant selector and settings reorder control fully keyboard-operable, focus-visible, per `platform-rules.md`'s Director Web section, in `web/components/menu/MonthlyMenuVariantSelector.tsx` and `web/components/MenuVariantSettingsForm.tsx`
- [ ] T046 Run `quickstart.md`'s four scenarios manually/via integration tests and confirm each expected outcome
- [ ] T047 Confirm FR-012/SC-003 explicitly: run 013e's/013i's own existing test suites unmodified against the new schema and confirm 100% still pass with zero changes needed to those test files — the strongest possible evidence the base-menu path is untouched

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup only for T003-T005's i18n keys being available to reference; T006-T013 have no Setup dependency and can start immediately. BLOCKS all user stories.
- **User Stories (Phase 3-5)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) has no dependency on US2/US3 — settings can be configured before any authoring exists.
  - US2 (P1) depends on US1's `UpdateLocationMenuVariantSettingsCommand` existing (T018) to have any enabled variant to author against, but its own endpoint/UI work (T028-T033) is otherwise independent.
  - US3 (P1) depends on US2's variant-aware `MonthlyMenu` write path (T028) existing so there's variant content to resolve, but its own query/UI work (T040-T044) is otherwise independent.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- T001-T005 (Setup) can all run in parallel.
- T011 and T012 (Foundational mappers) can run in parallel once T006/T007/T009 land.
- T014-T017 (US1 tests) can run in parallel.
- T021 and T023 (US1 web) can run in parallel once T019/T020 land.
- T024-T027 (US2 tests) can run in parallel.
- T034-T039 (US3 tests) can run in parallel with each other, once T040/T041 land.
- T044 (US3 parent-mobile test) can run in parallel with T045 (Polish) once T043 lands.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run Scenario 2's first two steps from `quickstart.md` independently
5. Demo if ready — a director can already see the settings take effect, even with no authoring UI yet

### Incremental Delivery

1. Setup + Foundational → schema and mappers ready
2. Add User Story 1 → validate independently → demo (settings only)
3. Add User Story 2 → validate independently (Scenario 2 in full) → demo (authoring works)
4. Add User Story 3 → validate independently (Scenario 3) → demo (parents see it — full feature)
5. Polish (Phase 6) → run all four `quickstart.md` scenarios end to end, including the explicit
   base-menu-regression check (T047)

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- T047 exists specifically because FR-012/SC-003's "zero behavior change for the common case"
  guarantee is the highest-risk regression surface in this feature (extending shared code rather
  than duplicating it, per research.md, is the architectural choice that makes this guarantee
  possible — but only running the *existing* test suites unmodified actually proves it held).
- Commit after each task or logical group, per this repo's standing convention.
