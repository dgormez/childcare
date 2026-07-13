# API Contract: Vaccine Catalog & Attachments

Extends `specs/013c-vaccine-health-records/contracts/vaccine-health-records-api.md`. Only new or
changed endpoints/shapes are documented here — everything else in that contract (e.g.
`DELETE /api/children/{childId}/vaccine-records/{id}`) is unchanged.

## Vaccine Catalog — base path `/api/vaccine-types`

### `GET /api/vaccine-types`

**Auth**: `DirectorOnly`.

**Response**: `200 OK`, list of `VaccineTypeResponse`, active entries only, ordered by
`category`, then `sortOrder`:

```json
[
  { "id": "...", "name": "DTPa-IPV-Hib-HepB", "category": "basisvaccinatieschema", "sortOrder": 1 },
  { "id": "...", "name": "HPV", "category": "basisvaccinatieschema", "sortOrder": 5 },
  { "id": "...", "name": "RSV (zuigelingen)", "category": "aanbevolen_niet_gratis", "sortOrder": 1 }
]
```

No pagination — the catalog is small and bounded (spec.md Technical Requirements). Read-only:
no create/rename/reorder/deactivate endpoint ships in this feature (spec.md Assumptions).

## Tenant Custom Vaccine Entries — base path `/api/vaccine-custom-entries`

### `GET /api/vaccine-custom-entries`

**Auth**: `DirectorOnly`.

**Response**: `200 OK`, list of `CustomVaccineEntryResponse` for the caller's own tenant only,
ordered alphabetically by `name`:

```json
[
  { "id": "...", "name": "Rabiës" }
]
```

Entries are created implicitly (see below) — there is no direct `POST` to this collection; a
director never explicitly "creates a custom entry," they just save a vaccine record with a
non-catalog name.

## Vaccine Records — changes to the existing `/api/children/{childId}/vaccine-records` endpoints

### `POST` / `PUT` request shape — adds one field

```json
{
  "vaccineName": "DTP",
  "vaccineTypeId": "3fa85f64-...",
  "doseNumber": 2,
  "administeredOn": "2026-06-01",
  "nextDueDate": "2026-12-01",
  "administeredBy": "Dr. Peeters",
  "notes": null
}
```

- `vaccineTypeId` (nullable, new): the catalog entry the director picked, if any. When present,
  the server trusts `vaccineName` as the (possibly director-edited) display text and stores the
  reference as-is (spec.md FR-004 — editing the auto-filled text does not clear the reference).
- When `vaccineTypeId` is null and `vaccineName` matches no active catalog entry, the server
  resolves (or creates) a `tenant_custom_vaccine_entries` row via the case/whitespace-insensitive
  lookup (research.md R3) and stores that reference instead — this happens automatically, the
  client never sends a "custom entry id" itself.
- The server does not validate `vaccineTypeId` against a hardcoded active-only check strictly —
  an id pointing at a now-deactivated entry is still accepted (a director could be re-saving/
  editing an old record that referenced it), consistent with spec.md's "deactivated entries still
  render correctly" requirement working in both directions.

**New failure mode**:
- `422` — `vaccineTypeId` does not resolve to any `vaccine_types` row at all (not just inactive —
  a nonexistent id) → `errors.vaccine_records.vaccine_type_not_found`.

### Response shape — adds two fields

```json
{
  "id": "...",
  "childId": "...",
  "vaccineName": "DTP",
  "vaccineTypeId": "3fa85f64-...",
  "attachmentDownloadUrl": null,
  "doseNumber": 2,
  "administeredOn": "2026-06-01",
  "nextDueDate": "2026-12-01",
  "administeredBy": "Dr. Peeters",
  "notes": null,
  "recordedBy": "...",
  "createdAt": "...",
  "updatedAt": "..."
}
```

- `vaccineTypeId` (nullable): echoes back whichever catalog entry is referenced, or `null` if
  none (custom-entry-backed or legacy pre-013g record).
- `attachmentDownloadUrl` (nullable): a freshly-signed download URL, generated per read — never
  the raw object path (identical pattern to `HealthRecordResponse.attachmentDownloadUrl`, 013c).

### `POST /api/children/{childId}/vaccine-records/{id}/attachment-upload-url` (new)

**Auth**: `DirectorOnly`.

**Request**: `{ "contentType": "application/pdf" }` (allowed: `application/pdf`, `image/jpeg`,
`image/png`; max 10MB — identical constraint to `HealthRecord`'s attachment, spec.md
Clarifications; `422 errors.vaccine_records.attachment_too_large` if a client bypasses the
client-side check).

**Response**: `200 OK`, `{ "uploadUrl": "https://storage.googleapis.com/...", "expiresInSeconds":
900 }` — identical shape and behavior to `HealthRecord`'s attachment-upload-url endpoint
(research.md R4: same port, `category: "vaccine-records"`), including the "object path set at
call time, not after upload completion" caveat documented in
`vaccine-health-records-api.md`'s equivalent endpoint.

**Failure modes**:
- `422` — unsupported `contentType`, or file exceeds 10MB.
- `404` — `childId`/`id` does not resolve within the tenant.
