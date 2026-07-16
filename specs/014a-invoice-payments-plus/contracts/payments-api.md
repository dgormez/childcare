# API Contract: Invoice Payments Plus

Director/parent routes are tenant-scoped (existing `TenantMiddleware`), reusing `DirectorOnly`/
parent-contact-resolution exactly as 014 does. The webhook route is the one new
`TenantExempt` (public) route this feature introduces (research.md R2).

## Director-facing

### `GET /api/organisations/me/payment-connection`

**New endpoint.** Current Mollie connection status for the organisation.

**Response 200**:

```jsonc
{ "status": "connected", "providerAccountLabel": "Kinderdagverblijf De Zonnebloem", "connectedAt": "2026-07-16T09:00:00Z" }
// or
{ "status": "disconnected" }
```

Never returns tokens (research.md R3).

### `POST /api/organisations/me/payment-connection/authorize`

**New endpoint.** Starts the Mollie OAuth flow.

**Response 200**: `{ "authorizationUrl": "https://www.mollie.com/oauth2/authorize?..." }` —
director-web redirects the browser to this URL.

### `POST /api/organisations/me/payment-connection/callback`

**New endpoint.** Completes the OAuth flow (director-web's redirect target exchanges the
`code` query param via this call).

**Request**: `{ "authorizationCode": "..." }`

**Response 200**: the same shape as the status endpoint, now `connected`.
**Response 422** `errors.paymentConnection.oauth_failed` on a Mollie-side failure — director-web
shows the retry affordance from spec.md's Loading/empty/error states.

### `DELETE /api/organisations/me/payment-connection`

**New endpoint.** Disconnects (FR-003). `204` on success.

### `PUT /api/locations/{locationId}/payment-reminder-settings`

**New endpoint.** Mirrors `UpdateLocationInvoiceSettingsCommand`'s (014) shape exactly.

**Request**:

```jsonc
{ "enabled": true, "delayDays": 3, "cadenceDays": 7 }
```

**Response 200**: updated `LocationResponse` (extended with the three new fields, same pattern
as `invoiceDueDays`).

## Parent-facing

### `POST /api/invoices/{id}/payment-link`

**New endpoint.** Creates (or reuses, research.md R6) an active payment for this invoice and
returns the hosted checkout URL. Only valid on a `Sent` invoice belonging to an organisation
with a `Connected` payment connection.

**Response 200**: `{ "checkoutUrl": "https://www.mollie.com/checkout/..." }`
**Response 422** `errors.invoice.not_sent` — invoice isn't in a payable state.
**Response 422** `errors.paymentConnection.not_connected` — organisation has no connected Mollie
account (drives FR-005's "no Pay now action shown" — director-web/parent-mobile check
`payment-connection` status before rendering the action; this response is the server-side guard
if the client state is stale).

### `GET /api/invoices/{id}/payment-status`

**New endpoint.** Polled by parent-mobile after returning from Mollie's hosted page, to resolve
the "confirming payment" state (spec.md FR-010) without waiting on the webhook to reach the
client through any other channel.

**Response 200**: `{ "invoiceStatus": "sent" | "paid", "paymentStatus": "open" | "paid" | "failed" | "cancelled" | "expired" | null }`

### `GET /api/invoices/{id}/betalingsbewijs`

**New endpoint.** On-demand receipt PDF (research.md R5), mirrors `GET /api/invoices/{id}/pdf`
(014)'s exact auth/not-found posture (indistinguishable 404 for not-found/not-mine/not-yet-paid
— same enumeration-resistance precedent as `GenerateParentInvoicePdfQuery`).

**Response 200**: `application/pdf` bytes.
**Response 404**: invoice doesn't exist, doesn't belong to the caller's child, or isn't `Paid`
yet.

## Public (webhook)

### `POST /api/webhooks/mollie/{paymentReference}`

**New endpoint, `TenantExempt`.** Mollie's webhook target (research.md R2) — `paymentReference`
is the opaque GUID generated at payment-creation time, never a tenant/invoice ID.

**Request** (from Mollie, informational only per FR-007 — not trusted): `{ "id": "tr_xxx" }`

**Response 200**: always, once processing completes (idempotent — research.md R6) or the
reference doesn't resolve, per Mollie's webhook-retry contract (a non-2xx triggers Mollie
retries; a `404` for an unresolvable reference is a permanent failure Mollie should not retry
indefinitely, so this endpoint still returns `200` even on a resolution miss, logging
server-side only — never surfaces which part of the payload was invalid, per spec.md's Edge
Cases).
