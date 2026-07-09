# Data Model: Closure Calendar

## KdvClosureDay

Location-specific closure date in a tenant schema.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key. |
| `LocationId` | `Guid` | Required; FK to `Location`; unique together with `Date`. |
| `Date` | `DateOnly` | Required; Europe/Brussels calendar day; create must be today or future. |
| `Label` | `string` | Required; max 200; director-authored content. |
| `ClosureType` | enum/string | `holiday`, `training`, `extraordinary`. |
| `NotifyParents` | `bool` | Defaults true. |
| `Status` | enum/string | `draft`, `published`, `cancelled`. |
| `NotificationSentAt` | `DateTime?` | Set after publish notification/message processing completes; null when not notified. |
| `PublishedAt` | `DateTime?` | Set when published. |
| `PublishedBy` | `Guid?` | Director tenant-user id. |
| `CancelledAt` | `DateTime?` | Set when cancelled. |
| `CancelledBy` | `Guid?` | Director tenant-user id. |
| `CreatedBy` | `Guid` | Director tenant-user id. |
| `UpdatedBy` | `Guid?` | Last director tenant-user id to edit a draft. |
| `AttendanceGeneratedAt` | `DateTime?` | Set when publish creates/updates closure attendance records. |
| `AttendanceGeneratedBy` | `Guid?` | Director tenant-user id whose publish triggered attendance generation. |
| `CreatedAt` | `DateTime` | UTC. |
| `UpdatedAt` | `DateTime` | UTC. |

### Validation

- `(LocationId, Date)` is unique.
- `Date` cannot be earlier than today's Europe/Brussels date when creating.
- Published/cancelled closures cannot move to a different location/date.
- Draft closures can be edited.
- Cancelling a draft removes it; cancelling a published closure sets `Status = cancelled`.

### State Transitions

```text
draft --publish--> published
draft --remove--> [deleted]
published --cancel--> cancelled
cancelled --(terminal)--> cancelled
```

## ClosureNotificationDelivery

Per-parent notification/message delivery outcome for publish and cancellation.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key. |
| `ClosureDayId` | `Guid` | FK to `KdvClosureDay`. |
| `ContactId` | `Guid` | Parent/contact recipient. |
| `Kind` | enum/string | `published`, `cancelled`. |
| `PushToken` | `string?` | Token attempted, if available. |
| `PushStatus` | enum/string | `not_applicable`, `sent`, `failed`. |
| `MessageId` | `Guid?` | FK to `ParentClosureMessage`. |
| `Error` | `string?` | Sanitized failure reason. |
| `CreatedAt` | `DateTime` | UTC. |

### Validation

- Unique by `(ClosureDayId, ContactId, Kind)` to prevent duplicate parent messages.
- Push failure does not roll back closure publish/cancellation.

## ParentClosureMessage

Minimal parent-visible in-app message for one-way closure notices.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key. |
| `ContactId` | `Guid` | Parent/contact recipient. |
| `ClosureDayId` | `Guid` | FK to `KdvClosureDay`. |
| `Kind` | enum/string | `published`, `cancelled`. |
| `TitleKey` | `string` | i18n key. |
| `BodyKey` | `string` | i18n key. |
| `ArgumentsJson` | JSON | Location name, date, label, type. |
| `CreatedAt` | `DateTime` | UTC. |
| `ReadAt` | `DateTime?` | Future parent app can mark read. |

## AttendanceRecord Changes

Existing entity. Feature 011 sets `Status = Closure` for enrolled child/location/date on publish.

Add fields if needed to satisfy audit requirements:

| Field | Type | Rules |
|-------|------|-------|
| `ClosureDayId` | `Guid?` | Source closure that generated this closure record. |
| `PriorStateJson` | JSON/string? | Previous attendance state when a confirmed same-day closure overwrites present/absent data. |
| `ClosureConfirmedBy` | `Guid?` | Director tenant-user id for confirmed overwrite. |

## Closure Calendar Reader

Application service/query surface for feature 014.

Input: `locationId`, inclusive `from`, inclusive `to`.

Output: set of closure `DateOnly` values where `Status = published`.
