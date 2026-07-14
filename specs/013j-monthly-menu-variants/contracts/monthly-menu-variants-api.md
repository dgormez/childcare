# API Contract: Monthly Menu Variants

Extends 013e's `contracts/monthly-menu-api.md` (director/parent endpoint family) and adds a new
location-settings endpoint (013f precedent). All wire-string `DietaryType` values are
`DietaryTypeExtensions`' existing convention: `halal`, `kosher`, `vegetarian`, `vegan`,
`gluten_free`.

## Director endpoints (`DirectorOnly`) — extended

### `GET /api/locations/{locationId}/monthly-menus/{year}/{month}?variant={dietaryType}`

Unchanged shape; `variant` is a new optional query parameter. Absent = base menu (unchanged
013e behavior). Present = that variant's menu; still returns `exists: false` shell if nothing has
been authored yet, same as the base menu's existing empty-shell behavior.

### `PUT /api/locations/{locationId}/monthly-menus/{year}/{month}?variant={dietaryType}`

Unchanged request/response shape. **New validation**: if `variant` is present and not currently
in the location's `menuVariantPriorityOrder`, returns `422` with `errorKey:
"errors.monthly_menu.variant_not_enabled"` (FR-006).

### `POST /api/locations/{locationId}/monthly-menus/{year}/{month}/publish?variant={dietaryType}`

### `POST /api/locations/{locationId}/monthly-menus/{year}/{month}/unpublish?variant={dietaryType}`

Both unchanged shape; same new `variant_not_enabled` validation as `PUT`. Each variant's publish
state is independent (FR-004) — publishing the base menu never publishes a variant, and vice
versa.

### `PUT /api/locations/{locationId}/menu-variant-settings`

**New endpoint**, mirrors 013f's `PUT /api/locations/{id}/reservation-settings`.

**Request**:

```jsonc
{ "menuVariantPriorityOrder": ["halal", "vegetarian"] }
```

Order is significant — index 0 is highest priority (FR-002). An empty array disables all
variants for the location (FR-001's "no variant enabled by default" is this endpoint's own
default state, not a separate flag).

**Response 200**: the updated `LocationResponse`, now including `menuVariantPriorityOrder`.

**Validation**: every entry must be a recognized `DietaryType` wire string; no duplicates.

## Parent endpoint (`ParentOnly`) — restructured

### `GET /api/parent/monthly-menu?year={year}&month={month}`

**Response 200** — was one entry per location, now one entry per (location, child) pair:

```jsonc
[
  {
    "locationId": "...",
    "locationName": "KDV Zonnebloem",
    "childId": "...",
    "childName": "Emma",
    "resolvedVariant": "vegetarian",
    "isPublished": true,
    "days": [ { "date": "2026-08-01", "soup": "...", "mainCourse": "...", "dessert": "...", "notes": null } ],
    "closureDates": ["2026-08-15"]
  },
  {
    "locationId": "...",
    "locationName": "KDV Zonnebloem",
    "childId": "...",
    "childName": "Lucas",
    "resolvedVariant": null,
    "isPublished": true,
    "days": [ ... ],
    "closureDates": ["2026-08-15"]
  }
]
```

`resolvedVariant: null` means the base menu was resolved for that child (FR-009) — the client
never sees the `"base"` DB sentinel value, only `null` or a real `DietaryType` wire string
(FR-011: the UI must not expose that a resolution/fallback process occurred, so this field exists
for the client's own labeling logic, not as user-visible "fallback" messaging).

## Failure modes

| Condition | Behavior |
|---|---|
| `PUT`/`publish`/`unpublish` with a `variant` not in the location's `menuVariantPriorityOrder` | `422 errors.monthly_menu.variant_not_enabled` (FR-006) |
| `menu-variant-settings` with a duplicate or unrecognized `DietaryType` string | `422` with field-level validation errors |
| Parent has a child with no active contract at any location | that child contributes zero entries (unchanged from 013e's location-only logic — a child without a location has nothing to resolve) |
| Parent has two children at the same location with different dietary needs | two separate entries for that location, one per child, independently resolved (FR-010) |
