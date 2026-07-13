# API Contract: Child Profile UI

All endpoints below already exist (feature 006, `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs`).
This feature extends their request/response bodies only — no new routes, no new authorization
policies.

## `POST /api/children` (existing — `DirectorOnly`)

**Request body** (`CreateChildRequest`, extended):

```jsonc
{
  "firstName": "string",           // required
  "lastName": "string",            // required
  "dateOfBirth": "date",           // required, ≤ today
  "gender": "string | null",
  "nationality": "string | null",
  "allergiesDescription": "string | null",   // max 2000
  "allergySeverity": "string | null",
  "medicalConditions": "string | null",      // max 2000
  "dietaryRestrictions": "string | null",    // max 2000
  "gpName": "string | null",                 // max 200
  "gpPhone": "string | null",                // max 30
  "pediatricianName": "string | null",       // NEW — max 200
  "pediatricianPhone": "string | null",      // NEW — max 30
  "healthInsuranceNumber": "string | null",  // max 50
  "kindcode": "string | null"                // max 20
}
```

**Response**: `201 Created`, body = extended `ChildResponse` (see below), `Location:
/api/children/{id}`.

**Errors**: `400` validation (`errors.child.*_required`, `errors.child.*_too_long`) — unchanged
error-key scheme, extended with `errors.child.pediatrician_name_too_long` /
`errors.child.pediatrician_phone_too_long` for the two new fields.

## `PUT /api/children/{id}` (existing — `DirectorOnly`)

Same body shape as create (full-record replace, matching `UpdateChildRequest`'s existing
pattern — not a partial patch). Response: `200 OK`, extended `ChildResponse`. Errors: same `400`
validation set, plus existing `404 errors.child.not_found`.

## `GET /api/children` / `GET /api/children/{id}` (existing — `DeviceOrStaffOrDirector`)

**Response** (`ChildResponse`, extended):

```jsonc
{
  "id": "guid",
  "firstName": "string",
  "lastName": "string",
  "dateOfBirth": "date",
  "photoDownloadUrl": "string | null",
  "gender": "string | null",
  "nationality": "string | null",
  "allergiesDescription": "string | null",
  "allergySeverity": "string | null",
  "medicalConditions": "string | null",
  "dietaryRestrictions": "string | null",
  "gpName": "string | null",
  "gpPhone": "string | null",
  "pediatricianName": "string | null",     // NEW
  "pediatricianPhone": "string | null",    // NEW
  "healthInsuranceNumber": "string | null",
  "kindcode": "string | null",
  "deactivatedAt": "date-time | null",
  "createdAt": "date-time",
  "updatedAt": "date-time"
}
```

No change to who can call this (director full access; caregiver/staff via the existing
`DeviceOrStaffOrDirector` eligibility scoping already applied by `GetChildByIdQuery`/
`ListChildrenQuery` — this feature does not alter that scoping, per spec's Security
Considerations).

## `GET /api/children/{id}/health-summary` (existing — unchanged)

**Not touched by this feature** — see research.md R3. `ChildHealthSummaryResponse` keeps its
current shape (vaccines/health records only).

## Unaffected endpoints (unchanged, listed for completeness)

`POST /api/children/{id}/deactivate`, `POST /api/children/{id}/reactivate`,
`POST /api/children/{id}/photo/upload-url` — no request/response shape change.
