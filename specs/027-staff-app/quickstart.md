# Quickstart: Staff App (Personal Rota & Leave)

Validation scenarios proving the feature works end-to-end. Assumes a running local backend
(`dotnet run` in `backend/ChildCare.Api`) against local/TestContainers PostgreSQL, a seeded
tenant with one `Location`, two `StaffProfile`s (each with `StaffLocationEligibility` for that
location, one with `ContractedDays: [Monday, Tuesday, Wednesday]`), and a director account. See
`specs/012-caregiver-scheduling/quickstart.md` for the shared rota-builder baseline this feature
extends.

## Scenario 1 — Director builds and publishes a week (US1)

1. Log in as director; `POST /api/staff-schedules` for a Thursday for the staff member whose
   `ContractedDays` excludes Thursday — expect `400`/grid-level rejection (director web: the
   cell is greyed and non-interactive; API: exercised via `Web (director) smoke check` below,
   since the grey-out is a client-side scheduling aid, not a server-side reject — the server
   still accepts a Thursday entry if sent directly, since a director may legitimately need to
   override in a real edge case; confirm this in the API layer explicitly so the behavior is
   documented, not assumed).
2. Build a full week of entries for the location.
3. `GET /api/staff-schedules/me` (as one of the staff members) — confirm the just-built week's
   entries are **not** returned yet (unpublished).
4. `POST /api/staff-schedules/{locationId}/publish` with that week's Monday.
5. `GET /api/staff-schedules/me` again — confirm the entries now appear, and confirm each
   affected staff member received a `SchedulePublished` push (via the fake push sender in
   tests).

## Scenario 2 — Staff views their own schedule (US2)

1. Authenticate as a staff member with a published multi-week schedule.
2. `GET /api/staff-schedules/me` — confirm only future, published, own-profile entries are
   returned, ordered by date.
3. Seed a second `StaffSchedule` row for the *same date, different location* for the same
   staff member — confirm both rows are returned (split-day edge case).
4. Authenticate as a different staff member with zero published entries — confirm an empty
   array, not an error.
5. Attempt to read another staff member's schedule via any endpoint using a client-supplied
   staff ID (if one existed) — confirm no such parameter is honored; the identity always comes
   from the JWT (FR-015).

## Scenario 3 — Sick report and on-the-fly cover (US3)

1. As the staff member with today's assignment, `POST /api/staff-schedules/report-sick`.
2. Confirm the assignment's `status` flips to `absent` and a `StaffLeaveRequest`
   (`type: sick`) is created.
3. As director, `GET /api/staff-schedules/{date}/sick-cover-candidates?...` — confirm it
   excludes staff without `StaffLocationEligibility` for that location and staff already
   assigned elsewhere that day/time.
4. `POST /api/staff-schedules/{id}/assign-cover` with an eligible replacement — confirm the
   original row keeps `status: absent` with `coverStaffId` set, a new `status: covered` row
   exists for the replacement, and both staff members receive `AssignmentChanged` pushes.
5. **Regression check**: call feature 010's BKR ratio endpoint before and after this scenario —
   confirm it's unaffected (extends `BkrDecouplingTests`, FR-016).
6. Attempt step 4 twice concurrently with two different candidates (simulated race) — confirm
   only one succeeds and the other gets `409 errors.staff_schedules.overlap` or an equivalent
   clean rejection, not a double-booked replacement (FR-018).

## Scenario 4 — Planned leave request approval (US4)

1. As a staff member, `POST /api/staff-leave-requests` for a future date range that overlaps
   two of their existing published `StaffSchedule` entries and one date with no entry.
2. As director, `GET /api/staff-leave-requests?status=pending` — confirm it appears.
3. `POST /api/staff-leave-requests/{id}/decide` with `{ "approve": true }` — confirm the two
   overlapping `StaffSchedule` rows flip to `absent`, the unscheduled date has no new row
   created, and the staff member receives a `LeaveRequestDecided` push.
4. Submit a second request, decide with `{ "approve": false }` — confirm no `StaffSchedule`
   rows change, only the notification fires.
5. As the staff member, `GET /api/staff-leave-requests/me` — confirm both requests show their
   correct final status, and confirm this endpoint never returns another staff member's rows.

## Web (director) smoke check

1. Log in to `web/` as director, navigate to `/scheduling`.
2. Confirm non-contracted-day cells and closure-day columns render greyed and non-interactive.
3. Build and publish a week; confirm a "published" indicator appears on the grid.
4. Mark a staff member absent for today; confirm the urgent sick-cover banner/prompt appears
   and walks through candidate selection.
5. Navigate to `/leave-requests`; confirm the pending queue lists a submitted request and
   approve/reject both work.
6. Confirm all visible strings resolve via `next-intl` in at least NL and EN.

## Staff app (`staff-mobile`) smoke check

1. `npx expo start` in `staff-mobile/`; log in as a staff member with a published schedule.
2. Confirm the home schedule view shows the correct week/day entries, a non-contracted day is
   visually de-emphasized, and a closure day shows "KDV gesloten".
3. Tap "Ik ben ziek", confirm the confirmation step, submit, and confirm the home view reflects
   the now-absent status on next load.
4. Submit a leave request via the form; confirm it appears (as pending) in the leave-requests
   list.
5. Confirm the notifications screen lists a received push event after the director publishes a
   week or decides a leave request (using Expo's push-test tooling / the fake sender in a dev
   build).
6. Toggle the device to airplane mode; confirm the already-fetched schedule still renders from
   cache, while the sick-report and leave-request actions surface a clear "needs connection"
   state rather than silently failing.
