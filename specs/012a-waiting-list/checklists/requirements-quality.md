# Specification Quality Checklist: Waiting List Management

**Purpose**: Validate requirements quality (completeness, clarity, consistency, measurability, coverage) before implementation
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)
**Depth**: Standard — **Audience**: Reviewer (PR) — **Focus**: (1) status-lifecycle & priority-ordering requirement clarity, (2) occupancy/data-consistency edge-case coverage

## Requirement Completeness

- [x] CHK001 - Are the required vs. optional fields for entry creation explicitly enumerated? [Completeness, Spec §FR-001]
- [x] CHK002 - Is the default value and derivation rule for a new entry's `priority` documented? [Completeness, Spec §FR-002]
- [x] CHK003 - Are requirements defined for what happens when a director attempts to reorder an entry that is not in `waiting` status? [Completeness, Spec §FR-005]
- [x] CHK004 - Is the full set of valid status transitions enumerated as an explicit allow-list, rather than described only by example? [Completeness, Spec §FR-007]
- [x] CHK005 - Are requirements defined for the case where an entry is left `enrolled` without ever being linked to a child record? [Completeness, Spec §FR-012]
- [x] CHK006 - Are authorization requirements specified for every new read and write operation individually, rather than only at a feature-wide level? [Completeness, Spec §FR-016, §FR-017]

## Requirement Clarity

- [x] CHK007 - Is "likely duplicate" quantified with an explicit matching rule rather than left to implementer judgment? [Clarity, Spec §FR-004]
- [x] CHK008 - Is "free capacity" defined with an explicit, unambiguous computation rather than only a narrative description? [Clarity, Spec §FR-014]
- [x] CHK009 - Is the scope of "per-location" priority ordering explicit enough to rule out a global-ordering interpretation? [Clarity, Spec Clarifications]
- [x] CHK010 - Is it unambiguous which transition(s) trigger the email notification versus which do not? [Clarity, Spec §FR-008, §FR-009]

## Requirement Consistency

- [x] CHK011 - Do the status-lifecycle requirements (§FR-007) and the priority-reorder restriction (§FR-005, only `waiting` entries) agree on which states are "active" versus "terminal"? [Consistency]
- [x] CHK012 - Are the default-filter requirement (§FR-003, defaults to `waiting`) and the duplicate-flagging requirement (§FR-004, applies across "the list") consistent about whether duplicate flagging applies only to the default filtered view or to every status? [Consistency]
- [x] CHK013 - Do the occupancy requirements (§FR-013-015) and the Assumptions section's sourcing decision (contracts, not attendance) agree without residual reference to attendance anywhere else in the spec? [Consistency]

## Acceptance Criteria Quality

- [x] CHK014 - Can SC-002 ("100% of status transitions outside the allow-list are rejected") be objectively verified against a finite, enumerable set of transition pairs? [Measurability, Spec §SC-002]
- [x] CHK015 - Can SC-003 ("100% of dates with a published closure entry show Closed") be objectively verified without ambiguity about what counts as a "published" closure entry? [Measurability, Spec §SC-003]
- [x] CHK016 - Is SC-001's "under 1 minute" registration target scoped clearly enough (e.g., data-entry time only, excluding page navigation) to be objectively measured? [Measurability, Spec §SC-001]

## Scenario Coverage

- [x] CHK017 - Are requirements defined for the primary registration flow, the reorder flow, the full status-lifecycle flow, the occupancy-view flow, and the enrollment/child-link flow, as five independent scenario classes? [Coverage, Spec User Stories 1-5]
- [x] CHK018 - Are exception/rejection-flow requirements defined for each write operation (invalid transition, non-reorderable status, missing required field)? [Coverage, Spec §FR-007, §Edge Cases]
- [x] CHK019 - Are requirements defined for the recovery path when a director marks an entry `offered` in error (i.e., the `offered → waiting` revert)? [Coverage, Spec §FR-007]

## Edge Case Coverage

- [x] CHK020 - Is the behavior specified for a waiting-list entry whose target `Location` is later deactivated? [Edge Case, Spec §Edge Cases]
- [x] CHK021 - Is the behavior specified for concurrent priority-reorder requests on the same location's queue? [Edge Case, Spec §Edge Cases]
- [x] CHK022 - Is the behavior specified for an occupancy query where the location has zero active contracts (fully empty)? [Edge Case, Gap]
- [x] CHK023 - Is the behavior specified for a family that withdraws and later reapplies (new entry vs. reopening the old one)? [Edge Case, Spec §Edge Cases]

## Non-Functional Requirements

- [x] CHK024 - Are internationalization requirements specified for every new user-facing string, including status labels and the duplicate badge? [Coverage, Spec §FR-018]
- [x] CHK025 - Are accessibility requirements specified for the priority-reorder interaction specifically (not just interactive elements generally)? [Coverage, Spec §UX Requirements]
- [x] CHK026 - Are performance/scale assumptions (row-count expectations, indexing needs) documented rather than left implicit? [Completeness, Spec §Technical Requirements]

## Dependencies & Assumptions

- [x] CHK027 - Is the dependency on feature 007's `Contract`/`ContractedDays` shape for occupancy computation explicitly documented? [Dependency, Spec §Assumptions]
- [x] CHK028 - Is the assumption that email notification reuses an existing mechanism (rather than feature 020's not-yet-built service) explicitly validated against the current codebase state? [Assumption, Spec §Assumptions]
- [x] CHK029 - Is the decision not to add a location-deactivation guard for referencing waiting-list entries explicitly justified rather than silently omitted? [Assumption, Spec §Assumptions]

## Notes

- Most items were already resolved during specification/clarification — see spec.md's
  Clarifications and Assumptions sections. This checklist pass found and fixed two genuine
  gaps, per this project's standing rule to fix every finding rather than log it as debt:
  - **CHK012**: FR-004 didn't specify whether duplicate detection compares across all statuses
    for a location or only the currently filtered/returned slice — fixed by making FR-004 (and
    data-model.md's Duplicate flagging section) explicit that detection always compares
    against the full location roster regardless of the applied status filter, so a duplicate
    is never missed just because its twin is hidden behind the default `waiting`-only view.
    tasks.md's T016 updated to test this explicitly.
  - **CHK022**: The zero-active-contracts occupancy case wasn't called out explicitly — fixed
    by adding it to spec.md's Edge Cases (the general FR-014 formula already handles it
    correctly with no special-casing needed; this just makes that explicit rather than
    implicit).
