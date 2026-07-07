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

## Staff Management (feature `005-staff`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.staff.not_found` | 404 | `GET/PUT /api/staff/{id}`, deactivate/reactivate/resend-invitation/photo-upload-url/location eligibility — no staff profile with that id in the caller's own tenant schema (FR-015) |
| `errors.staff.email_already_exists` | 409 | `POST /api/staff` — an account with this email already exists within the organisation (FR-008), including the concurrent-invite race (unique-constraint violation caught and mapped, not a 500) |
| `errors.staff.tenant_user_not_found` | 404 | `POST /api/staff` — `existingTenantUserId` supplied but does not resolve to a `Director`-role account in this tenant |
| `errors.staff.has_active_dependents` | 409 | `POST /api/staff/{id}/deactivate` — a registered `IStaffDeactivationGuard` (features 009/011, none registered by this feature) reports active dependents (FR-011). Currently unreachable — reserved for when 009/011 ship |
| `errors.staff.account_already_active` | 409 | `POST /api/staff/{id}/resend-invitation` — the linked account's password is already set (already accepted, or created via the director opt-in path, which never provisions an invitation) (FR-006a) |
| `errors.staff.invitation_invalid_or_expired` | 400 | `POST /api/staff/accept-invitation` — token not found, expired, or already used (single-use enforced even before `ExpiresAt` elapses, FR-006b) |
| `errors.staff.firstname_required` / `errors.staff.lastname_required` / `errors.staff.email_required` / `errors.staff.phone_required` | 422 | Required field missing/empty on create or update |
| `errors.staff.email_invalid` | 422 | `email` present but not a valid email address |
| `errors.staff.phone_invalid` | 422 | `phone` present but not a syntactically valid phone number (reuses feature 004's permissive international pattern) |
| `errors.staff.firstname_too_long` / `errors.staff.lastname_too_long` / `errors.staff.email_too_long` / `errors.staff.phone_too_long` | 422 | Field exceeds its `TenantDbContext` `HasMaxLength` (100/100/254/30 respectively, FR-001) |
| `errors.staff.qualification_required` | 422 | `qualificationLevel` missing when the target account's role is `Staff` (FR-003) — target role is either the new account's own role, or (opt-in path) the existing account's role |
| `errors.staff.organisation_slug_required` / `errors.staff.token_required` / `errors.staff.password_required` | 422 | Required field missing/empty on `POST /api/staff/accept-invitation` |
| `errors.staff.password_too_short` | 422 | `password` under 8 characters on `POST /api/staff/accept-invitation` (mirrors feature 003's `errors.auth.password_too_short`) |

This feature also reuses two pre-existing keys rather than inventing duplicates: `errors.location.not_found` (404, assigning/unassigning an eligible location that doesn't exist in this tenant) and `errors.auth.organisation_not_found` (404, `POST /api/staff/accept-invitation` with an unresolvable `organisationSlug` — this route is unauthenticated/tenant-exempt and resolves its own tenant the same way `POST /api/auth/login` does).

## Child File Management (feature `006-children`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.child.not_found` | 404 | `GET/PUT /api/children/{id}`, deactivate/reactivate/photo-upload-url, and every `/api/children/{childId}/...` sub-resource route — no child with that id in the caller's own tenant schema (FR-017) |
| `errors.child.has_active_dependents` | 409 | `POST /api/children/{id}/deactivate` — a registered `IChildDeactivationGuard` (feature 007, none registered by this feature) reports an active dependent (FR-013). Currently unreachable — reserved for when 007 ships |
| `errors.child.firstname_required` / `errors.child.lastname_required` | 422 | Required field missing/empty on create or update |
| `errors.child.date_of_birth_in_future` | 422 | `dateOfBirth` is a future date (FR-001, `/speckit-checklist` CHK001) |
| `errors.child.firstname_too_long` / `errors.child.lastname_too_long` / `errors.child.nationality_too_long` / `errors.child.gp_name_too_long` / `errors.child.gp_phone_too_long` / `errors.child.health_insurance_number_too_long` / `errors.child.kindcode_too_long` | 422 | Field exceeds its `TenantDbContext` `HasMaxLength` |
| `errors.child.allergies_description_too_long` / `errors.child.medical_conditions_too_long` / `errors.child.dietary_restrictions_too_long` | 422 | Free-text medical field exceeds 2000 characters (FR-003, `/speckit-checklist` CHK007) |
| `errors.contact.not_found` | 404 | `PUT /api/contacts/{id}`, and any `/api/children/{childId}/contacts/{contactId}` route — no contact with that id, or no link between this child and contact |
| `errors.contact.link_already_exists` | 409 | `POST /api/children/{childId}/contacts` — this `(childId, contactId)` pair is already linked (research.md R3) — use `PUT` to change the existing link's relationship instead |
| `errors.contact.firstname_required` / `errors.contact.lastname_required` / `errors.contact.phone_required` / `errors.contact.locale_required` | 422 | Required field missing/empty on create or update |
| `errors.contact.email_invalid` | 422 | `email` present but not a valid email address |
| `errors.contact.firstname_too_long` / `errors.contact.lastname_too_long` / `errors.contact.phone_too_long` / `errors.contact.email_too_long` / `errors.contact.locale_too_long` | 422 | Field exceeds its `TenantDbContext` `HasMaxLength` |
| `errors.contact.contact_id_required` | 422 | `POST /api/children/{childId}/contacts` — `contactId` missing |
| `errors.group.not_found` | 404 | Any `/api/children/{childId}/groups` route referencing a `groupId` that doesn't exist in this tenant |
| `errors.group.name_required` / `errors.group.name_too_long` | 422 | `POST /api/groups` — `name` missing or exceeds 100 characters |
| `errors.group.group_id_required` | 422 | `POST /api/children/{childId}/groups` — `groupId` missing |
| `errors.group.out_of_chronological_order` | 422 | `POST /api/children/{childId}/groups` — the child has a currently-open assignment whose `startDate` is on or after this request's `startDate` (FR-008a, `/speckit-checklist` CHK004) — assignments must be entered in chronological order |
| `errors.vaccination.vaccine_name_required` / `errors.vaccination.vaccine_name_too_long` | 422 | `POST /api/children/{childId}/vaccinations` — `vaccineName` missing or exceeds 200 characters |
| `errors.vaccination.date_administered_in_future` | 422 | `dateAdministered` is a future date (FR-010, `/speckit-checklist` CHK002) |

This feature also reuses `errors.location.not_found` (404) rather than inventing a duplicate — `POST /api/groups` with a `locationId` that either doesn't exist in this tenant, or exists but is deactivated (feature 004's existing key, extended in usage — a group cannot be newly created against an inactive location, `/speckit-checklist` CHK003).

## Enrolment Contracts (feature `007-contracts`)

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.contract.not_found` | 404 | `GET/PUT /api/contracts/{id}`, activate/amend/terminate/pdf, and `GET /api/children/{childId}/contracts` — no contract with that id in the caller's own tenant schema (FR-015) |
| `errors.contract.not_draft` | 409 | `PUT /api/contracts/{id}` or `POST /api/contracts/{id}/activate` on a contract whose status is not `draft` (FR-001a/FR-003) |
| `errors.contract.not_active` | 409 | `POST /api/contracts/{id}/amend` or `/terminate` on a contract whose status is not `active` (FR-007/FR-009a) |
| `errors.contract.already_active_at_location` | 409 | `POST /api/contracts/{id}/activate` or `/amend` — the child already has another `active` contract at this location (FR-004) |
| `errors.contract.day_overlap` | 409 | `POST /api/contracts/{id}/activate` or `/amend` — a contracted weekday overlaps another currently `active` contract for the same child at a different location (FR-005/FR-006, constitution Principle II) |
| `errors.contract.amendment_start_date_invalid` | 422 | `POST /api/contracts/{id}/amend` — `effectiveStartDate` is on or before the current contract's own `startDate` |
| `errors.contract.termination_date_invalid` | 422 | `POST /api/contracts/{id}/terminate` — `endDate` is before the contract's own `startDate` |
| `errors.contract.weekday_required` | 422 | `contractedDays` is empty on create/update/amend (FR-001) |
| `errors.contract.weekday_invalid` | 422 | A weekday outside Monday–Friday, or the same weekday listed twice in one contract (FR-001, `/speckit-checklist` CHK001) |
| `errors.contract.time_range_invalid` | 422 | A contracted day's `startTime` is not before its `endTime` |
| `errors.contract.daily_rate_invalid` | 422 | `dailyRateCents` is zero or negative (FR-001, `/speckit-checklist` CHK002) |
| `errors.contract.start_date_required` | 422 | `startDate` missing on create/update |
| `errors.contract.end_date_before_start_date` | 422 | `endDate` is present and earlier than `startDate` |

This feature also reuses `errors.child.not_found` (404, `POST /api/children/{childId}/contracts` and `GET /api/children/{childId}/contracts` with an unresolvable `childId`) and `errors.location.not_found` (404, a `locationId` that doesn't exist in this tenant **or** exists but is deactivated — FR-004a, matching feature 006's CHK003 precedent) rather than inventing duplicates.

## Shared / cross-cutting

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.unexpected` | 500 | Any unhandled exception anywhere in the API (global exception handler in `Program.cs`) — the full error is logged server-side regardless of environment (constitution Principle VI), but never exposed to the client |
| `errors.validation` | 422 | The top-level envelope key for any field-level validation failure — always paired with a `fieldErrors` object (e.g., `{ "field-name": "errors.registration.field_specific_key" }`). Returned both for FluentValidation failures (`RegisterOrganisationCommandValidator`/`CreateInvitationCommandValidator`) and for the `errors.registration.email_mismatch` case — the individual field keys above are the values nested inside `fieldErrors`, not top-level `errorKey` values themselves |
