# Contract: Device Pairing & Lifecycle

## `POST /api/devices/pair`

Auth: standard user JWT, `DirectorOnly` (feature 008's existing auth — a director signs in with
email/password first, same as today).

Request: `{ locationId: Guid, groupId: Guid, directorOverridePin: string }` (6-digit override
PIN, set here, not reused from anywhere else).

Response `200`: `{ deviceId: Guid, deviceToken: string, tokenVersion: int }`. The mobile client
stores `deviceToken` in `SecureStore` and never needs the director's original user JWT again —
`deviceToken` is the credential for everything from this point on (FR-002).

Every call creates a fresh `DevicePairing` row — pairing is a one-shot, client-driven flow (the
mobile app only ever reaches the pairing screen when it has no stored device token yet — T025)
rather than something the server can idempotency-guard without a client-supplied request key,
which this endpoint's request shape doesn't carry. A genuine double-submit (e.g. a retried
network request) would create two `DevicePairing` rows for one physical tablet; the mobile
client stores whichever response it receives last, so this is a latent-but-harmless duplicate
row, not a functional bug — deliberately not solved here (out of scope for this feature; would
need an idempotency key added to the request shape to fix properly).

## `POST /api/devices/{deviceId}/revoke`

Auth: standard user JWT, `DirectorOnly`.

Effect: sets `DevicePairing.RevokedAt = now()`. The *next* request from that device's token —
regardless of remaining TTL — is rejected `401 device.revoked` (FR-021, checked on every
request via the `OnTokenValidated` revocation-list lookup, research.md R1).

Response `204`.

## `POST /api/devices/exit-room-mode` (director override)

Auth: `DeviceToken` scheme + `directorOverridePin` in the request body.

Effect: verifies the override PIN against `DevicePairing.DirectorOverridePinHash`. On success,
returns a signal the mobile app uses to exit kiosk mode locally and return to feature 008's
email/password screen — the device token itself is **not** revoked by this call (the tablet may
be re-paired to the same or a different room afterward without a director needing to
re-authenticate from scratch if they're re-pairing the same tablet immediately).

Response `200`: `{ ok: true }`. Response `401`: `errors.devices.invalid_override_pin` — this PIN
verification is **not** subject to the shared caregiver-PIN lockout (research.md R2/spec
Clarifications) — a 6-digit director PIN has far higher entropy and a different threat model
(used rarely, by a director, not guessed at by a curious child). It has its own, separate
10-attempts/30-minute lockout (FR-005) tracked directly on this `DevicePairing` row
(`OverridePinFailedAttempts`/`OverridePinLockedUntil` — data-model.md), unrelated to and
independent from caregiver-PIN lockout on `StaffProfile`. Response `423` (Locked):
`errors.devices.override_pin_locked` with `{ lockedUntil: string }` while that lockout is
active.

## Rotation (not a distinct endpoint)

Applied via an `IEndpointFilter` on the `DeviceToken`-authorized route group (research.md R3).
Any response from a device-authenticated request MAY include header
`X-Device-Token-Refresh: <new-device-jwt>` when the presented token has fewer than 7 days of
validity left. Mobile client behavior: if present, store the new token immediately, replacing
the old one; continue using the *request's original* token for that one in-flight call (the new
token takes effect starting with the *next* call) — this is what keeps an offline-queue replay
burst safe (research.md R3/spec FR-020).

## Expired / stale token

Any device-authenticated request with an expired token, or a `token_version` that doesn't match
`DevicePairing.TokenVersion` (superseded by a rotation the client missed — e.g. it was offline
across a rotation boundary and reconnects with a stale cached token), returns `401
device.token_expired`. Mobile client behavior: clear all `SecureStore` credentials and tenant
cache, return to the pairing/setup flow (FR-022).
