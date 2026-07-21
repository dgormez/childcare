# Phase 1 Data Model: Digital Online Enrollment

## Location (existing entity, extended)

New columns:

| Field | Type | Default | Notes |
|---|---|---|---|
| `PublicEnrollmentEnabled` | `bool` | `false` | FR-002/FR-012 — every existing row defaults to `false`; no location's behavior changes until a director explicitly opts in. Mirrors `QrCheckInEnabled` (021), `RequiresCaregiverPin` (008b). |
| `PublicEnrollmentSlug` | `string` | generated at creation/migration | Unique **within the tenant schema** (not globally) — the location segment of the public URL `/enroll/{orgSlug}/{locationSlug}` (research.md R1). Auto-derived from `Name` (slugified, numeric suffix on collision); existing locations are backfilled by the migration so every location has one from day one, independent of whether public enrollment is enabled. |
| `DefaultEnrollmentLocale` | `string` | `"nl"` | The public form's starting language-toggle position for this location (spec.md Assumptions) — the parent's own selection, once changed, governs their confirmation/tour emails regardless of this default. |

No other changes to `Location`.

## WaitingListEntry (existing entity, feature 012a, extended)

New columns:

| Field | Type | Default | Notes |
|---|---|---|---|
| `Source` | `WaitingListEntrySource` enum (`DirectorEntered`, `SelfRegistered`) | `DirectorEntered` | FR-007 — existing rows default to `DirectorEntered` so 012a's shipped behavior is unaffected. |
| `ReferenceCode` | `string?` | `null` | FR-008 — set only for `SelfRegistered` entries; short, human-legible alphanumeric (research.md R5), unique within the tenant schema. `null` for director-entered entries (they have no self-service reference need). |
| `SubmittedLocale` | `string?` | `null` | `nl`/`fr`/`en` — the language the parent selected on the public form (set only for `SelfRegistered` entries); governs the confirmation email and any later tour-invitation email to this contact. |
| `TourProposedAt` | `DateTime?` | `null` | FR-015 — the date/time the director proposed when sending a tour invitation. |
| `TourInvitationStatus` | `TourInvitationStatus` enum (`NotSent`, `Sent`, `Accepted`, `Declined`) | `NotSent` | FR-016 — updated to `Accepted`/`Declined` when the recipient uses the emailed link; existing rows default to `NotSent`. |
| `TourInvitationSentAt` | `DateTime?` | `null` | Set when a tour invitation is sent; supports re-sending (overwritten on a subsequent send, per research.md R2 — no history retained). |
| `TourOutcome` | `string?` | `null` | FR-017 — a director's free-text record of what actually happened, independent of `TourInvitationStatus`. |

**Validation delta from 012a**: `ContactEmail` becomes **required** specifically when
`Source == SelfRegistered` (FR-003/edge case — the only delivery channel for the confirmation
and reference code); it remains optional for `DirectorEntered` entries, unchanged from 012a.

**Duplicate detection**: not a stored field — computed at read time in
`ListWaitingListEntriesQuery` by comparing `ChildFirstName`/`ChildLastName`/`DateOfBirth` against
other entries at the same `LocationId` (research.md R3). FR-011.

**Terminal-status guard**: `RespondTourInvitationCommand` (the accept/decline link handler)
MUST check `Status` is `Waiting` or `Offered` before writing `TourInvitationStatus` — a response
arriving after the entry reached `Enrolled`/`Withdrawn` is a no-op (FR-018), matching 012a's
existing terminal-status semantics.

## Tour Invitation (conceptual — not a new entity)

Modeled entirely as the `WaitingListEntry` fields above (research.md R2) — no new table.

## Notification (existing entity, first director-targeted use)

No schema change. `NotificationType` gains one new value, `EnrollmentSubmitted`. Every prior use
of `Notification` targets a parent/contact's `TenantUserId` (resolved via a contact-resolution
step); this feature is the first to set `TenantUserId` directly to a **director** account —
`EnrollmentNotificationService` creates one `Notification` row per `TenantUser` in the tenant
schema with `Role == Director` (Directors are tenant-wide, not location-scoped — confirmed by
`TenantUser`'s shape, which has no location-assignment field; only `Staff` profiles have
`StaffLocationEligibility`), per FR-010.

## State Transitions

`WaitingListEntry.Status` (012a's existing `waiting` → `offered` → `enrolled`/`withdrawn`
lifecycle) is **unchanged** — self-registration only changes how an entry enters `waiting`, not
the lifecycle itself. `TourInvitationStatus` is an independent, secondary state machine
(`NotSent` → `Sent` → `Accepted`/`Declined`), not a substate of `Status`, and never drives a
`Status` transition — the director always makes the `offered`/`enrolled`/`withdrawn` decision
explicitly, per FR-017's "independent of whether the recipient responded" requirement.
