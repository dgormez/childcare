# Feature Specification: Day Reservations (Parent Requests + Director Approval Queue)

**Feature Branch**: `013a-day-reservations`

**Created**: 2026-07-11

**Status**: Draft

**Input**: User description: "Allow parents to submit day requests (absence, extra day, exchange day) through the parent app, and give directors a single approval queue."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parent reports a sick day (Priority: P1)

A parent's child is sick and won't attend today or on an upcoming day. The parent opens the
parent app, taps "Mijn kind is ziek," picks the date and optionally types a reason, and submits.
The request appears as pending. When the director later approves it (marking it justified or
not), the parent gets a push notification and the day is recorded as a pre-registered absence.

**Why this priority**: This is the single most common reason parents contact a KDV directly today
(illness). It's the highest-frequency, highest-friction case and delivers value standalone.

**Independent Test**: Can be fully tested by a parent submitting an absence request for their own
child and a director approving it — verify the request appears in the queue, approval sets
`absence_justified`, and an attendance pre-registration exists for that child/date.

**Acceptance Scenarios**:

1. **Given** a parent is logged in with a linked child, **When** they submit an absence request
   for tomorrow with a reason, **Then** a `pending` day reservation is created and visible to the
   director's queue.
2. **Given** a pending absence request, **When** the director approves it and sets
   `absence_justified = true`, **Then** the request status becomes `approved`, the child has a
   pre-registered absence for that date, and the parent receives a push notification.
3. **Given** a pending absence request, **When** the director rejects it with a note, **Then** the
   status becomes `rejected`, no attendance record is created, and the parent receives a push
   notification including the note.
4. **Given** a parent attempts to submit an absence request for a date more than 1 day in the
   past, **When** they submit, **Then** the request is rejected with a clear validation message.

---

### User Story 2 - Director clears the request queue (Priority: P1)

A director opens the web admin and sees a single "Verzoeken" list of every pending request across
all children at their organisation, newest first. They tap a request, see the child, requested
date, type, and reason, and approve or reject it in one action — without navigating elsewhere for
context.

**Why this priority**: Without a usable queue, the parent-facing submission flow has no
operational payoff — this is the other half of the same workflow and must ship together for the
feature to deliver value at all.

**Independent Test**: Can be fully tested by creating several pending requests (across types) and
verifying the director sees them newest-first with full context, and that approve/reject actions
resolve them without a page reload losing context.

**Acceptance Scenarios**:

1. **Given** multiple pending requests exist, **When** the director opens the Verzoeken queue,
   **Then** they see all pending requests across all children, ordered newest first.
2. **Given** no pending requests exist, **When** the director opens the queue, **Then** they see a
   clear empty state.
3. **Given** an extra-day request for a date at or near location capacity, **When** the director
   views it, **Then** they see a capacity warning before deciding.

---

### User Story 3 - Parent requests an extra day (Priority: P2)

A parent wants to bring their child on a day not covered by the contract (e.g. an extra Friday).
They tap "Extra dag aanvragen," pick a date, optionally add a reason, and submit. The director
reviews and approves or rejects, accounting for capacity.

**Why this priority**: Common and valuable, but lower frequency than illness reporting (P1) and
shares the same submission/approval mechanics already delivered by User Story 1/2.

**Independent Test**: Can be fully tested by a parent submitting an extra-day request for a date
with available capacity and a director approving it — verify the request resolves independently
of the absence flow.

**Acceptance Scenarios**:

1. **Given** a parent is logged in, **When** they submit an extra-day request for a future date,
   **Then** a `pending` day reservation of type `extra` is created.
2. **Given** an approved extra-day request, **When** the approval is processed, **Then** the
   child's care schedule reflects the extra day for that date (no attendance pre-registration is
   created — extra days are additions to the normal contract, not absences).

---

### User Story 4 - Parent requests a day exchange (Priority: P2)

A parent needs to swap a contracted day for a different day in the same or a later week (e.g.
trade Monday for Thursday because of a public holiday). They tap "Dagwissel aanvragen," pick the
contracted day being given up and the new date, optionally add a reason, and submit. The system
validates the new date isn't a closure day before accepting the request.

