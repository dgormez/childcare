# Data Model: Monthly Menu CSV Import

This feature introduces **no new persisted entity, table, or migration**. It operates entirely on
in-memory representations, reusing feature 013e's existing `MonthlyMenuDay`-shaped data end to
end. This document exists to make that reuse explicit, not to define new storage.

## Existing persisted entity (013e, unchanged)

**`MonthlyMenuDay`** (`backend/ChildCare.Domain/Entities/MonthlyMenuDay.cs`, tenant schema,
unchanged by this feature):

| Field | Type | Notes |
|---|---|---|
| `MenuId` | `Guid` | FK to `MonthlyMenu` |
| `MenuDate` | `DateOnly`/`Date` | unique per `(MenuId, MenuDate)` |
| `Soup` | `string?` | max 500 chars |
| `MainCourse` | `string?` | max 500 chars |
| `Dessert` | `string?` | max 500 chars |
| `Notes` | `string?` | max 500 chars |

This feature's CSV import produces values that end up in exactly these four free-text fields
(plus the date used to select which day), through 013e's existing `UpsertMonthlyMenuCommand`
whole-month-replace write — no schema change.

## New in-memory shapes (this feature, frontend-only)

### `ParsedMenuCsvRow`

One row as read from the uploaded CSV, before validation:

| Field | Type | Source CSV column |
|---|---|---|
| `rawDate` | `string` | `date` |
| `soup` | `string \| undefined` | `soup` |
| `mainCourse` | `string \| undefined` | `main_course` |
| `dessert` | `string \| undefined` | `dessert` |
| `notes` | `string \| undefined` | `notes` |
| `rowNumber` | `number` | position in file, 1-indexed excluding header — used only for error display |

### `ValidatedMenuCsvRow`

The result of validating one `ParsedMenuCsvRow` against the currently-selected year/month and the
field-length limit. Validation reasons are mutually exclusive per row, checked in the precedence
order FR-022 defines (unparseable/wrong-format date → out-of-range date → duplicate date → field
too long) — a row failing more than one check is still reported with exactly one `errorReason`.
A fifth reason, `malformed_row` (FR-021, checked first — highest precedence), covers a column-
count mismatch against the header row; discovered during implementation that a ragged row's
`date` cell can still parse successfully when the missing/extra columns are trailing ones, so
column-count is checked explicitly rather than assumed to always corrupt the date.

| Field | Type | Notes |
|---|---|---|
| `date` | `string` (`YYYY-MM-DD`) | present only when valid |
| `fields` | `DayFields` (soup/mainCourse/dessert/notes) | present only when valid; reuses `MonthlyMenuDayGrid.tsx`'s existing `DayFields` shape so the merge step needs no translation |
| `status` | `"valid" \| "invalid"` | |
| `errorReason` | one of `"invalid_date" \| "date_out_of_range" \| "duplicate_date" \| "field_too_long"` when `status === "invalid"` | maps 1:1 to an i18n key under the `menu` namespace |
| `rowNumber` | `number` | carried through for the preview table |
| `willOverwriteExisting` | `boolean` | present only when `status === "valid"` — `true` when the grid's current state already has non-blank content for this date (FR-024); computed by comparing against the grid's existing `Map<date, DayFields>` at validation time, not stored, so it always reflects the grid state at the moment of upload |

### `MenuCsvImportResult`

The overall outcome of parsing+validating one uploaded file, shown in the preview:

| Field | Type | Notes |
|---|---|---|
| `rows` | `ValidatedMenuCsvRow[]` | every row, valid and invalid, in file order |
| `validCount` | `number` | derived — count of `status === "valid"` |
| `invalidCount` | `number` | derived — count of `status === "invalid"` |
| `fileLevelError` | `string \| null` | set instead of `rows` when the file could not be parsed as CSV at all (FR-015) |

## Relationships / flow

```text
File (director-selected)
  → Papa.parse → ParsedMenuCsvRow[]  (or fileLevelError if unparseable)
  → validateRows(rows, {year, month}) → ValidatedMenuCsvRow[] → MenuCsvImportResult
  → [preview UI, director confirms]
  → mergeIntoGridState(existing Map<date, DayFields>, validRows)
      → new Map<date, DayFields>  (same shape MonthlyMenuDayGrid.tsx already holds internally)
  → [existing Save button] → PUT .../monthly-menus/{year}/{month}  (013e, unchanged)
```

No new state transitions, lifecycle, or persistence — this is a staging step in front of a write
path that already exists.
