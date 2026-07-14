# Research: Platform-Admin Vaccine Catalog Management

## R1: Where does `IsPlatformAdmin` live, and how is it authorized?

**Decision**: `IsPlatformAdmin` is a new `bool` column on `TenantUser` (default `false`), included
in the JWT as a new claim (`"is_platform_admin"`, present as `"true"` only when set — omitted
otherwise, matching how `tenant_id`/role are always-present but keeping the token minimal for the
common case). A new `PlatformAdminOnly` authorization policy uses `RequireAssertion` to check
`RequireRole("director")` AND the new claim — no new authentication scheme.

**Rationale**: `TenantUser` (`backend/ChildCare.Domain/Entities/TenantUser.cs`) lives in
`TenantDbContext`, i.e. per-tenant schema — there is no tenant-independent account table anywhere
in this codebase (`PublicDbContext` only holds `Tenants`, `Invitations`, `VaccineTypes`). A
platform-admin is therefore necessarily an existing director account in one specific tenant, with
an added flag — not a new kind of account. Their request already carries a valid `tenant_id`
claim and passes through `TenantMiddleware` exactly like any other director request (deny-by-
default, per `backend/ChildCare.Api/Middleware/TenantMiddleware.cs`). This matches the existing
precedent set by 013g's own `GET /api/vaccine-types` (`VaccineTypeEndpoints.cs`), which already
authorizes as an ordinary `DirectorOnly`-scoped request against non-tenant-scoped
(`PublicDbContext`) data — its own code comment says exactly this: "vaccine_types is shared,
platform-wide reference data, not tenant-scoped, so this is not a TenantMiddleware bypass." This
feature's new write endpoints follow the identical shape, just gated by the additional claim.

The policy shape mirrors the existing `DeviceOrStaffOrDirector` policy
(`Program.cs:390-395`), which is the established precedent in this codebase for an *additive*
claim check on top of role-based auth via `RequireAssertion`, rather than registering a new
authentication scheme (that pattern — `AddAuthenticationSchemes` — is reserved for genuinely
different auth mechanisms, e.g. `DeviceOrDirector`'s device-token-or-JWT OR, which does not apply
here since a platform-admin only ever authenticates as a director).

**Alternatives considered**:
- A new tenant-independent `PlatformAdmin` table/scheme — rejected per the resolved clarification
  (reuse director login) and because it would be the first tenant-independent account in the
  system for a capability narrow enough not to need one.
- `RequireTenantExempt()` on the new endpoints (bypassing `TenantMiddleware` like `AuthEndpoints`/
  `AdminEndpoints` do) — rejected: unlike those pre-tenant/cross-cutting endpoints, a
  platform-admin's request already has a valid, resolvable tenant context (they logged in as a
  director of one tenant); exempting them would be a change with no corresponding need, and would
  make this feature's endpoints behave inconsistently with 013g's already-shipped read endpoint on
  the same data.

## R2: Audit trail for deactivation

**Decision**: Add `DeactivatedByUserId (Guid?)`, `DeactivatedByEmail (string?)`, `DeactivatedAt
(DateTime?)` to `VaccineType`. `DeactivatedByUserId` carries no DB-level FK (same reasoning as
013g's `VaccineRecord.VaccineTypeId` — the referenced `TenantUser` row lives in an arbitrary
tenant's schema, a genuine cross-schema-boundary reference that cannot be FK-enforced in
PostgreSQL). `DeactivatedByEmail` is denormalized at the moment of deactivation so the audit
record stays human-readable even if the admin account is later renamed — mirrors this codebase's
existing denormalization precedent (`child_events.recorded_by`, `VaccineRecord`'s
originally-saved name text per 013g FR-010). Reactivating clears all three fields; a later
re-deactivation repopulates them fresh (spec.md FR-008) — no history table, since the spec's own
accountability concern is "who deactivated it, when" for the *current* state, not a full change
log.

