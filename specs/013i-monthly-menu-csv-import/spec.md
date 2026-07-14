# Feature Specification: Monthly Menu CSV Import

**Feature Branch**: `013i-monthly-menu-csv-import`

**Created**: 2026-07-14

**Status**: Draft

**Input**: User description: "Let the director bulk-populate a month's menu via CSV upload
instead of typing each day by hand in the existing day-grid (013e). This is a director-web-only
authoring convenience — it does not add any new backend endpoint or change how parents see the
menu."

## Product Context

### Feature Type

User-facing UI (director-web only; a client-side authoring convenience layered on 013e's
existing manual day-grid — no new backend endpoint, no new data model).

### Primary Consumer

Director. Parents are unaffected — this feature changes only how a director fills in the
existing monthly-menu day grid, not what is ever shown to a parent.

### Workflow Boundary

Parent Communication (`Workflows/communication.md`) — the same workflow boundary 013e's monthly
menu already occupies, since this feature only adds a faster authoring path to that existing
capability; no new workflow is introduced.

Actors: Director (uploads a CSV, reviews the parsed preview, confirms the merge, then uses the
existing Save/Publish actions unchanged).

Actions: Select a CSV file. Client-side parse and validate every row. Review a preview showing
which rows will be applied and which are skipped (with a reason). Confirm the merge into the
day grid. Continue with the existing Save/Publish flow.

Data Flow: CSV file → browser-only parse/validate → valid rows merged into the day grid's
existing per-calendar-day state (rows for dates not present in the CSV are left as they were) →
director's own Save action submits the full month through 013e's existing write path, unchanged.

Outputs: A day grid populated from the CSV, ready for the director's normal review, Save, and
Publish steps.

Cross-Platform Impact: Director web only. No changes to the caregiver tablet, the parent app, or
any backend endpoint or data model.

### User Impact

This enables a director to populate an entire month's menu from a spreadsheet they already keep
in one action, resulting in faster monthly menu publishing with far less manual re-typing and
fewer transcription errors.

### UX Requirements

**Persona**: Director, desktop web, per `platform-rules.md`'s Director Web section — dense,
keyboard-and-mouse, 1280px+ viewport.

**Platform**: Web only.

**User job**: "Get this month's already-planned menu — usually kept in a spreadsheet — into the
system in one action instead of typing 28-31 rows by hand."

**Success criteria**:

- A correctly-formatted CSV populates every matching day in the grid in one upload, with zero
  retyping needed for those days.
- A CSV containing some invalid rows still applies every valid row and clearly reports which
  rows were skipped and why, rather than rejecting the whole file.

**Main flow**: Director clicks "Import CSV" on the Menu section → selects a file → the file is
parsed and validated locally → a preview appears listing every parsed row, flagging any invalid
ones with a reason and showing a summary count of rows that will be applied vs. skipped →
director confirms → valid rows are merged into the day grid's in-memory state → the existing
Save and Publish actions proceed exactly as they do today.

**Loading state**: Parsing is local/synchronous, so no network loading indicator is required;
the UI must still stay responsive while parsing so the interface never appears frozen.

**Empty state**: If the CSV contains zero valid rows, the director sees a clear rejection
message and the grid is left completely untouched.

**Error state**: Each invalid row shows an inline reason (unparseable date, date outside the
selected month, duplicate date within the file, a field over the length limit). A single
top-level error is shown for a file that cannot be parsed at all (wrong delimiter, missing
header, non-CSV/binary content), with no partial row processing in that case.

**Accessibility**: The file picker and the preview table are fully keyboard-operable. No error
is conveyed by color alone — every flagged row pairs its highlight with an icon and text, per
`design-system.md`. Focus moves to the preview/summary region once parsing completes.

**Offline behavior**: Not applicable. Parsing is a local file operation with no network
dependency; the only network call in this flow is the pre-existing Save action, unchanged from
013e.

### Technical Requirements

**API impact**: None. The feature reuses 013e's existing monthly-menu write endpoint unchanged;
no new endpoint is introduced.

**Data-model impact**: None.

**Security considerations**: The CSV is parsed entirely client-side; no file is ever uploaded to
a backend endpoint, so there is no new server-side file-handling surface. Because a client could
in principle be tampered with, the existing server-side validation on the monthly-menu write
path still applies in full to whatever is eventually saved — client-side validation here is a
UX convenience, not a substitute for it.

**Performance considerations**: A month contains at most 31 rows, so parsing cost is trivial;
no special performance handling is required beyond not blocking the UI thread noticeably.

