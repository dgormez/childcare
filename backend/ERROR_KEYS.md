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

## Shared / cross-cutting

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.unexpected` | 500 | Any unhandled exception anywhere in the API (global exception handler in `Program.cs`) — the full error is logged server-side regardless of environment (constitution Principle VI), but never exposed to the client |
| `errors.validation` | 422 | The top-level envelope key for any field-level validation failure — always paired with a `fieldErrors` object (e.g., `{ "field-name": "errors.registration.field_specific_key" }`). Returned both for FluentValidation failures (`RegisterOrganisationCommandValidator`/`CreateInvitationCommandValidator`) and for the `errors.registration.email_mismatch` case — the individual field keys above are the values nested inside `fieldErrors`, not top-level `errorKey` values themselves |
