# CSV Import Requirements Quality Checklist: Monthly Menu CSV Import

**Purpose**: Validate the quality of `spec.md`'s requirements in the four highest-risk areas for
a client-side CSV import feature — parsing edge cases, validation completeness, merge-into-grid
semantics, and safety against silently corrupting director-entered data — before implementation.
**Created**: 2026-07-14
**Feature**: [spec.md](../spec.md)

**Note**: This checklist tests whether the *requirements* are complete, clear, and unambiguous —
not whether an implementation works. Items reference `spec.md` sections; `[Gap]` marks a missing
requirement, `[Ambiguity]` marks an underspecified one. All 16 findings below were fixed directly
in `spec.md` (plus `data-model.md`, `contracts/csv-format.md`, `research.md`, `tasks.md`) rather
than deferred, per this repo's standing rule.

## Requirement Completeness — CSV Parsing Edge Cases

- [x] CHK001 Is the expected date format (e.g. `YYYY-MM-DD` specifically, versus any locale-parseable date string) stated as a functional requirement, not only shown in `contracts/csv-format.md`'s example? [Completeness, Ambiguity, Spec §FR-005] — **Resolved**: FR-005 now pins `YYYY-MM-DD` explicitly and rejects locale-ambiguous formats.
- [x] CHK002 Are requirements defined for a CSV that includes a UTF-8 byte-order-mark (BOM) — the common case for a file exported directly from Excel, which Assumptions names as the primary source of these files? [Gap, Spec §Assumptions] — **Resolved**: new FR-020 requires stripping a leading BOM before parsing.
- [x] CHK003 Are requirements defined for header-row matching tolerance — case sensitivity and leading/trailing whitespace in column names (e.g. `" Date "` vs `date`)? [Gap, Spec §FR-004] — **Resolved**: new FR-019 requires case-insensitive, whitespace-tolerant header matching.
- [x] CHK004 Are requirements defined for a data row with a different column count than the header (a ragged/malformed row short of a full file-level parse failure)? [Gap, Edge Cases] — **Resolved**: new FR-021 treats a column-count mismatch as an invalid row, not a file-level failure; added to Edge Cases.
- [x] CHK005 Is leading/trailing whitespace within a cell value (e.g. a `date` cell of `" 2026-08-01 "`) addressed — trimmed and accepted, or treated as invalid? [Gap, Spec §FR-005] — **Resolved**: FR-005 now specifies whitespace is trimmed before format matching.

## Requirement Completeness — Validation Coverage

- [x] CHK006 Is the behavior for a row where `date` is present but every other field is blank explicitly stated as valid (mirroring the existing manual grid's "leave all fields blank" allowance), rather than left to be inferred? [Gap, Spec §FR-005–FR-008] — **Resolved**: new FR-023 states this explicitly; added to Edge Cases.
- [x] CHK007 Is an upper bound on file size or row count specified, or is it explicitly declared out of scope, for a director who accidentally uploads a much larger file than one month's worth of rows? [Gap, Non-Functional, Spec §Performance considerations] — **Resolved**: new Assumptions bullet explicitly declares no size cap, relying on existing out-of-range/zero-valid-rows handling instead.
- [x] CHK008 Are the four invalid-row reasons (FR-005–FR-008) specified as mutually exclusive per row, or is the requirement clear on which reason takes precedence when a single row triggers more than one (e.g. a duplicate date that is also out of range)? [Ambiguity, Spec §FR-005–FR-008] — **Resolved**: new FR-022 defines an explicit precedence order (also recorded in `research.md`).

## Requirement Clarity & Safety — Merge-Into-Grid Semantics

- [x] CHK009 Does the spec require the preview to indicate, for each row that will be applied, whether the target day in the grid currently already holds director-entered (non-blank) content that the import would overwrite? [Gap, Spec §FR-010] — **Resolved**: new FR-024 (highest-impact fix) plus new SC-005 and User Story 2's acceptance scenario 6.
- [x] CHK010 Is it specified whether a CSV row with blank field values (e.g. `soup` empty) for a date that already has non-blank content in the grid clears that existing content, or is skipped for that field? [Ambiguity, Spec §FR-012] — **Resolved**: FR-012 now states blank imported fields overwrite (whole-day overwrite-by-date, no partial field merge), made safe by FR-024's visibility requirement.
- [x] CHK011 Is the behavior specified for a director who uploads and confirms a second CSV import in the same session before saving — does the second import merge against the original grid state, or against the first import's already-merged (unsaved) state? [Gap, Edge Cases] — **Resolved**: new FR-025 specifies imports compose sequentially against current in-memory state.
- [x] CHK012 Is there a requirement for any way to revert an already-confirmed-but-not-yet-saved import merge, short of manually re-editing every affected cell? [Gap, Spec §FR-013] — **Resolved**: new Assumptions bullet explicitly declares no dedicated undo action; existing discard-on-navigate-away behavior covers it.
- [x] CHK013 Is the behavior specified for a director who changes the selected location or month after uploading a CSV but before confirming the preview — does the preview invalidate, or could a stale preview be confirmed against the wrong month? [Gap, Edge Cases] — **Resolved**: new FR-026 closes the dialog without merging if location/month changes while it's open; added to Edge Cases.

## Acceptance Criteria Quality

- [x] CHK014 Can SC-003 ("without needing outside documentation") be objectively verified, or does it need a more concrete, testable proxy (e.g. a specific first-time-use scenario with no assistance)? [Measurability, Spec §SC-003] — **Resolved**: SC-003 rewritten as "accepted as valid on the first upload attempt," an objectively checkable outcome.
- [x] CHK015 Is SC-001's "several minutes of manual per-day entry" baseline specified anywhere (a concrete existing measurement or a stated assumption), or is it an unverified comparison? [Measurability, Spec §SC-001] — **Resolved**: SC-001 rewritten to compare against a concrete count (up to 124 manual field entries) instead of a vague time estimate.

## Dependencies & Assumptions

- [x] CHK016 Is the Assumptions section's claim that CSV files use "UTF-8-compatible encoding" validated against the realistic case of a director's spreadsheet tool defaulting to a different encoding (e.g. Windows-1252) on export? [Assumption, Spec §Assumptions] — **Resolved**: Assumptions now states parsing relies on browser-default UTF-8 decoding only, and that a wrong-encoding file would show as garbled text in the preview (visible before confirming, not a silent corruption) — the same FR-010/FR-024 preview mechanism that fixes CHK009 also covers this case.

## Notes

- Highest-impact findings were CHK009 and CHK010 — the spec required an explicit confirm step
  (FR-011) but did not require the preview to surface *what existing data would be overwritten*,
  which was the specific mechanism by which this feature could have silently corrupted a
  director's prior manual entry despite the confirmation gate technically being satisfied. Fixed
  via new FR-024/SC-005.
- All 16 items are now resolved directly in the requirements artifacts; none were deferred as
  follow-up debt.