**Testing requirements**: Unit coverage for the CSV parsing/validation logic (valid rows, each
invalid-row case, and an unparseable file) and component-level coverage for the import UI
(preview rendering, confirming the merge into grid state, and that the existing Save action is
unaffected).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import a well-formed month of menu data (Priority: P1)

A director who already tracks the month's menu in a spreadsheet exports it as a CSV and imports
it directly into the day grid, instead of retyping every day.

**Why this priority**: This is the entire value of the feature — without it, there is no time
saved over the existing manual grid.

**Independent Test**: Upload a CSV with one valid row per day of the selected month; the preview
shows every row as valid with a full-month "will apply" count; confirming fills every day's
soup/main/dessert/notes fields in the grid; the existing Save button then persists exactly what
013e's manual entry already persists.

**Acceptance Scenarios**:

1. **Given** the director has the Menu section open for a location and month with an empty
   draft, **When** they upload a CSV with a valid row for every day of that month, **Then** the
   preview shows all rows as valid and, after confirming, every day's grid cells are filled from
   the CSV.
2. **Given** the grid already has some days manually filled in, **When** the director imports a
   CSV that only covers a subset of the month's days, **Then** only the days present in the CSV
   are overwritten and the remaining days keep their existing values.
3. **Given** a successful import has been merged into the grid, **When** the director reviews
   and edits a cell before saving, **Then** the edit behaves exactly as manual typing does today,
   and the imported values are not persisted until Save is pressed.

---

### User Story 2 - Recover from a CSV with some bad rows (Priority: P2)

A director uploads a CSV that has a few mistakes (a typo'd date, a duplicate row, a stray row
from a different month) and needs to understand exactly what will and won't be applied before
committing to anything.

**Why this priority**: Real spreadsheets have mistakes; a feature that silently drops or
mis-applies rows is worse than no feature, so surfacing exactly what's wrong is essential to
trusting the import.

**Independent Test**: Upload a CSV mixing valid rows with an unparseable date, an out-of-month
date, and a duplicate date; the preview flags each invalid row with its specific reason and a
summary count, while every valid row still applies once confirmed.

**Acceptance Scenarios**:

1. **Given** a CSV row has a date that cannot be parsed as a calendar date, **When** the director
   uploads it, **Then** that row is flagged invalid with a reason and is excluded from the merge
   while other valid rows still apply.
2. **Given** a CSV row has a date outside the year/month currently selected in the grid, **When**
   the director uploads it, **Then** that row is flagged invalid as out of range and is not
   applied to any day.
3. **Given** a CSV contains two rows with the same date, **When** the director uploads it,
   **Then** both rows are flagged as duplicates and neither is applied, while unaffected dates
   in the same file still import normally.
4. **Given** a CSV field (soup/main course/dessert/notes) exceeds the field length limit,
   **When** the director uploads it, **Then** that row is flagged invalid for that reason and is
   excluded from the merge.
5. **Given** the director fixes the source spreadsheet and re-uploads a corrected CSV, **When**
   the new file is imported, **Then** it behaves exactly as a fresh import — matching dates are
   overwritten, non-matching dates are left alone.
6. **Given** the grid already has manually-entered, non-blank content for a day, **When** a valid
   CSV row targets that same date, **Then** the preview visibly flags that row as overwriting
   existing content before the director confirms (FR-024, SC-005).

---

### User Story 3 - Start from a template (Priority: P3)

A director who has never used the import feature before wants to know the exact CSV format
expected, without guessing column names.

**Why this priority**: Lowers the barrier to first use; without it, User Story 1 still works for
a director who already knows or infers the format, so this is a nice-to-have rather than
essential.

**Independent Test**: Clicking "Download template" produces a CSV file with the correct header
row (and an example row) that, when re-uploaded unmodified, is recognized as valid.

**Acceptance Scenarios**:

1. **Given** the director has never imported a CSV before, **When** they click "Download
   template," **Then** they receive a CSV file with the expected header columns.
2. **Given** the director fills in the downloaded template with their own data, **When** they
   upload it, **Then** it is parsed and validated the same as any other correctly-formatted CSV.

---

### Edge Cases

- The uploaded file is empty, has no data rows, or every row fails validation: the director sees
  a clear rejection message and the grid is left completely untouched.
- The uploaded file cannot be parsed as CSV at all (wrong delimiter, missing header row, a
  non-CSV/binary file): a single top-level error is shown; no rows are processed.
- The CSV includes a row for a closure day (feature 011): the row is accepted and applied like
  any other day — closure days are already shown greyed out to parents regardless of content, so
  no special handling is needed here.
- The CSV includes extra or unrecognized columns: they are silently ignored; only the recognized
  columns are read.
