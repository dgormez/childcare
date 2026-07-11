# Quickstart: Group Activities

Validates the feature end-to-end against a real backend (TestContainers Postgres locally, or the
dev API). See `contracts/group-activities-api.md` for full request/response shapes and
`data-model.md` for entity fields.

## Prerequisites

- Backend running locally (`dotnet run --project backend/ChildCare.Api`) against a tenant schema
  with a location, a group, at least one checked-in `RoomShift` (feature 008a), and a child with
  an active contract (`photos_internal = true` for the consent scenarios below).
- A device token for that location/group (feature 008a's pairing flow) and a director JWT
  (feature 003/005 login).

## Scenario 1 — Caregiver records an activity with photos (User Story 1)

```bash
curl -X POST "$API/api/group-activities" \
  -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
  -d '{"activityType":"outdoor","title":"In de tuin","description":"Buiten gespeeld","occurredAt":"2026-07-10T09:15:00Z"}'
# → 201, note the returned "id" — occurredAt is required (CreateGroupActivityCommandValidator)

curl -X POST "$API/api/group-activities/$ACTIVITY_ID/photos" \
  -H "Authorization: Bearer $DEVICE_TOKEN" \
  -F "photo=@sample.jpg" -F "caption=Zonnig weer"
# → 201, downloadUrl + thumbnailDownloadUrl both resolve to a viewable image
```

**Expected**: `GET /api/group-activities/timeline?groupId=$GROUP_ID` (device-authenticated)
includes the new activity with its photo, chronologically positioned among any child events
recorded around the same time.

## Scenario 2 — Parent sees it in the daily feed, gated by consent (User Story 2)

```bash
curl "$API/api/parent/children/$CHILD_ID/daily-summary?date=2026-07-10" \
  -H "Authorization: Bearer $PARENT_JWT"
```

**Expected**: `activities` includes the activity from Scenario 1 with `photos` populated (parent's
contract has `photos_internal = true`). Flip that contract flag to `false` and repeat — `photos`
must be `[]` while `title`/`description` remain present.

## Scenario 3 — Parent gallery (User Story 3)

```bash
curl "$API/api/parent/group-activities/gallery" -H "Authorization: Bearer $PARENT_JWT"
```

**Expected**: includes the photo from Scenario 1 (consent = true case). With consent = false,
returns `{ items: [], hasConsent: false }` — client renders the explicit no-consent empty state,
not a blank grid.

## Scenario 4 — Director deletes an activity (User Story 4)

```bash
curl -X DELETE "$API/api/group-activities/$ACTIVITY_ID" -H "Authorization: Bearer $DIRECTOR_JWT"
# → 204
```

**Expected**: the activity disappears from the caregiver timeline (Scenario 1's `GET`), the
parent daily feed (Scenario 2), and the gallery (Scenario 3) — all in the same request, no
propagation delay.

## Scenario 5 — Offline capture (User Story 1, FR-012)

Mobile-only, exercised via the app (not curl): airplane-mode the tablet, create an activity with
2 photos. Expect: activity appears immediately in the local timeline marked pending; photos show
"Foto's worden geüpload…". Restore connectivity — expect the activity and both photos to reach the
server without creating a duplicate activity (client-generated `id` is idempotent, per the
contract).
