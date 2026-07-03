# Data Model: Multi-Tenancy Scaffold

## Public schema (`PublicDbContext` — unchanged)

### Tenant (existing, feature 001 — no shape changes)

Already contains everything this feature needs to resolve/reject a request: `Id`, `Name`, `Slug`, `SchemaName`, `Plan`, `ProvisioningStatus`, `CreatedFromInvitationId`, `CreatedAt`. `ProvisioningStatus == Ready` is the fail-closed gate for FR-008; `Id` is the shape of the JWT `tenant_id` claim; `SchemaName` is what `TenantMiddleware`/`ITenantDbContextResolver` sets as the request's schema.

## Tenant schema (`TenantDbContext` — extended this feature)

### TenantUser (existing, feature 001 — extended)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `Email` | `string(254)` | unique per tenant schema (not globally — each organisation's schema has its own uniqueness scope) |
| `PasswordHash` | `string` | empty string for pure-OAuth accounts (matches legacy `User` behavior) |
| `Name` | `string(200)` | |
| `GoogleId` | `string?` | **new** — moved from legacy `User` |
| `AppleId` | `string?` | **new** — moved from legacy `User` |
| `EmailVerified` | `bool` | **new** — moved from legacy `User`, default `false` |
| `EmailVerificationToken` | `string?` | **new** |
| `EmailVerificationExpiry` | `DateTime?` | **new** |
| `PasswordResetToken` | `string?` | **new** |
| `PasswordResetExpiry` | `DateTime?` | **new** |
| `CreatedAt` | `DateTime` | existing |

Explicitly **not** carried over from the legacy `User`: `ExpoPushToken`, `StripeCustomerId`, `StripeSubscriptionId`, `SubscriptionStatus`, `SubscriptionCurrentPeriodEnd` — these belonged to the deleted `NotificationEndpoints`/`PaymentEndpoints` (research.md R4).

### TenantUserRefreshToken (new, moved from `ChildCare.Api.Models.UserRefreshToken`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `TenantUserId` | `Guid` | FK → `TenantUser.Id`, cascade delete |
| `Token` | `string(128)` | unique per tenant schema |
| `ExpiresAt` | `DateTime` | |

## Non-persisted: current-request organisation context

`ICurrentTenantService` (interface, read-only) / `CurrentTenantService` (concrete, settable) — not an entity, a per-request-scoped fact:

| Field | Type | Set by | Read by |
|---|---|---|---|
| `TenantId` | `Guid` | `TenantMiddleware`, from the JWT `tenant_id` claim | any handler/repository needing the current organisation |
| `SchemaName` | `string` | `TenantMiddleware`, from the resolved `Tenant.SchemaName` | `ITenantDbContextResolver`'s Scoped `TenantDbContext` registration |
| `TenantSlug` | `string` | `TenantMiddleware`, from the resolved `Tenant.Slug` | logging/diagnostics |

## Removed entirely (research.md R4)

- `ChildCare.Api.Data.AppDbContext` and its `DbSet`s
- `ChildCare.Api.Models.User`, `UserRefreshToken`, `Habit`, `HabitCompletion`
- Everything under `ChildCare.Api/Endpoints/HabitEndpoints.cs`, `PaymentEndpoints.cs`, `NotificationEndpoints.cs`
- `ChildCare.Api/Services/StripeService.cs` (only ever called from the now-deleted `PaymentEndpoints.cs`)

## State transitions

`TenantUser.EmailVerified`: `false` → `true` on successful `verify-email`, or immediately `true` for Google/Apple sign-ups (provider already verified the email) — unchanged behavior from the legacy `User`, just relocated.

No new state machine is introduced for `Tenant.ProvisioningStatus` by this feature — it already has `Provisioning` / `Ready` / `Failed` (feature 001); this feature only *reads* it (FR-008) and *writes* it during the migration rollout mechanism's own bookkeeping is not required (the rollout mechanism, R8, does not change `ProvisioningStatus` — a tenant that's `Ready` before a rollout stays `Ready` after; a tenant not yet `Ready` is simply skipped, since it has no committed baseline to roll a further migration onto yet).
