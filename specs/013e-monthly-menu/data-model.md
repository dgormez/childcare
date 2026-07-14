# Data Model: Monthly Menu

Phase 1 output. Three new tenant-schema tables. All FKs reference existing tenant-schema tables
(`locations`, `children`, `users`) — no cross-schema references.

## `MonthlyMenu` (`monthly_menus`)

One row per location per year/month.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `gen_random_uuid()` default |
| `LocationId` | `Guid` | FK → `locations.Id`, `NOT NULL` |
| `Year` | `int` | `NOT NULL` |
| `Month` | `int` | `NOT NULL`, `CHECK (Month BETWEEN 1 AND 12)` |
| `PublishedAt` | `DateTime?` | `NULL` = draft, not parent-visible (FR-002) |
| `CreatedBy` | `Guid?` | FK → `users.Id` |
| `CreatedAt` | `DateTime` | default `NOW()` |

**Constraints**: `UNIQUE (LocationId, Year, Month)` — enforces FR-005 (at most one menu per
location/year/month; editing an existing month updates this row, never creates a duplicate).

**Lifecycle**: `Draft` (`PublishedAt = null`) ⇄ `Published` (`PublishedAt` set). Un-publish
(FR-004) clears `PublishedAt` back to `null` — no separate status enum; the nullable timestamp
*is* the state, mirroring `DayReservation`'s use of nullable `DecidedAt`/`DecidedBy` as implicit
state rather than a redundant parallel enum.

## `MonthlyMenuDay` (`monthly_menu_days`)

One row per calendar date within a `MonthlyMenu`. A date with no row, or a row with all
course fields `null`, both render as "—" (FR-007) — the UI does not distinguish a missing row
from an explicitly blank one, so the write path may pre-create all of the month's day rows on
first save (simplifies the director-web day-grid form) or create rows only for days the director
fills in — implementation may choose either; both satisfy FR-007 identically from the read side.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `MenuId` | `Guid` | FK → `monthly_menus.Id`, cascade delete, `NOT NULL` |
| `MenuDate` | `DateOnly` | `NOT NULL` |
| `Soup` | `string?` | max length 500 |
| `MainCourse` | `string?` | max length 500 |
| `Dessert` | `string?` | max length 500 |
| `Notes` | `string?` | max length 500 (e.g. "geen warme maaltijd deze dag") |

**Constraints**: `UNIQUE (MenuId, MenuDate)`.

## `MealPreferenceChangeRequest` (`meal_preference_change_requests`)

One row per parent-submitted request.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `ChildId` | `Guid` | FK → `children.Id`, `NOT NULL` |
| `RequestedBy` | `Guid` | `TenantUserId` of the submitting parent — same convention as `DayReservation.RequestedBy` (stores the `TenantUserId`, not `Contact.Id`) |
| `NewTexture` | `string?` | `MealTexture` enum as string (013d's existing convention); `null` = no change requested to texture |
| `NewDietaryType` | `string[]?` | `DietaryType` enum list as Postgres `text[]` (013d's existing convention); `null` = no change requested |
| `Notes` | `string?` | parent's free-text note, max length 2000 (mirrors `DayReservation.Reason`'s bound) |
| `Status` | `string` | `MealPreferenceChangeRequestStatus` enum as string: `Pending` (default) / `Approved` / `Rejected` |
| `DecidedBy` | `Guid?` | FK → `users.Id` |
| `DecidedAt` | `DateTime?` | |
| `DecisionNotes` | `string?` | director's optional rejection reason, max length 2000 — kept distinct from the parent's own `Notes`, mirroring `DayReservation.Reason` (requester) vs. `DayReservation.DirectorNotes` (decider) |
| `CreatedAt` | `DateTime` | default `NOW()` |

**Constraints**: No DB-level uniqueness constraint for "one pending per child" — enforced in the
command handler (research.md R6). FK `ChildId → children.Id`, no cascade (a request survives
independently of later child-record edits, same as `DayReservation`).

**Validation** (FluentValidation, not DB-level): at least one of `NewTexture`/`NewDietaryType`
must be provided (a request that changes nothing is meaningless); `NewTexture`, if present, must
be a valid `MealTexture` value; `NewDietaryType` entries, if present, must each be a valid
`DietaryType` value.

**State transitions**: `Pending` → `Approved` (writes through to `MealPreference` via R1) or
`Pending` → `Rejected` (no side effect on `MealPreference`). Terminal once decided — no
`Cancelled` state (out of scope; the spec does not require a parent-initiated cancel, unlike
`DayReservation`'s `Cancelled`).

## Read models (not persisted)

- **`ParentMonthlyMenuView`**: per parent-facing request, one entry per distinct active-contract
  location across the parent's linked children — `{ locationId, locationName, isPublished, days[],
  closureDates[] }`. Computed on read from `MonthlyMenu`/`MonthlyMenuDay` plus
  `IClosureCalendarReader` (research.md R4/R5); not a table.
- **`MealPreferenceChangeRequestReviewItem`**: director-queue read pairing a pending
  `MealPreferenceChangeRequest` with the child's currently-active `HealthRecord` rows (013c,
  `ValidFrom`/`ValidUntil` covering today) for context (FR-013) — computed on read, no new join
  table.
