# Safety, Security & Regulatory-Accuracy Checklist: Staff HR Dossier & Time Registration

**Purpose**: Validate that spec.md's requirements are complete, unambiguous, and consistent
enough to safely implement — focused on subsidy-integrity risk (medewerkersbeleid hours are
money-linked), staff PII handling, and regulatory-category accuracy. Not a test plan; this
checks the requirements themselves, not the implementation.

**Created**: 2026-07-23
**Feature**: [spec.md](../spec.md)

## Requirement Completeness — Data Integrity of Subsidy-Relevant Hours

- [x] CHK001 Are requirements defined for validating that a staff member clocking in has an
      eligibility grant (`StaffLocationEligibility`) for the target location, the same way every
      other write path in this codebase enforces it (feature 009's device-location match,
      feature 012's converge-caught gap)? [Gap, Spec §FR-001]
- [x] CHK002 Are requirements defined for rejecting a clock-in `function` value that isn't one of
      the staff member's own configured `TimeEntryFunctions`, rather than trusting any of the
      three enum values from the request? [Gap, Spec §FR-004/FR-005]
- [x] CHK003 Does the director-correction requirement (FR-008) constrain a corrected `function`
      value to the staff member's configured `TimeEntryFunctions`, consistent with CHK002's
      clock-in constraint? [Consistency, Gap, Spec §FR-008]

## Requirement Completeness — Audit Trail for Integrity-Sensitive Actions

- [x] CHK004 Are requirements defined for recording who performed an unlock/re-lock action on a
      time entry and when, given that unlocking bypasses the immutability control the subsidy
      report's data integrity depends on? [Gap, Spec §FR-007]
- [x] CHK005 Are requirements defined for recording who uploaded or deleted an HR document,
      given the sensitivity of employment-contract data? [Gap, Spec §FR-011/FR-012]

## Requirement Clarity — Regulatory-Category Accuracy

- [x] CHK006 Is the source of the three medewerkersbeleid function categories
      (`kinderbegeleider`/`logistiek`/`verantwoordelijke`) and their subsidy relevance documented
      or flagged as unverified backlog input, consistent with how this pipeline treats
      regulatory facts for non-cited features (distinct from 015/019/033–041's verified-contract
      requirement)? [Ambiguity, Spec §Product Context/Technical Requirements]

## Requirement Completeness — Data Retention & Offboarding

- [x] CHK007 Does the spec state what happens to a staff member's time entries and HR documents
      after they are deactivated/offboarded — retained indefinitely, or deferred to feature
      038's not-yet-built retention lifecycle? [Gap, Spec §Assumptions]
- [x] CHK008 Is the interaction between staff deactivation and an open time entry's eventual
      correction workflow documented consistently between the Edge Cases section and any
      retention assumption? [Consistency, Spec §Edge Cases]

## Non-Functional Requirements — Access Control Consistency

- [x] CHK009 Are director-only access requirements for the dossier (FR-013) and for the
      subsidy report consistently stated using the same access-control terminology used
      elsewhere in this spec (`DirectorOnly`), with no implicit staff-readable path left
      ambiguous? [Consistency, Spec §FR-013/FR-016]

## Notes

- All items resolved by editing spec.md directly (new FRs for location/function-eligibility
  validation and audit logging, a new Assumption for retention/offboarding scope, and a
  clarifying note on the function-category source) rather than left as findings — per this
  pipeline's standing rule to fix every checklist finding, including advisory ones.
