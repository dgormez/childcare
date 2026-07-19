# Data Model: Email Communications

## Contact (extended)

Existing entity (`backend/ChildCare.Domain/Entities/Contact.cs`), gains one field:

| Field                 | Type      | Notes                                                        |
|-----------------------|-----------|---------------------------------------------------------------|
| `DigestUnsubscribedAt`| `DateTime?` | Null = subscribed (default). Set = unsubscribed, timestamped for audit (spec.md Technical Requirements). Toggled only by the unsubscribe/re-subscribe endpoint (R5) — never set directly by any bulk/announcement/closure send path, which must not be affected by this flag (FR-008). |

No other change to `Contact`. Existing `Locale` field (already present) drives template locale
selection for every email this feature sends (R1).

## BulkEmailSend

New entity — one row per director-initiated bulk send (also reused internally, without a
director-facing UI, for the daily digest's own send-attempt bookkeeping — see "Daily Digest Send"
below).

| Field                | Type       | Notes                                                                 |
|-----------------------|-----------|-------------------------------------------------------------------------------------------|
| `Id`                  | `Guid`    | PK.                                                                                        |
| `LocationId`          | `Guid`    | Required scope (FR-001).                                                                   |
| `GroupId`             | `Guid?`   | Null = whole-location scope; set = group-scoped, mirrors `Announcement.GroupId` (FR-001).  |
| `Subject`             | `string`  | Director-composed.                                                                         |
| `Body`                | `string`  | Director-composed (plain text or lightly-formatted; rendered into the shared HTML layout via R1's Scriban template — no raw HTML from the director, to avoid template-injection risk). |
| `AttachmentObjectPath`| `string?` | GCS object path once an attachment is uploaded (R3); null = no attachment. Realizes spec.md's conceptual "BulkEmailAttachment" entity as columns on the parent row (1:1, mirrors `HealthRecord.AttachmentObjectPath`'s existing single-column precedent) rather than a separate child table — simpler for a strictly-one-attachment-per-send feature. |
| `AttachmentFileName`  | `string?` | Original filename, for the outbound MIME attachment's display name.                        |
| `AttachmentContentType`| `string?`| One of `application/pdf`/`image/jpeg`/`image/png` (R3).                                    |
| `SentByTenantUserId`  | `Guid`    | Director who composed/sent it, mirrors `Announcement.SentByTenantUserId`.                  |
| `SentAt`              | `DateTime`| Defaults to `UtcNow`.                                                                       |

**Validation** (FluentValidation, mirrors `SendAnnouncementCommandValidator`): `Subject`
non-empty/≤200 chars, `Body` non-empty/≤5000 chars, `LocationId` must exist, `GroupId` (if set)
must belong to `LocationId`, `AttachmentContentType` (if an attachment is present) must be in the
allow-list.

## BulkEmailRecipient

New entity — one row per resolved recipient of a `BulkEmailSend` (mirrors
`AnnouncementRecipient`/`ClosureNotificationDelivery`'s per-recipient audit-row shape — R6).

| Field            | Type                        | Notes                                                              |
|------------------|-----------------------------|---------------------------------------------------------------------|
| `Id`             | `Guid`                      | PK.                                                                 |
| `BulkEmailSendId`| `Guid`                      | FK to `BulkEmailSend`.                                              |
| `ContactId`      | `Guid`                      | FK to `Contact`.                                                    |
| `Status`         | `BulkEmailDeliveryStatus`   | `Sent` / `SkippedNoEmail` / `ProviderFailure` (new enum, R6).       |
| `Error`          | `string?`                   | Exception type name only when `Status == ProviderFailure`, never the raw provider message (matches `ClosureNotificationDelivery.Error`'s convention). |
| `CreatedAt`      | `DateTime`                  | Defaults to `UtcNow`.                                               |

Backs the director's post-send summary (FR-012, SC-001): grouped counts by `Status`.

## Daily Digest Send (no new persisted entity)

The automatic daily digest and the on-demand resend (FR-004, FR-009) render directly from the
existing `GetDailySummaryQuery` read-model (feature 013/009) per child/contact/locale — nothing
new is persisted per email sent. The `send-daily-reports` CLI command (R2) logs per-tenant
sent/skipped/failed counts to stdout (matching `SendPaymentRemindersCommand`'s existing
`Console.WriteLine` summary convention) rather than writing audit rows, since there is no
director-facing UI surface (unlike bulk email) that needs to read them back — this mirrors why
`PaymentReminderNotificationService` doesn't persist a delivery-audit table either, only the
`Invoice.ReminderCount`/`LastReminderSentAt` fields needed to compute the next eligible send.

## Closure / Announcement email fan-out (no new entity)

`ClosureNotificationService.NotifyAsync` and `SendAnnouncementCommandHandler.Handle` are extended
to additionally call this feature's new `IBulkEmailSender`-style send path (or equivalent shared
service — concrete shape decided in `tasks.md`) per resolved recipient with an email on file,
reusing `BulkEmailRecipient`'s `Status` enum internally for the same partial-failure logging, but
without a new `BulkEmailSend` row per closure/announcement (those already have their own
`KdvClosureDay`/`Announcement` row as the "what was sent" record — a `BulkEmailSend` row would be
a redundant second record of the same event).

## New Enum: `BulkEmailDeliveryStatus`

```
Sent
SkippedNoEmail
ProviderFailure
```

Lives in `backend/ChildCare.Domain/Enums/`, alongside the existing `ClosureDeliveryStatus`
(`NotApplicable`/`Pending`/`Sent`/`Failed`) it's modeled after — not reused directly, since
`ClosureDeliveryStatus` is push-specific (`NotApplicable` covers "no push token," a concept that
doesn't map to email's "no email address" the same way, and `Pending` has no meaning for a
synchronous-within-the-batch email send the way it does for a push dispatch awaited separately).

## Migration

One EF Core migration: `Contact.DigestUnsubscribedAt` (nullable timestamp, default null — no
backfill needed), `BulkEmailSend` table, `BulkEmailRecipient` table (FK to `BulkEmailSend` and
`Contact`, indexed on `BulkEmailSendId` for the summary aggregation query). Generated as a SQL
script and run manually per `CLAUDE.md`'s "EF Core never auto-migrates in production" convention
— same as every prior feature.
