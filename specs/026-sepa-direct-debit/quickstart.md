# Quickstart: SEPA Direct Debit Batch Collection

## Prerequisites

- Backend running locally against Docker PostgreSQL (`backend/`, per repo README).
- A tenant with `Tenant.SepaCreditorIdentifier` set (feature 024's existing organisation
  settings) and a location with `Location.BankAccountNumber` set (feature 014's existing
  invoice settings).
- Three children under that location, each with a `Sent` invoice:
  1. Child A's contract has a signed, non-revoked SEPA mandate (feature 024).
  2. Child B's contract has never signed a mandate.
  3. Child C's contract had a signed mandate that is now revoked (FR-011).
- A director account, authenticated (`DirectorOnly` policy).

## Scenario 1 — Generate and download a batch (US1)

1. `GET /api/locations/{locationId}/sepa-batch-eligibility?month=<current month>`.
   Expect `creditorConfigured: true`, Child A's invoice in `eligible`, Child B's invoice in
   `excluded` with `reason: "NoMandate"`, Child C's invoice in `excluded` with
   `reason: "MandateRevoked"`.
2. `POST /api/locations/{locationId}/sepa-batches` with `invoiceIds: [<Child A's invoice>]` and
   `executionDate` two business days out.
3. Expect `200`, response body is a `pain.008.001.02` XML document. Verify it validates against
   `backend/ChildCare.Infrastructure/Sepa/Schemas/pain.008.001.02.xsd` and contains exactly one
   `DrctDbtTxInf` with `SeqTp = FRST` (Child A's contract has never had a batch before —
   research.md R3), the correct `InstdAmt`, Child A's debtor IBAN/name, and `EndToEndId` equal to
   the invoice's `OgmReference`.
4. `GET /api/invoices/{childAInvoiceId}` → `status == "PendingDebit"`.
5. `GET /api/locations/{locationId}/sepa-batches` → one entry, `invoiceCount: 1`.

## Scenario 2 — Second batch for the same contract uses RCUR (research.md R3)

1. Mark Child A's invoice from Scenario 1 paid (simulate CODA reconciliation — Scenario 3 below
   exercises the real path) or leave it `PendingDebit` and instead create a second `Sent` invoice
   for Child A in the following month.
2. Repeat Scenario 1 steps 2–3 for the new invoice. Expect `SeqTp = RCUR` this time — Child A's
   contract now has a prior successful batch under the same `SepaMandateReference`.

## Scenario 3 — Collection confirmed automatically (US2)

1. From Scenario 1's `PendingDebit` invoice, construct a CODA import fixture (feature 025) whose
   transaction's structured communication is that invoice's `OgmReference` and amount matches.
2. `POST /api/coda-imports` with the fixture.
3. `GET /api/invoices/{childAInvoiceId}` → `status == "Paid"` — identical to a CODA match against
   a plain `Sent` invoice (proves FR-009).

## Scenario 4 — Returned debit (US3)

1. Generate a fresh batch including one invoice (now `PendingDebit`).
2. `POST /api/invoices/{id}/mark-sepa-returned` with `{ "reason": "Insufficient funds" }`.
3. Expect `200`, `status == "Sent"`, `sepaBatchId == null`, reason visible on the invoice.
4. Repeat step 2 against an invoice that is `Paid` instead of `PendingDebit` — expect
   `422 errors.invoice.not_pending_debit` (FR-010's exclusion).

## Scenario 5 — Revoke a mandate (US4)

1. `POST /api/contracts/{childAContractId}/revoke-sepa-mandate`.
2. Expect `200`, `mandateStatus == "revoked"`.
3. `GET /api/locations/{locationId}/sepa-batch-eligibility?month=<current month>` → Child A's
   invoice now appears in `excluded` with `reason: "MandateRevoked"`.
4. `POST /api/contracts/{childAContractId}/signing-invitation` (feature 024's existing endpoint)
   → succeeds identically to inviting a never-signed contract, confirming FR-012's
   revoke-and-resign path has no special-cased blocker.

## Scenario 6 — Blocked generation (edge cases)

1. Against a location with no `Location.BankAccountNumber` set, call
   `GET /api/locations/{locationId}/sepa-batch-eligibility` → `creditorConfigured: false`.
   `POST /api/locations/{locationId}/sepa-batches` against it → `422
   errors.sepa_batch.creditor_not_configured`, no invoice status changes.
2. `POST /api/locations/{locationId}/sepa-batches` with `executionDate` set to tomorrow when
   tomorrow is a Saturday/Sunday, or to today → `422 errors.sepa_batch.execution_date_too_soon`.
3. Generate a batch for an invoice, then immediately attempt a second
   `POST /api/locations/{locationId}/sepa-batches` including that same invoice ID again → `422
   errors.sepa_batch.invoice_not_eligible` (now `PendingDebit`, FR-013).
