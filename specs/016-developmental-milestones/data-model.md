# Data Model: Developmental Milestones

## `developmental_domains` (public schema — shared, read-only reference data)

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid, PK | |
| `Code` | varchar(30), unique | `motor_gross`, `motor_fine`, `language`, `cognitive`, `social`, `emotional`, `self_care` |
| `NameNl` | varchar(100) | |
| `NameFr` | varchar(100) | |
| `NameEn` | varchar(100) | |
| `SortOrder` | int | Display order across domains |

No `IsActive`/soft-delete — unlike `VaccineType`, these 7 domains are a fixed, closed set defined
by BACKLOG.md; no deactivation path is in scope (spec FR-011).

## `developmental_milestones` (public schema — shared, read-only reference data)

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid, PK | |
| `DomainId` | uuid | FK → `developmental_domains.Id` (same-schema FK, unlike the tenant-side reference below) |
| `AgeFromMonths` | int | Inclusive lower bound of the age band |
| `AgeToMonths` | int | Inclusive upper bound of the age band |
| `DescriptionNl` | text | |
| `DescriptionFr` | text | |
| `DescriptionEn` | text | |
| `SortOrder` | int | Display order within a domain |

Index: `(DomainId, SortOrder)` for grouped display; `(AgeFromMonths, AgeToMonths)` for age-band
resolution queries.

## `child_milestone_observations` (tenant schema — append-only)

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid, PK | |
| `ChildId` | uuid | FK → `children.Id` (same-schema FK) |
| `MilestoneId` | uuid | **No DB FK** — references a public-schema row (research.md R1, same precedent as `VaccineRecord.VaccineTypeId`) |
| `Status` | varchar(20) | `emerging` \| `achieved` \| `not_yet` — validated in the Application layer (FluentValidation), not a DB CHECK constraint (matches how `child_events`' `EventType` is validated) |
| `ObservedAt` | date | The date the observation reflects (may differ from `CreatedAt`, e.g. a caregiver logging slightly after the fact) |
| `ObservedBy` | uuid[] (native Postgres array) | Every `StaffProfileId` checked in for that location/group at `ObservedAt` — resolved via the existing `IShiftAttributionService.ResolveRecordedByAsync`, identical to `ChildEvent.RecordedBy`'s exact shape and column type (0, 1, or 2+ entries; empty if nobody was checked in) rather than a single value, since a room can have more than one caregiver simultaneously checked in (discovered during implementation — corrects this document's original single-`uuid` description) |
| `Notes` | text, nullable | Optional free text |
| `CreatedAt` | timestamptz | Set once at insert; never updated |

**No `UpdatedAt`, no soft-delete column, no update/delete MediatR command or endpoint** —
immutability is structural (research.md R3), not policy-enforced.

Indexes: `(ChildId, MilestoneId, CreatedAt)` for per-milestone history and "most recent status"
resolution; `(ChildId)` for building the full portfolio in one query.

## Derived view: Milestone Portfolio

Not a stored entity — computed by `MilestonePortfolioBuilder` from the three tables above:

1. Load all `developmental_milestones` joined to `developmental_domains`, grouped by domain,
   ordered by `SortOrder`.
2. Load all `child_milestone_observations` for the child, ordered by `CreatedAt` descending.
3. For each milestone, the "current status" is the most recent observation's `Status` (or
   "no observations yet" if none exist).
4. Resolve the child's current age in months from `Child.DateOfBirth`; any milestone where
   `AgeFromMonths <= ageInMonths <= AgeToMonths` is flagged `IsCurrentFocus = true`.
5. Full history per milestone is the ordered list of that milestone's observations, included in
   the director query's response and available in the PDF; the parent-facing response includes
   only the current status per milestone (per UX Requirements' "plain, warm" framing — full
   observation-by-observation history is a director/management-view detail, not something a
   parent needs enumerated) plus the age-appropriate highlight.

## State Transitions

`child_milestone_observations` has no state machine — each row is a fact about a point in time.
"Current status" for a milestone is a read-time derivation (the latest row), not a stored field
that transitions. A regression (`achieved` → `not_yet`) is simply a new row with an earlier
status's opposite value; nothing on the previous row changes.
