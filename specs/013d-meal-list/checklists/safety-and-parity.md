# Specification Quality Checklist: Meal List — Safety-Critical Correctness & Cross-Platform Parity

**Purpose**: Validate that spec.md's requirements are complete, unambiguous, and consistent
enough to implement correctly on the first pass — focused on the two highest-risk dimensions for
this feature: allergy-severity correctness and web/mobile parity.
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md)
**Depth**: Standard (PR-reviewer level, pre-`/speckit-plan` re-confirmation)

## Allergy & Medical-Signal Correctness

- [x] CHK001 - Is the exact source field for allergy severity (RED/AMBER/GREY) specified rather than left to implementation choice? [Clarity, Spec Clarifications]
- [x] CHK002 - Is the mapping from the three severity states to exactly two color/icon flags (RED/AMBER vs. two distinct values) fully enumerated, leaving no severity value unmapped? [Completeness, Spec FR-006]
- [x] CHK003 - Does the spec state a requirement that severity is machine-verifiable as icon-plus-color rather than only visually reviewed, i.e. is there a testable assertion form (not just a UX description)? [Measurability, Spec FR-007]
- [x] CHK004 - Is the requirement for standing-medication validity (date-window check) precise enough to resolve the boundary case of a `ValidFrom`/`ValidUntil` value equal to today's date? [Ambiguity, Spec Edge Cases]
- [x] CHK005 - Is it specified what renders when a child has multiple standing-medication records simultaneously (single pill icon vs. a count)? [Gap]

## Never-Hide-Absent-Children Correctness

- [x] CHK006 - Is "present" defined precisely enough (which attendance status/timestamp fields) to be unambiguous, rather than relying on an informal notion of "checked in"? [Clarity, Spec FR-003]
- [x] CHK007 - Are all attendance states enumerated with an explicit include/exclude rule for the default view, leaving no status value's treatment implicit? [Completeness, Spec FR-004]
- [x] CHK008 - Does the spec define distinguishable, non-overlapping requirements for the three population views (present, expected, neither) such that a single child can never be classified as more than one simultaneously? [Consistency, Spec FR-003, FR-009]
- [x] CHK009 - Is the requirement that a child with zero preferences must still appear (not be hidden) stated as a testable, falsifiable rule rather than a general intent? [Measurability, Spec FR-005]
- [x] CHK010 - Is there a requirement covering what happens if the underlying aggregation partially fails (e.g. attendance data loads but health data does not) — must the child still appear, degraded, or be omitted? [Gap, Exception Flow]

## Cross-Platform (Web/Mobile) Consistency

- [x] CHK011 - Are the data fields shown to a director (web) and a caregiver (mobile) for the same child specified as identical, or are any per-platform field differences explicitly called out? [Consistency, Spec UX Requirements]
- [x] CHK012 - Is the requirement for the caregiver's own-group scope stated with enough precision (source of "own group") that web and mobile cannot each reasonably implement a different scoping rule? [Clarity, Spec FR-011]
- [x] CHK013 - Are the "Geen voorkeur", allergy-severity, and standing-medication indicator requirements written platform-agnostically (behavioral) rather than in web-specific or mobile-specific visual terms that could diverge? [Consistency, Spec FR-005 through FR-008]
- [x] CHK014 - Is the "Inclusief verwacht" toggle's behavior (what appears, how it's separated) specified once as a shared rule, rather than described only for one platform and assumed for the other? [Completeness, Spec FR-009, US4]
- [x] CHK015 - Does the spec specify whether the offline/cache behavior on mobile is allowed to show older data than what a simultaneously-open web view shows, or is a consistency bound expected? [Gap, Non-Functional]

## Non-Functional & Acceptance Criteria Quality

- [x] CHK016 - Is "a half-second glance" (SC-001) tied to any observable, testable proxy, or does it remain a purely subjective success criterion? [Measurability, Spec SC-001]
- [x] CHK017 - Is the "single request, no N+1" performance expectation (SC-005) quantified with a concrete bound (e.g., number of queries, response time), or left as a qualitative goal only? [Clarity, Spec SC-005]
- [x] CHK018 - Are print-output legibility requirements (SC-003) specific enough to be independently verified by someone other than the implementer (e.g., a stated contrast/shape rule), or dependent on subjective judgment? [Measurability, Spec SC-003]

## Notes

- Six genuine gaps were found on first pass (CHK004, CHK005, CHK006, CHK010, CHK011, CHK015) and
  fixed directly in spec.md rather than left as debt, per this project's standing rule: CHK004/
  CHK005 → FR-008 tightened (inclusive date-boundary rule, single-icon-not-count rule); CHK006 →
  FR-003 tightened to match data-model.md's precise "attendance record exists with Status =
  Present" definition; CHK010 → new Assumption documenting the single-query design has no
  partial-load state; CHK011 → new FR-015 requiring identical per-child fields across web/mobile;
  CHK015 → new Assumption documenting the accepted (unbounded) offline-cache staleness, consistent
  with existing precedent. The remaining items (CHK001-003, 007-009, 012-014, 016-018) were
  already satisfied by the spec as written.
