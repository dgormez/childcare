# Research: Monthly Menu CSV Import

No `[NEEDS CLARIFICATION]` markers remain in `spec.md` — the synthesized BACKLOG.md prompt block
already resolved every open question against 013e's shipped code before the spec was written.
This document records the handful of implementation-level decisions research surfaced while
grounding the plan in the actual `web/` codebase.

## Decision: CSV parsing library

**Decision**: Use `papaparse` for client-side CSV parsing.

**Rationale**: CSV quoting/escaping (a comma or newline inside a quoted `notes` field, e.g. a
director writing `"Soep, dan hoofdgerecht"`) is exactly the class of edge case a hand-rolled
`split(",")` parser gets wrong silently. `papaparse` is dependency-free, MIT-licensed, has no
Node-specific APIs (works in-browser), and is small enough (~20KB) that adding it as a `web/`
dependency carries negligible cost against the time saved not re-implementing RFC 4180 parsing
by hand.

**Alternatives considered**:
- Hand-rolled split-based parser — rejected: silently mishandles quoted fields containing commas,
  which is a realistic real-world case for free-text `notes`/`main_course` values.
- `csv-parse` (Node's `csv` package family) — rejected: primarily designed for Node streams; less
  ergonomic for a one-shot in-browser `File` → rows parse than `papaparse`'s `Papa.parse(file,
  { header: true, complete })` API.

## Decision: Merge integration point

**Decision**: `MonthlyMenuDayGrid.tsx` gains a new prop (e.g. `onImportMerge`) or an exported
merge helper that accepts a `Map<string, DayFields>` (or equivalent parsed-row list) and applies
it onto the grid's existing internal `toFieldMap`-derived state, keyed by date — not a new
separate state store.

**Rationale**: The grid already holds one row of `DayFields` per calendar date of the month
(`toFieldMap`, `web/components/menu/MonthlyMenuDayGrid.tsx`). Reusing that exact state shape
means the merge is a plain object-map overlay (imported dates overwrite, others untouched) with
no risk of the import path and the manual-typing path drifting into two different representations
of "the grid's current values." The existing Save button already sends the full `MonthlyMenuDaySave[]`
derived from that same state, so nothing about the write path changes.

**Alternatives considered**:
- A separate "staged import" state merged only on an explicit second confirm inside the grid —
  rejected as unnecessary complexity: the spec's own preview-then-confirm step (FR-010/FR-011)
  already is that confirmation gate, performed in the import dialog *before* touching grid state;
  a second confirmation inside the grid would be redundant.

## Decision: Validation logic placement

**Decision**: All parsing/validation lives in a new framework-free module, `web/lib/menu/
csvImport.ts`, exporting pure functions (`parseCsv`, `validateRows`, or a combined
`parseAndValidateMenuCsv`) returning a typed result (valid rows + invalid rows with reasons).
The dialog component only renders that result and calls the merge function on confirm.

**Rationale**: Matches this codebase's existing separation of validation/business logic from
components (e.g., `UpsertMonthlyMenuCommandValidator` on the backend keeps validation out of the
handler). Keeping parsing/validation as pure functions makes them directly unit-testable without
rendering a component, and keeps `MonthlyMenuCsvImportDialog.tsx` a thin presentation layer.

**Alternatives considered**: Inlining parse/validate logic directly in the dialog component —
rejected: harder to unit test in isolation, and mixes I/O-adjacent (file reading) concerns with
pure validation logic that has no reason to depend on React.

## Decision: Field-length validation source of truth

**Decision**: The 500-character-per-field limit is duplicated as a named constant in
`web/lib/menu/csvImport.ts` (e.g. `MAX_FIELD_LENGTH = 500`), matching
`UpsertMonthlyMenuCommandValidator`'s existing `MaximumLength(500)` rules on the backend.

**Rationale**: No shared contract file exists between backend FluentValidation rules and frontend
TypeScript validation in this codebase yet (confirmed by inspecting `013e`'s implementation — the
manual grid has no client-side length pre-check at all today, relying entirely on the server
returning a validation error). Introducing cross-language contract generation for a single
integer constant is out of scope for this feature; a duplicated, clearly-commented constant is
the same trade-off this codebase already accepts elsewhere (e.g. `design-decisions.md`'s note on
`mobile/theme/colors.js` vs `web/theme/colors.ts` being hand-kept-in-sync copies). If the limit
ever changes, both the backend validator and this constant must be updated together — worth
flagging in the constant's own code comment for future maintainers.

**Alternatives considered**: Skipping client-side length validation entirely and letting the
existing Save button's server-side validation catch it — rejected: the spec (FR-008) explicitly
requires the *import preview* to flag an over-length field before the director ever gets to Save,
which requires the check to exist client-side.

## Decision: CSV template content

**Decision**: The "Download template" action (FR-016) generates a CSV client-side (via a
`data:` URL / `Blob` download, no backend call) containing the header row
(`date,soup,main_course,dessert,notes`) plus one example data row using a placeholder date in the
grid's currently-selected month.

**Rationale**: Consistent with the whole feature being client-side-only; no backend endpoint is
needed to serve a static template. Using the currently-selected month's first day as the example
date row means the downloaded template opens as an immediately-valid file if re-uploaded
unmodified (User Story 3's acceptance scenario), rather than needing the director to already know
the date format.

## Decision: Overwrite-visibility computation (FR-024)

**Decision**: `willOverwriteExisting` is computed inside `validateMenuCsvRows` by accepting the
grid's current `Map<string, DayFields>` as an input parameter alongside the parsed rows, and
checking — for each row that validates as `"valid"` — whether the map already has a non-blank
`DayFields` entry (any of the four fields non-empty) at that date. This is a pure comparison, not
a stored flag, so it always reflects whatever the grid's state was at the moment of upload,
including the effect of a prior unsaved import in the same session (FR-025).

**Rationale**: This closes the highest-impact gap `/speckit-checklist`'s `csv-import.md` run
found (CHK009/CHK010): without it, a director could confirm an import that silently replaces
manually-typed content with no visibility into what was overwritten until after Save. Computing
it as part of validation (rather than as a separate pass in the dialog component) keeps the "is
this safe to apply" decision entirely within the already-tested `csvImport.ts` module.

**Alternatives considered**: Computing the overwrite flag inside the dialog component instead —
rejected: would split "is this row valid" and "would this row overwrite something" across two
places that both need the same grid-state snapshot, risking the two falling out of sync.

## Decision: Invalid-reason precedence order (FR-022)

**Decision**: `validateMenuCsvRows` checks each row in a fixed order and returns the first
failing check's reason: date format/parseability → out-of-range → duplicate-within-file → field
length. A row failing multiple checks (e.g. an unparseable date that also happens to duplicate
another unparseable date once both are excluded) is still reported with exactly one reason.

**Rationale**: `/speckit-checklist` flagged that the original FR-005–FR-008 wording didn't state
what happens when more than one condition applies to the same row, which would otherwise leave
the exact preview message implementation-defined. Checking date validity first is a genuine
correctness dependency, not just a display choice — the duplicate-date check partitions rows by
`date`, so an unparseable date must be resolved (or the row already excluded) before duplicate
detection can group rows meaningfully. Out-of-range is checked before duplicate detection since a
row outside the file's own selected month is a more specific, more actionable diagnosis than
"this shares a date with another out-of-range row." Field length is checked last since it is
independent of every other check and any of the other three conditions are more likely to be
what the director needs to fix first to get the row recognized as a real menu day at all.
