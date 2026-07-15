# Data Model: Invoicing

## `Invoice` (new, tenant schema, table `invoices`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key, as every other entity in this codebase. |
| `SequenceNumber` | `long` | Tenant-schema-scoped identity column (`ValueGeneratedOnAdd`). Feeds the OGM base number only — never used as a foreign key or exposed as "the" invoice identifier (research.md R3). |
| `ChildId` | `Guid` | FK to `Child`. |
| `ContractId` | `Guid` | FK to `Contract` — the specific contract this invoice bills against. |
| `LocationId` | `Guid` | FK to `Location`. |
| `PeriodMonth` | `DateOnly` | First-of-month date, e.g. `2027-07-01` for July 2027. |
| `Status` | `InvoiceStatus` enum (`Draft`/`Sent`/`Paid`) | `Overdue` is never stored — computed as `Status == Sent && DueDate < today` (research.md R4). |
| `SubtotalCents` | `int` | Billable-day total before extra charges. |
| `TotalCents` | `int` | `SubtotalCents` + sum of `LineItems.ExtraCharges`. |
| `LineItems` | `jsonb` → `InvoiceLineItems` | See shape below. |
| `OgmReference` | `string` | `+++XXX/XXXX/XXXXX+++`, derived from `SequenceNumber` (research.md R3). Unique. |
| `DueDate` | `DateOnly` | `CreatedAt`'s date + `Location.InvoiceDueDays` at generation time (spec.md FR-005a). |
| `SentAt` | `DateTime?` | Null until sent. |
| `PaidAt` | `DateTime?` | Null until paid; the director-recorded payment date. |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

**Unique index**: `(ChildId, ContractId, LocationId, PeriodMonth)` — spec.md FR-003.

**`InvoiceLineItems` (JSONB shape)**:

```json
{
  "presentDays": 18,
  "unjustifiedAbsentDays": 1,
  "dailyRateCents": 3500,
  "closureDaysExcluded": 2,
  "daysMin5u": 15,
  "daysMin11u": 4,
  "extraCharges": [
    { "label": "Registration fee", "amountCents": 2500 }
  ]
}
```

`daysMin5u`/`daysMin11u` are duration-categorized present-day counts (spec.md FR-019),
derived from `AttendanceRecord.PlannedDurationMinutes` at ≥5h/≥11h thresholds — stored now,
consumed by nothing yet (future Belcotax/Opgroeien reporting compatibility).

**State transitions**:

```text
Draft --(send)--> Sent --(mark paid)--> Paid
  ^                 |
  |                 v
  +---(regenerate)--+   (regenerate on Draft or Sent only; Paid is immutable — FR-012)
```

## `Location` (existing, extended)

Adds, alongside the existing `NaamLocatie`/`Dossiernummer`/`Verantwoordelijke`
Opgroeien-reporting fields:

| Field | Type | Notes |
|---|---|---|
| `Erkenningsnummer` | `string?` | Childcare license number, per-location. |
| `BankAccountNumber` | `string?` | IBAN the location is paid into, printed on the PDF. |
| `InvoiceDueDays` | `int` | Default `14`. Offset added to an invoice's generation date to compute `DueDate` (spec.md FR-005a). |

## `Tenant` (existing, public schema, extended)

| Field | Type | Notes |
|---|---|---|
| `KboNumber` | `string?` | Belgian company registration number — one per legal entity, regardless of location count. |

## Existing entities read (unchanged)

- **`Contract`** (007): `DailyRateCents`, `StartDate`, `EndDate`, `ContractedDays`,
  `LocationId`, `ChildId`. Source of the daily rate and the active-date-range restriction on the
  billable-day computation.
- **`AttendanceRecord`** (009/010): `Status` (`Present`/`Absent`/`Closure`), `AbsenceJustified`,
  `PlannedDurationMinutes`, `Date`, `ChildId`, `LocationId`. The sole input to
  `BillableDayCalculator` (research.md R2) — `DayReservation` and `ClosureDay` are not queried
  directly at invoicing time since their effects are already reflected here.
- **`Child`**: `FirstName`/`LastName` for the invoice/PDF display name.
- **`ChildContact`**/parent-contact-resolution (existing pattern, 003/009): resolves which
  parent(s) an invoice is visible to and notified for.

## Billable-day computation (`BillableDayCalculator`)

For a given `(ChildId, ContractId, LocationId, PeriodMonth)`:

1. Compute the effective date range: `[max(Contract.StartDate, PeriodMonth start),
   min(Contract.EndDate ?? PeriodMonth end, PeriodMonth end)]`.
2. Load every `AttendanceRecord` for that `(ChildId, LocationId)` within the effective range.
3. `presentDays` = count where `Status == Present`.
4. `unjustifiedAbsentDays` = count where `Status == Absent && AbsenceJustified == false`.
5. `closureDaysExcluded` = count where `Status == Closure` (reported for the line-item
   breakdown; already excluded from the two counts above by construction).
6. `daysMin5u`/`daysMin11u` = subsets of `presentDays` where `PlannedDurationMinutes >= 300` /
   `>= 660` respectively.
7. `subtotalCents` = `(presentDays + unjustifiedAbsentDays) * Contract.DailyRateCents`.

A day with no `AttendanceRecord` row at all contributes to none of the above counts (research.md
R2's "open question" — not billed, matches the literal FR-002 wording).

## OGM reference generation (`OgmReferenceGenerator`)

1. `SequenceNumber` (from the just-inserted `Invoice` row) zero-padded to 10 digits → `base`.
2. `check = base mod 97`; if `check == 0`, `check = 97`.
3. Format as `+++{base[0..3]}/{base[3..7]}/{base[7..10]}{check:D2}+++`.

Deterministic given `SequenceNumber`; uniqueness follows from `SequenceNumber`'s own uniqueness
(identity column), not from anything the checksum itself guarantees.
