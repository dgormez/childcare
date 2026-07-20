# Specification Quality Checklist: Safety, Compliance & Data-Integrity

**Purpose**: Validate that spec.md's requirements around the identity-verification audit trail
and the encrypted NRN are complete, unambiguous, and safe to implement — a requirements-quality
pass, not an implementation test.
**Created**: 2026-07-20
**Feature**: [spec.md](../spec.md)
**Depth**: Standard/thorough, matching features 013g/013h's safety-focused checklist passes.

## Requirement Completeness

- [x] CHK001 - Does the spec define read-access scope for verification data (document type,
      note, NRN's last-4) — specifically whether caregiver-tablet/device-token reads of the
      shared child response should see it, or only director-web? [Gap, Spec §FR-014]
- [x] CHK002 - Does the spec define what happens to recorded verification data and the NRN when
      a child record itself is deleted/purged (vs. deactivated), given an existing GDPR-purge
      flow already exists for photos (feature 031)? [Gap, Coverage]
- [x] CHK003 - Does the spec constrain the free-text note field against holding the very data
      the NRN field exists to protect (e.g., a director pasting a full NRN into
      `id_document_note` instead of the dedicated encrypted field)? [Gap, Spec §FR-001, §FR-011]
- [x] CHK004 - Is a maximum length specified for the identity-verification note field anywhere in
      spec.md itself (not just left to the implementation plan)? [Gap, Spec §FR-001]
- [x] CHK005 - Does the spec state whether a deactivated/departed child's record can still be
      verified (e.g., closing out a dossier retroactively), or whether verification is blocked
      once deactivated? [Gap, Edge Cases]
- [x] CHK006 - Does the spec state whether a contact must be linked to at least one child to be
      verified, or whether verification is available on any contact record regardless of link
      state? [Gap, Spec §FR-002]

## Requirement Clarity

- [x] CHK007 - Is "the expected Belgian national register number format" in FR-010 defined
      precisely enough to implement without re-deriving it (digit count, accepted separator
      characters, checksum scope) inside the requirement itself, rather than only in the
      Assumptions section? [Clarity, Spec §FR-010]
- [x] CHK008 - Is "any application log at any level" in FR-011 specific enough to be
      unambiguous about scope (structured application logs, exception/stack traces, HTTP
      request/response logging middleware, error-reporting integrations)? [Clarity, Spec §FR-011]
- [x] CHK009 - Is "a short free-text note" (FR-001/FR-002) quantified, or does it rely on the
      reader inferring a bound? [Clarity, Spec §FR-001]

## Requirement Consistency

- [x] CHK010 - Are the Contact-side requirements (FR-002, FR-005, FR-006) consistent with the
      Child-side ones in every respect except the NRN, which Key Entities explicitly scopes to
      Child only? [Consistency, Spec §Key Entities]
- [x] CHK011 - Do FR-006/FR-006a's "first vs. most-recent" attribution model and User Story 3's
      acceptance scenarios describe the same behavior after the Clarifications-session
      supersession, with no leftover reference to the earlier "expandable history list" concept?
      [Consistency, Spec §Clarifications, §FR-006a]

## Acceptance Criteria Quality

- [x] CHK012 - Is SC-002 ("100% of verified records show both...attribution") independently
      verifiable without inspecting implementation internals — i.e., verifiable purely from what
      the UI/API surfaces? [Measurability, Spec §SC-002]
- [x] CHK013 - Is SC-003's "always matches the actual count...with no manual refresh" testable
      as stated, given no explicit staleness/consistency bound (e.g., must reflect a verification
      made 1 second ago on the very next view)? [Measurability, Spec §SC-003]

## Scenario Coverage

- [x] CHK014 - Are concurrent-correction scenarios addressed — two directors submitting a
      correction to the same child/contact's verification at nearly the same time — or is
      last-write-wins an implicit, undocumented assumption? [Gap, Exception Flow]
- [x] CHK015 - Are recovery/rollback requirements addressed for a failed NRN save (e.g., format
      passes client-side but the encryption step fails server-side) — must no partial state be
      persisted? [Gap, Recovery Flow, Spec §FR-010]
- [x] CHK016 - Does the spec address the interaction between User Story 4's per-child badge
      (FR-007a) and a location-filtered admin-home view — is the unverified count/badge scoped
      per-location or always organisation-wide? [Gap, Spec §FR-007, §FR-007a]

## Edge Case Coverage

- [x] CHK017 - Is the behavior specified for an NRN that passes structural format validation but
      is copy-pasted with an obviously invalid embedded date (e.g., month 13) — accepted per the
      Assumptions' "structural check only" decision, or is that inconsistent with implying a
      "valid" NRN was recorded? [Ambiguity, Spec §Assumptions]
- [x] CHK018 - Are the 5 document-type values (birth certificate, Kids-ID, eID, passport, other)
      confirmed as exhaustive, with "other" as the only escape hatch, and is that consistent with
      every acceptance scenario that references a document type? [Consistency, Spec §FR-001]

## Non-Functional Requirements

- [x] CHK019 - Are authorization requirements for *reading* verification/NRN data specified with
      the same rigor as the write-side restriction in FR-014 (which only covers
      recording/updating)? [Gap, Spec §FR-014]
- [x] CHK020 - Are data-retention requirements for verification records and the NRN referenced
      against this codebase's broader retention posture (BACKLOG feature 038), or left entirely
      unaddressed as out of this feature's scope? [Gap, Dependency]

## Dependencies & Assumptions

- [x] CHK021 - Is the Assumptions section's claim that "the field's actual regulatory use is
      deferred to...015/Belcotax 281.86" validated against that feature's actual current scope,
      or is it an unverified forward reference? [Assumption, Spec §Assumptions]
- [x] CHK022 - Is the dependency on this codebase's existing Director/Staff/Parent role model
      (rather than a new org-owner tier) documented clearly enough that a future reader
      understands why FR-014 doesn't restrict corrections further? [Traceability, Spec
      §Assumptions]

