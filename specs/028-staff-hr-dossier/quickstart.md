# Quickstart: Staff HR Dossier & Time Registration

Validation scenarios proving the feature works end-to-end. Assumes a running local backend
(`dotnet run` in `backend/ChildCare.Api`) against local/TestContainers PostgreSQL, a seeded
tenant with one `Location`, one `Group` at that location, a director account, and a `StaffProfile`
with `StaffLocationEligibility` for that location.

## Scenario 1 — Staff clocks in and out (US1)

1. As director, `PATCH /api/staff/{staffProfileId}/time-entry-functions` with a single function
   (`["kinderbegeleider"]`).
2. As that staff member, `POST /api/staff-time-entries/clock-in` with the location — confirm
   `200`, no `function` required in the request, and the response's `function` is
   `kinderbegeleider` (auto-selected, single-function case — FR-005).
3. `POST /api/staff-time-entries/clock-in` again — confirm `409
   errors.staff_time_entries.already_clocked_in` (FR-003).
4. `POST /api/staff-time-entries/clock-out` — confirm `200`, `clockedOutAt` set.
5. Repeat step 1 with two functions configured; clock in without a `function` in the request —
   confirm `400 errors.staff_time_entries.function_required`; retry with `function` supplied —
   confirm `200`.

## Scenario 2 — Director corrects a missed clock-out (US2)

1. Clock in and leave the entry open (skip clock-out).
2. As director, `GET /api/staff-time-entries?staffProfileId=...` — confirm the open entry is
   listed with `isLocked: false` (within the 7-day window).
3. `PATCH /api/staff-time-entries/{id}` with a `clockedOutAt` — confirm `200`, the entry is now
   closed.
4. Seed an entry with `clockedInAt` 8 days in the past (still open). Attempt `PATCH` — confirm
   `423 errors.staff_time_entries.locked`.
5. `POST /api/staff-time-entries/{id}/unlock`, then repeat the `PATCH` — confirm `200`.
6. `GET` the entry again — confirm it is still unlocked (no auto re-lock); `POST
   /api/staff-time-entries/{id}/relock`, then confirm `isLocked: true` again (still >7 days old,
   no active override).

## Scenario 3 — Director maintains an HR dossier (US3)

1. As director, `POST /api/staff/{staffProfileId}/documents/upload-url` with
   `contentType: application/pdf` — confirm a signed upload URL and object path.
2. `POST /api/staff/{staffProfileId}/documents` with `documentType: employment_contract`,
   `validUntil` 30 days from today.
3. `GET /api/staff/{staffProfileId}/documents` — confirm the document is listed with a working
   signed download URL.
4. `GET /api/staff/contracts-expiring` — confirm this staff member appears (within 60 days).
5. Upload a second document with `documentType: qualification`, no `validUntil` — confirm it does
   **not** appear in `contracts-expiring`.
6. `GET /api/staff/{other-staff-with-no-documents}/documents` — confirm `200` with an empty array,
   not an error (empty-state — Acceptance Scenario 4).

## Scenario 4 — Director generates the medewerkersbeleid subsidy report (US4)

1. Seed `AttendanceRecord`s with `CheckInAt`/`CheckOutAt` for several children at the location
   across a date range, and closed `StaffTimeEntry` rows for the staff member across the same
   range, tagged with a known function.
2. `GET /api/reports/staff-hours?locationId=...&from=...&to=...` — confirm `totalChildHours`
   matches a manual sum of the seeded attendance durations, and `byFunction[].totalStaffHours`
   matches the seeded time-entry durations for that function.
3. Seed one *open* time entry inside the period — confirm the report total is unchanged
   (excluded, FR-019/Acceptance Scenario 4).
4. Query a date range with zero time entries — confirm `200` with `totalStaffHours: 0` and
   `ratio: null` (not a 500 or a divide-by-zero — Acceptance Scenario 2).
5. `GET /api/reports/staff-hours/export?...` — confirm `200 text/csv` with one row per closed time
   entry, and that its per-row durations sum to the same `totalStaffHours` figure from step 2.

## Web (director) smoke check

1. Log in to director-web; open Staff → a staff member row → confirm the new detail page loads
   with **Dossier** and **Tijdsregistraties** tabs.
2. Upload a document on the Dossier tab; confirm it appears in the list immediately.
3. Open the director dashboard; confirm the new "Personeel — verlopende contracten" block shows
   the staff member seeded with a near-expiry contract, and that clicking it navigates to their
   detail page.
4. Open Rapporten → Personeel; select the seeded location/period; confirm the ratio table renders
   and the CSV download link works.

## Staff-mobile smoke check

1. Log in as the seeded staff member; confirm the schedule screen (app home) shows a "Begin
   dienst" action.
2. Tap it; confirm it flips to "Einde dienst" without navigating away.
3. Turn off connectivity (simulator airplane mode); confirm the action is disabled with a
   connectivity message, matching `report-sick.tsx`'s existing offline behavior.
