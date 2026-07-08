# Feature Specification: Caregiver App Kiosk Mode (Room Shift Register)

**Feature Branch**: `008a-caregiver-kiosk-mode`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Replace the personal-login model from feature 008 with a room-based shift register. The tablet is permanently authenticated as a room device. Caregivers check in and out with a PIN to record their physical presence. Event logging requires no individual auth — the device token is sufficient."

## Clarifications

### Session 2026-07-08

- Q: Should the sensitive-action (administered_by) PIN confirmation share the same per-PIN rate-limit/lockout as check-in/check-out, or does it have separate/no rate limiting? → A: One shared lockout counter per PIN, incremented by a failed attempt on any surface that checks that PIN — 5 failures anywhere locks that PIN for 10 minutes everywhere.

## Product Context

### Feature Type

Mixed (User-facing UI + API-backend capability + Data-model change).

### Primary Consumer

Caregiver (primary, on the tablet) and Director (room setup, PIN management, revocation — on the web admin).

### Workflow Boundary

**Classroom Operations** (`workflows.md`'s "Staffing" line item — this feature is its first real
content, so `Workflows/classroom-operations.md` is created as part of this spec).

- **Actors**: Caregiver, Director, System (auto-checkout, token rotation, revocation enforcement).
- **Actions**: Director pairs a tablet to a room; director sets/resets caregiver PINs; caregiver
  checks in/out via PIN; caregiver logs a routine action (no PIN); caregiver confirms a
  medical-action administrator via PIN; director revokes a lost/stolen tablet.
- **Data Flow**: A device token (tablet identity) and a room-shift log (caregiver presence)
  are both read on every authenticated write, together determining who gets recorded as having
  performed an action.
- **Outputs**: An accurate, auditable record of which caregiver(s) were present in a room at
  any given moment, consumable by any feature that logs a caregiver-attributed action (feature
  009 child events, feature 010 attendance).
- **Cross-platform Impact**: Caregiver tablet (primary — replaces feature 008's login screen as
  the daily entry point), Director web (room/device management, PIN management, revocation),
  backend (new `room_shifts`/device-token infrastructure). Parent mobile: unaffected.

### User Impact

This enables a caregiver to record their presence in a childcare room in under five seconds
with a PIN, instead of a full email/password login every shift, resulting in an app that
matches how Belgian KDVs actually staff a room (multiple caregivers, one shared tablet) rather
than forcing an artificial single-user session model.

### UX Requirements

- **Persona**: A caregiver mid-shift, hands often occupied, needing to identify themselves in
  seconds without breaking attention away from children for long.
- **Platform**: Caregiver tablet, landscape, kiosk-locked (see `platform-rules.md`).
- **User job**: "Let me and my coworker both be recorded as present, without either of us
  fighting over who's 'logged in' on the one tablet in the room."
- **Success criteria**: Check-in/out in under 5 seconds; two caregivers simultaneously present
  is the normal case, never a conflict.
- **Main flow**: Tablet shows room home screen (idle) — every caregiver eligible at this
  location, as a photo card — → caregiver taps their own card → a PIN keypad overlay appears
  addressed to them by name → tablet confirms and the card switches to its checked-in state.
  Select-then-PIN, not PIN-only, per the industry-standard pattern for small, known staff pools
  (confirmed against Procare and KinderSign) — the tap already identifies the caregiver, so the
  PIN step exists purely to confirm identity, not to search for it.
- **Loading state**: PIN submission shows an inline spinner on the keypad overlay's confirm
  affordance; no full-screen loading state for an action this frequent.
