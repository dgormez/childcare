# Quickstart: Digital Online Enrollment

Validation scenarios proving this feature works end-to-end. Run against local dev
(`docker compose up` PostgreSQL + API) unless noted.

## Prerequisites

- A tenant (`orgSlug` known) with at least one location that has a `PublicEnrollmentSlug`, and
  one director account.
- A second, untouched location in the same tenant (for SC-002's isolation check).
- SMTP configured (or Mailpit locally) to observe the confirmation/tour-invitation emails.

## Scenario 1 — Setting defaults to disabled, director opt-in is isolated

1. `GET /api/public/enrollment/{orgSlug}/{locationSlug}` for a location that has never touched
   this setting — expect `{"enabled": false, ...}` (FR-002).
2. Visit the public enrollment page for that location — expect the "not currently accepting
   online applications" state, not the form.
3. `POST /api/public/enrollment/{orgSlug}/{locationSlug}` directly with a valid body anyway —
   expect `403 errors.public_enrollment.disabled` (FR-013's server-side enforcement).
4. As director, `PUT /api/locations/{locationId}/public-enrollment-setting` with
   `{"enabled": true}` — expect `200`.
5. `GET` the second, untouched location's setting — still disabled (SC-002).

## Scenario 2 — Full submission → director conversion loop

1. With the setting enabled (Scenario 1), submit the public form with valid data and
   `locale: "fr"` — expect `200` with a `referenceCode`.
2. Confirm a `WaitingListEntry` now exists with `Source = SelfRegistered`, `Status = Waiting`,
   the submitted `ReferenceCode`, and `SubmittedLocale = "fr"` (FR-007).
3. Confirm a confirmation email was sent to the submitted address, in French, containing the
   same reference code (FR-009, SC-001-adjacent).
4. Confirm every director `TenantUser` in the tenant received a `Notification` with
   `Type = EnrollmentSubmitted` (FR-010).
5. As director, transition the entry to `Enrolled` (existing `POST
   /api/waiting-list/{id}/status`) and open the child/contact creation flow — expect every
   field (child name/DOB, contact name/email/phone) pre-filled with zero retyping (FR-014,
   SC-003).

## Scenario 3 — Anti-spam paths

1. Submit the public form with the honeypot (`website`) field non-empty — expect `200` with a
   `referenceCode`-shaped response, but confirm **no** `WaitingListEntry` was created and **no**
   email was sent (FR-005).
2. Submit 4 valid submissions from the same source within an hour — expect the first 3 to
   succeed and the 4th to be rejected with a `429` (FR-006, SC-004).

## Scenario 4 — Duplicate flagging (never auto-rejected)

1. Submit the public form twice with the same `childFirstName`/`childLastName`/`dateOfBirth` at
   the same location (different contact details is fine).
2. Confirm **both** entries were created (`200` both times, two distinct `WaitingListEntry`
   rows) and that `ListWaitingListEntriesQuery`'s response flags the second as a possible
   duplicate of the first (FR-011, SC-005) — neither is auto-rejected.

## Scenario 5 — Tour invitation lifecycle

1. As director, `POST /api/waiting-list/{id}/tour-invitation` with a `proposedAt` — expect
   `200`, `TourInvitationStatus = "Sent"`, and a tour-invitation email sent to the entry's
   contact containing accept/decline links.
2. Follow the "accept" link (`GET /api/public/enrollment/tour-response?...&response=accepted`) —
   expect an HTML confirmation page and `TourInvitationStatus = "Accepted"` on the entry.
3. As director, `POST /api/waiting-list/{id}/tour-outcome` with a free-text outcome — expect
   `200` and the outcome saved, independent of step 2's response (FR-017).
4. Transition the entry to `Withdrawn`, then follow the *same* accept/decline link again —
   expect the "no longer active" page and `TourInvitationStatus` unchanged from step 2 (FR-018).

## Scenario 6 — Manual re-disable mid-fill

1. With the setting enabled, begin a submission (fetch the `GET` location-info response as the
   public page would).
2. As director, disable the setting.
3. Submit the previously-fetched form data — expect `403 errors.public_enrollment.disabled`
   (server-side enforcement, not just a hidden page), consistent with Scenario 1 step 3.
