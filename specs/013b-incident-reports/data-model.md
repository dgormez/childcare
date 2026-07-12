# Data Model: Incident Reports

## IncidentReport (new entity, tenant schema, table `incident_reports`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `gen_random_uuid()` default |
| `ChildId` | `Guid` | FK → `children.id`, NOT NULL. No cascade delete (FR-008) — `Child.DeactivatedAt` never removes or hides this row. |
| `LocationId` | `Guid` | FK → `locations.id`, NOT NULL. Resolved from the child's active room-shift context at submission, needed for the location filter (FR-009) and PDF header — not on the original BACKLOG schema, added since the cross-KDV inspection view (FR-009/FR-017) requires a location dimension to filter/index on. |
| `OccurredAt` | `DateTimeOffset` | NOT NULL. Caregiver-set, can be backdated (FR-003). |
| `LocationDetail` | `string?` | Free text — `'indoor'`/`'outdoor'`/`'transit'` offered as quick-select chips (mirrors `InjuryType`'s selection-first UX), or free text for anything else. |
| `Description` | `string` | NOT NULL (FR-002). |
| `InjuryType` | `IncidentInjuryType` (enum, stored as text) | NOT NULL (FR-002). One of `None`/`Scrape`/`Bump`/`Cut`/`Fall`/`Bite`/`Burn`/`AllergicReaction`/`Other`. |
| `FirstAidGiven` | `string?` | Optional free text. |
| `DoctorCalled` | `bool` | Default `false`. |
| `DoctorNotes` | `string?` | Optional; only meaningfully populated when `DoctorCalled`. |
| `ParentNotified` | `bool` | Default `false`. |
| `ParentNotifiedAt` | `DateTimeOffset?` | Nullable. |
| `ParentNotifiedHow` | `ParentNotifiedHow?` (enum, stored as text) | Nullable. One of `Phone`/`App`/`InPerson`. |
| `ReportedBy` | `List<Guid>` (JSONB or `uuid[]`) | Resolved server-side via `IShiftAttributionService` (research.md R1) — zero, one, or more caregiver ids. Never client-submitted. |
| `Witnesses` | `string?` | Optional free text. |
| `FollowUp` | `string?` | The only field editable after the 24-hour lock (FR-006). |
| `ReviewedAt` | `DateTimeOffset?` | Nullable. Set on first director detail-view read (research.md R3). Never reset by edits. Not on the original BACKLOG schema — added to satisfy FR-010/FR-011 (substitutes for the nonexistent director push channel). |
| `CreatedAt` | `DateTimeOffset` | `NOW()` default. Immutability clock (FR-005) is measured from this field, not `OccurredAt`. |
| `UpdatedAt` | `DateTimeOffset?` | Set on any accepted edit. |

### Validation rules

- `Description` and `InjuryType` required at creation (FR-002).
- Within 24 hours of `CreatedAt`: reporting caregiver or any director may edit any field
  (FR-007). After 24 hours: only `FollowUp` may change (FR-005/FR-006), enforced in
  `UpdateIncidentReportCommandHandler` regardless of client input.
- `ReportedBy` is never accepted from the request body — always overwritten server-side.
- `ParentNotifiedAt`/`ParentNotifiedHow` are informational only; no validation ties them to
  `ParentNotified` (a caregiver could mark `ParentNotified = true` without filling in the how/when,
  e.g. logged retroactively by a director).

### Relationships

- **Child** (1) → **IncidentReport** (many). No cascade delete; `Child.DeactivatedAt` has no
  effect on existing `IncidentReport` rows (FR-008).
- **Location** (1) → **IncidentReport** (many). Used for the cross-KDV filter (FR-009) and as the
  PDF's location-detail source (name/address/`Dossiernummer`, research.md R5).
- No relationship to `TenantUser` beyond the resolved `ReportedBy` array of ids (not a formal FK
  list — same pattern as `ChildEvent.RecordedBy`).

### Indexes

- `(LocationId, OccurredAt)` — supports FR-017's cross-KDV date-range + location filtering without
  a full table scan.
- `(ChildId, OccurredAt)` — supports the child-filtered view (this feature's substitute for a
  per-child "child file" tab, per spec Assumptions).

## Enums (new)

- **`IncidentInjuryType`**: `None`, `Scrape`, `Bump`, `Cut`, `Fall`, `Bite`, `Burn`,
  `AllergicReaction`, `Other`. Wire string mapping follows the `ChildEventTypeExtensions`
  multi-word convention (009) — do not rely on `ToString().ToLowerInvariant()` for
  `AllergicReaction`.
- **`ParentNotifiedHow`**: `Phone`, `App`, `InPerson`. Same multi-word wire-mapping note applies to
  `InPerson`.

## State transitions

```
[created] --(within 24h)--> [editable: any field] --(24h elapses)--> [locked: only FollowUp editable]
[created/editable/locked] --(director opens detail view, first time)--> [+reviewed, sticky]
```

No `deleted`/`cancelled` state — incident reports are never deleted (legal document retention,
FR-008).
