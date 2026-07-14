# Tasks: Monthly Menu CSV Import

**Input**: Design documents from `specs/013i-monthly-menu-csv-import/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required per spec.md's Technical Requirements (unit coverage for parsing/validation,
component coverage for the import UI) and constitution Principle V's standing practice of
covering the happy path plus key negative flows — same standard every prior feature has followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dependency and i18n scaffolding shared across all stories.

- [ ] T001 [P] Add `papaparse` (+ `@types/papaparse`) as a `web/` dependency in `web/package.json`
- [ ] T002 [P] Add new `menu.*` i18n keys under the existing `menu` namespace — import action label, "Download template" label, preview table column headers, applied/skipped summary line, per-row error reasons (`invalid_date`, `date_out_of_range`, `duplicate_date`, `field_too_long`), file-level parse error, and the zero-valid-rows rejection message — to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The parse/validate/merge/template module every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T003 Define `ParsedMenuCsvRow`, `ValidatedMenuCsvRow`, `MenuCsvImportResult` types and the `MAX_FIELD_LENGTH = 500` constant (per data-model.md, matching `UpsertMonthlyMenuCommandValidator`'s server-side limit) in `web/lib/menu/csvImport.ts`
- [ ] T004 Implement `parseMenuCsv(file: File)` using `papaparse` (`header: true`, case-insensitive header matching for `date`/`soup`/`main_course`/`dessert`/`notes`, unrecognized columns ignored) returning `ParsedMenuCsvRow[]` or a file-level parse error, in `web/lib/menu/csvImport.ts` (depends on T003)
- [ ] T005 Implement `validateMenuCsvRows(rows: ParsedMenuCsvRow[], { year, month }): ValidatedMenuCsvRow[]` — unparseable-date check, year/month range check, duplicate-date check, per-field `MAX_FIELD_LENGTH` check, each producing the matching `errorReason` — in `web/lib/menu/csvImport.ts` (depends on T003, T004)
- [ ] T006 Implement `mergeMenuCsvRowsIntoGrid(currentDays: Map<string, DayFields>, validRows: ValidatedMenuCsvRow[]): Map<string, DayFields>` — overwrites only matching dates, returns a new map, does not mutate the input — in `web/lib/menu/csvImport.ts` (depends on T005)
- [ ] T007 Implement `buildMenuCsvTemplate(year: number, month: number): string` — header row plus one example data row dated the 1st of the given month, per `contracts/csv-format.md` — in `web/lib/menu/csvImport.ts` (depends on T003)

**Checkpoint**: Core parse/validate/merge/template module complete and independently
unit-testable — user story implementation can now begin.

---

## Phase 3: User Story 1 - Import a well-formed month of menu data (Priority: P1) 🎯 MVP

**Goal**: A director uploads a CSV with a valid row for every day of the selected month, reviews
an all-valid preview, confirms, and the grid is fully populated — ready for the existing Save
button.

**Independent Test**: Upload a CSV with one valid row per day of the selected month; the preview
shows every row as valid with a full-month "will apply" count; confirming fills every day's grid
cells; pressing the existing Save button persists exactly what manual entry already persists.

### Tests for User Story 1

- [ ] T008 [P] [US1] Unit tests: `parseMenuCsv` + `validateMenuCsvRows` correctly parse and validate a full valid month (every row valid, dates matching the grid's year/month) in `web/__tests__/menuCsvImport.test.ts`
- [ ] T009 [P] [US1] Unit test: `mergeMenuCsvRowsIntoGrid` overwrites only the dates present in `validRows` and leaves every other date's existing value untouched (FR-012) in `web/__tests__/menuCsvImport.test.ts`

### Implementation for User Story 1

- [ ] T010 [US1] Create `MonthlyMenuCsvImportDialog.tsx` — file picker, calls `parseMenuCsv`/`validateMenuCsvRows` on selection, renders an all-valid preview table with a summary count and a Confirm action — in `web/components/menu/MonthlyMenuCsvImportDialog.tsx` (depends on T004, T005)
- [ ] T011 [US1] Add an "Import CSV" button to `MonthlyMenuDayGrid.tsx` that opens the dialog; on Confirm, call `mergeMenuCsvRowsIntoGrid` against the grid's existing `toFieldMap`-derived state and update it (does not call Save) — in `web/components/menu/MonthlyMenuDayGrid.tsx` (depends on T006, T010)
- [ ] T012 [US1] Verify the merged state flows unchanged into the existing `onSave`/`handleSave` prop path in `web/app/(app)/menu/page.tsx` — no changes expected beyond confirming the grid's existing `MonthlyMenuDaySave[]` derivation already reflects imported values; adjust only if it does not
- [ ] T013 [P] [US1] Component test: uploading a full valid-month CSV shows an all-valid preview, confirming fills the grid, and the existing Save button still submits the same `PUT` payload shape as manual entry in `web/__tests__/MonthlyMenuCsvImportDialog.test.tsx`

**Checkpoint**: A director can import a well-formed CSV end to end and Save, independently of
error-recovery or template-download behavior.

---

## Phase 4: User Story 2 - Recover from a CSV with some bad rows (Priority: P2)

**Goal**: A CSV mixing valid and invalid rows still applies every valid row, and clearly explains
exactly what was skipped and why, before the director confirms anything.

**Independent Test**: Upload a CSV mixing valid rows with an unparseable date, an out-of-month
date, and a duplicate date; the preview flags each invalid row with its specific reason and a
summary count, while every valid row still applies once confirmed.

### Tests for User Story 2

- [ ] T014 [P] [US2] Unit tests: `validateMenuCsvRows` flags an unparseable date, an out-of-range date, duplicate dates, and an over-length field with the correct `errorReason` each (FR-005–FR-008) in `web/__tests__/menuCsvImport.test.ts`
- [ ] T015 [P] [US2] Unit test: `mergeMenuCsvRowsIntoGrid` applies only the valid rows from a mixed valid/invalid batch, leaving invalid rows' dates untouched in `web/__tests__/menuCsvImport.test.ts`

### Implementation for User Story 2

- [ ] T016 [US2] Render invalid rows in the preview with an icon-plus-text reason per row (never color alone, FR-018) and an "N will apply / M skipped" summary line in `web/components/menu/MonthlyMenuCsvImportDialog.tsx` (depends on T010)
- [ ] T017 [US2] Handle the zero-valid-rows case (FR-014): show a clear rejection message, disable Confirm, perform no merge, in `web/components/menu/MonthlyMenuCsvImportDialog.tsx`
- [ ] T018 [US2] Handle the file-level parse-failure case (FR-015): show a single top-level error distinct from the per-row list, process no rows, in `web/components/menu/MonthlyMenuCsvImportDialog.tsx`
- [ ] T019 [P] [US2] Component test: a mixed valid/invalid CSV shows per-row reasons plus the summary count, and confirming applies only the valid rows in `web/__tests__/MonthlyMenuCsvImportDialog.test.tsx`
- [ ] T020 [P] [US2] Component test: an all-invalid/empty CSV and a malformed non-CSV file each show the correct distinct rejection/error state and leave the grid untouched in `web/__tests__/MonthlyMenuCsvImportDialog.test.tsx`

**Checkpoint**: Mixed-quality CSVs are handled transparently — nothing silently drops or
mis-applies a row — independently of the template-download convenience.

---

## Phase 5: User Story 3 - Start from a template (Priority: P3)

**Goal**: A first-time user can download a correctly-formatted CSV template and re-upload it
unmodified as valid input.

**Independent Test**: Clicking "Download template" produces a CSV file with the correct header
row (and an example row) that, when re-uploaded unmodified, is recognized as valid.

### Implementation for User Story 3

- [ ] T021 [US3] Add a "Download template" action that triggers a client-side `Blob` download of `buildMenuCsvTemplate`'s output for the grid's currently-selected year/month in `web/components/menu/MonthlyMenuCsvImportDialog.tsx` (depends on T007)
- [ ] T022 [P] [US3] Unit test: `buildMenuCsvTemplate` produces the expected header row plus one example row dated within the given year/month in `web/__tests__/menuCsvImport.test.ts`
- [ ] T023 [P] [US3] Component test: clicking "Download template" triggers a download whose content matches `buildMenuCsvTemplate`'s output in `web/__tests__/MonthlyMenuCsvImportDialog.test.tsx`

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that span all three stories.

- [ ] T024 [P] Accessibility pass: file input and preview table fully keyboard-operable, focus moves to the preview/summary region once parsing completes (per spec.md's UX Requirements) in `web/components/menu/MonthlyMenuCsvImportDialog.tsx`
- [ ] T025 Run `quickstart.md`'s four scenarios manually against the local `web` dev server and confirm each expected outcome

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001's `papaparse` dependency) — BLOCKS all user
  stories.
- **User Stories (Phase 3–5)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) has no dependency on US2/US3.
  - US2 (P2) extends the same `MonthlyMenuCsvImportDialog.tsx` US1 creates (T010) but adds
    independently-testable behavior (invalid-row rendering, rejection states).
  - US3 (P3) extends the same dialog with an additive action (template download) with no
    dependency on US2.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Within Each User Story

- Tests are written alongside (not strictly before, given the small scope) their implementation
  tasks but MUST pass before the story's checkpoint is considered done.
- Foundational module (T003–T007) before any story's UI work.
- Dialog shell (T010) before US2/US3's extensions to it (T016–T018, T021).

### Parallel Opportunities

- T001 and T002 (Setup) can run in parallel.
- T008 and T009 (US1 tests) can run in parallel; T013 depends on T010–T012 having landed.
- T014 and T015 (US2 tests) can run in parallel with each other, and with US1's implementation
  once Foundational is done, since they target the same already-built `csvImport.ts` functions.
- T019 and T020 (US2 component tests) can run in parallel.
- T022 and T023 (US3 tests) can run in parallel.
- T024 (Polish) can run in parallel with the last story's tests once the dialog exists.

---

## Parallel Example: User Story 1

```bash
# Launch both US1 unit tests together:
Task: "Unit tests: parseMenuCsv + validateMenuCsvRows for a full valid month in web/__tests__/menuCsvImport.test.ts"
Task: "Unit test: mergeMenuCsvRowsIntoGrid overwrites only matching dates in web/__tests__/menuCsvImport.test.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run Scenario 1 from `quickstart.md` independently
5. Demo if ready — US1 alone already delivers the core time-saving value

### Incremental Delivery

1. Setup + Foundational → parse/validate/merge/template module ready
2. Add User Story 1 → validate independently → demo (MVP)
3. Add User Story 2 → validate independently (Scenario 2) → demo
4. Add User Story 3 → validate independently (Scenario 3) → demo
5. Polish (Phase 6) → run all four `quickstart.md` scenarios end to end

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- No backend tasks exist in this list — confirmed by plan.md's Constitution Check: this feature
  adds no endpoint, command, or migration.
- Commit after each task or logical group, per this repo's standing convention.
