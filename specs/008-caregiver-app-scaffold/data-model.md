# Phase 1 Data Model: Caregiver App Scaffold

## On-device SQLite (mobile, new)

### `offline_queue`

| Field | Type | Notes |
|---|---|---|
| `id` | `TEXT` (PK) | Client-generated UUID. |
| `tenant_id` | `TEXT` | The signed-in caregiver's tenant id — every query filters by this; cleared entirely on logout (FR-019). |
| `entity_type` | `TEXT` | `'child_event'` \| `'attendance_record'` \| ... — this feature registers none; only a synthetic `'_test_entity'` value is used by this feature's own tests (research.md R4). |
| `operation` | `TEXT` | `'create'` \| `'update'` \| `'delete'`. |
| `payload` | `TEXT` | JSON-serialized request body. |
| `endpoint` | `TEXT` | Relative API path to replay against. |
| `http_method` | `TEXT` | `'POST'` \| `'PATCH'` \| `'DELETE'`. |
| `created_at` | `TEXT` | ISO8601 — the ordering key for sync (FR-013). |
| `synced_at` | `TEXT?` | `NULL` = pending. Never deleted once synced (FR-014's audit-record requirement). |
| `sync_error` | `TEXT?` | Last attempt's error, or a conflict note when discarded per FR-014a's default. |

### `read_cache`

| Field | Type | Notes |
|---|---|---|
| `cache_key` | `TEXT` (PK) | e.g. `"children:groupId=<id>"`. |
| `tenant_id` | `TEXT` | Same tenant-scoping/logout-clearing rule as `offline_queue`. |
| `data` | `TEXT` | JSON. |
| `cached_at` | `TEXT` | ISO8601. |
| `expires_at` | `TEXT?` | Column exists for a future feature's use; this feature always writes `NULL` (FR-015a — no time-based expiry is implemented here). |

## Mobile in-memory state (Zustand `store/useStore.ts`)

- **Auth slice** (extended, not new): `{ staffProfileId, tenantUserId, email, role, accessToken, organisationSlug }` — `staffProfileId` populated from `GET /api/staff/me` immediately after login (research.md R6); `organisationSlug` retained so silent refresh (`POST /api/auth/refresh`) can resend it without re-prompting the caregiver.
- Habits-domain state (habits list, subscription status, etc.) is removed entirely — no longer relevant to the caregiver app.

## Backend additions (PostgreSQL, schema-per-tenant — no new tables, no migration)

### `GetStaffMeQuery` (new)

Reads existing `staff_profiles` (by `TenantUserId` = caller's JWT `ClaimTypes.NameIdentifier`) and `staff_location_eligibility` (all rows for that profile). Returns `StaffMeResponse(Guid StaffProfileId, string FirstName, string LastName, string Role, IReadOnlyList<Guid> EligibleLocationIds)`.

### `ListChildrenQuery` (extended)

New optional `GroupId` parameter — when present, joins `child_group_assignments` (`EndDate IS NULL`, i.e. currently active) filtered to that group, in addition to the existing `DeactivatedAt`/`IncludeDeactivated` filter. New optional caller-scoping parameters `CallerRole`/`CallerEligibleLocationIds` — when `CallerRole == "staff"`, results are further restricted to children with a currently-active `child_group_assignments` row whose `Group.LocationId` is in the caller's eligible locations (a child with no active group assignment is invisible to any caregiver's scoped view — spec.md Assumptions). When `CallerRole == "director"` (or the parameters are omitted), behavior is unchanged from feature 006.

### `ListGroupsQuery` (extended)

Same caller-scoping parameters as above — when the caller is `Staff`, results are filtered to `LocationId IN (caller's eligible locations)`; unchanged for `Director`.

## Reused, unmodified

- `Child`, `Group`, `ChildGroupAssignment`, `StaffProfile`, `StaffLocationEligibility`, `Location` (all features 004–006) — read-only from this feature's perspective, no schema changes.
- `LoginCommandHandler`/`RefreshTokenCommandHandler`/`LogoutCommandHandler` (feature 003) — the mobile client is updated to call these correctly (research.md R3); the backend contract itself does not change.
