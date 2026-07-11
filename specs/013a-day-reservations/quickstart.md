# Quickstart: Day Reservations

Validates the feature end-to-end once implemented. Assumes the standard local dev setup (backend
API running against Docker PostgreSQL, a seeded tenant with a director + a parent linked to a
child with an active contract — see `SETUP_CHECKLIST.md`).

## Prerequisites

- Backend running locally (`backend/ChildCare.Api`), migrations applied to the dev tenant schema.
- A tenant with: one active `Contract` for a child with at least one `ContractedDay` (e.g.
  Monday), one director `TenantUser`, one parent `TenantUser` linked via `Contact` to that child.
- `web/` running locally, logged in as the director.
- `parent-mobile/` running locally (Expo), logged in as the linked parent.

## Scenario 1 — Absence request, approved

1. In `parent-mobile`, tap "Mijn kind is ziek", pick tomorrow's date, submit.
   - Expect: request appears with `pending` status in the parent's own request history.
2. In `web`, open the "Verzoeken" queue.
   - Expect: the new request appears, newest first, showing child name, type, date, reason.
3. Approve it, setting justified = true.
   - Expect: queue no longer shows it as pending; `GET /api/attendance` (or the attendance
     history screen) shows a pre-registered `Absent` record for that child/date with
     `AbsenceJustified = true`.
4. Check the parent's push notification (or the notification log in dev).
   - Expect: an approval notification was sent.

## Scenario 2 — Exchange request, rejected at submission (closure day)

1. Determine a published closure date for the child's location (`GET /api/closure-calendar`).
2. In `parent-mobile`, tap "Dagwissel aanvragen", set `exchangeForDate` to the child's contracted
   Monday, and `requestedDate` to the closure date.
3. Submit.
   - Expect: `400 errors.day_reservations.closure_day`, no reservation created.

## Scenario 3 — Extra-day request, approved, no attendance side effect

1. In `parent-mobile`, tap "Extra dag aanvragen" for a future date with no matching contracted
   day.
2. Submit, then in `web` approve it from the queue.
   - Expect: reservation status becomes `approved`; no new `AttendanceRecord` is created (verify
     via `GET /api/attendance?date=...` — no row for that child/date until an actual check-in
     happens).

## Scenario 4 — Cancel and concurrency

1. Submit any request type, then cancel it from `parent-mobile` before the director acts.
   - Expect: status becomes `cancelled`; it disappears from the director's pending queue.
2. Submit a new request; have two director sessions attempt to approve/reject it near-
   simultaneously (or simulate via two rapid API calls).
   - Expect: exactly one succeeds (`200`), the other receives `409 errors.day_reservations.
     not_pending`.

See [contracts/day-reservations-api.md](contracts/day-reservations-api.md) for full request/
response shapes and [data-model.md](data-model.md) for the underlying schema.
