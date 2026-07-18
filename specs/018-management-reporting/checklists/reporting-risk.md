# Requirements Quality Checklist: Management Reporting

**Purpose**: Validate requirement completeness, clarity, and consistency in the four areas most
likely to hide gaps for this feature — tenant isolation, colour-coding/accessibility, calendar-day
boundary correctness, and export (CSV/PDF) parity with on-screen totals.
**Created**: 2026-07-18
**Feature**: [spec.md](../spec.md)

**Note**: This checklist tests the requirements themselves (are they complete, clear, consistent,
measurable), not whether any implementation works. All items below were resolved against
spec.md as of this checklist's review pass — see each item's resolution note.

## Tenant Isolation

- [x] CHK001 Is tenant scoping specified for every one of the five report sections individually,
  or only asserted once at a general level that a reader could miss applies to all of them?
  [Completeness, Spec §FR-012] — **Resolved**: FR-012 explicitly says "every report and the
  data-completeness monitor," which unambiguously covers all five sections; no per-section
  restatement needed.
- [x] CHK002 Is the behavior specified for what a multi-location director's `locationId` filter
  does if a `locationId` from another tenant is supplied — reject, ignore, or silently return
  nothing? [Gap, Spec §FR-013] — **Fixed**: FR-013 now states a foreign-tenant `locationId` is
  treated as no valid selection, never leaked or substituted.
- [x] CHK003 Are the data-completeness monitor's staff-related checks (qualification, PIN)
  explicitly scoped to tenant-owned `StaffProfile` rows, or could the requirement as written be
  read to include platform-wide staff data? [Ambiguity, Spec §FR-011] — **No change needed**:
  `StaffProfile` has no platform-wide equivalent anywhere in this codebase (unlike the
  `VaccineType` catalog), so there is no real reading under which this could mean non-tenant data.
- [x] CHK004 Is there a requirement that the aggregate (no-location-filter) view sums only the
  viewing director's own tenant's locations, distinct from a requirement that per-location
  results are correctly scoped? [Completeness, Spec §FR-013] — **No change needed**: FR-013's
  "aggregate across all their locations" combined with FR-012's tenant scope makes this
  unambiguous.

## Colour-Coding & Accessibility

- [x] CHK005 Are the numeric thresholds for green/amber/red occupancy status stated as a
  requirement, or only as an assumption/example — and is that distinction clear to an
  implementer who skips the Assumptions section? [Clarity, Spec Assumptions] — **Fixed**: new
  FR-020 states the occupancy threshold rule directly as a functional requirement.
- [x] CHK006 Is the requirement that colour is always paired with an icon stated once at a
  general level, or does each of the two colour-coded sections (occupancy, BKR) have its own
  explicit acceptance scenario proving it? [Consistency, Spec §FR-018] — **Fixed**: User Story 1
  Acceptance Scenario 2 already covered occupancy; Scenario 3 (BKR) now explicitly cross-references
  FR-018's icon pairing too.
- [x] CHK007 Is the specific icon-per-status pairing (which icon for green, which for amber, which
  for red) defined for this feature, or does it only cross-reference `design-system.md` without
  restating the concrete mapping an implementer needs? [Gap] — **Fixed**: FR-018 now states the
  concrete green→check-circle/amber→clock/red→alert-triangle mapping directly.
- [x] CHK008 Are keyboard-navigation and focus-ring requirements stated as testable acceptance
  criteria, or only as a general platform principle with no feature-specific scenario verifying
  the location filter, export buttons, and drill-in rows specifically? [Measurability, Spec UX Requirements]
  — **Fixed**: new FR-021 makes this a concrete, testable functional requirement naming the
  specific elements.
- [x] CHK009 Is there a requirement covering what a colour-coded status looks like for a value
  that legitimately has no meaningful colour (e.g., a group with `Capacity` unset)? [Edge Case,
  Spec Edge Cases] — **No change needed**: already covered by the existing "A group has no
  `Capacity` set" Edge Cases bullet.

## Calendar-Day Boundary Correctness

- [x] CHK010 Is "today" defined precisely enough (which calendar, which timezone) for an
  implementer to resolve it without re-deriving the definition from a different feature's spec?
  [Clarity, Spec §FR-016] — **No change needed**: FR-016 names the existing, established
  `BelgianCalendarDay` convention directly rather than leaving it implicit.
- [x] CHK011 Is there an explicit acceptance scenario covering the midnight-transition case for
  every section that has a "today" concept (occupancy, BKR live ratio), or only one general
  edge-case bullet covering all of them implicitly? [Coverage, Spec Edge Cases] — **No change
  needed**: the existing Edge Cases bullet is written generally ("today" is the unambiguous
  `BelgianCalendarDay`) and correctly applies uniformly to every "today" concept in this spec —
  a per-section restatement would be pure duplication, not new information.
