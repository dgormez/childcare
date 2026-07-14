# Data Model: Platform-Admin Vaccine Catalog Management

## `TenantUser` (extended — tenant schema, `TenantDbContext`)

| Field | Type | Notes |
|---|---|---|
| `IsPlatformAdmin` | `bool` | **NEW**. Default `false`. Set only via the `grant-platform-admin` CLI command (research.md R3) — no in-app write path anywhere, including this feature's own UI. |

No other changes to `TenantUser`.

## `VaccineType` (extended — public schema, `PublicDbContext`, introduced by 013g)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Existing (013g). |
| `Name` | `string` | Existing (013g). Now editable by a platform-admin (FR-005). |
| `Category` | `VaccineCategory?` | Existing (013g). Now editable by a platform-admin (FR-005). |
| `SortOrder` | `int` | Existing (013g). Now editable by a platform-admin (FR-006, up/down reorder). |
| `IsActive` | `bool` | Existing (013g), default `true`. Now toggleable by a platform-admin (FR-007). |
| `CreatedAt` | `DateTime` | Existing (013g). |
| `UpdatedAt` | `DateTime` | Existing (013g). Bumped on any platform-admin edit. |
| `DeactivatedByUserId` | `Guid?` | **NEW**. No DB-level FK (research.md R2 — cross-schema reference, mirrors 013g's own `VaccineRecord.VaccineTypeId` precedent). Null when `IsActive == true`. |
| `DeactivatedByEmail` | `string?` | **NEW**. Denormalized at deactivation time (research.md R2). Null when `IsActive == true`. |
| `DeactivatedAt` | `DateTime?` | **NEW**. Null when `IsActive == true`. |

**State transitions**: `IsActive: true → false` (deactivate) populates all three new fields from
the acting platform-admin's identity/clock. `IsActive: false → true` (reactivate) clears all
three back to null. A subsequent deactivate populates them fresh (no history retained beyond the
current state — spec.md FR-008, research.md R2).

**Invariant** (enforced by every command handler that touches these fields, in a single DB
transaction per action — spec.md FR-011): `IsActive == true` if and only if all three audit
fields are null; `IsActive == false` if and only if all three are populated. No handler may leave
the row in a state where these disagree, even transiently across a failure — each of
Deactivate/Reactivate either fully applies (entity flag + all three audit fields) or fully fails
(no partial write), which the existing EF Core `SaveChangesAsync` per-request-scope transaction
already guarantees without any extra transaction-management code.

**Validation** (FluentValidation, mirrors 013g's existing `VaccineType` seed constraints):
- `Name`: required, max length matching 013g's existing column constraint.
- `Category`: must be a valid `VaccineCategory` enum value or null (matches 013g).
- `SortOrder`: non-negative integer.

**Unaffected**: 013g's `GET /api/vaccine-types` response shape and behavior (FR-010) — the three
new audit fields are platform-admin-management-view-only, never serialized into the existing
tenant-facing read endpoint's response.

## No new entities

This feature extends two existing entities (`TenantUser`, `VaccineType`) and adds no new tables.
