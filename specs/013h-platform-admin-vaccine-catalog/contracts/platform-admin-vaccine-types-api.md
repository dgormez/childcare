# API Contract: Platform-Admin Vaccine Catalog Endpoints

All endpoints below are **new**. All require `.RequireAuthorization("PlatformAdminOnly")`
(research.md R1) — a director-authenticated request whose JWT additionally carries the
`is_platform_admin` claim. A director without the claim receives `403 Forbidden`. 013g's existing
`GET /api/vaccine-types` and `GET /api/vaccine-custom-entries` are unchanged (FR-010) and not
listed here.

## `GET /api/platform-admin/vaccine-types`

Returns every catalog entry (active and inactive), including audit fields.

**Response 200**:
```json
[
  {
    "id": "uuid",
    "name": "string",
    "category": "MandatoryBasisvaccinatieschema | RecommendedNotFree | null",
    "sortOrder": 0,
    "isActive": true,
    "deactivatedByEmail": "string | null",
    "deactivatedAt": "ISO-8601 | null"
  }
]
```

## `POST /api/platform-admin/vaccine-types`

Creates a new catalog entry.

**Request**: `{ "name": "string", "category": "string | null" }`
`sortOrder` defaults to `max(existing) + 1`; `isActive` defaults to `true`.

**Response 201**: the created entry (same shape as the list item above).
**Response 400**: validation failure (empty name, invalid category).

## `PATCH /api/platform-admin/vaccine-types/{id}`

Renames and/or re-categorizes an entry (FR-005).

**Request**: `{ "name": "string", "category": "string | null" }`

**Response 200**: the updated entry.
**Response 404**: unknown `id`.

## `POST /api/platform-admin/vaccine-types/{id}/reorder`

Moves an entry up or down one position relative to its current neighbors (FR-006, research.md
R4 — mirrors `WaitingListTable`'s up/down button interaction, one step per call).

**Request**: `{ "direction": "up" | "down" }`

**Response 200**: the full, freshly-ordered list (same shape as the `GET` above) — returned in
full so the client doesn't need a separate re-fetch after every reorder click.
**Response 404**: unknown `id`.
**Response 400**: `direction` is `"up"` on the first entry or `"down"` on the last (no-op edge,
returned as a 400 rather than silently ignored, so the UI can decide how to surface it — expected
to simply disable the button at that boundary, per FR-006/Edge Cases).

## `POST /api/platform-admin/vaccine-types/{id}/deactivate`

Deactivates an active entry (FR-007, FR-008). No-op (200, unchanged audit fields) if already
inactive (Edge Cases).

**Response 200**: the updated entry, including newly populated `deactivatedByEmail`/
`deactivatedAt`.
**Response 404**: unknown `id`.

## `POST /api/platform-admin/vaccine-types/{id}/reactivate`

Reactivates a deactivated entry (FR-007), clearing its audit fields (research.md R2). No-op if
already active.

**Response 200**: the updated entry, with `deactivatedByEmail`/`deactivatedAt` now `null`.
**Response 404**: unknown `id`.

## Explicitly unchanged

- `GET /api/vaccine-types` (013g) — response shape, auth policy (`DirectorOnly`), and behavior are
  untouched (FR-010). A regression test asserts this endpoint's contract is unaffected by any
  endpoint above.
- `GET /api/vaccine-custom-entries` (013g) — untouched, out of this feature's scope entirely.