- The director imports a CSV, merges it into the grid, then navigates away or changes the
  selected location/month without saving: the imported-but-unsaved values are discarded exactly
  as any other unsaved manual grid edit would be today.
- The director changes the selected location or month while the import preview dialog is still
  open (before confirming): the dialog closes without merging anything (FR-026), rather than
  risk confirming a preview built for one month against a different, now-selected month.
- A data row has a different number of columns than the header (e.g. a trailing stray comma, or
  a missing trailing field): that row is flagged invalid and excluded, the same as any other
  per-row validation failure (FR-021) — it does not abort the whole file.
- The file was exported by Excel with a leading UTF-8 byte-order-mark: the BOM is stripped before
  parsing so it does not corrupt the header's first column name (FR-020).
- A row's `date` is present and valid but every other field is blank: the row is valid (FR-023),
  matching the manual grid's existing allowance for a day with no menu content.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Menu section MUST offer an "Import CSV" action, available alongside the
  existing Save/Publish/Unpublish actions, regardless of whether the current month's menu is a
  draft or already published.
- **FR-002**: The system MUST let the director select a local CSV file to import.
- **FR-003**: The system MUST parse and validate the file entirely client-side; importing MUST
  NOT call any new backend endpoint.
- **FR-004**: The system MUST recognize CSV columns `date`, `soup`, `main_course`, `dessert`, and
  `notes`, mapping them to the same fields the manual day grid already edits. Unrecognized
  columns MUST be ignored.
- **FR-005**: For each data row, the system MUST validate that `date` matches the `YYYY-MM-DD`
  format (leading/trailing whitespace trimmed before matching) and parses as a real calendar
  date; a row whose `date` does not match this format — including a locale-ambiguous format such
  as `DD/MM/YYYY` — MUST be flagged invalid (`invalid_date`) and excluded from the merge, never
  guessed at.
- **FR-006**: For each data row, the system MUST validate that `date` falls within the year and
  month currently selected in the grid; a row outside that range MUST be flagged invalid as
  out-of-range and excluded from the merge.
- **FR-007**: The system MUST detect duplicate dates within a single uploaded file; every row
  sharing a duplicated date MUST be flagged invalid and excluded from the merge.
- **FR-008**: The system MUST validate each of `soup`, `main_course`, `dessert`, and `notes`
  against the same 500-character maximum length the manual grid's save path already enforces
  server-side; a row with a field over that limit MUST be flagged invalid for that reason and
  excluded from the merge.
- **FR-009**: Row-level validation MUST evaluate every row independently; one invalid row MUST
  NOT prevent other valid rows in the same file from being parsed and offered for import.
- **FR-010**: Before any values are merged into the grid, the system MUST show the director a
  preview of every parsed row, distinguishing rows that will be applied from rows that are
  flagged invalid (with the specific reason for each), plus a summary count of how many rows
  will be applied versus skipped.
- **FR-011**: The system MUST require an explicit director confirmation from the preview before
  merging any parsed values into the grid's in-memory state.
- **FR-012**: On confirmation, the system MUST overwrite only the grid's day rows whose dates
  matched a valid CSV row; every other day in the grid MUST retain its current value unchanged.
  A blank field in an otherwise-valid CSV row (e.g. an empty `notes` cell) overwrites any
  existing non-blank value for that field on the matched day — import is a whole-day
  overwrite-by-date, the same as if the director had manually cleared that cell, not a
  field-by-field partial merge. FR-024 requires this to be visible to the director before they
  confirm.
- **FR-013**: Merging imported values into the grid MUST NOT itself persist any data; the
  director's existing Save action MUST remain the only way imported values are written, exactly
  as with manually typed values today.
- **FR-014**: If the uploaded file contains zero valid rows (whether because it is empty or every
  row fails validation), the system MUST show a clear rejection and MUST NOT alter the grid.
- **FR-015**: If the uploaded file cannot be parsed as CSV at all, the system MUST show a single
  top-level error and MUST NOT process any rows.
- **FR-016**: The system MUST offer a downloadable CSV template containing the expected header
  row (with at least one example data row) in the format the import expects.
- **FR-017**: All director-facing text produced by this feature (action labels, preview
  summaries, per-row and top-level error messages) MUST be provided through the existing i18n
  system (NL/FR/EN), matching every other user-facing string in the Menu section.
- **FR-018**: The import preview and file-selection controls MUST be operable by keyboard alone,
  and every invalid-row indication MUST be conveyed by more than color alone (an icon plus text).
- **FR-019**: Header-row column matching MUST be case-insensitive and MUST tolerate
  leading/trailing whitespace around column names (e.g. `" Date "` matches `date`), so a
  spreadsheet export with inconsistent header casing is not rejected as unrecognized.
