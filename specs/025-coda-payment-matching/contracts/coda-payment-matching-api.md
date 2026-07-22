# Contract: CODA/CODABOX Payment Matching API

All routes are director-facing, under `/api`, `RequireAuthorization("DirectorOnly")` — same
`InvoiceEndpoints`/`MonthlyMenuEndpoints` group pattern. No parent- or caregiver-facing surface
(spec.md's Cross-platform Impact).

## `POST /api/coda-imports`

`multipart/form-data`, single `file` field (the `.coda` upload).

- Parses the file via `CodaParser` (research.md R1). A parse failure (not well-formed CODA)
  returns before any persistence:
  - `422 errors.coda_import.invalid_file` — never the raw parser exception message (FR-002,
    Principle VI).
- On success, for every parsed transaction:
  1. Dedupe check (FR-013, data-model.md's `coda_transactions` index): if an existing row for
     this tenant matches on `(ValueDate, AmountCents, SenderIbanLast4)` and, after decrypting,
     the same `SenderIban` and `Communication`, skip it — counted in `SkippedDuplicateCount`.
  2. Otherwise run matching in order: FR-004 (exact OGM) → FR-009's closed-invoice check → FR-005
     (open-invoice amount+IBAN suggestion, respecting FR-005a's no-guess-on-multiple-candidates
     rule) → FR-016 (negative amount ⇒ `Reversal`) → FR-007 (`Unmatched`).
  3. An `Ogm` match calls `MarkInvoicePaidCommand` (research.md R4) immediately, unless FR-010's
     partial-amount case applies (amount < invoice total ⇒ row persisted as `Ogm`,
     `Applied = false`, invoice untouched) or the target invoice is already `Paid` (⇒ `Duplicate`
     instead, `MarkInvoicePaidCommand` never called).
  4. Persists one `coda_imports` row plus one `coda_transactions` row per non-skipped
     transaction.
- `200`:
  ```json
  {
    "importId": "guid",
    "transactionCount": 20,
    "skippedDuplicateCount": 0,
    "summary": { "ogm": 15, "ibanAmountSuggested": 2, "unmatched": 3, "duplicate": 0, "closedInvoice": 0, "reversal": 0 }
  }
  ```

## `GET /api/coda-transactions`

Query params: `matchType` (optional, one of data-model.md's `MatchType` values),
`needsReview` (optional `bool` — shorthand for `matchType in (Unmatched, Duplicate,
ClosedInvoice) AND reviewedAt IS NULL`).

- `200`: array of
  ```json
  {
    "id": "guid",
    "importId": "guid",
    "valueDate": "2026-07-15",
    "amountCents": 45000,
    "senderIbanMasked": "•••• 0166",
    "senderName": "string",
    "communication": "string",
    "matchType": "IbanAmount",
    "applied": false,
    "matchedInvoice": { "id": "guid", "childName": "string", "totalCents": 45000, "receivedCents": 0 } ,
    "reviewedAt": null
  }
  ```
  `senderIbanMasked` from `SenderIbanLast4` only — the full IBAN is never returned to the client
  (mirrors `ContractMapper`'s existing `SepaIbanLast4` masking, FR-014). `matchedInvoice` is null
  when `matchType` is `Unmatched`/`Reversal`.

## `POST /api/coda-transactions/{id}/confirm`

Only valid when `matchType == IbanAmount` and `applied == false`.

- Calls `MarkInvoicePaidCommand` for `MatchedInvoiceId` with this transaction's `ValueDate`/
  `AmountCents`, then sets `Applied = true`.
- `404 errors.coda_transaction.not_found`
- `422 errors.coda_transaction.not_confirmable` — wrong `matchType`, already `applied`, or the
  underlying invoice is no longer `Sent` (e.g. marked paid through another path in the meantime —
  spec.md's stale-suggestion edge case; the transaction is instead flipped to `Unmatched`/
  `Duplicate` server-side as appropriate and the client is told to refresh).
- `200`: the updated transaction (same shape as the list endpoint's row).

## `POST /api/coda-transactions/{id}/reject`

Only valid when `matchType == IbanAmount` and `applied == false`.

- Sets `matchType = Unmatched`, clears `MatchedInvoiceId`.
- `404 errors.coda_transaction.not_found`
- `422 errors.coda_transaction.not_confirmable` (same guard as `confirm`).
- `200`: the updated transaction.

## `POST /api/coda-transactions/{id}/review`

Marks an `Unmatched`/`Duplicate`/`ClosedInvoice` transaction as handled (FR-012). Never touches
`MatchedInvoiceId`/any invoice.

- `404 errors.coda_transaction.not_found`
- `422 errors.coda_transaction.not_reviewable` — wrong `matchType` (`Ogm`/`IbanAmount` rows are
  reviewed implicitly by being applied, not through this action) or already reviewed.
- `200`: the updated transaction, `reviewedAt` set.
