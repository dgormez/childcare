# Safety/Correctness Checklist: CODA/CODABOX Payment Matching

**Purpose**: Validate that spec.md's requirements around the invoice paid-once invariant,
encrypted PII handling, and dedupe correctness are complete, unambiguous, and consistent —
before implementation, not after.
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)

**Note**: This checklist tests the requirements themselves (are they complete/clear/consistent),
not the eventual implementation.

## Invoice Paid-Once Invariant

- [x] CHK001 Is the "an invoice can only be marked paid once" rule stated as a testable
      requirement rather than only implied by FR-004's happy path? [Clarity, Spec §FR-008]
- [x] CHK002 Are the requirements for an OGM match against an already-paid invoice (duplicate)
      and an amount/sender match against an already-paid invoice from an earlier period
      (closed-invoice) distinguishable by a reader with no access to this feature's internal
      design notes, or do they read as the same case twice? [Clarity, Spec §FR-008, §FR-009]
- [x] CHK003 Does the spec define what happens if two transactions in the *same* import both
      resolve to the same invoice (e.g., two OGM-matching lines for one invoice in one file)?
      [Gap, Coverage]
- [x] CHK004 Is it specified whether a confirmed suggested match (FR-006) and an exact reference
      match (FR-004) share the identical "already paid" guard, or could a race between the two
      leave an inconsistent outcome unaddressed? [Consistency, Spec §FR-004, §FR-006, §FR-008]

## Partial Payment Correctness

- [x] CHK005 Is "the invoice's outstanding total" in FR-010 defined precisely enough to compute
      (e.g., does it account for prior partial payments already recorded, not just the invoice's
      original total)? [Ambiguity, Spec §FR-010]
- [x] CHK006 Does the spec define what happens when a second partial payment, combined with the
      first, would exactly or over-complete the invoice — does the invoice become payable
      automatically, or does a partial payment never auto-close the invoice regardless of
      cumulative total? [Gap, Edge Case]
- [x] CHK007 Is "remaining balance" (User Story 3, Acceptance Scenario 2) specified as a value the
      director sees, with clear enough wording that an implementer knows it must reflect the
      cumulative received amount, not just the single latest transaction? [Clarity]

## Encrypted PII Handling

- [x] CHK008 Is the requirement that sender IBAN be "encrypted at rest" (FR-014) paired with a
      requirement covering how it may still be *displayed* to a director (e.g., masked/partial),
      or does the spec leave the display format entirely open? [Completeness, Spec §FR-014]
- [x] CHK009 Does the spec state what "access logged" (FR-014) means in observable terms — which
      actions count as access requiring a log entry — or is it left to implementation judgment
      with no acceptance criterion to verify against? [Measurability, Spec §FR-014]
- [x] CHK010 Is sender name (as opposed to sender IBAN) explicitly scoped as non-sensitive/not
      requiring encryption, or is this an unstated assumption a reader could reasonably disagree
      with? [Ambiguity, Spec Key Entities]

## Dedupe Correctness

- [x] CHK011 Is the re-import dedupe rule (FR-013, informed by the Clarifications session) precise
      enough to be independently implemented by two different engineers the same way — i.e., does
      it fully enumerate which fields must match, or could "matches on X, Y, Z" be read as
      OR instead of AND? [Clarity, Spec §FR-013]
- [x] CHK012 Does the spec address the case where two *genuinely distinct* real-world transactions
      happen to share identical value date, amount, sender, and communication (e.g., two
      identical-amount standing orders from the same account on the same day) — is this
      acknowledged as an accepted false-positive dedupe risk, or left unaddressed? [Gap, Edge Case]
- [x] CHK013 Is the "how many were skipped" reporting requirement (FR-013) specific about whether
      skipped counts are shown per-import or only as a running total, so success criteria can be
      objectively verified? [Measurability, Spec §FR-013]

## Unmatched/Review-Queue Completeness

- [x] CHK014 Does SC-004's "every transaction is accounted for" requirement have a precise enough
      acceptance test described (transaction-count parity) that "accounted for" can't be
      satisfied by a transaction silently vanishing into a category not covered by the parity
      check? [Measurability, Spec §SC-004]
- [x] CHK015 Is the reviewed/handled action (FR-012) specified as never affecting invoice state
      strongly enough to rule out an implementer accidentally wiring it to also touch
      `MatchedInvoiceId` or invoice status? [Clarity, Spec §FR-012]

## Notes

- All 15 items resolved by editing spec.md directly (2026-07-22), per this pipeline's standing
  rule that checklist findings are fixed, not deferred as debt: strengthened FR-008/FR-009's
  duplicate-vs-closed-invoice distinction, added the same-import double-match and cumulative-
  partial-payment-completes-the-invoice edge cases, tightened FR-006 to share FR-008's
  already-paid guard explicitly, redefined FR-010's "outstanding total" as cumulative (not just
  original total), added FR-014's masked-display/access-logging/sender-name-not-sensitive
  clauses, tightened FR-013's dedupe wording to explicit AND-of-all-fields and per-import
  reporting, added an accepted-false-positive-risk edge case for the dedupe rule, and extended
  SC-004's parity check to explicitly include the skipped-as-already-imported count.
