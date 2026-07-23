# Staff HR Dossier & Time Registration API Contract

All endpoints require tenant-scoped authentication (`TenantMiddleware`). HR dossier and
time-entry-correction endpoints require `DirectorOnly`. Clock in/out requires `StaffOrDirector`
and always resolves the acting staff member's own `StaffProfileId` server-side from the JWT
`NameIdentifier` claim — never a client-supplied ID (research.md R2), matching feature 027's
`GET /api/staff-schedules/me` precedent.

## Time registration

### POST `/api/staff-time-entries/clock-in`

`StaffOrDirector`. Creates an open time entry for the acting staff member.

Request:

```json
{ "locationId": "uuid", "groupId": "uuid|null", "function": "kinderbegeleider|null" }
```

`function` is required only when the acting staff member's `StaffProfile.TimeEntryFunctions` has
more than one entry (FR-005); omitted/ignored otherwise, in which case the server uses the
staff member's single configured function.

Response `200`:

```json
{ "id": "uuid", "clockedInAt": "2026-07-23T07:58:00Z", "function": "kinderbegeleider" }
```

Errors: `409 errors.staff_time_entries.already_clocked_in` (an open entry already exists — FR-003);
`400 errors.staff_time_entries.function_required` (multiple functions configured, none supplied);
`400 errors.staff_time_entries.no_function_configured` (empty `TimeEntryFunctions` — FR-010).

### POST `/api/staff-time-entries/clock-out`

`StaffOrDirector`. Closes the acting staff member's open time entry.

Response `200`: the updated entry. Errors: `404 errors.staff_time_entries.no_open_entry`.

### GET `/api/staff-time-entries?staffProfileId={id}&from={date}&to={date}`

`DirectorOnly`. Lists time entries for one staff member in a date range, each including computed
`isLocked`.

### PATCH `/api/staff-time-entries/{id}`

`DirectorOnly`. Corrects `clockedOutAt`, `function`, `groupId`, or `notes` on an entry. Rejected
with `423 errors.staff_time_entries.locked` if `isLocked == true`. Returns `200` with `{
"overlapWarning": true|false }` alongside the updated entry when the correction overlaps another
entry for the same staff member (FR-009) — a warning, not a block.

### POST `/api/staff-time-entries/{id}/unlock`

`DirectorOnly`. Sets `UnlockedAt = now` (FR-007). Response `200`.

### POST `/api/staff-time-entries/{id}/relock`

`DirectorOnly`. Clears `UnlockedAt` back to `null`. Response `200`.

## HR dossier

### GET `/api/staff/{staffProfileId}/documents`

`DirectorOnly`. Lists a staff member's `StaffDocument` rows (signed download URL resolved fresh
per item, R3).

### POST `/api/staff/{staffProfileId}/documents/upload-url`

`DirectorOnly`. Request: `{ "contentType": "application/pdf" }` → response: `{ "objectPath":
"...", "uploadUrl": "..." }` (client uploads directly to GCS, standard signed-upload flow — same
two-step idiom as every existing document/photo feature).

### POST `/api/staff/{staffProfileId}/documents`

`DirectorOnly`. Confirms the upload and creates the `StaffDocument` row: `{ "documentType":
"employment_contract", "title": "...", "objectPath": "...", "validFrom": "date|null",
"validUntil": "date|null" }`. Response `201`.

### DELETE `/api/staff/{staffProfileId}/documents/{documentId}`

`DirectorOnly`. Deletes the DB row and the GCS object (best-effort, per `IStaffDocumentStorage
.DeleteAsync`'s idempotent semantics).

### PATCH `/api/staff/{staffProfileId}/time-entry-functions`

`DirectorOnly`. Request: `{ "functions": ["kinderbegeleider", "logistiek"] }` (at least one
required — `400 errors.staff.no_function_configured` otherwise). Updates
`StaffProfile.TimeEntryFunctions`.

## Contract-expiry alerts

### GET `/api/staff/contracts-expiring`

`DirectorOnly`. Returns every staff member with an `EmploymentContract`-type document whose
`ValidUntil` is `<= today + 60 days` (inclusive of already-past dates — FR-014). Response: `[{
"staffProfileId": "uuid", "staffName": "string", "validUntil": "date", "isExpired": true|false }]`,
sorted soonest/most-overdue first (mirrors `DueSoonBlock`'s existing sort precedent).

## Medewerkersbeleid subsidy report

### GET `/api/reports/staff-hours?locationId={id}&from={date}&to={date}`

`DirectorOnly`. Response:

```json
{
  "locationId": "uuid",
  "from": "2026-07-01",
  "to": "2026-07-31",
  "totalChildHours": 812.5,
  "byFunction": [
    { "function": "kinderbegeleider", "totalStaffHours": 420.0, "ratio": 1.93 },
    { "function": "logistiek", "totalStaffHours": 40.0, "ratio": null }
  ]
}
```

`ratio` is `null` when `totalStaffHours == 0` (no divide-by-zero — FR-016 Acceptance Scenario 2).
No pass/fail evaluation against Opgroeien's thresholds (Clarifications — that's feature 041's job).

### GET `/api/reports/staff-hours/export?locationId={id}&from={date}&to={date}`

`DirectorOnly`. `200 text/csv` — one row per closed time entry (`staffName,date,function,
clockedInAt,clockedOutAt,durationHours`), reusing the same query as the on-screen report (R6).
