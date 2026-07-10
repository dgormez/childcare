# Staff Schedules API Contract

All endpoints require tenant-scoped authentication (`TenantMiddleware`). Write/list/
management endpoints require `DirectorOnly`; `GET /api/staff-schedules/me` requires
`StaffOrDirector` and is registered as a standalone route (not inside the `DirectorOnly`
group), matching `GET /api/staff/me`'s precedent (feature 008).

## GET `/api/staff-schedules?locationId={guid}&weekStart={yyyy-MM-dd}`

Returns all schedule entries for one location's week (Monday `weekStart` through the
following Sunday), for the rota builder's week-grid view.

Response `200`:

```json
[
  {
    "id": "uuid",
    "staffProfileId": "uuid",
    "locationId": "uuid",
    "groupId": "uuid|null",
    "date": "2026-07-13",
    "startTime": "08:00",
    "endTime": "16:00",
    "isAbsent": false,
    "absenceReason": null,
    "createdAt": "2026-07-10T12:00:00Z",
    "updatedAt": "2026-07-10T12:00:00Z"
  }
]
```

Errors: `403 errors.auth.forbidden`, `404 errors.locations.not_found`, `400 errors.validation`.

## POST `/api/staff-schedules`

Creates a schedule entry.

Request:

```json
{
  "staffProfileId": "uuid",
  "locationId": "uuid",
  "groupId": "uuid|null",
  "date": "2026-07-13",
  "startTime": "08:00",
  "endTime": "16:00"
}
```

Response: `201` with the created entry (shape above).

Errors:

- `400 errors.validation` (end before start, missing fields)
- `404 errors.staff.not_found` / `errors.locations.not_found` / `errors.groups.not_found`
- `403 errors.staff_schedules.not_eligible` — the staff member has no `StaffLocationEligibility`
  row for this location (FR-017)
- `409 errors.staff_schedules.overlap` — staff member already scheduled at another location
  with an overlapping time range on this date (FR-003)
- `409 errors.staff_schedules.duplicate` — exact `(staffProfileId, date, startTime)` already
  exists

## PATCH `/api/staff-schedules/{id}`

Edits a future-dated entry (location, group, start/end time).

Request: same shape as POST, any subset of editable fields.

Response: `200` with the updated entry.

Errors: as POST, plus `404 errors.staff_schedules.not_found`,
`400 errors.staff_schedules.past_date` (FR-004).

## DELETE `/api/staff-schedules/{id}`

Deletes a future-dated entry.

Response: `204`.

Errors: `404 errors.staff_schedules.not_found`, `400 errors.staff_schedules.past_date`.

## POST `/api/staff-schedules/{id}/absence`

Marks or un-marks an entry absent.

Request:

```json
{
  "isAbsent": true,
  "absenceReason": "sick"
}
```

`absenceReason` is one of `sick|leave|holiday`, required when `isAbsent = true`, ignored
(should be omitted or null) when `isAbsent = false`.

Response: `200` with the updated entry.

Errors: `404 errors.staff_schedules.not_found`, `400 errors.staff_schedules.past_date`,
`400 errors.validation` (missing `absenceReason` when `isAbsent = true`).

## POST `/api/staff-schedules/copy-week`

Copies one location's week to another week.

Request:

```json
{
  "locationId": "uuid",
  "sourceWeekStart": "2026-07-13",
  "targetWeekStart": "2026-07-20"
}
```

Response `200`:

```json
{
  "copiedCount": 18,
  "skipped": [
    { "date": "2026-07-22", "staffProfileId": "uuid", "reason": "closure_day" },
    { "date": "2026-07-24", "staffProfileId": "uuid", "reason": "existing_entry" }
  ]
}
```

`reason` is one of `closure_day` (FR-009) or `existing_entry` (FR-009a) — the two documented
skip conditions; the copy never overwrites and never fails outright for a partial conflict
(research.md R4).

Errors: `404 errors.locations.not_found`, `400 errors.validation` (target not a Monday),
`400 errors.staff_schedules.invalid_copy_target` (target week not strictly after the source
week — FR-016).

## GET `/api/staff-schedules/projected-on-duty?locationId={guid}&date={yyyy-MM-dd}&time={HH:mm}`

Planning-only projected on-duty qualified-staff count for the rota builder (FR-007) — **not**
feature 010's live BKR ratio, which is a separate endpoint (`GetBkrRatioQuery`, unchanged by
this feature) sourced from real-time check-in presence.

Response `200`:

```json
{
  "projectedOnDutyCount": 2,
  "staffProfileIds": ["uuid", "uuid"]
}
```

Errors: `404 errors.locations.not_found`, `400 errors.validation`.

## GET `/api/staff-schedules/me`

Returns the authenticated caregiver's own schedule entries from today forward (FR-012). No
UI consumes this in this feature — it exists for feature 027 (Staff App). `StaffOrDirector`
policy; resolves the caller's `StaffProfileId` from the JWT, never a route parameter, so a
caregiver can never read another staff member's schedule through this endpoint.

Response `200`: array of entries, same shape as the list endpoint above, `staffProfileId`
always equal to the caller's own.

Errors: `404 errors.staff.profile_not_found` (no `StaffProfile` linked to this account —
mirrors `GET /api/staff/me`'s precedent).
