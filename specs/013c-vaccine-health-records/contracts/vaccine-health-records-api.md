# API Contract: Vaccine & Health Records

All endpoints are tenant-scoped (require a valid director JWT or, for read-only caregiver access,
either a device token or a staff JWT, per `TenantMiddleware`).

## Vaccine Records — base path `/api/children/{childId}/vaccine-records`

### `POST /api/children/{childId}/vaccine-records`

**Auth**: `DirectorOnly`.

**Request** (`CreateVaccineRecordRequest`):

```json
{
  "vaccineName": "DTP",
  "doseNumber": 2,
  "administeredOn": "2026-06-01",
  "nextDueDate": "2026-12-01",
  "administeredBy": "Dr. Peeters",
  "notes": null
}
```

**Response**: `201 Created` with the created `VaccineRecordResponse` (see below). `recordedBy` is
never read from the request — resolved server-side from the caller's JWT.

**Failure modes**:
- `422` — missing `vaccineName`/`administeredOn`, or `administeredOn` in the future
  (`errors.vaccine_records.vaccine_name_required`, `errors.vaccine_records.administered_on_required`,
  `errors.vaccine_records.administered_on_in_future`).
- `404` — `childId` does not resolve within the tenant.

### `GET /api/children/{childId}/vaccine-records`

**Auth**: `DirectorOnly` — this is the full-detail list feeding the Gezondheid tab. Caregivers never
call this endpoint directly; their read-only access goes through the dedicated
`/health-summary` endpoint below (FR-014), which returns a narrower, summary-shaped payload.

**Response**: `200 OK`, list of `VaccineRecordResponse`, sorted `administeredOn` descending
(FR-003).

### `PUT /api/children/{childId}/vaccine-records/{id}`

**Auth**: `DirectorOnly`.

**Request**: same shape as `CreateVaccineRecordRequest`, full replacement of editable fields.

**Failure modes**: same as `POST`; `404` if the record does not exist or is already soft-deleted.

### `DELETE /api/children/{childId}/vaccine-records/{id}`

**Auth**: `DirectorOnly`. Soft-delete (`deleted_at = now()`) — no hard delete.

**Response**: `204 No Content`.

## Health Records — base path `/api/children/{childId}/health-records`

### `POST /api/children/{childId}/health-records`

**Auth**: `DirectorOnly`.

**Request** (`CreateHealthRecordRequest`):

```json
{
  "recordType": "allergy",
  "title": "Peanut allergy",
  "description": "Confirmed by allergist, carries an EpiPen at all times.",
  "validFrom": "2026-01-01",
  "validUntil": null
}
```

**Response**: `201 Created` with the created `HealthRecordResponse`. No attachment is included in
this call — attaching a file is a separate two-step flow (below), matching FR-007's requirement
that a failed attachment upload never blocks saving the record.

**Failure modes**:
- `422` — missing/invalid `recordType`, missing `title`/`description`, or `validUntil` before
  `validFrom` (`errors.health_records.record_type_invalid`, `errors.health_records.title_required`,
  `errors.health_records.description_required`, `errors.health_records.valid_until_before_valid_from`).
- `404` — `childId` does not resolve within the tenant.

### `POST /api/children/{childId}/health-records/{id}/attachment-upload-url`

**Auth**: `DirectorOnly`.

**Request**: `{ "contentType": "application/pdf" }` (allowed: `application/pdf`, `image/jpeg`,
`image/png`; max 10MB per spec.md FR-006 — enforced client-side before requesting the upload URL
and server-side via the GCS signed policy, `422 errors.health_records.attachment_too_large` if a
client bypasses the client-side check).

**Response**: `200 OK`, `{ "uploadUrl": "https://storage.googleapis.com/...", "expiresInSeconds":
900 }` — the director's browser uploads the file directly to this signed URL (research.md R2);
the API never proxies the bytes. The health record's `attachment_object_path` is set at the time
this endpoint is called (deterministic path), not after upload completion — a failed client-side
upload leaves the record pointing at an object that doesn't exist yet, which `GET`'s download-URL
resolution treats as "no attachment" would be indistinguishable; **implementation note for
tasks.md**: verify object existence (or accept eventual re-upload) rather than trusting the upload
succeeded, consistent with FR-007's "failed upload never blocks the record" requirement working in
both directions.

