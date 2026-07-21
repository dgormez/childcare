# Contract: Digital Online Enrollment API

Public (anonymous, `.RequireTenantExempt()`) endpoints resolve tenant/location from URL
segments, never a JWT claim (research.md R1). Director-facing endpoints reuse the existing
`DirectorOnly` policy — no new authorization policy invented.

## `GET /api/public/enrollment/{orgSlug}/{locationSlug}`

Anonymous, tenant-exempt. Lets the public form know whether to render itself or the disabled
message, before any submission is attempted (FR-013).

- `404 errors.public_enrollment.not_found` — `orgSlug` doesn't resolve to a `Ready` tenant, or
  `locationSlug` doesn't resolve within it. Deliberately indistinguishable between "no such org"
  and "no such location" (mirrors `OrganisationSlugResolver`'s existing not-found collapsing —
  this is a public route, no reason to leak which segment was wrong).
- `200`:
  ```json
  { "locationName": "string", "enabled": true, "defaultLocale": "nl" }
  ```
  No capacity, contact, or other tenant data is exposed (FR-021) — only what the public form
  needs to render itself or the disabled state.

## `POST /api/public/enrollment/{orgSlug}/{locationSlug}`

Anonymous, tenant-exempt, `.RequireRateLimiting("public-enrollment")` (research.md R6 — 3/IP/
rolling hour, FR-006).

Request:
```json
{
  "childFirstName": "string",
  "childLastName": "string",
  "dateOfBirth": "date",
  "requestedStartDate": "date",
  "contactName": "string",
  "contactEmail": "string",
  "contactPhone": "string | null",
  "notes": "string | null",
  "locale": "nl" | "fr" | "en",
  "website": "string"
}
```
`website` is the honeypot field (FR-005) — named to look like a plausible real field to a bot,
never rendered as a visible input by the real form. If non-empty, the endpoint returns the same
`200` shape below **without** calling any command (no entry created, no email sent) — the
rejection must not be observable to the submitter.

Validation (FluentValidation pipeline behavior, per Constitution III):
- `childFirstName`/`childLastName`/`contactName`/`contactEmail`: required.
- `contactEmail`: valid email format (required specifically for self-registered entries, per
  data-model.md's validation delta from 012a).
- `dateOfBirth`: required, not in the future (FR-004).
- `locale`: one of `nl`/`fr`/`en`.
- A validation failure returns `422` with locale-aware `errorKey`s per field (Constitution IV) —
  never a raw exception message.

- `404 errors.public_enrollment.not_found` — same collapsing as the `GET` above.
- `403 errors.public_enrollment.disabled` — `Location.PublicEnrollmentEnabled` is `false`
  (FR-013's server-side enforcement, checked even if the client bypasses the UI's own disabled
  state).
- `429` (standard ASP.NET Core rate-limiter rejection response) — more than 3 submissions from
  this IP within the rolling hour (FR-006); the public page renders this as the calm,
  human-readable "please try again later" state per spec.md, not a raw status code.
- `200` (honeypot-triggered OR genuine success — same shape, FR-005):
  ```json
  { "referenceCode": "string" }
  ```
  On genuine success: creates a `WaitingListEntry` (`Source = SelfRegistered`, `Status =
  Waiting`) per FR-007, sends the confirmation email (`EmailService.SendEnrollmentConfirmationAsync`,
  in `locale`) per FR-009, and creates a `Notification` for every director in the tenant per
  FR-010 (`EnrollmentNotificationService`, data-model.md). On honeypot trigger: `referenceCode`
  is a plausible-looking but unusable placeholder (not tied to any real entity) — the response
  shape must not itself reveal the rejection to an unsophisticated bot.

## `PUT /api/locations/{locationId}/public-enrollment-setting`

`DirectorOnly`. Mirrors feature 021's `PUT /api/locations/{id}/qr-checkin-setting` exactly (same
update-and-return-the-whole-`LocationResponse` convention, same log-only-on-change behavior).

Request:
```json
{ "enabled": true }
```
- Updates `Location.PublicEnrollmentEnabled` (FR-012).
- `200`: the full updated `LocationResponse`.
- `404 errors.locations.not_found` / `403` (existing `DirectorOnly` policy) — unchanged
  conventions.

## `POST /api/waiting-list/{id}/tour-invitation`

`DirectorOnly` (existing `/api/waiting-list` group).

Request:
```json
{ "proposedAt": "datetime" }
```
- Requires the entry to have a `ContactEmail` on file — `422 errors.waiting_list.no_contact_email`
  if absent (director-entered entries may lack one, per 012a).
- Generates a signed token (`ITourInvitationTokenService.CreateToken(entryId)`, research.md R5),
  builds the accept/decline links, sends the tour-invitation email
  (`EmailService.SendTourInvitationAsync`, in the entry's `SubmittedLocale` if self-registered,
  else the location's `DefaultEnrollmentLocale`), and sets `TourInvitationStatus = Sent`,
  `TourInvitationSentAt = now`, `TourProposedAt = proposedAt` (FR-015). Re-sending overwrites
  these fields (research.md R2 — no history kept).
- `200`: the updated `WaitingListEntryResponse` (now including `TourInvitationStatus`/
  `TourProposedAt`/`TourInvitationSentAt`).
- `404 errors.waiting_list.not_found` — unchanged convention.

## `POST /api/waiting-list/{id}/tour-outcome`

`DirectorOnly`.

Request:
```json
{ "outcome": "string" }
```
- Sets `TourOutcome` (FR-017) — independent of `TourInvitationStatus`, callable whether or not
  an invitation was ever sent or responded to.
- `200`: the updated `WaitingListEntryResponse`.
- `404 errors.waiting_list.not_found` — unchanged convention.

## `GET /api/public/enrollment/tour-response`

Anonymous, tenant-exempt. Server-rendered HTML page (research.md R4), mirrors
`EmailEndpoints.RenderUnsubscribePage`'s exact pattern — not a JSON API response, not a `web/`
Next.js route.

Query: `?token={string}&org={orgSlug}&response=accepted|declined`

- Resolves `org` → tenant (`OrganisationSlugResolver`), then `token` → `WaitingListEntryId`
  (`ITourInvitationTokenService.TryParseToken`) — either failing renders a generic "invalid or
  expired link" HTML page (fails closed, no leak of which step failed, mirrors
  `DigestUnsubscribeLinkResolver`'s exact failure collapsing).
- If the resolved entry's `Status` is not `Waiting` or `Offered` (i.e., already `Enrolled`/
  `Withdrawn`), the page shows a neutral "this invitation is no longer active" message and does
  **not** write `TourInvitationStatus` (FR-018's terminal-status guard, data-model.md).
- Otherwise, sets `TourInvitationStatus = Accepted` or `Declined` per the `response` parameter
  (FR-016) and renders a confirmation page in the entry's `SubmittedLocale`. Idempotent — a
  repeated click with the same token/response is a no-op that still shows the confirmation page
  (mirrors `UnsubscribeDigestCommandHandler`'s idempotency).
