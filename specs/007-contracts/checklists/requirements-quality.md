# Specification Quality Checklist: Enrolment Contracts

**Purpose**: Validate requirements quality — focus areas: the day-overlap validator, the amendment/termination lifecycle, and the money/JSONB fields
**Created**: 2026-07-07
**Feature**: [spec.md](../spec.md)
**Depth**: Standard | **Audience**: Reviewer (pre-`/speckit-plan` gate, matching prior features' pattern)

## Requirement Completeness

- [X] CHK001 - Is a validation rule against duplicate weekdays within a single contract's `contractedDays` documented in the Functional Requirements, not only in the data model? [Gap, Spec §FR-001] — fixed: FR-001 now states "each weekday appearing at most once".
- [X] CHK002 - Is a positivity constraint on `dailyRateCents` (must be greater than zero) stated as a Functional Requirement, not only inferred from the Assumptions/money-in-cents framing? [Gap, Spec §FR-001/FR-012] — fixed: FR-001 now states "a whole number greater than zero".
- [X] CHK003 - Does the spec define what a contract's photo/media consent choices default to when a create/update/amend request omits the consent object entirely or omits individual fields within it? [Gap, Spec §FR-010] — fixed: FR-010 now states omitted flags default to `false`, never `true`.

## Requirement Clarity

- [X] CHK004 - Does FR-007 specify whether an amendment's supplied terms are a full replacement of the contract's terms or a partial merge with the existing ones? [Ambiguity, Spec §FR-007] — fixed: FR-007 now states "a full replacement, not a partial patch".
- [X] CHK005 - Does the spec clarify whether a draft contract's edit capability (FR-001-adjacent, in-place mutation) permits changing which child or location the contract is for, or whether those are fixed at creation? [Ambiguity, Spec §FR-001] — fixed: new FR-001a states child/location are fixed at creation and edits are a full-replacement of terms only.
- [X] CHK006 - Is the locale used for contract PDF text generation specified, given the PDF is rendered server-side as a fixed set of bytes (unlike JSON error responses, where the client resolves a locale key)? [Gap, Spec §FR-011/FR-016] — fixed: FR-011 now specifies a director-selected locale at generation time, defaulting to Dutch.

## Acceptance Criteria Quality

- [X] CHK007 - Is SC-002's "zero exceptions observed under concurrent activation attempts" paired with a concrete verification method (e.g., a minimum number of simultaneous attempts) so it is objectively measurable rather than aspirational? [Measurability, Spec §SC-002] — fixed: SC-002 now specifies "at least 20 truly simultaneous conflicting activation attempts".

## Scenario Coverage

- [X] CHK008 - Does the spec address what happens when a `PUT` (draft edit) request omits fields that were previously set on the draft — full replacement (like create) or partial patch? [Gap, Spec §FR-001, Edge Cases] — fixed: new FR-001a states draft edits are "a full replacement of those terms".

## Consistency

- [X] CHK009 - Are the "at most one active contract per location" (FR-004) and "day-overlap across locations" (FR-005) rules phrased so a reader can tell they are two independent checks (not one subsuming the other), including in the case where a child has zero prior contracts? [Consistency, Spec §FR-004/FR-005] — fixed: both FRs now cross-reference each other and state they apply independently.

## Notes

- CHK001/CHK002/CHK003 are Completeness gaps found by comparing spec.md's Functional Requirements against data-model.md/research.md, which already encode these rules — the rule exists somewhere in the design, just not yet as a testable FR a reader of spec.md alone would catch.
- CHK003 and CHK006 are flagged higher-impact than the others: CHK003 touches consent for photographing/filming minors (a privacy-sensitive default getting this wrong in the unsafe direction would be a real incident, not a cosmetic gap); CHK006 is the first feature in this codebase to render user-facing text server-side into a fixed artifact, so there is no existing precedent to fall back on silently.
