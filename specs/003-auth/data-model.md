# Data Model: Authentication & Role-Based Authorization

## Public schema (`PublicDbContext` — no shape changes)

### Tenant (existing, feature 001 — read-only for this feature)

`Slug` (already unique-indexed) is what this feature resolves pre-auth requests against (research.md R1); `ProvisioningStatus == Ready` remains the fail-closed gate, now checked at login time in addition to `TenantMiddleware`'s existing post-login check (feature 002).

## Tenant schema (`TenantDbContext` — extended this feature)

### TenantUser (existing, feature 001/002 — extended)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, unchanged |
| `Email` | `string(254)` | unchanged — unique per tenant schema |
| `PasswordHash` | `string` | unchanged — empty for pure-OAuth accounts |
| `Name` | `string(200)` | unchanged |
| `GoogleId` | `string?` | unchanged |
| `AppleId` | `string?` | unchanged |
| `EmailVerified` | `bool` | unchanged |
| `EmailVerificationToken` / `Expiry` | `string?` / `DateTime?` | unchanged |
| `PasswordResetToken` / `Expiry` | `string?` / `DateTime?` | unchanged |
| `Role` | `UserRole` | **new** — `Director` \| `Staff` \| `Parent`; stored as lowercase string + `CHECK` constraint (research.md R3); `NOT NULL`, migration backfills existing rows as `'director'` |
| `CreatedAt` | `DateTime` | unchanged |

### TenantUserRefreshToken (existing, feature 002 — no shape change)

Unchanged. Resolution of *which* tenant schema to query it against now comes from the client-supplied `OrganisationSlug` on the refresh request (research.md R1) instead of the feature 002 default-tenant shim.

## New enum

### UserRole (`ChildCare.Domain.Enums`)

```csharp
public enum UserRole { Director, Staff, Parent }
```

No state machine — a `TenantUser`'s role is fixed at account-creation time by whichever provisioning flow created it (organisation onboarding → `Director`; feature 005 staff provisioning → `Staff`; feature 006/012 parent provisioning → `Parent`). This feature introduces no role-change operation; if one is needed later, it belongs to whichever feature owns staff/director management.

## Non-persisted: JWT claims (extended)

| Claim | Type | Set by | Read by |
|---|---|---|---|
| `NameIdentifier` | `Guid` | unchanged | unchanged |
| `Email` | `string` | unchanged | unchanged |
| `tenant_id` | `Guid` | unchanged | `TenantMiddleware` (feature 002) |
| `ClaimTypes.Role` | `string` (`"director"`\|`"staff"`\|`"parent"`) | **new** — `IAccessTokenIssuer.IssueAccessToken` (research.md R4) | ASP.NET Core's `RequireRole`/`IsInRole`, via the new `DirectorOnly`/`StaffOrDirector`/`ParentOnly` policies (research.md R5) |

## Non-persisted: request DTOs gaining `OrganisationSlug`

| Request | Field added | Source of the value (client-side, out of scope for this backend feature) |
|---|---|---|
| `LoginRequest` | `OrganisationSlug` (required) | App-known context (subdomain, config, or an org picker) |
| `GoogleAuthRequest` | `OrganisationSlug` (required) | Same as above |
| `AppleAuthRequest` | `OrganisationSlug` (required) | Same as above |
| `RefreshRequest` | `OrganisationSlug` (required) | Cached by the client from the login/OAuth call that issued the token being refreshed |
| `ForgotPasswordRequest` | `OrganisationSlug` (required) | Same app-known context as login |
| `ResetPasswordRequest` | `OrganisationSlug` (required) | Read back from the `&org=` query parameter on the emailed reset link (research.md R2) |
| `VerifyEmailRequest` | `OrganisationSlug` (required) | Read back from the `&org=` query parameter on the emailed verification link (research.md R2) |

`RegisterRequest` is deleted entirely (research.md R10) — no successor DTO.

## Removed entirely (research.md R8, R10)

- `ChildCare.Api.Services.AuthService` — logic redistributed into `ChildCare.Application/Auth/*` command handlers plus two new `ChildCare.Infrastructure/Auth/*` validators (research.md R7)
- `POST /api/auth/register` endpoint and its `RegisterRequest` DTO
- The feature-002 "default tenant" shim (`ResolveDefaultTenantAsync`) — superseded by real slug-based resolution (research.md R1)

## State transitions

Unchanged from feature 002: `TenantUser.EmailVerified` `false` → `true` on successful `verify-email`, or immediately `true` for a first Google/Apple *link* (provider already verified the email) — the only behavioral change is that Google/Apple sign-in can now only ever *link* an existing account (research.md R7), never create one, so this transition only ever fires against a `TenantUser` row that already existed before the OAuth sign-in attempt.
