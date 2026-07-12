# Feature Specification: Reservation Settings

**Feature Branch**: `013f-reservation-settings`

**Created**: 2026-07-11

**Status**: Draft

**Input**: User description: "Add per-location configuration for the day reservation feature
(013a). Some KDVs do not allow parents to request day swaps or have strict policies on absences.
Directors need control over which request types are active and how they behave."

## Clarifications

### Session 2026-07-11

- Q: When an absence request auto-approves under `informational` mode, should it produce the same
  downstream effect as a director-approved absence (creating the attendance pre-registration per
  013a FR-010), or does auto-approval only set status with no attendance side effect? → A: Same
  downstream effect as an approval-mode decision — auto-approval must create the attendance
  pre-registration exactly as FR-010 describes for a director approval, otherwise an
  informational-mode absence would never actually register as an absence in attendance, defeating
  the purpose of the mode.
- Q: 013a's `DayReservation` deliberately has no `LocationId` (research.md R7) — a request isn't
  tied to one location, since a child can hold active contracts at multiple locations
  simultaneously (feature 007 split-location rule). Which location's mode/notice-hours settings
  govern a submission that has no single obvious location, especially `extra` requests, which by
  definition don't correspond to any contracted weekday at all? → A: Resolve the candidate
  location set from the child's active contracts (same weekday-match approach 013a's own approval
  handler already uses for `absence`; all active-contract locations for `extra`, since no weekday
  match is possible by definition). When more than one location applies, the most restrictive
  outcome governs: `disabled` at any candidate location blocks the request; otherwise `approval`
  at any candidate location forces `approval` (auto-approval only happens when every candidate
  location is `informational`); the highest `reservation_notice_hours` among candidates applies.
  This mirrors the precedent already set by 013a's own exchange closure-day check, which blocks
  the request if any of the child's active-contract locations has a closure on the target date. If
  no candidate location can be resolved at all (child has no active contract), mode/notice-hours
  enforcement is skipped and the request proceeds as `approval` — 013a's original, unconditional
  behavior for that edge case.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director configures request-type behaviour per location (Priority: P1)

