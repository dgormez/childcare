# Phase 0 Research: Location Management

Each decision below resolves a technical unknown from the Technical Context. Format: Decision / Rationale / Alternatives considered.

## R1. `Location` carries no `OrganisationId`/tenant column — schema is the isolation boundary

**Decision**: `Location` (like `TenantUser`) has no foreign key or column referencing the owning organisation/tenant. It lives in `ITenantDbContext`/`TenantDbContext` alongside `Users`/`RefreshTokens`, scoped entirely by `search_path` (constitution Principle I). Every query (`ListLocationsQuery`, `GetLocationByIdQuery`) and write reaches `Location` only through the current request's `ITenantDbContext` (resolved by `TenantMiddleware` from the JWT's `tenant_id` claim, feature 002) — there is no `WHERE OrganisationId = @current` clause anywhere, because there is no such column to filter on.

**Rationale**: This is exactly how `TenantUser` is already modeled in this codebase — no `TenantId` column exists there either, because the entire point of schema-per-tenant (constitution Principle I) is that PostgreSQL's `search_path` makes cross-tenant rows structurally invisible, not merely filtered out by an `WHERE` clause a future handler could forget to add. Following the established pattern satisfies spec.md FR-007 ("structurally impossible," not just prevented by convention) more strongly than an app-level filter would, and keeps `Location` consistent with the one other tenant-domain entity that exists today.

**Alternatives considered**: Adding an `OrganisationId` column defensively, "just in case." Rejected — it would be redundant data (the schema already encodes this), require every query to remember to filter by it (reintroducing exactly the convention-based risk Principle I explicitly rejects), and contradicts the existing `TenantUser` precedent for no clear benefit.

## R2. `Location` deactivation/reactivation is a soft-delete flag, not a status enum

**Decision**: `Location.DeactivatedAt` (`DateTime?`) is the single source of truth for active/inactive: `null` = active, non-null = deactivated. Reactivation clears it back to `null` (FR-008, clarified session 2026-07-06 Q1). `ListLocationsQuery` filters `DeactivatedAt == null` by default; an `includeDeactivated` flag (query parameter) allows historical/audit access (SC-005) without needing a second endpoint.

**Rationale**: Matches the field name and shape the original feature backlog and spec.md already specify (`deactivated_at`) — a nullable timestamp is simpler than introducing a `LocationStatus` enum (`Active`/`Deactivated`) for what is fundamentally a two-state, timestamped toggle. `Tenant.ProvisioningStatus` and the (future) `UserRole` pattern of enum + CHECK constraint is reserved for fields with three or more meaningfully distinct states; `Location`'s active/deactivated is binary and already has a natural timestamp value worth capturing (when it was deactivated).

**Alternatives considered**: A `LocationStatus` enum (`Active` | `Deactivated`) alongside a separate `DeactivatedAt` audit column — rejected as redundant: the nullable timestamp already encodes both the boolean state and the audit timestamp in one column, with no information loss.

## R3. Concurrency — no optimistic-concurrency token; plain EF Core last-write-wins

**Decision**: `Location` has no `[Timestamp]`/`xmin`-mapped concurrency token property. `UpdateLocationCommandHandler` loads the entity, applies the incoming field values, and calls `SaveChangesAsync()` — whichever request's `SaveChangesAsync()` commits last simply overwrites the previous save's values (FR-017, clarified session 2026-07-06 Q4).

**Rationale**: Directly implements the resolved clarification. No other entity in this codebase (`Tenant`, `TenantUser`) uses a concurrency token today, so introducing one for `Location` alone would be a new pattern for a low-stakes admin record (infrequent edits, no financial/regulatory data), not justified by the feature's actual risk profile.

