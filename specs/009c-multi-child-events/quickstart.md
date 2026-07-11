# Quickstart: Multi-Child Events

## Prerequisites

- Backend running locally against Docker PostgreSQL.
- A tenant with a location, a group, and at least 3 active children (features 004/005/006 data).
- A paired kiosk device (feature 008a) with at least one caregiver checked in via
  `/room-shifts/check-in`, and at least 3 children checked in present today (feature 010).

## Backend validation (curl, device-token authenticated)

1. Confirm the prerequisite fix (research.md R2): the roster-loading routes now accept the device
   token.
   ```bash
   curl -H "Authorization: Bearer $DEVICE_TOKEN" "$API/api/groups"
   curl -H "Authorization: Bearer $DEVICE_TOKEN" "$API/api/children?groupId=$GROUP_ID"
   ```
   Expect `200` on both (previously `403` before this feature's fix).

2. Submit a full-success batch (a group nap start for 3 present children):
   ```bash
   curl -X POST "$API/api/child-events/batch" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childIds":["'$CHILD_A'","'$CHILD_B'","'$CHILD_C'"],"eventType":"sleep","occurredAt":"'$(date -u +%FT%TZ)'","payload":{"quality":null},"visibleToParent":true}'
   ```
   Expect `200` with `created` containing all 3 children and `errors: []`. Verify
   `GET /api/child-events?childId=$CHILD_A` (and B, C) each show the new sleep event.

3. Submit a partial-failure batch: check one of the three children out
   (`POST /api/attendance` check-out or equivalent), then resubmit the same batch shape with all
   three `childIds`.
   Expect `200` with `created` containing the two still-present children and `errors` containing
   the checked-out child with `reason: "not_present"`. Confirm the two successful `ChildEvent`
   rows exist and no row exists for the checked-out child.

4. Submit an oversized batch (31 distinct `childIds`, can be repeated/fake guids for this check).
   Expect `422 { errorKey: "errors.validation", fieldErrors: { "Items.Count":
   "errors.child_events.batch_too_large" } }` (the standard `ValidationBehavior` shape, contracts/
   child-events-batch-api.md) and no `ChildEvent` rows created at all.

5. Submit a batch with an individual-only event type (`"eventType": "temperature"`).
   Expect the same `errors.validation` shape, with `fieldErrors.EventType =
   "errors.child_events.batch_type_not_supported"`.

## Mobile validation (Expo, kiosk-paired tablet)

1. Open the caregiver app's room roster ("children" tab) — confirm it loads (validates the R2
   fix end-to-end, not just via curl).
2. Tap the multi-select entry point in the header. Confirm present children's cards become
   selectable and absent children are not selectable.
3. Tap "Alles selecteren" — confirm every present child becomes selected.
4. Tap "Log event" — confirm the sheet that opens only offers the 8 multi-select-eligible event
   types (no temperature/medication/weight/growth_check).
5. Fill in a diaper event and submit — confirm the roster returns to normal mode and a success
   toast names the number of children.
6. Repeat with airplane mode on — confirm the app behaves identically from the caregiver's point
   of view (optimistic success), then confirm exactly one new row appears in the local offline
   queue (not one per child) once connectivity returns and the queue syncs.
