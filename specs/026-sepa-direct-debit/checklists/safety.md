# Safety/Data-Integrity/Security Checklist: SEPA Direct Debit Batch Collection

**Purpose**: Validate that spec.md's requirements around batch generation atomicity, mandate
revocation correctness, encrypted IBAN handling, and pain.008 message-level integrity are
complete, unambiguous, and consistent — before implementation, not after. Same depth as feature
025's own safety checklist for the prior financial feature in this codebase.
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)

**Note**: This checklist tests the requirements themselves (are they complete/clear/consistent),
not the eventual implementation.

## Batch Generation Atomicity

- [x] CHK001 Does the spec require that invoice status transitions (Sent → PendingDebit) and the
      `SepaBatch` audit record be treated as a single all-or-nothing outcome, or could a reader
      implement them as separate steps that leave a partially-updated batch on a mid-generation
      failure? [Gap, Spec §FR-007]
- [x] CHK002 Is it specified what happens if the decrypted debtor IBAN for one invoice in a
      multi-invoice batch cannot be recovered (corrupted ciphertext, key-rotation mismatch) —
      does the whole batch fail, or is that one invoice silently dropped from the file? [Gap,
      Exception Flow]

## Concurrent Batch Generation

- [x] CHK003 Does the spec define the outcome when two batch-generation requests for overlapping
      invoice selections are submitted at nearly the same time, beyond the sequential-reads
      framing in Edge Cases — i.e., is a genuine race (both requests reading "eligible" before
      either writes `PendingDebit`) explicitly ruled out, or only the already-sequential case?
      [Ambiguity, Spec Edge Cases]

## Mandate Revocation Correctness

- [x] CHK004 Does the spec state whether revoking a mandate (FR-011) has any effect on an invoice
      that is already `PendingDebit` from a batch generated before the revocation, or is silence
      here meant to be read as "no effect, that batch already left the system"? [Ambiguity, Spec
      §FR-011]
- [x] CHK005 Is "every current and future invoice... ineligible" (FR-011) precise enough to rule
      out a reader interpreting "current" as also retroactively pulling an already-`PendingDebit`
      invoice back out of an in-flight batch? [Clarity, Spec §FR-011]

## pain.008 Message-Level Integrity

- [x] CHK006 Beyond XSD structural schema-validity (FR-006), does the spec require the generated
      message's own declared control totals (number of transactions, control sum) to match the
      actual set of instructions included — a business-rule consistency check the XSD itself
      cannot enforce? [Gap, Spec §FR-006]
- [x] CHK007 Does the spec require that the batch total shown to the director (FR-008's history
      list) and the amount actually encoded in the XML's control sum can never diverge? [Gap,
      Consistency]

## Sequence Type (FRST/RCUR) Correctness

- [x] CHK008 Is "no earlier batch has ever successfully included an invoice under the contract's
      current SEPA mandate reference" (FR-002a) specific enough about what "successfully
      included" means — does a batch that was later returned (FR-010) still count as a prior
      successful inclusion for sequence-type purposes, or does spec leave this ambiguous?
      [Ambiguity, Spec §FR-002a, §FR-010]

## Notes

- All 8 items resolved by editing spec.md directly (2026-07-22), per this pipeline's standing
  rule that checklist findings are fixed, not deferred as debt.
