# Phase 1 Data Model: Caregiver App Kiosk Mode

All new tables/columns live in the tenant schema (constitution Principle I) — no changes to
`PublicDbContext`/the shared `tenants` table.

## `StaffProfile` (feature 005, extended)

New columns:

| Field | Type | Notes |
|---|---|---|
| `PinHash` | `string?` | bcrypt hash of the caregiver's 4-digit PIN. Null until a director sets one. Never the plaintext PIN, never logged. |
| `PinFailedAttempts` | `int` | Default `0`. Reset to `0` on success. On failure: if `PinFirstFailedAttemptAt` is null or more than 2 minutes ago, reset the streak (`= 1`, `PinFirstFailedAttemptAt = now`); otherwise increment. This anchors the 2-minute window to the *first* failure in the current streak, not a fixed clock-aligned window (spec FR-012 — prevents an attacker pacing exactly 4 attempts per fixed window indefinitely). |
| `PinFirstFailedAttemptAt` | `DateTime?` | Start of the current failure streak — see above. Cleared on success or once `PinLockedUntil` is set. |
| `PinLockedUntil` | `DateTime?` | Set when `PinFailedAttempts` hits 5 within its streak's 2-minute window; `VerifyPinCommand` rejects outright while this is in the future, returning the same locked-response shape regardless of which surface (check-in/out, sensitive-action confirmation) made the request (research.md R2). |

**Uniqueness rule** (application-level, not a DB constraint — a PIN's "location" scope is
derived from `StaffLocationEligibility`, feature 005, which can change over time): when a
director sets a PIN, `SetCaregiverPinCommand` checks every *other* caregiver eligible for any
location this caregiver is eligible for, and rejects if any of them already use the same PIN
hash comparison target. Since PINs are hashed, this is done by re-deriving/comparing against
stored hashes for the relevant candidate set — see `contracts/pin-management-api.md` for the
exact validation flow. (This candidate-set enumeration is specific to the *uniqueness check* at
PIN-assignment time — see below for why check-in-time verification needs no such enumeration.)

**Check-in-time verification is direct, not a search**: every PIN-verifying call
(check-in/check-out/administrator-confirmation) carries an explicit `staff_id` — the caregiver
taps their own photo card before entering a PIN (select-then-PIN, spec User Story 3/research.md
R6) — so `VerifyPinCommand` loads that one `StaffProfile` by id, confirms it's active and
eligible at the device's location (FR-004/024/025 — see research.md R6), and bcrypt-compares
directly against its `PinHash`. Because the target is always known up front, lockout is a
straightforward per-`StaffProfile` counter (the three fields above) — no candidate-set
iteration and no value-keyed lockout table are needed for this feature's current design (an
earlier draft, before select-then-PIN, needed both; see research.md R2/R6 for that history).

The director-override PIN (FR-005) is a separate, single-target comparison
(`DevicePairing.DirectorOverridePinHash`) and keeps its own lockout counter on `DevicePairing`
(see below) — unrelated to caregiver-PIN lockout.

## `RoomShift` (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `StaffProfileId` | `Guid` | FK → `StaffProfile`. |
| `LocationId` | `Guid` | FK → `Location` (feature 004). |
| `GroupId` | `Guid` | FK → `Group` (feature 006). |
| `DevicePairingId` | `Guid` | FK → `DevicePairing` — which tablet recorded this shift. |
| `CheckedInAt` | `DateTime` | UTC. |
| `CheckedOutAt` | `DateTime?` | UTC. Null = still open. Set by an explicit check-out, by auto-checkout (research.md R5), or by deactivation (FR-024)/re-pairing (FR-026). |
| `ClosedReason` | `string?` | `null` (explicit check-out) \| `"auto_checkout"` \| `"deactivated"` \| `"reassigned"` — lets a director's correction UI distinguish an intentional check-out from a system-closed one. |
| `CreatedAt` | `DateTime` | UTC. |

**Concurrency**: two simultaneous check-ins for the same room are both accepted (spec Edge
Cases) — there is no uniqueness constraint preventing multiple open `RoomShift` rows for
different `StaffProfileId`s at the same location/group. A single caregiver cannot have two
*open* shifts simultaneously across different rooms — enforced by `CheckInCommand` checking for
an existing open shift for that `StaffProfileId` before creating a new one (auto-closes the
prior one with `ClosedReason = "reassigned"` if found, consistent with FR-026's intent applied
per-caregiver).

