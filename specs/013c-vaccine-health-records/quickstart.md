# Quickstart: Vaccine & Health Records

Validation scenarios proving the feature works end-to-end. See [contracts/vaccine-health-records-api.md](./contracts/vaccine-health-records-api.md)
for exact request/response shapes and [data-model.md](./data-model.md) for the schema.

## Prerequisites

- Backend running locally (`dotnet run` in `backend/ChildCare.Api`) against Docker PostgreSQL, or
  the TestContainers integration test suite (`dotnet test`).
- A tenant with at least one director account and one child already provisioned (reuse an
  existing dev-seed tenant/child, or feature 001/006's onboarding + child-creation flows).
- For the caregiver scenario: a paired kiosk device token (feature 008a) or a staff account
  assigned to the child's location.

## Scenario 1 — Director records a vaccine, sees it in history (US1)

1. `POST /api/children/{childId}/vaccine-records` with `{"vaccineName": "DTP", "administeredOn":
   "2026-06-01", "nextDueDate": "2026-08-01"}`, director JWT. Expect `201`.
2. `GET /api/children/{childId}/vaccine-records`. Expect the record in the response, most-recent
   first.
3. `PUT` the same record's `nextDueDate` to a new date. `GET` again — confirm the update.
4. `DELETE` the record. `GET` again — confirm it no longer appears (soft-deleted).

## Scenario 2 — Director records a health record with an attachment (US2)

1. `POST /api/children/{childId}/health-records` with a `doctor_note` record. Expect `201`,
   `attachmentDownloadUrl: null`.
2. `POST /api/children/{childId}/health-records/{id}/attachment-upload-url` with `{"contentType":
   "application/pdf"}`. Expect a signed `uploadUrl`.
3. `PUT` the returned `uploadUrl` directly (not through the API) with a small PDF payload.
4. `GET /api/children/{childId}/health-records`. Expect `attachmentDownloadUrl` now populated with
   a signed, time-limited URL; fetching it returns the uploaded PDF bytes.
5. Repeat step 1 with no attachment step at all — confirm the record still saves successfully
   (FR-007's "attachment failure never blocks the record" also implies "no attachment attempted at
   all" is a valid, non-error path).

## Scenario 3 — Director dashboard shows due-soon vaccines (US3)

1. Create three vaccine records across two children: one with `nextDueDate` 10 days from today,
   one with `nextDueDate` 5 days in the past (overdue), one with `nextDueDate` 60 days from today.
2. `GET /api/vaccine-records/due-soon`. Expect exactly the first two children in the response, the
   overdue one flagged `isOverdue: true`, sorted soonest/most-overdue first. The 60-day child is
   absent.
3. Confirm a tenant/director with zero due-soon vaccines gets `200` with an empty array, not an
   error.

## Scenario 4 — Caregiver reads a child's health summary, read-only (US4)

1. As a director, create an active `allergy` health record and a due-soon vaccine record for a
   child assigned to Location A.
2. As a caregiver device/staff account eligible for Location A: `GET
   /api/children/{childId}/health-summary`. Expect the allergy record and the due-soon vaccine in
   the response.
3. Attempt any write against `/api/children/{childId}/vaccine-records` or `/health-records` using
   the same caregiver credentials. Expect `401`/`403` — confirms FR-014 (read-only enforcement).
4. As a caregiver device/staff account *not* eligible for the child's location: repeat step 2.
   Expect `404` (same as a nonexistent child id, per FR-015/research.md R3).

## Scenario 5 — GDPR export exclusion (FR-016)

No bulk export/email-summary feature exists yet in this codebase (research.md, plan.md Technical
Context) — this scenario is a forward-looking regression test, not an end-to-end UI check. Confirm
via an integration test that no existing serialization path (e.g. any `ToResponse`/DTO shared with
an export mechanism) accidentally includes `VaccineRecord`/`HealthRecord` data without an explicit
opt-in flag, so the day a bulk-export feature ships, it inherits the exclusion by construction
rather than by remembering to add it.

## Scenario 6 — Legacy data migration (research.md R1)

1. Against a pre-migration tenant schema seeded with a `vaccination_records` row (simulating an
   existing tenant with real legacy data), apply this feature's migration.
2. Confirm the row now exists in `vaccine_records` with the same `child_id`/`vaccine_name`/
   `administered_on`/`next_due_date`, `recorded_by` null, `deleted_at` null.
3. Confirm `vaccination_records` no longer exists and `GET/POST /api/children/{id}/vaccinations`
   returns `404` (route removed).