### `GET /api/children/{childId}/health-records`

**Auth**: `DirectorOnly` — same reasoning as the vaccine-records list above: caregivers use the
dedicated `/health-summary` endpoint, never this full-detail list, directly.

**Response**: `200 OK`, list of `HealthRecordResponse`. Each record with a non-null
`attachment_object_path` includes a freshly-signed `attachmentDownloadUrl` (never the raw object
path) — null if no attachment.

### `PUT /api/children/{childId}/health-records/{id}`

**Auth**: `DirectorOnly`. Same request shape as create; attachment is managed via the separate
upload-url endpoint, not this call.

### `DELETE /api/children/{childId}/health-records/{id}`

**Auth**: `DirectorOnly`. Soft-delete only.

**Response**: `204 No Content`.

## Caregiver summary — `GET /api/children/{childId}/health-summary`

**Auth**: Device token (kiosk tablet, feature 008a) or staff JWT — read-only, no write path exists
on this route (FR-014). Enforces the same location-eligibility scoping as `GetChildByIdQuery`
(research.md R3); an ineligible caller receives the same generic `404` a nonexistent child id
would.

**Response**: `200 OK`:

```json
{
  "childId": "uuid",
  "activeHealthRecords": [ { "...": "HealthRecordResponse, expired records excluded" } ],
  "dueSoonVaccines": [
    { "vaccineName": "DTP", "nextDueDate": "2026-07-20", "isOverdue": false }
  ]
}
```

This is the payload the caregiver app's extended medical quick-access sheet renders (extends
feature 008's existing allergy/medical-notes sheet, per spec.md's Assumptions) and the payload
cached in the mobile read-cache for offline availability (feature 008's existing pattern).

## Director dashboard — `GET /api/vaccine-records/due-soon`

**Auth**: `DirectorOnly`.

**Query params**: `withinDays?` (default `30`).

**Response**: `200 OK`:

```json
[
  {
    "childId": "uuid",
    "childName": "Emma Peeters",
    "locationId": "uuid",
    "vaccineName": "DTP",
    "nextDueDate": "2026-07-05",
    "isOverdue": true
  }
]
```

One row per child (the child's most urgent due-soon/overdue vaccine, research.md R4), sorted
`nextDueDate` ascending, scoped to every location the signed-in director manages. Empty array
(not an error) when nothing is due.

## Response shape — `VaccineRecordResponse`

```json
{
  "id": "uuid",
  "childId": "uuid",
  "vaccineName": "DTP",
  "doseNumber": 2,
  "administeredOn": "2026-06-01",
  "nextDueDate": "2026-12-01",
  "administeredBy": "Dr. Peeters",
  "notes": null,
  "recordedBy": "uuid-or-null",
  "createdAt": "2026-06-01T10:00:00Z",
  "updatedAt": "2026-06-01T10:00:00Z"
}
```

## Response shape — `HealthRecordResponse`

```json
{
  "id": "uuid",
  "childId": "uuid",
  "recordType": "allergy",
  "title": "Peanut allergy",
  "description": "Confirmed by allergist, carries an EpiPen at all times.",
  "validFrom": "2026-01-01",
  "validUntil": null,
  "isExpired": false,
  "attachmentDownloadUrl": null,
  "recordedBy": "uuid-or-null",
  "createdAt": "2026-01-01T10:00:00Z",
  "updatedAt": null
}
```

`isExpired` is computed server-side (`validUntil` in the past) — the client never computes this
itself, avoiding client/server clock-skew disagreements about a record's expired state.

## Removed endpoints (superseded — research.md R1)

- `GET /api/children/{childId}/vaccinations`
- `POST /api/children/{childId}/vaccinations`
