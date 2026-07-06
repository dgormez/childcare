# Backend Error Keys

Every error response returned by the API uses a locale-aware `errorKey` (constitution Principle IV,
NON-NEGOTIABLE) — never raw English text. This file is a backend-owned reference listing every key
in use, for whichever frontend project eventually renders them in NL/FR/EN (no such project exists
yet in this repo — see PROJECT-BRIEF.md's web admin/mobile apps). This is a documentation task, not
a translation task: it lists the keys and their trigger conditions, not the NL/FR/EN copy itself.

## Organisation Onboarding (feature `001-organisation-onboarding`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.unauthorized` | 401 | `POST /api/admin/invitations` called with a missing or incorrect `X-Superadmin-Key` header |
| `errors.invitation.email_required` | 422 | `POST /api/admin/invitations` — `email` field missing/empty |
| `errors.invitation.email_invalid` | 422 | `POST /api/admin/invitations` — `email` field not a valid email address |
| `errors.invitation.not_found` | 404 | `POST /api/organisations/register` — the invitation token doesn't exist, has expired, or was already used to complete a registration (deliberately indistinguishable — research.md R5) |
| `errors.registration.invitation_token_required` | 422 | `POST /api/organisations/register` — `invitationToken` field missing/empty |
| `errors.registration.organisation_name_required` | 422 | `POST /api/organisations/register` — `organisationName` field missing/empty |
| `errors.registration.director_name_required` | 422 | `POST /api/organisations/register` — `directorName` field missing/empty |
| `errors.registration.email_required` | 422 | `POST /api/organisations/register` — `email` field missing/empty |
| `errors.registration.email_invalid` | 422 | `POST /api/organisations/register` — `email` field not a valid email address |
| `errors.registration.password_required` | 422 | `POST /api/organisations/register` — `password` field missing/empty |
| `errors.registration.password_too_short` | 422 | `POST /api/organisations/register` — `password` under 8 characters |
| `errors.registration.email_mismatch` | 422 | `POST /api/organisations/register` — the submitted `email` doesn't match the invitation's target email (FR-018); distinct from `errors.invitation.not_found` because the caller holds a real, valid invitation in this case |

## Multi-Tenancy Scaffold (feature `002-multi-tenancy-scaffold`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.tenant.missing` | 401 | A non-exempt request's JWT has no `tenant_id` claim (FR-006) |
| `errors.tenant.not_found` | 403 | A non-exempt request's `tenant_id` claim matches no known tenant, or is present but malformed/not a valid GUID (FR-007, spec.md Edge Cases — deliberately treated the same as "unknown"), or the tenant lookup itself threw (FR-008a — deliberately indistinguishable from "unknown tenant"; the real exception is logged server-side only) |
| `errors.tenant.not_ready` | 403 | A non-exempt request's `tenant_id` resolves to a tenant whose `ProvisioningStatus != Ready` (FR-008) |

## Authentication & Role-Based Authorization (feature `003-auth`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.auth.organisation_not_found` | 404 | `organisationSlug` on login/refresh/Google/Apple/forgot-password matches no tenant, or matches one whose `ProvisioningStatus != Ready` (FR-015) — deliberately collapsed, since slugs are not secret (research.md R1, R9) |
| `errors.auth.invalid_credentials` | 401 | Email/password mismatch, unknown email within a correctly-resolved organisation, or a Google/Apple token that is valid but matches no existing account (FR-009) — all collapsed into one generic key so no response reveals whether an email is registered (SC-005) |
| `errors.auth.method_not_allowed_for_role` | 403 | A sign-in attempt via a method not permitted for the resolved account's role (FR-017) — e.g. Google sign-in against a Staff-role account, or Apple sign-in against a Director-role account |
| `errors.auth.token_invalid_or_expired` | 400 | A password-reset or email-verification token is invalid, expired, or already used — replaces the pre-feature-003 hardcoded English strings (constitution Principle IV) |
| `errors.auth.organisation_slug_required` | 422 | `organisationSlug` field missing/empty on any auth request that requires one |
| `errors.auth.email_required` | 422 | `email` field missing/empty |
| `errors.auth.email_invalid` | 422 | `email` field not a valid email address |
| `errors.auth.password_required` | 422 | `password`/`newPassword` field missing/empty |
| `errors.auth.password_too_short` | 422 | `newPassword` (reset-password) under 8 characters |
| `errors.auth.refresh_token_required` | 422 | `refreshToken` field missing/empty |
| `errors.auth.id_token_required` | 422 | `idToken` (Google) field missing/empty |
| `errors.auth.identity_token_required` | 422 | `identityToken` (Apple) field missing/empty |
| `errors.auth.token_required` | 422 | `token` (verify-email/reset-password) field missing/empty |
| `errors.auth.apple_email_required_first_signin` | 400 | Apple sign-in's first link attempt for a given account has no resolvable email (Apple only sends it once) |

## Location Management (feature `004-locations`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.location.not_found` | 404 | `GET/PUT /api/locations/{id}`, deactivate/reactivate/duplicate — no location with that id exists in the caller's own tenant schema (a different organisation's location id can never match, FR-007) |
| `errors.location.has_active_dependents` | 409 | `POST /api/locations/{id}/deactivate` — a registered `ILocationDeactivationGuard` (features 005/007, none registered by this feature) reports active dependents (FR-012). Currently unreachable — reserved for when 005/007 ship |
| `errors.location.name_required` | 422 | `name` field missing/empty on create or update |
| `errors.location.address_required` | 422 | `address` field missing/empty on create or update |
| `errors.location.phone_required` | 422 | `phone` field missing/empty on create or update |
| `errors.location.email_required` | 422 | `email` field missing/empty on create or update |
| `errors.location.email_invalid` | 422 | `email` field present but not a valid email address |
| `errors.location.max_capacity_invalid` | 422 | `maxCapacity` missing, zero, or negative (FR-001, FR-010) |
| `errors.location.phone_invalid` | 422 | `phone` field present but not a syntactically valid phone number (permissive international format — spec.md Assumptions) (`/speckit-converge` finding F1) |
| `errors.location.name_too_long` | 422 | `name` exceeds 200 characters (`TenantDbContext` `HasMaxLength`, `/speckit-converge` finding F2) |
| `errors.location.address_too_long` | 422 | `address` exceeds 500 characters |
| `errors.location.phone_too_long` | 422 | `phone` exceeds 30 characters |
| `errors.location.email_too_long` | 422 | `email` exceeds 254 characters |
| `errors.location.naam_locatie_too_long` | 422 | `naamLocatie` exceeds 200 characters |
| `errors.location.dossiernummer_too_long` | 422 | `dossiernummer` exceeds 50 characters |
| `errors.location.verantwoordelijke_too_long` | 422 | `verantwoordelijke` exceeds 200 characters |

## Shared / cross-cutting

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.unexpected` | 500 | Any unhandled exception anywhere in the API (global exception handler in `Program.cs`) — the full error is logged server-side regardless of environment (constitution Principle VI), but never exposed to the client |
| `errors.validation` | 422 | The top-level envelope key for any field-level validation failure — always paired with a `fieldErrors` object (e.g., `{ "field-name": "errors.registration.field_specific_key" }`). Returned both for FluentValidation failures (`RegisterOrganisationCommandValidator`/`CreateInvitationCommandValidator`) and for the `errors.registration.email_mismatch` case — the individual field keys above are the values nested inside `fieldErrors`, not top-level `errorKey` values themselves |
