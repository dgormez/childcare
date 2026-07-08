# Contract: Director & Organisation Name Exposure

## `AuthenticatedUser` — extended field (login, refresh, Google, Apple)

All four existing auth response paths (`POST /api/auth/login`, `POST /api/auth/refresh`,
`POST /api/auth/google`, `POST /api/auth/apple`) return `AuthSessionResponse`, which embeds
`AuthenticatedUser`. This feature adds one field:

```json
{
  "accessToken": "…",
  "refreshToken": "…",
  "user": {
    "id": "guid",
    "email": "string",
    "emailVerified": true,
    "role": "Director",
    "name": "string"
  }
}
```

**Backward compatibility**: purely additive — no existing field removed or retyped. Mobile and
any other existing consumer of `AuthSessionResponse` are unaffected (they already ignore unknown
fields via generated-type structural typing).

**Source**: `TenantUser.Name`, already populated at account creation (registration for
directors, invitation-acceptance for staff — features 001/005). No new input is collected; this
is exposure of existing data only.

## `GET /api/organisations/me`

**Authorization**: `DirectorOnly`, tenant-scoped.

**Query parameters**: none.

**Response** `200 OK`:

```json
{ "name": "string" }
```

**Source**: `Tenant.Name` (public schema), resolved via `ICurrentTenantService.TenantId` against
`PublicDbContext.Tenants`. Read-only — this endpoint has no corresponding write; organisation
name changes are out of scope for this feature (no settings screen exists yet).

**Errors**: Standard `401`/`403` from `TenantMiddleware`/`DirectorOnly`. No feature-specific
failure modes — the tenant is guaranteed to exist and be `Ready` by the time `TenantMiddleware`
allows the request through.