- [x] CHK012 Is the boundary behavior for the monthly attendance summary specified — e.g., which
  calendar day counts as the first/last day of "the month" when a location operates across a
  timezone-adjacent boundary — or is this left to be inferred from the general "today" rule,
  which is a different concept (a day boundary vs. a month boundary)? [Gap] — **No change
  needed**: `AttendanceRecord.Date` is already an unambiguous calendar `DateOnly` (data-model.md);
  grouping existing calendar dates by month has no timezone ambiguity to resolve, unlike "today"
  which is a live clock-dependent concept.
- [x] CHK013 Is the default/boundary behavior for the BKR breach-history date range (last 30
  days when unspecified) stated as a testable requirement with a concrete boundary date, or only
  as prose that could be interpreted as 30 calendar days vs. 30×24 hours? [Ambiguity, Spec §FR-005]
  — **No change needed**: "last 30 days" in a feature already anchored to `BelgianCalendarDay`
  reads unambiguously as 30 calendar days; the contracts doc's `from`/`to` are typed as dates, not
  timestamps, which forecloses the hours reading.

## Export (CSV/PDF) Parity

- [x] CHK014 Is "totals match" between on-screen, CSV, and PDF exports defined precisely enough
  to be objectively verified (e.g., byte-for-byte numeric equality per row) or does it remain a
  qualitative statement an implementer could satisfy with only approximate agreement? [Measurability,
  Spec §SC-002] — **No change needed**: SC-002 already says totals must match "exactly," and
  research.md R5 specifies a single shared aggregation feeding all three outputs, which
  structurally guarantees exact agreement rather than leaving it to chance.
- [x] CHK015 Are the CSV column set and ordering specified as a requirement, or left entirely to
  the contracts document with no cross-reference from the spec's acceptance scenarios? [Gap,
  Spec User Story 3] — **No change needed**: exact column layout is implementation-level detail
  appropriately placed in contracts/data-model, not a business-level functional requirement; the
  spec's own quality bar (technology-agnostic FRs) correctly excludes it.
- [x] CHK016 Is there a requirement covering what happens if the export is requested for a month
  with zero attendance records at all (not just a closure day) — does it produce an empty-but-valid
  CSV/PDF, or is this scenario unaddressed? [Edge Case, Gap] — **Fixed**: new Edge Cases bullet
  covers a month with zero attendance records producing a valid, correctly empty result.
- [x] CHK017 Is the export failure/error behavior (retryable inline error, no stack trace)
  specified for CSV and PDF exports individually, or only asserted once generically for "a failed
  export" without confirming both formats are covered? [Consistency, Spec UX Requirements] —
  **No change needed**: the existing statement says "a failed export," which is format-agnostic
  by construction — restating it per format would be duplication, not new coverage.
- [x] CHK018 Is there a requirement that a re-generated/re-requested export for the same month
  reflects any attendance corrections made since the first request (i.e., not cached), or is this
  left unstated? [Gap] — **Fixed**: new FR-022 makes on-demand, never-cached export computation a
  direct functional requirement.

## Cross-Cutting Consistency

- [x] CHK019 Do the invoice status overview's "current month" scoping and the attendance
  summary's "director-selected month" scoping use consistent terminology for what a "month" means
  (calendar month, first-of-month anchor), or could a reader interpret them as two different
  conventions? [Consistency, Spec §FR-006, §FR-009] — **No change needed**: these are
  intentionally two different capabilities (a live current-status view vs. a historical/any-month
  report), not two inconsistent conventions for the same concept; both anchor to the same
  first-of-month calendar convention `Invoice.PeriodMonth` already uses.
- [x] CHK020 Is the data-completeness monitor's scope boundary (four specific checks, explicitly
  excluding staff document/dossier gaps) stated clearly enough that a reviewer wouldn't expect
  the BACKLOG's "staff document gaps" phrase to be implemented literally? [Clarity, Spec
  Assumptions] — **No change needed**: the Assumptions section already states this exclusion
  explicitly and names the reason (feature 028 doesn't exist yet) and the precedent it follows
  (013g/013h).
- [x] CHK021 Are success criteria SC-001 through SC-006 each independently measurable without
  relying on subjective judgment (e.g., "within 30 seconds" vs. "at a glance")? [Measurability,
  Spec Success Criteria] — **No change needed**: each SC already states a concrete, checkable
  condition (a time bound, a zero-untranslated-strings count, an exact totals match, or a binary
  "can identify every X").

## Notes

- Check items off as completed: `[x]`
- Seven items (CHK002, CHK005, CHK006, CHK007, CHK008, CHK016, CHK018) resulted in spec.md
  changes (FR-013, FR-018, new FR-020/FR-021/FR-022, a User Story 1 acceptance-scenario
  clarification, and a new Edge Cases bullet). The remaining fourteen items were verified against
  the current spec and found already adequate, with the reasoning recorded above rather than left
  as a bare pass.
