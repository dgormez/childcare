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
