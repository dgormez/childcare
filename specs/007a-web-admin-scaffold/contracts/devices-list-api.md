# Contract: Devices List API

## `GET /api/devices`

**Authorization**: `DirectorOnly`, tenant-scoped (standard `TenantMiddleware` resolution — no
`RequireTenantExempt`).

**Query parameters**: none. Returns all paired devices for the tenant, active and revoked alike
(the client distinguishes via `revokedAt`) — mirrors `GET /api/staff`'s default-includes-active
plus explicit deactivated flag isn't needed here since Devices is a small enough list that
showing revoked devices (visually distinguished) is more useful than hiding them, per spec User
Story 3's "disappears from (or is visually distinguished in) the active devices list."

**Response** `200 OK`:

```json
[
  {
    "id": "guid",
    "locationId": "guid",
    "locationName": "string",
    "groupId": "guid",
    "groupName": "string",
    "pairedByTenantUserId": "guid",
    "pairedByName": "string",
    "pairedAt": "2026-07-08T09:00:00Z",
    "revokedAt": null
  }
]
```

Empty tenant → `200 OK` with `[]` (never a 404) — matches every other list endpoint in this
codebase (`GET /api/staff`, `GET /api/locations`).

**Errors**: Standard `401`/`403` from `TenantMiddleware`/`DirectorOnly` if the caller isn't an
authenticated director of a ready tenant. No feature-specific error responses — this is a pure
read with no failure modes beyond auth.

## `POST /api/devices/{id}/revoke` (existing, feature 008a — unchanged)

Referenced here only for completeness: the Devices screen calls this existing endpoint. See
`specs/008a-caregiver-kiosk-mode/contracts/device-pairing-api.md` for its full contract. No
changes made by this feature.
