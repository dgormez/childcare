# API Contract: Platform-Admin Portal

All routes below (except registration) require `PlatformAdminOnly` — additive on top of
`DirectorOnly`, per `PlatformAdminVaccineTypeEndpoints.cs`'s existing precedent.

## Invitations

### `GET /api/platform-admin/invitations`

Returns `PlatformAdminInvitationResponse[]` (see data-model.md), newest first.

### `POST /api/platform-admin/invitations`

Request:

```json
{ "email": "jane@example.com", "organisationNameNote": "Zonnebloem KDV", "locale": "nl" }
```

- `email`: required, valid email format.
- `organisationNameNote`: optional, max 200 chars.
- `locale`: optional, one of `nl`/`fr`/`en`, defaults `nl`.

Behavior: if a Pending or Expired invitation already exists for this email, it is marked Revoked
(attributed to the acting platform-admin) before the new one is created — the existing
`CreateInvitationCommandHandler` supersede behavior, extended per research.md R3. The new
invitation's `CreatedByUserId`/`CreatedByEmail` are set from the acting platform-admin's claims
(research.md R12). Sends the invitation email in the requested locale (research.md R9). Returns
`201 Created` with `PlatformAdminInvitationResponse`.

### `POST /api/platform-admin/invitations/{id}/resend`

No request body. Creates a fresh invitation (new token, new expiry) for the same
email/note/locale, marks `{id}` Revoked (superseded), sends a new email. Returns `200 OK` with
the new `PlatformAdminInvitationResponse`.

Failure: `404` if `{id}` doesn't exist; `409` (`errorKey: "errors.invitations.already_accepted"`)
if `{id}`'s derived status is Accepted (FR-007).

### `POST /api/platform-admin/invitations/{id}/revoke`

No request body. Sets `RevokedByUserId`/`RevokedByEmail`/`RevokedAt` on `{id}` (acting user
resolved server-side from JWT claims, never the request body — mirrors
`PlatformAdminVaccineTypeEndpoints.ActingUserOf`). Returns `200 OK` with the updated
`PlatformAdminInvitationResponse`.

Failure: `404` if `{id}` doesn't exist; `409` if already Accepted (FR-007) or already Revoked
(idempotency: revoking an already-Revoked invitation is a no-op success, not an error — matches
this codebase's general tolerance for idempotent re-clicks on a disabled-looking action).

## Organisation directory

### `GET /api/platform-admin/organisations`

Returns `PlatformAdminOrganisationResponse[]` (see data-model.md), ordered by `createdAt`
descending. Read-only — no POST/PATCH/DELETE on this resource (FR-013).

## Registration (existing endpoint, unchanged)

### `POST /api/organisations/register`

Already exists (`OrganisationEndpoints.cs`, feature 001) — no contract change. The new
`web/app/register/page.tsx` is simply its first-ever consumer. Documented here only for
completeness/traceability:

Request: `{ invitationToken, organisationName, directorName, email, password }`.

Response: `201 Created` with `RegisterOrganisationResponse` on success; `404`
(`errors.invitation.not_found` — covers not-found/expired/revoked/already-used, deliberately
indistinguishable) or `422` (`errors.registration.email_mismatch`) on failure.

**New in this feature**: `.RequireRateLimiting("organisation-register")` is added to this route
(research.md R13) — this feature is what first makes it genuinely reachable by public traffic,
mirroring `PublicEnrollmentEndpoints.cs`'s existing policy on an equivalent newly-public write
path. This is the one actual contract change this feature makes to the endpoint; everything
else about it (request/response shape, status codes) is unchanged.
