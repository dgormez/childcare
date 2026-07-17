# Requirements Quality & Constitution Compliance Checklist: Developmental Milestones

**Purpose**: Validate that spec.md/plan.md/tasks.md are complete, unambiguous, and consistent
before implementation, with explicit attention to this codebase's constitutional gates (tenant
isolation, i18n, immutability-by-design).
**Created**: 2026-07-16
**Feature**: [spec.md](../spec.md)

**Note**: Findings are advisory per the process-next-feature workflow — only a genuine spec
contradiction or missing functional requirement blocks progress.

## Requirement Completeness

- [x] CHK001 Are the 7 developmental domains and their identifying codes fully enumerated? [Completeness, Spec §FR-001]
- [x] CHK002 Is the append-only/no-edit constraint stated as a testable requirement rather than only a narrative note? [Completeness, Spec §FR-003]
- [x] CHK003 Is the age-band boundary inclusivity rule (both ends inclusive) explicitly documented rather than left implicit? [Completeness, Spec Assumptions]
- [x] CHK004 Are requirements defined for what happens when the reference catalog gains a new milestone after observations already exist? [Completeness, Spec Edge Cases]
- [x] CHK005 Are requirements defined for the empty-portfolio state on all three surfaces (caregiver, director, parent)? [Completeness, Spec UX Requirements]

## Requirement Clarity

- [x] CHK006 Is "current focus" / age-appropriate highlighting defined with a precise, computable rule rather than a vague adjective? [Clarity, Spec §FR-004]
- [x] CHK007 Is "warm, plain language" for the parent-facing portfolio given a concrete contrast (vs. clinical/database phrasing) rather than left subjective? [Clarity, Spec UX Requirements]
- [x] CHK008 Is the status vocabulary (`emerging`/`achieved`/`not_yet`) closed and unambiguous, with no room for a caregiver-supplied free-text status? [Clarity, Spec §FR-002/FR-012]

## Requirement Consistency

- [x] CHK009 Does the reference-catalog storage decision (public schema) stated in the Technical Requirements section agree with the same decision in Assumptions and in plan.md's Constitution Check? [Consistency, Spec Technical Requirements ↔ Assumptions ↔ plan.md]
- [x] CHK010 Do the director-view and parent-view requirements agree on what data each is allowed to see (full history vs. current-status-only)? [Consistency, Spec §FR-005/FR-006, data-model.md Derived View]
- [x] CHK011 Does the caregiver-recording authorization requirement stay consistent with the precedent cited (child-events device policy) across spec.md, plan.md, and contracts/? [Consistency, Spec §FR-010, research.md R6]

## Acceptance Criteria Quality

- [x] CHK012 Can SC-001's "3 taps or fewer" claim be objectively verified against the described main flow's step count? [Measurability, Spec §SC-001]
- [x] CHK013 Is SC-004 ("100% of recorded observations remain present and unaltered") independently verifiable without relying on implementation internals? [Measurability, Spec §SC-004]
- [x] CHK014 Is SC-005 ("zero cross-tenant or cross-family exposure") paired with a concrete negative test scenario elsewhere in the spec? [Traceability, Spec §SC-005 ↔ Edge Cases]

## Scenario Coverage

- [x] CHK015 Are regression scenarios (status moving from `achieved` back to `not_yet`) covered for both the recording flow and the portfolio "current status" resolution? [Coverage, Spec User Story 1 AC2, Edge Cases]
- [x] CHK016 Are unauthorized-access scenarios (parent viewing another family's child) covered for both the JSON portfolio endpoint and the PDF export endpoint? [Coverage, Spec User Story 3 AC2, tasks.md T047]
- [x] CHK017 Is the zero-observation ("nothing recorded yet") scenario covered separately for caregiver, director, and parent surfaces, given each has different empty-state copy? [Coverage, Spec UX Requirements]

## Non-Functional Requirements

- [x] CHK018 Are accessibility requirements (icon+color pairing for status, 48pt touch targets) specified for the new caregiver-tablet and director-web UI, not just referenced generically? [Coverage, Spec UX Requirements Accessibility]
- [x] CHK019 Is offline behavior for observation recording specified with a concrete precedent (existing child-event offline queue) rather than left to implementation discretion? [Clarity, Spec UX Requirements Offline behavior]
- [x] CHK020 Is the performance expectation for portfolio reads tied to a concrete mechanism (indexing) rather than an unquantified "must be fast"? [Measurability, Spec Technical Requirements Performance]

## Constitution Compliance

- [x] CHK021 Does the plan's Constitution Check justify why the shared-catalog (public-schema) design does not violate Principle I's tenant-isolation gate, rather than asserting compliance without reasoning? [Consistency, plan.md Constitution Check]
- [x] CHK022 Is the i18n requirement (NL/FR/EN for both catalog content and UI strings) traceable to a specific functional requirement rather than only the general project-wide rule? [Traceability, Spec §FR-009]
- [x] CHK023 Is the "no update/delete path" immutability requirement specified strongly enough to be checkable as a structural property (research.md R3) rather than only a policy convention that could be bypassed? [Measurability, Spec §FR-003, research.md R3]

## Dependencies & Assumptions

- [x] CHK024 Is the assumption that "observed_by" needs no PIN-reconfirmation (unlike medication/temperature events) explicitly justified against the precedent it diverges from? [Assumption, Spec Assumptions]
- [x] CHK025 Is the decision to exclude platform-admin catalog-editing UI from this feature's scope explicitly justified rather than silently omitted? [Assumption, Spec Assumptions §FR-011]
- [x] CHK026 Is the on-demand (unstored) PDF rendering decision explicitly justified against the alternative (stored, like fiscal attestations) with a stated reason for the divergence? [Assumption, research.md R4]

## Notes

- All items reviewed against the current spec.md/plan.md/research.md/data-model.md/tasks.md and
  pass — no gaps found requiring a spec revision. Items are left as checked, standing
  documentation of what was verified, per this repo's established checklist convention (see
  015's own checklist history).
- No [NEEDS CLARIFICATION] markers or unresolved ambiguities remain.
