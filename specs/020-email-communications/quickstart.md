# Quickstart: Email Communications

## Prerequisites

- Local backend running against Docker PostgreSQL, migrations applied (including this feature's
  `Contact.DigestUnsubscribedAt`/`BulkEmailSend`/`BulkEmailRecipient` migration).
- `Email:SmtpHost` configured (e.g. a local Mailhog/Mailpit container) so sends aren't silently
  no-op'd — point `Email:SmtpHost`/`Email:SmtpPort` at it in `appsettings.Development.json` or an
  environment override.
- A seeded tenant with one location, one group, at least 4 families including: one household with
  two children at the same location sharing one contact, one contact with no email on file, one
  child with two guardian contacts in different locales (e.g. NL and FR).
- A director JWT for that tenant; a caregiver device token for the on-demand resend check.

## Validate: bulk send collapses siblings to one email, skips no-email contacts

```
GET /api/email/bulk-send/recipient-count?locationId={id}
POST /api/email/bulk-send
{ "locationId": "{id}", "groupId": null, "subject": "Test", "body": "Hello families" }
```

Expect `recipientCount` to equal the number of distinct households, not children (User Story 1,
Scenario 1). Expect the send response's `skippedNoEmailCount` to be 1 for the seeded no-email
contact, and `sentCount` to cover everyone else. Check Mailhog/Mailpit: the sibling household
received exactly one email.

## Validate: bulk send with an attachment

```
POST /api/email/attachments/upload-url { "contentType": "application/pdf" }
```

PUT a small PDF to the returned `uploadUrl`, then send a bulk email referencing the returned
`objectPath`. Expect the received email (Mailhog/Mailpit) to include the PDF as an intact
attachment (User Story 1, Scenario 3).

## Validate: zero-recipient scope is a no-op, not an error

Call `recipient-count` for an empty group. Expect `recipientCount: 0`. Call `bulk-send` for that
scope anyway. Expect `sentCount: 0`, no error (User Story 1, Scenario 5).

## Validate: daily digest sends independently per contact, per locale

```
dotnet run -- send-daily-reports
```

Expect Mailhog/Mailpit to show two separate emails for the two-guardian child, one in NL, one in
FR — not one combined email (User Story 2, Scenario 1). Expect a child with zero events that day
to still receive an email whose body clearly reads "no updates logged today" (Scenario 2).

## Validate: unsubscribe stops only that contact's digest

Open the unsubscribe link from one of the two guardians' received emails (`GET
/api/email/unsubscribe?token=...&org={tenant-slug}` — the `org` slug is required to resolve the
correct tenant schema, since this is a public route with no JWT to resolve it from, per
research.md R5), confirm via the page's action. Re-run `send-daily-reports`.
Expect only the unsubscribed guardian to be skipped; the other guardian and every other
household's contacts still receive their digest (User Story 2, Scenarios 3/6).

## Validate: unsubscribed contact still receives bulk/announcement/closure/resend

With the same contact still unsubscribed from the digest: send a bulk email covering their child,
publish a closure day, send an announcement, and trigger an on-demand resend for their child.
Expect all four to reach that contact normally (User Story 2 Scenario 4, User Story 3, User Story
4).

## Validate: on-demand resend bypasses digest-unsubscribe

```
POST /api/email/daily-report/{childId}/resend
```

(Caregiver device token or director JWT.) Expect delivery even though the target contact is
digest-unsubscribed (User Story 3, Scenario 2).

## Validate: closure/announcement email fan-out

Publish a `KdvClosureDay` and send an `Announcement` as already covered by features 011/013's own
quickstarts; additionally confirm (Mailhog/Mailpit) that every resolved contact with an email on
file received an email alongside the existing in-app message/push, including contacts with no
parent-app account (`TenantUserId == null`) who would previously have received neither (R4) — a
key behavioural difference from the pre-existing push-only fan-out.

## Validate: idempotent unsubscribe

POST the same unsubscribe token twice. Expect both responses to succeed with `unsubscribed: true`
and no error (FR-020).