**Why this priority**: Same tier of value as extra-day requests; a distinct entry point and
validation path (must reference an actual contracted day) justify its own story, but it reuses the
same submission/approval mechanics.

**Independent Test**: Can be fully tested by a parent submitting an exchange request that swaps a
real contracted day for a valid future date, and verifying rejection when the target date is a
closure day or the source date isn't actually contracted.

**Acceptance Scenarios**:

1. **Given** a child has a contracted day on Monday, **When** the parent requests to exchange it
   for a Thursday that is not a closure day, **Then** a `pending` day reservation of type
   `exchange` is created with both dates recorded.
2. **Given** a parent attempts to exchange a day that is not one of the child's contracted days,
   **When** they submit, **Then** the request is rejected at submission with a clear validation
   message.
3. **Given** the requested exchange target date is a published closure day, **When** the parent
   submits, **Then** the request is rejected at submission with a clear validation message.

---

### User Story 5 - Parent withdraws a pending request (Priority: P3)

A parent realizes their child isn't sick after all, or their plans changed, before the director
has acted on the request. They cancel it from the app.

**Why this priority**: Convenience/cleanup on top of the core flow — valuable but not required for
the feature's core value (director still sees stale requests and can reject them manually if this
didn't exist).

**Independent Test**: Can be fully tested by a parent cancelling their own pending request and
verifying it disappears from the director's active queue and cannot be approved/rejected
afterward.

**Acceptance Scenarios**:

1. **Given** a parent has a pending request, **When** they cancel it, **Then** its status becomes
   `cancelled` and it no longer appears in the director's active queue.
2. **Given** a request has already been approved or rejected, **When** the parent tries to cancel
   it, **Then** the action is rejected — only `pending` requests can be cancelled.

---

### Edge Cases

- A parent submits an extra-day request for a date at or over location capacity: the system does
  not hard-block submission; the director sees a capacity warning at decision time and may reject
  with a note (per Out of Scope: no automatic slot-availability enforcement).
- An exchange request's target date is a published closure day: rejected at submission.
- An exchange request's source date is not an actual contracted day for that child: rejected at
  submission.
- A parent tries to submit or cancel a request for a child they are not linked to: rejected as
  unauthorized.
- A parent submits an absence request for a date more than 1 day in the past: rejected at
  submission with a message directing them to contact the director directly.
- Two directors act on the same pending request concurrently: the second action fails cleanly
  (request is no longer `pending`) rather than double-processing.
- A director rejects a request with a note: the parent's push notification includes that note.
- A child has no linked contract yet when a parent submits an exchange request (no contracted
  days to validate against): rejected at submission — nothing to exchange.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a parent to submit a day reservation request of type
  `absence`, `extra`, or `exchange` for a child they are linked to, with a requested date and an
  optional free-text reason.
- **FR-002**: The system MUST reject an absence-request submission dated more than 1 day in the
  past.
- **FR-003**: For `exchange` requests, the system MUST require and validate that the day being
  given up (`exchange_for_date`) is an actual contracted day for that child, rejecting submission
  otherwise.
- **FR-004**: For `exchange` requests, the system MUST reject submission if the requested target
  date is a published closure day for the child's location.
- **FR-005**: The system MUST prevent a parent from submitting or cancelling a request for any
  child they are not linked to.
- **FR-006**: The system MUST present directors with a single queue of all pending day
  reservation requests across all children at their organisation, ordered newest-created first.
- **FR-007**: The system MUST allow a director to approve or reject a pending request in one
  action, from the queue, without requiring navigation to another screen for context (child name,
  request type, requested date(s), and reason must be visible in the queue view).
- **FR-008**: When approving an `absence` request, the system MUST require the director to set
  whether the absence is justified (`absence_justified`), and MUST record who decided and when.
- **FR-009**: When rejecting a request, the system MUST allow the director to attach an optional
  note, and MUST record who decided and when.
- **FR-010**: When an `absence` request is approved, the system MUST create a pre-registered
  absence in attendance for that child, date, and location, carrying the `absence_justified` flag
  set at approval.
- **FR-011**: When an `absence` approval's target date is already a published closure day for
  that location, the system MUST reject the approval action itself (the underlying attendance
  pre-registration cannot be created for a closure date) and surface this to the director.
