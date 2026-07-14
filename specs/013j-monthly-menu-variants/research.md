# Research: Monthly Menu Variants

No `[NEEDS CLARIFICATION]` markers remain in `spec.md` — the three genuinely open product-shape
questions were resolved with the product owner before writing it (see BACKLOG.md's `### 013j`
section). This document records the implementation-level decisions research surfaced while
grounding the plan in the actual backend schema.

## Decision: `MonthlyMenu.Variant` storage — sentinel string column, plain `string` CLR type

**Decision**: Store `Variant` as a non-nullable `text` column with the wire value `"base"`
representing the base menu. The domain entity's CLR property is a plain `string` (default
`"base"`), not `DietaryType?` — no EF Core `HasConversion` is registered for it at all. The
`DietaryType?` <-> `"base"`-sentinel-string translation happens entirely in the Application layer
(`MonthlyMenuVariantHelper.ToStorageWire`/`FromStorageWire`), reusing
`DietaryTypeExtensions.ToWireString`/`TryParseWireString`. Command/query records (e.g.
`GetMonthlyMenuQuery.Variant`) still expose `DietaryType?` as their public API — only the entity
and its EF configuration are plain strings. The unique index becomes
`(LocationId, Year, Month, Variant)` on this non-nullable column. `Location.
MenuVariantPriorityOrder` follows the identical reasoning: `List<string>` (wire strings), not
`List<DietaryType>`, with no EF conversion either.

**Rationale — the sentinel value itself**: PostgreSQL unique indexes treat `NULL` as distinct
from every other `NULL` — a naive `Variant DietaryType?` nullable column would let multiple
base-menu rows exist for the same `(LocationId, Year, Month)` with no constraint violation,
silently breaking the exact invariant 013e depended on (`UpsertMonthlyMenuCommand`'s
find-or-create-by-`(LocationId, Year, Month)` logic would then pick an arbitrary one of several
rows).

**Rationale — avoiding `DietaryType?`/`List<DietaryType>` EF conversions entirely (discovered
during implementation, not anticipated during planning)**: The original plan called for
`Variant DietaryType?` with a custom `HasConversion` (sentinel logic in the lambda) and
`MenuVariantPriorityOrder List<DietaryType>` with the same whole-collection `HasConversion`
pattern `MealPreference.DietaryType` already uses successfully. Implementing this exactly as
planned crashed every write/read touching either property with `System.ArgumentException:
Expression of type 'DietaryType' cannot be used for parameter of type 'Nullable<DietaryType>'`
inside `Npgsql.EntityFrameworkCore.PostgreSQL`'s `NpgsqlArrayConverter`. Isolating it (removing
each new conversion in turn) showed the trigger was `MonthlyMenu.Variant`'s new *nullable scalar*
`DietaryType?` converter colliding with `MealPreference.DietaryType`'s **pre-existing**
`List<DietaryType>` converter (013d, unrelated to this feature, already shipped and working) — a
Npgsql provider bug in how it discovers/reuses an "element converter for `DietaryType`" across
the whole model when both a nullable-scalar and a list conversion for the same enum coexist.
Since `MealPreference.DietaryType` cannot be touched (out of scope, already shipped), the fix was
to keep *both* of this feature's own properties as plain strings at the EF-mapped/entity level,
eliminating any `DietaryType`-keyed value converter this feature would otherwise add to the
model. The JSON wire contract (`string`/`string[]`) is completely unaffected either way — this
was purely an internal representation choice forced by an EF/Npgsql provider limitation, worth
remembering generally: introducing a new nullable-enum `HasConversion` in a model that already
has a `List<TheSameEnum>` conversion elsewhere is a de-risked pattern to avoid in this codebase
until this specific Npgsql bug is fixed upstream.

**Alternatives considered**:
- A genuinely nullable column with a Postgres partial unique index
  (`WHERE "Variant" IS NULL`) covering only the base-menu case, plus the normal composite index
  for variants — rejected: two separate index definitions to keep in sync is more moving parts
  than one sentinel value, for no behavioral benefit, and doesn't even address the Npgsql
  collision above (that's about EF's own converter machinery, not the SQL index shape).
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

**Decision**: `List<string>` (wire strings, order-preserving) stored as a plain `text[]`, no EF
conversion — see the merged `MonthlyMenu.Variant` decision above for the full rationale (the
originally-planned `List<DietaryType>` + `HasConversion` approach, matching
`MealPreference.DietaryType`'s pattern, is what triggered the Npgsql collision this feature had
to route around). Postgres `text[]` preserves insertion order, which is required since the list
*is* the priority order (FR-002/FR-008), not just a set membership check — order still matters
identically whether the elements are stored as raw strings or converted enums.

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
