# API Contract: Invoicing

All routes are tenant-scoped (existing `TenantMiddleware`). Director routes reuse the existing
`DirectorOnly` policy; parent routes reuse the existing parent-contact-resolution authorization.

## Director-facing

### `POST /api/locations/{locationId}/invoices/generate`

**New endpoint.** Bulk-generates draft invoices for every child with a contract active at any
point during the given month, at this location.

**Request**:

```jsonc
{ "year": 2027, "month": 7 }
```

**Response 200**: `InvoiceResponse[]` — every invoice that now exists for this location/month
(newly created and any that already existed and were left untouched, since generation is
idempotent per spec.md FR-003/US1/AC5). Regeneration of an already-`Draft` invoice's line items
happens automatically as part of this same call.

### `GET /api/locations/{locationId}/invoices`

**New endpoint.** Filterable/sortable list for a location.

**Query params**: `year`, `month` (optional — omitting both lists all months), `status`
(`draft`/`sent`/`paid`/`overdue` — `overdue` filters `sent` rows whose `dueDate` has passed).

**Response 200**: `InvoiceResponse[]`.

### `GET /api/invoices/{id}`

**New endpoint.** Single invoice detail (director).

**Response 200**: `InvoiceResponse`. `404` if not found or not in this tenant.

### `PUT /api/invoices/{id}/extra-charges`

**New endpoint.** Replaces the invoice's extra-charges array (add/remove/edit in one call — the
web form sends the full desired list, mirroring how `MenuVariantSettingsForm` sends the full
desired `menuVariantPriorityOrder` rather than incremental add/remove calls, 013j precedent).
Only valid on a `Draft` invoice.

**Request**:

```jsonc
{ "extraCharges": [{ "label": "Registration fee", "amountCents": 2500 }] }
```

**Response 200**: the updated `InvoiceResponse`, with `totalCents` recomputed.
**Response 422** `errors.invoice.not_draft` if the invoice is not `Draft`.

### `POST /api/invoices/send`

**New endpoint.** Sends one or more `Draft` invoices (individual send is `{"invoiceIds": [id]}`,
batch send is the same shape with many ids).

**Request**:

```jsonc
{ "invoiceIds": ["...", "..."] }
```

**Response 200**: `InvoiceResponse[]` — the now-`Sent` invoices.
**Response 422** `errors.invoice.not_draft` if any id is not currently `Draft` (whole request
rejected, no partial send — same all-or-nothing pattern as every other batch action in this
codebase).

### `POST /api/invoices/{id}/mark-paid`

**New endpoint.**

**Request**:

```jsonc
{ "paidAt": "2027-08-03" }
```

**Response 200**: the updated `InvoiceResponse`, `status: "paid"`.
**Response 422** `errors.invoice.not_sent` if the invoice is currently `Draft` or already `Paid`.

### `POST /api/invoices/{id}/regenerate`

**New endpoint.** Recomputes line items/totals from current attendance data. Valid on `Draft` or
`Sent`; re-notifies the parent if it was `Sent`.

**Response 200**: the updated `InvoiceResponse`.
**Response 422** `errors.invoice.paid_immutable` if the invoice is `Paid`.

### `GET /api/invoices/{id}/pdf`

**New endpoint.** Streams the rendered PDF (`application/pdf`), generated on-demand from the
invoice's current `LineItems` — never a stored file (research.md R1).

### `PUT /api/locations/{id}/invoice-settings`

**New endpoint.** Mirrors 013f's `PUT /api/locations/{id}/reservation-settings` shape exactly.

**Request**:

```jsonc
{
  "erkenningsnummer": "123456",
  "bankAccountNumber": "BE68539007547034",
  "invoiceDueDays": 14
}
```

**Response 200**: the updated `LocationResponse`, now including these three fields.

### `PUT /api/organisations/me`

**New endpoint** (no PUT existed on this resource before — `GET /api/organisations/me` is
007a's existing read). Org-wide, not per-location.

**Request**: `{ "kboNumber": "0123.456.789" }`

**Response 200**: the updated organisation profile response (same shape `GET` returns), now
including `kboNumber`.

## Parent-facing

### `GET /api/parent/invoices`

**New endpoint.** Every `Sent`/`Paid` invoice (including computed-`overdue`) for every child the
requesting parent has an active or past contract for — `Draft` invoices are never included
(spec.md FR-008).

**Response 200**: `ParentInvoiceEntry[]` — `InvoiceResponse` shape plus `childName`/
`locationName` for display, mirroring `ParentMonthlyMenuEntry`'s existing per-child-entry
precedent (013j).

### `GET /api/parent/invoices/{id}/pdf`

**New endpoint.** Same on-demand PDF render as the director route, scoped to only the requesting
parent's own children's invoices. `404` (not `403`, matching every other parent-scoped resource
in this codebase) if the invoice doesn't belong to one of their children, or is still `Draft`.

## Response shape

```jsonc
// InvoiceResponse
{
  "id": "guid",
  "childId": "guid",
  "childName": "Emma Peeters",
  "contractId": "guid",
  "locationId": "guid",
  "locationName": "KDV Zonnebloem",
  "periodMonth": "2027-07-01",
  "status": "sent",           // draft | sent | paid
  "isOverdue": false,          // computed: status == sent && dueDate < today
  "subtotalCents": 66500,
  "totalCents": 69000,
  "lineItems": {
    "presentDays": 18,
    "unjustifiedAbsentDays": 1,
    "dailyRateCents": 3500,
    "closureDaysExcluded": 2,
    "daysMin5u": 15,
    "daysMin11u": 4,
    "extraCharges": [{ "label": "Registration fee", "amountCents": 2500 }]
  },
  "ogmReference": "+++123/4567/89012+++",
  "dueDate": "2027-07-29",
  "sentAt": "2027-07-15T09:00:00Z",
  "paidAt": null,
  "createdAt": "2027-07-15T09:00:00Z",
  "updatedAt": "2027-07-15T09:00:00Z"
}
```
