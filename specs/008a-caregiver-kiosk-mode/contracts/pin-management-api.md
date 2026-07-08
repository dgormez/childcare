# Contract: Caregiver PIN Management (feature 005's staff screen, extended)

Auth: standard user JWT, `DirectorOnly` — this is a web-admin operation, unrelated to the
device-token layer.

## `PUT /api/staff/{staffProfileId}/pin`

Request: `{ pin: string }` (exactly 4 digits).

Validation:
- `pin` MUST be 4 numeric digits.
- Uniqueness (data-model.md's "Uniqueness rule"): rejected `409 errors.pin.not_unique_at_location`
  if any other caregiver eligible for any location this caregiver is eligible for
  (`StaffLocationEligibility`, feature 005) already holds this same PIN.

Effect: sets `StaffProfile.PinHash` (bcrypt), and resets `PinFailedAttempts`/
`PinFirstFailedAttemptAt`/`PinLockedUntil` to their defaults — lockout is a per-`StaffProfile`
counter (data-model.md), so assigning a new PIN value naturally starts that caregiver with a
clean lockout state. Does **not** touch the caregiver's account password (FR-008) — a
completely separate credential.

Response `204`.

## `DELETE /api/staff/{staffProfileId}/pin`

Clears `PinHash` (caregiver can no longer check in until a director sets a new one). Used when
offboarding or when a director wants to force a PIN reset without immediately choosing a new
value.

Response `204`.

## Interaction with deactivation (FR-024)

When a `StaffProfile` is deactivated, `DeactivateStaffProfileCommandHandler`
(`backend/ChildCare.Application/Staff/DeactivateStaffProfileCommand.cs`, feature 005) gains a
new side effect: it closes any open `RoomShift` for that caregiver immediately (`ClosedReason =
"deactivated"`). Separately, `VerifyPinCommand` checks `StaffProfile.DeactivatedAt`/eligibility
*before* the PIN comparison (research.md R6) and rejects `403 errors.staff.not_eligible_here`
outright — a deactivated caregiver's `staff_id` is rejected regardless of whether the PIN they
submit would otherwise be correct.
