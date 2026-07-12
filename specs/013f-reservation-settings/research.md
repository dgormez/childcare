# Research: Reservation Settings

## R1: `DecidedBy = null` as the "system decided" signal

**Decision**: Reuse `DayReservation.DecidedBy` (already nullable `Guid?`) as the system-attribution
signal for `informational`-mode auto-approvals — set it to `null` instead of a director's
`TenantUserId`. No new column, no magic sentinel `Guid`.

**Rationale**: Every existing approval/rejection path always sets a real director id (FR-009/013a),
so `DecidedBy == null` on an `approved` row is otherwise impossible today — an unambiguous,
backward-compatible signal. A sentinel `Guid.Empty` would need a lookup exception everywhere
`DecidedBy` is joined against `TenantUser`; `null` needs none.

**Alternatives considered**: A new `DecidedBySystem bool` column — rejected as redundant state
(two columns could theoretically disagree) for no benefit over the already-nullable field.

## R2: Director "notification" for `informational` auto-approvals — no push channel exists

**Decision**: Surface auto-approved `informational` requests in the existing director "Verzoeken"
queue screen (`web/app/(app)/requests/page.tsx`, 013a) — extend `DayReservationsTable` to render a
distinct badge/label when `decidedBy` is `null` (system-decided) — rather than building a new
notification channel.

**Rationale**: `TenantUser` (the director's account) has no push-token field; no browser-push
infrastructure exists anywhere in the codebase; the only in-app notification centre
(`Notification` entity, `GET /api/parent/notifications`) is hard-scoped `ParentOnly` (feature 013).
The existing queue screen already has a status filter (`pending`/`all`/`approved`/...) — an
`approved` row with `decidedBy = null` is already fetchable via `?status=approved` or `?status=all`
today with zero backend changes; the only work is a UI label distinguishing "system" from a named
director in `DayReservationsTable`.

**Alternatives considered**: Building a first director-facing push/notification-centre system —
rejected as materially out of scope for a settings feature (a multi-week capability of its own);
would also need a `TenantUser.PushToken`-equivalent that browsers can't provide without a
browser-push (VAPID/service-worker) integration nothing in this codebase has ever set up.

## R3: Candidate-location resolution for mode/notice-hours enforcement (spec FR-017)

**Decision**: `ReservationPolicyResolver` (new, `ChildCare.Application/DayReservations/`) resolves
a `(Mode, NoticeHours)` pair per submission:
- `absence`: locations whose active contract covers `RequestedDate.DayOfWeek` (mirrors
  `ApproveDayReservationCommandHandler`'s existing resolution exactly); if none match, fall back to
  every location the child holds an active contract at.
- `extra`: every location the child holds an active contract at (no weekday match is possible by
  definition — an extra day is, by definition, not a contracted day).
- `exchange`: every location the child holds an active contract at (identical query
  `SubmitDayReservationCommandHandler` already runs for the closure-day check — reused, not
  duplicated).
- Zero candidate locations (child has no active contract, e.g. still on 012a's waiting list):
  enforcement is skipped entirely, request proceeds as `approval` — 013a's original, unconditional
  behavior for this case.
- Multiple candidates: `disabled` at any wins (request rejected); else `approval` at any wins
  (auto-approval only when every candidate is `informational`); `NoticeHours` = the maximum
  (strictest) among candidates.

**Rationale**: `DayReservation` deliberately has no `LocationId` column (013a research.md R7) — a
request isn't intrinsically tied to one location. The "most restrictive wins" rule mirrors the
precedent 013a itself already set for the closure-day check on `exchange` requests (loops every
candidate location, rejects if *any* has a closure) — applying the same directional bias
(restriction from any location wins) keeps this feature consistent with the one multi-location
precedent that already exists, rather than inventing a new, contradictory rule.

**Alternatives considered**: Adding a `LocationId` to the submission request (parent picks which
location an extra/absence day is for) — rejected for the same reason 013a's research.md R7 already
rejected it for attendance-writing purposes: it asks the parent a question with an obvious answer
95%+ of the time, and a wrong answer would misapply the wrong location's policy silently. The
resolver approach requires no new client-facing field.

## R4: Notice-hours boundary computation

**Decision**: Compare against the Brussels-midnight instant of `RequestedDate`
(`BelgianCalendarDay.UtcRangeFor(requestedDate).StartUtc`), not the raw `DateTime.UtcNow` vs.
`RequestedDate` as dates — i.e. `StartUtc - DateTime.UtcNow >= TimeSpan.FromHours(noticeHours)`.

**Rationale**: `RequestedDate` is a `DateOnly` with no time-of-day; "N hours' notice" only has a
well-defined meaning relative to a concrete instant, and every other day-boundary rule in this
codebase already anchors to `BelgianCalendarDay` (009's edit window, 013a's own past-date check) —
reusing it here avoids a second, silently different day-boundary convention.

## R5: Web `Location` screen does not exist yet — must be built to host the settings tab

**Decision**: `web/app/(app)/locations/page.tsx` is still 007a's `NotYetAvailable` placeholder —
004 (Locations) shipped backend-only. Per 007a's own shipped-notes ("Whichever feature builds one
of these next should replace that placeholder page's contents directly rather than routing around
it"), this feature builds a real, minimally-scoped Locations screen: a list (name, address,
capacity, active/deactivated status) plus a per-location settings panel with two tabs — "Algemeen"
(reusing 004's existing `PUT /api/locations/{id}` for name/address/phone/email/capacity/Opgroeien
fields) and "Reserveringsinstellingen" (this feature's new fields). Create/duplicate/deactivate UI
is explicitly NOT built here — those remain available via the API only, consistent with this
feature's own scope (reservation settings, not full location administration).

**Rationale**: 013f's core deliverable (a director-facing settings UI) has nowhere to live without
this. Per the standing rule established after 007a shipped: a referenced-but-unbuilt web screen is
a hard dependency for the next feature that needs it, not something to defer with another
backend-only workaround (the same mistake made twice, in 005 and 007, before 007a existed to fix
it).

**Alternatives considered**: Skip the web UI, ship backend-only with a note that a future feature
should build it — rejected; this is precisely the pattern the 007a note was written to prevent, and
013f without a working settings UI delivers no actual value to a director.

## R6: Parent-app entry-point visibility for a global, not per-child, quick-action bar

**Decision**: The parent-mobile home screen's three quick-action buttons (`absence`/`extra`/
`exchange`) are child-agnostic — the child is picked inside each form, not on the home screen
(existing 013a structure). This feature hides a home-screen button only when the type is
`disabled` for every one of the parent's linked children (nothing to do there is genuinely
nothing); each form itself performs the authoritative per-child check once a child is selected
(or immediately, for a single-child family — the common case), blocking submission with a clear
inline message if that specific child's resolved location(s) disable the type.

**Rationale**: 013a's existing architecture doesn't scope these buttons per child, and rebuilding
that structure is out of this feature's scope. This resolution satisfies FR-006's intent (no
confusing dead-end for the common single-child case) without a home-screen redesign.
