# Contract: Attendance API

`POST` (check-in, check-out, mark-absent) and `GET` (BKR ratio) are device-token authenticated
only (`RequireAuthorization("DeviceAuthenticated")`, feature 008a's `DeviceTokenRotationFilter`
applied), mirroring `ChildEventEndpoints.cs`'s existing group pattern.

`PATCH`/`DELETE` (director/caregiver corrections) accept either a device token (caregiver,
same-day + own-location only) or a director's user JWT (any record, any day) via the existing
`DeviceOrDirector` policy (feature 009) — no new authorization mechanism.

`GET` (list/history, for director web) is `DirectorOnly` — this is the first director-facing
attendance screen; no caregiver-tablet consumer needs the full cross-location history view.

## `POST /api/attendance/check-in`

Request:
```json
{
  "childId": "guid",
  "locationId": "guid",
  "groupId": "guid",
  "date": "2026-07-09"
}
```

- Creates (or, if the child has no record yet for this `childId`/`locationId`/`date`) an
  `AttendanceRecord` with `status = present`, `checkInAt = now`, `plannedDurationMinutes` derived
  per data-model.md (matched against the contract at *this specific* `locationId` only, FR-006),
  `recordedBy` populated from `IShiftAttributionService`.
- If an existing record for this `childId`/`locationId`/`date` has `status = absent`, this
  transitions it to `status = present` with `checkInAt = now` instead of returning a conflict
  (FR-001a) — returns `200`, not `201`, since it's an update of an existing row.
- `201`: full `AttendanceRecordResponse` (below), for a genuinely new record.
- `409 errors.attendance.already_recorded` — a record already exists for this
  `childId`/`locationId`/`date` with `status = present` (FR-003/FR-012). The client's offline sync
  handler marks this as synced-with-conflict rather than retrying (research.md R4). An
  absence-mark request racing this one for the same key resolves via the same unique constraint —
  whichever write commits first wins, the other gets this same `409` (FR-005).
- `403 errors.attendance.closure_day` — an existing record for this child/location/date already
  has `status = closure` (FR-015).
- `404 errors.children.not_found` — `childId` doesn't resolve within this tenant.

## `POST /api/attendance/check-out`

Request:
```json
{ "childId": "guid", "locationId": "guid", "date": "2026-07-09" }
```

- Sets `checkOutAt = now` on the matching `present`-status record.
- `200`: updated `AttendanceRecordResponse`.
- `404 errors.attendance.not_found` — no matching record with `status = present` and `checkInAt`
  set and `checkOutAt` still null (FR-002a) — covers both "never checked in" and "already checked
  out" (idempotent-not-found; a second check-out attempt does not silently overwrite the first
  check-out time).

## `POST /api/attendance/absence`

Request:
```json
{
  "childId": "guid",
  "locationId": "guid",
  "date": "2026-07-09",
  "absenceJustified": true,
  "absenceReason": "Sick, doctor's note"
}
```

- Creates an `AttendanceRecord` with `status = absent`. Callable by device token (caregiver) or
  director JWT — both may set either justified or unjustified (spec.md: this is descriptive data
  entry, not an approval gate).
- `201`: full `AttendanceRecordResponse`.
- `409 errors.attendance.already_recorded` — same conflict rule as check-in.

## `PATCH /api/attendance/{id}`

Request: any subset of `{ status, checkInAt, checkOutAt, absenceJustified, absenceReason }`.

- `200`: updated `AttendanceRecordResponse`.
- `403 errors.attendance.edit_window_expired` — device-token request where the record's `date` is
  not today (`Europe/Brussels`) or the requesting device's `LocationId` claim doesn't match the
  record's `LocationId` (research.md R5, reusing feature 009's `ChildEventEditWindowPolicy`
  pattern).
- `403 errors.attendance.closure_status_immutable` — attempting to set `status = closure` directly
  (FR-015; reserved for a future feature 011 mechanism).
- `422 { errorKey: "errors.validation", fieldErrors }` — the merged result would violate a status
  invariant (FR-011a): `status = present` with no `checkInAt`, or `status = absent` with no
  `absenceJustified` set, or a `present`/`absent` status retaining a stale `checkInAt`/
  `checkOutAt`/`absenceJustified` value from a prior state — the standard `ValidationBehavior`
  pipeline response, not a bespoke shape.
- `404 errors.attendance.not_found` — unknown id.

## `DELETE /api/attendance/{id}`

