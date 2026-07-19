# API Contract: Email Communications

All routes are tenant-scoped via `ITenantDbContext` except the unsubscribe endpoint, which is
deliberately public/unauthenticated (spec.md Security Considerations). Error responses use the
existing i18n-key convention (`{ "errorKey": "errors.email.<reason>" }`), 422 for
FluentValidation failures per this codebase's standard pipeline behaviour.

## `POST /api/email/attachments/upload-url`

**Auth**: `DirectorOnly`.

Request:

```json
{ "contentType": "application/pdf" }
```

Returns a signed GCS upload URL (R3) plus the object path the send request must reference:

```json
{ "uploadUrl": "https://storage.googleapis.com/...", "objectPath": "bulk-email-attachments/{id}/attachment.pdf" }
```

`422 errors.email.invalid_content_type` if `contentType` isn't `application/pdf`/`image/jpeg`/
`image/png` (FR-017).

## `POST /api/email/bulk-send`

**Auth**: `DirectorOnly`.

Request:

```json
{
  "locationId": "...",
  "groupId": null,
  "subject": "Menu update for next week",
  "body": "...",
  "attachmentObjectPath": null,
  "attachmentFileName": null,
  "attachmentContentType": null
}
```

`422 errors.email.subject_required` / `errors.email.subject_too_long` /
`errors.email.body_required` / `errors.email.body_too_long` / `errors.location.not_found` /
`errors.group.not_found` (mirrors `SendAnnouncementCommandValidator`'s existing rule set).
`422 errors.email.attachment_too_large` if the uploaded object exceeds 10MB (verified
server-side via the storage port before sending — R3).

Returns the delivery outcome summary (FR-012, SC-001):

```json
{
  "bulkEmailSendId": "...",
  "sentCount": 42,
  "skippedNoEmailCount": 1,
  "providerFailureCount": 0
}
```

A scope resolving to zero recipients returns `sentCount: 0` with no error (FR-016) — the
director-web client is expected to show the zero-recipient state *before* calling this endpoint
(via a lightweight recipient-count preview, next endpoint below), but the send endpoint itself
never errors on zero recipients either.

## `GET /api/email/bulk-send/recipient-count`

**Auth**: `DirectorOnly`. Query: `locationId`, `groupId?`.

Returns the resolved household count for the compose screen's pre-send preview (FR-016):

```json
{ "recipientCount": 5 }
```

## `POST /api/email/daily-report/{childId}/resend`

**Auth**: `StaffOrDirector` (caregiver or director — spec.md Primary Consumer).

No request body. Sends the current day's daily report email to every contact of `childId` with an
email on file, independent of digest-unsubscribe state (FR-009).

Returns:

```json
{ "sentCount": 2, "skippedNoEmailCount": 0 }
```

`404 errors.child.not_found` if `childId` doesn't resolve within the caller's tenant.

## `GET /api/email/unsubscribe`

**Auth**: none (public — spec.md Security Considerations). Query: `token` (R5's signed,
purpose-scoped `IDataProtector` payload), `org` (the tenant's public `Tenant.Slug` — mirrors
`ResetPasswordCommand`'s `OrganisationSlug`/`AuthLinkBuilder`'s `org` query param exactly, R5;
resolved via `OrganisationSlugResolver` **before** the token is looked up, since there's no JWT
`tenant_id` claim on this public route to resolve the schema from otherwise).

Renders a simple, framework-minimal confirmation page (not a JSON API response — this is the one
parent-facing web surface a browser navigates directly, per spec.md's UX Requirements) showing
the contact's current subscription state and a single confirm action. Invalid/unresolvable `org`,
or an invalid/tampered `token` once resolved → a calm "this link isn't valid" message, never a
raw error (FR-018).

## `POST /api/email/unsubscribe`

**Auth**: none (public). Body: `{ "token": "...", "org": "..." }`.

Resolves the tenant schema from `org`, then toggles `Contact.DigestUnsubscribedAt` (sets it if
currently subscribed) for the token's contact within that schema. Idempotent (FR-020) —
re-posting an already-applied token succeeds silently, no error.

```json
{ "unsubscribed": true }
```

## `POST /api/email/resubscribe`

**Auth**: none (public). Body: `{ "token": "...", "org": "..." }`. Same
token+org shape/verification as unsubscribe (R5 — the token is purpose-scoped to
"digest-unsubscribe" toggling generally, not single-direction). Clears
`Contact.DigestUnsubscribedAt`. Idempotent.

```json
{ "unsubscribed": false }
```

## Internal (no HTTP surface): `send-daily-reports` CLI subcommand

`dotnet run -- send-daily-reports`, invoked only by the Cloud Scheduler job (R2) — not an HTTP
endpoint, listed here because it's this feature's other primary "contract" (its trigger
mechanism and exit-code semantics matter for the Terraform job definition). Exit code `0` if
every tenant succeeded, `1` if any tenant failed (matches `SendPaymentRemindersCommand`'s
existing convention) — the Cloud Run Job's own execution status surfaces failures to Cloud
Monitoring without any new alerting code in this feature.

## Extended (not new routes): closure/announcement email fan-out

`POST /api/closures/{id}/publish` and `POST /api/announcements` (both pre-existing, features 011
and 013) gain no new request/response shape — they additionally send email to resolved recipients
as a side effect (FR-010, FR-011), invisible to their existing contracts.
