# Contract: Children API (`/api/children/*`, `/api/contacts/*`, `/api/groups/*`)

All requests/responses are JSON. All error bodies are `{ "errorKey": "..." }` (constitution Principle IV) — new keys below are added to `backend/ERROR_KEYS.md` during implementation. Every route requires the `DirectorOnly` policy and is **not** tenant-exempt: `TenantMiddleware` (feature 002) resolves `ICurrentTenantService`/`ITenantDbContext` before any handler runs.

## `GET /api/children`

Query params: `includeDeactivated` (bool, default `false`).

- `200` — `ChildResponse[]`. Default: only children with `deactivatedAt == null` (FR-012).

## `GET /api/children/{id}`

- `200` — `ChildResponse`, including a freshly-signed `photoDownloadUrl` (`null` if no photo set).
- `404 errors.child.not_found` — no child with that id in the caller's own tenant schema.

## `POST /api/children`

Request (`CreateChildRequest`): `firstName`, `lastName`, `dateOfBirth`, plus optional `gender`, `nationality`, `allergiesDescription`, `allergySeverity`, `medicalConditions`, `dietaryRestrictions`, `gpName`, `gpPhone`, `healthInsuranceNumber`, `kindcode`.

- `201` — `ChildResponse` (FR-001, FR-002 — no contract or group required).
- `422 errors.validation` with `fieldErrors` — missing/empty `firstName`/`lastName`/`dateOfBirth`; over-length fields; `dateOfBirth` in the future (`errors.child.date_of_birth_in_future`).

## `PUT /api/children/{id}`

Request (`UpdateChildRequest`): same fields as create.

- `200` — updated `ChildResponse` (FR-003).
- `404 errors.child.not_found`
- `422 errors.validation` — same field rules as create.

## `POST /api/children/{id}/deactivate`

- `200` — `ChildResponse` with `deactivatedAt` set (FR-012).
- `404 errors.child.not_found`
- `409 errors.child.has_active_dependents` — a registered `IChildDeactivationGuard` (feature 007, not yet registered by this feature) reports an active dependent (FR-013). Currently unreachable — reserved for when 007 ships.
- Already-deactivated: idempotent `200`, unchanged.

## `POST /api/children/{id}/reactivate`

- `200` — `ChildResponse` with `deactivatedAt` cleared (FR-014).
- `404 errors.child.not_found`
- Already-active: idempotent `200`, no change.

## `POST /api/children/{id}/photo/upload-url`

- `200` — `RequestPhotoUploadUrlResponse` (`uploadUrl`, `objectPath`) — same shared shape feature 005 introduced (research.md R1). The client `PUT`s the image directly to `uploadUrl`.
- `404 errors.child.not_found`

---

## `GET /api/contacts`

- `200` — `ContactResponse[]` — every contact in the tenant, for a director to search when linking a sibling to an existing contact (research.md R6, FR-006).

## `POST /api/contacts`

Request (`CreateContactRequest`): `firstName`, `lastName`, `phone`, optional `email`, `locale`.

- `201` — `ContactResponse`.
- `422 errors.validation` — missing/invalid required fields.

## `PUT /api/contacts/{id}`

- `200` — updated `ContactResponse`. Since a `Contact` may be linked to multiple children, this update is visible on every linked child's file immediately (FR-006).
- `404 errors.contact.not_found`
- `422 errors.validation`

## `POST /api/children/{childId}/contacts`

Request (`LinkContactToChildRequest`): `contactId`, `relationship`, `canPickup`, `isPrimary`.

- `201` — `ChildContactResponse`. If this is the child's first ever contact link, `isPrimary` is forced `true` regardless of the request value (FR-007).
- `404 errors.child.not_found` / `404 errors.contact.not_found`
- `409 errors.contact.link_already_exists` — this `(childId, contactId)` pair is already linked (research.md R3) — use `PUT` to change the existing link's relationship/flags instead.
- `422 errors.validation` — missing `contactId`/`relationship`.

## `PUT /api/children/{childId}/contacts/{contactId}`

Request: `relationship`, `canPickup`, `isPrimary` — unambiguous since a `(childId, contactId)` pair identifies exactly one `ChildContact` row (research.md R3, revised — one relationship per pair, not a route param).

- `200` — updated `ChildContactResponse`. Setting `isPrimary: true` here clears the flag on the child's previous primary contact link (never deletes it — FR-007).
- `404 errors.child.not_found` / `404 errors.contact.not_found`

## `DELETE /api/children/{childId}/contacts/{contactId}`

- `200` — the link is removed (the `ChildContact` row is deleted; the underlying `Contact` record is untouched, and remains linked to any other children). If the removed link was this child's primary contact and at least one other contact link remains, the most-recently-linked remaining one is automatically promoted to primary (FR-007, found during spec review CHK005).
- `404 errors.child.not_found`
- Not currently linked: idempotent `200`, no change.

---

## `GET /api/groups`

Query params: `locationId` (optional filter).

- `200` — `GroupResponse[]`.

## `POST /api/groups`

Request (`CreateGroupRequest`): `name`, `locationId`.

- `201` — `GroupResponse` (FR-008).
- `404 errors.location.not_found` — `locationId` doesn't exist in this tenant, **or exists but is deactivated** (feature 004's existing key, reused — a group cannot be newly created against an inactive location, found during spec review CHK003).
- `422 errors.validation` — missing `name`.

## `GET /api/children/{childId}/groups`

- `200` — `ChildGroupAssignmentResponse[]` — full history, ordered most-recent-first.

## `POST /api/children/{childId}/groups`

Request (`AssignChildToGroupRequest`): `groupId`, `startDate`.

- `201` — the new `ChildGroupAssignmentResponse`. Any currently-open assignment (`endDate == null`) for this child has its `endDate` set to the day before `startDate` (FR-008a).
- `404 errors.child.not_found` / `404 errors.group.not_found`
- `422 errors.validation` — missing `startDate`/`groupId`.
- `422 errors.group.out_of_chronological_order` — the child has a currently-open assignment whose `startDate` is on or after this request's `startDate` (FR-008a, found during spec review CHK004) — assignments must be entered in chronological order.

---

## `GET /api/children/{childId}/vaccinations`

- `200` — `VaccinationResponse[]`, each including a computed `isDue` flag (FR-011).

## `POST /api/children/{childId}/vaccinations`

Request (`RecordVaccinationRequest`): `vaccineName`, `dateAdministered`, optional `nextDueDate`.

- `201` — `VaccinationResponse`.
- `404 errors.child.not_found`
- `422 errors.validation` — missing `vaccineName`/`dateAdministered`; `dateAdministered` in the future (`errors.vaccination.date_administered_in_future`, found during spec review CHK002).
