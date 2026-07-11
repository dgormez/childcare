# Contract: Group Activities API

Auth idiom mirrors `ChildEventEndpoints.cs` exactly (research.md R8): caregiver-tablet writes are
`DeviceAuthenticated` + `DeviceTokenRotationFilter`; director actions are `DirectorOnly`; parent
reads are `ParentOnly`.

## `POST /api/group-activities`

Device-authenticated (caregiver tablet). `groupId`/`locationId` come from the device token's own
claims (never client-supplied), matching `RecordChildEventCommand`'s existing convention.

Request:

```json
{
  "id": "guid (client-generated, idempotent create)",
  "activityType": "outdoor",
  "title": "In de tuin",
  "description": null,
  "occurredAt": "2026-07-10T09:15:00Z"
}
```

`occurredAt` is client-captured at creation time (no manual time picker, spec.md Assumptions) but
still client-supplied, not server-assigned — mirrors `RecordChildEventRequest.OccurredAt` so an
offline-queued activity keeps its real capture moment rather than being timestamped at sync time.

- Response `201`: `GroupActivityResponse` (id, activityType, title, description, occurredAt,
  recordedBy: guid[], photos: [], createdAt) — `recordedBy` populated server-side from
  `IShiftAttributionService` (research.md R1).
- Idempotent by `id`: a retried request with the same `id` returns the existing record (`200`),
  matching `ChildEvent`'s FR-013a precedent.
- `422 { errorKey: "errors.validation", fieldErrors }` — title missing/too long, description too
  long, invalid `activityType`.

## `POST /api/group-activities/{id}/photos`

Device-authenticated. `multipart/form-data`: one image file field, optional `caption` field.

- Resizes to max 1920px long edge, generates a 400px thumbnail (research.md R2/R3), uploads both
  to GCS, creates a `GroupActivityPhoto` row.
- Response `201`: `GroupActivityPhotoResponse` (id, downloadUrl, thumbnailDownloadUrl, caption,
  uploadedAt) — signed GCS URLs, 15-minute validity, same as every other photo read in this
  codebase.
- `404 errors.group_activities.not_found` — unknown/deleted activity id.
- `409 errors.group_activities.photo_limit_reached` — activity already has 10 photos.
- `413 errors.group_activities.photo_too_large` — raw upload exceeds 10MB.

## `GET /api/group-activities/timeline?groupId={id}&date={yyyy-MM-dd}`

Device-authenticated. `date` optional — defaults to today (the only value the caregiver-tablet
client ever requests, per spec.md's Assumptions that the tablet shows today only). `groupId` is
not client-selectable in practice — it must match the device token's own `group_id` claim (`403`
otherwise, same defensive-check pattern as `ChildEventEndpoints`'s edit-window device-location
check); it is still required as an explicit param, not silently inferred, so the contract is
self-documenting about which group a given request is scoped to.

- Response `200`: `GroupTimelineResponse` — a chronologically ordered list of entries, each
  either a `ChildEvent`-shaped item or a `GroupActivity`-shaped item (`kind: "child_event" |
  "group_activity"` discriminator), for that group/date (research.md R4).

## `GET /api/group-activities/director-timeline?groupId={id}&date={yyyy-MM-dd}`

`DirectorOnly`. Same response shape as above; `date` required (no implicit "today" default,
since the director UI always shows an explicit date picker per spec.md's UX Requirements).

## `DELETE /api/group-activities/{id}`

`DirectorOnly`.

- `204` on success — hard delete (spec.md Assumptions: no audit-trail requirement for this
  moderation action). Deletes the `GroupActivityPhoto` rows and their GCS objects (full +
  thumbnail) before deleting the activity row, so no orphaned GCS objects remain.
- `404 errors.group_activities.not_found`.

## `GET /api/parent/children/{childId}/daily-summary?date={yyyy-MM-dd}` (extended, not new)

`ParentOnly`. Existing feature-009 endpoint (`GetParentDailySummaryQuery`) gains an `activities`
array in its response (research.md R5):

```json
{
  "napsCount": 2,
  "bottlesCount": 3,
  "...": "...(unchanged existing fields)",
  "activities": ["...(unchanged — existing per-child Activity-type event descriptions, feature 013)"],
  "groupActivities": [
    {
      "id": "guid",
      "activityType": "outdoor",
      "title": "In de tuin",
      "description": "Buiten gespeeld met de bal",
      "occurredAt": "2026-07-10T09:15:00Z",
      "photos": []
    }
  ]
}
```

Named `groupActivities`, not `activities` — `DailySummaryResponse.Activities` already exists
(feature 013) as a flat list of individual, per-child `ChildEventType.Activity` descriptions; a
same-named field for this feature's distinct group-level concept would collide and confuse the
two.

- `photos` is populated only when the requesting parent's child has an active contract with
  `photos_internal = true` (research.md R6); otherwise always `[]`, never a partial/error state.

## `GET /api/parent/group-activities/gallery?month={yyyy-MM}` (new)

`ParentOnly`. `month` optional — defaults to the current calendar month (spec.md Assumptions: no
historical month browsing in this feature).

- Response `200`: flat list of `{ activityId, groupId, photo: PhotoResponse }` entries across
  every group the parent's child(ren) belong to, most recent first, consent-filtered per R6.
  Text-only activities (no photos) never appear here.
- If the parent has no consented child anywhere, returns an empty list (`200`, not an error) —
  the client renders the "no photo consent" empty state (spec.md User Story 3, Acceptance
  Scenario 2) based on an empty result plus a separate `hasConsent: boolean` flag on the response,
  so the client can distinguish "no consent" from "consent but nothing recorded yet this month."
