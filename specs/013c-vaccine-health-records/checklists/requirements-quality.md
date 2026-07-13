# Specification Quality Checklist: Vaccine & Health Records

**Purpose**: Validate requirements quality (completeness, clarity, consistency, measurability,
coverage) before/during implementation — not a test of the implementation itself.
**Created**: 2026-07-12
**Feature**: [spec.md](../spec.md)
**Depth**: Standard. **Audience**: Reviewer (pre-implementation). **Focus**: (1) GDPR/regulatory
record-keeping requirements for medical data, (2) dual-persona UX consistency (director CRUD vs.
caregiver read-only) and the edge cases around attachments, deactivation, and the legacy-table
migration.

## Requirement Completeness

- [X] CHK001 Is a maximum or pagination behavior specified for the "Vaccinations due soon"
      dashboard block when many children are due at once? [Gap, Spec §FR-010] — **Resolved**:
      FR-010 now states no pagination, single scrollable list, justified against this system's
      actual scale.
- [X] CHK002 Are the allowed attachment file types and any size limit specified as a
      user-facing requirement, not only as an implementation detail? [Gap, Spec §FR-006] —
      **Resolved**: FR-006 now specifies PDF/JPEG/PNG, max 10MB, with a user-facing rejection
      message requirement; propagated to data-model.md, contracts/, and tasks.md.
- [X] CHK003 Does the spec define what happens when a director attempts to add a vaccine or
      health record for a deactivated/soft-deleted child? [Gap, Edge Case] — **Resolved**: new
      Edge Cases bullet — writes are allowed regardless of the child's active/deactivated state.
- [X] CHK004 Is there a requirement addressing duplicate vaccine record entry (e.g. the same
      vaccine + dose number recorded twice for one child) — intentionally unconstrained, or a
      gap? [Gap, Spec §FR-001] — **Resolved**: new Edge Cases bullet — no deduplication is
      enforced, by design.
