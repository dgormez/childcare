# API Contract: Day Reservations

Base path: `/api/day-reservations`. Follows `WaitingListEndpoints.cs`'s thin-endpoint,
`errorKey`-on-failure pattern.

## Parent-facing (`ParentOnly` policy)

### `POST /api/day-reservations`

Submit a new request.

Request body:
```json
{ "childId": "uuid", "type": "absence|extra|exchange", "requestedDate": "YYYY-MM-DD",
  "exchangeForDate": "YYYY-MM-DD|null", "reason": "string|null" }
```

Responses:
- `201 Created` — the created reservation (`status: "pending"`).
- `422` — `errors.validation` with `fieldErrors.requestedDate = "errors.day_reservations.
  past_date"` (FR-002) or `fieldErrors.exchangeForDate = "errors.day_reservations.
  missing_exchange_date"` (exchange without `exchangeForDate`) — pure calendar/presence checks,
  caught by `SubmitDayReservationCommandValidator` before the handler runs, so they use this
  codebase's standard FluentValidation error shape rather than a handler-level `errorKey`.
- `400` — `errors.day_reservations.not_contracted_day` (exchange, FR-003, DB-dependent so it's a
  handler-level check, not a validator rule); `errors.day_reservations.closure_day` (exchange
  target date, FR-004, same reason).
- `403` — `errors.day_reservations.child_not_linked` (FR-005) — returned uniformly whether the
  child doesn't exist or simply isn't linked to the caller, so a caller can't distinguish the two
  (mirrors `GetParentDailySummaryQueryHandler`'s existing precedent for the same reason feature
  001 genericized invitation-lookup failures).

### `POST /api/day-reservations/{id}/cancel`

Parent withdraws their own pending request (FR-014).

Responses:
- `200 OK` — updated reservation (`status: "cancelled"`).
- `403` — `errors.day_reservations.child_not_linked` (not the requesting parent's own request).
- `409` — `errors.day_reservations.not_pending` (already decided, FR-015).
- `404` — `errors.day_reservations.not_found`.

### `GET /api/day-reservations/mine?childId={optional}`

Parent's own request history across all statuses (FR-019), newest first.

Response: `200 OK` — array of reservations for the calling parent's linked children.

## Director-facing (`DirectorOnly` policy)

### `GET /api/day-reservations?status=pending`

The approval queue (FR-006). `status` optional, defaults to `pending`; newest-created first.

Response: `200 OK` — array of reservations including child name/location context needed for
FR-007's "no navigation elsewhere" requirement (denormalized child display name in the response
DTO, same pattern as `WaitingListEntry` responses already denormalize child/contact display
fields).

### `POST /api/day-reservations/{id}/approve`

Body (absence only): `{ "absenceJustified": true|false }`. Body ignored/optional for
`extra`/`exchange`.

Responses:
- `200 OK` — updated reservation (`status: "approved"`).
- `409` — `errors.day_reservations.not_pending` (FR-015/FR-016); `errors.day_reservations.
  closure_day` (absence approval where the date has since become a closure day, FR-011).
- `400` — `errors.day_reservations.missing_justified_flag` (absence approval without the flag).
- `404` — `errors.day_reservations.not_found`.

### `POST /api/day-reservations/{id}/reject`

Body: `{ "directorNotes": "string|null" }`.

Responses:
- `200 OK` — updated reservation (`status: "rejected"`).
- `409` — `errors.day_reservations.not_pending`.
- `404` — `errors.day_reservations.not_found`.

## Side effects (not separate endpoints)

- Approve/reject → `IExpoPushSender` notification to the requesting parent (FR-013), including
  `directorNotes` when present on rejection.
- Absence approve → `AttendanceRecord` created via the existing `MarkAbsentCommand` path
  (FR-010), carrying `AbsenceJustified` from the request body.