A director opens a location's settings in the web admin and finds a "Reserveringsinstellingen"
tab. For each of the three day-reservation types (absence, extra day, swap), they pick one of
three modes: disabled (parents can't submit this type at all), informational (parent submits, it
is auto-approved immediately, director is just notified), or approval (director must review and
decide — today's 013a behaviour). They also set a minimum notice period, in hours, that applies
to all enabled types. Saving takes effect immediately for any new request.

**Why this priority**: Without this, 013a's fixed approval-queue model is the only behaviour
every KDV gets, regardless of whether it fits their actual policy. This is the entire point of
the feature and every other story depends on the setting existing.

**Independent Test**: Can be fully tested by a director changing a location's
`reservation_absences_mode` from `approval` to `disabled`, saving, and verifying the location
record reflects the new value with no effect on other locations.

**Acceptance Scenarios**:

1. **Given** a director is viewing a location's settings, **When** they open the
   Reserveringsinstellingen tab, **Then** they see the current mode for each of the three request
   types (defaulting to `approval` for absence/extra and `disabled` for swap on a location that
   has never set them) and the current minimum notice hours.
2. **Given** the director changes the absence mode to `informational` and saves, **When** the save
   completes, **Then** the location's `reservation_absences_mode` is persisted as `informational`
   and every other location's settings are unaffected.
3. **Given** a director sets minimum notice hours to `24`, **When** they save, **Then** the
   location's `reservation_notice_hours` is persisted as `24`.

---

### User Story 2 - Parent app respects the location's configured modes (Priority: P1)

A parent opens the day-reservation entry points in the parent app. For any request type set to
`disabled` at their child's location, the corresponding button is simply absent — no explanation
screen, no disabled-and-greyed button. For a request type set to `informational`, the parent
submits exactly as before, but the request is immediately marked approved with no waiting period,
and the director is notified rather than asked to decide. For `approval` (today's behaviour),
nothing changes.

**Why this priority**: This is the half of the feature that actually delivers value to parents
and directors day-to-day — a configured mode with no client-side effect would be invisible to the
people the setting exists for. Ships alongside Story 1 as the other required half of an MVP.

**Independent Test**: Can be fully tested by configuring one location's absence mode as
`disabled` and a second location's absence mode as `informational`, then verifying a parent at
the first location has no absence entry point while a parent at the second location can submit
one that resolves to `approved` immediately with a director notification, with no queue entry
requiring action.

**Acceptance Scenarios**:

1. **Given** a location has `reservation_swaps_mode = disabled`, **When** a parent linked to a
   child at that location opens the day-reservation entry points, **Then** no swap-request entry
   point is shown.
2. **Given** a location has `reservation_absences_mode = informational`, **When** a parent
   submits an absence request, **Then** the request is created with status `approved`,
   `decided_at` set to the submission time, and a system-attributed decision, with no manual
   director action required.
3. **Given** a location has `reservation_absences_mode = informational`, **When** the request is
   auto-approved, **Then** the director can see it in the same "Verzoeken" queue screen used for
   pending requests, distinguishable from items awaiting a decision so it doesn't read as
   something requiring action.
4. **Given** a location has `reservation_extras_mode = approval` (unchanged default), **When** a
   parent submits an extra-day request, **Then** behaviour is identical to feature 013a today —
   the request lands in the director's pending queue.

---

### User Story 3 - Disabled or under-notice submissions are rejected server-side (Priority: P1)

A parent's app is out of date, cached, or otherwise shows a request type the location has since
disabled — or a parent submits a request that doesn't meet the location's minimum notice period.
The API rejects the request with a clear reason rather than creating it, regardless of what the
client showed.

**Why this priority**: The setting is meaningless as a policy control if it can be bypassed by
calling the API directly or from a stale client — this is the enforcement half of Story 1/2 and
must ship in the same pass, not as a follow-up hardening task.

**Independent Test**: Can be fully tested by POSTing a day-reservation request for a `disabled`
type directly against the API and verifying a 403 response, and by POSTing a request dated
inside the configured notice window and verifying a validation rejection — both independent of
whether the parent app's UI would have allowed the attempt.

**Acceptance Scenarios**:

1. **Given** a location has `reservation_swaps_mode = disabled`, **When** a parent submits a
   swap request for a child at that location anyway, **Then** the API rejects it with a 403 and
   an i18n error key equivalent to "this request type is not available," and no reservation
   record is created.
2. **Given** a location has `reservation_notice_hours = 24`, **When** a parent submits a request
   for a date less than 24 hours away, **Then** the API rejects the submission with a validation
   error identifying the required notice period, and no reservation record is created.

---

### User Story 4 - Director is warned before a mode change strands pending requests (Priority: P2)

A director switches a request type from `approval` to `informational` or `disabled` while
requests of that type are still sitting in the pending queue. Because the new mode won't process
those old requests automatically, the director sees a warning before saving so they know to
resolve the existing queue manually.

**Why this priority**: A real but secondary safety net — the feature functions correctly without
it (pending requests simply stay pending, as already specified), but a director could otherwise
be surprised that changing a mode doesn't retroactively resolve open requests.

**Independent Test**: Can be fully tested by creating a pending request of a given type, then
attempting to change that type's mode away from `approval`, and verifying a warning is shown
before the save is confirmed, listing (or counting) the affected pending requests.

**Acceptance Scenarios**:

1. **Given** a location has one or more `pending` requests of type `absence`, **When** a director
   changes `reservation_absences_mode` away from `approval` and attempts to save, **Then** they
   see a warning stating how many pending requests of that type exist and that they will not be
   automatically affected, before the change is committed.
2. **Given** a location has zero pending requests of a given type, **When** a director changes
   that type's mode, **Then** no warning is shown and the save proceeds directly.
3. **Given** the director sees the warning and confirms anyway, **When** they confirm, **Then**
   the mode change is saved and the existing pending requests remain `pending`, unaffected.

---

### Edge Cases

- A parent submits a request for a type that was `approval` when they opened the app but has
  since been switched to `disabled` before they tap submit: rejected at the API per Story 3,
  independent of what the client rendered.
- `reservation_notice_hours = 0` (the default for absence/extra): no notice-period restriction —
  same-day submissions remain allowed, matching 013a's existing behaviour.
- A location has never had its reservation settings explicitly saved: the three modes and notice
  hours behave per their column defaults (`approval`/`approval`/`disabled`/`0`) — a location
  created before this feature shipped is not left in an undefined state.
- Switching a mode from `disabled` back to `approval` or `informational`: no warning needed (no
  pending requests could exist for a type that was disabled), and the request type reappears in
  the parent app immediately.
- A director sets `reservation_notice_hours` to a very large value (e.g. 8760, one year): treated
  as a valid, if impractical, configuration — not specially validated beyond a sane upper bound
  (see FR-011).
- An `informational`-mode absence request's auto-approval still needs to succeed the same way an
  approval-mode approval would — e.g. it still cannot create an attendance pre-registration for a
  date that's a published closure day (mirrors 013a FR-011). Such a submission is rejected at
  submission time with the same closure-day validation, rather than silently auto-approved into an
  invalid state.
- A child holds active contracts at two locations simultaneously (feature 007 split-location
  enrolment) and one location disables a request type while the other allows it: the request is
  rejected — the most restrictive candidate location's setting governs (FR-017).
- A child has no active contract at all (e.g. still on the waiting list per 012a) when a parent
  submits an absence or extra-day request: no location can be resolved, so mode/notice-hours
  enforcement does not apply and the request proceeds to the `approval` queue — 013a's original
  behavior for this case, unchanged by this feature (FR-017).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST store, per location, three independent mode settings — one each for
  `absence`, `extra`, and `exchange` (swap) day-reservation types — each set to exactly one of
  `disabled`, `informational`, or `approval`.
- **FR-002**: The system MUST default `reservation_absences_mode` and `reservation_extras_mode` to
  `approval`, and `reservation_swaps_mode` to `disabled`, for any location that has not explicitly
  set them (including every location that existed before this feature shipped).
- **FR-003**: The system MUST store, per location, a minimum notice period in whole hours
  (`reservation_notice_hours`), defaulting to `0` (no restriction).
- **FR-004**: The system MUST allow a director to view and update all three mode settings and the
  notice-hours setting for any location in their organisation, taking effect immediately for
  requests submitted after the change.
- **FR-005**: The system MUST NOT retroactively alter any request already in `pending`,
  `approved`, `rejected`, or `cancelled` status when a location's mode settings change.
- **FR-006**: The parent app MUST NOT present a submission entry point for any day-reservation
  type whose mode is `disabled` at the child's location.
- **FR-007**: The system MUST reject, with a 403 response and an i18n error key, any day
  reservation submission for a type whose mode is `disabled` at the relevant location —
  independent of what the submitting client displayed.
- **FR-008**: When a request's type is in `informational` mode at submission time, the system
  MUST create the request already in `approved` status, with `decided_at` set to the submission
  time and the decision attributed to the system (not a director), and MUST apply the identical
  downstream effects a director's approval would trigger — for `absence` requests, this includes
  creating the attendance pre-registration per 013a FR-010; for `extra`/`exchange` requests, this
  means no attendance pre-registration is created, per 013a FR-012.
- **FR-009**: When a request's type is in `informational` mode, the system MUST still apply every
  other submission-time validation that mode would otherwise be subject to (e.g. 013a's
  closure-day check for absences, contracted-day check for exchanges) — an informational
  submission that would be invalid under `approval` mode MUST be rejected at submission, not
  auto-approved.
- **FR-010**: When a request auto-approves under `informational` mode, the system MUST make it
  visible to directors in the same queue screen used for pending requests, clearly distinguished
  from items awaiting a decision (e.g. a separate "auto-approved" section or status badge) so it
  reads as informational rather than actionable.
- **FR-011**: The system MUST validate `reservation_notice_hours` as a non-negative integer within
  a sane bound (0–8760 hours / one year) when a director sets it.
- **FR-012**: The system MUST reject a parent-app day reservation submission whose requested date
  falls within the location's configured `reservation_notice_hours` window from the current time,
  with a validation error identifying the required notice period.
- **FR-013**: The notice-hours check (FR-012) MUST apply to every day-reservation submission
  through the one submission path that exists today (the parent-app-facing endpoint). No
  director/caregiver-initiated submission-on-behalf-of-a-parent path exists in this codebase
  (013a shipped parent-only submission); should a future feature add one, that feature MUST
  decide separately whether FR-012 applies to it.
- **FR-014**: Before saving a mode change away from `approval` for a request type that currently
  has one or more `pending` requests at that location, the system MUST warn the director, stating
  how many pending requests of that type exist, before the change is committed. The director MUST
  be able to confirm and proceed, or cancel the change.
- **FR-015**: When mode is `approval` (unchanged from 013a), submission and decision behaviour
  MUST be identical to feature 013a's existing behaviour.
- **FR-016**: All user-facing strings (parent app and director web) introduced by this feature
  MUST use i18n keys covering NL/FR/EN, including the per-mode explanatory text shown to
  directors and the disabled-type rejection message shown to parents.
- **FR-018**: Two directors saving settings for the same location concurrently MUST NOT corrupt
  the stored state (each save fully overwrites all four fields as a single unit, never a partial
  field-by-field merge) — the system MAY apply simple last-write-wins semantics; no optimistic
  concurrency conflict needs to be surfaced to the director, consistent with this being a
  low-frequency, single-organisation administrative action with no cross-director race precedent
  elsewhere in this codebase's director-settings screens (Staff, Devices).
- **FR-017**: Because a day reservation is not tied to a single location (013a research.md R7),
  the system MUST resolve the set of candidate locations for mode/notice-hours enforcement from
  the child's active contracts: for `absence`, the location whose contract covers the requested
  date's weekday (same resolution 013a's approval flow already uses), falling back to every
  active-contract location when no weekday match exists; for `extra`, every active-contract
  location (no weekday match is possible by definition); for `exchange`, every active-contract
  location (matching the existing closure-day check's candidate set). When more than one
  candidate location applies, the system MUST apply the most restrictive outcome: `disabled` at
  any candidate location rejects the request; otherwise, the request is treated as `approval`
  unless every candidate location's mode for that type is `informational`; the highest
  `reservation_notice_hours` among candidate locations governs the notice check. When zero
  candidate locations can be resolved (child has no active contract), mode/notice-hours
  enforcement MUST be skipped and the request MUST proceed as `approval`.

### Key Entities

- **Location Reservation Settings**: Four fields added to the existing Location record —
  `reservation_absences_mode`, `reservation_extras_mode`, `reservation_swaps_mode` (each one of
  `disabled`/`informational`/`approval`), and `reservation_notice_hours` (non-negative integer).
  Not a new entity; extends feature 004's `Location`, following the precedent already set by that
  feature's own nullable Opgroeien-reporting fields.
- **Day Reservation** (existing, feature 013a): unchanged in shape. This feature affects how a
  request reaches `approved` status (system-decided vs. director-decided) and whether submission
  is permitted at all, but does not add fields to the entity itself.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can change a location's reservation policy (any of the three modes, or
  the notice period) and have it apply to the very next submission attempt, with no deployment or
  delay.
- **SC-002**: 100% of day-reservation submissions for a `disabled` type are rejected server-side,
  even when attempted directly against the API bypassing the parent app's UI.
- **SC-003**: 100% of `informational`-mode submissions that pass the same validation an
  `approval`-mode submission would have passed result in an immediately `approved` status with no
  director decision step.
- **SC-004**: 100% of parent-app submissions dated inside a location's configured notice window
  are rejected at submission with a clear reason.
- **SC-005**: Zero pending requests are silently altered by a mode change — every pre-existing
  pending request remains exactly `pending` until a director acts on it directly.

## Assumptions

- "Location settings screen (004)" refers to feature 004's existing per-location settings screen
  in the web admin (director web), which this feature adds a new tab to — no new top-level
  navigation entry is introduced.
- **Premise correction from the original brief**: the brief describes the director being notified
  of an `informational` auto-approval via "push notification." No such channel exists for
  directors today — `TenantUser` (the director's account) has no push token field, no browser-push
  infrastructure exists, and the codebase's only notification centre (`Notification` entity,
  `/api/parent/notifications`) is `ParentOnly` (feature 013). Building a first-ever director push
  channel is out of scope for a settings feature. This spec instead surfaces auto-approved
  informational requests in the existing director "Verzoeken" queue screen (013a) — the screen
  directors already check daily for pending requests — satisfying the underlying need (the
  director finds out) without inventing new notification infrastructure.
- "System" as the `decided_by` value for auto-approved requests follows the same
  system-attributed-actor precedent already used elsewhere in this codebase (e.g. 009a's
  automated backfill) rather than inventing a new actor concept.
- Per feature 013a's own assumption, "their own children" parent-to-child linkage is unchanged by
  this feature.
- **Premise correction from the original brief**: the brief's edge case describes a "caregiver
  (via the director's web admin) submits a retroactive absence" to illustrate why the notice-hours
  check shouldn't apply to staff-initiated submissions. No such submission path exists — feature
  013a's `POST /api/day-reservations` is `ParentOnly`; there is no director- or caregiver-facing
  way to submit a day reservation on a family's behalf anywhere in this codebase. Building that
  capability is a distinct feature in its own right, not something a settings feature should
  introduce as a side effect. FR-013 reflects this: the notice-hours check simply applies to the
  one submission path that exists.
- The notice-hours bound of 8760 (one year) is a defensive input-validation ceiling, not a
  product requirement from the original brief — chosen so a director typo (e.g. an extra zero)
  fails validation instead of silently blocking all future submissions indefinitely.
- Per the original feature brief, per-child overrides and time-window restrictions (e.g. "only
  before 8am") remain out of scope — all settings stay per-location and notice-hours-only.