- **Empty state**: At the start of the day every card shows as not-checked-in (no separate empty
  state needed — the roster itself is never empty, only everyone's checked-in status is).
- **Error state**: Incorrect PIN — shake animation + a clear, non-blocking message. Locked-out
  PIN — a distinct message naming the cooldown, not a generic "incorrect PIN."
  Revoked/expired device — a full-screen "reactivation required" state, not a silent failure.
- **Accessibility**: PIN keypad touch targets ≥ 64pt (highest-frequency single interaction on
  this surface, per `platform-rules.md`).
- **Offline behavior**: Check-in/out queues through feature 008's existing offline queue
  (`entity_type = 'room_shift'`) and replays on reconnect; routine event logging is unaffected
  by connectivity since it was already offline-capable. Medical-action PIN confirmation is
  skipped when offline (`administered_by = null`, completed retroactively by a director).

### Technical Requirements

- **API impact**: New device-token issuance/rotation/revocation endpoints; a new roster
  endpoint (every location-eligible caregiver, photo + checked-in state, powering the room home
  screen's photo cards); new check-in/check-out endpoints, both taking `{ staff_id, pin }`
  (select-then-PIN, not PIN-only — the client identifies the caregiver, the server verifies);
  a new administrator-confirmation endpoint, same `{ staff_id, pin } | { skip: true }` shape;
  existing caregiver-authenticated write endpoints gain device-token as an accepted (additional)
  credential type.
- **Data-model impact**: New `room_shifts` table (tenant schema); new `pin_hash` field on the
  existing `StaffProfile` (feature 005); new device-pairing record (tenant schema) tracking
  issued/rotated/revoked device tokens per tablet.
- **Security considerations**: Device token is a second, distinct signing/validation path
  alongside the existing user-session JWT (feature 003) — see research.md for how it composes
  with `TenantMiddleware`/`ICurrentTenantService` without weakening tenant isolation. PINs are
  bcrypt-hashed, rate-limited per PIN, never logged or transmitted in recoverable form.
- **Performance considerations**: Device-token validation happens on every authenticated
  request from a tablet — must be a cheap check (signature + revocation-list lookup), not a
  chain of additional queries, to avoid adding latency to already-latency-sensitive offline-sync
  replay bursts.
- **Testing requirements**: Real TestContainers-backed integration tests for device-token
  issuance/validation/rotation/revocation and the check-in/check-out/rate-limit/attribution
  logic (constitution Principle V — no faking the database). Mobile: real (test-mode) SQLite
  for the offline-queue integration, consistent with feature 008's own test approach.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director pairs a tablet to a room (Priority: P1)

A director opens the caregiver app on a new tablet, signs in with their own email and
password, and assigns the tablet to a specific location and group. The tablet then locks into
room mode: the email/password screen is no longer reachable by ordinary caregivers, and the
tablet is now permanently identified to the backend as belonging to that room.

**Why this priority**: Nothing else in this feature works without a paired tablet — it's the
floor every other story is built on.

**Independent Test**: Can be fully tested by signing in as a director on a fresh install,
completing room setup, force-quitting and reopening the app, and confirming it reopens
directly into room mode for the same location/group with no re-authentication required.

**Acceptance Scenarios**:

1. **Given** a brand-new tablet install, **When** a director signs in and selects a location
   and group, **Then** the backend issues a device token scoped to that location/group and the
   tablet stores it securely and enters room mode.
2. **Given** a tablet already in room mode, **When** the app restarts or the OS backgrounds and
   resumes it, **Then** the tablet remains in room mode for the same location/group with no
   re-authentication.
3. **Given** a tablet in room mode, **When** a director enters their director-override PIN,
   **Then** the tablet exits room mode back to the email/password screen, ready to be re-paired.

---

### User Story 2 - Director manages caregiver PINs (Priority: P1)

A director opens the web admin's staff screen and sets or resets a 4-digit PIN for a
caregiver, so that caregiver can check in on a room tablet. PINs are unique within a location
and are managed independently of the caregiver's own account password.

**Why this priority**: Check-in cannot be demonstrated or tested until at least one caregiver
has a PIN — this is a hard prerequisite for User Story 3, and is itself fully testable without
any tablet involved.

**Independent Test**: Can be fully tested via the web admin alone — set a PIN for a caregiver,
confirm it's stored hashed (never retrievable in plaintext), attempt to reuse the same PIN for
a second caregiver at the same location and confirm it's rejected.

**Acceptance Scenarios**:

1. **Given** a caregiver with no PIN set, **When** a director sets one from the staff screen,
   **Then** it's stored as a salted hash and the caregiver can use it to check in.
2. **Given** a caregiver already has a PIN, **When** a director resets it, **Then** the old PIN
   no longer works, the new one does, and the caregiver's account password is unaffected.
3. **Given** two caregivers at the same location, **When** a director tries to set the same PIN
   for both, **Then** the second assignment is rejected with a clear error.

---

### User Story 3 - Caregiver checks in and out via PIN (Priority: P1)

A caregiver walks up to the paired room tablet, which shows every caregiver eligible at this
location as a photo card. They tap their own card, a PIN keypad overlay appears addressed to
them by name, and entering their PIN immediately records them as present — no session opens, no
screen is "claimed." A second caregiver can do the same moments later; both cards show as
checked in simultaneously. Either can check out independently the same way — tap their own
(now checked-in) card, confirm their PIN.

Select-then-PIN, not PIN-only: with 2 caregivers per room (BKR norm), the caregiver identifies
themselves by tapping their own photo first, and the app sends that identity (`staff_id`)
alongside the PIN — the server verifies the PIN against that *specific* caregiver's record
rather than searching for whose PIN it might be. This is the industry-standard pattern for
small, known staff pools (confirmed against Procare and KinderSign); PIN-only entry is better
suited to large, anonymous pools like a parent-facing kiosk, which this isn't.

**Why this priority**: This is the core value of the shift-register model — accurately
reflecting who is actually in the room, including the normal case of two caregivers at once.

**Independent Test**: Can be fully tested by tapping caregiver A's card and entering their PIN,
confirming their card switches to checked-in with a check-in time, tapping caregiver B's card
and checking in without checking A out, confirming both show checked-in simultaneously, then
checking each out independently the same way.

**Acceptance Scenarios**:

1. **Given** a caregiver's card is not checked in, **When** they tap their card and enter their
   correct PIN, **Then** a check-in is recorded and their card switches to the checked-in state.
2. **Given** a caregiver's card is already checked in, **When** they tap their card and enter
   their correct PIN, **Then** a check-out is recorded and their card returns to the
   not-checked-in state.
3. **Given** caregiver A's card is checked in, **When** caregiver B taps their own (different)
   card and checks in without A checking out, **Then** both cards show as checked in at once.
4. **Given** an incorrect PIN is entered for the tapped caregiver, **When** it's submitted,
   **Then** the tablet shows a clear rejection with no state change, and 5 incorrect attempts
   for that caregiver within 2 minutes locks their check-in/check-out ability for 10 minutes
   (every other caregiver's card remains fully usable).

---

### User Story 4 - Authenticated actions need no individual login (Priority: P1)

While the tablet is paired and at least reachable, any caregiver can perform a routine
authenticated write action (this feature proves the mechanism via a generic authenticated
action, since no real domain event exists yet — feature 009 is the first real consumer)
without any additional login prompt. The system records which checked-in caregiver(s), if any,
were present when the action happened.

**Why this priority**: This is the entire point of the two-layer model — without it, every
future caregiver feature would need to reinvent its own auth story. Proving the contract now,
generically, is what lets feature 009 build directly on top of it.

**Independent Test**: Using a synthetic authenticated test action (mirroring feature 008's
`_test_entity` pattern for its sync engine), submit the action with zero caregivers checked in,
with exactly one checked in, and with two checked in — and confirm the recorded attribution
matches in each case, with the action never blocked by attribution state.

**Acceptance Scenarios**:

1. **Given** a valid, non-revoked device token and nobody checked in, **When** an authenticated
   write action is submitted, **Then** it succeeds and is recorded with no caregiver attributed.
2. **Given** exactly one caregiver checked in, **When** an authenticated write action is
   submitted, **Then** it's attributed to that caregiver alone.
3. **Given** two caregivers checked in, **When** an authenticated write action is submitted,
   **Then** it's attributed to both of them.
4. **Given** no device token (or a revoked/expired one) on the request, **When** any write
   action is attempted, **Then** it's rejected regardless of shift-log state.

---

### User Story 5 - Medical-action administrator confirmation (Priority: P2)

For a defined subset of sensitive actions (this feature proves the mechanism via a synthetic
confirmation action; feature 009's medication/temperature events are the first real
consumers), the caregiver is prompted to confirm who administered it before submitting —
distinct from, and in addition to, the shift check-in PIN. This uses the same select-then-PIN
pattern as check-in/out (User Story 3), narrowed to only the caregivers currently checked in
(typically one or two, per BKR): the caregiver taps their own card from that short list, then
confirms with their PIN. The caregiver may skip this step entirely; the action still completes.

**Why this priority**: Important for the medical-accountability use case, but not required for
the tablet to be usable day one — routine actions (User Story 4) work without it.

**Independent Test**: Using a synthetic confirm-administrator action, submit it with a
checked-in caregiver's `staff_id` and correct PIN and confirm the administrator field is set to
that caregiver; submit again with "skip" and confirm the field is left unset with the action
still recorded.

**Acceptance Scenarios**:

1. **Given** a sensitive action pending submission, **When** the caregiver taps their own card
   from the checked-in roster and enters a valid PIN, **Then** that caregiver's identity is
   recorded in the dedicated administrator field, separate from the general attribution field.
2. **Given** the same prompt, **When** the caregiver taps "Skip," **Then** the action still
   completes, the administrator field is left unset, and a director can fill it in later.
3. **Given** the tablet is offline, **When** a sensitive action is submitted, **Then** the
   confirmation step is skipped automatically (administrator field left unset) rather than
   blocking the action.
4. **Given** a `staff_id` that is valid but not currently checked in, **When** it's submitted
   with any PIN (correct or not) at this confirmation step, **Then** the request is rejected —
   only a currently-checked-in caregiver can be named as administrator (FR-017), and the
   checked-in-only roster this step's UI presents means this should not normally be reachable
   through the app itself, only via a direct API call.

---

### User Story 6 - Device token rotates silently (Priority: P2)

As a paired tablet's device token approaches expiry, the backend transparently issues a
replacement during the tablet's normal traffic, with no caregiver-visible interruption and
without breaking a burst of offline-queued requests replaying on reconnect.

**Why this priority**: Correctness-critical for a tablet that stays paired for months, but
invisible when working — doesn't block earlier stories' testability.

**Independent Test**: Issue a token artificially close to its rotation threshold, make an
authenticated request, confirm a replacement token is returned and the old one is immediately
invalid, then confirm a batch of requests using the pre-rotation token that were already
in-flight (simulating a reconnect replay) are still accepted.

**Acceptance Scenarios**:

1. **Given** a device token with fewer than 7 days of validity remaining, **When** any
   authenticated request is made, **Then** the response includes a replacement token and the
   app stores it, invisibly to the caregiver.
2. **Given** a tablet reconnecting after an offline period with a token needing rotation,
   **When** the queued offline requests replay, **Then** rotation happens once up front and all
   queued requests succeed under the (still valid at submission time) prior token.
3. **Given** a device token that has fully expired (30 days with no successful rotation),
   **When** any request is made, **Then** it's rejected with a distinguishable
   reactivation-required error, and the tablet returns to the pairing/setup flow.

---

### User Story 7 - Director revokes a lost or stolen tablet (Priority: P2)

A director marks a specific tablet as revoked from the web admin. The next request from that
tablet — regardless of how much of its 30-day token validity remains — is rejected, and the
tablet clears its stored credentials and returns to the initial setup flow.

**Why this priority**: Security-critical but reactive — only matters once a tablet is actually
lost, so it doesn't block earlier stories' day-to-day testability.

**Independent Test**: Pair a tablet, revoke it from the web admin, then confirm the very next
API call from that tablet's token is rejected (not just future ones), and any offline-queued
actions from that tablet are rejected on sync and logged for audit.

**Acceptance Scenarios**:

1. **Given** an active, paired tablet, **When** a director revokes it from the web admin,
   **Then** the very next request from that tablet's device token is rejected, independent of
   the token's remaining validity window.
2. **Given** a revoked device token, **When** the tablet app receives the rejection, **Then**
   it clears all locally stored credentials and cached tenant data and returns to setup.
3. **Given** offline-queued actions from a since-revoked tablet, **When** they attempt to sync,
   **Then** they're rejected and the rejection is logged server-side for director review.

---

### Edge Cases

- What happens when two caregivers check in at the exact same moment? Both succeed — there's
  no artificial single-occupant lock on a room.
- How does the system handle a caregiver who forgets to check out at the end of the day?
  Auto-checkout at a daily cutoff (local midnight) for any shift left open; a director can
  correct the recorded times afterward.
- What happens when a tablet is reassigned to a different group mid-day? A director exits room
  mode via override PIN and re-runs setup; any shifts still open under the tablet's prior room
  assignment are auto-closed at the moment of re-setup.
- How does the system handle a caregiver's account being deactivated while checked in? Their
  open shift is closed immediately, and their PIN is rejected on any further check-in attempt
  from that moment on.
- How does the system handle a caregiver eligible to work at more than one location (feature
  005)? The same PIN works at any location they're eligible for; eligibility is validated at
  check-in time, not cached on the device.
- What happens to offline-queued routine actions from a tablet that gets revoked before it
  reconnects? They're rejected on sync and logged server-side for audit — never silently
  dropped, never silently accepted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a director pair a tablet to a specific location and group via a
  one-time setup step authenticated with the director's own email and password (feature 008's
  auth flow).
- **FR-002**: System MUST issue a device token scoped to the paired tenant/location/group/device
  upon successful pairing, and the tablet MUST store it only in on-device secure storage
  (SecureStore), never general-purpose storage.
- **FR-003**: System MUST require a valid, non-revoked, non-expired device token on every API
  call originating from a paired tablet; requests without one MUST be rejected regardless of
  any other authentication state.
- **FR-004**: A device token's claims MUST be validated against the requested resource's
  tenant, location, *and* group scope on every call — token possession alone MUST NOT grant
  access beyond what the token was actually issued for, including across two groups at the
  same location. Concretely, for any call that names a `staff_id` (check-in, check-out,
  sensitive-action confirmation), the referenced caregiver MUST be eligible at the device
  token's own location — a `staff_id` for a caregiver not eligible there MUST be rejected
  regardless of whether the accompanying PIN would otherwise be correct.
- **FR-005**: System MUST let a director exit room mode via a director-specific override PIN,
  distinct from caregiver check-in PINs, established during pairing. This PIN is deliberately
  *not* part of the shared caregiver-PIN lockout (FR-012) — it has its own, more lenient
  per-device limit (10 incorrect attempts triggers a 30-minute lockout on that device's
  override PIN specifically), reflecting its higher entropy (6 digits) and much lower
  attempt frequency, while still ensuring it is never entirely unprotected against guessing.
- **FR-006**: System MUST let a director assign or reset a 4-digit PIN for a caregiver, stored
  only as a salted hash — never in plaintext, at rest or in transit logs.
- **FR-007**: System MUST enforce PIN uniqueness within a single location — two caregivers
  eligible for the same location MUST NOT be assigned the same PIN.
- **FR-008**: Resetting a caregiver's PIN MUST NOT alter their separate account password
  (feature 003/005).
- **FR-009**: System MUST let a caregiver check in by selecting their own photo card from the
  room home screen's roster and confirming with their PIN (select-then-PIN, not PIN-only — spec
  User Story 3); the client identifies the caregiver explicitly (`staff_id`) and the server
  verifies the submitted PIN against that specific caregiver's stored hash. A correct PIN for a
  not-currently-checked-in caregiver MUST record a check-in timestamp in a server-side shift
  log.
- **FR-010**: System MUST let an already-checked-in caregiver check out the same way — select
  their own (checked-in) card, confirm with their PIN; this MUST record a check-out timestamp
  on their currently-open shift. Whether a given card tap results in a check-in or a check-out
  is determined by that caregiver's current shift state, not by two different keypad modes the
  caregiver has to choose between.
- **FR-011**: System MUST support multiple caregivers simultaneously checked in to the same
  room — this is the expected norm, not an exceptional state.
- **FR-012**: System MUST reject an incorrect PIN with a clear, non-blocking response naming the
  caregiver it was checked against, and MUST rate-limit repeated incorrect attempts for a
  specific caregiver (`staff_id`) using a sliding window: if the 5th failure for that caregiver
  falls within 2 minutes of its 1st failure in the current run of failures, that caregiver's
  check-in/check-out/confirmation ability locks out for 10 minutes (a fixed window that resets
  every 2 minutes would allow an attacker to pace exactly 4 attempts per window indefinitely; a
  sliding window does not). Because every PIN-verifying call now carries an explicit `staff_id`
  (select-then-PIN, User Story 3), this lockout is a straightforward per-caregiver counter, not a
  value-keyed one. It MUST be shared across every surface that validates a caregiver PIN
  (check-in, check-out, and the FR-017 sensitive-action confirmation) — a failed attempt on any
  of them counts toward the same caregiver's lockout, not a separate counter per surface. A
  request made while a caregiver is already locked out MUST return the identical locked-response
  shape (distinct from, and never conflated with, an ordinary incorrect-PIN response) regardless
  of which of the three surfaces made the request, and regardless of whether that specific
  request is what triggered the lockout or arrived after it was already active. A *different*
  caregiver's card is entirely unaffected by another caregiver's lockout — this is what makes
  lockout resilient to one caregiver's simple typo without punishing everyone else in the room.
- **FR-013**: The room home screen MUST show every caregiver eligible at this location as a
  photo card, visually distinguishing those currently checked in (with their check-in time) from
  those not — updated immediately after any check-in/check-out. A caregiver with no profile
  photo on file (feature 005) MUST still show a card with a placeholder avatar, never be omitted
  from the roster.
- **FR-014**: System MUST let an authenticated device (valid device token) submit routine write
  actions without prompting for any additional individual caregiver authentication.
- **FR-015**: System MUST attribute each authenticated write action to whichever caregiver(s)
  were checked in at the action's occurred-at time, and MUST NOT block the action if nobody is
  currently checked in.
- **FR-016**: When exactly one caregiver is checked in at the relevant time, System MUST
  attribute the action to that caregiver alone; when more than one is checked in, System MUST
  record all of them against the action.
- **FR-017**: For a defined subset of sensitive actions, System MUST prompt for administrator
  confirmation before allowing submission, distinct from and in addition to the check-in/
  check-out PIN flow. This uses the same select-then-PIN pattern as check-in/out, narrowed to
  only the currently-checked-in caregivers (typically one or two — spec User Story 5): the
  caregiver taps their own card from that short roster, then confirms with their PIN, sending
  `staff_id` alongside it. This confirmation shares the same per-caregiver rate limit as FR-012
  — it is not a separate, unprotected guessing surface. The server MUST independently reject a
  `staff_id` that does not currently have an open shift (is not checked in), regardless of
  whether the client's UI would normally prevent selecting one — consistent with the
  shift-register model's core premise that every recorded action traces back to someone actually
  present, not merely someone who knows a valid PIN; a caregiver who has already checked out
  cannot be named as the administrator of an action happening after they left.
- **FR-018**: A caregiver MUST be able to skip the sensitive-action PIN confirmation; the action
  MUST still be recorded, with the administering-caregiver field left unset for a director to
  complete retroactively.
- **FR-019**: System MUST NOT transmit or store a caregiver PIN in any recoverable plaintext
  form; all PIN verification MUST happen server-side against a salted hash.
- **FR-020**: System MUST silently rotate a tablet's device token before it nears expiry,
  without requiring caregiver action and without interrupting in-flight or queued offline
  requests using the prior, still-valid token.
- **FR-021**: System MUST let a director revoke a specific tablet's device token; once revoked,
  System MUST reject every subsequent request from that token immediately, independent of the
  token's remaining validity window.
- **FR-022**: Upon receiving a revoked-token or expired-token rejection, the tablet app MUST
  clear all locally stored credentials and cached tenant data, and return to the initial
  pairing/setup flow.
- **FR-023**: System MUST automatically close any shift left open past a defined daily cutoff
  (local midnight), recording that it was system-closed (distinct from an explicit caregiver
  check-out) rather than merely leaving an ambiguous timestamp, and MUST let a director
  manually correct a shift's recorded times afterward. Any such manual correction MUST be
  audit-logged (who corrected it, when, and the prior recorded value) with the same rigor as
  the audit logging FR-021 requires for rejected actions from a revoked device — both are
  after-the-fact record changes a director needs to be able to account for.
- **FR-024**: System MUST close a caregiver's open shift immediately if their account is
  deactivated while checked in, and MUST reject further check-in attempts on that PIN from the
  moment of deactivation.
- **FR-025**: A caregiver eligible to work at multiple locations MUST be able to use the same
  PIN to check in at any location they're eligible for, with eligibility validated at each
  check-in attempt (not cached on-device).
- **FR-026**: Reassigning a paired tablet to a different location or group MUST auto-close any
  shifts still open under the tablet's prior room assignment, at the moment of re-setup.
- **FR-027**: All user-facing strings introduced by this feature MUST be available in Dutch,
  French, and English.
- **FR-028**: The check-in/check-out PIN keypad MUST use touch targets of at least 64pt.
- **FR-029**: Device-token validation MUST happen before, and independently of, any PIN
  validation on the same request — a request with an invalid, missing, expired, or revoked
  device token MUST be rejected for that reason, regardless of whether a PIN was also supplied
  or would have been valid, and the rejection reason returned MUST reflect the device-token
  failure, not a PIN-related one, when both would independently fail.
- **FR-030**: When a device token is both eligible for silent rotation (FR-020) and revoked
  (FR-021) at the time of the same request, revocation MUST take precedence — System MUST NOT
  issue a rotated replacement for a token belonging to a revoked device under any circumstance.

### Key Entities

- **Device (paired tablet)**: The room-scoped hardware identity issued during director setup —
  which tenant/location/group it belongs to, its current device token's issuance/rotation
  state, and whether it has been revoked.
- **Room Shift**: A single caregiver's presence window in a room — which caregiver, which
  location/group, check-in time, and check-out time (null while still open).
- **Caregiver PIN**: A caregiver's short numeric credential (stored as a hash) used for shift
  check-in/out and for sensitive-action administrator confirmation — unique within a location,
  and always verified against a caregiver the client has already identified (`staff_id`,
  select-then-PIN), never searched for.
- **Room roster card**: Not a new stored entity — a read-time projection of every caregiver
  eligible at this location (`StaffProfile` + `StaffLocationEligibility`, feature 005) with
  their existing profile photo (feature 005's `IProfilePhotoStorage`, placeholder if unset) and
  current checked-in state (derived from `Room Shift`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can check in or check out in under 5 seconds from approaching the
  tablet.
- **SC-002**: 100% of routine actions taken on a paired tablet succeed without any individual
  caregiver authentication prompt.
- **SC-003**: 100% of actions taken while at least one caregiver is checked in are attributed
  to a specific caregiver, or to every caregiver simultaneously checked in.
- **SC-004**: A director can revoke a lost tablet and have its very next request rejected,
  without waiting for the token's passive 30-day expiry.
- **SC-005**: Two caregivers can be simultaneously checked in to the same room 100% of the
  time, with neither check-in ever blocking or failing because of the other.
- **SC-006**: Zero caregiver PINs are ever stored or transmitted in a recoverable plaintext
  form.

## Assumptions

- The device token is a second, distinct signing/validation path alongside the existing
  user-session JWT from feature 003 — it is not simply a long-lived user session token, since
  it is not tied to any single caregiver's identity or session lifecycle.
- "Routine event logging" (FR-014–016) has no real UI or domain event type yet in this feature
  — feature 009 (child events) is its first real consumer. This feature proves the contract via
  a synthetic/generic authenticated write action, mirroring how feature 008 proved its sync
  engine via a synthetic `_test_entity` before any real feature registered a handler.
- Sensitive-action PIN confirmation (FR-017–018) likewise has no real medical-event type yet in
  this feature — proven via a synthetic confirm-administrator action for the same reason.
- The daily auto-checkout cutoff (FR-023) is local midnight in the tenant's configured
  timezone.
- `room_shifts` and device-pairing records are tenant-schema-scoped, consistent with the
  existing multi-tenant architecture (constitution Principle I) — no cross-tenant visibility of
  shift data.
- A caregiver's PIN is independent per organisation (tenant) — this feature does not address a
  caregiver working across multiple organisations, which is out of scope for the whole product
  currently.
- **"Web admin" in User Stories 2 and 7 means backend API only, integration-tested — no web UI
  ships in this feature.** No director-facing web UI exists anywhere in this codebase yet
  (`web/` is still the unmodified Habits template); features 005 and 007 both shipped their own
  "web admin" pieces backend-only for the same reason. Feature `007a-web-admin-scaffold`
  (added to BACKLOG.md alongside this spec) is the actual mobile-scaffold-equivalent for the
  web app and is where PIN management and device revocation get a real screen, calling the
  APIs this feature builds.
- Compromise or rotation of the device-JWT *signing key itself* (as opposed to revoking a
  single paired tablet's token, FR-021) is explicitly out of scope for this feature — that is
  an operational/infrastructure incident-response concern (rotating a secret in the deployment
  environment, which would invalidate every currently-issued device token at once), not a
  product requirement this spec defines behavior for.
