# Specification Quality Checklist: Fiscal Attestations — Safety, Compliance & Data Integrity

**Purpose**: Validate that spec.md's requirements around PII handling (NRN/SSIN), regulatory
correctness (Belcotax 281.86), multi-location attribution, and period-aggregation accuracy are
complete, unambiguous, and objectively verifiable — before implementation.
**Created**: 2026-07-16
**Feature**: [spec.md](../spec.md)
**Depth**: Standard | **Audience**: Reviewer (pre-implementation gate)

## PII / NRN Handling

- [x] CHK001 - Is the NRN/SSIN non-storage rule stated as an absolute constraint covering every
  layer (database, logs, intermediate structures), not just "the database"? [Clarity, Spec
  §FR-007]
- [x] CHK002 - Does the spec distinguish between "NRN is never collected" and "NRN is collected
  but never persisted"? [Clarity, Spec §Out of Scope, FR-015] — resolved: explicitly never
  collected at all, not collected-then-discarded.
- [x] CHK003 - Is there a requirement (not just an assumption) that verifies NRN absence is
  actively checked, rather than merely asserted by omission? [Completeness, Spec §Technical
  Requirements] — resolved: Technical Requirements' Testing requirements bullet requires "an
  assertion proving NRN is never persisted."
- [x] CHK004 - Are requirements defined for what happens if a future integration (e.g. feature
  019's electronic submission) needs the NRN — is the boundary between "this feature" and "that
  future feature" unambiguous enough to prevent scope creep during this feature's implementation?
  [Boundary, Spec §Out of Scope, Assumptions]

## Regulatory Correctness (Belcotax 281.86 / Opgroeien)

- [x] CHK005 - Are all mandatory PDF fields for the official attest individually enumerated
  (not summarized as "required fields"), so each is independently verifiable? [Completeness,
  Spec §FR-006]
- [x] CHK006 - Is the source of the "certification type code 1" and "official declaration
  wording" requirements traceable to a cited authority, distinguishing a verified legal fact from
  an assumed one? [Traceability, Spec §FR-006, Assumptions]
- [x] CHK007 - Is the boundary between this feature's MVP (manual Belcotax-on-web entry) and the
  deferred automated submission (feature 019 item 5) stated precisely enough that an implementer
  can't accidentally build submission logic in scope creep? [Clarity, Spec §Out of Scope]

## Multi-Location Attribution

- [x] CHK008 - Is the rule for splitting a transferred child's attestation per location stated
  with a concrete trigger condition (which field/relationship determines "different location"),
  not just a narrative example? [Clarity, Spec §Edge Cases, FR-005]
- [x] CHK009 - Does the spec specify which location-identifying fields (name/address/KBO/
  erkenningsnummer) must be location-specific vs. organisation-wide on a per-location
  attestation, avoiding an implementer defaulting all fields to a single "current" location?
  [Completeness, Spec §FR-005, FR-006]
- [x] CHK010 - Is there a requirement covering the total-amount invariant across a
  multi-location split (each location's attestation sums only its own paid invoices, and the
  sum across all of a child's attestations for the year equals their total paid amount)?
  [Completeness, Gap] — resolved: FR-005 plus the Edge Cases entry both state per-location
  scoping; success criteria don't separately assert the cross-attestation sum invariant, which is
  implied but not independently measurable — left as an implementation-level invariant validated
  by tests (T022), not a missing product requirement, since spec.md's SC section is meant to be
  technology-agnostic and this is a data-correctness detail already covered by FR-005.

## Period-Aggregation Accuracy

- [x] CHK011 - Is "up to four periods" accompanied by a fully specified overflow rule (what
  happens on a fifth genuine rate change), rather than leaving that scenario unaddressed?
  [Completeness, Spec §Edge Cases, FR-004]
- [x] CHK012 - Is the overflow/consolidation rule's effect on accuracy (total amount) versus
  precision (per-period breakdown) explicitly distinguished, so a reviewer can confirm totals
  are never approximated even when the breakdown is coarser? [Clarity, Spec §Edge Cases]
- [x] CHK013 - Is the amount computation basis (paid invoice totals, not days × rate) stated
  explicitly enough to prevent an implementer from recomputing from day-count × rate and silently
  dropping extra charges? [Clarity, Spec §FR-002] — resolved: FR-002 states amounts come
  exclusively from `Paid` invoices; the days×rate vs. actual-total distinction is a plan-level
  design detail (research.md R3), appropriately not spec-level per the template's
  technology-agnostic requirement.
- [x] CHK014 - Is there a requirement for what a period's day count represents (calendar days
  vs. billable/attendance days), given the domain has multiple day-count concepts (present days,
  unjustified-absent days, closure-excluded days) that could otherwise be conflated? [Ambiguity,
  Gap] — resolved during this checklist pass: spec.md's FR-004 says "day count" without
  specifying which of 014's day categories it means. See Findings below — this is the one real
  gap this checklist surfaced.

## Data Integrity — Correction / Regeneration

- [x] CHK015 - Is "regenerate replaces in place" specified precisely enough to distinguish it
  from "regenerate creates a new versioned record," given the document is used for a legal
  filing where version history could matter? [Clarity, Spec §Assumptions, FR-008]
- [x] CHK016 - Does the spec address whether a parent who already downloaded a now-superseded
  attestation is informed of the correction, or only that the corrected version becomes
  available on next access? [Coverage, Spec §User Story 3 Acceptance Scenario 3] — resolved:
  acceptance scenario 3 covers "sees the corrected version," FR-016 covers the regeneration
  notification — together these address it.
- [x] CHK017 - Is there a requirement preventing a bulk-generation re-run from undoing a
  director's deliberate correction, and is its precedence over a routine re-run unambiguous?
  [Consistency, Spec §FR-009, Edge Cases]

## Success Criteria Measurability

- [x] CHK018 - Can SC-005 ("zero pre-filled NRN/SSIN") be objectively verified without reference
  to implementation, i.e. is it framed as an outcome inspectable from the artifact (the PDF
  itself) rather than a code-level claim? [Measurability, Spec §SC-005]
- [x] CHK019 - Can SC-002 ("100% of attestations... show an accurate multi-period breakdown")
  be verified against a concrete oracle (the child's actual contract-rate history), or does it
  rely on a subjective notion of "accurate"? [Measurability, Spec §SC-002]

## Notes

**One real gap found and fixed (CHK014)**: spec.md's FR-004 said each period has a "day count"
without specifying which of 014's several day-count concepts (present days, unjustified-absent
days, closure-excluded days) it means — genuinely ambiguous for an implementer, since 014
established multiple distinct day categories on the same invoice. Fixed directly in spec.md
(FR-004 now specifies "billable days — present plus unjustified-absent, matching the day count
`Invoice.SubtotalCents` was itself derived from") rather than left as spec debt, per this loop's
standing rule to fix every checklist finding, not just log it.

All other items were verified against the existing spec text and found already adequately
specified — no further changes needed.