**Manual correction audit trail (FR-023)**: `CorrectShiftCommand` does not write to a separate
audit table — there is no `AuditLog` entity anywhere in this codebase yet, and adding one is out
of scope for this feature. Instead it emits a structured `ILogger` entry (`RoomShiftId`,
`StaffProfileId`, `CorrectedByTenantUserId`, prior `CheckedInAt`/`CheckedOutAt`/`ClosedReason`,
new values, timestamp) — the same mechanism FR-021 already uses for logging rejected actions
from a revoked device, so both audit obligations are satisfied consistently without introducing
a second, heavier-weight logging path for this feature alone.

## `DevicePairing` (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK — this is the `device_id` claim embedded in the device token. |
| `TenantId` | `Guid` | Which organisation this tablet belongs to. |
| `LocationId` | `Guid` | |
| `GroupId` | `Guid` | |
| `DirectorOverridePinHash` | `string` | bcrypt hash of the 6-digit director-override PIN set during pairing. |
| `TokenIssuedAt` | `DateTime` | UTC — start of the current 30-day TTL window. |
| `TokenVersion` | `int` | Incremented on every rotation (research.md R3); embedded in the token so a stale, pre-rotation token is naturally rejected without needing a separate revocation-list entry per rotation. |
| `RevokedAt` | `DateTime?` | Null = active. Set by `RevokeDeviceCommand` (FR-021) — checked on *every* request (research.md R1's forwarder routes to the `DeviceToken` scheme's `OnTokenValidated` event, which does this lookup). |
| `PairedByTenantUserId` | `Guid` | Which director completed setup — audit trail. |
| `OverridePinFailedAttempts` | `int` | Default `0`. FR-005's *own*, separate lockout for the director-override PIN, unrelated to caregiver-PIN lockout — a single-target comparison against `DirectorOverridePinHash` on this row. Same sliding-window shape as `StaffProfile.PinFailedAttempts`, just a higher threshold (10) and longer window (30 min — spec FR-005). |
| `OverridePinFirstFailedAttemptAt` | `DateTime?` | Start of the current failure streak — see above. |
| `OverridePinLockedUntil` | `DateTime?` | Set at 10 failures within the streak's 30-minute window; `ExitRoomModeCommand` rejects outright while this is in the future. |
| `CreatedAt` | `DateTime` | UTC. |

## Device token claims

The device JWT (signed with a distinct signing key from the user-JWT key — research.md R1)
carries:

- `tenant_id` — read by `TenantMiddleware` unchanged.
- `device_id` — the `DevicePairing.Id`, used for the revocation-list lookup.
- `location_id`, `group_id` — scope for check-in/out and event-attribution queries.
- `token_version` — must match `DevicePairing.TokenVersion` or the token is rejected as stale
  (superseded by a rotation).
- Standard `iss`/`aud`/`exp` claims, `iss` distinct from the user-JWT issuer so R1's forwarder
  can route on it cheaply.

## `IShiftAttributionService` (reusable contract for 009/010)

```csharp
public interface IShiftAttributionService
{
    Task<IReadOnlyList<Guid>> ResolveRecordedByAsync(
        Guid locationId, Guid groupId, DateTime occurredAtUtc, CancellationToken ct);
}
```

Returns every `StaffProfileId` with a `RoomShift` open (`CheckedInAt <= occurredAtUtc &&
(CheckedOutAt == null || CheckedOutAt > occurredAtUtc)`) for that location/group at that
instant — zero, one, or more than one entry, per FR-015/016. Callers (future feature 009/010
command handlers) store the result as-is: empty array if nobody was checked in, one entry if
exactly one caregiver was, multiple if more than one.

## Room roster (read-time projection, not a stored entity)

`GetRoomRosterQuery` (research.md R7) returns every `StaffProfile` eligible at the device
token's `LocationId` (`StaffLocationEligibility`), joined with:

- `PhotoUrl` — from `IProfilePhotoStorage` (feature 005), or a client-rendered placeholder
  avatar if `ProfilePhotoObjectPath` is unset (FR-013).
- `CheckedIn: bool` and `CheckedInAt: DateTime?` — whether that caregiver has an open
  `RoomShift` for this device's location/group right now.

Powers the room home screen's photo-card grid (FR-013) and replaces the narrower "who's here"
list an earlier draft of this feature had — the roster always shows everyone, with checked-in
state as one of its fields, rather than a separate endpoint that only lists the checked-in
subset.
