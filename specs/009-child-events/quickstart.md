# Quickstart: Child Event Timeline

## Prerequisites

- Backend running locally against Docker PostgreSQL (`backend/scripts` dev setup, unchanged from
  prior features).
- A tenant with at least one location, one group, one active child, and one staff/caregiver
  profile (features 004/005/006 data) — reuse an existing dev-seeded tenant.
- A paired kiosk device (feature 008a room setup already completed) with at least one caregiver
  checked in via `/room-shifts/check-in`.

## Backend validation (curl, device-token authenticated)

1. Record a routine event:
   ```bash
   curl -X POST "$API/api/child-events" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childId":"'$CHILD_ID'","eventType":"diaper","occurredAt":"'$(date -u +%FT%TZ)'","payload":{"type":"wet"},"visibleToParent":true}'
   ```
   Expect `201` with `recordedBy` populated from the currently-checked-in caregiver(s).

2. Record a fever-triggering temperature event:
   ```bash
   curl -X POST "$API/api/child-events" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childId":"'$CHILD_ID'","eventType":"temperature","occurredAt":"'$(date -u +%FT%TZ)'","payload":{"celsius":38.7},"visibleToParent":true}'
   ```
   Expect `201`; server logs (or, once a recipient exists, dispatches) a push notification
   attempt to every `CanPickup = true` contact of that child.

3. Start and complete a sleep event:
   ```bash
   curl -X POST "$API/api/child-events" ... -d '{"eventType":"sleep","payload":{"quality":null},...}'
   # note the returned id, then:
   curl -X PATCH "$API/api/child-events/$EVENT_ID" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"endedAt":"'$(date -u +%FT%TZ)'","payload":{"quality":"good"}}'
   ```
   Expect the response's `payload.durationMinutes` to reflect the elapsed time.

4. Verify the same-day edit window: attempt a `PATCH` on an event with `occurredAt` set to
   yesterday using a caregiver-scoped call — expect `403 errors.child_events.edit_window_expired`.
   Repeat with a director JWT — expect `200`.

5. Verify daily summary exclusion:
   ```bash
   curl "$API/api/child-events/daily-summary?childId=$CHILD_ID&date=$(date -u +%F)"
   ```
   Record one more event with `"visibleToParent": false`, re-request the summary, and confirm its
   counts are unchanged (the staff-internal event must not appear).

6. Verify pagination: create 25+ events for one child, then:
   ```bash
   curl "$API/api/child-events?childId=$CHILD_ID&limit=10"
   ```
   Expect exactly 10 rows and a non-null `nextCursor`; requesting again with `before=$nextCursor`
   returns the next page with no overlap or gap.

## Mobile validation (Expo caregiver app, manual)

1. Open the app on a paired kiosk device with a caregiver checked in.
2. From the group view, tap a child card → confirm the quick-action sheet opens (not a
   full-screen modal).
3. Record a diaper, bottle, and mood event in sequence — confirm each completes in ≤2 taps and
   appears immediately at the top of that child's timeline.
4. Enable airplane mode. Record another routine event — confirm it appears immediately marked
   "pending sync." Disable airplane mode — confirm it syncs automatically and the badge clears.
5. Start a sleep event while offline, then end it while still offline, then reconnect — confirm
   exactly one completed sleep event appears on the timeline (not two, not "in progress" forever).
6. Record a temperature reading — confirm the administrator-confirmation PIN step appears (or is
   skippable), consistent with feature 008a's existing check-in PIN UX.

## Expected test coverage (backend, TestContainers per constitution Principle V)

- Happy-path create/list/update/delete for at least one event type.
- Payload validation rejects a malformed/mismatched payload per `EventType`.
- Temperature threshold trigger (`>38.0`) fires the alert service; `<=38.0` does not.
- Same-day edit window: caregiver blocked on a prior-day event, director not blocked.
- `visibleToParent = false` excluded from `GET /daily-summary`.
- Soft-delete excludes the event from all subsequent reads without removing the row.
- `IShiftAttributionService` reuse: `RecordedBy` reflects 0/1/2 checked-in caregivers correctly
  (already covered generally by feature 008a's own tests; this feature's tests confirm the
  integration point, not re-test the service itself).
