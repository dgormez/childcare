# Quickstart: Caregiver Scheduling (Weekly Staff Rota)

Validation scenarios proving the feature works end-to-end. Assumes a running local backend
(`dotnet run` in `backend/ChildCare.Api`) against a local/TestContainers PostgreSQL, a
seeded tenant with at least one `Location`, two `StaffProfile`s (one
`QualifiedCaregiver`, one `StudentVolunteer`), and a director account. See existing
`specs/*/quickstart.md` files for the shared local-setup prerequisites (unchanged by this
feature).

## Scenario 1 — Director builds a week's rota (US1)

1. Log in as director; `POST /api/staff-schedules` with a valid staff/location/date/time.
2. `GET /api/staff-schedules?locationId=...&weekStart=...` — the new entry appears.
3. `POST /api/staff-schedules` again for the same staff member, same date, a different
   *non-overlapping* location/time — both entries now appear (multi-location support).
4. `POST /api/staff-schedules` for the same staff member, same date, an *overlapping* time at
   a different location — expect `409 errors.staff_schedules.overlap`.
5. `PATCH /api/staff-schedules/{id}` on a future-dated entry — succeeds.
6. Manually backdate an entry's `Date` in the test fixture (or wait for a date to pass in an
   integration test using a fixed clock) and attempt `PATCH`/`DELETE` — expect
   `400 errors.staff_schedules.past_date`.

## Scenario 2 — Rota copy (US2)

1. Populate a full week for one location.
2. `POST /api/staff-schedules/copy-week` targeting the following week.
3. `GET /api/staff-schedules?locationId=...&weekStart=<next week>` — every source entry
   appears with shifted dates.
4. Create a `KdvClosureDay` (feature 011) for one day in the target week *before* copying;
   re-run the copy against a fresh source week — confirm that date's entries are skipped and
   listed in the response's `skipped` array with `reason: "closure_day"`.
5. Pre-populate one conflicting entry in the target week, then copy — confirm that slot is
   skipped (`reason: "existing_entry"`) while all other slots still copy successfully.

## Scenario 3 — Absence marking (US3)

1. `POST /api/staff-schedules/{id}/absence` with `{ "isAbsent": true, "absenceReason": "sick" }`
   on a today-dated entry for the `QualifiedCaregiver`.
2. `GET /api/staff-schedules/projected-on-duty?locationId=...&date=...&time=...` covering
   that shift — confirm the absent staff member is excluded from `projectedOnDutyCount`.
3. **Regression check**: call feature 010's `GET /api/attendance/bkr-ratio?locationId=...`
   (unchanged endpoint) — confirm its `qualifiedStaffCount` is unaffected by the absence
   marking (it only reflects `RoomShifts` check-in state, never `StaffSchedule`), proving the
   two computations stay decoupled (research.md R1).
4. Un-mark the absence on the same future-dated entry — confirm it reverts.

## Scenario 4 — Caregiver's own schedule read (US4)

1. Authenticate as the caregiver's own `TenantUser` account (not device-token).
2. `GET /api/staff-schedules/me` — confirm only that caregiver's own entries are returned,
   even when other staff have entries at the same location/date.
3. Authenticate as a caregiver with zero schedule entries — confirm an empty array, not an
   error.

## Web (director) smoke check

1. Log in to `web/` as director, navigate to `/scheduling`.
2. Confirm the week-grid view loads, an empty week shows the empty-state sentence + icon
   (design-system.md), and assigning a cell round-trips through the API above.
3. Confirm all visible strings resolve via `next-intl` in at least NL and EN (switch locale).
