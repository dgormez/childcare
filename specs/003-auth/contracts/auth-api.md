# Contract: Auth API (`/api/auth/*`)

All requests/responses are JSON. All error bodies are `{ "errorKey": "..." }` (constitution Principle IV) — see `backend/ERROR_KEYS.md` for the full list, extended by research.md R9. Rate-limit policies are unchanged from feature 002 (`auth-strict`, `auth-oauth`, `auth-refresh`).

## Removed

**`POST /api/auth/register`** — deleted (research.md R10). Returns 404 (route no longer exists).

## `POST /api/auth/login`

**Exempt from `TenantMiddleware`.**

Request:

```json
{ "organisationSlug": "acme-kdv", "email": "director@acme.test", "password": "..." }
```

Responses:

- `200` — `AuthResponse` (`accessToken`, `refreshToken`, `user: { id, email, emailVerified }`). `accessToken` now carries a `role` claim (data-model.md).
- `404 errors.auth.organisation_not_found` — unknown slug, or tenant not `Ready`.
- `401 errors.auth.invalid_credentials` — organisation resolved, but email/password did not match any account in it.
- `403 errors.auth.method_not_allowed_for_role` — never applies to password login itself (password is allowed for every role); listed here only for completeness with Google/Apple below.

## `POST /api/auth/google` / `POST /api/auth/apple`

**Exempt from `TenantMiddleware`.**

Request (`google`):

```json
{ "organisationSlug": "acme-kdv", "idToken": "..." }
```

Request (`apple`):

```json
{ "organisationSlug": "acme-kdv", "identityToken": "...", "email": "optional, first sign-in only" }
```

Behavior change from the current implementation (research.md R7, spec.md FR-009): **link-only, never auto-create.**

Responses:

- `200` — `AuthResponse`, same shape as login.
- `404 errors.auth.organisation_not_found`
- `401 errors.auth.invalid_credentials` — provider token is valid, but no existing `TenantUser` in that organisation matches it by provider-id or email (this is the new "no auto-create" rejection).
- `403 errors.auth.method_not_allowed_for_role` — the matched account's role does not permit this sign-in method (FR-017): e.g. a `Staff`-role account attempting Google sign-in (caregiver app is password-only).
- `400` (unchanged) — Apple sign-in with no resolvable email on a first-time link attempt.
- Provider-side token validation failure (bad signature/issuer/audience/expiry) → `401 errors.auth.invalid_credentials` (unchanged behavior, now via `IGoogleTokenValidator`/`IAppleTokenValidator`, research.md R7).

## `POST /api/auth/refresh`

**Exempt from `TenantMiddleware`.**

Request:

```json
{ "organisationSlug": "acme-kdv", "refreshToken": "..." }
```

Responses:

- `200` — `AuthResponse` with a newly rotated refresh token (old one invalidated, unchanged behavior).
- `404 errors.auth.organisation_not_found`
- `401 errors.auth.invalid_credentials` — token not found, expired, or already rotated (unchanged semantics, resolved against the specified organisation's schema instead of the default-tenant shim).

## `POST /api/auth/forgot-password`

**Exempt from `TenantMiddleware`.**

Request:

```json
{ "organisationSlug": "acme-kdv", "email": "..." }
```

Response: always `200` (unchanged — does not reveal whether the email exists, SC-005), `404 errors.auth.organisation_not_found` only if the slug itself is unresolvable.

The emailed reset link now includes the org slug: `{resetBase}?token={token}&org={slug}` (research.md R2).

## `POST /api/auth/reset-password`

**Exempt from `TenantMiddleware`.**

Request:

```json
{ "organisationSlug": "acme-kdv", "token": "...", "newPassword": "..." }
```

Responses: `200` (unchanged — also invalidates all refresh tokens for the account, FR-006), `404 errors.auth.organisation_not_found`, `400 errors.auth.token_invalid_or_expired` (replaces today's hardcoded English message — Constitution Principle IV fix).

## `POST /api/auth/verify-email`

**Exempt from `TenantMiddleware`.**

Request:

```json
{ "organisationSlug": "acme-kdv", "token": "..." }
```

Responses: `200` (unchanged), `404 errors.auth.organisation_not_found`, `400 errors.auth.token_invalid_or_expired`.

The emailed verification link now includes the org slug: `{verifyBase}?token={token}&org={slug}` (research.md R2).

## `POST /api/auth/logout`, `DELETE /api/auth/account`, `POST /api/auth/resend-verification`

**Not exempt** — unchanged from feature 002: authenticated, resolved through the ordinary `TenantMiddleware`/`ICurrentTenantService` path via the JWT's `tenant_id` claim. No `OrganisationSlug` field needed (the caller is already authenticated within a known tenant). No behavioral change in this feature beyond moving the underlying logic into MediatR commands (research.md R8).

## New: role-gated endpoints (for downstream features, established here)

No new business endpoints ship in this feature. This feature establishes the three named policies any future endpoint can declare:

```csharp
app.MapGet("/api/example", ...).RequireAuthorization("DirectorOnly");
app.MapGet("/api/example", ...).RequireAuthorization("StaffOrDirector");
app.MapGet("/api/example", ...).RequireAuthorization("ParentOnly");
```

A minimal test-only endpoint per policy is added under the test project (or a `[ApiExplorerSettings(IgnoreApi = true)]`-marked internal route) solely to exercise US3's acceptance scenarios — see quickstart.md Scenario 3.
