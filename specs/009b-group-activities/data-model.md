# Data Model: Group Activities

## `GroupActivity` (tenant schema, table `group_activities`)

| Field           | Type              | Notes |
|-----------------|-------------------|-------|
| `Id`            | `Guid`            | PK, client-generatable (idempotent create, mirrors `ChildEvent`'s `RecordChildEventCommand` FR-013a pattern). |
| `GroupId`       | `Guid`            | FK → `groups.id`. Required. |
| `LocationId`    | `Guid`            | FK → `locations.id`. Required — denormalized alongside `GroupId` the same way `ChildEvent` stores both, since the device token's claims carry both directly (R8) and every read path filters by location too. |
| `ActivityType`  | `GroupActivityType` (enum, stored as string) | One of `Outdoor`, `Creative`, `Music`, `Story`, `Celebration`, `Other`. Default `Other`. |
| `Title`         | `string`          | Required, max 200 chars. Pre-filled client-side from `ActivityType`, editable. |
| `Description`   | `string?`         | Optional free text, max 2000 chars (matches the `Notes` length precedent from feature 012a's convergence finding). |
| `OccurredAt`    | `DateTime` (UTC)  | Required. Defaults to save time client-side (spec.md Assumptions) — no separate time-picker. |
| `RecordedBy`    | `List<Guid>`      | Owned JSONB list of `StaffProfile.Id`, resolved server-side via `IShiftAttributionService` (R1). May be empty. Never client-supplied. |
| `RecordedByDeviceId` | `Guid`       | The device that recorded it — mirrors `ChildEvent.RecordedByDeviceId`, useful for audit/debugging which tablet posted an activity. |
| `CreatedAt`     | `DateTime` (UTC)  | Set server-side on insert. |

No `UpdatedAt`/`DeletedAt` — per spec.md's Assumptions, this entity has no edit path and uses hard delete, not soft delete, so those columns would be permanently unused.

**Validation** (`FluentValidation`, mirrors `RecordChildEventCommandValidator`'s structure):
- `GroupId`, `LocationId`, `Title`, `OccurredAt`: `NotEmpty`.
- `Title`: `MaximumLength(200)`.
- `Description`: `MaximumLength(2000)` when present.
- `ActivityType`: must parse to a known `GroupActivityType` value (invalid string → `400 errors.group_activities.invalid_activity_type`, same idiom as `ChildEventEndpoints`'s `invalid_event_type`).

## `GroupActivityPhoto` (tenant schema, table `group_activity_photos`)

| Field            | Type      | Notes |
|------------------|-----------|-------|
| `Id`             | `Guid`    | PK, client-generatable (matches offline-queue-friendly id pattern used elsewhere). |
| `GroupActivityId`| `Guid`    | FK → `group_activities.id`, `ON DELETE CASCADE` — deleting an activity removes its photo rows (GCS objects are deleted explicitly in the command handler, not via a DB trigger, since GCS isn't transactional with Postgres). |
| `ObjectPath`     | `string`  | GCS object path of the resized (max 1920px long edge) full image — never a public URL, per constitution Principle VI. Deterministic: `group-activities/{groupActivityId}/{photoId}.jpg`. |
| `ThumbnailObjectPath` | `string` | GCS object path of the 400px thumbnail: `group-activities/{groupActivityId}/{photoId}-thumb.jpg`. |
| `Caption`        | `string?` | Optional, max 500 chars. |
| `UploadedAt`     | `DateTime` (UTC) | Set server-side on successful upload+resize. |

**Validation**: enforced at the upload endpoint, before a row is created — reject (`413`) if the raw upload exceeds 10MB, reject (`409 errors.group_activities.photo_limit_reached`) if the activity already has 10 photos.

## Relationships

- `GroupActivity` (1) → `GroupActivityPhoto` (0..10): composition — photos have no independent lifecycle outside their activity (cascade delete).
- `GroupActivity.GroupId` → `Group` (existing entity, feature 006): many activities per group, no new relationship needed on `Group` itself.
- `GroupActivity` has no direct relationship to `Child` — it is deliberately not per-child (spec.md's core framing). Which parents see an activity is derived at read time via `ChildGroupAssignment` (existing entity: which children were in `GroupId` as of `OccurredAt`'s date), not stored on the activity.

## New enum: `GroupActivityType`

```csharp
public enum GroupActivityType { Outdoor, Creative, Music, Story, Celebration, Other }
```

Wire-string mapping uses a small `GroupActivityTypeExtensions` (plain lowercase round-trip, not `ChildEventTypeExtensions`'s snake_case mapping — none of these six values are multi-word) to keep the EF Core column conversion, API request parsing, and response mapping from drifting onto three independent copies of the same logic.

## Read-side shapes (not new tables — computed at query time)

- **Group timeline entry** (R4): a discriminated union of `ChildEvent` and `GroupActivity` rows for one `(GroupId, date)`, ordered by `OccurredAt`. No new table; assembled in `GetGroupTimelineQuery`.
- **Parent daily feed entry** (R5): `GetDailySummaryQuery`'s existing response gains a `GroupActivities` list (title, description, `occurredAt`, and a `photos: PhotoResponse[]` array that is empty when consent (R6) isn't met) — named distinctly from the existing `Activities` field (feature 013's per-child `ChildEventType.Activity` descriptions) to avoid colliding with that unrelated, already-shipped concept.
- **Parent gallery entry** (R5/R6): one row per `GroupActivityPhoto` across the current calendar month for every group the parent's child(ren) belong to, consent-filtered the same way; text-only activities (no photos) are excluded from this view per spec.md's Assumptions.
