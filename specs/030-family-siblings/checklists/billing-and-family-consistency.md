# Billing & Family-Consistency Requirements Checklist: Family Siblings

**Purpose**: Validate that the money-correctness requirements (sibling discount, family
bundling, paid-cascade) and multi-child/family data-consistency requirements are complete,
unambiguous, and consistent before implementation — the two highest-risk clusters in this spec
(financial output changes and per-child data integrity across siblings).
**Created**: 2026-07-19
**Feature**: [spec.md](../spec.md)

**Depth**: Standard. **Audience**: PR reviewer. **Focus**: Billing/money correctness (US2, US3);
multi-child/family data consistency (US1, US4, US5).

## Requirement Completeness

- [x] CHK001 Is the eligibility rule for sibling discount (which children qualify as a "sibling
  group") fully specified, not just the discount amount? [Completeness, Spec §FR-006]
- [x] CHK002 Is the tie-breaker for which sibling is full-price explicitly defined rather than
  left to implementation judgment? [Completeness, Spec §Assumptions]
- [x] CHK003 Are requirements defined for what happens to a discount/bundling group when a
  sibling's contract ends mid-period (only one child left invoiced for that period)? [Gap]
- [x] CHK004 Is the requirement for how a bundled invoice's per-child `OgmReference`/payment-
  matching identifiers behave specified, or only the combined total? [Completeness, Spec §FR-008]
- [x] CHK005 Are requirements defined for a partial-failure bulk day-reservation response
  (which children succeeded vs. skipped, and why) rather than only the happy path? [Completeness,
  Spec §FR-003]
- [x] CHK006 Is the requirement for reusing an existing contact vs. creating a new one during
  duplicate detection specific about the match criteria (email, phone, or both)? [Completeness,
  Spec §FR-013]

## Requirement Clarity

- [x] CHK007 Is "children sharing a common active parent contact" (discount eligibility)
  unambiguous about which contact when a child has multiple linked contacts? [Clarity, Spec
  §Clarifications]
- [x] CHK008 Is "one payment action covers the whole bundled group" quantified — does the
  requirement state which invoice statuses must match before the group is eligible (e.g. all
  must be `Sent`)? [Clarity, Spec §FR-009a]
- [x] CHK009 Is "reflects each child's own contract period" (US3 AC3) specific enough to be
  independently verified without referencing the implementation? [Clarity, Spec §User Story 3]

## Requirement Consistency

- [x] CHK010 Do the sibling-discount eligibility rule (FR-006, primary-contact-based) and the
  family-bundling grouping rule (Clarifications, also primary-contact-based) use identical
  language for "same primary contact," avoiding a subtle definitional drift between the two
  features? [Consistency, Spec §FR-006 vs §Clarifications]
- [x] CHK011 Is the "one location only" scoping (Edge Cases: siblings at two locations, no
  cross-location discount) stated consistently between the Edge Cases section and FR-005/FR-006?
  [Consistency, Spec §Edge Cases]
- [x] CHK012 Does the primary-contact-based bundling rule in User Story 3's Acceptance Scenario 5
  align with the identical rule in the Clarifications section, without restating it in
  conflicting terms? [Consistency, Spec §User Story 3 vs §Clarifications]

## Acceptance Criteria Quality

- [x] CHK013 Can "clearly labeled line item (not a silent reduction)" (FR-005) be objectively
  verified by a reviewer without inspecting implementation code? [Measurability, Spec §FR-005]
- [x] CHK014 Are the success criteria for bundling (SC-003: "reducing... to one") measurable
  against a concrete before/after invoice count rather than a qualitative impression?
  [Measurability, Spec §SC-003]
- [x] CHK015 Is SC-005 ("100% of locations that do not opt in see zero change") stated in a way
  that is testable against the existing 014/014a test suites, i.e. does the spec anticipate a
  regression-check acceptance path? [Measurability, Spec §SC-005]

## Scenario Coverage

- [x] CHK016 Are requirements defined for a family with 3+ children where the discount and
  bundling settings interact (all three grouped, only one full price)? [Coverage, Spec §User
  Story 2 AC3]
- [x] CHK017 Are requirements defined for the shared-custody scenario's effect on billing (two
  parent contacts, only one primary per child) — does discount/bundling grouping correctly
  exclude the non-primary parent's other children? [Coverage, Spec §Edge Cases]
- [x] CHK018 Are requirements defined for a bulk day-reservation submission where every selected
  child is blocked (fully-failed bulk action), not just the partially-blocked case? [Gap, Spec
  §FR-003]
- [x] CHK019 Are requirements defined for what a director sees/can do when disabling bundling
  after invoices were already bundled for a prior, still-open period? [Coverage, Spec §Edge
  Cases]

## Edge Case Coverage

- [x] CHK020 Is the behavior specified when a contact linked to a child is not a parent-app user
  (no login) with respect to discount/bundling eligibility? [Edge Case, Spec §Edge Cases]
- [x] CHK021 Is the behavior specified for twins with identical contract start dates (the
  full-price tie-breaker) — is there a defined secondary tie-breaker, or is the ambiguity
  acceptable/documented? [Edge Case, Gap]
- [x] CHK022 Is the requirement for a deactivated child's continued read-only access (FR-016)
  explicit about which historical data types are in scope, avoiding an open-ended "all history"
  claim? [Clarity, Spec §FR-016]

## Non-Functional Requirements

- [x] CHK023 Are i18n requirements (FR-018) explicit that the new discount line-item label,
  bulk-reservation UI, and Contacts-tab strings are all covered, not just a generic statement?
  [Completeness, Spec §FR-018]
- [x] CHK024 Are authorization requirements for the new bulk/family-scoped endpoints (a parent
  must only ever act on children they're linked to) stated as explicitly as the existing
  per-child endpoints' requirements, rather than assumed by extension? [Gap]

## Dependencies & Assumptions

- [x] CHK025 Is the assumption that `ChildContact`/`Contact` (from 006/013) is reused rather than
  a new data model clearly flagged as an assumption a reviewer should verify against current
  code, not treated as settled fact? [Assumption, Spec §Assumptions]
- [x] CHK026 Is the dependency on 013f's per-location reservation-mode resolution for bulk
  day-reservations' per-child outcome stated explicitly? [Dependency, Spec §FR-002]

## Notes

- All items passed on review — the spec's Clarifications section (added during `/speckit-
  clarify`) already resolved the three highest-risk billing ambiguities (wrap-vs-replace,
  payment cascade, bundling-by-primary-contact) before this checklist ran, which is why
  completeness/clarity/consistency items score clean rather than surfacing new gaps.
- CHK021 (twin tie-breaker) surfaced one genuine gap: the spec had no secondary tie-breaker for
  two siblings with an identical contract start date. Fixed directly in spec.md's Assumptions
  (earlier-created contract record as the deterministic secondary tie-breaker) rather than left
  as an open note, per this loop's standing rule to resolve checklist findings, not just log
  them.
