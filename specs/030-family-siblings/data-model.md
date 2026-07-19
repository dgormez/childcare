# Data Model: Family Siblings

No new tables. This feature extends three existing entities and one existing enum; every change
is additive/nullable-default so an unconfigured location or existing row is unaffected
(spec.md SC-005).

## `Location` (tenant schema) — extended

Existing per-location settings block (014/013f/014a pattern continues):

| Field | Type | Default | Notes |
|---|---|---|---|
| `SiblingDiscountPct` | `decimal(5,2)` | `0` | 0 = no discount (current behavior). Applied per R3: to every child in a same-primary-contact group except the earliest-contract-start child. |
| `FamilyInvoiceBundlingEnabled` | `bool` | `false` | Off = one invoice per child (current behavior, unchanged). |

## `Invoice` (tenant schema) — extended

| Field | Type | Default | Notes |
|---|---|---|---|
| `FamilyGroupId` | `Guid?` | `null` | Null unless generated while `FamilyInvoiceBundlingEnabled` was true for that location/period and the child had 2+ siblings sharing the same primary contact (R4). Shared across every invoice in the same bundle; stable across regeneration of the same still-open group. |

No change to `ChildId`/`ContractId`/`LocationId`/`PeriodMonth`/`LineItems` semantics — a
discount, when applicable, is expressed as an additional entry in the existing
`InvoiceLineItems.ExtraCharges` list (already-supported shape, negative `AmountCents`,
i18n-keyed label e.g. `invoices.lineItems.siblingDiscount`), not a new field.

## `ContactRelationship` (enum) — extended

Existing: `Mother, Father, Guardian, EmergencyContact, AuthorisedPickup`.

Added (appended, additive per R6): `FosterParent`, `Other`.

## `ChildContact` (tenant schema) — unchanged

Already the many-to-many "family membership" junction (`ChildId`, `ContactId`, `Relationship`,
`CanPickup`, `IsPrimary` with existing at-most-one-primary-per-child enforcement). This feature
surfaces it in the web admin UI (no schema change) and uses its existing `IsPrimary` flag as the
grouping key for sibling discount/bundling (R3/R4).

## `Child` (tenant schema) — unchanged

`DeactivatedAt` already exists and already means what spec.md's "previous children" concept
needs (R8) — no change.

## New read models (not stored — response shapes only)

- **`FamilyInvoiceResponse`**: `FamilyGroupId`, list of `{ ChildId, ChildName, LineItems,
  SubtotalCents }`, combined `TotalCents`, shared `DueDate`/`Status`/`OgmReference`-per-invoice
  (each grouped invoice keeps its own `OgmReference` for payment-matching continuity — the PDF
  surfaces all of them). Produced by mapping N grouped `Invoice` rows, not a new table.
- **`ParentPreviousChildResponse`**: same shape as `ParentChildResponse` (id, name, photo, DOB)
  plus an enrollment-period summary (earliest contract start → `DeactivatedAt`), for the
  "previous children" list (R8).

## Validation rules (new)

- `SiblingDiscountPct`: `0–100` inclusive.
- Bulk day-reservation `ChildIds`: non-empty, every id must be one of the requesting parent's
  own active linked children (same check `SubmitDayReservationCommand` already performs per
  child — enforced per-child, not just at the list level, so a forged id in the list fails only
  that entry, not the whole batch).
- `LinkContactToChildCommand`/`UpdateChildContactLinkCommand`: `Relationship` must parse to a
  valid `ContactRelationship` including the two new values — existing validator behavior,
  unchanged.
