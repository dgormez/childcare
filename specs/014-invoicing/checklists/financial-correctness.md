# Financial Correctness Checklist: Invoicing

**Purpose**: Requirements-quality validation focused on money/cents correctness, billable-day
rule edge cases, OGM checksum completeness, invoice status-transition completeness, and
parent-visibility/security boundaries around draft vs. sent invoices.
**Created**: 2026-07-15
**Feature**: [spec.md](../spec.md)

## Money & Cents Correctness

- [X] CHK001 - Is every monetary field's unit (cents vs. major currency unit) explicitly stated,
  not just implied by field naming convention? [Clarity, Spec §FR-017, §Key Entities]
- [X] CHK002 - Is the rounding/truncation behavior specified for any computation that could
  produce a non-integer intermediate result (e.g. a future prorated or partial-day rate)?
  [Gap] — currently N/A since FR-002's rule is whole-day multiplication only; confirm no
  partial-day billing is silently assumed anywhere else in the spec.
- [X] CHK003 - Is it specified what happens if `TotalCents` (subtotal + extra charges) would
  overflow the stored integer type, or go negative (e.g. a negative extra-charge amount)?
  [Edge Case, Gap, Spec §FR-006]
- [X] CHK004 - Does the spec define whether an extra-charge amount can be zero or negative
  (e.g. a discount), or whether extra charges are additive-only? [Ambiguity, Spec §FR-006]
- [X] CHK005 - Is `SubtotalCents` vs. `TotalCents`'s exact relationship (whether extra charges
  are ever anything other than a pure addition) stated unambiguously? [Clarity, Spec §Key
  Entities]

## Billable-Day Rule Edge Cases

- [X] CHK006 - Does the spec define the billing treatment of a contracted day with no attendance
  record at all (neither present nor marked absent)? [Gap] — resolved in research.md but not
  spec.md itself; confirm whether this belongs in spec.md's Edge Cases for completeness.
- [X] CHK007 - Is the precedence explicit when a single day could theoretically satisfy more than
  one classification (e.g. a closure day where a caregiver also logged a check-in)? [Consistency,
  Spec §FR-002]
- [X] CHK008 - Does the spec define behavior when a child has two contracts at the same location
  with overlapping active date ranges within the same month (e.g. an amendment mid-month)?
  [Edge Case, Gap]
- [X] CHK009 - Is "unjustified absence" defined precisely enough to be objectively computed, or
  does it depend on a field whose own default/unset state is ambiguous (e.g. an absence marked
  with no justification decision made yet)? [Clarity, Spec §FR-002]
- [X] CHK010 - Does the spec state what happens if a location's published closure day is added
  or removed *after* invoices for that month were already generated (does it affect only future
  generation/regeneration, or retroactively)? [Edge Case, Gap]

## OGM Reference Completeness

- [X] CHK011 - Is the OGM reference's exact format (grouping, delimiters, digit count)
  unambiguous enough to be independently implemented by two different engineers identically?
  [Clarity, Spec §FR-004]
- [X] CHK012 - Does the spec define what happens to an invoice's OGM reference if generation is
  attempted more than once before the invoice is ever sent (does the reference get reassigned,
  or is it fixed at first creation)? [Ambiguity, Spec §FR-004, §FR-003]
- [X] CHK013 - Is the checksum edge case (remainder of zero) explicitly called out as a
  requirement, not just an implementation detail left to research/plan? [Completeness, Spec
  §Edge Cases]

## Invoice Status-Transition Completeness

- [X] CHK014 - Are all valid status transitions enumerated, and is it explicit that every
  transition NOT listed is implicitly forbidden (vs. merely unaddressed)? [Completeness, Spec
  §FR-007, §FR-009, §FR-011, §FR-012, §FR-013]
- [X] CHK015 - Does the spec define behavior for sending a batch where some invoices are eligible
  (`draft`) and others are not — is the entire batch rejected, or are eligible ones processed and
  ineligible ones reported separately? [Ambiguity, Spec §US2/AC3, §FR-007]
- [X] CHK016 - Is "overdue" precisely defined as a function of exactly which two fields, with no
  room for a third interpretation (e.g. does an overdue invoice that gets marked paid immediately
  stop being overdue, or retain some overdue marker)? [Clarity, Spec §FR-010]
- [X] CHK017 - Does the spec define what happens to `SentAt`/`DueDate` when a `sent` invoice is
  regenerated — are they preserved, or recomputed? [Gap, Spec §FR-011]
- [X] CHK018 - Is it specified whether marking an invoice paid is reversible (director error
  correction) or a one-way transition? [Gap, Spec §FR-009]

## Parent-Visibility & Security Boundaries

- [X] CHK019 - Is the draft-invisibility rule stated as applying uniformly to every parent-facing
  read path (list, detail, PDF download), or only to the list — could a `draft` invoice's PDF or
  detail be reachable by a parent who already knows/guesses its id? [Consistency, Spec §FR-008,
  §Security considerations]
- [X] CHK020 - Does the spec define the authorization boundary for a parent whose contract with a
  child has since ended (e.g. a departed child) — do their historical invoices remain visible to
  that parent, and for how long? [Gap, Edge Case]
- [X] CHK021 - Is it explicit whether a second parent/contact linked to the same child sees the
  same invoice list, or whether invoice visibility is scoped to only the contact who originally
  registered? [Ambiguity, Spec §FR-008, §FR-015]
- [X] CHK022 - Does the spec distinguish the response for "invoice doesn't exist" vs. "invoice
  exists but isn't visible to this parent" (information-leakage boundary), consistent with how
  other parent-scoped resources in this codebase behave? [Consistency, Gap]

## Acceptance Criteria Quality

- [X] CHK023 - Can SC-002's "100% of generated invoices have a checksum-valid, unique OGM
  reference" be objectively verified without needing to inspect the implementation, i.e. is a
  checksum-validation procedure implied clearly enough from the spec alone? [Measurability, Spec
  §SC-002]
- [X] CHK024 - Is SC-005's "byte-for-byte unchanged" claim measurable given that `UpdatedAt`
  could arguably tick even on a rejected/no-op regeneration attempt — does the spec clarify
  whether a rejected regeneration attempt is required to leave literally every field (including
  timestamps) untouched? [Ambiguity, Spec §SC-005, §FR-012]
