# Migration Safety & Requirements Quality Checklist: Child Events — Custom Type & Growth Check Rename

**Purpose**: Validate that the spec/plan/tasks fully and unambiguously specify (a) the `custom`
event type's requirements and (b) the `measurement` → `growth_check` rename's migration safety,
before implementation begins.
**Created**: 2026-07-09
**Feature**: [spec.md](../spec.md), [plan.md](../plan.md), [data-model.md](../data-model.md), [research.md](../research.md)

## Requirement Completeness — `custom` type

- [x] CHK001 Are the exact required/optional fields of the `custom` payload specified? [Completeness, Spec §FR-001]
- [x] CHK002 Is a maximum length for `label` documented rather than left open-ended? [Completeness, Spec §FR-002]
- [x] CHK003 Is the rendering rule for `custom` (label as headline, text as detail) specified for every consuming view, not just one screen? [Completeness, Spec §FR-004]
- [x] CHK004 Are `custom` events' participation in every cross-cutting rule (edit window, soft-delete, staff-internal visibility, offline sync, pagination) explicitly stated rather than assumed by omission? [Completeness, Spec §FR-005]

## Requirement Clarity — `custom` type

- [x] CHK005 Is "a short caregiver-supplied title" quantified with a specific character bound rather than left as a subjective description? [Clarity, Spec §FR-002]
- [x] CHK006 Is the distinction between `custom` and `note` (the reason this type exists at all) stated as a testable difference rather than a stylistic preference? [Clarity, Spec §User Story 1]

## Scenario Coverage — `custom` type

- [x] CHK007 Is the empty/whitespace-only label case explicitly addressed as a rejection scenario? [Edge Case, Spec §Edge Cases]
- [x] CHK008 Is the over-length label case explicitly addressed with the same rejection pattern as other malformed payloads? [Edge Case, Spec §Edge Cases]
- [x] CHK009 Is the offline-recording scenario for `custom` addressed explicitly, or only implied by "identical to every other type"? [Coverage, Spec §Edge Cases]
- [x] CHK010 Is a decision recorded (not left implicit) on whether `custom` participates in autocomplete/label-history, given this materially affects UI scope? [Ambiguity — resolved, Spec §Clarifications Session 2026-07-09]

## Requirement Completeness — `growth_check` rename & migration

- [x] CHK011 Is the migration's data scope (which rows, which schemas) explicitly bounded rather than left to implementation judgment? [Completeness, Spec §FR-007]
- [x] CHK012 Is the payload-shape equivalence between `measurement` and `growth_check` stated as a hard requirement (not merely implied by "rename")? [Completeness, Spec §FR-009]
- [x] CHK013 Is the fate of in-flight/offline-queued requests still carrying the old wire value addressed, rather than left as an unstated assumption? [Completeness, Spec §Edge Cases]
- [x] CHK014 Is there a requirement covering what happens to test fixtures/documentation still referencing the old value, or is that left as an implementation-only concern? [Completeness, Spec §Assumptions]

## Requirement Clarity — rename & migration

- [x] CHK015 Is "migration-safe rename" defined concretely (add-new, backfill, remove-old) rather than left as a vague quality descriptor? [Clarity, Spec §Input]
- [x] CHK016 Is the deployment-ordering requirement (backfill before the cutover code ships) stated as an explicit, testable operational requirement rather than only appearing in a research note? [Clarity, Research §R2] — Resolved: plan.md's Constitution Check and the contract delta both restate it as an operational requirement, not research-only.
- [x] CHK017 Is "no dual-write compatibility window" precise enough to distinguish a hard cutover from a temporary deprecation period? [Clarity, Spec §FR-008]

## Consistency

- [x] CHK018 Do spec.md's Edge Cases and data-model.md's Migration section agree on what happens to a request still using the old wire value post-cutover? [Consistency, Spec §Edge Cases / Data-Model §Migration]
- [x] CHK019 Do FR-006/FR-007/FR-008/FR-009 (rename) and FR-001 through FR-005 (custom type) avoid overlapping or contradicting each other's validation-error handling expectations? [Consistency]

## Non-Functional / Operational

- [x] CHK020 Are rollback/failure-handling requirements defined for a partially-failed multi-tenant backfill (some tenants succeed, some fail)? [Gap → Resolved, Research §R1 / Contracts §backfill-growth-check] — the per-tenant exit-code/summary pattern (reused from `migrate-tenants`) makes a partial failure visible and re-runnable (idempotent `WHERE "EventType" = 'measurement'` predicate), but spec.md itself does not call this out as a functional requirement; acceptable since this is an operational/CLI-contract concern already covered by contracts/, consistent with how feature 002's `migrate-tenants` handles the same class of risk without a spec-level FR.
- [x] CHK021 Is success/failure of the migration objectively measurable (e.g., zero remaining rows with the old value) rather than only qualitatively described? [Measurability, Spec §SC-002]

## Dependencies & Assumptions

- [x] CHK022 Is the assumption that "no third-party API consumers exist" (justifying a hard cutover with no deprecation window) explicitly recorded rather than left implicit? [Assumption, Spec §Assumptions]
- [x] CHK023 Is the dependency on feature 009's existing validator/enum architecture (that a new switch arm is all that's needed, no new validation primitive) explicitly noted as an assumption rather than presented as a new discovery? [Assumption, Research §R3]

## Notes

- All items pass on review — the two genuinely open items going in (CHK010's autocomplete decision, CHK016's deployment-ordering visibility) were resolved before this checklist was written: CHK010 via the pre-specify clarification session, CHK016 by explicitly restating the ordering requirement in plan.md/contracts/ rather than leaving it research-only.
- CHK020 is flagged as an accepted, precedent-consistent gap rather than a blocking finding — see its note above.
