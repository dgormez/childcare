# Feature Specification: CODA/CODABOX Payment Matching

**Feature Branch**: `025-coda-payment-matching`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "Import bank statements in CODA format and automatically match payments to open invoices. Directors currently do this manually in Excel or their bank's online portal — this feature saves significant monthly admin time."

## Clarifications

### Session 2026-07-22

- Q: FR-013 requires re-imports to skip already-recorded transactions rather than duplicating
  them, but a CODA file doesn't carry a single universally-reliable transaction ID across every
  Belgian bank's export. What determines whether an imported transaction is "the same" as one
  already on file? → A: A transaction is considered already-imported when an existing row for
  the same tenant matches on value date, amount, sender IBAN, and communication text together
  (a composite natural key) — self-answered per this pipeline's standing rule to pick the
  recommended default when one exists and no genuinely novel scope question is raised. This is
  the same class of decision as feature 009a's raw-per-tenant dedupe logic: a deterministic,
  content-based key rather than relying on an external system's opaque identifier.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import a bank statement and see it auto-reconciled (Priority: P1)

A director downloads a CODA file from their bank's online portal at the end of the month and
uploads it to the billing section of the web admin. The system parses every transaction and
automatically marks the invoices that carry a matching payment reference as paid — no manual
lookup required.

**Why this priority**: This is the entire value proposition — going from "line-by-line manual
matching in a spreadsheet" to "upload and most of it is already done." Without this, the feature
delivers nothing.

**Independent Test**: Upload a CODA file containing several transactions whose structured
communication matches existing sent invoices' payment references. Verify each matched invoice
transitions to paid with the transaction's date and amount, and an import summary reports how
many transactions were auto-matched.

**Acceptance Scenarios**:

1. **Given** a sent invoice with a known payment reference, **When** a CODA file containing a
   transaction whose structured communication carries that exact reference is imported, **Then**
   the invoice is marked paid with the transaction's value date and amount, and the transaction
   is recorded as an exact reference match.
2. **Given** a CODA file with 20 transactions where 15 carry a recognizable structured payment
   reference, **When** the import completes, **Then** the director sees a summary showing 15
   matched automatically and 5 requiring attention.
3. **Given** an invoice already marked paid, **When** a CODA transaction referencing that same
   invoice is imported, **Then** the invoice is not modified again and the transaction is flagged
   as a duplicate payment for the director's review rather than silently ignored or erroring the
   import.

---

### User Story 2 - Review and confirm a suggested match (Priority: P2)

Some incoming payments don't carry a structured reference (a parent typed their own description,
or the bank stripped it). The director sees these transactions listed with a system-suggested
invoice match based on amount and the sending account, and confirms or rejects each suggestion
with one action.

**Why this priority**: Structured references aren't universal — parents can type free-text
communications, and some banks don't always preserve them. Without this, a meaningful share of
real-world payments would silently fall into the unmatched pile every month, undermining the
time-savings promise of P1.

**Independent Test**: Import a CODA transaction with no recognizable payment reference, whose
amount and sending account match exactly one open invoice belonging to a family with that IBAN
on file. Verify it appears as a suggested match, and confirming it marks the invoice paid the
same way an exact match would.

**Acceptance Scenarios**:

1. **Given** a transaction with no reference match but an amount and sender IBAN that match one
   open invoice for a known family, **When** the import completes, **Then** the transaction
   appears in the review list as a suggested match against that invoice, not yet applied.
2. **Given** a suggested match, **When** the director confirms it, **Then** the invoice is marked
   paid with the transaction's date and amount, identically to an exact reference match.
3. **Given** a suggested match, **When** the director rejects it, **Then** the invoice is left
   untouched and the transaction moves to the unmatched list for manual handling.
4. **Given** a transaction whose amount matches multiple open invoices from different families
   with the same sender IBAN (e.g., a bundled family payment), **When** the import completes,
   **Then** the transaction is left unmatched rather than guessing among them, and is listed for
   manual review.

---

### User Story 3 - Handle transactions that need manual attention (Priority: P3)