- [X] CHK005 Is a requirement in place for what a caregiver sees when a child has more than one
      due-soon/overdue vaccine (does the summary show all, or only the most urgent, and does that
      match the dashboard's own one-per-child collapsing behavior)? [Gap, Spec §FR-013] —
      **Resolved**: FR-013 now explicitly shows all due-soon/overdue flags for the one child in
      view, deliberately not collapsed like the cross-child dashboard.

## Requirement Clarity

- [X] CHK006 Is "due soon" (FR-009) unambiguous at its boundary — is a vaccine whose
      `next_due_date` is exactly today classified as due-soon or overdue? [Ambiguity, Spec §FR-009]
      — **Resolved**: FR-009 now states today = due-soon, strictly-before-today = overdue.
- [X] CHK007 Is "one additional tap beyond the existing medical quick-access sheet" (FR-013)
      measurable given the quick-access sheet itself is already reached in one tap from the group
      view — does this mean the health summary is the *same* tap/sheet, or a second tap from
      within it? [Ambiguity, Spec §FR-013] — **Resolved**: FR-013 reworded — this feature extends
      the existing sheet's content, no additional tap or second sheet.
- [X] CHK008 Is the `other` health-record type's intended use distinguished clearly enough from
      `doctor_note` to prevent inconsistent categorization by different directors? [Clarity, Spec
      §Key Entities] — **Resolved**: Key Entities now defines the distinction as source/origin
      (clinical visit vs. everything else), not severity.

## Requirement Consistency

- [X] CHK009 Do FR-011 (dashboard distinguishes overdue vs. upcoming) and FR-013 (caregiver
      summary shows "due-soon/overdue vaccine flags") agree on whether the caregiver-facing view
      also needs the overdue/upcoming visual distinction, or only a single undifferentiated flag?
      [Consistency, Spec §FR-011, §FR-013] — **Resolved**: FR-011 now explicitly requires the same
      icon pairing to be reused on the caregiver summary sheet; tasks.md T069 cross-references
      T060's `DueSoonBlock` icon choice.
- [X] CHK010 Are the "empty state" requirements (FR-019) consistent between the director's
      Gezondheid tab (per-section empty states) and the caregiver summary sheet (single combined
      empty state), or does one context risk being overlooked during implementation? [Consistency,
      Spec §FR-019] — **Verified, no change needed**: FR-019's wording already explicitly covers
      both contexts by name.

## Acceptance Criteria Quality

- [X] CHK011 Is SC-001's "under one minute" add-record success criterion measurable without a
      defined starting point (e.g. from tapping "Add" vs. from opening the Gezondheid tab)?
      [Measurability, Spec §SC-001] — **Resolved**: SC-001 now specifies the starting point
      (tapping "Add") and ending point (record appears in the list).
- [X] CHK012 Is SC-004 ("100% absent from bulk export... unless opted in") verifiable today given
      no bulk export/email-summary feature exists yet in this codebase, or does it only become
      testable once such a feature ships — and is that timing dependency stated anywhere? [Gap,
      Spec §SC-004] — **Resolved**: SC-004 now states how it's verified today (regression test)
      vs. what becomes possible once a real export feature ships.

## Scenario Coverage

- [X] CHK013 Are exception-flow requirements defined for an attachment upload that succeeds on
      the storage side but the follow-up API confirmation call fails (partial-success state)?
      [Gap, Exception Flow, Spec §FR-007] — **Resolved**: new Assumptions bullet — the design has
      no separate confirmation step, structurally avoiding this failure class rather than needing
      to handle it.
- [X] CHK014 Is a recovery/rollback requirement defined for the legacy `vaccination_records`
      migration if the backfill copy step fails partway through (spec.md doesn't mention the
      legacy table at all — this is entirely captured in research.md/data-model.md instead)?
      [Gap, Recovery Flow] — **Resolved**: new Edge Cases bullet in spec.md — the migration is a
      single transactional operation; a failure rolls back automatically.
- [X] CHK015 Are requirements defined for what a director sees if they attempt to edit a vaccine
      or health record that another director has just soft-deleted (concurrent edit vs. delete,
      not just concurrent edit vs. edit)? [Gap, Edge Case] — **Resolved**: new Edge Cases bullet —
      treated as a standard not-found response.

## Non-Functional Requirements

- [X] CHK016 Are the GDPR export-exclusion requirements (FR-016) specific enough about *what
      constitutes* "an explicit, separate action" (a checkbox, a confirmation dialog, a named
      permission) to be implementable consistently whenever a future export feature is built?
      [Clarity, Spec §FR-016] — **Resolved**: FR-016 now explicitly defers the exact UI mechanism
      to whichever future feature builds the export, since none exists yet to specify against.
- [X] CHK017 Is a requirement defined for whether health-record attachments are included in the
      same GDPR export-exclusion rule as the structured record data, or only the record fields?
      [Gap, Spec §FR-016] — **Resolved**: FR-016 now explicitly includes attachments.
- [X] CHK018 Are accessibility requirements for the attachment upload control itself (keyboard
      operability, screen-reader labeling of upload progress/failure) specified, beyond the
      general "web meets keyboard navigation" statement? [Gap, Spec §UX Requirements] —
      **Resolved**: new FR-020 requires keyboard operability and `aria-live` progress/failure
      announcements; propagated to tasks.md T053.

## Dependencies & Assumptions

- [X] CHK019 Is the assumption that "the caregiver quick-access summary extends feature 008's
      existing sheet" validated against that sheet's actual current capacity/layout constraints,
      or only asserted? [Assumption, Spec §Assumptions] — **Resolved**: Assumptions section now
      states this is validated during implementation with an explicit fallback (layout adjustment
      within the same sheet, never a second sheet/extra tap); tasks.md T069 carries the explicit
      verification step.
- [X] CHK020 Is the dependency on a not-yet-existing per-child detail screen (director web) scoped
      tightly enough in the Assumptions section to prevent this feature's implementation from
      growing into a full child-file screen? [Spec §Assumptions] — **Resolved**: Assumptions
      section tightened to name exactly what the shell includes (header + Gezondheid tab only)
      and explicitly rules out adding other tabs speculatively.

## Notes

- All 20 items resolved during the plan/tasks phase — per this project's standing rule
  (process-next-feature.md), no LOW-severity item is left as logged debt.