- **FR-020**: A UTF-8 byte-order-mark (BOM) at the start of the file — the common case for a file
  exported directly from Excel — MUST be stripped before parsing and MUST NOT cause a
  file-level parse failure or corrupt the first column's header or values.
- **FR-021**: A data row whose column count does not match the header row MUST be flagged as an
  invalid row (excluded from the merge, reported in the preview like any other invalid row) —
  not treated as a file-level parse failure — as long as the file otherwise has a discernible
  CSV structure (delimiter and header row).
- **FR-022**: When a single row triggers more than one invalid condition simultaneously, the
  system MUST report exactly one reason per row, in this precedence order: unparseable/wrong-
  format date, out-of-range date, duplicate date, field too long — so the director always sees
  one actionable reason, not a stacked or ambiguous combination.
- **FR-023**: A row whose `date` is present and valid but every other field (`soup`,
  `main_course`, `dessert`, `notes`) is blank MUST be treated as valid, mirroring the manual day
  grid's own allowance for a day with no menu content.
- **FR-024**: For every row the preview marks as "will apply," the system MUST indicate whether
  the target day currently already holds non-blank content in the grid that the import would
  overwrite, so the director can see exactly what will be replaced before confirming — not only
  which rows are valid.
- **FR-025**: If the director imports a second CSV in the same session before saving, the merge
  MUST apply against the grid's current in-memory state (which may already include a prior
  unsaved import's merged values), so successive imports compose — the second import's matching
  dates overwrite the first's, non-matching dates from the first import remain.
- **FR-026**: The import dialog MUST be scoped to the year and month selected at the moment it
  was opened; if the director changes the selected location or month while the dialog is open,
  the dialog MUST close without merging anything, rather than allow a preview generated for one
  month to be confirmed against a different, now-selected month.

### Key Entities

This feature introduces no new persisted entity. It reads and writes only the day grid's
existing in-memory representation of `MonthlyMenuDay`-shaped rows (date, soup, main course,
dessert, notes) already defined by feature 013e, and persists through 013e's existing write path
unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can populate a full month's menu (28-31 days) from a correctly-formatted
  CSV and reach the point of pressing Save in under 30 seconds — versus up to 124 individual
  field entries (31 days × 4 fields) if typed by hand into the manual grid.
- **SC-002**: A CSV containing a mix of valid and invalid rows results in 100% of the valid rows
  being applied and 100% of the invalid rows being clearly identified with a specific reason,
  with zero rows silently dropped or silently mis-applied to the wrong date.
- **SC-003**: A director who has never used the feature before, given only the downloadable
  template and no other instructions, can fill it in and have it accepted as valid on the first
  upload attempt.
- **SC-004**: Re-importing a corrected CSV after fixing earlier errors requires no different
  steps than the first import, and produces the expected result on the first retry.
- **SC-005**: When an import would overwrite a day that already has director-entered content,
  100% of those rows are visibly flagged as an overwrite in the preview before the director
  confirms — never a silent replacement the director only discovers after saving.

## Assumptions

- Directors already maintain the month's menu content in a spreadsheet or similar tool outside
  this system, which motivated deferring this out of 013e's MVP; this feature does not itself
  provide spreadsheet-authoring tools, only the import path.
- CSV files are assumed to use standard comma-separated formatting with a header row; no support
  for alternative delimiters (semicolon, tab) is required for the initial version. Parsing relies
  on the browser's default UTF-8 decoding with no separate encoding auto-detection — a file saved
  in a different encoding (e.g. Windows-1252) may render accented Dutch/French characters
  incorrectly, but this would be visible in the preview (FR-010/FR-024) before the director
  confirms, not a silent corruption.
- No explicit file-size or row-count cap is enforced. A file covering the wrong month, or far
  more rows than one month needs, is handled by the same per-row validation as any other file
  (FR-006's out-of-range check, up to FR-014's zero-valid-rows rejection for a completely
  mismatched file) rather than a separate size limit.
- No dedicated "undo import" action exists. An already-confirmed-but-unsaved import merge is
  reverted the same way any unsaved manual grid edit is today — by re-editing the affected cells,
  or by navigating away/changing month without saving (see Edge Cases).
- The existing 013e day grid's whole-month "replace on Save" write model is unchanged by this
  feature; import only affects what is staged in the grid before that same Save action runs.
- No audit trail specific to CSV imports (distinct from manual edits) is required — an imported
  and then saved menu is indistinguishable from a manually typed one once persisted, matching
  013e's existing (lack of) field-level edit history.
- CSV export of an existing menu, and per-dietary-restriction variant menus (013j), are
  explicitly out of scope for this feature.
