# Data Model: Fiscal Attestations

## `FiscalAttestation` (tenant schema, table `fiscal_attestations`)

One row per (ChildId, LocationId, TaxYear). Regenerating (FR-008) updates this same row in place
— never a new row for the same key (unique index enforces this).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. Also the deterministic GCS object-path key (`fiscal-attestations/{Id}.pdf`) — regenerate overwrites the same object, mirroring `GcsProfilePhotoStorage`'s deterministic-path precedent. |
| `ChildId` | `Guid` | FK → `children`. |
| `LocationId` | `Guid` | FK → `locations`. Per research.md R6 — one row per location the child had `Paid` invoices at that year. |
| `TaxYear` | `int` | Calendar year (spec.md Assumptions). |
| `Periods` | `string` (JSON text) | Up to 4 entries, shape below (`FiscalAttestationPeriods`) — raw JSON validated in the Application layer, mirroring `Invoice.LineItems`' existing precedent (not a JSONB EF value converter). |
| `TotalAmountCents` | `int` | Sum of all periods' `AmountCents`. |
| `PdfObjectPath` | `string` | GCS object path, always `fiscal-attestations/{Id}.pdf` — stored explicitly (not recomputed from `Id`) so a future path-scheme change doesn't strand existing rows. |
| `GeneratedAt` | `DateTime` | Set on generate and on every regenerate (overwritten, not appended). |
| `CreatedAt` | `DateTime` | Set once, at first generation. |
| `UpdatedAt` | `DateTime` | Set on every regenerate. |

**Indexes**: unique on (`ChildId`, `LocationId`, `TaxYear`) — enforces "one row per child +
location + tax year" (R6) and is the natural lookup key for both the director list view and the
regenerate command.

### `FiscalAttestationPeriods` (JSON shape, Application layer)

```csharp
public record FiscalAttestationPeriod(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Days,
    int AmountCents,
    int? DailyRateCents); // null only for a >4-period consolidation merge (research.md R3 step 4)
```

Serialized as a JSON array of up to 4 `FiscalAttestationPeriod` entries, camelCase — same
serialization convention as `Invoice.LineItems`/`InvoiceLineItems`.

## Relationships

- `FiscalAttestation.ChildId` → `Child.Id` (existing entity, 006).
- `FiscalAttestation.LocationId` → `Location.Id` (existing entity, 004) — supplies
  `Name`/`Address`/`Erkenningsnummer` for the PDF (research.md R6).
- No direct FK to `Invoice` or `Contract` — the periods are a computed aggregate snapshot
  (research.md R3), not a live reference; this is deliberate (R1: a filed tax document must not
  silently change if a referenced invoice is later edited — only an explicit regenerate updates
  it).
- `Tenant.KboNumber` (org-wide, existing) supplies the PDF's KBO/ondernemingsnummer field —
  no new field needed.

## No new field needed elsewhere

`Child` (FirstName/LastName/DateOfBirth), `Contact` (FirstName/LastName/Locale, via
`ChildContact`), `Location` (Name/Address/Erkenningsnummer), and `Tenant` (Name/KboNumber) already
carry every field the PDF needs (spec.md FR-006) — confirmed during research (plan.md's Technical
Context / research.md).

## State / lifecycle

`FiscalAttestation` has no status enum — its existence for a given (Child, Location, TaxYear) key
*is* the "generated" state; a missing row for an eligible child is the "not yet generated" state
(surfaced by joining the eligible-children set against existing rows, not a stored status column).
Regeneration is an in-place overwrite (`GeneratedAt`/`UpdatedAt`/`Periods`/`TotalAmountCents`/PDF
bytes all replaced); there is no "failed" persisted state — a failed generation simply leaves no
row (or leaves the prior row untouched, for a failed regenerate attempt), and the director-facing
list computes "failed" transiently for the current request only (spec.md FR-010/FR-012), not as
stored data.
