# Quickstart: Reservation Settings

## Prerequisites

- Backend running locally (`dotnet run` in `backend/ChildCare.Api`) against a tenant schema with
  a director account, at least one location, and a child with an active contract at that
  location (see 007's quickstart for contract setup).
- A parent account linked to that child (013's linkage).

## Backend validation (no UI needed)

1. Log in as director, `GET /api/locations/{id}` → confirm the response includes
   `reservationAbsencesMode: "approval"`, `reservationExtrasMode: "approval"`,
   `reservationSwapsMode: "disabled"`, `reservationNoticeHours: 0` on a location that has never
   had settings explicitly saved (FR-002 default).
2. `PUT /api/locations/{id}/reservation-settings` with
   `{ "absencesMode": "disabled", "extrasMode": "approval", "swapsMode": "disabled", "noticeHours": 0, "confirmDespitePending": false }`.
   Confirm `200 OK` and the location now reports `reservationAbsencesMode: "disabled"`.
3. Log in as the linked parent, `POST /api/day-reservations` with
   `{ "childId": "...", "type": "absence", "requestedDate": "<tomorrow>" }`. Confirm `403
   Forbidden`, `errorKey: "errors.day_reservations.request_type_disabled"`, and no reservation
   row was created (`GET /api/day-reservations/mine` as the parent stays empty).
4. As director, `PUT .../reservation-settings` again with `absencesMode: "informational"`.
   Resubmit the parent's absence POST. Confirm `201 Created` with `status: "approved"`,
   `decidedBy: null`, `decidedAt` set, and (if the requested date + child/location combination has
   no closure conflict) a corresponding `AttendanceRecord` exists for that child/date/location
   (FR-008, mirrors 013a's approval-time attendance write).
5. As director, `GET /api/day-reservations?status=all` — confirm the auto-approved row from step 4
   appears with `decidedBy: null`.
6. Set `noticeHours: 24` via the settings endpoint. Submit an absence request for *today* as the
   parent. Confirm `422` with a `fieldErrors.requestedDate` key
   `errors.day_reservations.notice_period_required`.
7. Create a `pending` extra-day request as the parent (with `extrasMode` still `approval`). As
   director, attempt `PUT .../reservation-settings` changing `extrasMode` to `disabled` with
   `confirmDespitePending: false`. Confirm `409` with `pendingCounts.extra >= 1`. Resubmit with
   `confirmDespitePending: true` — confirm `200 OK`, and the earlier pending request is still
   `status: "pending"` when re-fetched (FR-005/FR-014).

## Web (director) validation

1. Navigate to `/locations` — confirm a real location list renders (no longer the
   "Nog niet beschikbaar" placeholder).
2. Open a location, switch to the "Reserveringsinstellingen" tab — confirm the three dropdowns and
   notice-hours field reflect current values, and saving a change that would strand pending
   requests shows the warning dialog before committing (User Story 4).

## Parent-mobile validation

1. With a location's `reservation_swaps_mode = disabled`, open the parent app home screen for a
   parent whose only child is at that location — confirm the "Dagwissel aanvragen" quick action is
   absent.
2. Attempt the same request type via the absence/extra/exchange form directly for a
   multi-child parent where only one child's location disables it — confirm the form still opens
   (home screen stays generic) but blocks submission with a clear inline message once that child
   is selected.
