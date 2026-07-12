# API Contract: Incident Reports

All endpoints are tenant-scoped (require a valid device token or director JWT, per
`TenantMiddleware`). Base path: `/api/incident-reports`.

## `POST /api/incident-reports`

**Auth**: Device token (caregiver tablet, feature 008a) only. **Corrected during implementation**:
this feature builds no director-facing "file incident" screen (US1/US2's tasks are caregiver-tablet
filing + director list/detail/PDF only), and `LocationId`/`GroupId` have no resolution source other
than a paired device's own claims (FR-019) — a `DirectorOnly` caller would have nowhere to derive
them from. If a future feature adds director-side filing, it needs its own explicit `locationId`
request field, not a bare extension of this auth policy.

**Request** (`FileIncidentReportRequest`):

```json
{
  "childId": "uuid",
  "occurredAt": "2026-07-12T09:14:00Z",
  "locationDetail": "outdoor",
  "description": "Child tripped on the playground and scraped their knee.",
  "injuryType": "scrape",
  "firstAidGiven": "Cleaned and bandaged",
  "doctorCalled": false,
  "doctorNotes": null,
  "parentNotified": true,
  "parentNotifiedAt": "2026-07-12T09:20:00Z",
  "parentNotifiedHow": "phone",
  "witnesses": null,
  "followUp": null
}
```

**Response**: `201 Created` with the created `IncidentReportResponse` (see below).
`reportedBy` is never read from the request — resolved server-side.

**Failure modes**:
- `422` — missing `description` or `injuryType` (`errors.incident_reports.description_required`,
  `errors.incident_reports.injury_type_required`). **Corrected during implementation**: every
  FluentValidation failure in this codebase returns `422` via the shared `ValidationBehavior`
  pipeline (Program.cs), not `400` — this contract's original `400` was aspirational text, not an
  actual per-feature exception to that global convention.
- `404` — `childId` does not resolve within the tenant.

## `GET /api/incident-reports/{id}`

**Auth**: `DirectorOnly`, or a device token for a child currently assigned to that device's
location/group (not restricted to reports the device itself filed — FR-018, mirrors feature 008's
medical-quick-access location/group scoping).

**Response**: `200 OK` with `IncidentReportResponse`. **Side effect**: if the caller is a director
and `reviewedAt` is currently null, it is set to now (research.md R3) before the response is
returned.

## `GET /api/incident-reports`

**Auth**: `DirectorOnly` (cross-KDV inspection view, FR-009).

**Query params**: `childId?`, `locationId?`, `from?` (date), `to?` (date), `page?` (default `1`),
`pageSize?` (default `25`).

**Response**: `200 OK`, paginated list of `IncidentReportResponse`, sorted `occurredAt` descending
by default with a secondary sort by `id` for stable pagination across entries sharing the same
`occurredAt` (FR-009).

## `PUT /api/incident-reports/{id}`

**Auth**: `DirectorOnly`, or a device token whose paired location matches the report's `locationId`
(FR-007 — location-scoped, not restricted to the specific device that filed it).

**Request**: same shape as `FileIncidentReportRequest`, all fields optional (partial update).

**Behavior**:
- If `CreatedAt` is within 24 hours: any included field is applied.
- If `CreatedAt` is more than 24 hours old: only `followUp` may be present in the request body;
  any other field present triggers a rejection (the whole request is rejected, not partially
  applied) — no silent partial-locking.

**Failure modes**:
- `409` — request includes a locked field on a report older than 24 hours
  (`errors.incident_reports.locked`).

## `GET /api/incident-reports/{id}/pdf`

**Auth**: `DirectorOnly`.

**Query params**: `locale?` (`nl`|`fr`|`en`, default `nl` — mirrors feature 007's contract-PDF
convention).

**Response**: `200 OK`, `application/pdf` bytes. Includes every field, the location's name/
address/`Dossiernummer` (research.md R5), and a signature line for the reporting caregiver(s).

## Response shape — `IncidentReportResponse`

```json
{
  "id": "uuid",
  "childId": "uuid",
  "locationId": "uuid",
  "occurredAt": "2026-07-12T09:14:00Z",
  "locationDetail": "outdoor",
  "description": "...",
  "injuryType": "scrape",
  "firstAidGiven": "...",
  "doctorCalled": false,
  "doctorNotes": null,
  "parentNotified": true,
  "parentNotifiedAt": "2026-07-12T09:20:00Z",
  "parentNotifiedHow": "phone",
  "reportedBy": ["uuid"],
  "witnesses": null,
  "followUp": null,
  "reviewedAt": null,
  "createdAt": "2026-07-12T09:14:32Z",
  "updatedAt": null
}
```
