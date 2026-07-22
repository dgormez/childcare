# Quickstart: CODA/CODABOX Payment Matching

## Prerequisites

- Backend running locally against Docker PostgreSQL (`backend/`, per repo README).
- A tenant with at least one location, one child with an active `Sent` invoice carrying a known
  `OgmReference` (feature 014), and one child whose contract has a SEPA mandate on file (feature
  024, so `Contract.SepaIbanEncrypted`/`SepaIbanLast4` are populated) with another `Sent` invoice.
- A director account, authenticated (`DirectorOnly` policy).
- A sample `.coda` test fixture file containing:
  1. One transaction whose structured communication is the first invoice's exact `OgmReference`
     digits, amount equal to that invoice's `TotalCents`.
  2. One transaction with no structured communication, whose amount and sender IBAN match the
     second invoice/contract exactly.
  3. One transaction that matches nothing (arbitrary amount/IBAN/communication).
  4. One transaction with a negative amount.
  5. A duplicate of transaction 1 (same value date/amount/IBAN/communication) appended a second
     time in the same file, to exercise FR-013's within-import dedupe.

## Scenario 1 — Upload and auto-match (US1)

1. `POST /api/coda-imports` with the fixture file.
2. Expect `200` with `summary.ogm == 1`, `summary.ibanAmountSuggested == 1`,
   `summary.unmatched == 1`, `summary.reversal == 1`, and `skippedDuplicateCount == 1` (the
   appended duplicate of transaction 1 is recognized and skipped, not double-counted in `ogm`).
3. `GET /api/invoices/{firstInvoiceId}` → `status == "Paid"`, `paidAt` equal to the transaction's
   value date.

## Scenario 2 — Confirm a suggested match (US2)

1. `GET /api/coda-transactions?matchType=IbanAmount` → the second transaction, `applied: false`.
2. `POST /api/coda-transactions/{id}/confirm`.
3. Expect `200`, `applied: true`. `GET /api/invoices/{secondInvoiceId}` → `status == "Paid"`.
4. Repeat from step 1 with a fresh suggested-match fixture, but call `/reject` instead of
   `/confirm` at step 2 — expect the invoice to remain `Sent` and the transaction to reappear
   under `GET /api/coda-transactions?matchType=Unmatched`.

## Scenario 3 — Manual review queue (US3)

1. `GET /api/coda-transactions?needsReview=true` → includes the unmatched transaction from
   Scenario 1.
2. `POST /api/coda-transactions/{id}/review`.
3. Re-run `GET /api/coda-transactions?needsReview=true` → that transaction no longer appears.
   `GET /api/coda-transactions?matchType=Unmatched` still returns it, now with `reviewedAt` set —
   confirms review dismisses it from the attention queue without deleting the record.

## Scenario 4 — Duplicate and closed-invoice payments

1. Re-upload the Scenario 1 fixture file a second time (a fresh, distinct upload — not a re-run
   of the same request).
2. Expect the transaction whose OGM matches the now-`Paid` first invoice to come back as
   `matchType: "Duplicate"`, and the invoice's `PaidAt`/`Status` unchanged from Scenario 1's
   value (proves FR-008's "never re-applied" invariant).
3. Construct a transaction whose amount+IBAN coincidentally match the same now-`Paid` invoice's
   contract but with a different (non-matching) communication — upload it in a new file. Expect
   `matchType: "ClosedInvoice"`, invoice untouched (FR-009).

## Scenario 5 — Partial payment

1. Upload a transaction whose OGM matches a `Sent` invoice's reference but whose amount is less
   than that invoice's `TotalCents`.
2. Expect `matchType: "Ogm"`, `applied: false`. `GET /api/invoices/{id}` → still `Sent`.
3. `GET /api/coda-transactions` for that row → `matchedInvoice.receivedCents` equals the partial
   amount, less than `matchedInvoice.totalCents` (FR-010).

## Expected outcomes (traces to spec.md Success Criteria)

- SC-002: ≥90% of a realistic statement's transactions land in `Ogm` without any director action
  — verify against the fixture's known-reference transactions.
- SC-003: no `Invoice` ever reaches `Paid` a second time or is re-credited — Scenario 4 is the
  direct regression test for this.
- SC-004: `transactionCount` from the import summary equals the sum of `summary.*` values plus
  `skippedDuplicateCount` — every transaction is accounted for, none silently dropped.
