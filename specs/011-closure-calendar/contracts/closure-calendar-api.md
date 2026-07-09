# Closure Calendar API Contract

All endpoints require tenant-scoped authentication. Director management endpoints require `DirectorOnly`.

## GET `/api/closures?locationId={guid}&year={yyyy}`

Returns closure days for one location/year.

Response `200`:

```json
[
  {
    "id": "uuid",
    "locationId": "uuid",
    "date": "2026-12-25",
    "label": "Kerstvakantie",
    "closureType": "holiday",
    "notifyParents": true,
    "status": "draft",
    "notificationSentAt": null,
    "publishedAt": null,
    "cancelledAt": null,
    "deliverySummary": {
      "sent": 0,
      "failed": 0,
      "messageCount": 0
    },
    "createdAt": "2026-07-09T12:00:00Z",
    "updatedAt": "2026-07-09T12:00:00Z"
  }
]
```

Errors:

- `403 errors.auth.forbidden`
- `404 errors.locations.not_found`
- `400 errors.validation`

## POST `/api/closures`

Creates a draft closure.

Request:

```json
{
  "locationId": "uuid",
  "date": "2026-12-25",
  "label": "Kerstvakantie",
  "closureType": "holiday",
  "notifyParents": true
}
```

Response:

- `201` with closure response.

Errors:

- `400 errors.closures.past_date`
- `400 errors.validation`
- `404 errors.locations.not_found`
- `409 errors.closures.duplicate_date`

## PATCH `/api/closures/{id}`

Updates a draft closure.

Request:

```json
{
  "label": "Pedagogische studiedag",
  "closureType": "training",
  "notifyParents": true
}
```

Response:

- `200` with closure response.

Errors:

- `400 errors.closures.not_editable`
- `404 errors.closures.not_found`

## POST `/api/closures/{id}/publish`

Publishes a closure and optionally notifies parents.

Request:

```json
{
  "confirmExistingAttendance": false
}
```

Response `200`:

```json
{
  "closure": { "id": "uuid", "status": "published" },
  "attendanceRecordsCreated": 42,
  "attendanceRecordsUpdated": 0,
  "requiresAttendanceConfirmation": false,
  "notificationSummary": {
    "recipients": 36,
    "pushSent": 34,
    "pushFailed": 2,
    "messagesCreated": 36
  }
}
```

Conflict requiring confirmation:

- `409 errors.closures.attendance_confirmation_required`

```json
{
  "errorKey": "errors.closures.attendance_confirmation_required",
  "checkedInCount": 3
}
```

## POST `/api/closures/{id}/cancel`

Cancels a published closure or removes a draft.

Response `200` for published closure:

```json
{
  "closure": { "id": "uuid", "status": "cancelled" },
  "attendanceRecordsReleased": 42,
  "attendanceRecordsPreserved": 1,
  "notificationSummary": {
    "recipients": 36,
    "pushSent": 34,
    "pushFailed": 2,
    "messagesCreated": 36
  }
}
```

Response `204` for removed draft.

## GET `/api/closures/billable-exclusions?locationId={guid}&from={yyyy-mm-dd}&to={yyyy-mm-dd}`

Internal/director-authenticated query surface for future invoicing. Returns published closure dates.

Response `200`:

```json
{
  "locationId": "uuid",
  "dates": ["2026-12-25", "2026-12-26"]
}
```
