# Waiting List API Contract

All endpoints require tenant-scoped authentication (`TenantMiddleware`) and `DirectorOnly`
authorization — no caregiver or parent access (FR-016/FR-017).

## GET `/api/waiting-list?locationId={guid}&status={status?}`

Returns waiting-list entries for one location, sorted by `priority` ascending. `status`
defaults to `waiting` when omitted (FR-003); pass an explicit status (`waiting|offered|
enrolled|withdrawn`) to see another slice, or `all` to see every status.

Response `200`:

```json
[
  {
    "id": "uuid",
    "childFirstName": "Emma",
    "childLastName": "Peeters",
    "dateOfBirth": "2025-03-10",
    "contactName": "Sophie Peeters",
    "contactEmail": "sophie@example.com",
    "contactPhone": "+32...",
    "locationId": "uuid",
    "requestedStartDate": "2026-09-01",
    "priority": 0,
    "status": "waiting",
    "notes": null,
    "childId": null,
    "isDuplicate": false,
    "registeredAt": "2026-07-10T12:00:00Z",
    "updatedAt": null
  }
]
```

Errors: `403 errors.auth.forbidden`, `404 errors.locations.not_found`, `400 errors.validation`.

## POST `/api/waiting-list`

Creates a waiting-list entry. `status` always starts `waiting`; `priority` is always appended
(FR-002) — neither is settable at creation.

Request:

```json
{
  "childFirstName": "Emma",
  "childLastName": "Peeters",
  "dateOfBirth": "2025-03-10",
  "contactName": "Sophie Peeters",
  "contactEmail": "sophie@example.com",
  "contactPhone": "+32...",
  "locationId": "uuid",
  "requestedStartDate": "2026-09-01",
  "notes": null
}
```

Response: `201` with the created entry (shape above).

Errors: `400 errors.validation` (missing required fields, invalid email, future DOB),
`404 errors.locations.not_found`.

## PATCH `/api/waiting-list/{id}`

Edits an entry's non-lifecycle fields (name, DOB, contact, requested start date, notes,
location). Does not change `status` or `priority` — see the dedicated endpoints below.

Response: `200` with the updated entry.

Errors: `404 errors.waiting_list.not_found`, `400 errors.validation`.

## POST `/api/waiting-list/{id}/reorder`

Moves a `waiting`-status entry up or down within its location's queue (FR-005). Not
reorderable once the entry has moved past `waiting` (FR-005, research.md R6).

Request:

```json
{ "direction": "up" }
```

`direction` is `up|down`.

Response: `200` with the affected location's full re-sorted list (same shape as the GET list
endpoint) so the client can update in one round trip.

Errors: `404 errors.waiting_list.not_found`, `409
errors.waiting_list.not_reorderable_in_current_status` (entry is not `waiting`), `400
errors.validation` (already at the top/bottom, no-op).

## POST `/api/waiting-list/{id}/status`

Transitions an entry's status (FR-007).

Request:

```json
{ "status": "offered" }
```

`status` is one of `waiting|offered|enrolled|withdrawn`. Only the transitions in FR-007's
allow-list succeed.

Response: `200` with the updated entry. When the transition is `waiting → offered` and
`contactEmail` is present, an email notification is sent (FR-008) before the response returns;
absence of `contactEmail` does not fail the request (FR-008).

Errors: `404 errors.waiting_list.not_found`, `409
errors.waiting_list.invalid_status_transition` (FR-007 — any transition outside the allow-list,
including anything originating from `enrolled`/`withdrawn`).

## POST `/api/waiting-list/{id}/link-child`

Links an entry to an existing child record, or creates a new one first (FR-010/FR-011/FR-012).
Callable regardless of the entry's current status (FR-012 — an `enrolled` entry left unlinked
remains linkable later).

Request (link existing):

```json
{ "childId": "uuid" }
```

Request (create new, pre-filled):

```json
{ "createNewChild": true }
```

When `createNewChild: true`, the handler internally issues feature 006's `CreateChildCommand`
with `firstName`/`lastName`/`dateOfBirth` copied from the entry (research.md R5), then links
the resulting child.

Response: `200` with the updated entry, `childId` populated.

Errors: `404 errors.waiting_list.not_found`, `404 errors.children.not_found` (bad `childId`),
`400 errors.validation` (neither `childId` nor `createNewChild` provided, or both provided).

## GET `/api/waiting-list/occupancy?locationId={guid}&from={yyyy-MM-dd}&to={yyyy-MM-dd}`

Forward-looking projected occupancy for a location across a date range (FR-013/FR-014/FR-015).
Computed from active contracts and `Location.MaxCapacity`, never from attendance
(research.md R1).

Response `200`:

```json
[
  { "date": "2026-09-01", "freeCapacity": 3, "closed": false },
  { "date": "2026-09-02", "freeCapacity": null, "closed": true }
]
```

`freeCapacity` is `null` whenever `closed: true` — a closed date never reports a numeric count
(FR-015).

Errors: `404 errors.locations.not_found`, `400 errors.validation` (range too large / `to`
before `from`).
