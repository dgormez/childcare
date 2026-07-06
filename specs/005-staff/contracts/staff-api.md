# Contract: Staff API (`/api/staff/*`)

All requests/responses are JSON. All error bodies are `{ "errorKey": "..." }` (constitution Principle IV) — new keys below are added to `backend/ERROR_KEYS.md` during implementation. Every route requires the `DirectorOnly` policy **except** `accept-invitation`, which is unauthenticated (the invitee has no account/token yet) — and every `DirectorOnly` route is **not** tenant-exempt: `TenantMiddleware` (feature 002) resolves `ICurrentTenantService`/`ITenantDbContext` before any handler runs.

## `GET /api/staff`

Query params: `includeDeactivated` (bool, default `false`).

- `200` — `StaffResponse[]`. Default: only profiles with `deactivatedAt == null` (FR-010).

## `GET /api/staff/{id}`

- `200` — `StaffResponse`, including `eligibleLocationIds` and a freshly-signed `photoDownloadUrl` (`null` if no photo set).
- `404 errors.staff.not_found` — no staff profile with that id in the caller's own tenant schema.

## `POST /api/staff`

Request (`CreateStaffProfileRequest`):

```json
{
  "firstName": "...", "lastName": "...", "email": "...", "phone": "...",
  "qualificationLevel": "QualifiedCaregiver",
  "role": "Staff",
  "existingTenantUserId": null
}
```

- `201` — `StaffResponse`. When `existingTenantUserId` is omitted, a new `TenantUser` (Role = Staff) + `StaffProfile` + `StaffInvitation` are created and an invitation email is sent (FR-001, FR-005, FR-006, research.md R5).
- `201` (director opt-in path) — when `existingTenantUserId` references an existing `Director`-role account, only a `StaffProfile` is created and linked; no invitation is sent (research.md R6, FR-001).
- `422 errors.validation` with `fieldErrors` — missing/empty `firstName`/`lastName`/`email`/`phone`; invalid email format; `qualificationLevel` missing when `role == Staff` (FR-003).
- `409 errors.staff.email_already_exists` — an account with this email already exists within the organisation (FR-008).
- `404 errors.staff.tenant_user_not_found` — `existingTenantUserId` supplied but does not resolve to a `Director`-role account in this tenant.

## `PUT /api/staff/{id}`

Request (`UpdateStaffProfileRequest`):

```json
{ "firstName": "...", "lastName": "...", "phone": "...", "qualificationLevel": "Auxiliary" }
```

- `200` — updated `StaffResponse` (FR-009).
- `404 errors.staff.not_found`
- `422 errors.validation` — same field rules as create (qualification requirement still conditional on the linked account's role).

## `POST /api/staff/{id}/deactivate`

No request body.

- `200` — `StaffResponse` with `deactivatedAt` set (FR-010).
- `404 errors.staff.not_found`
- `409 errors.staff.has_active_dependents` — a registered `IStaffDeactivationGuard` (features 009/011, not yet registered by this feature) reports active dependents (FR-011). Currently unreachable — reserved for when 009/011 ship.
- Already-deactivated profile: idempotent `200`, unchanged.

## `POST /api/staff/{id}/reactivate`

No request body.

- `200` — `StaffResponse` with `deactivatedAt` cleared (FR-012).
- `404 errors.staff.not_found`
- Already-active profile: idempotent `200`, no change.

## `PUT /api/staff/{id}/locations/{locationId}`

No request body — assigns eligibility (FR-004).

- `200` — updated `StaffResponse` including the new location in `eligibleLocationIds`.
- `404 errors.staff.not_found` / `404 errors.location.not_found` (feature 004's existing key, reused — the location does not exist in this tenant).
- Already-assigned pair: idempotent `200`, no duplicate row (composite PK prevents it).

## `DELETE /api/staff/{id}/locations/{locationId}`

- `200` — updated `StaffResponse` with the location removed from `eligibleLocationIds`.
- `404 errors.staff.not_found`
- Not currently assigned: idempotent `200`, no change.

## `POST /api/staff/{id}/photo/upload-url`

No request body.

- `200` — `RequestPhotoUploadUrlResponse` (`uploadUrl`, `objectPath`) (research.md R3). The client `PUT`s the image directly to `uploadUrl`; the API is not involved in the byte transfer.
- `404 errors.staff.not_found`

Note: this feature does not add a separate "confirm upload complete" endpoint — `objectPath` is written to `StaffProfile.ProfilePhotoObjectPath` at the time the upload URL is issued (the object path is deterministic per staff profile, research.md R3), so the next `GET` simply reflects whatever object currently exists at that path once the client's direct `PUT` to GCS succeeds.

## `POST /api/staff/accept-invitation` *(unauthenticated, tenant-exempt)*

Request (`AcceptStaffInvitationRequest`):

```json
{ "organisationSlug": "...", "token": "...", "password": "..." }
```

Note: unlike every other route in this group, this one is unauthenticated, so `TenantMiddleware` has no JWT to resolve a tenant from — it must be `.RequireTenantExempt()` and resolve the schema itself via `organisationSlug` (`OrganisationSlugResolver` + `ITenantDbContextResolver.ForSchema`), exactly mirroring how `ResetPasswordCommandHandler`/`VerifyEmailCommandHandler` (feature 003) already handle this same structural problem. Found during implementation — the original contract draft omitted `organisationSlug`, which would have made this endpoint unusable (`/speckit-implement` fix).

- `200` — sets the linked `TenantUser.PasswordHash`; the invitee can now log in via the existing email/password flow (feature 003), `organisationSlug` + `email` + this new `password` (FR-006, FR-007).
- `404 errors.auth.organisation_not_found` — `organisationSlug` matches no tenant, or one whose provisioning isn't `Ready` (reuses feature 003's existing key, same collapsed-slug rationale).
- `400 errors.staff.invitation_invalid_or_expired` — token not found, expired, or already used — including a second accept attempt on an already-used token even before its `ExpiresAt` elapses (FR-006b) — mirrors feature 003's generic `errors.auth.token_invalid_or_expired` framing — same non-enumeration rationale.
- `422 errors.validation` — `password` missing or under 8 characters (mirrors feature 003's `errors.auth.password_too_short`).

## `POST /api/staff/{id}/resend-invitation`

No request body.

- `200` — `StaffResponse` (unchanged) — the prior invitation (if any still valid) is superseded and a new one is emailed to the profile's address (FR-006a).
- `404 errors.staff.not_found`
- `409 errors.staff.account_already_active` — the linked `TenantUser.PasswordHash` is already set — either the invitation was already accepted, or this profile was created via the director opt-in path (which never provisions an invitation, since the account already has working credentials). Resend only applies to an account still awaiting its first login.

Note: a failed email send (FR-006) never surfaces as an error response here or on `POST /api/staff` — it is logged server-side and the caller still receives `200`/`201`.
