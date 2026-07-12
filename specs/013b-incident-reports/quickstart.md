# Quickstart: Incident Reports

## Prerequisites

- Backend running locally against Docker PostgreSQL (`docker compose up`, per repo root README),
  migrations applied (`dotnet ef database update` against the tenant schema, or via the
  tenant-migration CLI, per feature 002).
- A tenant with at least one location, one child, and one caregiver checked in via a room shift
  (feature 008a) — needed to see a non-empty `reportedBy`.
- `mobile/` running against that backend (Expo dev client) and `web/` running (`npm run dev`).

## Scenario 1 — File a report on the tablet (Story 1)

1. Open the caregiver app, sign into a room (008a), check in as a caregiver.
2. Navigate to a child's profile (`app/(app)/child/[id].tsx`).
3. Tap "Incident melden."
4. Leave `description` empty and attempt submit → verify a validation error naming the missing
   field.
5. Fill in `description`, select an injury type chip, submit.
6. Verify the incident appears in the child's local view (optimistic), and via
   `GET /api/incident-reports/{id}` that `reportedBy` contains the checked-in caregiver's id.

## Scenario 2 — Director reviews and exports (Story 2)

1. Sign into `web/` as a director.
2. Open `/incidents` — verify the report from Scenario 1 appears, unreviewed indicator visible.
3. Apply a child filter and a date-range filter — verify only matching reports remain.
4. Open the report's detail view — verify the unreviewed indicator clears
   (`GET /api/incident-reports/{id}` sets `reviewedAt`).
5. Click "Export PDF" — verify the download contains the location's name/address/`Dossiernummer`
   and a caregiver signature line.

## Scenario 3 — Immutability (Story 3)

1. Using a report older than 24 hours (seed one directly in the test database with a backdated
   `CreatedAt`, or wait), attempt `PUT /api/incident-reports/{id}` with a changed `description` →
   verify `409` and the stored description is unchanged.
2. `PUT` with only `followUp` set → verify `200` and the note is saved.

## Scenario 4 — Offline filing (Story 4)

1. Disable network on the mobile device/simulator.
2. Repeat Scenario 1, steps 3–5 — verify the report appears locally with a pending-sync indicator
   and no error.
3. Re-enable network — verify the sync engine replays the queued report and the pending-sync
   indicator clears, and the report becomes visible in `/incidents`.

## Reference

See [contracts/incident-reports-api.md](contracts/incident-reports-api.md) for full request/
response shapes and [data-model.md](data-model.md) for the entity/validation rules.