## Resolution notes (2026-07-20)

All 22 items resolved by editing spec.md directly (standing pipeline rule — no LOW-severity item
left as debt):

- **CHK001/CHK019** (the one architecturally significant finding): added **FR-015**, a new
  read-side restriction — verification/NRN fields must not appear in caregiver-tablet/Staff reads
  of the shared child/contact response, only Director reads. This will require plan.md/data-model.md/
  tasks.md's `ChildMapper`/`ContactMapper`/query-handler tasks to branch on caller role before
  projecting these fields — flagged for a follow-up plan/tasks revision.
- **CHK002/CHK020**: added an Assumptions bullet — verification/NRN data has no special-case
  retention/deletion path; it rides along with the rest of the `Child`/`Contact` record and BACKLOG
  038 governs it like every other field, same as everything else this feature doesn't special-case.
- **CHK003**: added an Assumptions bullet documenting the note-field NRN-leakage risk as an
  accepted, UI-copy-mitigated (not technically enforced) risk, with reasoning for why
  pattern-matching free text was rejected.
- **CHK004/CHK009**: FR-001 now states the note's max length (500 chars) inline instead of only
  in the plan.
- **CHK005**: added an Edge Case — verification remains available on deactivated children.
- **CHK006**: added an Edge Case — an unlinked contact can still be verified.
- **CHK007**: FR-010 now states the structural check inline (11 digits after stripping
  separators) instead of only in Assumptions.
- **CHK008**: FR-011 now enumerates log scope explicitly (structured logs, traces, HTTP
  request/response logging middleware).
- **CHK010, CHK011, CHK012, CHK018, CHK022**: reviewed against the current spec text — already
  consistent/clear/measurable, no change needed.
- **CHK013**: reviewed — SC-003's "no manual refresh" wording already implies live-query
  semantics; a numeric staleness bound would over-specify an always-live DB read for no benefit.
  No change needed.
- **CHK014**: added an Edge Case — last-write-wins on concurrent corrections, consistent with
  every other mutable field on `Child`/`Contact` having no optimistic concurrency control today.
- **CHK015**: reviewed — a failed encryption step throws before `SaveChangesAsync`, so no partial
  state can persist; this is standard transactional behavior already implied by FR-010's "MUST
  NOT be persisted" on any validation/save failure, not a gap needing new spec text.
- **CHK016**: FR-008 now states the admin-home count's location-scoping and confirms the
  per-child list badge is unaffected by it (the child list has no location filter).
- **CHK017**: broadened the relevant Assumptions bullet to explicitly cover implausible
  structurally-valid dates (e.g., month 13), not just the checksum omission.
- **CHK021**: corrected a factual error the original Assumptions bullet carried over from the
  backlog note — feature 015 already shipped as a parent-facing PDF certificate with no NRN
  dependency in what it actually built; the real future consumer is feature 019
  (`019-ikt-compliance`, not started) or a later Belcotax-on-web filing feature, per
  `workflows.md`'s Government Reporting workflow mapping.
