# Quickstart: Vaccine Catalog & Attachments

Validation scenarios proving the feature works end-to-end. See
[contracts/vaccine-catalog-api.md](./contracts/vaccine-catalog-api.md) for exact request/response
shapes and [data-model.md](./data-model.md) for the schema.

## Prerequisites

- Backend running locally (`dotnet run` in `backend/ChildCare.Api`) against Docker PostgreSQL, or
  the TestContainers integration test suite (`dotnet test`).
- A tenant with at least one director account and one child already provisioned.
- The `vaccine_types` catalog migration applied (seeds ~9 rows across two categories — see
  data-model.md).

## Scenario 1 — Director picks a catalog entry (US1)

1. `GET /api/vaccine-types`. Expect the seeded entries, active only, grouped
   `basisvaccinatieschema` / `aanbevolen_niet_gratis`.
2. `POST /api/children/{childId}/vaccine-records` with `{"vaccineName": "HPV", "vaccineTypeId":
   "<the HPV catalog entry's id>", "administeredOn": "2026-06-01"}`. Expect `201`, response
   `vaccineTypeId` echoes the same id.
3. As the platform operator, deactivate that catalog entry directly (`is_active = false`).
4. `GET /api/children/{childId}/vaccine-records`. Expect the record from step 2 still present
   with `vaccineName: "HPV"` — no error, no blank field (spec.md Edge Cases).

## Scenario 2 — Director's custom entry is remembered (US2)

1. `POST /api/children/{childId}/vaccine-records` with `{"vaccineName": "Rabiës",
   "vaccineTypeId": null, "administeredOn": "2026-06-01"}` for child A. Expect `201`.
2. `GET /api/vaccine-custom-entries`. Expect one entry, `name: "Rabiës"`.
3. `POST` the same body but `"vaccineName": "rabies "` (different case/trailing space) for a
   different child B in the same tenant. Expect `201`, and confirm via a direct DB check (or a
   second `GET /api/vaccine-custom-entries` still showing exactly one entry) that both records
   resolved to the **same** `tenant_custom_vaccine_entries` row — no duplicate created.
4. As a director in a *different* tenant, `GET /api/vaccine-custom-entries`. Expect an empty
   list — confirms tenant isolation (spec.md Acceptance Scenario 4).

## Scenario 3 — Attachment upload never blocks the record (US3)

1. `POST /api/children/{childId}/vaccine-records` with a valid body, no attachment step at all.
   Expect `201`, `attachmentDownloadUrl: null` in the response — confirms "no attachment
   attempted" is a valid non-error path.
2. `POST /api/children/{childId}/vaccine-records/{id}/attachment-upload-url` with
   `{"contentType": "image/jpeg"}`. Expect a signed `uploadUrl`.
3. `PUT` the returned `uploadUrl` directly (not through the API) with a small JPEG payload.
4. `GET /api/children/{childId}/vaccine-records`. Expect `attachmentDownloadUrl` now populated
   with a signed, time-limited URL; fetching it returns the uploaded bytes.
5. Repeat step 2 with `{"contentType": "application/zip"}`. Expect `422`, and confirm (via step 4
   style `GET`) the vaccine record from step 1 is completely unaffected — still saved, unchanged.

## Scenario 4 — Web picker UX (manual/browser check, no automated harness — per this repo's
static-review-only design-compliance process)

1. Open a child's Gezondheid tab, click "add vaccine record."
2. Confirm the vaccine-name field is a combobox: typing filters a dropdown grouped by category
   plus a separate "Other (used before)" group once at least one custom entry exists for the
   tenant.
3. Select a catalog entry — confirm the text field auto-fills.
4. Type a name matching nothing — confirm an "add custom" affordance appears and using it saves
   successfully.
5. Confirm the whole picker is operable via keyboard alone (Tab to focus, arrow keys to navigate
   options, Enter to select) — per `platform-rules.md`'s director-web keyboard-navigation
   requirement.
6. Save the record, then use the attachment control (mirrors `HealthRecordAttachmentControl`) to
   attach a photo — confirm "view attachment" appears after a successful upload.
