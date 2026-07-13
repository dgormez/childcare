# Data Model: Meal List (Maaltijdenlijst)

## New entity: `MealPreference` (table: `child_meal_preferences`, tenant schema)

One row per child, holding kitchen-facing meal preferences. Independent of, and complementary to,
`Child.DietaryRestrictions` (free-text, feature 006) — see spec.md's Clarifications for why both
coexist.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `gen_random_uuid()` default. |
| `ChildId` | `Guid` | FK → `children(Id)`. **Unique** — one row per child. |
| `Texture` | `MealTexture` enum | `Pureed` \| `Mixed` \| `Pieces` \| `Normal`. Default `Normal`. |
| `DietaryType` | `List<DietaryType>` (Postgres `text[]`) | Zero or more of `Halal`/`Kosher`/`Vegetarian`/`Vegan`/`GlutenFree`. Empty by default. |
| `PortionSize` | `MealPortionSize` enum | `Small` \| `Normal` \| `Large`. Default `Normal`. |
| `AdditionalNotes` | `string?` | Free text, nullable. |
| `UpdatedAt` | `DateTime?` | Set on every write. |
| `UpdatedBy` | `Guid?` | FK → the acting director's `TenantUser.Id`. |
| `CreatedAt` | `DateTime` | Set once on first write. |

**Validation** (`UpsertMealPreferenceCommandValidator`):
- `ChildId` must reference an existing, non-deactivated child.
- `AdditionalNotes` capped at a reasonable length (mirrors 012a's `MaximumLength(2000)`
  precedent for a free-text note field) to avoid an unbounded value reaching Postgres unhandled.

**Lifecycle**: Upsert only — no delete endpoint (a preference reverting to defaults is itself a
valid upsert: `Texture = Normal`, `DietaryType = []`, `PortionSize = Normal`,
`AdditionalNotes = null`). No soft-delete needed since the row carries no standalone meaning once
its child is deactivated (feature 006's existing child soft-delete already hides the child from
every location-scoped read this feature performs).

## Read model: `MealListEntry` (not persisted — computed per request)

One entry per child returned by `GET /locations/{id}/meal-list?date=`, aggregated from:

| Source | Field(s) used |
|---|---|
| `AttendanceRecord` (010) | `Status == Present && CheckOutAt == null` — determines Present vs. excluded (Absent/Closure/already-checked-out). `CheckOutCommand` never changes `Status`, only sets `CheckOutAt`, so both conditions are required. |
| `Contract` + `ContractedDay` (007) | Active contract covering `LocationId` + today's weekday — determines "expected" (only computed when no `AttendanceRecord` exists yet for the date). |
| `ChildGroupAssignment` (006) | Current group (`GroupId` where `EndDate is null or > date`) — used to group the response. |
| `Child` (006) | `FirstName`, `LastName`, `AllergySeverity`. |
| `HealthRecord` (013c) | `RecordType = MedicationStanding`, `ValidFrom`/`ValidUntil` window covering today — standing-medication indicator. |
| `MealPreference` (this feature) | `Texture`, `DietaryType`, `PortionSize`, `AdditionalNotes` — absent row renders as "Geen voorkeur" defaults. |

**Shape** (response, see `contracts/meal-list-api.md` for the full schema):

```text
MealListResponse
├── GroupId, GroupName
│   └── Children: [MealListChildEntry]
└── Expected (only present/non-empty when ?includeExpected=true)
    └── Children: [MealListChildEntry] (no GroupId grouping — flat list)

MealListChildEntry
├── ChildId, FirstName, LastName
├── Texture, DietaryType[], PortionSize, AdditionalNotes (or "no preference" flag)
├── AllergySeverity: "severe" | "mild_moderate" | "none"
└── HasStandingMedication: bool
```

## New enums

- `MealTexture`: `Pureed`, `Mixed`, `Pieces`, `Normal` (default `Normal`).
- `MealPortionSize`: `Small`, `Normal`, `Large` (default `Normal`).
- `DietaryType`: `Halal`, `Kosher`, `Vegetarian`, `Vegan`, `GlutenFree`. Stored as a Postgres
  `text[]` column via an EF Core value converter (mirrors `Contract.ContractedDays`' existing
  owned-collection convention where applicable, adapted for a flat enum array rather than an
  owned type — see research.md; a `text[]` column with a converter is simpler here since each
  entry has no sub-fields, unlike `ContractedDay`).

## Migration

`AddChildMealPreferences` — creates `child_meal_preferences` with the FK/unique constraint above.
Per Constitution VI / `CLAUDE.md`: authored as a normal EF Core migration, SQL script generated
and applied manually per tenant schema — no auto-migrate.
