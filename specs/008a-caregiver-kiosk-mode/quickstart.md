# Quickstart: Caregiver App Kiosk Mode

## Prerequisites

- Backend running locally, Development environment.
- Org A registered, a location + group created, a director account.
- A caregiver `StaffProfile` (feature 005) eligible for that location, no PIN set yet.
- `cd mobile && npm install` (no new dependencies expected — reuses feature 008's stack).

## Scenario 1 — Director pairs a tablet (User Story 1)

1. Fresh install, launch the app → lands on feature 008's email/password screen.
2. Sign in as the director, select the location and group, set a 6-digit override PIN.
3. Confirm the app enters room mode: shows the room home screen (photo-card roster, all
   not-checked-in).
4. Force-quit and relaunch → still in room mode for the same location/group, no
   re-authentication (FR-002 equivalent for device tokens).
5. Enter the override PIN → confirm the app exits back to the email/password screen.

## Scenario 2 — Director sets a caregiver PIN (User Story 2)

1. From the web admin's staff screen, set a 4-digit PIN for the caregiver from Prerequisites.
2. Attempt to set the *same* PIN for a second caregiver eligible at the same location → confirm
   it's rejected.
3. Reset the first caregiver's PIN to a new value → confirm the old one no longer works at
   check-in and the caregiver's account password is unaffected.

## Scenario 3 — Check-in/check-out, including two at once (User Story 3)

1. On the paired tablet, load the room roster → confirm every location-eligible caregiver
   appears as a photo card, none checked in.
2. Tap caregiver A's card, enter A's correct PIN → confirm A's card switches to checked-in with
   a check-in time.
3. Tap caregiver B's card (different caregiver, same location), enter B's PIN → confirm both A
   and B show as checked in simultaneously.
4. Tap A's (now checked-in) card, enter A's PIN again → confirm A is checked out; B remains
   checked in.
5. Tap A's card and enter an incorrect PIN 5 times within 2 minutes → confirm a 10-minute
   lockout for A specifically, and that B's card is entirely unaffected.

## Scenario 4 — Device-token-only authenticated actions (User Story 4)

1. With nobody checked in, call `POST /api/room-shifts/check-in` with a valid device token and
   caregiver A's `staffId`/PIN — confirm it succeeds with no additional HTTP-layer auth beyond
   the device token (the PIN is request-body content proving *presence*, not a second
   credential — proves the device token alone is sufficient auth).
2. With exactly caregiver A checked in, exercise `IShiftAttributionService` (integration test,
   not a UI step) for an `occurredAt` during A's open shift → confirm it resolves to `[A]`.
3. With both A and B checked in, exercise it again → confirm it resolves to `[A, B]`.
4. With a revoked or missing device token, confirm any write is rejected regardless of
   check-in state.
5. Call `POST /api/room-shifts/check-in` with a `staffId` belonging to a caregiver eligible only
   at a *different* location than this device's own → confirm `403
   errors.staff.not_eligible_here`, regardless of whether the submitted PIN is correct (FR-004).

## Scenario 5 — Sensitive-action PIN confirmation (User Story 5)

1. With A checked in, call `POST /api/room-shifts/confirm-administrator` with
   `{ staffId: A, pin: <A's correct PIN> }` → confirm `administeredByStaffProfileId` is set to A.
2. Call it again with `{ skip: true }` → confirm it succeeds with `administeredByStaffProfileId:
   null`.
3. Call it with a caregiver C's `staffId` who is valid/eligible but not currently checked in →
   confirm `409 errors.room_shifts.not_checked_in` regardless of whether C's PIN is correct.
4. Fail A's PIN twice on this endpoint, then fail it 3 more times on check-in → confirm the 5th
   failure (across both surfaces, same `staffId`) triggers the lockout (proves the shared
   counter, research.md R2).

## Scenario 6 — Silent device-token rotation (User Story 6)

1. Issue a device token artificially close to its 7-day rotation threshold (test seam, not a
   UI step).
2. Make an authenticated request → confirm the response carries `X-Device-Token-Refresh` and
   the old token is no longer valid for a *new* request afterward.
3. Simulate a burst of already-in-flight requests using the pre-rotation token (offline-queue
   replay) → confirm they all still succeed.

## Scenario 7 — Device revocation (User Story 7)

1. Revoke the paired tablet's device from the web admin.
2. Make any authenticated request from that tablet's token → confirm `401 device.revoked`
   immediately, not after the token's normal 30-day expiry.
3. Confirm the mobile app clears `SecureStore` and returns to the pairing/setup flow.

## Automated coverage

Backend: `DevicePairingTests.cs`, `RoomShiftTests.cs` (check-in/out, lockout, concurrent
caregivers), `ShiftAttributionServiceTests.cs`, `PinManagementTests.cs` — all TestContainers-backed.
Mobile: `__tests__/services/deviceAuth.test.ts`, `__tests__/screens/room-setup.test.tsx`,
`__tests__/screens/room-home.test.tsx` — see `tasks.md` for the concrete breakdown.
