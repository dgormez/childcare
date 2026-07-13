# API Contracts: Configurable Caregiver PIN

## `PUT /api/locations/{id}/checkin-settings`

New endpoint, mirrors `PUT /api/locations/{id}/reservation-settings` (013f).

**Request** (`UpdateLocationCheckInSettingsRequest`):
```json
{ "requiresCaregiverPin": false }
```

**Response**: `200 OK` with the full `LocationResponse` (extended, see below), same pattern as
the reservation-settings endpoint. `403`/`404` follow existing director-only/location-not-found
handling.

**Authorization**: `DirectorOnly` (matches every other location-settings write).

## `LocationResponse` (extended)

Adds one field to the existing response:
```json
{
  "...": "existing fields unchanged",
  "requiresCaregiverPin": true
}
```

## `GET /api/room-shifts/roster` (response shape changes)

**Before** (bare array):
```json
[
  { "staffProfileId": "...", "firstName": "Marie", "photoUrl": "...", "checkedIn": true, "checkedInAt": "..." }
]
```

**After** (wrapped object — breaking change, all consumers must update):
```json
{
  "requiresCaregiverPin": false,
  "caregivers": [
    { "staffProfileId": "...", "firstName": "Marie", "photoUrl": "...", "checkedIn": true, "checkedInAt": "..." }
  ]
}
```

## `POST /api/room-shifts/check-in` / `POST /api/room-shifts/check-out`

**Request** (`CheckInRequest`/`CheckOutRequest`) — `pin` becomes optional:
```json
{ "staffId": "...", "pin": null }
```
- If the location's `requiresCaregiverPin` is `true`: `pin` MUST be a valid, non-empty PIN for
  that staff member — behavior unchanged from today (missing/invalid PIN is rejected).
- If `false`: any `pin` value is ignored; the shift is written directly from `staffId`.

**Response**: unchanged shape (`RoomShift` created/closed) — no field added, since the shift
record itself carries no indication of which mode produced it (by design, per data-model.md).

## `POST /api/room-shifts/confirm-administrator`

**No contract change.** Continues to require `staffId` + `pin`, or `skip: true`, exactly as
today — independent of the location's `requiresCaregiverPin` setting (resolved via clarify, see
spec.md and research.md R5).
