# Staff App API Contract

All endpoints require tenant-scoped authentication (`TenantMiddleware`). Director-facing
endpoints require `DirectorOnly`; staff-personal endpoints require `StaffOrDirector` and always
resolve the acting staff member's own `StaffProfileId` from the JWT `NameIdentifier` claim —
never a client-supplied ID (FR-015), matching `GET /api/staff-schedules/me`'s existing
precedent (feature 012).

## Extended — `GET /api/staff-schedules/me`

Unchanged request shape. Response now filters to `IsPublished == true` rows only (previously
returned every future row unconditionally).

Response `200` (extended fields on each entry):

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
    "status": "scheduled",
    "absenceReason": null,
    "coverStaffId": "uuid|null",
    "notes": "string|null",
    "isPublished": true,
    "createdAt": "2026-07-10T12:00:00Z",
    "updatedAt": "2026-07-10T12:00:00Z"
  }
]
```

`isAbsent` is removed from the response body (was a stored bool, now derivable client-side as
`status === "absent"` — matches research.md R3's server-side computed-property change).

## POST `/api/staff-schedules/{locationId}/publish`

Director-only. Publishes (or, with `unpublish: true`, un-publishes) every `StaffSchedule` row
for one location and one Monday-anchored week.

Request:

```json
{ "weekStart": "2026-07-13", "unpublish": false }
```

Response: `200` with `{ "publishedCount": 14 }`. Notifies every distinct affected staff member
(`SchedulePublished`) when publishing; no notification on unpublish.

Errors: `400 errors.validation` (`weekStart` not a Monday), `404 errors.locations.not_found`.

## POST `/api/staff-schedules/report-sick`

`StaffOrDirector`. The acting staff member reports themselves sick for today (or tomorrow, per
spec.md's cutoff — resolved server-side, not client-chosen).

Request: `{}` (no body — the date and staff identity are both server-resolved).

Response `200`: the updated `StaffSchedule` row (`status: "absent"`) if one existed for the
resolved date, or `204` if the staff member had no assignment that day (a sick report with no
schedule to affect is still recorded — see `StaffLeaveRequest` side effect below).

Side effect: also creates a `StaffLeaveRequest` (`type: "sick"`, single-day range,
`status: "pending"`, auto-approved by the system in the same transaction since a same-day sick
report needs no director review — recorded purely for the "Verlofaanvragen" history/audit
trail) and notifies the director (existing announcement-style notification path, director
recipient).

Errors: `404 errors.staff.profile_not_found` (caller has no `StaffProfile`).

## GET `/api/staff-schedules/{date}/sick-cover-candidates?excludeStaffProfileId={guid}`

Director-only. Lists staff eligible to cover the given absent staff member's assignment.

Response `200`:

```json
[
  { "staffProfileId": "uuid", "name": "string", "qualificationLevel": "string" }
]
```

Excludes: staff without `StaffLocationEligibility` for the absence's location; staff with any
other `StaffSchedule` row overlapping the same date/time at any location; deactivated staff.

## POST `/api/staff-schedules/{id}/assign-cover`

Director-only. `{id}` is the absent `StaffSchedule` row's id.

Request:

```json
{ "coverStaffProfileId": "uuid" }
```

Response `200`: `{ "original": { ...StaffSchedule }, "coverEntry": { ...StaffSchedule } }` —
the original row (unchanged `status: "absent"`, now with `coverStaffId` set) and the newly
created `Covered`-status row for the replacement, immediately published regardless of the
week's own publish state (research.md R4). Notifies both staff members
(`AssignmentChanged`).

Errors: `403 errors.staff_schedules.not_eligible`, `409 errors.staff_schedules.overlap`
(replacement already has a conflicting entry — race-checked under
`IAdvisoryLockService.RunExclusiveAsync`, FR-018).

## POST `/api/staff-leave-requests`

`StaffOrDirector`. Staff member submits a planned leave request.

Request:

```json
{ "type": "annual", "dateFrom": "2026-08-03", "dateTo": "2026-08-07", "notes": "string|null" }
```

Response `201`:

```json
{
  "id": "uuid",
  "staffProfileId": "uuid",
  "type": "annual",
  "dateFrom": "2026-08-03",
  "dateTo": "2026-08-07",
  "notes": "string|null",
  "status": "pending",
  "decidedBy": null,
  "decidedAt": null,
  "createdAt": "2026-07-22T09:00:00Z"
}
```

Errors: `400 errors.validation` (`dateTo` before `dateFrom`, range entirely in the past).

## GET `/api/staff-leave-requests/me`

`StaffOrDirector`. Returns the caller's own leave requests, newest first. Never returns another
staff member's rows (FR-012/015).

## GET `/api/staff-leave-requests?status=pending`

Director-only. The "Verlofaanvragen" queue. `status` filter optional.

## POST `/api/staff-leave-requests/{id}/decide`

Director-only.

Request:

```json
{ "approve": true }
```

Response `200`: the updated `StaffLeaveRequest`. On `approve: true`, also marks every date in
range with an existing `StaffSchedule` row as `Absent` (FR-011, data-model.md's "On Approved"
rule) and notifies the staff member (`LeaveRequestDecided`); on `approve: false`, only
notifies, no rota change.

Errors: `409 errors.staff_leave_requests.already_decided` (re-deciding a non-`Pending` request).
