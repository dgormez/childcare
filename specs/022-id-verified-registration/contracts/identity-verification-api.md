# API Contract: ID-Verified Registration

## `POST /api/children/{id}/identity-verification` (NEW — `DirectorOnly`)

Records or corrects a child's identity verification (FR-001, FR-004, FR-005).

**Request body** (`VerifyChildIdentityRequest`):

```jsonc
{
  "documentType": "birth_certificate | kids_id | eid | passport | other",  // required
  "note": "string | null"                                                  // optional, max 500
}
```

**Response**: `200 OK`, extended `ChildResponse` (see data-model.md). `IdVerifiedAt`/
`IdVerifiedByEmail` reflect the calling director and current server time; `FirstIdVerifiedAt`/
`FirstIdVerifiedByEmail` are set only if this is the first verification, otherwise unchanged.

**Errors**: `422` `{ errorKey: "errors.validation", fieldErrors: { DocumentType:
"errors.child.document_type_required" } }` (missing/invalid `documentType`) or `{ ...,
fieldErrors: { Note: "errors.child.identity_note_too_long" } }` — this codebase's shared
`ValidationBehavior`/global exception handler shape (Program.cs), not a per-field `400` (013h's
shipped-note already established this correction once; carried forward here). `404
errors.child.not_found`.

**Auth**: `VerifiedByUserId`/`VerifiedByEmail` are resolved server-side from the caller's JWT
claims (`ClaimTypes.NameIdentifier`/`ClaimTypes.Email`) — never accepted from the request body,
mirroring feature 013h's `DeactivateVaccineTypeCommand` pattern.

## `PUT /api/children/{id}/nrn` (NEW — `DirectorOnly`)

Sets or updates a child's National Register Number (FR-009, FR-010, FR-011).

**Request body** (`SetChildNrnRequest`):

```jsonc
{
  "nrn": "string"   // required, 11 digits after stripping non-digit separators
}
```

**Response**: `200 OK`, extended `ChildResponse` — `NrnLast4` reflects the newly saved value. The
raw `nrn` is never echoed back.

**Errors**: `422` with `fieldErrors: { Nrn: "errors.child.nrn_invalid_format" }` (not 11 digits
after normalization), `404 errors.child.not_found`.

## `POST /api/contacts/{id}/identity-verification` (NEW — `DirectorOnly`)

Records or corrects a contact's (parent/guardian's) identity verification (FR-002, FR-004,
FR-005). Same request/error shape as the child endpoint above, scoped to `Contact`.

**Response**: `200 OK`, extended `ContactResponse`.

**Errors**: `422` with `fieldErrors: { DocumentType: "errors.contact.document_type_required" }`
or `{ Note: "errors.contact.identity_note_too_long" }`, `404 errors.contact.not_found`.

## `GET /api/children` / `GET /api/children/{id}` (existing — unchanged auth, extended response)

**Response** (`ChildResponse`, extended — see data-model.md for the full field list). Enables:

- The `/children` list page badge (FR-007a) — reads `idVerifiedAt` directly, no new request.
- The child-detail "Identiteit bevestigen" section's read-only display when already verified.

**Role-gated fields (FR-015, research.md R8)**: `idVerifiedAt`, `idVerifiedByEmail`,
`idDocumentType`, `idDocumentNote`, `firstIdVerifiedAt`, `firstIdVerifiedByEmail`, `nrnLast4` are
present only when the caller is a Director. A Staff caller or a caregiver-tablet device-token
caller reading these same two routes receives `null` for every one of these fields — this route's
authorization (`DeviceOrStaffOrDirector`) is unchanged, but its response content now varies by
caller role for this field subset specifically.

## `GET /api/children/{childId}/contacts` (existing — unchanged auth)

**Response** (`ChildContactResponse`, extended — see data-model.md). Enables the per-contact-row
verification badge/action inside `ChildContactsTab` without a second request per contact.

## `GET /api/reports/data-completeness` (existing — extended, feature 018)

**Response** (`DataCompletenessResponse`, unchanged shape): `flags: DataCompletenessFlagResponse[]`
gains a new possible `type` value, `"missing_identity_verification"`, alongside the four existing
ones (`missing_pickup_contact`, `overdue_vaccine`, `missing_qualification`, `missing_pin`).
`subjectType` is always `"child"` for this flag; `detail` is always `null` (no extra context
needed beyond the child's name, per the existing pattern for `missing_pickup_contact`).

Scoping (research.md R5): every active (`DeactivatedAt == null`) child without `IdVerifiedAt`,
optionally intersected with `locationId` via the child's current `ChildGroupAssignment` — **not**
the handler's existing attendance-linked child set, since a brand-new, not-yet-attended enrolment
is exactly the case this flag needs to catch.
