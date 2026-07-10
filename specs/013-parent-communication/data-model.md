# Data Model: Parent Communication (013)

All new tables live in the tenant schema (`ChildCare.Domain/Entities`, migrated via `TenantDbContext`), consistent with every prior feature. C# identifiers are English-only per constitution Principle IV.

## Modified entities

### `Contact` (feature 006 — extended)

| Field | Type | Notes |
|---|---|---|
| `TenantUserId` | `Guid?` | **New.** Set once a parent-app invitation for this contact is accepted. Null = no parent account exists yet. One `Contact` ↔ at most one `TenantUser` (`Role = Parent`). |

`PushToken` (existing, feature 009) is unchanged in shape — this feature is simply the first to write it (R2).

## New entities

### `ParentInvitation`

Structural copy of `StaffInvitation` (feature 005).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `ContactId` | `Guid` | FK → `Contact`. The contact being invited. |
| `Email` | `string` | Snapshot of the contact's email at invite time (mirrors `StaffInvitation.Email`). |
| `TokenHash` | `byte[]` | Hash of the plaintext token sent in the invite link (`InvitationTokenCodec`, reused as-is). |
| `ExpiresAt` | `DateTime` | |
| `CreatedAt` | `DateTime` | |

No `UsedAt` column — "used" is derived from whether the linked `TenantUser.PasswordHash` is non-empty, same as `StaffInvitation`.

**Validation**: A `ParentInvitation` can only be created for a `Contact` with `CanPickup = true` (via at least one active `ChildContact` link) and a non-null `Email` (FR-000a). Creating one when `Contact.TenantUserId` is already set is rejected (already has an account).

### `MessageThread`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `Subject` | `string` | |
| `ChildId` | `Guid?` | Nullable — null for a general, non-child-specific thread (FR-003). |
| `CreatedAt` | `DateTime` | |
| `LastActivityAt` | `DateTime` | Denormalized for "most recently active first" ordering (User Story 2, Scenario 3) — updated on every new message. |

### `MessageThreadParticipant`

| Field | Type | Notes |
|---|---|---|
| `ThreadId` | `Guid` | FK → `MessageThread`. Part of composite PK. |
| `TenantUserId` | `Guid` | FK → `TenantUser`. Part of composite PK (R6 — uniform ID type across parent/staff/director participants). |
| `AddedAt` | `DateTime` | When this participant joined (supports FR-006a backfill). |

Composite PK `(ThreadId, TenantUserId)`, matching the original prompt's `(thread_id, user_id)`.

### `Message`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `ThreadId` | `Guid` | FK → `MessageThread`. |
| `SenderId` | `Guid` | FK → `TenantUser`. |
| `Body` | `string` | |
| `SentAt` | `DateTime` | |
| `ReadAt` | `DateTime?` | "Read by the other side" marker (R7) — null until the first cross-side read. |

### `Announcement`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `LocationId` | `Guid` | FK → `Location`. Scope anchor (FR-007). |
| `GroupId` | `Guid?` | FK → `Group`, nullable. Null = whole-location scope; set = group-scoped. |
| `Subject` | `string` | |
| `Body` | `string` | |
| `SentByTenantUserId` | `Guid` | FK → `TenantUser` (director). |
| `SentAt` | `DateTime` | |

### `AnnouncementRecipient`

One row per contact the announcement actually reached (R8 — bounded to contacts with an active parent account).

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AnnouncementId` | `Guid` | FK → `Announcement`. |
| `ContactId` | `Guid` | FK → `Contact`. |
| `ReadAt` | `DateTime?` | Per-recipient read state (announcements are one-to-many, unlike shared threads — each parent has their own read state here). |

### `Notification`

Generic in-app notification-centre entry (R4). Scoped to `NewMessage` / `Announcement` / `TemperatureAlert` for this feature — extensible for future types (e.g. day-reservation decisions, feature 013a) without a redesign.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `TenantUserId` | `Guid` | FK → `TenantUser` (recipient — always `Role = Parent` for this feature). |
| `Type` | `NotificationType` enum | `NewMessage`, `Announcement`, `TemperatureAlert`. |
| `SourceId` | `Guid` | Points at the `MessageThread.Id`, `Announcement.Id`, or `ChildEvent.Id` depending on `Type`. **No database-level FK constraint** — this column is intentionally polymorphic across three different target tables, so a single FK cannot be declared without breaking two of the three `Type` values. Referential integrity for this column is an application-layer concern, not a schema one. |
| `TitleKey` | `string` | i18n key, same pattern as `ParentClosureMessage.TitleKey`. |
| `BodyKey` | `string` | i18n key. |
| `ArgumentsJson` | `string` | Serialized args for the body template (e.g. child name, sender name). |
| `CreatedAt` | `DateTime` | |
| `ReadAt` | `DateTime?` | |

## State transitions

- **`Contact.TenantUserId`**: `null` → set (one-way; a parent account is never unlinked in this feature — deactivation is future scope).
- **`ParentInvitation`**: created → accepted (implicit, via `TenantUser.PasswordHash` becoming non-empty) | expired (implicit, via `ExpiresAt`). No explicit status column, matching `StaffInvitation`'s precedent.
- **`Message.ReadAt` / `AnnouncementRecipient.ReadAt` / `Notification.ReadAt`**: `null` → set once, never cleared.

## Validation rules

- FR-000a: `ParentInvitation` creation requires `Contact.CanPickup = true` (derived from `ChildContact`) and `Contact.Email != null`.
- FR-002/FR-017/FR-018: every parent-facing query filters by `TenantUserId` → `Contact.TenantUserId` → `ChildContact` → `ChildId`, scoped to the tenant schema (structural, per `TenantDbContext`/`TenantMiddleware`, constitution Principle I). No parent-facing query ever reads `ChildEvent` without the existing `VisibleToParent` filter.
- FR-003a/FR-006a: `MessageThread` creation/participant-backfill logic queries `ChildContact` for `ChildId` to resolve which `Contact`s (and their linked `TenantUserId`s, where non-null) become participants.
