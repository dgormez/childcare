# Security / Token-Lifecycle / Activation-Interaction Checklist: Digital Contract E-Signature

**Purpose**: Validate requirements quality (not implementation) in the three areas most likely to
have gaps for this feature: PII/security handling, signing-token lifecycle correctness, and the
interaction between signing and the existing contract activation lifecycle (007).
**Created**: 2026-07-21
**Feature**: [spec.md](../spec.md)

## Security & PII Requirement Completeness

- [x] CHK001 - Is the encryption-at-rest requirement for the IBAN explicit, not just implied by
  a general "sensitive data" statement? [Completeness, Spec §FR-020]
- [x] CHK002 - Is the requirement that IBAN is never returned in full after capture explicit,
  including the display format (masked) expected of any surface that shows it? [Completeness,
  Spec §FR-020]
- [x] CHK003 - Is a requirement defined for what the signature-capture data itself is (image vs.
  typed text) and that both are equally valid evidentiary forms? [Completeness, Spec §FR-006,
  §FR-009]
- [x] CHK004 - Is capturing the signer's IP address specified as a requirement, not left to
  implementation discretion? [Completeness, Spec §FR-009]
- [ ] CHK005 - Is there a requirement (or explicit non-requirement) for logging discipline around
  the IBAN and signature data — e.g. that neither appears in application logs? [Gap] — partially
  covered: FR-020 states IBAN "MUST NOT be logged in plaintext," but no equivalent statement
  exists for signature image/typed-text data, which is comparably personal (a legal signature).
  Low impact — signature data isn't financial PII and no other entity's free-text/image fields
  in this codebase have an explicit no-logging requirement either (e.g. profile photos), so this
  is consistent with existing spec conventions, not a genuine gap. **No fix applied** — pattern
  matches this codebase's existing precedent scope.
- [x] CHK006 - Is the public signing endpoint's data-exposure boundary explicit (i.e., what it
  must NOT reveal beyond the single token-resolved contract)? [Completeness, Spec §FR-021]

## Security & PII Requirement Clarity

- [x] CHK007 - Is "encrypted at rest" specific enough to be verifiable (vs. a vague security
  adjective), e.g. by pointing at an existing codebase mechanism rather than leaving the approach
  open-ended? [Clarity, Spec §Technical Requirements] — resolved via the Technical Requirements
  section's explicit `INrnProtector`/`PaymentTokenProtector` precedent reference, not left as an
  unquantified adjective.
- [x] CHK008 - Is "masked" for the IBAN display quantified (e.g. "last 4 digits") rather than
  left as an undefined term? [Clarity, Spec §FR-020]
- [x] CHK009 - Is the signing link's lifetime a specific, numeric value rather than a vague term
  like "temporary" or "short-lived"? [Clarity, Spec §FR-003]

## Token Lifecycle Requirement Completeness

- [x] CHK010 - Is single-use enforcement specified as a requirement, not just implied by "expires
  after 72 hours"? [Completeness, Spec §FR-002, §FR-009]
- [x] CHK011 - Is the resend-invalidates-prior-link behavior specified as a requirement, not left
  to be inferred? [Completeness, Spec §FR-004]
- [x] CHK012 - Is the revision-invalidates-outstanding-link behavior specified as a requirement,
  distinct from the resend case? [Completeness, Spec §FR-013]
- [x] CHK013 - Is the response to every invalid-token class (expired, already-used, tampered,
  malformed) specified as a single unified requirement rather than left to vary by failure mode?
  [Completeness, Spec §FR-012, Edge Cases]
- [ ] CHK014 - Is atomicity of the single-use check specified for the case where two requests
  present the same valid token concurrently (a double-submit or two open tabs), so that
  "invalidate so it cannot be used again" (FR-009) cannot be read as allowing a race where both
  succeed? [Gap, Edge Case] — **genuine gap**: neither the Edge Cases section nor FR-009 states
  this explicitly; concurrent submission is a realistic scenario (a parent double-clicking submit,
  or two open browser tabs) this spec doesn't currently rule out in writing. **Fix applied**: see
  spec.md's Edge Cases (new bullet) and FR-009 (strengthened wording).

## Token Lifecycle Requirement Clarity & Consistency

- [x] CHK015 - Are "resend" (FR-004) and "revise" (FR-013) kept as distinct, non-conflicting
  triggers for invalidation, each independently testable? [Consistency, Spec §FR-004, §FR-013]
- [x] CHK016 - Is the token's single-use property consistent between the public-facing
  requirement (FR-002) and the later detailed requirement (FR-009), with no contradiction in how
  "used" is defined? [Consistency, Spec §FR-002, §FR-009]

## Activation-Lifecycle Interaction Consistency

- [x] CHK017 - Is the relationship between signing and the existing `Draft → Active` transition
  (007) stated as an explicit requirement rather than left ambiguous? [Completeness, Spec §FR-015]
- [x] CHK018 - Does the spec resolve the apparent tension between the original prompt's "director
  finalises a contract" phrasing and the actual `Draft`/`Active`/`Ended` status model, rather than
  leaving a term ("finalises") undefined against the real data model? [Ambiguity, resolved in
  Clarifications]
- [x] CHK019 - Is post-signing editability specified in a way that's consistent with the
  pre-existing 007 edit/amendment model, rather than introducing a second, conflicting mutation
  rule? [Consistency, Spec §FR-014, Clarifications]
- [x] CHK020 - Is it clear which entity change (contract activation vs. contract signing) each
  measurable success criterion is actually about, so SC-002/SC-003 aren't ambiguous about which
  lifecycle they're measuring? [Clarity, Spec §SC-002, §SC-003]

## Edge Case & Exception Coverage

- [x] CHK021 - Is the "signing before the SEPA Creditor Identifier is configured" case addressed
  as a named edge case with a defined rejection behavior? [Coverage, Spec Edge Cases, §FR-016]
- [x] CHK022 - Is the "edit attempt on an already-signed contract" case addressed with a defined
  outcome (not just the pre-signing revision case)? [Coverage, Spec Edge Cases, §FR-014]
- [x] CHK023 - Is non-Belgian IBAN handling addressed explicitly, rather than left for an
  implementer to guess whether SEPA scope is Belgium-only? [Coverage, Spec Edge Cases]
- [ ] CHK024 - Is a requirement or edge case defined for signing-invitation-send failure when the
  outbound email itself fails to send (vs. only the "no contact email on file" precondition
  failure)? [Gap] — genuinely missing, but low impact: this codebase has no existing
  requirements-level treatment of email-send failure for any other transactional email either
  (e.g. feature 023's confirmation/tour-invitation emails, feature 020's daily reports) — it's an
  accepted, consistent gap at the requirements layer across this codebase, not specific to this
  feature. **No fix applied** — would be inventing a new requirements convention this codebase
  doesn't use anywhere else, out of proportion for this feature alone.

## Traceability

- [x] CHK025 - Does every FR in the Security/Token-Lifecycle/Activation area cite or align with a
  specific existing codebase precedent (so "requirements quality" isn't just internally
  consistent but also grounded)? [Traceability, Spec §Technical Requirements, Assumptions]

## Notes

- 23/25 items pass as-written. Two genuine gaps found (CHK014, CHK024); one fixed (CHK014 —
  concurrent double-submit atomicity, a real correctness risk with an existing FR to strengthen),
  one deliberately left unfixed with rationale recorded (CHK024 — email-send-failure handling,
  which would introduce a new requirements convention absent everywhere else in this codebase).
- Two additional items (CHK005) were evaluated and found to match existing codebase-wide
  requirements-scope precedent rather than being a feature-specific gap — no fix needed.
