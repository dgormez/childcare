# Quickstart: Configurable Caregiver PIN

## Prerequisites

- Backend running locally (`dotnet run` in `ChildCare.Api`) against a tenant schema with at
  least one location and two staff profiles with PINs set (per feature 008a).
- Web admin (`npm run dev` in `web/`) and mobile app (`npx expo start` in `mobile/`) both able to
  reach the local backend.
- A director account and a room tablet already paired to the test location (per 008a's room
  setup flow).

## Scenario 1 — Director turns the requirement off

1. Log into the web admin as a director.
2. Navigate to `/locations/{id}` for the test location → open the "Inchecken" tab.
3. Confirm the toggle shows "required" (default) with tradeoff copy visible.
4. Turn the toggle off and save.
5. **Expected**: save succeeds, toggle now shows "off", tradeoff copy remains visible for
   context. `GET /api/locations/{id}` reflects `requiresCaregiverPin: false`.

## Scenario 2 — Caregiver checks in with no PIN step

1. On the paired room tablet (or `GET /api/room-shifts/roster` directly), confirm the roster
   response's top-level `requiresCaregiverPin` is now `false`.
2. Tap an unchecked-in caregiver's card.
3. **Expected**: no PIN keypad appears; the card immediately shows checked-in state and time.
4. Tap the same card again.
5. **Expected**: no PIN keypad appears; check-out completes immediately.

## Scenario 3 — Existing PIN-on location is unaffected

1. Repeat Scenario 2 against a different location whose setting was never changed
   (`requiresCaregiverPin: true` by default).
2. **Expected**: tapping a card still shows the PIN keypad exactly as before this feature shipped.

## Scenario 4 — BKR ratio and attribution parity

1. With the test location's setting off, check in two caregivers (no PIN).
2. Query the current staffing ratio for that location.
3. **Expected**: both caregivers count toward the ratio identically to a PIN-verified check-in.
4. Log a routine child event while both are checked in.
5. **Expected**: the event's recorded-by resolves to the checked-in caregiver(s) exactly as it
   would under PIN verification.

## Scenario 5 — Medical confirmation still requires PIN

1. With the test location's setting off, log a temperature or medication event.
2. **Expected**: the administrator-confirmation step still shows the PIN keypad (or the existing
   Skip option) — unaffected by the location's `requiresCaregiverPin` setting.

## Scenario 6 — Re-enabling preserves existing PINs

1. Turn the location's setting back on.
2. Check in a caregiver whose PIN was never changed during the off period.
3. **Expected**: their original PIN (set before this feature's testing began) still verifies
   successfully — no forced re-set.
