# Specification Quality Checklist: Child Profile UI

**Purpose**: Validate requirements quality (completeness, clarity, consistency, measurability,
coverage) for the CRUD-form and cross-platform-consistency aspects of this feature, ahead of
implementation.
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md)
**Depth**: Standard
**Audience**: Reviewer (pre-implementation)
**Focus areas**: (1) Field-level requirement completeness/clarity for the create/edit form and
its validation; (2) Cross-platform (web director / mobile caregiver) consistency of the
GP-vs-pediatrician distinction.

## Requirement Completeness

- [x] CHK001 - Is the full set of director-editable fields enumerated in one place rather than implied across multiple sections? [Completeness, Spec §Product Context/UX Requirements, §Requirements FR-002/FR-004]
- [x] CHK002 - Are validation requirements (required vs. optional) stated for every field individually, not just "medical fields are optional"? [Completeness, Spec §FR-001/FR-002]
- [x] CHK003 - Is a maximum-length or format constraint specified for every new field, or is it explicitly deferred to an existing precedent? [Completeness, Spec §Key Entities, data-model.md]
- [x] CHK004 - Are photo-upload requirements addressed in this feature's scope, or explicitly deferred to an existing mechanism? [Completeness, Spec §Assumptions]

## Requirement Clarity

- [x] CHK005 - Is "the child list screen" (FR-014's "New child" action) unambiguous about which screen it refers to, given a create flow and a detail flow exist? [Clarity, Spec §FR-014]
- [x] CHK006 - Is "distinct i18n keys" (FR-008) specific enough to be independently verifiable per locale, rather than a general intent statement? [Clarity, Spec §FR-008]
- [x] CHK007 - Is the phrase "inline, non-blocking-modal error feedback" (FR-013) precise enough to distinguish it from every other error-presentation pattern already used elsewhere in this codebase? [Clarity, Spec §FR-013]

## Requirement Consistency

- [x] CHK008 - Do the GP-contact requirements (FR-006, FR-007) and the pediatrician-contact requirements use parallel, non-conflicting language for their independence from each other? [Consistency, Spec §FR-006/FR-007]
- [x] CHK009 - Are the director (create/edit) and caregiver (read-only) requirements consistent about which fields exist on the entity, with no field appearing in one persona's requirements but not the other's data model? [Consistency, Spec §Key Entities vs. §FR-010]
- [x] CHK010 - Does the "Profiel"/"Gezondheid" tab requirement (FR-005) align with the Assumptions section's statement about which tabs are in vs. out of scope, without contradiction? [Consistency, Spec §FR-005 vs. §Assumptions]

## Acceptance Criteria Quality

- [x] CHK011 - Can SC-001's "under 1 minute" be objectively measured without requiring implementation knowledge (e.g., is the start/end action pair defined)? [Measurability, Spec §SC-001]
- [x] CHK012 - Is SC-002's "single form submission, zero fields requiring a separate save step" independently verifiable against FR-001-FR-002 without additional interpretation? [Measurability, Spec §SC-002]
- [x] CHK013 - Does SC-005 ("without a full page reload") define an outcome a non-technical reviewer could confirm, or does it presuppose implementation mechanics? [Clarity, Spec §SC-005]

## Scenario Coverage

- [x] CHK014 - Are requirements defined for what a director sees when opening the "Profiel" tab of a child created via the 012a waiting-list conversion flow (i.e., a record with most fields still null)? [Coverage, Spec §Edge Cases]
- [x] CHK015 - Are requirements defined for the caregiver-read scenario where the underlying child record itself (not just the two new fields) has never been populated with any medical/contact data? [Coverage, Spec §US3 Acceptance Scenario 3]
- [x] CHK016 - Are requirements defined for what happens if a director attempts to create a child while offline, or is web assumed always-online without that assumption being stated? [Gap, Spec §UX Requirements]

## Edge Case Coverage

- [x] CHK017 - Is the deactivated-child edit-permission edge case (Edge Cases, 4th bullet) specific about what "unchanged by this feature" means operationally, or does it defer entirely to an unreferenced external rule? [Clarity, Spec §Edge Cases]
- [x] CHK018 - Is there a requirement covering simultaneous edits by two directors to the same child record (concurrent-write conflict), or is this explicitly out of scope? [Gap, Spec §Edge Cases]

## Non-Functional Requirements

- [x] CHK019 - Are accessibility requirements for the create/edit form's keyboard navigation and focus order stated at the same specificity level as other director-web features, or only generally referenced? [Consistency, Spec §UX Requirements]
- [x] CHK020 - Are there any performance/volume requirements implied by this feature (e.g., large-organisation child counts) that are addressed, or is "no requirement" an explicit statement rather than a silent omission? [Completeness, Spec §Technical Requirements]

## Dependencies & Assumptions

- [x] CHK021 - Is the assumption that "the create/update commands, entity, and authorization already exist" validated against the actual codebase state as of this spec, with a citation the reader can check? [Traceability, Spec §Clarifications Session 2026-07-13]
- [x] CHK022 - Is the assumption that the 012a conversion-flow gap is out of scope stated with enough reasoning that a reviewer could dispute it on its merits rather than take it on faith? [Clarity, Spec §Clarifications/Edge Cases]

## Ambiguities & Conflicts

- [x] CHK023 - Is there any remaining ambiguity about whether "Profiel" tab content is fully replaced by an edit form or displays read-only values with a separate edit affordance? [Ambiguity, Spec §UX Requirements Main flow]