**Alternatives considered**: Postgres `xmin`-based optimistic concurrency (EF Core's built-in support via `.UseXminAsConcurrencyToken()`) — rejected per the clarification; would add a stale-write-rejection UX (reload-and-retry) with no corresponding business requirement asking for it.

## R4. Deactivation dependent-check extension point — `IEnumerable<ILocationDeactivationGuard>`, empty for this feature

**Decision**: Add `ChildCare.Application.Common.ILocationDeactivationGuard` with one method, `Task<bool> HasActiveDependentsAsync(Guid locationId, ITenantDbContext db, CancellationToken ct)`. `DeactivateLocationCommandHandler` resolves `IEnumerable<ILocationDeactivationGuard>` from DI and calls each; if any returns `true`, the command fails with `errors.location.has_active_dependents` (FR-012) instead of setting `DeactivatedAt`. This feature registers **zero** implementations (`services.AddScoped<ILocationDeactivationGuard, ...>()` is not called at all in `Program.cs` for this feature) — with no registered guards, the `IEnumerable` resolves empty and deactivation always succeeds, matching spec.md's Assumption that no dependent entities exist yet.

**Rationale**: Using `IEnumerable<T>` (ASP.NET Core DI's native support for multiple registrations of the same interface) rather than a single `ILocationDeactivationGuard` means feature 005 (staff) and feature 007 (contracts) can each register their own guard independently later — `services.AddScoped<ILocationDeactivationGuard, StaffAssignmentDeactivationGuard>()` in 005, `services.AddScoped<ILocationDeactivationGuard, ActiveContractDeactivationGuard>()` in 007 — without either feature's registration overwriting the other's (a single-implementation seam would force whichever feature ships second to modify the first's registration or compose the checks manually). This is the concrete mechanism behind spec.md FR-012's "designed so features 005/007 can register their own condition."

**Alternatives considered**: A single `ILocationDeactivationGuard` interface swapped/replaced by whichever feature ships next — rejected, since it silently drops the earlier feature's guard the moment a later one is registered instead of composing, an easy-to-miss regression. A MediatR notification (`LocationDeactivatingNotification`) with handlers that throw to block — rejected as a less explicit mechanism for a yes/no gate than a typed interface with a boolean return; notifications are a better fit for fire-and-forget side effects than for something whose result must gate the command's own success/failure.

## R5. "Duplicate location" is a create-time convenience, not a persisted link

**Decision**: `DuplicateLocationCommand(Guid sourceLocationId, ...)` loads the source `Location`, constructs a brand-new `Location` with a new `Id` and all copyable field values (`Name`, `Address`, `Phone`, `Email`, `MaxCapacity`, `NaamLocatie`, `Dossiernummer`, `Verantwoordelijke`, `FlexPermission`, `BoPermission`), and saves it. No column or table records that the new location was duplicated from another — `Location` has no `DuplicatedFromId`/`SourceLocationId` field (FR-015, clarified session 2026-07-06 Q2).

**Rationale**: Directly implements the resolved clarification (Option C): a lightweight convenience that solves the "don't retype everything" pain point without building a cross-feature relocation/migration mechanism that features 005/007/011 don't have the entities to support yet. Leaving no persisted link also sidesteps a design question this feature has no business answering: what "linked" would even mean once staff/contract carryover is designed later is 005/007/011's call, not this feature's.

**Alternatives considered**: Full relocation support (Option A from clarification) with a `SupersedesLocationId` link so later features could auto-carry-over staff/contract assignments — rejected per the resolved clarification, since staff are explicitly not bound to one location (multi-location assignment already exists conceptually per feature 011) and building the link now, before 005/007/011 exist to consume it, risks a shape that doesn't match how those features actually end up modeling assignments.

## R6. Read side — `ListLocationsQuery`/`GetLocationByIdQuery` as MediatR queries, not direct EF Core calls

**Decision**: Both reads are modeled as MediatR queries with handlers in `ChildCare.Application/Locations/`, following the same registration/pipeline as every command (constitution Principle III's `AddMediatR`/`ValidationBehavior` setup, already wired in `Program.cs` from features 001–003 — no new registration needed beyond adding `Location`'s command/query types to the same assembly MediatR already scans).

**Rationale**: The constitution permits "simple, single-entity lookups" to query directly, but this feature's list endpoint isn't perfectly trivial (it has an `includeDeactivated` filter, R2) and using MediatR uniformly for both reads and writes keeps every location-related operation testable and mockable the same way, without a special-cased direct-query endpoint that looks inconsistent with every other endpoint in the file.

**Alternatives considered**: Direct `ITenantDbContext` queries inline in `LocationEndpoints.cs` for the two GETs — rejected only for consistency; either approach would have satisfied the constitution, but mixing the two styles in one endpoint file (writes via MediatR, reads direct) was judged more confusing for whoever reads this code next than the small amount of MediatR ceremony for two simple queries.
