# Research: Monthly Menu Variants

No `[NEEDS CLARIFICATION]` markers remain in `spec.md` — the three genuinely open product-shape
questions were resolved with the product owner before writing it (see BACKLOG.md's `### 013j`
section). This document records the implementation-level decisions research surfaced while
grounding the plan in the actual backend schema.

## Decision: `MonthlyMenu.Variant` storage — sentinel string, not a nullable column

**Decision**: Store `Variant` as a non-nullable `text` column with the wire value `"base"`
representing the base menu, converted to/from a domain-level `DietaryType?` (`null` for `"base"`)
via an EF Core `HasConversion`, reusing `DietaryTypeExtensions.ToWireString`/
`TryParseWireString` for the five real values. The unique index becomes
`(LocationId, Year, Month, Variant)` on this non-nullable column.

**Rationale**: PostgreSQL unique indexes treat `NULL` as distinct from every other `NULL` — a
naive `Variant DietaryType?` nullable column would let multiple base-menu rows exist for the same
`(LocationId, Year, Month)` with no constraint violation, silently breaking the exact invariant
013e depended on (`UpsertMonthlyMenuCommand`'s find-or-create-by-`(LocationId, Year, Month)`
logic would then pick an arbitrary one of several rows). A sentinel non-null value sidesteps this
Postgres behavior entirely while keeping the domain-level API (`DietaryType?`) exactly as
`spec.md` describes it.

**Alternatives considered**:
- A genuinely nullable column with a Postgres partial unique index
  (`WHERE "Variant" IS NULL`) covering only the base-menu case, plus the normal composite index
  for variants — rejected: two separate index definitions to keep in sync is more moving parts
  than one sentinel value, for no behavioral benefit.
- A separate boolean `IsBaseMenu` flag alongside a nullable `Variant` — rejected: reintroduces
  the same NULL-uniqueness gap unless also constrained, and adds a second field that must stay
  consistent with the first.

## Decision: extending existing commands/queries in place, not new variant-specific ones

**Decision**: `GetMonthlyMenuQuery`, `UpsertMonthlyMenuCommand`, `PublishMonthlyMenuCommand`, and
`UnpublishMonthlyMenuCommand` each gain a `Variant` parameter (defaulting to base/`"base"` at the
endpoint layer when the query string is absent) rather than gaining variant-specific sibling
commands.

**Rationale**: FR-012/SC-003 require the base-menu path to be byte-for-byte behaviorally
unchanged. The only way to guarantee that is for the base menu to run through literally the same
code as every variant, parameterized — not a second, separately-maintained implementation that
could drift. This mirrors this codebase's own established instinct (013i extended
`MonthlyMenuDayGrid`/the existing write path rather than building a parallel one for CSV-imported
data).

**Alternatives considered**: A distinct `MonthlyMenuVariant` entity/command family alongside the
untouched original — rejected: doubles the surface area to test and maintain, and the two would
inevitably need to share almost all their logic anyway (same day-grid shape, same publish
semantics), so parameterizing one implementation is strictly simpler.

## Decision: variant-not-enabled rejection lives in the command validator, not the endpoint

**Decision**: `UpsertMonthlyMenuCommand`/`PublishMonthlyMenuCommand`/`UnpublishMonthlyMenuCommand`
each validate that the requested `Variant` (when not `"base"`) is present in the location's
current `MenuVariantPriorityOrder` before proceeding, returning a `422`/domain failure — not a
check performed only in the director-web UI.

**Rationale**: FR-006 explicitly requires rejecting this "through the web UI or a direct API
call" — this is a server-side authorization-adjacent invariant (a director shouldn't be able to
publish content for a variant they never enabled, e.g. after later disabling it), and this
codebase's established convention (Constitution III, FluentValidation pipeline behavior) is that
validation belongs in the command, not the endpoint or the client.

## Decision: `Location.MenuVariantPriorityOrder` storage

**Decision**: `List<DietaryType>` stored as `text[]`, converted via the exact same
`HasConversion`/`ValueComparer` pattern `MealPreference.DietaryType` already uses
(`TenantDbContext.cs`), defaulting to an empty array.

**Rationale**: This is the identical shape (an ordered — order matters here, more than for
`MealPreference.DietaryType` where it doesn't — list of the same enum) to an already-solved
problem in this exact codebase. Postgres `text[]` preserves insertion order, which is required
since the list *is* the priority order (FR-002/FR-008), not just a set membership check.

## Decision: `GetParentMonthlyMenuQuery` restructuring — resolve per child inside the existing
location loop, don't add a second query

**Decision**: Keep the existing "for each location the parent's children are contracted at" loop
structure, but change its innermost step from "load one menu for the location" to "for each
child at this location, resolve their menu" — producing one `ParentMonthlyMenuEntry` per
(location, child) instead of one per location. Both the base menu and every enabled variant for
that location/month are loaded once per location (not once per child) and resolution then reads
from that already-loaded set per child, avoiding an N+1 query pattern as household size grows.

**Rationale**: Matches this codebase's existing efficiency instinct (013e's own loop already
batches per-location, not per-child-then-per-location) while making the minimal structural change
needed to add the per-child dimension FR-010 requires.

**Alternatives considered**: A second, separate parent-facing endpoint for variant resolution
that the app calls alongside the existing one — rejected: `parent-mobile/services/menu.ts`
already has a single fetch-then-cache-fallback path per month; splitting it into two round trips
adds complexity and a new partial-failure mode (what if one succeeds and the other doesn't) for
no benefit over resolving server-side in one response.