- **FR-012**: When an `extra` or `exchange` request is approved, the system MUST NOT create an
  attendance pre-registration — these represent schedule additions/swaps only, not absences.
- **FR-013**: The system MUST notify the parent via push notification when a request's status
  changes to `approved` or `rejected`; a rejection notification MUST include the director's note
  when one was provided.
- **FR-014**: The system MUST allow a parent to cancel their own request only while it is in
  `pending` status; the resulting status MUST be `cancelled`.
- **FR-015**: The system MUST prevent any status transition on a request that is not currently
  `pending` (e.g. cannot approve/reject/cancel an already-decided or already-cancelled request).
- **FR-016**: The system MUST prevent two concurrent decisions on the same request from both
  succeeding — only the first decision is applied; the second MUST fail cleanly.
- **FR-017**: All user-facing strings (parent app and director web) MUST use i18n keys covering
  NL/FR/EN.
- **FR-018**: The parent app MUST expose three distinct entry points corresponding to the three
  request types: report a sick day (`absence`), request an extra day (`extra`), request a day
  exchange (`exchange`).
- **FR-019**: A parent MUST be able to view the status and history of their own submitted
  requests (pending, approved, rejected, cancelled).

### Key Entities

- **Day Reservation**: A parent-submitted request affecting a single child's care schedule for a
  specific date. Has a type (`absence`, `extra`, `exchange`), a requested date, an optional
  exchange-source date (exchange only), an optional parent-supplied reason, a status
  (`pending`/`approved`/`rejected`/`cancelled`), an approval-time justified flag (absence only),
  who submitted it and who decided it, an optional director note, and timestamps. Belongs to
  exactly one child.
- **Attendance Pre-registration**: The attendance-side record created when an `absence` request is
  approved — an existing concept (Attendance & Presence workflow) this feature writes into, not a
  new entity this feature owns.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A parent can submit any of the three request types in under 30 seconds from opening
  the entry point to seeing a pending confirmation.
- **SC-002**: A director can approve or reject a request from the queue in a single interaction,
  without navigating to a separate screen to find child or contract context.
- **SC-003**: 100% of approved or rejected requests result in a push notification delivery attempt
  to the requesting parent.
- **SC-004**: 100% of exchange requests whose target date is a closure day, or whose source date
  is not a contracted day, are rejected at submission time (never reach the director queue in an
  invalid state).
- **SC-005**: Zero instances of two directors both successfully deciding the same request.

## Assumptions

- "Their own children" is resolved the same way feature 013 (Parent Communication) resolves
  parent-to-child linkage — via the parent's linked `Contact` record(s) — no new linkage mechanism
  is introduced here.
- Push notification delivery reuses the existing Expo push infrastructure (feature 009/013); no
  new notification channel is introduced.
- "Extra day" and "exchange" approvals updating "the effective care schedule for invoice purposes"
  (per the original feature brief) is scoped to recording the approved day reservation itself as
  the source of truth; feature 014 (invoicing, not yet built) is expected to read approved
  `extra`/`exchange` day reservations when it ships, rather than this feature reaching into
  invoicing logic that doesn't exist yet.
- Automatic slot-availability/capacity enforcement at submission time is explicitly out of scope
  (per the original feature brief) — the director sees a capacity warning at decision time only,
  sourced the same way feature 012a's occupancy view computes capacity (active contracts against
  location `MaxCapacity`).
- A director's approval of an `absence` request whose date has meanwhile become a closure day is a
  genuine failure mode (FR-011) since the same closure-day rule blocks attendance pre-registration
  everywhere else in the system (see `MarkAbsentCommand`) — this is treated as a rejected approval
  action with a clear message, not a silent no-op.
- Cross-tenant/cross-parent authorization tests follow the existing `ParentOnly`/`DirectorOnly`
  policy pattern already established in features 003/013 — no new authorization mechanism is
  introduced.
- FR-002's "more than 1 day in the past" is evaluated against the same Europe/Brussels-anchored
  calendar day (`BelgianCalendarDay`, feature 009) every other date-boundary rule in this system
  already uses — not the server's raw UTC date — so a request submitted late at night doesn't get
  a different past/future answer than the same wall-clock moment would give a caregiver or
  director looking at the same date.
