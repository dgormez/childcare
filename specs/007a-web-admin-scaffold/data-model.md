# Data Model: Web Admin Scaffold

This feature introduces **no new database tables or columns**. It reads existing tenant-schema
entities (`StaffProfile`, `DevicePairing`, `Location`, `Group`, `TenantUser`) and the shared
`Tenant` entity (public schema), and adds two response DTOs plus one field on an existing DTO to
expose data that already exists but is not yet returned by any endpoint.

## Existing entities consumed (no changes)

- **StaffProfile** (feature 005): `Id`, `FirstName`, `LastName`, `Role`, `DeactivatedAt`,
  eligible location ids — rendered in the Staff table.
- **DevicePairing** (feature 008a): `Id`, `LocationId`, `GroupId`, `PairedByTenantUserId`,
  `CreatedAt` (paired-at), `RevokedAt` — rendered in the Devices table.
- **Location** (feature 004): `Id`, `Name` — resolved to display names for both Staff (eligible
  locations) and Devices (paired location) rows.
- **Group** (feature 006): `Id`, `Name`, `LocationId` — resolved to display names for Devices rows.
- **TenantUser** (feature 003): `Id`, `Name`, `Email`, `Role` — source of the signed-in
  director's display name (FR-005a) and of "paired by" director names on the Devices screen.
- **Tenant** (feature 001, public schema): `Id`, `Name` — source of the organisation display
  name (FR-005a).

## New/changed response contracts

### `AuthenticatedUser` (extended — `ChildCare.Contracts.Responses.AuthSessionResponse.cs`)

| Field | Type | Change |
|---|---|---|
| `Id` | `Guid` | unchanged |
| `Email` | `string` | unchanged |
| `EmailVerified` | `bool` | unchanged |
| `Role` | `string` | unchanged |
| `Name` | `string` | **new** — populated from `TenantUser.Name` in all four auth handlers |

No validation rules — `Name` is already required/non-null on `TenantUser` (set at
registration/invitation time by features 001/005).

### `OrganisationResponse` (new — `ChildCare.Contracts.Responses.OrganisationResponse.cs`)

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` | The tenant's display name, from `Tenant.Name`. |

Returned by `GET /api/organisations/me`. No identifiers beyond name are exposed — the client
already knows its own tenant context implicitly via the JWT; this endpoint exists purely to
surface the one display field the client cannot derive itself.

### `DeviceSummaryResponse` (new — `ChildCare.Contracts.Responses.RoomShiftResponses.cs`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Device/pairing id (used as the revoke-action target). |
| `LocationId` | `Guid` | |
| `LocationName` | `string` | Resolved from `Location.Name`. |
| `GroupId` | `Guid` | |
| `GroupName` | `string` | Resolved from `Group.Name`. |
| `PairedByTenantUserId` | `Guid` | |
| `PairedByName` | `string` | Resolved from `TenantUser.Name`. |
| `PairedAt` | `DateTime` | From `DevicePairing.CreatedAt`. |
| `RevokedAt` | `DateTime?` | Null = active. Included so the client can visually distinguish a revoked device without a separate call. |

Returned as a list by `GET /api/devices`. No state-transition fields (no "revoking" intermediate
state) — revocation is immediate and atomic, matching feature 008a's `RevokeDeviceCommand`.

## Frontend view models (TypeScript, `web/`, derived from generated OpenAPI types — not new API
contracts)

- **StaffRow**: derived from `StaffResponse` (feature 005) joined client-side against
  `GET /api/locations` (feature 004) to resolve `EligibleLocationIds` → display names for the
  table's "location(s)" column. No new backend join is introduced — this mirrors how the
  existing `StaffResponse` already stores ids, not names, by design (see feature 005's
  shipped-notes).
- **DeviceRow**: `DeviceSummaryResponse` mapped 1:1 to table rows; no additional client-side
  joins needed since the new endpoint already resolves names server-side (kept consistent with
  how `ListStaffQuery` resolves photo URLs server-side rather than pushing resolution to the
  client).

## State transitions

None introduced by this feature. Existing state transitions this UI merely *triggers* (already
enforced server-side, unchanged by this feature):

- Staff: active ↔ deactivated (feature 005's `DeactivateStaffProfileCommand`/
  `ReactivateStaffProfileCommand`).
- PIN: unset → set → reset (feature 008a's `SetCaregiverPinCommand`/`DeleteCaregiverPinCommand`).
- Device: active → revoked, one-way (feature 008a's `RevokeDeviceCommand` — `RevokedAt` is never
  cleared once set).
