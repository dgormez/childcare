# Data Model: Platform-Admin Portal — Invitations, Registration & Organisation Directory

## Invitation (extended)

`backend/ChildCare.Domain/Entities/Invitation.cs` — Public schema, `PublicDbContext.Invitations`.
Existing fields (`Id`, `Email`, `TokenHash`, `ExpiresAt`, `CreatedAt`) are unchanged. New fields:

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `OrganisationNameNote` | `string` | Yes | Informational only (spec.md FR-001) — never validated against or used to pre-fill the registration form's own `OrganisationName` input. Max length 200 (matches `Tenant.Name`'s own `HasMaxLength(200)`, since it's describing the same kind of value). |
| `Locale` | `string` | No, defaults `"nl"` | One of `"nl"`, `"fr"`, `"en"` (FluentValidation-enforced, mirrors this codebase's existing locale-string validation pattern e.g. `SendBulkEmailAsync`'s `locale` param). Drives which language the invitation email (R9) and, if re-sent, the resend email use. |
| `RevokedByUserId` | `Guid?` | Yes | No DB-level FK (research.md R4) — the acting `TenantUser` lives in an arbitrary tenant schema, a cross-schema-boundary reference Postgres cannot FK-enforce, identical precedent to `VaccineType.DeactivatedByUserId`. |
| `RevokedByEmail` | `string?` | Yes | Denormalized at the moment of the action (create/resend supersede, or explicit revoke) so the audit trail stays human-readable if the acting account later changes. |
| `RevokedAt` | `DateTime?` | Yes | Set by both an explicit "Revoke" action and an automatic supersede (research.md R3) — the data model makes no distinction between the two triggers, per spec.md's Clarifications. |

**Invariant**: `RevokedByUserId`/`RevokedByEmail`/`RevokedAt` are either all null or all
populated together — never a partial state (same invariant shape as `VaccineType`'s
deactivation fields).

**Status derivation** (never a stored column — research.md R2):

```text
Accepted  if a Tenant row exists with Tenant.CreatedFromInvitationId == this.Id
Revoked   else if RevokedAt is not null
Expired   else if ExpiresAt <= UtcNow
Pending   otherwise
```

**Migration**: one new Public-schema EF Core migration under
`backend/ChildCare.Infrastructure/Persistence/Migrations/Public/`, adding the five columns above.
No tenant-schema migration, no `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests`
revert-helper update needed — that recurring pattern (012a onward) only applies to
*tenant*-schema migrations; this one is Public-schema, applied once, not per-tenant.

## Tenant (read-only — no schema change)

`backend/ChildCare.Domain/Entities/Tenant.cs` — already has every field the directory needs:
`Name`, `Slug`, `Plan`, `ProvisioningStatus`, `KboNumber`, `CreatedAt`, `CreatedFromInvitationId`.
This feature adds no new field here and performs no write against `Tenant` at all — strictly a
read, joined to `Invitation` for the registering email (research.md R5).

## Query/response shapes (not persisted entities)

### `PlatformAdminInvitationResponse` (list/create/resend/revoke result)

| Field | Type | Source |
|---|---|---|
| `id` | `Guid` | `Invitation.Id` |
| `email` | `string` | `Invitation.Email` |
| `organisationNameNote` | `string?` | `Invitation.OrganisationNameNote` |
| `locale` | `string` | `Invitation.Locale` |
| `status` | `string` | Derived (`pending`/`accepted`/`expired`/`revoked`) |
| `expiresAt` | `DateTime` | `Invitation.ExpiresAt` |
| `createdAt` | `DateTime` | `Invitation.CreatedAt` |
| `revokedByEmail` | `string?` | `Invitation.RevokedByEmail` (only when status is `revoked`) |
| `revokedAt` | `DateTime?` | `Invitation.RevokedAt` |

`token`/`tokenHash` are never included in this response — the platform-admin never needs to see
or copy the raw token; only the emailed link carries it (matches
`CreateInvitationResponse`'s existing precedent of returning the token only to the original
ops-key-gated caller, not to a general list view).

### `PlatformAdminOrganisationResponse` (directory list result)

| Field | Type | Source |
|---|---|---|
| `id` | `Guid` | `Tenant.Id` |
| `name` | `string` | `Tenant.Name` |
| `plan` | `string` | `Tenant.Plan` |
| `provisioningStatus` | `string` | `Tenant.ProvisioningStatus` — surfaced as-is (research.md R6), not reinterpreted as active/inactive |
| `kboNumber` | `string?` | `Tenant.KboNumber` |
| `createdAt` | `DateTime` | `Tenant.CreatedAt` |
| `registeredByEmail` | `string?` | `Invitation.Email` via `Tenant.CreatedFromInvitationId` join — named deliberately to signal "who registered this org," not "current director" (research.md R5) |

No `id`-keyed mutation endpoints exist for this response — FR-013 (read-only) means no
corresponding request/update contract.