- `204` on success.
- Same `403`/`404` semantics as `PATCH`. No soft-delete flag on this entity (unlike `ChildEvent`)
  — a mistaken record is corrected via `PATCH`, or removed outright since attendance has no
  parent-facing view that would need a "was this deleted" audit trail the way child events do;
  `UpdatedAt`/audit logging at the MediatR pipeline level (existing convention) still records who
  deleted what and when.

## `GET /api/attendance/bkr?locationId={id}`

- Returns the computed BKR Ratio (data-model.md) for `locationId`, right now, including `status`
  (`green`/`amber`/`red` per FR-007e's precise threshold comparison — never a UI-computed value).
- `200` always (never a 404) — an empty room with zero present/zero staff returns `presentCount:
  0, qualifiedStaffCount: 0, isNapTime: false, threshold: 8, status: "green"` (no breach possible
  with zero children present, per FR-007b's "at least one child present" precondition for a
  breach). A location with no `RoomShift` history at all behaves identically to one with a
  history but nobody currently checked in (both `qualifiedStaffCount: 0`).
- The caregiver tablet MUST poll this endpoint at least every 15 seconds and recompute locally
  within 5 seconds of a check-in/check-out/absence action taken on the same device (FR-008a/
  SC-006) — this is a client-side polling contract, not a server push mechanism.

## `GET /api/attendance/today`

**Added during implementation** — not in the original contract draft. The caregiver-tablet group
view needs to know each child's current present/absent state (to render check-in/out correctly on
load, and after another tablet's action), but every read endpoint besides `bkr` was `DirectorOnly`
until this one. Scoped to the device's own `LocationId` claim (no query parameter — never
client-supplied, same convention as check-in/check-out).

- Returns every `AttendanceRecordResponse` for the device's own location, for today
  (`Europe/Brussels`).
- `200` always, with an empty array when nothing has been recorded yet today.

## `GET /api/attendance?locationId={id}&date={yyyy-MM-dd}&before={cursor}&limit={n}`

- Director-web history/correction view. Returns up to `limit` (default 20, max 100) records for
  `locationId`/`date`, ordered `date DESC, id`, cursor-paginated (research.md R8) — same shape as
  `ListChildEventsQuery`.
- Response includes `nextCursor` (null when no more pages).

## Response shape (`AttendanceRecordResponse`)

```json
{
  "id": "guid",
  "childId": "guid",
  "locationId": "guid",
  "date": "2026-07-09",
  "status": "present",
  "checkInAt": "2026-07-09T07:32:00Z",
  "checkOutAt": null,
  "plannedDurationMinutes": 480,
  "absenceJustified": null,
  "absenceReason": null,
  "recordedBy": ["guid"],
  "createdAt": "2026-07-09T07:32:00Z",
  "updatedAt": "2026-07-09T07:32:00Z"
}
```

## Offline queue mapping (mobile, `entity_type = 'attendance_record'`)

| Local action | `operation` | `endpoint` | Notes |
|---|---|---|---|
| Check in | `create` | `POST /api/attendance/check-in` | Client-generated `id` included so the server uses it as the PK. |
| Check out | `update` | `POST /api/attendance/check-out` | Looked up by `childId`/`locationId`/`date`, not `id` — the client may not yet know the server-assigned id if check-in itself is still queued; see merge note below. |
| Mark absent | `create` | `POST /api/attendance/absence` | |

**Check-out-before-check-in-synced case** (corrected during implementation): no payload merge is
used here, unlike feature 009's sleep-end case. A check-out queued while its originating check-in
is still pending simply relies on the sync engine's existing FIFO ordering (queue rows replay in
`created_at` order) — the check-out naturally runs after the check-in it depends on. Merging
`checkOutAt` into the check-in row (as originally planned) was found to be actively wrong: check-in
and check-out are two separate endpoints with different request shapes, so a merged field would be
silently ignored by the check-in endpoint and the check-out would never actually apply.

Conflict policy (`onConflict` in the new sync handler): **server-wins** — on `409`
(`errors.attendance.already_recorded`), the queued row is marked synced with
`sync_error = "conflict: already recorded"` and not retried (research.md R4). This differs from
feature 009's `child_event` handler, whose general policy is "all writes preserved" (independent
append-only records); attendance has a real per-child-per-day uniqueness constraint, so a 409 here
is a genuine duplicate, not a second valid fact, and server-wins is the correct resolution.
