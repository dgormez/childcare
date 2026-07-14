# Quickstart: Monthly Menu CSV Import

Validation scenarios proving the feature works end-to-end, once implemented. Assumes a running
local stack (`backend` on its dev port, `web` on `localhost:3000`) against a tenant seeded with
at least one location and one director account — the same prerequisites 013e's own quickstart
already establishes. No new seed data, migration, or backend restart is needed since this feature
adds no schema or endpoint.

## Prerequisites

- `web` running locally (`npm run dev` in `web/`).
- A director account logged in, on the Menu section (`/menu`) for a location and month with no
  existing menu (a clean draft is the easiest starting point for Scenario 1).

## Scenario 1 — Import a well-formed CSV (User Story 1)

1. On the Menu page, select a location and a month with an empty grid.
2. Click "Import CSV". Select a CSV file containing a valid row for every day of that month (see
   `contracts/csv-format.md`'s example for the expected format).
3. Confirm the preview shows every row as valid, with a summary count equal to the number of days
   in the month.
4. Confirm the import, and verify every day's soup/main course/dessert/notes cells in the grid are
   now filled from the CSV.
5. Press the existing Save button. Confirm the save succeeds (same success path as manually typing
   the grid, unchanged from 013e).

**Expected outcome**: the grid is fully populated in one upload; Save behaves identically to
saving manually-typed data.

## Scenario 2 — Partial import with mixed valid/invalid rows (User Story 2)

1. On the Menu page, select a location/month where a few days are already manually filled in.
2. Prepare a CSV covering a subset of days, including: one row with an unparseable date, one row
   with a date outside the selected month, two rows sharing the same date (duplicate), and one row
   with a `notes` value over 500 characters.
3. Upload the CSV. Confirm the preview flags each bad row with its specific reason (unparseable
   date / out of range / duplicate / field too long) and shows a summary count of rows that will
   apply versus rows that will be skipped.
4. Confirm the import. Verify only the valid rows' dates were overwritten in the grid, and every
   day not covered by a valid CSV row (including the days that were already manually filled in
   before the import) kept its prior value.

**Expected outcome**: valid rows apply, invalid rows are clearly explained and excluded, and
untouched days are genuinely untouched.

## Scenario 3 — Template download and re-upload (User Story 3)

1. On the Menu page, click "Download template". Confirm a CSV file downloads with the header row
   `date,soup,main_course,dessert,notes` and one example data row.
2. Without modifying the file, upload it back via "Import CSV".
3. Confirm the preview shows the example row as valid.

**Expected outcome**: the downloaded template is immediately valid input, proving the header
format and the parser agree with no manual trial-and-error needed.

## Scenario 4 — Fully invalid file (edge case, FR-014/FR-015)

1. Upload an empty CSV (header row only, no data rows). Confirm a clear rejection message appears
   and the grid is unchanged.
2. Upload a non-CSV file (e.g. a `.txt` or image file renamed to `.csv`). Confirm a single
   top-level parse error appears, distinct from the per-row error list in Scenario 2, and the grid
   is unchanged.

**Expected outcome**: neither failure mode silently does nothing or partially corrupts the grid —
both produce a clear, actionable message.
