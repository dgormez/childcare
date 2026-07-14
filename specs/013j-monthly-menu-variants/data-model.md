# Data Model: Monthly Menu Variants

## Extended entities

### `MonthlyMenu` (tenant schema, extends 013e)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | unchanged |
| `LocationId` | `Guid` | unchanged |
| `Year` | `int` | unchanged |
| `Month` | `int` | unchanged |
| `Variant` | `string` (domain: `DietaryType?`) | **NEW**. Wire value `"base"` = the existing base menu (`null` at the domain level); otherwise one of `DietaryTypeExtensions`' wire strings (`"halal"`, `"kosher"`, `"vegetarian"`, `"vegan"`, `"gluten_free"`). Non-nullable at the DB level — see research.md's sentinel-value decision. |
| `PublishedAt` | `DateTime?` | unchanged — publish state is per `(LocationId, Year, Month, Variant)` row, so each variant publishes independently |
| `CreatedBy` | `Guid?` | unchanged |
| `CreatedAt` | `DateTime` | unchanged |
| `Days` | `List<MonthlyMenuDay>` | unchanged relationship |

**Unique index**: `(LocationId, Year, Month, Variant)` — replaces 013e's
`(LocationId, Year, Month)` index. At most 6 rows per location/month (1 base + up to 5 variants).

### `Location` (extends existing entity)

| Field | Type | Notes |
|---|---|---|
| `MenuVariantPriorityOrder` | `List<DietaryType>` | **NEW**. Ordered — index 0 is highest priority. Empty list (default) means no variants enabled; a location that never touches this setting behaves exactly as before 013j (FR-012). Stored as `text[]`, same conversion pattern as `MealPreference.DietaryType`. |

## Unchanged entities (read, not modified)

- **`MonthlyMenuDay`** (013e) — no changes. A variant's days are rows exactly like the base
  menu's, just belonging to a `MonthlyMenu` with a non-`"base"` `Variant`.
- **`MealPreference`** (013d) — no changes. Its existing `DietaryType List<DietaryType>` field is
  the read-only input to this feature's resolution logic; this feature does not write to it.

## New in-memory / contract shapes

### `MenuVariantPriorityOrder` (API request/response shape)

An ordered array of wire-string `DietaryType` values, e.g. `["halal", "vegetarian"]`. Order is
significant (FR-002) — index 0 resolves first when a child matches more than one entry.

### `ParentMonthlyMenuEntry` (restructured, was location-keyed, now (location, child)-keyed)

| Field | Type | Notes |
|---|---|---|
| `LocationId` | `Guid` | unchanged |
| `LocationName` | `string` | unchanged |
| `ChildId` | `Guid` | **NEW** — the entry is now per child, not per location |
| `ChildName` | `string` | **NEW** — for the parent-mobile per-child section label |
| `ResolvedVariant` | `string?` | **NEW** — wire-string `DietaryType`, or `null` when the base menu was resolved (never exposes `"base"` as a value to the client; `null` means base) |
| `IsPublished` | `bool` | unchanged meaning: whether *some* menu (variant or base) was found published for this child |
| `Days` | `IReadOnlyList<MonthlyMenuDayEntry>` | unchanged shape, now the resolved menu's days |
| `ClosureDates` | `IReadOnlyList<DateOnly>` | unchanged |

## Resolution algorithm (FR-008/FR-009)

```text
for each child the parent has an active contract for, grouped by location:
  load the location's MenuVariantPriorityOrder (already-fetched Location row)
  load every published MonthlyMenu for (location, year, month) — base + all enabled variants,
    one query per location, not per child (research.md's efficiency decision)
  resolvedVariant = null  // base, as fallback
  for each dietaryType in location.MenuVariantPriorityOrder (in order):
    if child.MealPreference.DietaryType contains dietaryType
       and a published MonthlyMenu exists for (location, year, month, dietaryType):
      resolvedVariant = dietaryType
      break
  entry = build ParentMonthlyMenuEntry from either the resolved variant's MonthlyMenu
          or the base MonthlyMenu (whichever resolvedVariant points to)
```

A child with no `MealPreference` row, or an empty `DietaryType` list, never matches any variant
and always resolves to the base menu — identical to today's behavior (FR-009, Edge Cases).
