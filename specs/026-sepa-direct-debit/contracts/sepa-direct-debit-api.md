# Contract: SEPA Direct Debit Batch Collection API

All routes are director-facing, under `/api`, `RequireAuthorization("DirectorOnly")` — same
`InvoiceEndpoints`/`CodaTransactionEndpoints` group pattern. No parent- or caregiver-facing
surface (spec.md's Cross-platform Impact).

## `GET /api/locations/{locationId}/sepa-batch-eligibility?month=2026-08`

Lists `Sent` invoices for the location/month split into eligible/excluded (FR-001,
data-model.md's Eligibility rule).

- `200`:
  ```json
  {
    "creditorConfigured": true,
    "eligible": [
      { "invoiceId": "guid", "childName": "string", "totalCents": 45000, "debtorName": "string" }
    ],
    "excluded": [
      { "invoiceId": "guid", "childName": "string", "totalCents": 45000, "reason": "NoMandate" }
    ]
  }
  ```
  `reason` is one of `NoMandate`, `MandateRevoked`, `NonPositiveAmount` (data-model.md's priority
  order). `creditorConfigured` is `false` when the location is missing its creditor identifier
  (`Tenant.SepaCreditorIdentifier`) or bank account (`Location.BankAccountNumber`) — the client
  shows FR-004's blocking message instead of a selection list when `false`.

## `POST /api/locations/{locationId}/sepa-batches`

```json
{ "invoiceIds": ["guid", "guid"], "executionDate": "2026-08-05" }
```

- Re-validates every invoice against the eligibility rule server-side (never trusts the client's
  earlier read — an invoice could have changed state since) and re-validates `executionDate`
  against FR-005's minimum-business-day rule.
- `422 errors.sepa_batch.creditor_not_configured` — FR-004, before anything else runs.
- `422 errors.sepa_batch.execution_date_too_soon` — FR-005, includes the minimum valid date.
- `422 errors.sepa_batch.invoice_not_eligible` — one or more submitted invoice IDs are no longer
  eligible (FR-013's already-`PendingDebit` case, or any other eligibility-rule failure);
  identifies which ones so the client can refresh its selection rather than guess.
- `422 errors.sepa_batch.no_invoices_selected` — empty `invoiceIds`.
- Otherwise: generates the pain.008.001.02 XML (research.md R1), validates it against the
  embedded schema (research.md R2) before proceeding —
  `500 errors.sepa_batch.generation_failed` (logged in full server-side, Principle VI) on a
  schema-validation failure, with **no** invoice status change and **no** `SepaBatch` row
  persisted.
- On success: persists one `SepaBatch` row, sets `Invoice.Status = PendingDebit` and
  `Invoice.SepaBatchId` on every included invoice, and returns the XML file
  (`Content-Type: application/xml`, `Content-Disposition: attachment`) directly as the response
  body — the file is never separately persisted to storage (spec.md's Security considerations).

## `GET /api/locations/{locationId}/sepa-batches`

Batch history (FR-008).

- `200`: array of
  ```json
  {
    "id": "guid",
    "executionDate": "2026-08-05",
    "generatedAt": "2026-07-22T10:00:00Z",
    "invoiceCount": 12,
    "totalCents": 540000
  }
  ```

## `POST /api/invoices/{id}/mark-sepa-returned`

```json
{ "reason": "Insufficient funds" }
```

FR-010. Only valid when `Invoice.Status == PendingDebit`.

- `404 errors.invoice.not_found`
- `422 errors.invoice.not_pending_debit` — wrong status (including the `Paid` case spec.md's
  User Story 3/Acceptance Scenario 2 explicitly excludes).
- `422 errors.sepa_batch.reason_required` — empty/whitespace reason.
- `200`: the updated invoice — `Status = Sent`, `SepaBatchId = null`, reason recorded and visible
  on the invoice (exact display field per tasks.md's data-model decision — reuses the existing
  invoice-notes convention if one exists, else a new `Invoice.SepaReturnReason` column).

## `POST /api/contracts/{id}/revoke-sepa-mandate`

FR-011. Only valid when the contract has a signed, non-revoked mandate.

- `404 errors.contract.not_found`
- `422 errors.contract.mandate_not_revocable` — no mandate signed yet, or already revoked.
- `200`: the updated contract (`ContractResponse` gains `SepaRevokedAt` alongside the existing
  `SepaIbanMasked`/`SepaMandateReference` fields — mirrors `SigningStatus`'s existing derived-
  field precedent with a parallel `MandateStatus`: `none` / `signed` / `revoked`).

No new endpoint for re-signing (FR-012) — this reuses feature 024's existing
`POST /api/contracts/{id}/signing-invitation` (`ContractsEndpoints.cs`) unchanged; a revoked
contract is simply eligible for that existing action again once `SepaRevokedAt` is set, the same
way a never-signed one already is.
