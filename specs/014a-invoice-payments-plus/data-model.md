# Data Model: Invoice Payments Plus

## `PaymentProviderConnection` (new, public schema, table `payment_provider_connections`)

One row per organisation (`Tenant`) — Mollie OAuth connection status. Lives in the public
schema (research.md R3) because the webhook path must resolve and use these credentials before
any tenant context is established.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `TenantId` | `Guid` | FK to `Tenant`, unique — one connection per organisation. |
| `Provider` | `string` | `"mollie"` — a plain string, not an enum, so a second provider (R1) doesn't require a migration to add. |
| `ProviderAccountId` | `string` | Mollie's organisation ID for the connected sub-merchant account. |
| `ProviderAccountLabel` | `string` | Display name/email returned by Mollie at connection time — the only account identifier ever shown to a director. |
| `EncryptedAccessToken` | `string` | `IDataProtector`-encrypted (research.md R3). Never serialized to any API response. |
| `EncryptedRefreshToken` | `string` | Same protection. |
| `TokenExpiresAt` | `DateTime` | For proactive refresh. |
| `Status` | `PaymentConnectionStatus` enum (`Connected`/`Disconnected`) | FR-003 — disconnecting sets this rather than deleting the row (keeps historical `Payment` rows attributable). |
| `ConnectedAt` | `DateTime` | |
| `DisconnectedAt` | `DateTime?` | |

**Unique index**: `TenantId`.

## `Payment` (new, public schema, table `payments`)

One row per payment *attempt* against an invoice. Lives in the public schema, not the tenant
schema, because the webhook (research.md R2) must resolve `TenantId`/`InvoiceId` from
`PaymentReference` alone, before tenant context exists — this table IS that resolution index.
`InvoiceId` still refers to a tenant-schema row; no cross-schema FK is created (same
cross-schema-reference posture as `VaccineRecord.VaccineTypeId`, 013g), since resolving it is
exactly this table's job.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `PaymentReference` | `Guid` | System-generated, unique, opaque — the webhook URL path segment (research.md R2). Never derived from the OGM reference (that's tenant-scoped, not globally unique). |
| `TenantId` | `Guid` | FK to `Tenant`. Resolved by the webhook handler to switch tenant context. |
| `InvoiceId` | `Guid` | The tenant-schema `Invoice.Id` this payment is for. |
| `ProviderPaymentId` | `string?` | Mollie's payment ID, set once Mollie confirms creation. Null only in the brief window between our row's creation and Mollie's create-payment API response. |
| `Status` | `PaymentStatus` enum (`Open`/`Paid`/`Failed`/`Cancelled`/`Expired`) | Mirrors Mollie's own payment-status vocabulary; drives the "Pay now" reuse decision (research.md R6). |
| `AmountCents` | `int` | The invoice's outstanding amount at creation time — never mutates the invoice's own `TotalCents` (spec.md FR-011). |
| `FeeCents` | `int?` | PSP fee, recorded once Mollie reports it (typically at settlement) — separate from `AmountCents`/invoice total. |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

**Unique index**: `PaymentReference`. **Non-unique index**: `(TenantId, InvoiceId, Status)` — the
"find the current open payment for this invoice" lookup (research.md R6).

## `Invoice` (existing, tenant schema — extended)

| Field | Type | Notes |
|---|---|---|
| `ReminderCount` | `int` | Default `0`. Incremented by the reminder job (research.md R8); capped at 3 (spec.md FR-013). |
| `LastReminderSentAt` | `DateTime?` | Null until the first reminder. Drives the per-location cadence check. |

No other `Invoice` field changes — 014's billable-day computation, PDF content, and existing
`Draft → Sent → Paid` transition are untouched (spec.md FR-021).

## `Location` (existing, tenant schema — extended)

| Field | Type | Notes |
|---|---|---|
| `PaymentRemindersEnabled` | `bool` | Default `false` — opt-in (spec.md Assumptions). |
| `PaymentReminderDelayDays` | `int` | Default `3` — days after `DueDate` before the first reminder (2026-07-16 clarification). |
| `PaymentReminderCadenceDays` | `int` | Default `7` — repeat interval thereafter, up to the 3-reminder cap. |

Mirrors `InvoiceDueDays`'s existing pattern exactly — a location that never touches these
settings behaves identically to before this feature (spec.md SC-005).

## Enums (new)

```csharp
public enum PaymentConnectionStatus { Connected, Disconnected }
public enum PaymentStatus { Open, Paid, Failed, Cancelled, Expired }
```

## State transitions

```text
PaymentProviderConnection:
  (none) --(OAuth complete)--> Connected --(disconnect)--> Disconnected --(OAuth complete)--> Connected
  (re-connecting after disconnect reuses the same row, per FR-003's "reconnect" edge case)

Payment:
  (none) --(create)--> Open --(webhook confirms paid)--> Paid
                          |--(webhook confirms failed)--> Failed
                          |--(webhook confirms cancelled)--> Cancelled
                          |--(TTL elapsed, checked live)--> Expired
  Paid/Failed/Cancelled/Expired are terminal — "Pay now" reuse (research.md R6) only reuses an
  Open payment; any terminal state creates a fresh Payment row on the next attempt.

Invoice (014, unchanged) + this feature's read of it:
  Draft --(send)--> Sent --(mark paid, manual OR Payment webhook)--> Paid
  ReminderCount only increments while Status == Sent; frozen once Paid.
```

## Betalingsbewijs

Not a stored entity (research.md R5) — rendered on demand from `Invoice`'s `Paid`-state fields,
the same way 014's invoice PDF already works. No new table.
