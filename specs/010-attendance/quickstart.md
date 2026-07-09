# Quickstart: Daily Attendance Registration

## Prerequisites

- Backend running locally against Docker PostgreSQL (unchanged dev setup).
- A tenant with at least one location, one group, one active child with an active contract
  covering today's weekday, and one qualified staff/caregiver profile (features 004/005/006/007
  data) — reuse an existing dev-seeded tenant.
- A paired kiosk device (feature 008a room setup already completed) with at least one qualified
  caregiver checked in via `/room-shifts/check-in`.

## Backend validation (curl, device-token authenticated)

1. Check a child in:
   ```bash
   curl -X POST "$API/api/attendance/check-in" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childId":"'$CHILD_ID'","locationId":"'$LOCATION_ID'","groupId":"'$GROUP_ID'","date":"'$(date -u +%F)'"}'
   ```
   Expect `201` with `status: "present"`, `checkInAt` set, `plannedDurationMinutes` matching the
   child's contracted hours for today's weekday, and `recordedBy` populated from the
   currently-checked-in caregiver(s).

2. Attempt a duplicate check-in for the same child/location/date — expect `409
   errors.attendance.already_recorded`.

3. Check the child back out:
   ```bash
   curl -X POST "$API/api/attendance/check-out" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childId":"'$CHILD_ID'","locationId":"'$LOCATION_ID'","date":"'$(date -u +%F)'"}'
   ```
   Expect `200` with `checkOutAt` set, `status` unchanged (`present`).

4. Read the live BKR ratio:
   ```bash
   curl "$API/api/attendance/bkr?locationId=$LOCATION_ID" -H "Authorization: Bearer $DEVICE_TOKEN"
   ```
   Expect `presentCount`/`qualifiedStaffCount`/`threshold`/`status` reflecting the seeded data.
   Check a child in on a room tablet with only one qualified caregiver checked in until
   `presentCount` exceeds 8 — expect `status: "red"`.

5. Mark a different child absent and justified:
   ```bash
   curl -X POST "$API/api/attendance/absence" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childId":"'$CHILD_ID_2'","locationId":"'$LOCATION_ID'","date":"'$(date -u +%F)'","absenceJustified":true,"absenceReason":"Sick"}'
   ```
   Expect `201`; re-check the BKR ratio and confirm this child is excluded from `presentCount`.

6. Verify the same-day edit window: attempt a `PATCH` on a record with `date` set to yesterday
   using a caregiver-scoped (device token) call — expect `403
   errors.attendance.edit_window_expired`. Repeat with a director JWT — expect `200`.

7. Verify the extra-day case: check in a child whose contract doesn't cover today's weekday —
   expect `201` with `plannedDurationMinutes: null`.

8. Verify the absent→present transition: mark a child absent, then check them in — expect `200`
   (not `409`) with `status: "present"` and `checkInAt` set.

9. Verify check-out idempotency: check a child out twice in a row — expect the second call to
   return `404 errors.attendance.not_found`, and the first `checkOutAt` value unchanged.

## Mobile validation (Expo caregiver app, manual)

1. Open the app on a paired kiosk device with a qualified caregiver checked in.
2. From the group view, tap an unchecked child's card — confirm it visually updates to present
   with a check-in time, in a single tap.
3. Tap the same card again — confirm check-out is recorded and reflected visually.
4. Confirm the BKR indicator is visible, colour-coded, and paired with a text label (not colour
   alone) — check enough children in to breach the ratio and confirm it turns red without
   blocking further check-ins.
5. Enable airplane mode. Check a child in — confirm it appears immediately marked "pending sync."
   Disable airplane mode — confirm it syncs automatically.
6. Attempt to trigger a duplicate check-in from two tablets (or two rapid taps) for the same
   child/day — confirm only one record results and no error surfaces destructively to the
   caregiver.
7. Mark a child absent via the separate absence action — confirm it requires a distinct,
   deliberate interaction from the one-tap check-in gesture.

## Director web validation (manual)

1. Open the attendance correction/history screen for a location.
2. Find a record with a missing check-out from a prior day; correct it — confirm the update
   succeeds regardless of the record's age.
3. Attempt the same correction while signed in as a caregiver (device token) on a prior-day
   record — confirm it is rejected.

## Expected test coverage (backend, TestContainers per constitution Principle V)

- Happy-path check-in/check-out/absence create.
- Unique-constraint conflict: duplicate check-in for the same child/location/date returns `409`.
- BKR threshold boundary cases: 8/9 (solo/2+, non-nap), 14 (nap-time inferred from open sleep
  events), zero-qualified-staff breach.
- `planned_duration_minutes` derivation: matching contracted day, and the null "extra day" case.
- Same-day (caregiver, device-location match) vs any-day (director) correction authorization.
- `StudentVolunteer` staff excluded from the BKR qualified-staff count.
- `closure`-status records reject a manual check-in attempt.
