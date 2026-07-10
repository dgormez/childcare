# API Contracts: Parent Communication (013)

Minimal APIs, MediatR-backed (constitution Principle III). Auth policy noted per route. New policy needed: none — `DirectorOnly`, `StaffOrDirector`, `ParentOnly` already exist (feature 003).

## Parent account provisioning

- `POST /api/parent-invitations` — **DirectorOnly**. Body: `{ contactId }`. Validates `Contact.CanPickup` (via any `ChildContact`) and non-null `Email` (FR-000a). Creates a `TenantUser (Role=Parent, PasswordHash="")` + `ParentInvitation`, emails the invite link (mirrors `POST /api/staff` → invitation flow). 409 if `Contact.TenantUserId` already set.
- `POST /api/parent-invitations/accept` — **Anonymous, tenant-exempt** (organisation slug in request body, mirrors `POST /api/staff/accept-invitation`). Body: `{ organisationSlug, token, password }`. Sets `TenantUser.PasswordHash`, sets `Contact.TenantUserId`. On success, backfills this contact onto every existing `MessageThread` for their linked children (FR-006a). Generic `404 errors.invitation.not_found` on invalid/expired/used token (feature 001's non-enumerable-error precedent).

## Daily summary

- `GET /api/parent/children/{childId}/daily-summary?date=YYYY-MM-DD` — **ParentOnly**. Authorizes caller's linked `Contact` is a `ChildContact` of `childId`, then delegates to `GetDailySummaryQuery` (extended per research.md R5). 403 if not authorized. Default `date` = today (Belgian calendar day, matching `BelgianCalendarDay` helper).
- `GET /api/parent/children` — **ParentOnly**. Lists the caller's own children (id, name, photo) — needed for the home screen to know which children to show summaries for (User Story 1, Scenario 3).

## Messaging

- `POST /api/parent/message-threads` — **ParentOnly**. Body: `{ childId?, subject, body }`. Validates `childId` (if present) belongs to the caller. Creates thread + first message + participants (self + every other parent contact of the child with an active account, per FR-003a).
- `GET /api/parent/message-threads` — **ParentOnly**. Lists caller's threads, most-recently-active first, with unread indicator.
- `GET /api/parent/message-threads/{id}` — **ParentOnly**. 403/404 if caller is not a participant (FR-006). Marks staff-authored messages as read on fetch.
- `POST /api/parent/message-threads/{id}/messages` — **ParentOnly**. Body: `{ body }`. 403/404 if not a participant.
- `GET /api/message-threads` — **DirectorOnly**. Lists all threads for the org, with unread-from-parent count (FR-013). Only the list/detail-read routes are `DirectorOnly` since no staff-facing web UI ships in v1 (spec Assumptions); the reply route below is `StaffOrDirector` regardless, matching every other endpoint's authorization pattern in this codebase rather than narrowing the policy to match the currently-shipped UI surface.
- `GET /api/message-threads/{id}` — **DirectorOnly**. Full thread view.
- `POST /api/message-threads/{id}/messages` — **StaffOrDirector**. Body: `{ body }`. Sender = caller.

## Announcements

- `POST /api/announcements` — **DirectorOnly**. Body: `{ locationId, groupId?, subject, body }`. Resolves recipients = contacts of currently-enrolled children in scope with `TenantUserId != null` (R8), creates `AnnouncementRecipient` rows, `Notification` rows, and dispatches pushes (R3). Completes with 0 recipients, not an error, per Story 3 Scenario 4.
- `GET /api/announcements` — **DirectorOnly**. Sent history.
- `GET /api/parent/announcements/{id}` — **ParentOnly**. 403/404 unless an `AnnouncementRecipient` row exists for the caller's contact. Marks `ReadAt`. No reply endpoint exists (FR-009 — read-only by omission, not by a blocked write).

## Notifications

- `GET /api/parent/notifications` — **ParentOnly**. Most-recent-first, all types.
- `POST /api/parent/notifications/{id}/read` — **ParentOnly**. Sets `ReadAt`; 403/404 if not the owner.

## Push token

- `PUT /api/parent/push-token` — **ParentOnly**. Body: `{ pushToken }`. Resolves caller's linked `Contact`, overwrites `Contact.PushToken` (R2) — satisfies FR-014's replace-on-reinstall requirement by construction (single column, last write wins).

## Error keys (new, i18n)

`errors.parent_invitation.not_eligible` (no email / not can-pickup), `errors.parent_invitation.already_has_account`, `errors.invitation.not_found` (reused from feature 001), `errors.message_thread.not_participant`, `errors.announcement.not_recipient`.
