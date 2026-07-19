# API Contract: Family Siblings

All routes below are tenant-scoped, reusing the existing `TenantMiddleware`. No new public
(cross-tenant) routes. Director routes require `DirectorOnly`; parent routes reuse the existing
`ICurrentParentContactResolver`-based authorization every parent endpoint already uses.

## Parent-facing

### `POST /api/parent/day-reservations/bulk`

**New endpoint.** Submits the same day-reservation request for multiple linked children in one
call (spec FR-001/FR-002/FR-003).

**Request**:

```jsonc
{
  "childIds": ["<guid>", "<guid>"],
  "type": "absence", // "absence" | "extra" | "exchange" — same enum as the existing single endpoint
  "requestedDate": "2026-08-03",
  "exchangeForDate": null,
  "reason": "Family holiday"
}
```

**Response 200**:

```jsonc
{
  "results": [
    { "childId": "<guid>", "childName": "Emma", "succeeded": true, "reservation": { /* existing DayReservationResponse shape */ } },
    { "childId": "<guid>", "childName": "Lucas", "succeeded": false, "errorKey": "errors.day_reservations.request_type_disabled" }
  ]
}
```

Always `200` if at least the caller is authorized — per-child outcomes carry their own
success/failure (FR-003), mirroring how `GenerateInvoicesCommand`'s per-contract loop never
fails the whole request for one bad contract. `403`/`errors.day_reservations.child_not_linked`
only if the caller isn't linked to *any* of the requested children at all.

### `GET /api/parent/children/previous`

**New endpoint.** Lists the requesting parent's deactivated linked children (spec FR-015/R8).

**Response 200**: `ParentPreviousChildResponse[]` — same shape as the existing `GET
/api/parent/children`, plus `enrollmentStart`/`enrollmentEnd` dates. Empty array (not an error)
when the parent has no deactivated children — parent-mobile hides the entry point entirely in
that case (FR-017).

Existing `GET /api/parent/children/{childId}/daily-summary` and `GET /api/parent/invoices`
already work unmodified for a deactivated child once its id is known (R8) — no contract change.

### `GET /api/parent/invoices` — response shape extended

Existing endpoint (014). When an invoice's `FamilyGroupId` is set, grouped invoices collapse
into one entry in the response list:

```jsonc
{
  "id": "<first child's invoice id — used for legacy single-invoice actions if ever needed>",
  "familyGroupId": "<guid>",
  "children": [
    { "childId": "<guid>", "childName": "Emma", "subtotalCents": 45000 },
    { "childId": "<guid>", "childName": "Lucas", "subtotalCents": 40500 }
  ],
  "totalCents": 85500,
  "status": "sent",
  "dueDate": "2026-08-14"
  // ...existing per-invoice fields (status/dueDate/etc.) — identical across grouped invoices by construction
}
```

An invoice with no `familyGroupId` is unaffected — same shape as today.

### `GET /api/parent/invoices/family/{familyGroupId}/pdf`

**New endpoint.** Combined family invoice PDF (spec FR-008, R5). Same authorization shape as the
existing `GET /api/parent/invoices/{id}/pdf` (indistinguishable not-found for
non-existent/unowned/draft, per that endpoint's existing enumeration-resistance precedent) —
authorized if the caller is linked to *any* child among the grouped invoices.

## Director-facing

### `PUT /api/locations/{locationId}/sibling-billing-settings`

**New endpoint.** Mirrors `UpdateLocationInvoiceSettingsCommand`'s (014) shape.

**Request**: `{ "siblingDiscountPct": 10, "familyInvoiceBundlingEnabled": true }`

**Response 200**: updated location settings (existing `LocationResponse` shape, extended with
these two fields).

### `GET /api/children/{childId}/contacts` — existing, now consumed by web

No contract change — `ChildCare.Api/Endpoints/ContactsEndpoints.cs` already exposes this
(006/013). The web Contacts tab (spec FR-011/FR-012) is the first UI consumer.

### `GET /api/contacts` — existing, now consumed for duplicate detection

No contract change (R7) — the web "add contact" dialog fetches this once and filters
client-side for an email/phone match.

### `POST /api/children/{childId}/contacts`, `PUT .../contacts/{contactId}`, `DELETE
.../contacts/{contactId}` — existing, now consumed by web

No contract change. `Relationship` now additionally accepts `"fosterParent"` and `"other"`
(R6).

### `POST /api/locations/{locationId}/invoices/generate` — existing, behavior extended

No request/response shape change. When the location's `SiblingDiscountPct > 0` and/or
`FamilyInvoiceBundlingEnabled = true`, the generated `InvoiceResponse[]` reflects the discount
line item and/or shared `familyGroupId` per R2/R3/R4 — existing callers (director-web's invoice
generation screen) need no changes beyond rendering the two new optional fields already covered
above.

### `POST /api/invoices/{id}/mark-paid` — existing, behavior extended

No request/response shape change. When the target invoice has a `familyGroupId`, every invoice
sharing it transitions `Sent → Paid` together (R5/FR-009a) — the response still describes the
one invoice that was targeted; director-web's invoice list reflects all grouped invoices as paid
on its next refresh (same refresh-on-mutate pattern already used elsewhere).