Some transactions won't match anything automatically — money from an unrecognized account, a
mistyped reference, a partial payment, or a payment against an invoice from months ago that's
already closed out. The director needs a clear place to see these, understand why each one
didn't auto-match, and resolve or dismiss them.

**Why this priority**: This is what keeps the reconciliation review complete and trustworthy —
without it, "unmatched" payments simply disappear from view and the director is back to manually
combing through the bank portal, which is the exact problem this feature exists to remove. Lower
priority than P1/P2 because a director can still get most of the value (auto + suggested
matching) before this exists, but the feature isn't trustworthy without it.

**Independent Test**: Import a CODA file containing an unmatchable transaction, a partial payment
against an open invoice, and a payment referencing an invoice that's already fully paid. Verify
each is shown with a distinct, correctly labeled status and none of them silently change any
invoice's paid status incorrectly.

**Acceptance Scenarios**:

1. **Given** a transaction that matches no invoice by reference or by amount+sender, **When** the
   import completes, **Then** it is listed as an unrecognized transfer ("onbekende
   overschrijving") for manual review.
2. **Given** an open invoice with a total of €500, **When** a CODA transaction for €300
   referencing that invoice is imported, **Then** the invoice is not marked paid, and the
   director sees the partial payment recorded against the invoice with a €200 remaining balance.
3. **Given** an invoice that reached paid status in an earlier import or a prior month, **When** a
   CODA transaction's structured reference matches that same invoice again, **Then** it is
   flagged as a duplicate payment ("dubbele betaling") for director review.
4. **Given** a CODA transaction whose amount and sender coincidentally match an already-paid
   invoice from an earlier billing period (not the invoice's own reference), **When** the import
   completes, **Then** it is flagged as a payment against a closed invoice ("betaling voor
   afgesloten factuur") rather than reopening or double-crediting that invoice.
5. **Given** any transaction in the review list, **When** the director marks it as handled outside
   the system (e.g., refunded, recorded elsewhere), **Then** it no longer appears as needing
   attention, without affecting any invoice's status.

---

### Edge Cases

- What happens when the uploaded file isn't a valid CODA file (wrong format, corrupted, empty)?
  The import is rejected before any transaction is recorded, with a clear human-readable reason —
  never a raw parser error.
- What happens when the same CODA file (or an overlapping statement period) is uploaded twice?
  Previously-imported transactions are not duplicated as new rows; already-recorded transactions
  are recognized and skipped, and the director is told how many were skipped as already-imported.
- How does the system handle a transaction amount that is negative (a reversal/refund on the
  statement rather than an incoming payment)? It is not treated as a payment toward any invoice —
  it's recorded and surfaced separately for the director's awareness, not matched.
- How does the system handle a transaction whose structured reference is malformed (right shape
  but fails the reference's own checksum)? Treated as if no structured reference were present —
  falls through to amount+sender matching, not treated as an exact match.
- What happens if a suggested match's underlying invoice is marked paid through another path
  (e.g., a director manually marking it paid) before the director acts on the suggestion? The
  stale suggestion is no longer confirmable — confirming it is subject to the exact same
  already-paid guard as FR-008, and re-surfaces the transaction as a duplicate/closed-invoice
  case instead of applying it.
- What happens when two transactions in the *same* imported file both resolve to the same
  invoice (e.g., two lines carrying that invoice's exact reference)? The first one processed
  marks the invoice paid; the second is flagged as a duplicate payment (FR-008) against that
  now-paid invoice — the once-only-paid rule applies within a single import exactly as it does
  across separate imports.
- What happens when a series of partial payments against one invoice reaches or exceeds its full
  total? The invoice is marked paid at the point the cumulative received amount meets or exceeds
  its total, using the date of the transaction that completed it — a partial payment only ever
  blocks the paid transition while a genuine shortfall remains (see FR-010).
- Could two genuinely distinct real-world transactions ever be wrongly treated as the same
  already-imported transaction (FR-013)? Only if they share an identical value date, amount,
  sender IBAN, and communication text — accepted as a vanishingly rare, tolerable false-positive
  risk for the dedupe rule chosen in Clarifications, not something this feature attempts to
  disambiguate further.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to upload a CODA-format bank statement file from the billing
  section of the web admin.
- **FR-002**: The system MUST reject a file that is not a well-formed CODA statement, showing the
  director a clear, human-readable reason without exposing internal parser or system errors.
- **FR-003**: The system MUST parse each transaction in a valid CODA file into: statement/import
  date, value date, amount, sender account (IBAN), sender name, and the free-text/structured
  communication.
- **FR-004**: For each imported transaction, the system MUST attempt an exact match against the
  structured payment reference already assigned to a sent invoice (feature 014). A successful
  exact match marks that invoice paid, using the transaction's value date and amount, unless the
  invoice is already paid (see FR-008).
- **FR-005**: When no exact reference match is found, the system MUST look for exactly one open
  (sent, not yet paid) invoice whose total matches the transaction amount and whose family's IBAN
  on file matches the transaction's sender account, and present it to the director as a suggested
  match requiring explicit confirmation before any invoice status changes.
- **FR-005a**: If more than one open invoice qualifies as an amount+sender candidate for the same
  transaction, the system MUST NOT auto-select one — the transaction is left unmatched for manual
  review rather than guessing.
- **FR-006**: The director MUST be able to confirm a suggested match, which marks the underlying
  invoice paid identically to an exact reference match — subject to the same already-paid guard
  as FR-008, so confirming a suggestion whose invoice was independently paid in the meantime MUST
  fail the confirmation and re-surface the transaction as a duplicate/closed-invoice case instead
  of applying it — or reject it, which leaves the invoice untouched and moves the transaction to
  the unmatched list.
- **FR-007**: A transaction that matches neither by reference nor by amount+sender MUST be listed
  as an unrecognized transfer for manual review, and MUST NOT be silently dropped from the import.
- **FR-008**: An invoice MUST only ever be marked paid once — this guard applies uniformly to
  every path that can mark an invoice paid (FR-004's exact match, FR-006's confirmed suggestion,
  and any transaction within the same import as a prior match), with no path exempt. A
  transaction whose exact reference matches an invoice that is *already* paid MUST be flagged as
  a duplicate payment ("dubbele betaling") for director review rather than re-applying or
  erroring — distinct from FR-009's closed-invoice case in that here the transaction's own
  reference genuinely names the already-paid invoice, rather than merely coinciding with one by
  amount and sender.
- **FR-009**: A transaction that, by amount and sender only (no reference naming that invoice),
  coincidentally lines up with an invoice from an earlier billing period that is already paid
  MUST be flagged distinctly as a payment against a closed invoice ("betaling voor afgesloten
  factuur"), and MUST NOT reopen or re-credit that invoice.
- **FR-010**: An invoice's outstanding total is its full total minus the sum of all amounts
  already received against it from prior applied or partial transactions (not simply its
  original total). When a transaction amount is less than that outstanding total, the system
  MUST NOT mark the invoice paid; the partial payment MUST be recorded and shown against the
  invoice along with the resulting remaining balance. When a transaction's amount, combined with
  amounts already received, meets or exceeds the invoice's full total, the invoice MUST be
  marked paid at that point, using the completing transaction's date.
- **FR-011**: The director MUST be able to view all imported transactions for the tenant,
  filterable by how each was resolved (auto-matched, suggested/pending confirmation, unmatched,
  duplicate, closed-invoice payment).
- **FR-012**: The director MUST be able to mark any unmatched, duplicate, or closed-invoice
  transaction as reviewed/handled, without that action changing any invoice's status.
- **FR-013**: Re-importing a file whose transactions were already recorded in a prior import MUST
  NOT create duplicate transaction records. A transaction is considered already-imported when an
  existing record for the same tenant matches on **all of** value date, amount, sender IBAN, and
  communication text (not any one alone); the system MUST recognize and skip such transactions
  and tell the director how many were skipped **in that specific import**.
- **FR-014**: Sender account (IBAN) data MUST be stored encrypted at rest. The system MUST NOT
  display a sender's full IBAN to a director anywhere in this feature — only a masked/partial
  form (e.g., last 4 digits) — and MUST log an access record each time the full IBAN is decrypted
  for internal use (e.g., to confirm a match candidate), consistent with this system's handling
  of other financial PII. Sender name is not classified as sensitive under this requirement and
  may be stored and displayed in plain text.
- **FR-015**: All director-facing strings introduced by this feature MUST be available in Dutch,
  French, and English.
- **FR-016**: A negative-amount transaction (a reversal/refund on the statement) MUST NOT be
  eligible for invoice matching; it MUST be recorded and shown to the director separately from
  the matchable/payment transactions.

### Key Entities

- **Imported Bank Transaction**: One line from an imported CODA statement — import date, value
  date, amount, sender IBAN, sender name, communication text, which invoice (if any) it resolved
  to, and how it was resolved (exact reference, suggested and confirmed, unmatched, duplicate
  payment, or closed-invoice payment). Belongs to one tenant.
- **Invoice** *(existing, feature 014)*: The billing record a transaction is matched against.
  This feature only ever moves an invoice from sent to paid, following the invoice's existing
  once-only paid transition (feature 014's own status rule) — it never introduces a new invoice
  status.
- **Partial Payment**: An amount received against an invoice that does not fully cover its
  outstanding total. Tracked against the invoice without changing its status, so the director can
  see what's been received versus what's still owed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can go from having just downloaded a bank statement file to a fully
  reconciled set of invoices in well under five minutes for a typical monthly statement, versus
  the manual cross-checking process this replaces.
- **SC-002**: At least 90% of transactions carrying a recognizable structured payment reference
  are automatically matched to the correct invoice with no director action required.
- **SC-003**: Zero invoices are ever marked paid more than once, and zero already-paid invoices
  are silently re-credited by a later import, across all imports.
- **SC-004**: Every transaction in an imported statement is accounted for — none disappear
  silently — verified by transaction-count parity between the uploaded file and the sum of every
  resolution category after import (auto-matched, suggested, unmatched, duplicate, closed-
  invoice, reversal) **plus** the count skipped as already-imported (FR-013); a transaction that
  is skipped is exactly as "accounted for" as one that lands in the review list.
- **SC-005**: A director can distinguish, at a glance from the review list, which transactions
  were handled automatically, which need a one-click confirmation, and which need real manual
  investigation.

## Assumptions

- Each tenant location reconciles a single bank account for MVP — multi-account-per-tenant
  matching is out of scope (per the feature's own stated scope), matching this feature's explicit
  BACKLOG constraint.
- "Written off" invoices, as referenced informally in the originating backlog description, map to
  this system's existing terminal `Paid` status (feature 014's invoice lifecycle has no separate
  write-off state) — a payment coincidentally matching an already-paid invoice from an earlier
  period is what FR-009's "closed invoice" case covers.
- Direct bank API integration (CODABOX's statement-delivery API, mentioned in the originating
  backlog description as a "Phase 2 extension") is explicitly out of scope for this feature's
  implementation. Manual CODA file upload (FR-001) is the complete MVP delivery mechanism; a
  future feature can add CODABOX as an additional import source behind the same matching logic,
  the same way this system has previously built a manual path first and layered an automated
  delivery mechanism on afterward (e.g., feature 014's on-demand invoicing preceding 014a's
  automated payment-reminder scheduling).
- Direct bank API integration under PSD2/open banking remains out of scope entirely, per the
  originating backlog description.
- "Family" for the purposes of amount+sender matching (FR-005) refers to the invoice's associated
  primary contact/guardian, consistent with how this system already associates an IBAN with a
  billing contact for existing payment features (e.g., feature 014a's SEPA mandate, feature 030's
  family billing).
- A CODA statement's transactions are assumed to arrive already correctly signed (positive =
  incoming credit, negative = outgoing/reversal) per the CODA standard; this feature does not
  need to infer transaction direction from any other field.