**Alternatives considered**: A separate `VaccineTypeAuditLog` table — rejected as over-scoped;
the spec explicitly limits accountability to deactivation, and three nullable columns fully cover
that without introducing a new entity/table for a single field's history.

## R3: Granting the flag to a specific account (including the operator's own, per this feature's Assumptions)

**Decision**: New CLI command `grant-platform-admin`, following the exact pattern established by
`MigrateTenantsCommand`/`BackfillGrowthCheckCommand`
(`backend/ChildCare.Api/Cli/MigrateTenantsCommand.cs`,
`backend/ChildCare.Api/Cli/BackfillGrowthCheckCommand.cs`, dispatched in `Program.cs`'s `args[0]`
switch before the web host builds): loop `PublicDbContext.Tenants.Where(t =>
t.ProvisioningStatus == ProvisioningStatus.Ready)`, and for each tenant schema run a
parameterized `UPDATE "{schema}"."Users" SET "IsPlatformAdmin" = true WHERE "Email" =
{emailParam}` (schema name interpolated as trusted input from the `Tenants` table, exactly like
`BackfillGrowthCheckCommand`; the email itself passed as a SQL parameter, not interpolated, since
it is CLI-supplied). Reports per-tenant hit/miss and a summary line, same shape as the two
existing commands. Invoked as `dotnet run -- grant-platform-admin <email>`.

**Rationale**: This is a repeatable, auditable (via command output / shell history) way to
perform the grant, per spec.md's Assumptions — not a one-off manual SQL statement with no record
of having run it. It reuses an already-proven pattern rather than inventing a new one.
Immediately after this feature ships, this command will be run once against the operator's own
account (dgormez@gmail.com) so the feature is usable without a second manual step.

**Alternatives considered**: A one-off manual `UPDATE` statement run by hand — rejected; the
spec's own Assumptions section explicitly asks for a repeatable command, and every prior
single-purpose data-touching operation in this codebase (002, 009a) already established the CLI
pattern instead of ad hoc SQL.

## R4: Reorder UI

**Decision**: Up/down arrow buttons per row (lucide `ArrowUp`/`ArrowDown`), calling an
`onReorder(entry, direction)` callback — identical shape to `WaitingListTable.tsx`'s existing
priority-reorder UI (feature 012a), the only reorder pattern that exists anywhere in `web/` today.

**Rationale**: No drag-and-drop library (`@dnd-kit`, `react-beautiful-dnd`, etc.) is installed
anywhere in `web/` — confirmed by dependency scan. `WaitingListTable`'s button-based approach is
fully keyboard-accessible for free (standard `<button>` elements, no custom keyboard-reorder
logic needed) and needs no new dependency, matching design-system.md's "shared components reused
rather than reimplemented" and Constitution Principle VII (no new dependency without a proven
need). A dedicated drag library for one low-frequency admin screen would be exactly the kind of
premature complexity this codebase's conventions argue against.

**Alternatives considered**: Introducing `@dnd-kit` for a pointer-drag experience — rejected;
this is a rarely-used admin screen (single platform operator, occasional catalog edits), not a
high-frequency interaction that justifies a new UI dependency.

## R5: Migration placement

**Decision**: One `Public` migration (`Migrations/Public/...AddVaccineTypeDeactivationAudit.cs`)
adding the three audit columns to `VaccineType`; one `Tenant` migration
(`Migrations/Tenant/...AddIsPlatformAdminToUsers.cs`) adding the boolean column to `TenantUser`'s
table (`Users`). Both follow the existing `Migrations/Public/` vs `Migrations/Tenant/` split
established by every prior feature (013g's own `AddVaccineTypeCatalog` / `AddVaccineCatalogAndAttachments` pair is the most recent example). Per constitution Principle VI, neither
auto-applies in production — SQL scripts are generated and run manually.

**Rationale**: Matches the existing dual-`DbContext` migration convention exactly; no new
pattern introduced.
