# Contract: Locations API (`/api/locations/*`)

All requests/responses are JSON. All error bodies are `{ "errorKey": "..." }` (constitution Principle IV) — new keys below are added to `backend/ERROR_KEYS.md` during implementation. Every route requires the `DirectorOnly` policy and is **not** tenant-exempt — `TenantMiddleware` (feature 002) resolves `ICurrentTenantService`/`ITenantDbContext` before any handler runs, so a request with no/invalid `tenant_id` claim never reaches these handlers at all (feature 002's existing `errors.tenant.*` keys apply first).

## `GET /api/locations`

Query params: `includeDeactivated` (bool, default `false`).

- `200` — `LocationResponse[]`. Default: only locations with `deactivated_at == null` (FR-002, FR-008). `includeDeactivated=true` returns all locations for the organisation, active and deactivated (SC-005 historical/audit access).

## `GET /api/locations/{id}`

- `200` — `LocationResponse`.
- `404 errors.location.not_found` — no location with that id in the caller's own tenant schema (structurally, an id from a different organisation's schema can never match — FR-007).

## `POST /api/locations`

Request (`CreateLocationRequest`):

```json
{ "name": "...", "address": "...", "phone": "...", "email": "...", "maxCapacity": 24 }
```

- `201` — `LocationResponse`. `naamLocatie`/`dossiernummer`/`verantwoordelijke` are `null`, `flexPermission`/`boPermission` are `false` (FR-004/005).
- `422 errors.validation` with `fieldErrors` — missing/empty `name`/`address`/`phone`/`email`, invalid email format, or `maxCapacity` not a positive integer (FR-001, FR-010).

## `PUT /api/locations/{id}`

Request (`UpdateLocationRequest`) — full replace of both core and Opgroeien-settings fields:

```json
{
  "name": "...", "address": "...", "phone": "...", "email": "...", "maxCapacity": 24,
  "naamLocatie": "...", "dossiernummer": "...", "verantwoordelijke": "...",
  "flexPermission": false, "boPermission": false
}
```

- `200` — updated `LocationResponse`. Concurrent updates resolve last-write-wins (FR-017) — no `412`/conflict response exists for this route.
- `404 errors.location.not_found`
- `422 errors.validation` with `fieldErrors` — same field rules as create; `naamLocatie`/`dossiernummer`/`verantwoordelijke` remain optional (may be submitted as `null`/omitted, FR-004).

## `POST /api/locations/{id}/deactivate`

No request body.

- `200` — `LocationResponse` with `deactivatedAt` set.
- `404 errors.location.not_found`
- `409 errors.location.has_active_dependents` — a registered `ILocationDeactivationGuard` (features 005/007, not yet registered by this feature) reports active dependents (FR-012). No guard is registered by this feature, so this response is currently unreachable — reserved for when 005/007 ship.
- Already-deactivated location: idempotent `200`, `deactivatedAt` unchanged.

## `POST /api/locations/{id}/reactivate`

No request body.

- `200` — `LocationResponse` with `deactivatedAt` cleared (`null`) (FR-008, clarified).
- `404 errors.location.not_found`
- Already-active location: idempotent `200`, no change.

## `POST /api/locations/{id}/duplicate`

No request body — `{id}` is the source location.

- `201` — new `LocationResponse` with a new `id`, all copyable fields identical to the source, no reference back to the source (FR-015, research.md R5).
- `404 errors.location.not_found` — source location does not exist in the caller's tenant schema.
