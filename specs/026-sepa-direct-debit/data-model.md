# Data Model: SEPA Direct Debit Batch Collection

## Modified entities

### `Contract` (existing, feature 024/007) — tenant schema

Adds one column to the existing SEPA mandate fields (`SepaIbanEncrypted`, `SepaIbanLast4`,
`SepaMandateReference`, `SepaAuthorisedAt`):

| Column | Type | Notes |
|---|---|---|
| `SepaRevokedAt` | `DateTime?` | Null = never revoked (or never signed). Set by FR-011's revoke action. A contract is eligible for a batch only when `SepaAuthorisedAt IS NOT NULL AND SepaRevokedAt IS NULL`. |

Re-signing after revocation (FR-012 — via feature 024's existing invitation flow) sets a **new**
`SepaMandateReference` (`SepaMandateReferenceGenerator`, unchanged) and clears `SepaRevokedAt`,
which is also what makes R3's FRST/RCUR determination correctly restart at `FRST` — a new
reference has no prior batch history to match against.

### `Invoice` (existing, feature 014) — tenant schema

| Column | Type | Notes |
|---|---|---|
| `SepaBatchId` | `Guid?` | Set when the invoice is included in a generated batch (FR-007); cleared when the invoice returns to `Sent` via FR-010's returned-debit action. Null for every invoice not currently `PendingDebit`. |

### `InvoiceStatus` (existing enum, feature 014)

```
Draft, Sent, PendingDebit, Paid
```

`PendingDebit` is inserted between `Sent` and `Paid`. Status only ever moves forward except for
FR-010's explicit `PendingDebit → Sent` reversal (a returned debit) — every other existing
invariant (`Draft → Sent → Paid` forward-only, `Paid` is immutable) is unchanged; feature 025's
`MarkInvoicePaidCommand` and `ImportCodaFileCommand` are extended (not replaced) to treat
`PendingDebit` as an eligible "open" status alongside `Sent` (spec.md FR-009).

## New entities

### `SepaBatch` — tenant schema

One row per successfully generated pain.008 batch — audit/history record (spec.md FR-007/FR-008).
Immutable once created.

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `LocationId` | `Guid` | FK to `Location`. |
| `ExecutionDate` | `DateOnly` | The date set by the director (FR-002), validated against FR-005 at generation time. |
| `GeneratedByUserId` | `Guid` | The director who generated it. |
| `GeneratedAt` | `DateTime` | |
| `TotalCents` | `int` | Sum of included invoices' amounts, denormalized for FR-008's history list (avoids re-summing on every list read). |
| `InvoiceCount` | `int` | Denormalized alongside `TotalCents`, same reason. |

Included invoices are found via `Invoice.SepaBatchId = SepaBatch.Id` — no separate join table,
mirroring how `FamilyGroupId` on `Invoice` (feature 030) already represents a one-to-many grouping
without a dedicated join table.

## Reused, unmodified

- `Tenant.SepaCreditorIdentifier` (024) — batch creditor identifier.
- `Tenant.Name` — batch creditor name.
- `Location.BankAccountNumber` (014) — batch creditor IBAN.
- `Contract.SepaIbanEncrypted` / `SepaMandateReference` / `SepaAuthorisedAt` (024) — per-instruction
  debtor IBAN, mandate reference, mandate signing date.
- `Invoice.TotalCents` / `OgmReference` (014) — per-instruction amount, end-to-end ID.
- `ChildContacts` → `Contacts` primary-contact join (existing, used by `GenerateInvoicePdfQuery`) —
  per-instruction debtor name (research.md R6).

## State transitions

```
Invoice.Status:
  Sent ──(FR-002, batch generation)──> PendingDebit ──(FR-009, CODA match via 025)──> Paid
                                        PendingDebit ──(FR-010, returned debit)──> Sent

Contract SEPA mandate:
  (no mandate) ──(024, signing)──> signed (SepaAuthorisedAt set)
  signed ──(FR-011, revoke)──> revoked (SepaRevokedAt set)
  revoked ──(FR-012, new invitation + 024 signing)──> signed (new SepaMandateReference,
                                                                SepaRevokedAt cleared)
```

## Eligibility rule (FR-001), precisely

An invoice is batch-eligible when **all** of:
1. `Invoice.Status == Sent`
2. `Contract.SepaAuthorisedAt IS NOT NULL`
3. `Contract.SepaRevokedAt IS NULL`
4. `Invoice.TotalCents > 0`

Exclusion reason shown to the director (FR-001) is the first of (2)/(3)/(4) that fails, in that
priority order — "no mandate" takes precedence over "non-positive amount" since a missing mandate
is the more common and more actionable case for a director to see first.
