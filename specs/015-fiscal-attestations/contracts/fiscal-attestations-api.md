# API Contract: Fiscal Attestations

All routes are tenant-scoped (existing `TenantMiddleware`) — no public/webhook route in this
feature, unlike 014a.

## Director-facing

### `POST /api/fiscal-attestations/generate`

**New endpoint.** Bulk-generates attestations for a tax year (FR-001/FR-009) — only for
eligible children/locations that don't already have a row for that year (existing rows are left
untouched; see `regenerate` below to correct one). `DirectorOnly`.

**Request**: `{ "taxYear": 2026 }`

**Response 200**:

```jsonc
{
  "taxYear": 2026,
  "results": [
    { "childId": "...", "locationId": "...", "status": "generated" },
    { "childId": "...", "locationId": "...", "status": "alreadyExists" },
    { "childId": "...", "locationId": "...", "status": "failed" }
  ]
}
```

Per-item `status` reflects FR-010's failure isolation — a single failure never fails the whole
request (`200` overall; failures are visible per row, not via HTTP status).

### `GET /api/fiscal-attestations?taxYear=2026`

**New endpoint.** Director-facing list for a tax year (FR-012) — every child with at least one
`Paid` invoice that year, joined against existing `FiscalAttestation` rows to compute status
(`generated` / `notYetGenerated`) transiently (data-model.md's State/lifecycle — no stored status
column). `DirectorOnly`.

**Response 200**: `FiscalAttestationResponse[]`, each:

```jsonc
{
  "id": "...",              // null if notYetGenerated
  "childId": "...",
  "childName": "Emma Peeters",
  "locationId": "...",
  "locationName": "KDV De Zonnebloem",
  "taxYear": 2026,
  "totalAmountCents": 184000,
  "status": "generated",
  "generatedAt": "2026-07-16T09:00:00Z"
}
```

### `POST /api/fiscal-attestations/{childId}/{locationId}/{taxYear}/regenerate`

**New endpoint.** Re-aggregates current `Paid`-invoice data and replaces the existing PDF in
place (FR-008) — creates the row if none existed yet (regenerate also serves as "generate one").
`DirectorOnly`.

**Response 200**: the updated `FiscalAttestationResponse`.
**Response 422** `errors.fiscalAttestation.no_paid_invoices` — the child/location/year has no
`Paid` invoices (FR-003); nothing is created or overwritten.

### `GET /api/fiscal-attestations/{id}/download-url`

**New endpoint.** Signed GCS download URL for the director (research.md R1 — never proxies
bytes through the API, same as every other `Gcs*Storage`-backed download). `DirectorOnly`.

**Response 200**: `{ "downloadUrl": "https://storage.googleapis.com/...", "expiresAt": "..." }`

## Parent-facing

### `GET /api/parent/fiscal-attestations`

**New endpoint.** Every generated attestation for every child the requesting contact is linked
to (mirrors `GetParentInvoicesQuery`'s exact "every linked contact sees the same children's
data" resolution, 014).

**Response 200**: `FiscalAttestationResponse[]` (same shape as the director list, scoped to the
caller's linked children; no `notYetGenerated` rows — a parent only ever sees what exists).

### `GET /api/parent/fiscal-attestations/{id}/download-url`

**New endpoint.** Same signed-URL shape as the director route, restricted to attestations for
children the caller is linked to (FR-011). `404` (not `403`) for an attestation that exists but
isn't the caller's — same enumeration-resistance posture 014's `GenerateParentInvoicePdfQuery`
already established.

**Response 200**: `{ "downloadUrl": "https://storage.googleapis.com/...", "expiresAt": "..." }`
**Response 404**: attestation doesn't exist or doesn't belong to a linked child.
