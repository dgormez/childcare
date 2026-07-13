# Safety, Tenant-Isolation & Data-Integrity Checklist: Vaccine Catalog & Attachments

**Purpose**: Validate requirements quality for the three highest-risk dimensions of this
feature: regulatory-adjacent correctness (a Belgian legal vaccination record), tenant-isolation
correctness (the first shared, non-tenant-scoped catalog table), and data-integrity edge cases
(catalog/custom-entry mutual exclusivity, soft-delete rendering).
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md)

**Depth**: Standard. **Audience**: Reviewer (this feature's own implementer, acting as reviewer
per the standing single-pass process). **Focus**: safety/regulatory, tenant isolation, data
integrity — UI polish and performance are out of scope for this checklist.

## Regulatory / Safety-Adjacent Correctness

- [x] CHK001 Does the spec state what must happen to a vaccine record's legally-relevant fields (`vaccineName`, `administeredOn`) when the picker/attachment features fail, so the underlying Vaccinatieboekje record-keeping obligation is never silently degraded? [Completeness, Spec §FR-005, FR-012]
- [x] CHK002 Is it explicit that a catalog-entry rename or deactivation must never alter the legally-relevant content of an already-saved record? [Clarity, Spec Edge Cases]
- [x] CHK003 Are the requirements for "record still displays correctly after its catalog reference is deactivated" specific enough to be objectively verified (exact fields, exact behavior), rather than a general "must not break"? [Measurability, Spec §FR-010]
- [x] CHK004 Does the spec define whether an attachment is ever treated as authoritative over the typed record fields, or explicitly only a fallback — avoiding an ambiguity where two sources of truth could silently disagree? [Clarity, Spec Summary/US3]

## Tenant Isolation

- [x] CHK005 Does the spec explicitly state that the shared catalog contains no tenant-identifying or personal data, which is the reasoning basis for exempting it from tenant-schema placement? [Completeness, Spec Assumptions]
- [x] CHK006 Are the boundaries between "shared, cross-tenant" data (the catalog) and "tenant-scoped" data (remembered custom entries) stated unambiguously enough that a reader could not confuse which table a new requirement belongs to? [Clarity, Spec §FR-001, FR-006]
- [x] CHK007 Is there a requirement explicitly forbidding any director-facing read or write path from ever returning another tenant's custom entries? [Coverage, Spec §FR-008, Acceptance Scenario US2.4]
- [x] CHK008 Does the spec address what a caregiver (a role explicitly scoped to one tenant/location) is and isn't shown from the shared catalog, so no requirement implies the catalog is tenant-filtered when it should not be? [Consistency, Spec §FR-014]
- [x] CHK009 Is the platform-operator-only write access to the canonical catalog stated as a hard requirement (not just an assumption), so a future contributor cannot read the spec and reasonably conclude director write-access was merely deferred rather than disallowed in this feature? [Ambiguity, Spec §FR-009, Assumptions]

## Data-Integrity Edge Cases

- [x] CHK010 Does the spec define the resolution rule when a director both picks a catalog entry and edits the text away from that entry's name — is the record's catalog reference kept, cleared, or is this left ambiguous? [Clarity, Spec §FR-004]
- [x] CHK011 Is the mutual-exclusivity rule between a catalog-entry reference and a custom-entry reference stated as an explicit requirement (not just implied by the narrative), so an implementer cannot reasonably build a record that carries both or neither without contradicting a stated rule? [Gap, Spec §FR-004, FR-006]
- [x] CHK012 Are the case/whitespace-insensitivity rules for custom-entry deduplication precise enough to be independently implemented consistently (e.g. does "insensitive" cover diacritics like "Rabiës" vs "Rabies", or only case/whitespace)? [Ambiguity, Spec §FR-007, Acceptance Scenario US2.3]
- [x] CHK013 Does the spec define behavior for the concurrent-write race case (two directors typing near-duplicate names at the same moment), not just the sequential case? [Edge Case, Spec Edge Cases]
- [x] CHK014 Is there a requirement covering what happens if the catalog is completely empty or fails to load, distinct from "an individual entry is deactivated"? [Coverage, Spec Edge Cases]
- [x] CHK015 Does the spec define what "attachment upload failure never blocks the record" means precisely — e.g. is a record ever left in a state where its attachment reference points at a nonexistent object, and if so is that explicitly acceptable? [Clarity, Spec §FR-012, Acceptance Scenario US3.2]

## Notes

- All 15 items were checked against the current spec.md during this same pass (not deferred) —
  see the per-item resolution below. Per the standing process rule, every finding is fixed in
  the spec now rather than logged as unaddressed debt.
- **Findings and fixes applied**: CHK003, CHK009, CHK010, CHK011, CHK012, and CHK015 surfaced
  genuine gaps in spec.md's precision; each was resolved by tightening the relevant FR/Edge Case
  wording in spec.md directly (see spec.md's updated FR-004, FR-007, FR-009, FR-010, FR-012, and
  Edge Cases). CHK001, CHK002, CHK004-CHK008, CHK013, CHK014 were already adequately covered and
  required no change.
